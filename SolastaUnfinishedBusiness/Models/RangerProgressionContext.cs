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
internal static class RangerProgressionContext
{
    private static readonly FighterProgressionInitialization Initialization = new();
    internal static bool Active { get; private set; }
    private static readonly CompressedProgressionDefinitions Definitions = new("Ranger12");
    private static Dictionary<FeatureDefinition, string> PoolOrigins => Definitions.PoolOrigins;
    private static HashSet<FeatureDefinitionPower> RangerPowers => Definitions.Powers;
    private static HashSet<FeatureDefinition> WrappedFeatures => Definitions.WrappedFeatures;
    private static readonly HashSet<RulesetCharacterHero> ReportedDeadlocks = new();
    private static string settingsSignature;
    private static readonly HashSet<RulesetCharacter> ReportedUnsupported = new();
    private static readonly HashSet<string> SupportedSubclasses = new();
    private static readonly Dictionary<FeatureDefinition, int> OriginalLevels = new();
    internal static int OriginalUnlockLevel(FeatureDefinition feature) => OriginalLevels.TryGetValue(feature, out var level) ? level : 0;

    private static string SettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(x => x.PropertyType == typeof(bool) || x.PropertyType == typeof(int))
        .Where(x => x.Name.Contains("Ranger") || x.Name.Contains("Rogue") || x.Name.Contains("Monk") || x.Name.Contains("Barbarian") || x.Name.Contains("Fighter") || x.Name.Contains("Feat") || x.Name.Contains("Asi") ||
                    x.Name.Contains("HuntersMark") || x.Name.Contains("WeaponMastery") || x.Name.Contains("Multiclass") || x.Name.Contains("Ritual") ||
                    x.Name is "EnableLevel20" or "MaxAllowedClasses" or "EnableMinInOutAttributes" or
                        "UseAlternateSpellPointsSystem" or "EnablePreparedSpellsTables2024")
        .OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(Main.Settings))) +
        "|Subclasses=" + string.Join(",", DiscoverSubclasses().Select(x => x.Name));

    private static bool restartRequired;
    internal static bool SessionConsistent => !Active || !restartRequired && settingsSignature == SettingsSignature();

    internal static int EffectiveLevel(RulesetCharacter character)
    {
        var actual = character.GetClassLevel(Ranger);
        if (Active && actual > 12 && ReportedUnsupported.Add(character))
            Main.Error("Ranger prototype: existing Ranger above 12 is unsupported. Do not save this hero in compressed mode.");
        return RangerProgressionRules.EffectiveLevel(Active, actual);
    }

    internal static int EffectiveSubclassLevel(RulesetCharacter character, string subclass) =>
        RangerProgressionRules.EffectiveLevel(Active, character.GetSubclassLevel(Ranger, subclass));

    internal static bool HoldsPower(FeatureDefinitionPower power) => RangerPowers.Contains(power);

    internal static bool IsCapped(RulesetCharacterHero hero, CharacterClassDefinition klass) =>
        klass == Ranger && RangerProgressionRules.IsCapped(Active, hero.GetClassLevel(Ranger));

    // Use actual runtime registrations and subclass choices; never turn on hidden/obsolete subclasses.
    internal static IEnumerable<CharacterSubclassDefinition> DiscoverSubclasses()
    {
        var names = new HashSet<string>(Ranger.FeatureUnlocks.SelectMany(x => Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().SelectMany(x => x.Subclasses));
        if (SubclassesContext.KlassListContextTab.TryGetValue(Ranger, out var registered) &&
            Main.Settings.KlassListSubclassEnabled.TryGetValue(Ranger.Name, out var enabled))
        {
            names.UnionWith(registered.AllSubClasses.Where(x => enabled.Contains(x.Name)).Select(x => x.Name));
        }

        return DatabaseRepository.GetDatabase<CharacterSubclassDefinition>()
            .Where(x => names.Contains(x.Name) && !x.GuiPresentation.Hidden).OrderBy(x => x.Name);
    }

    internal static void LateLoad()
    {
        if (!Initialization.ShouldInitialize(Main.Settings.EnableRangerCompressedProgressionPrototype))
        {
            return;
        }

        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass)
        {
            Main.Error("Ranger prototype requires EnableLevel20 and EnableMulticlass. Prototype not activated.");
            return;
        }

        if (!RangerMarkContext.IsReady())
        {
            Main.Error("Ranger prototype: action/invocation adapter not bound. Prototype not activated.");
            return;
        }
        RangerMarkContext.Initialize();
        var subclasses = DiscoverSubclasses().ToArray();
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>
        {
            (Ranger.Name, Ranger.FeatureUnlocks)
        };
        sources.AddRange(subclasses.Select(x => (x.Name, x.FeatureUnlocks)));
        foreach (var source in sources)
        foreach (var unlock in source.Unlocks)
        foreach (var feature in Flatten(unlock.FeatureDefinition))
            if (!OriginalLevels.ContainsKey(feature) || unlock.Level < OriginalLevels[feature]) OriginalLevels[feature] = unlock.Level;
        var prepared = sources.Select(x => (x.Unlocks, Prepared: Transform(
            x.Unlocks.Where(u => x.Name != Ranger.Name || RangerInvocationContext.KeepBaseEvent(u, x.Unlocks))
                .Select((u, i) => new FeatureUnlockByLevel(RangerSpellcastingContext.AdaptAutoPrepared(
                RangerInvocationContext.Adapt(RangerMarkContext.AdaptFeature(u.FeatureDefinition), x.Name, u.Level, i)), u.Level)).ToList(), x.Name))).ToArray();
        RangerSpellcastingContext.Initialize();
        foreach (var item in prepared)
        {
            item.Unlocks.Clear();
            item.Unlocks.AddRange(item.Prepared);
        }

        SupportedSubclasses.UnionWith(subclasses.Select(x => x.Name));
        Main.Info("Ranger prototype discovered subclasses: " + string.Join(", ", SupportedSubclasses.OrderBy(x => x)));

        Main.Info("Ranger selected 2024 rules: " + string.Join(", ", typeof(Settings).GetProperties()
            .Where(x => x.Name.Contains("Ranger") && (x.Name.EndsWith("2024") || x.Name == "EnableRangerSpellCastingAtLevel1" || x.Name == "EnableRangerRelentlessHunter2024AsNoConcentration"))
            .Select(x => x.Name + "=" + x.GetValue(Main.Settings))));
        settingsSignature = SettingsSignature();
        Active = true;
        Main.Info("Ranger 20-to-12 TEST prototype active. New test heroes only; restart after settings changes.");
    }

    private static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature) =>
        CompressedProgressionDefinitions.Flatten(feature);

    private static List<FeatureUnlockByLevel> Transform(List<FeatureUnlockByLevel> target, string owner) =>
        Definitions.Prepare(target, owner);

    internal static bool CanContinue(RulesetCharacterHero hero)
    {
        if (!Active || hero.GetClassLevel(Ranger) == 0)
        {
            return true;
        }

        if (hero.GetClassLevel(Ranger) > 0 && !RangerSpellcastingContext.IsSupportedHero(hero))
        {
            Main.Error("Ranger prototype: mixed casting repertoires are not supported yet. Use a new single-caster test hero.");
            return false;
        }

        if (!SessionConsistent)
        {
            Main.Error("Ranger prototype: progression settings changed during this session. Restart required.");
            return false;
        }

        if (hero.ClassesAndSubclasses.TryGetValue(Ranger, out var subclass) &&
            !SupportedSubclasses.Contains(subclass.Name))
        {
            Main.Error("Ranger prototype: unsupported Ranger subclass; use a new supported test hero.");
            return false;
        }

        if (!RangerCompanionContext.CanAdvance(hero)) return false;
        if (hero.GetClassLevel(Ranger) < 12)
        {
            return true;
        }

        var allowed = new List<CharacterClassDefinition>();
        MulticlassInOutRulesHelper.EnumerateHeroAllowedClassDefinitions(hero, allowed, out _);
        if (allowed.Count != 0 && hero.GetClassLevel(Ranger) == 12)
        {
            return true;
        }

        if (ReportedDeadlocks.Add(hero))
        {
            Main.Error("Ranger prototype: cannot level up. Ranger is capped at 12 and no eligible alternative " +
                       "exists (or this is an unsupported Ranger above 12). Multiclass requirements were NOT changed.");
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
                tag += "Ranger12" + origin;
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

            RangerInvocationContext.Clear(data, tag);
            foreach (var key in data.LevelupTrainedExpertises.Keys.Where(x => x.StartsWith(tag + "Ranger12", StringComparison.Ordinal)).ToArray())
                data.LevelupTrainedExpertises.Remove(key);

            foreach (var stack in data.PointPoolStacks.Values)
            {
                foreach (var key in stack.ActivePools.Keys
                             .Where(x => x.StartsWith(tag + "Ranger12", StringComparison.Ordinal)).ToArray())
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
            if (Active && !__result && __instance.GetClassLevel(Ranger) > 0 && (WrappedFeatures.Contains(featureDefinition) || featureDefinition is FeatureDefinitionPower power && HoldsPower(power)))
            {
                __result = Ranger;
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

            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Ranger 20 to 12 prototype",
                "Level-up blocked: dismiss active Wild Master companion; Ranger cap/multiclass eligibility, unsupported subclass or mixed casting, " +
                "or progression settings changed (restart required). See the UB log for the exact reason. " +
                "Multiclass ability requirements remain in force.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetEffectPower), nameof(RulesetEffectPower.GetClassLevel))]
    internal static class RangerPowerLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetEffectPower __instance, RulesetCharacter character, ref int __result)
        {
            if (Active && character.GetClassLevel(Ranger) > 0 && (RangerPowers.Contains(__instance.PowerDefinition) ||
                           __instance.PowerDefinition.GetFirstSubFeatureOfType<SolastaUnfinishedBusiness.Behaviors.ClassHolder>()?.Class == Ranger))
            {
                __result = EffectiveLevel(character);
            }
        }
    }

    internal static bool CanUseClassMechanics(RulesetCharacter character)
    {
        if (!Active || character == null || character.GetClassLevel(Ranger) == 0) return true;
        if (SessionConsistent && character.GetClassLevel(Ranger) <= 12 &&
            character.GetOriginalHero() is { } hero && RangerSpellcastingContext.IsSupportedHero(hero)) return true;
        Main.Error("Ranger prototype: restart required or unsupported old/mixed-caster hero. Class action rejected before resource mutation.");
        return false;
    }

    private static bool AllowRuleSwitch()
    {
        if (!Active) return true;
        restartRequired = true;
        Main.Info("Ranger prototype: rule selection saved for next launch. Full restart required.");
        return false;
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerSpellCastingAtOne))]
    internal static class SwitchRangerSpellCastingAtOneRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerDeftExplorer))]
    internal static class SwitchRangerDeftExplorerRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerExpertise))]
    internal static class SwitchRangerExpertiseRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerFavoredEnemy))]
    internal static class SwitchRangerFavoredEnemyRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerFeralSenses))]
    internal static class SwitchRangerFeralSensesRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerFoeSlayers))]
    internal static class SwitchRangerFoeSlayersRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerNatureShroud))]
    internal static class SwitchRangerNatureShroudRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerPreciseHunter))]
    internal static class SwitchRangerPreciseHunterRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerRelentlessHunter))]
    internal static class SwitchRangerRelentlessHunterRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerRoving))]
    internal static class SwitchRangerRovingRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerTireless))]
    internal static class SwitchRangerTirelessRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerPrimevalAwareness))]
    internal static class SwitchRangerPrimevalAwarenessRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchRangerAnyFightingStyle))]
    internal static class SwitchRangerAnyFightingStyleRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchOneDndSpellHuntersMark))]
    internal static class SwitchOneDndSpellHuntersMarkRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchOneDndSpellRitualOnAllCasters))]
    internal static class SwitchOneDndSpellRitualOnAllCastersRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchOneDndPreparedSpellsTables))]
    internal static class SwitchOneDndPreparedSpellsTablesRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(FeatsContext), nameof(FeatsContext.SwitchAsiAndFeat))]
    internal static class AsiRestartPatch { internal static bool Prefix() => AllowRuleSwitch(); }
    [HarmonyPatch(typeof(FeatsContext), nameof(FeatsContext.SwitchEveryFourLevelsFeats))]
    internal static class FeatProgressionRestartPatch { internal static bool Prefix() => AllowRuleSwitch(); }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchWeaponMastery))]
    internal static class MasteryRestartPatch { internal static bool Prefix() => AllowRuleSwitch(); }
    [HarmonyPatch(typeof(RulesContext), "SwitchRangerToUseCustomInvocationPools")]
    internal static class InvocationRestartPatch { internal static bool Prefix() => AllowRuleSwitch(); }

}
