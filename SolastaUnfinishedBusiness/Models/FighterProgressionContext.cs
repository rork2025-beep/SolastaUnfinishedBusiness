using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

// Session-latched opt-in. Restart restores the upstream definitions when disabled.
internal static class FighterProgressionContext
{
    private static readonly FighterProgressionInitialization Initialization = new();
    internal static bool Active { get; private set; }
    private static readonly Dictionary<FeatureDefinition, string> PoolOrigins = new();
    private static readonly HashSet<FeatureDefinitionPower> FighterPowers = new();
    private static readonly HashSet<FeatureDefinition> WrappedFeatures = new();
    private static readonly HashSet<RulesetCharacterHero> ReportedDeadlocks = new();
    private static string settingsSignature;

    private static string SettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(x => x.PropertyType == typeof(bool) || x.PropertyType == typeof(int))
        .Where(x => x.Name.Contains("Fighter") || x.Name.Contains("Feat") || x.Name.Contains("Asi") ||
                    x.Name.Contains("WeaponMastery") || x.Name.Contains("Multiclass") ||
                    x.Name is "EnableLevel20" or "MaxAllowedClasses" or "EnableMinInOutAttributes")
        .OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(Main.Settings)));

    internal static int EffectiveLevel(RulesetCharacter character)
    {
        var actual = character.GetClassLevel(Fighter);
        return Active ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    }

    internal static bool IsCapped(RulesetCharacterHero hero, CharacterClassDefinition klass) =>
        Active && klass == Fighter && hero.GetClassLevel(Fighter) >= 12;

    internal static void LateLoad()
    {
        if (!Initialization.ShouldInitialize(Main.Settings.EnableFighterCompressedProgressionPrototype))
        {
            return;
        }

        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass)
        {
            Main.Error("Fighter prototype requires EnableLevel20 and EnableMulticlass. Prototype not activated.");
            return;
        }

        var champion = DatabaseHelper.CharacterSubclassDefinitions.MartialChampion;
        var weaponMaster = DatabaseHelper.GetDefinition<CharacterSubclassDefinition>("MartialWeaponMaster");
        // Validate all inputs before changing any progression.
        var lists = new[] { Fighter.FeatureUnlocks, champion.FeatureUnlocks, weaponMaster.FeatureUnlocks };
        foreach (var list in lists)
        {
            _ = new FighterProgressionPlan<FeatureDefinition>(list.Select(x => (x.FeatureDefinition, x.Level)));
        }

        var prepared = new[]
        {
            Transform(Fighter.FeatureUnlocks, "Fighter"),
            Transform(champion.FeatureUnlocks, "MartialChampion"),
            Transform(weaponMaster.FeatureUnlocks, "MartialWeaponMaster")
        };
        // Commit only once every generated progression has passed validation.
        for (var i = 0; i < lists.Length; i++)
        {
            lists[i].Clear();
            lists[i].AddRange(prepared[i]);
        }

        // Prevent selecting unimplemented Fighter subclasses; do not change saved visibility settings.
        foreach (var choice in Fighter.FeatureUnlocks.SelectMany(x => Flatten(x.FeatureDefinition))
                     .OfType<FeatureDefinitionSubclassChoice>())
        {
            choice.Subclasses.RemoveAll(x => x != champion.Name && x != weaponMaster.Name);
        }

        settingsSignature = SettingsSignature();
        Active = true;
        Main.Info("Fighter 20-to-12 TEST prototype active. New test heroes only; restart after settings changes.");
    }

    private static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature)
    {
        yield return feature;
        if (feature is FeatureDefinitionFeatureSet set)
        {
            foreach (var child in set.FeatureSet.SelectMany(Flatten))
            {
                yield return child;
            }
        }
    }

    private static List<FeatureUnlockByLevel> Transform(List<FeatureUnlockByLevel> target, string owner)
    {
        var plan = new FighterProgressionPlan<FeatureDefinition>(target.Select(x => (x.FeatureDefinition, x.Level)));
        var result = new List<FeatureUnlockByLevel>();
        foreach (var power in target.SelectMany(x => Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPower>())
        {
            FighterPowers.Add(power);
        }

        foreach (var group in plan.Events.GroupBy(x => x.Level))
        {
            // Keep low-level identities intact: UB multiclass proficiency filters compare definitions by identity.
            if (group.Key <= 5)
            {
                result.AddRange(group.Select(x => new FeatureUnlockByLevel(x.Feature, x.Level)));
                continue;
            }

            var wrapper = FeatureDefinitionFeatureSetBuilder.Create($"Fighter12{owner}Level{group.Key}")
                .SetGuiPresentationNoContent(true)
                .SetMode(FeatureDefinitionFeatureSet.FeatureSetMode.Union).AddToDB();
            // Add directly: the builder sorts by title, which would destroy acquisition order.
            foreach (var item in group)
            {
                var origin = $"{owner}Original{item.OriginalLevel}Event{item.OriginalIndex}";
                var cloned = CloneChoices(item.Feature, origin);
                wrapper.FeatureSet.Add(cloned);
                foreach (var feature in Flatten(cloned))
                {
                    WrappedFeatures.Add(feature);
                }
                foreach (var power in Flatten(item.Feature).OfType<FeatureDefinitionPower>())
                {
                    FighterPowers.Add(power);
                }
            }

            result.Add(new FeatureUnlockByLevel(wrapper, group.Key));
        }

        // Validate the actual generated definitions, not only the planning layer.
        string Signature(FeatureDefinition feature) => feature is FeatureDefinitionPointPool pool
            ? $"Pool:{pool.PoolType}:{pool.PoolAmount}"
            : feature.Name;
        var before = plan.Events.SelectMany(x => Flatten(x.Feature))
            .Where(x => x is not FeatureDefinitionFeatureSet).Select(Signature).ToArray();
        var after = result.SelectMany(x => Flatten(x.FeatureDefinition))
            .Where(x => x is not FeatureDefinitionFeatureSet).Select(Signature).ToArray();
        if (!before.SequenceEqual(after))
        {
            throw new InvalidOperationException("Fighter prototype changed reward counts or acquisition order: " + owner);
        }

        Main.Info($"Fighter prototype verified {owner}: {plan.Events.Count} original acquisition events.");
        return result;
    }

    private static FeatureDefinition CloneChoices(FeatureDefinition source, string origin)
    {
        if (source is FeatureDefinitionPointPool pool)
        {
            var clone = FeatureDefinitionPointPoolBuilder.Create(pool, $"Fighter12{origin}{pool.Name}").AddToDB();
            PoolOrigins.Add(clone, origin);
            return clone;
        }

        if (source is not FeatureDefinitionFeatureSet set || !Flatten(set).OfType<FeatureDefinitionPointPool>().Any())
        {
            return source;
        }

        var copy = FeatureDefinitionFeatureSetBuilder.Create(set, $"Fighter12{origin}{set.Name}").AddToDB();
        copy.FeatureSet.Clear();
        for (var i = 0; i < set.FeatureSet.Count; i++)
        {
            copy.FeatureSet.Add(CloneChoices(set.FeatureSet[i], $"{origin}Child{i}"));
        }

        return copy;
    }

    internal static bool CanContinue(RulesetCharacterHero hero)
    {
        if (!Active)
        {
            return true;
        }

        if (settingsSignature != SettingsSignature())
        {
            Main.Error("Fighter prototype: progression settings changed during this session. Restart required.");
            return false;
        }

        if (hero.ClassesAndSubclasses.TryGetValue(Fighter, out var subclass) &&
            subclass.Name is not ("MartialChampion" or "MartialWeaponMaster"))
        {
            Main.Error("Fighter prototype: unsupported Fighter subclass; use a new supported test hero.");
            return false;
        }

        if (hero.GetClassLevel(Fighter) < 12)
        {
            return true;
        }

        var allowed = new List<CharacterClassDefinition>();
        MulticlassInOutRulesHelper.EnumerateHeroAllowedClassDefinitions(hero, allowed, out _);
        if (allowed.Count != 0 && hero.GetClassLevel(Fighter) == 12)
        {
            return true;
        }

        if (ReportedDeadlocks.Add(hero))
        {
            Main.Error("Fighter prototype: cannot level up. Fighter is capped at 12 and no eligible alternative " +
                       "exists (or this is an unsupported Fighter above 12). Multiclass requirements were NOT changed.");
        }

        return false;
    }

    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.ApplyFeatureDefinitionPointPool))]
    internal static class RewardPoolPatch
    {
        internal static void Prefix(FeatureDefinition feature, ref string tag)
        {
            if (Active && PoolOrigins.TryGetValue(feature, out var origin))
            {
                tag += "Fighter12" + origin;
            }
        }
    }

    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.ClearPrevious))]
    internal static class ClearPoolsPatch
    {
        internal static void Prefix(RulesetCharacterHero hero, string tag)
        {
            if (!Active || !hero.TryGetHeroBuildingData(out var data))
            {
                return;
            }

            foreach (var stack in data.PointPoolStacks.Values)
            {
                foreach (var key in stack.ActivePools.Keys
                             .Where(x => x.StartsWith(tag + "Fighter12", StringComparison.Ordinal)).ToArray())
                {
                    stack.ActivePools.Remove(key);
                }
            }
        }
    }

    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.RemoveActiveFeaturesFromHeroByTag))]
    internal static class RemovePoolsPatch
    {
        internal static void Prefix(RulesetCharacterHero hero, string tag) => ClearPoolsPatch.Prefix(hero, tag);
    }

    // Native ownership lookup only checks top-level unlocks. Restore ownership for wrapped features.
    [HarmonyPatch(typeof(RulesetCharacterHero), nameof(RulesetCharacterHero.FindClassHoldingFeature))]
    internal static class FeatureOwnerPatch
    {
        internal static void Postfix(RulesetCharacterHero __instance, FeatureDefinition featureDefinition,
            ref CharacterClassDefinition __result)
        {
            if (Active && !__result && __instance.GetClassLevel(Fighter) > 0 && WrappedFeatures.Contains(featureDefinition))
            {
                __result = Fighter;
            }
        }
    }

    [HarmonyPatch(typeof(RulesetCharacterHero), nameof(RulesetCharacterHero.CanLevelUp), MethodType.Getter)]
    internal static class CanLevelUpPatch
    {
        internal static void Postfix(RulesetCharacterHero __instance, ref bool __result)
        {
            if (__result)
            {
                __result = CanContinue(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(CharacterLevelUpScreen), nameof(CharacterLevelUpScreen.InitiateLevelUpInGame))]
    internal static class BeginLevelUpPatch
    {
        internal static bool Prefix(RulesetCharacterHero hero)
        {
            if (CanContinue(hero))
            {
                return true;
            }

            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Fighter 20 to 12 prototype",
                "Level-up blocked: Fighter cap/multiclass eligibility, unsupported Fighter subclass, " +
                "or progression settings changed (restart required). See the UB log for the exact reason. " +
                "Multiclass ability requirements remain in force.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetEffectPower), nameof(RulesetEffectPower.GetClassLevel))]
    internal static class FighterPowerLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetEffectPower __instance, RulesetCharacter character, ref int __result)
        {
            if (Active && FighterPowers.Contains(__instance.PowerDefinition))
            {
                __result = EffectiveLevel(character);
            }
        }
    }
}
