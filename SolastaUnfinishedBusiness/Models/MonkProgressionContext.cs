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
internal static class MonkProgressionContext
{
    private static readonly FighterProgressionInitialization Initialization = new();
    internal static bool Active { get; private set; }
    private static readonly CompressedProgressionDefinitions Definitions = new("Monk12");
    private static Dictionary<FeatureDefinition, string> PoolOrigins => Definitions.PoolOrigins;
    private static HashSet<FeatureDefinitionPower> MonkPowers => Definitions.Powers;
    private static HashSet<FeatureDefinition> WrappedFeatures => Definitions.WrappedFeatures;
    private static readonly HashSet<RulesetCharacterHero> ReportedDeadlocks = new();
    private static string settingsSignature;
    private static readonly HashSet<string> SupportedSubclasses = new();

    private static string SettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(x => x.PropertyType == typeof(bool) || x.PropertyType == typeof(int))
        .Where(x => x.Name.Contains("Monk") || x.Name.Contains("Barbarian") || x.Name.Contains("Fighter") || x.Name.Contains("Feat") || x.Name.Contains("Asi") ||
                    x.Name.Contains("WeaponMastery") || x.Name.Contains("Multiclass") ||
                    x.Name is "EnableLevel20" or "MaxAllowedClasses" or "EnableMinInOutAttributes")
        .OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(Main.Settings))) +
        "|Subclasses=" + string.Join(",", DiscoverSubclasses().Select(x => x.Name));

    internal static int EffectiveLevel(RulesetCharacter character)
    {
        var actual = character.GetClassLevel(Monk);
        return MonkProgressionRules.EffectiveLevel(Active, actual);
    }

    internal static bool IsCapped(RulesetCharacterHero hero, CharacterClassDefinition klass) =>
        klass == Monk && MonkProgressionRules.IsCapped(Active, hero.GetClassLevel(Monk));

    internal static int ScalingLevel(RulesetCharacter character, CharacterClassDefinition klass) =>
        klass == Monk ? EffectiveLevel(character) : BarbarianProgressionContext.ScalingLevel(character, klass);

    internal static int EffectiveSubclassLevel(RulesetCharacter character, string subclass)
    {
        var actual = character.GetSubclassLevel(Monk, subclass);
        return MonkProgressionRules.EffectiveLevel(Active, actual);
    }

    // Use actual runtime registrations and subclass choices; never turn on hidden/obsolete subclasses.
    internal static IEnumerable<CharacterSubclassDefinition> DiscoverSubclasses()
    {
        var names = new HashSet<string>(Monk.FeatureUnlocks.SelectMany(x => Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().SelectMany(x => x.Subclasses));
        if (SubclassesContext.KlassListContextTab.TryGetValue(Monk, out var registered))
        {
            names.UnionWith(registered.AllSubClasses.Select(x => x.Name));
        }

        return DatabaseRepository.GetDatabase<CharacterSubclassDefinition>()
            .Where(x => names.Contains(x.Name) && !x.GuiPresentation.Hidden && x.Name != "WayOfWealAndWoe").OrderBy(x => x.Name);
    }

    private static FeatureDefinition BuildBodyAndMind()
    {
        var dexterity = FeatureDefinitionAttributeModifierBuilder.Create("Monk12BodyAndMindDexterity")
            .SetGuiPresentationNoContent(true)
            .SetModifier(FeatureDefinitionAttributeModifier.AttributeModifierOperation.Additive,
                AttributeDefinitions.Dexterity, MonkProgressionRules.BodyAndMindBonus).AddToDB();
        var wisdom = FeatureDefinitionAttributeModifierBuilder.Create("Monk12BodyAndMindWisdom")
            .SetGuiPresentationNoContent(true)
            .SetModifier(FeatureDefinitionAttributeModifier.AttributeModifierOperation.Additive,
                AttributeDefinitions.Wisdom, MonkProgressionRules.BodyAndMindBonus).AddToDB();
        return FeatureDefinitionFeatureSetBuilder.Create("Monk12BodyAndMind")
            .SetGuiPresentation("FeatureMonkBodyAndMind", Category.Feature)
            .SetMode(FeatureDefinitionFeatureSet.FeatureSetMode.Union)
            .AddFeatureSet(dexterity, wisdom).AddCustomSubFeatures(new BodyAndMindMaximum()).AddToDB();
    }

    // Removable bonuses leave BaseValue untouched, so respec cannot copy them as original ability scores.
    private sealed class BodyAndMindMaximum : ICustomLevelUpLogic
    {
        private readonly ConditionalWeakTable<RulesetCharacterHero, BodyAndMindGrantState> grants = new();

        public void ApplyFeature(RulesetCharacterHero hero, string tag) =>
            ChangeMaximum(hero, grants.GetValue(hero, _ => new BodyAndMindGrantState()).Apply(tag));

        public void RemoveFeature(RulesetCharacterHero hero, string tag) =>
            ChangeMaximum(hero, grants.GetValue(hero, _ => new BodyAndMindGrantState()).Remove(tag));

        private static void ChangeMaximum(RulesetCharacterHero hero, int delta)
        {
            if (delta == 0)
            {
                return;
            }

            foreach (var name in new[] { AttributeDefinitions.Dexterity, AttributeDefinitions.Wisdom })
            {
                var attribute = hero.GetAttribute(name);
                attribute.MaxValue += delta;
                attribute.MaxEditableValue += delta;
            }

            hero.RefreshAll();
        }
    }

    internal static void LateLoad()
    {
        if (!Initialization.ShouldInitialize(Main.Settings.EnableMonkCompressedProgressionPrototype))
        {
            return;
        }

        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass)
        {
            Main.Error("Monk prototype requires EnableLevel20 and EnableMulticlass. Prototype not activated.");
            return;
        }

        if (!MonkNativeProgressionPatches.IsReady())
        {
            Main.Error("Monk prototype: required native level patches are unavailable. Compression NOT activated.");
            return;
        }

        var subclasses = DiscoverSubclasses().ToArray();
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>
        {
            (Monk.Name, Monk.FeatureUnlocks)
        };
        sources.AddRange(subclasses.Select(x => (x.Name, x.FeatureUnlocks)));
        // Transform the final selected rules variant; never add 2024 Body and Mind to a 2014 progression.
        var originalCapstones = Monk.FeatureUnlocks.Count(x => x.FeatureDefinition.Name == "FeatureMonkBodyAndMind");
        if (originalCapstones != (Main.Settings.EnableMonkBodyAndMind2024 ? 1 : 0))
        {
            throw new InvalidOperationException("Monk prototype: inconsistent Body and Mind rules variant.");
        }

        var capstone = originalCapstones == 1 ? BuildBodyAndMind() : null;
        var prepared = sources.Select(x => (x.Unlocks, Prepared: Transform(x.Unlocks.Select(u =>
            capstone && u.FeatureDefinition.Name == "FeatureMonkBodyAndMind"
                ? new FeatureUnlockByLevel(capstone, u.Level) : u).ToList(), x.Name))).ToArray();
        foreach (var item in prepared)
        {
            item.Unlocks.Clear();
            item.Unlocks.AddRange(item.Prepared);
        }

        SupportedSubclasses.UnionWith(subclasses.Select(x => x.Name));
        Main.Info("Monk prototype discovered subclasses: " + string.Join(", ", SupportedSubclasses.OrderBy(x => x)));

        settingsSignature = SettingsSignature();
        Active = true;
        Main.Info("Monk 20-to-12 TEST prototype active. New test heroes only; restart after settings changes.");
    }

    private static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature) =>
        CompressedProgressionDefinitions.Flatten(feature);

    private static List<FeatureUnlockByLevel> Transform(List<FeatureUnlockByLevel> target, string owner) =>
        Definitions.Prepare(target, owner);

    internal static bool CanContinue(RulesetCharacterHero hero)
    {
        if (!Active)
        {
            return true;
        }

        if (settingsSignature != SettingsSignature())
        {
            Main.Error("Monk prototype: progression settings changed during this session. Restart required.");
            return false;
        }

        if (hero.ClassesAndSubclasses.TryGetValue(Monk, out var subclass) &&
            !SupportedSubclasses.Contains(subclass.Name))
        {
            Main.Error("Monk prototype: unsupported Monk subclass; use a new supported test hero.");
            return false;
        }

        if (hero.GetClassLevel(Monk) < 12)
        {
            return true;
        }

        var allowed = new List<CharacterClassDefinition>();
        MulticlassInOutRulesHelper.EnumerateHeroAllowedClassDefinitions(hero, allowed, out _);
        if (allowed.Count != 0 && hero.GetClassLevel(Monk) == 12)
        {
            return true;
        }

        if (ReportedDeadlocks.Add(hero))
        {
            Main.Error("Monk prototype: cannot level up. Monk is capped at 12 and no eligible alternative " +
                       "exists (or this is an unsupported Monk above 12). Multiclass requirements were NOT changed.");
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
                tag += "Monk12" + origin;
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
                             .Where(x => x.StartsWith(tag + "Monk12", StringComparison.Ordinal)).ToArray())
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
            if (Active && !__result && __instance.GetClassLevel(Monk) > 0 && WrappedFeatures.Contains(featureDefinition))
            {
                __result = Monk;
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

            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Monk 20 to 12 prototype",
                "Level-up blocked: Monk cap/multiclass eligibility, unsupported Monk subclass, " +
                "or progression settings changed (restart required). See the UB log for the exact reason. " +
                "Multiclass ability requirements remain in force.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetEffectPower), nameof(RulesetEffectPower.GetClassLevel))]
    internal static class MonkPowerLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetEffectPower __instance, RulesetCharacter character, ref int __result)
        {
            if (Active && (MonkPowers.Contains(__instance.PowerDefinition) ||
                           __instance.PowerDefinition.GetFirstSubFeatureOfType<SolastaUnfinishedBusiness.Behaviors.ClassHolder>()?.Class == Monk))
            {
                __result = EffectiveLevel(character);
            }
        }
    }
}
