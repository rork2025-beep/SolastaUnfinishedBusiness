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
internal static class WizardProgressionContext
{
    private static readonly FighterProgressionInitialization Initialization = new();
    internal static bool Active { get; private set; }
    private static readonly CompressedProgressionDefinitions Definitions = new("Wizard12");
    private static Dictionary<FeatureDefinition, string> PoolOrigins => Definitions.PoolOrigins;
    private static HashSet<FeatureDefinitionPower> WizardPowers => Definitions.Powers;
    private static HashSet<FeatureDefinition> WrappedFeatures => Definitions.WrappedFeatures;
    private static readonly HashSet<RulesetCharacterHero> ReportedDeadlocks = new();
    private static string settingsSignature;
    private static readonly HashSet<RulesetCharacter> ReportedUnsupported = new();
    private static readonly HashSet<string> SupportedSubclasses = new();

    private static string SettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(x => x.PropertyType == typeof(bool) || x.PropertyType == typeof(int))
        .Where(x => x.Name.Contains("Wizard") || x.Name.Contains("Rogue") || x.Name.Contains("Monk") || x.Name.Contains("Barbarian") || x.Name.Contains("Fighter") || x.Name.Contains("Feat") || x.Name.Contains("Asi") ||
                    x.Name.Contains("WeaponMastery") || x.Name.Contains("Multiclass") ||
                    x.Name is "EnableLevel20" or "MaxAllowedClasses" or "EnableMinInOutAttributes" or
                        "UseAlternateSpellPointsSystem" or "EnablePreparedSpellsTables2024")
        .OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(Main.Settings))) +
        "|Subclasses=" + string.Join(",", DiscoverSubclasses().Select(x => x.Name));

    internal static int EffectiveLevel(RulesetCharacter character)
    {
        var actual = character.GetClassLevel(Wizard);
        if (Active && actual > 12 && ReportedUnsupported.Add(character))
            Main.Error("Wizard prototype: existing Wizard above 12 is unsupported. Do not save this hero in compressed mode.");
        return WizardProgressionRules.EffectiveLevel(Active, actual);
    }

    internal static bool IsCapped(RulesetCharacterHero hero, CharacterClassDefinition klass) =>
        klass == Wizard && WizardProgressionRules.IsCapped(Active, hero.GetClassLevel(Wizard));

    internal static int ScalingLevel(RulesetCharacter character, CharacterClassDefinition klass) =>
        klass == Wizard ? EffectiveLevel(character) : klass == Sorcerer
            ? SorcererProgressionContext.EffectiveLevel(character) : klass == Warlock
                ? WarlockProgressionContext.EffectiveLevel(character) : klass == Bard
                    ? BardProgressionContext.EffectiveLevel(character) : klass == Cleric
                        ? ClericProgressionContext.EffectiveLevel(character) : klass == Druid
                            ? DruidProgressionContext.EffectiveLevel(character) : klass == Paladin
                                ? PaladinProgressionContext.EffectiveLevel(character) : klass == Ranger
                                    ? RangerProgressionContext.EffectiveLevel(character) : RogueProgressionContext.ScalingLevel(character, klass);

    internal static int EffectiveSubclassLevel(RulesetCharacter character, string subclass)
    {
        var actual = character.GetSubclassLevel(Wizard, subclass);
        return WizardProgressionRules.EffectiveLevel(Active, actual);
    }

    // Use actual runtime registrations and subclass choices; never turn on hidden/obsolete subclasses.
    internal static IEnumerable<CharacterSubclassDefinition> DiscoverSubclasses()
    {
        var names = new HashSet<string>(Wizard.FeatureUnlocks.SelectMany(x => Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().SelectMany(x => x.Subclasses));
        if (SubclassesContext.KlassListContextTab.TryGetValue(Wizard, out var registered) &&
            Main.Settings.KlassListSubclassEnabled.TryGetValue(Wizard.Name, out var enabled))
        {
            names.UnionWith(registered.AllSubClasses.Where(x => enabled.Contains(x.Name)).Select(x => x.Name));
        }

        return DatabaseRepository.GetDatabase<CharacterSubclassDefinition>()
            .Where(x => names.Contains(x.Name) && !x.GuiPresentation.Hidden).OrderBy(x => x.Name);
    }

    internal static void LateLoad()
    {
        if (!Initialization.ShouldInitialize(Main.Settings.EnableWizardCompressedProgressionPrototype))
        {
            return;
        }

        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass)
        {
            Main.Error("Wizard prototype requires EnableLevel20 and EnableMulticlass. Prototype not activated.");
            return;
        }

        var subclasses = DiscoverSubclasses().ToArray();
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>
        {
            (Wizard.Name, Wizard.FeatureUnlocks)
        };
        sources.AddRange(subclasses.Select(x => (x.Name, x.FeatureUnlocks)));
        var prepared = sources.Select(x => (x.Unlocks, Prepared: Transform(
            x.Unlocks.Select(u => new FeatureUnlockByLevel(WizardSpellcastingContext.AdaptAutoPrepared(u.FeatureDefinition), u.Level)).ToList(), x.Name))).ToArray();
        WizardSpellcastingContext.Initialize();
        foreach (var item in prepared)
        {
            item.Unlocks.Clear();
            item.Unlocks.AddRange(item.Prepared);
        }

        SupportedSubclasses.UnionWith(subclasses.Select(x => x.Name));
        Main.Info("Wizard prototype discovered subclasses: " + string.Join(", ", SupportedSubclasses.OrderBy(x => x)));

        Main.Info($"Wizard selected rules: School3={Main.Settings.EnableWizardToLearnSchoolAtLevel3}; " +
                  $"Scholar2={Main.Settings.EnableWizardToLearnScholarAtLevel2}; Memorize2024={Main.Settings.EnableWizardMemorizeSpell2024}");
        settingsSignature = SettingsSignature();
        Active = true;
        Main.Info("Wizard 20-to-12 TEST prototype active. New test heroes only; restart after settings changes.");
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

        if (hero.GetClassLevel(Wizard) > 0 && !WizardSpellcastingContext.IsSupportedHero(hero))
        {
            Main.Error("Wizard prototype: mixed casting repertoires are not supported yet. Use a new single-caster test hero.");
            return false;
        }

        if (settingsSignature != SettingsSignature())
        {
            Main.Error("Wizard prototype: progression settings changed during this session. Restart required.");
            return false;
        }

        if (hero.ClassesAndSubclasses.TryGetValue(Wizard, out var subclass) &&
            !SupportedSubclasses.Contains(subclass.Name))
        {
            Main.Error("Wizard prototype: unsupported Wizard subclass; use a new supported test hero.");
            return false;
        }

        if (hero.GetClassLevel(Wizard) < 12)
        {
            return true;
        }

        var allowed = new List<CharacterClassDefinition>();
        MulticlassInOutRulesHelper.EnumerateHeroAllowedClassDefinitions(hero, allowed, out _);
        if (allowed.Count != 0 && hero.GetClassLevel(Wizard) == 12)
        {
            return true;
        }

        if (ReportedDeadlocks.Add(hero))
        {
            Main.Error("Wizard prototype: cannot level up. Wizard is capped at 12 and no eligible alternative " +
                       "exists (or this is an unsupported Wizard above 12). Multiclass requirements were NOT changed.");
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
                tag += "Wizard12" + origin;
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
                             .Where(x => x.StartsWith(tag + "Wizard12", StringComparison.Ordinal)).ToArray())
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
            if (Active && !__result && __instance.GetClassLevel(Wizard) > 0 && WrappedFeatures.Contains(featureDefinition))
            {
                __result = Wizard;
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

            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Wizard 20 to 12 prototype",
                "Level-up blocked: Wizard cap/multiclass eligibility, unsupported Wizard subclass or mixed casting, " +
                "or progression settings changed (restart required). See the UB log for the exact reason. " +
                "Multiclass ability requirements remain in force.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetEffectPower), nameof(RulesetEffectPower.GetClassLevel))]
    internal static class WizardPowerLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetEffectPower __instance, RulesetCharacter character, ref int __result)
        {
            if (Active && (WizardPowers.Contains(__instance.PowerDefinition) ||
                           __instance.PowerDefinition.GetFirstSubFeatureOfType<SolastaUnfinishedBusiness.Behaviors.ClassHolder>()?.Class == Wizard))
            {
                __result = EffectiveLevel(character);
            }
        }
    }
}
