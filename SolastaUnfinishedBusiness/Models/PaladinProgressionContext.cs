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
internal static class PaladinProgressionContext
{
    private static readonly FighterProgressionInitialization Initialization = new();
    internal static bool Active { get; private set; }
    private static readonly CompressedProgressionDefinitions Definitions = new("Paladin12");
    private static Dictionary<FeatureDefinition, string> PoolOrigins => Definitions.PoolOrigins;
    private static HashSet<FeatureDefinitionPower> PaladinPowers => Definitions.Powers;
    private static HashSet<FeatureDefinition> WrappedFeatures => Definitions.WrappedFeatures;
    private static readonly HashSet<RulesetCharacterHero> ReportedDeadlocks = new();
    private static string settingsSignature;
    private static readonly HashSet<RulesetCharacter> ReportedUnsupported = new();
    private static readonly HashSet<string> SupportedSubclasses = new();

    private static string SettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(x => x.PropertyType == typeof(bool) || x.PropertyType == typeof(int))
        .Where(x => x.Name.Contains("Paladin") || x.Name.Contains("Rogue") || x.Name.Contains("Monk") || x.Name.Contains("Barbarian") || x.Name.Contains("Fighter") || x.Name.Contains("Feat") || x.Name.Contains("Asi") ||
                    x.Name.Contains("Smite") || x.Name.Contains("WeaponMastery") || x.Name.Contains("Multiclass") || x.Name.Contains("Ritual") ||
                    x.Name is "EnableLevel20" or "MaxAllowedClasses" or "EnableMinInOutAttributes" or
                        "UseAlternateSpellPointsSystem" or "EnablePreparedSpellsTables2024")
        .OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(Main.Settings))) +
        "|Subclasses=" + string.Join(",", DiscoverSubclasses().Select(x => x.Name));

    internal static bool SessionConsistent => !Active || settingsSignature == SettingsSignature();

    internal static int EffectiveLevel(RulesetCharacter character)
    {
        var actual = character.GetClassLevel(Paladin);
        if (Active && actual > 12 && ReportedUnsupported.Add(character))
            Main.Error("Paladin prototype: existing Paladin above 12 is unsupported. Do not save this hero in compressed mode.");
        return PaladinProgressionRules.EffectiveLevel(Active, actual);
    }

    internal static int EffectiveSubclassLevel(RulesetCharacter character, string subclass) =>
        PaladinProgressionRules.EffectiveLevel(Active, character.GetSubclassLevel(Paladin, subclass));

    internal static bool HoldsPower(FeatureDefinitionPower power) => PaladinPowers.Contains(power);

    internal static bool UseSmite2024 => (Active || RulesCaptured) ? Smite2024 : Main.Settings.EnablePaladinSmite2024;
    private static bool RulesCaptured;
    private static bool Smite2024;
    internal static bool RestoringTouch2024 { get; private set; }
    internal static bool Channel2024 { get; private set; }
    internal static void CaptureRules()
    {
        Smite2024 = Main.Settings.EnablePaladinSmite2024;
        RestoringTouch2024 = Main.Settings.EnablePaladinRestoringTouch2024;
        Channel2024 = Main.Settings.EnablePaladinChannelDivinity2024;
        RulesCaptured = true;
    }
    internal static bool SmiteRule(RulesetCharacter hero) => Active && hero != null && hero.GetClassLevel(Paladin) > 0
        ? UseSmite2024 : Main.Settings.EnablePaladinSmite2024;
    internal static bool RestoringTouchRule(RulesetCharacter hero) => Active && hero != null && hero.GetClassLevel(Paladin) > 0
        ? RestoringTouch2024 : Main.Settings.EnablePaladinRestoringTouch2024;
    internal static bool ChannelRule(RulesetCharacter hero) => Active && hero != null && hero.GetClassLevel(Paladin) > 0
        ? Channel2024 : Main.Settings.EnablePaladinChannelDivinity2024;

    internal static bool IsCapped(RulesetCharacterHero hero, CharacterClassDefinition klass) =>
        klass == Paladin && PaladinProgressionRules.IsCapped(Active, hero.GetClassLevel(Paladin));

    // Use actual runtime registrations and subclass choices; never turn on hidden/obsolete subclasses.
    internal static IEnumerable<CharacterSubclassDefinition> DiscoverSubclasses()
    {
        var names = new HashSet<string>(Paladin.FeatureUnlocks.SelectMany(x => Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().SelectMany(x => x.Subclasses));
        if (SubclassesContext.KlassListContextTab.TryGetValue(Paladin, out var registered) &&
            Main.Settings.KlassListSubclassEnabled.TryGetValue(Paladin.Name, out var enabled))
        {
            names.UnionWith(registered.AllSubClasses.Where(x => enabled.Contains(x.Name)).Select(x => x.Name));
        }

        return DatabaseRepository.GetDatabase<CharacterSubclassDefinition>()
            .Where(x => names.Contains(x.Name) && !x.GuiPresentation.Hidden).OrderBy(x => x.Name);
    }

    internal static void LateLoad()
    {
        if (!Initialization.ShouldInitialize(Main.Settings.EnablePaladinCompressedProgressionPrototype))
        {
            return;
        }

        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass)
        {
            Main.Error("Paladin prototype requires EnableLevel20 and EnableMulticlass. Prototype not activated.");
            return;
        }

        CaptureRules();
        PaladinSpellcastingContext.CaptureRules();
        var subclasses = DiscoverSubclasses().ToArray();
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>
        {
            (Paladin.Name, Paladin.FeatureUnlocks)
        };
        sources.AddRange(subclasses.Select(x => (x.Name, x.FeatureUnlocks)));
        var prepared = sources.Select(x => (x.Unlocks, Prepared: Transform(
            x.Unlocks.Where(u => x.Name != Paladin.Name || !UseSmite2024 ||
                u.FeatureDefinition != DatabaseHelper.FeatureDefinitionAdditionalDamages.AdditionalDamagePaladinDivineSmite)
                .Select(u => new FeatureUnlockByLevel(PaladinSpellcastingContext.AdaptAutoPrepared(u.FeatureDefinition), u.Level)).ToList(), x.Name))).ToArray();
        PaladinSpellcastingContext.Initialize();
        foreach (var item in prepared)
        {
            item.Unlocks.Clear();
            item.Unlocks.AddRange(item.Prepared);
        }

        SupportedSubclasses.UnionWith(subclasses.Select(x => x.Name));
        Main.Info("Paladin prototype discovered subclasses: " + string.Join(", ", SupportedSubclasses.OrderBy(x => x)));

        Main.Info("Paladin selected 2024 rules: " + string.Join(", ", typeof(Settings).GetProperties()
            .Where(x => x.Name.Contains("Paladin") && (x.Name.EndsWith("2024") || x.Name == "EnablePaladinSpellCastingAtLevel1" || x.Name == "EnablePaladinRechargeLv20Feature"))
            .Select(x => x.Name + "=" + x.GetValue(Main.Settings))));
        settingsSignature = SettingsSignature();
        Active = true;
        Main.Info("Paladin 20-to-12 TEST prototype active. New test heroes only; restart after settings changes.");
    }

    private static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature) =>
        CompressedProgressionDefinitions.Flatten(feature);

    private static List<FeatureUnlockByLevel> Transform(List<FeatureUnlockByLevel> target, string owner) =>
        Definitions.Prepare(target, owner);

    internal static bool CanContinue(RulesetCharacterHero hero)
    {
        if (!Active || hero.GetClassLevel(Paladin) == 0)
        {
            return true;
        }

        if (hero.GetClassLevel(Paladin) > 0 && !PaladinSpellcastingContext.IsSupportedHero(hero))
        {
            Main.Error("Paladin prototype: mixed casting repertoires are not supported yet. Use a new single-caster test hero.");
            return false;
        }

        if (settingsSignature != SettingsSignature())
        {
            Main.Error("Paladin prototype: progression settings changed during this session. Restart required.");
            return false;
        }

        if (hero.ClassesAndSubclasses.TryGetValue(Paladin, out var subclass) &&
            !SupportedSubclasses.Contains(subclass.Name))
        {
            Main.Error("Paladin prototype: unsupported Paladin subclass; use a new supported test hero.");
            return false;
        }

        if (hero.GetClassLevel(Paladin) < 12)
        {
            return true;
        }

        var allowed = new List<CharacterClassDefinition>();
        MulticlassInOutRulesHelper.EnumerateHeroAllowedClassDefinitions(hero, allowed, out _);
        if (allowed.Count != 0 && hero.GetClassLevel(Paladin) == 12)
        {
            return true;
        }

        if (ReportedDeadlocks.Add(hero))
        {
            Main.Error("Paladin prototype: cannot level up. Paladin is capped at 12 and no eligible alternative " +
                       "exists (or this is an unsupported Paladin above 12). Multiclass requirements were NOT changed.");
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
                tag += "Paladin12" + origin;
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

            foreach (var key in data.LevelupTrainedExpertises.Keys.Where(x => x.StartsWith(tag + "Paladin12", StringComparison.Ordinal)).ToArray())
                data.LevelupTrainedExpertises.Remove(key);

            foreach (var stack in data.PointPoolStacks.Values)
            {
                foreach (var key in stack.ActivePools.Keys
                             .Where(x => x.StartsWith(tag + "Paladin12", StringComparison.Ordinal)).ToArray())
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
            if (Active && !__result && __instance.GetClassLevel(Paladin) > 0 && (WrappedFeatures.Contains(featureDefinition) || featureDefinition is FeatureDefinitionPower power && HoldsPower(power)))
            {
                __result = Paladin;
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

            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Paladin 20 to 12 prototype",
                "Level-up blocked: Paladin cap/multiclass eligibility, unsupported Paladin subclass or mixed casting, " +
                "or progression settings changed (restart required). See the UB log for the exact reason. " +
                "Multiclass ability requirements remain in force.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetEffectPower), nameof(RulesetEffectPower.GetClassLevel))]
    internal static class PaladinPowerLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetEffectPower __instance, RulesetCharacter character, ref int __result)
        {
            if (Active && character.GetClassLevel(Paladin) > 0 && (PaladinPowers.Contains(__instance.PowerDefinition) ||
                           __instance.PowerDefinition.GetFirstSubFeatureOfType<SolastaUnfinishedBusiness.Behaviors.ClassHolder>()?.Class == Paladin))
            {
                __result = EffectiveLevel(character);
            }
        }
    }

    internal static bool CanUseClassMechanics(RulesetCharacter character)
    {
        if (!Active || character == null || character.GetClassLevel(Paladin) == 0) return true;
        if (SessionConsistent && character.GetClassLevel(Paladin) <= 12 &&
            character.GetOriginalHero() is { } hero && PaladinSpellcastingContext.IsSupportedHero(hero)) return true;
        Main.Error("Paladin prototype: restart required or unsupported old/mixed-caster hero. Class action rejected before resource mutation.");
        return false;
    }

    [HarmonyPatch(typeof(SmiteSpells2024Context), nameof(SmiteSpells2024Context.SwitchSmiteSpells))]
    internal static class SmiteSpellSwitchRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchOneDndSpellRitualOnAllCasters))]
    internal static class RitualSwitchRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }

    private static bool AllowRuleSwitch()
    {
        if (!Active) return true;
        Main.Info("Paladin prototype: rule selection saved for next launch. Full restart required.");
        return false;
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchPaladinRechargeLv20Power))]
    internal static class SwitchPaladinRechargeLv20PowerRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchPaladinDivineSmite))]
    internal static class SwitchPaladinDivineSmiteRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchPaladinSpellCastingAtOne))]
    internal static class SwitchPaladinSpellCastingAtOneRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchPaladinAbjureFoes))]
    internal static class SwitchPaladinAbjureFoesRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchPaladinChannelDivinity))]
    internal static class SwitchPaladinChannelDivinityRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchPaladinLayOnHand))]
    internal static class SwitchPaladinLayOnHandRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchPaladinRestoringTouch))]
    internal static class SwitchPaladinRestoringTouchRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchPaladinAnyFightingStyle))]
    internal static class SwitchPaladinAnyFightingStyleRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
}
