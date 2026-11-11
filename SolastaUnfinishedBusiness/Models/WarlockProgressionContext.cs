using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.Interfaces;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

// Session-latched opt-in. Restart restores the upstream definitions when disabled.
internal static class WarlockProgressionContext
{
    private static readonly FighterProgressionInitialization Initialization = new();
    internal static bool Active { get; private set; }
    private static readonly CompressedProgressionDefinitions Definitions = new("Warlock12");
    private static Dictionary<FeatureDefinition, string> PoolOrigins => Definitions.PoolOrigins;
    private static HashSet<FeatureDefinitionPower> WarlockPowers => Definitions.Powers;
    private static HashSet<FeatureDefinition> WrappedFeatures => Definitions.WrappedFeatures;
    private static readonly HashSet<RulesetCharacterHero> ReportedDeadlocks = new();
    private static string settingsSignature;
    private static readonly Dictionary<FeatureDefinitionCustomInvocationPool, int> CustomOrigins = new();
    private static readonly HashSet<RulesetCharacter> ReportedUnsupported = new();
    private static readonly HashSet<string> SupportedSubclasses = new();

    private static string SettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(x => x.PropertyType == typeof(bool) || x.PropertyType == typeof(int))
        .Where(x => x.Name.Contains("Warlock") || x.Name.Contains("Rogue") || x.Name.Contains("Monk") || x.Name.Contains("Barbarian") || x.Name.Contains("Fighter") || x.Name.Contains("Feat") || x.Name.Contains("Asi") ||
                    x.Name.Contains("WeaponMastery") || x.Name.Contains("Multiclass") ||
                    x.Name is "EnableLevel20" or "MaxAllowedClasses" or "EnableMinInOutAttributes" or
                        "UseAlternateSpellPointsSystem" or "EnablePreparedSpellsTables2024")
        .OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(Main.Settings))) +
        "|Subclasses=" + string.Join(",", DiscoverSubclasses().Select(x => x.Name));

    internal static int EffectiveLevel(RulesetCharacter character)
    {
        var actual = character.GetClassLevel(Warlock);
        if (Active && actual > 12 && ReportedUnsupported.Add(character))
            Main.Error("Warlock prototype: existing Warlock above 12 is unsupported. Do not save this hero in compressed mode.");
        return WarlockProgressionRules.EffectiveLevel(Active, actual);
    }

    internal static bool IsCapped(RulesetCharacterHero hero, CharacterClassDefinition klass) =>
        klass == Warlock && WarlockProgressionRules.IsCapped(Active, hero.GetClassLevel(Warlock));

    internal static int ScalingLevel(RulesetCharacter character, CharacterClassDefinition klass) =>
        klass == Warlock ? EffectiveLevel(character) : WizardProgressionContext.ScalingLevel(character, klass);

    internal static int EffectiveSubclassLevel(RulesetCharacter character, string subclass)
    {
        var actual = character.GetSubclassLevel(Warlock, subclass);
        return WarlockProgressionRules.EffectiveLevel(Active, actual);
    }

    // Use actual runtime registrations and subclass choices; never turn on hidden/obsolete subclasses.
    internal static IEnumerable<CharacterSubclassDefinition> DiscoverSubclasses()
    {
        var names = new HashSet<string>(Warlock.FeatureUnlocks.SelectMany(x => Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().SelectMany(x => x.Subclasses));
        if (SubclassesContext.KlassListContextTab.TryGetValue(Warlock, out var registered) &&
            Main.Settings.KlassListSubclassEnabled.TryGetValue(Warlock.Name, out var enabled))
        {
            names.UnionWith(registered.AllSubClasses.Where(x => enabled.Contains(x.Name)).Select(x => x.Name));
        }

        return DatabaseRepository.GetDatabase<CharacterSubclassDefinition>()
            .Where(x => names.Contains(x.Name) && !x.GuiPresentation.Hidden).OrderBy(x => x.Name);
    }

    internal static void LateLoad()
    {
        if (!Initialization.ShouldInitialize(Main.Settings.EnableWarlockCompressedProgressionPrototype))
        {
            return;
        }

        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass)
        {
            Main.Error("Warlock prototype requires EnableLevel20 and EnableMulticlass. Prototype not activated.");
            return;
        }

        var subclasses = DiscoverSubclasses().ToArray();
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>
        {
            (Warlock.Name, Warlock.FeatureUnlocks)
        };
        sources.AddRange(subclasses.Select(x => (x.Name, x.FeatureUnlocks)));
        var prepared = sources.Select(x => (x.Unlocks, Prepared: Transform(
            x.Unlocks.Select((u, i) => new FeatureUnlockByLevel(PrepareCustomChoices(
                WarlockSpellcastingContext.AdaptAutoPrepared(u.FeatureDefinition), x.Name, u.Level, i.ToString()), u.Level)).ToList(), x.Name))).ToArray();
        WarlockInvocationContext.Initialize(Warlock.FeatureUnlocks);
        WarlockSpellcastingContext.Initialize();
        foreach (var item in prepared)
        {
            item.Unlocks.Clear();
            item.Unlocks.AddRange(item.Prepared);
        }

        foreach (var invocation in DatabaseRepository.GetDatabase<InvocationDefinition>()
                     .Where(x => x is not InvocationDefinitionCustom && !x.GuiPresentation.Hidden))
            if (invocation.GrantedFeature != null)
                WarlockPowers.UnionWith(Flatten(invocation.GrantedFeature).OfType<FeatureDefinitionPower>());
        SupportedSubclasses.UnionWith(subclasses.Select(x => x.Name));
        Main.Info("Warlock prototype discovered subclasses: " + string.Join(", ", SupportedSubclasses.OrderBy(x => x)));

        Main.Info("Warlock selected 2024 rules: " + string.Join(", ", typeof(Settings).GetProperties()
            .Where(x => x.Name.StartsWith("EnableWarlock") && x.Name.EndsWith("2024"))
            .Select(x => x.Name + "=" + x.GetValue(Main.Settings))));
        settingsSignature = SettingsSignature();
        Active = true;
        Main.Info("Warlock 20-to-12 TEST prototype active. New test heroes only; restart after settings changes.");
    }

    private static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature) =>
        CompressedProgressionDefinitions.Flatten(feature);

    private static List<FeatureUnlockByLevel> Transform(List<FeatureUnlockByLevel> target, string owner) =>
        Definitions.Prepare(target, owner);

    private static FeatureDefinition PrepareCustomChoices(FeatureDefinition source, string owner, int level, string index)
    {
        if (level <= 5) return source;
        if (source is FeatureDefinitionCustomInvocationPool custom)
        {
            var copy = CustomInvocationPoolDefinitionBuilder.Create(custom, $"Warlock12{owner}Original{level}Event{index}{custom.Name}")
                .Setup(custom.PoolType, custom.Points, custom.IsUnlearn).AddToDB();
            CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(custom, copy);
            CustomOrigins.Add(copy, level); return copy;
        }
        if (source is not FeatureDefinitionFeatureSet set || !Flatten(set).OfType<FeatureDefinitionCustomInvocationPool>().Any()) return source;
        var result = FeatureDefinitionFeatureSetBuilder.Create(set, $"Warlock12Custom{owner}{level}Event{index}{set.Name}").AddToDB();
        result.FeatureSet.Clear();
        for (var i = 0; i < set.FeatureSet.Count; i++)
            result.FeatureSet.Add(PrepareCustomChoices(set.FeatureSet[i], owner, level, index + "Child" + i));
        CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(set, result); return result;
    }

    internal static void AssignCustomPoolOrigins(List<(string, FeatureDefinitionCustomInvocationPool)> pools)
    {
        if (!Active) return;
        for (var i = 0; i < pools.Count; i++)
            if (CustomOrigins.TryGetValue(pools[i].Item2, out var original))
                pools[i] = (pools[i].Item1 + "Warlock12Original" + original.ToString("D2"), pools[i].Item2);
    }

    internal static void RecoverPactSlots(RulesetCharacterHero hero)
    {
        if (!Active || !WarlockSpellcastingContext.IsSupportedHero(hero)) return;
        var repertoire = SharedSpellsContext.GetWarlockSpellRepertoire(hero);
        if (repertoire == null) return;
        var circle = SharedSpellsContext.GetWarlockSpellLevel(hero);
        if (!repertoire.usedSpellsSlots.TryGetValue(circle, out var spent) || spent <= 0) return;
        repertoire.usedSpellsSlots[circle] = 0;
        repertoire.RepertoireRefreshed?.Invoke(repertoire);
    }

    internal static bool CanContinue(RulesetCharacterHero hero)
    {
        if (!Active)
        {
            return true;
        }

        if (hero.GetClassLevel(Warlock) > 0 && !WarlockSpellcastingContext.IsSupportedHero(hero))
        {
            Main.Error("Warlock prototype: mixed casting repertoires are not supported yet. Use a new single-caster test hero.");
            return false;
        }

        if (settingsSignature != SettingsSignature())
        {
            Main.Error("Warlock prototype: progression settings changed during this session. Restart required.");
            return false;
        }

        if (hero.ClassesAndSubclasses.TryGetValue(Warlock, out var subclass) &&
            !SupportedSubclasses.Contains(subclass.Name))
        {
            Main.Error("Warlock prototype: unsupported Warlock subclass; use a new supported test hero.");
            return false;
        }

        if (hero.GetClassLevel(Warlock) < 12)
        {
            return true;
        }

        var allowed = new List<CharacterClassDefinition>();
        MulticlassInOutRulesHelper.EnumerateHeroAllowedClassDefinitions(hero, allowed, out _);
        if (allowed.Count != 0 && hero.GetClassLevel(Warlock) == 12)
        {
            return true;
        }

        if (ReportedDeadlocks.Add(hero))
        {
            Main.Error("Warlock prototype: cannot level up. Warlock is capped at 12 and no eligible alternative " +
                       "exists (or this is an unsupported Warlock above 12). Multiclass requirements were NOT changed.");
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
                tag += "Warlock12" + origin;
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
                             .Where(x => x.StartsWith(tag + "Warlock12", StringComparison.Ordinal)).ToArray())
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
            if (Active && !__result && __instance.GetClassLevel(Warlock) > 0 && WrappedFeatures.Contains(featureDefinition))
            {
                __result = Warlock;
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

            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Warlock 20 to 12 prototype",
                "Level-up blocked: Warlock cap/multiclass eligibility, unsupported Warlock subclass or mixed casting, " +
                "or progression settings changed (restart required). See the UB log for the exact reason. " +
                "Multiclass ability requirements remain in force.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetEffectPower), nameof(RulesetEffectPower.GetClassLevel))]
    internal static class WarlockPowerLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetEffectPower __instance, RulesetCharacter character, ref int __result)
        {
            if (Active && character.GetClassLevel(Warlock) > 0 &&
                (WarlockPowers.Contains(__instance.PowerDefinition) ||
                           __instance.PowerDefinition.GetFirstSubFeatureOfType<SolastaUnfinishedBusiness.Behaviors.ClassHolder>()?.Class == Warlock))
            {
                __result = EffectiveLevel(character);
            }
        }
    }
}
