using System;
using System.Collections.Generic;
using System.Linq;
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
internal static class ClericProgressionContext
{
    private static readonly FighterProgressionInitialization Initialization = new();
    internal static bool Active { get; private set; }
    private static readonly CompressedProgressionDefinitions Definitions = new("Cleric12");
    private static Dictionary<FeatureDefinition, string> PoolOrigins => Definitions.PoolOrigins;
    private static HashSet<FeatureDefinitionPower> ClericPowers => Definitions.Powers;
    private static HashSet<FeatureDefinition> WrappedFeatures => Definitions.WrappedFeatures;
    private static readonly HashSet<RulesetCharacterHero> ReportedDeadlocks = new();
    private static string settingsSignature;
    private static readonly HashSet<RulesetCharacter> ReportedUnsupported = new();
    private static readonly HashSet<string> SupportedSubclasses = new();

    private static string SettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(x => x.PropertyType == typeof(bool) || x.PropertyType == typeof(int))
        .Where(x => x.Name.Contains("Cleric") || x.Name.Contains("Rogue") || x.Name.Contains("Monk") || x.Name.Contains("Barbarian") || x.Name.Contains("Fighter") || x.Name.Contains("Feat") || x.Name.Contains("Asi") ||
                    x.Name.Contains("WeaponMastery") || x.Name.Contains("Multiclass") || x.Name.Contains("Ritual") ||
                    x.Name is "EnableLevel20" or "MaxAllowedClasses" or "EnableMinInOutAttributes" or
                        "UseAlternateSpellPointsSystem" or "EnablePreparedSpellsTables2024")
        .OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(Main.Settings))) +
        "|Subclasses=" + string.Join(",", DiscoverSubclasses().Select(x => x.Name));

    internal static bool SessionConsistent => !Active || settingsSignature == SettingsSignature();

    internal static int EffectiveLevel(RulesetCharacter character)
    {
        var actual = character.GetClassLevel(Cleric);
        if (Active && actual > 12 && ReportedUnsupported.Add(character))
            Main.Error("Cleric prototype: existing Cleric above 12 is unsupported. Do not save this hero in compressed mode.");
        return ClericProgressionRules.EffectiveLevel(Active, actual);
    }

    // The upstream global OR suppresses legacy Cleric short-rest recovery when only Paladin2024 is on.
    internal static bool PreserveChannelUsesOnShortRest(RulesetCharacter character) =>
        ClericProgressionRules.Use2024ChannelRecovery(Active && character.GetClassLevel(Cleric) > 0,
            Main.Settings.EnableClericChannelDivinity2024, Main.Settings.EnablePaladinChannelDivinity2024);

    internal static bool IsCapped(RulesetCharacterHero hero, CharacterClassDefinition klass) =>
        klass == Cleric && ClericProgressionRules.IsCapped(Active, hero.GetClassLevel(Cleric));

    // Use actual runtime registrations and subclass choices; never turn on hidden/obsolete subclasses.
    internal static IEnumerable<CharacterSubclassDefinition> DiscoverSubclasses()
    {
        // Cleric uses FilterByDeity: the official choice.Subclasses list is empty.
        var names = new HashSet<string>(DatabaseRepository.GetDatabase<DeityDefinition>()
            .Where(x => x.SelectableByPlayer && !x.GuiPresentation.Hidden).SelectMany(x => x.Subclasses));
        names.UnionWith(Cleric.FeatureUnlocks.SelectMany(x => Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().SelectMany(x => x.Subclasses));
        if (SubclassesContext.KlassListContextTab.TryGetValue(Cleric, out var registered) &&
            Main.Settings.KlassListSubclassEnabled.TryGetValue(Cleric.Name, out var enabled))
        {
            names.UnionWith(registered.AllSubClasses.Where(x => enabled.Contains(x.Name)).Select(x => x.Name));
        }

        return DatabaseRepository.GetDatabase<CharacterSubclassDefinition>()
            .Where(x => names.Contains(x.Name) && !x.GuiPresentation.Hidden).OrderBy(x => x.Name);
    }

    internal static void LateLoad()
    {
        if (!Initialization.ShouldInitialize(Main.Settings.EnableClericCompressedProgressionPrototype))
        {
            return;
        }

        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass)
        {
            Main.Error("Cleric prototype requires EnableLevel20 and EnableMulticlass. Prototype not activated.");
            return;
        }

        if (!ClericNativeProgressionPatches.IsReady())
        {
            Main.Error("Cleric prototype: native intervention adapter not verified. Prototype not activated.");
            return;
        }
        Main.Info("Cleric prototype: Divine Intervention2024 setting, UI and implementation are excluded from this UB build; installed legacy effects are retained.");
        // The upstream Sear switch omits the original8 Destroy upgrade from its removal list.
        // In opt-in Sear mode only, remove this residual incompatible legacy acquisition.
        if (Main.Settings.EnableClericSearUndead2024)
            Main.Info("Cleric prototype: Sear mode excludes residual TurnUndead8 legacy acquisition.");
        var subclasses = DiscoverSubclasses().ToArray();
        if (subclasses.Any(x => x.Name == "DomainOrder" && !x.FeatureUnlocks.Any(u => u.Level == 10 &&
                u.FeatureDefinition is FeatureDefinitionPower power && power.Name.StartsWith("PowerClericDivineIntervention", StringComparison.Ordinal))))
            Main.Error("Cleric prototype: installed DomainOrder lacks original10 Divine Intervention. Its original20 improvement is retained; this domain's Intervention progression is incomplete.");
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>
        {
            (Cleric.Name, Cleric.FeatureUnlocks)
        };
        sources.AddRange(subclasses.Select(x => (x.Name, x.FeatureUnlocks)));
        var prepared = sources.Select(x => (x.Unlocks, Prepared: Transform(
            x.Unlocks.Where(u => x.Name != Cleric.Name || !Main.Settings.EnableClericSearUndead2024 ||
                                 u.FeatureDefinition != DatabaseHelper.GetDefinition<FeatureDefinitionPower>("PowerClericTurnUndead8"))
                .Select(u => new FeatureUnlockByLevel(ClericSpellcastingContext.AdaptAutoPrepared(u.FeatureDefinition), u.Level)).ToList(), x.Name))).ToArray();
        ClericSpellcastingContext.Initialize();
        foreach (var item in prepared)
        {
            item.Unlocks.Clear();
            item.Unlocks.AddRange(item.Prepared);
        }

        SupportedSubclasses.UnionWith(subclasses.Select(x => x.Name));
        Main.Info("Cleric prototype discovered subclasses: " + string.Join(", ", SupportedSubclasses.OrderBy(x => x)));

        Main.Info("Cleric selected 2024 rules: " + string.Join(", ", typeof(Settings).GetProperties()
            .Where(x => x.Name.Contains("Cleric") && (x.Name.EndsWith("2024") || x.Name == "EnableClericToLearnDomainAtLevel3"))
            .Select(x => x.Name + "=" + x.GetValue(Main.Settings))));
        settingsSignature = SettingsSignature();
        Active = true;
        Main.Info("Cleric 20-to-12 TEST prototype active. New test heroes only; restart after settings changes.");
    }

    private static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature) =>
        CompressedProgressionDefinitions.Flatten(feature);

    private static List<FeatureUnlockByLevel> Transform(List<FeatureUnlockByLevel> target, string owner) =>
        Definitions.Prepare(target, owner);

    internal static bool CanContinue(RulesetCharacterHero hero)
    {
        if (!Active || hero.GetClassLevel(Cleric) == 0)
        {
            return true;
        }

        if (hero.GetClassLevel(Cleric) > 0 && !ClericSpellcastingContext.IsSupportedHero(hero))
        {
            Main.Error("Cleric prototype: mixed casting repertoires are not supported yet. Use a new single-caster test hero.");
            return false;
        }

        if (settingsSignature != SettingsSignature())
        {
            Main.Error("Cleric prototype: progression settings changed during this session. Restart required.");
            return false;
        }

        if (hero.ClassesAndSubclasses.TryGetValue(Cleric, out var subclass) &&
            !SupportedSubclasses.Contains(subclass.Name))
        {
            Main.Error("Cleric prototype: unsupported Cleric subclass; use a new supported test hero.");
            return false;
        }

        if (hero.GetClassLevel(Cleric) < 12)
        {
            return true;
        }

        var allowed = new List<CharacterClassDefinition>();
        MulticlassInOutRulesHelper.EnumerateHeroAllowedClassDefinitions(hero, allowed, out _);
        if (allowed.Count != 0 && hero.GetClassLevel(Cleric) == 12)
        {
            return true;
        }

        if (ReportedDeadlocks.Add(hero))
        {
            Main.Error("Cleric prototype: cannot level up. Cleric is capped at 12 and no eligible alternative " +
                       "exists (or this is an unsupported Cleric above 12). Multiclass requirements were NOT changed.");
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
                tag += "Cleric12" + origin;
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

            foreach (var key in data.LevelupTrainedExpertises.Keys.Where(x => x.StartsWith(tag + "Cleric12", StringComparison.Ordinal)).ToArray())
                data.LevelupTrainedExpertises.Remove(key);

            foreach (var stack in data.PointPoolStacks.Values)
            {
                foreach (var key in stack.ActivePools.Keys
                             .Where(x => x.StartsWith(tag + "Cleric12", StringComparison.Ordinal)).ToArray())
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
            if (Active && !__result && __instance.GetClassLevel(Cleric) > 0 && WrappedFeatures.Contains(featureDefinition))
            {
                __result = Cleric;
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

            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Cleric 20 to 12 prototype",
                "Level-up blocked: Cleric cap/multiclass eligibility, unsupported Cleric subclass or mixed casting, " +
                "or progression settings changed (restart required). See the UB log for the exact reason. " +
                "Multiclass ability requirements remain in force.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetEffectPower), nameof(RulesetEffectPower.GetClassLevel))]
    internal static class ClericPowerLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetEffectPower __instance, RulesetCharacter character, ref int __result)
        {
            if (Active && character.GetClassLevel(Cleric) > 0 && (ClericPowers.Contains(__instance.PowerDefinition) ||
                           __instance.PowerDefinition.GetFirstSubFeatureOfType<SolastaUnfinishedBusiness.Behaviors.ClassHolder>()?.Class == Cleric))
            {
                __result = EffectiveLevel(character);
            }
        }
    }

    // Definition switches are deferred until the next full launch once compression is active.
    private static bool AllowRuleSwitch()
    {
        if (!Active) return true;
        Main.Info("Cleric prototype: rule selection saved for next launch. Full game restart required.");
        return false;
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchClericBlessedStrikes))]
    internal static class SwitchClericBlessedStrikesRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchClericChannelDivinity))]
    internal static class SwitchClericChannelDivinityRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchClericDivineOrder))]
    internal static class SwitchClericDivineOrderRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchClericDomainLearningLevel))]
    internal static class SwitchClericDomainLearningLevelRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchClericSearUndead))]
    internal static class SwitchClericSearUndeadRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
}
