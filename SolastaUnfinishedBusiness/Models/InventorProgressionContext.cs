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
using static SolastaUnfinishedBusiness.Classes.InventorClass;

namespace SolastaUnfinishedBusiness.Models;

// Session-latched opt-in. Restart restores the upstream definitions when disabled.
internal static class InventorProgressionContext
{
    private static readonly FighterProgressionInitialization Initialization = new();
    internal static bool Active { get; private set; }
    private static FeatureDefinition newHeroMarker;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RulesetCharacterHero, object> FreshHeroes = new();
    internal static void RegisterNewHero(RulesetCharacterHero hero)
    {
        if (Active && hero != null && hero.ClassesHistory.Count == 0) FreshHeroes.GetValue(hero, _ => new object());
    }
    internal static bool IsNewTestHero(RulesetCharacterHero hero) => newHeroMarker != null &&
        (hero.HasAnyFeature(newHeroMarker) || FreshHeroes.TryGetValue(hero, out _) && hero.ClassesHistory.Count == 1 &&
            hero.TryGetHeroBuildingData(out _) && !LevelUpHelper.IsLevelingUp(hero));
    private static readonly CompressedProgressionDefinitions Definitions = new("Inventor12");
    private static Dictionary<FeatureDefinition, string> PoolOrigins => Definitions.PoolOrigins;
    private static HashSet<FeatureDefinitionPower> InventorPowers => Definitions.Powers;
    private static HashSet<FeatureDefinition> WrappedFeatures => Definitions.WrappedFeatures;
    private static readonly HashSet<RulesetCharacterHero> ReportedDeadlocks = new();
    private static string settingsSignature;
    private static readonly HashSet<RulesetCharacter> ReportedUnsupported = new();
    private static readonly HashSet<string> SupportedSubclasses = new();

    private static string SettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(x => x.PropertyType == typeof(bool) || x.PropertyType == typeof(int))
        .Where(x => x.Name.Contains("CompressedProgressionPrototype") || x.Name.Contains("Multiclass") ||
                    x.Name.Contains("Inventor") || x.Name.Contains("Innovation") || x.Name.Contains("Feat") ||
                    x.Name.Contains("Asi") || x.Name.Contains("WeaponMastery") || x.Name.Contains("Ritual") ||
                    x.Name is "EnableLevel20" or "MaxAllowedClasses" or "EnableMinInOutAttributes" or
                        "UseAlternateSpellPointsSystem" or "EnablePreparedSpellsTables2024" or "DisableStreamlinedMultiLevelUp" or "IncreaseMaxAttunedItems")
        .OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(Main.Settings))) +
        "|Subclasses=" + string.Join(",", DiscoverSubclasses().Select(x => x.Name)) +
        "|Spells=" + string.Join(",", SpellCasting.SpellListDefinition.SpellsByLevel
            .SelectMany(x => x.Spells).Select(x => x.Name).OrderBy(x => x));

    internal static bool SessionConsistent => !Active || settingsSignature == SettingsSignature();

    // Resolve only UB Inventor constructs to their native owner; never rewrite copied CharacterLevel/PB attributes.
    internal static RulesetCharacter SourceOwner(RulesetCharacter character) => Active &&
        character is RulesetCharacterMonster monster && monster.MonsterDefinition.CharacterFamily == InventorConstructFamily
            ? character.GetMySummoner()?.RulesetCharacter ?? character : character;

    internal static int EffectiveLevel(RulesetCharacter character)
    {
        character = SourceOwner(character);
        var actual = character.GetClassLevel(Class);
        if (Active && actual > 12 && ReportedUnsupported.Add(character))
            Main.Error("Inventor prototype: existing Inventor above 12 is unsupported. Do not save this hero in compressed mode.");
        return InventorProgressionRules.EffectiveLevel(Active, actual);
    }

    internal static int EffectiveSubclassLevel(RulesetCharacter character, string subclass) =>
        InventorProgressionRules.EffectiveLevel(Active, character.GetSubclassLevel(Class, subclass));

    internal static bool HoldsPower(FeatureDefinitionPower power) => InventorPowers.Contains(power);

    internal static bool IsCapped(RulesetCharacterHero hero, CharacterClassDefinition klass) =>
        klass == Class && InventorProgressionRules.IsCapped(Active, hero.GetClassLevel(Class));

    // Use actual runtime registrations and subclass choices; never turn on hidden/obsolete subclasses.
    internal static IEnumerable<CharacterSubclassDefinition> DiscoverSubclasses()
    {
        var names = new HashSet<string>(Class.FeatureUnlocks.SelectMany(x => Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().SelectMany(x => x.Subclasses));
        if (SubclassesContext.KlassListContextTab.TryGetValue(Class, out var registered) &&
            Main.Settings.KlassListSubclassEnabled.TryGetValue(Class.Name, out var enabled))
        {
            names.UnionWith(registered.AllSubClasses.Where(x => enabled.Contains(x.Name)).Select(x => x.Name));
        }

        return DatabaseRepository.GetDatabase<CharacterSubclassDefinition>()
            .Where(x => names.Contains(x.Name) && !x.GuiPresentation.Hidden).OrderBy(x => x.Name);
    }

    internal static void LateLoad()
    {
        if (!Initialization.ShouldInitialize(Main.Settings.EnableInventorCompressedProgressionPrototype))
        {
            return;
        }

        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass)
        {
            Main.Error("Inventor prototype requires EnableLevel20 and EnableMulticlass. Prototype not activated.");
            return;
        }

        if (!WarlockInvocationContext.FinalizationAdapterAvailable ||
            SharedSpellsContext.ClassCasterType[ClassName] != FeatureDefinitionCastSpellBuilder.CasterProgression.HalfRoundUp ||
            SpellCasting.SpellKnowledge != RuleDefinitions.SpellKnowledge.WholeList ||
            SpellCasting.SpellReadyness != RuleDefinitions.SpellReadyness.Prepared || SpellCasting.SlotsPerLevels.Count < 20)
        {
            Main.Error("Inventor prototype: native finalization binding or installed WholeList/Prepared/HalfRoundUp casting is incompatible. Prototype remains disabled.");
            return;
        }
        if (!DiscoverSubclasses().Any())
        {
            Main.Error("Inventor prototype: no active registered Innovations; enable a supported Innovation and restart. Prototype remains disabled.");
            return;
        }
        newHeroMarker = FeatureDefinitionBuilder.Create("FeatureInventorCompressedProgressionPrototype")
            .SetGuiPresentationNoContent(true).AddToDB();
        Class.FeatureUnlocks.Add(new FeatureUnlockByLevel(newHeroMarker, 1));
        InventorSpellcastingContext.CaptureRules();
        var subclasses = DiscoverSubclasses().ToArray();
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>
        {
            (Class.Name, Class.FeatureUnlocks)
        };
        sources.AddRange(subclasses.Select(x => (x.Name, x.FeatureUnlocks)));
        InventorInfusionContext.Capture(Class.FeatureUnlocks);
        var prepared = sources.Select(x => (x.Unlocks, Prepared: Transform(x.Unlocks
            .Select((u, i) => new FeatureUnlockByLevel(InventorSpellcastingContext.AdaptAutoPrepared(
                InventorInfusionContext.Adapt(u.FeatureDefinition, x.Name, u.Level, i)), u.Level)).ToList(), x.Name))).ToArray();
        InventorSpellcastingContext.Initialize();
        foreach (var item in prepared)
        {
            item.Unlocks.Clear();
            item.Unlocks.AddRange(item.Prepared);
        }

        SupportedSubclasses.UnionWith(subclasses.Select(x => x.Name));
        Main.Info("Inventor prototype discovered subclasses: " + string.Join(", ", SupportedSubclasses.OrderBy(x => x)));

        InventorItemSafetyContext.RegisterPowers(InventorPowers);
        settingsSignature = SettingsSignature();
        Active = true;
        Main.Info("Inventor 20-to-12 TEST prototype active. New test heroes only; restart after settings changes.");
    }

    private static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature) =>
        CompressedProgressionDefinitions.Flatten(feature);

    private static List<FeatureUnlockByLevel> Transform(List<FeatureUnlockByLevel> target, string owner) =>
        Definitions.Prepare(target, owner);

    internal static bool CanContinue(RulesetCharacterHero hero)
    {
        if (!Active || hero.GetClassLevel(Class) == 0)
        {
            return true;
        }

        if (!IsNewTestHero(hero))
        {
            Main.Error("Inventor prototype: existing unmarked Inventor cannot be converted automatically. Use a NEW test hero created after enabling this prototype.");
            return false;
        }
        if (hero.GetClassLevel(Class) > 0 && !InventorSpellcastingContext.IsSupportedHero(hero))
        {
            Main.Error("Inventor prototype: mixed casting repertoires are not supported yet. Use a new single-caster test hero.");
            return false;
        }

        if (settingsSignature != SettingsSignature())
        {
            Main.Error("Inventor prototype: progression settings changed during this session. Restart required.");
            return false;
        }

        if (hero.ClassesAndSubclasses.TryGetValue(Class, out var subclass) &&
            !SupportedSubclasses.Contains(subclass.Name))
        {
            Main.Error("Inventor prototype: unsupported Inventor subclass; use a new supported test hero.");
            return false;
        }

        if (!InventorItemSafetyContext.CanAdvance(hero)) return false;

        if (hero.GetClassLevel(Class) < 12)
        {
            return true;
        }

        var allowed = new List<CharacterClassDefinition>();
        MulticlassInOutRulesHelper.EnumerateHeroAllowedClassDefinitions(hero, allowed, out _);
        if (allowed.Count != 0 && hero.GetClassLevel(Class) == 12)
        {
            return true;
        }

        if (ReportedDeadlocks.Add(hero))
        {
            Main.Error("Inventor prototype: cannot level up. Inventor is capped at 12 and no eligible alternative " +
                       "exists (or this is an unsupported Inventor above 12). Multiclass requirements were NOT changed.");
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
                tag += "Inventor12" + origin;
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

            InventorInfusionContext.Clear(data, tag);

            foreach (var key in data.LevelupTrainedExpertises.Keys.Where(x => x.StartsWith(tag + "Inventor12", StringComparison.Ordinal)).ToArray())
                data.LevelupTrainedExpertises.Remove(key);

            foreach (var stack in data.PointPoolStacks.Values)
            {
                foreach (var key in stack.ActivePools.Keys
                             .Where(x => x.StartsWith(tag + "Inventor12", StringComparison.Ordinal)).ToArray())
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
            if (Active && !__result && __instance.GetClassLevel(Class) > 0 && (WrappedFeatures.Contains(featureDefinition) || featureDefinition is FeatureDefinitionPower power && HoldsPower(power)))
            {
                __result = Class;
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

            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Inventor 20 to 12 prototype",
                "Level-up blocked: Inventor cap/multiclass eligibility, unsupported Inventor subclass or mixed casting, " +
                "or progression settings changed (restart required). See the UB log for the exact reason. " +
                "Multiclass ability requirements remain in force.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetEffectPower), nameof(RulesetEffectPower.GetClassLevel))]
    internal static class InventorPowerLevelPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetEffectPower __instance, RulesetCharacter character, ref int __result)
        {
            if (Active && SourceOwner(character).GetClassLevel(Class) > 0 && (InventorPowers.Contains(__instance.PowerDefinition) ||
                           __instance.PowerDefinition.GetFirstSubFeatureOfType<SolastaUnfinishedBusiness.Behaviors.ClassHolder>()?.Class == Class))
            {
                __result = EffectiveLevel(character);
            }
        }
    }

    internal static bool CanUseClassMechanics(RulesetCharacter character)
    {
        if (character != null) character = SourceOwner(character);
        if (!Active || character == null || character.GetClassLevel(Class) == 0) return true;
        if (SessionConsistent && character.GetClassLevel(Class) <= 12 &&
            character.GetOriginalHero() is { } hero && IsNewTestHero(hero) && InventorSpellcastingContext.IsSupportedHero(hero)) return true;
        Main.Error("Inventor prototype: restart required or unsupported old/mixed-caster hero. Class action rejected before resource mutation.");
        return false;
    }

    [HarmonyPatch(typeof(Tabletop2024Context), nameof(Tabletop2024Context.SwitchOneDndSpellRitualOnAllCasters))]
    internal static class RitualSwitchRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    [HarmonyPatch(typeof(SolastaUnfinishedBusiness.Classes.InventorClass), nameof(SolastaUnfinishedBusiness.Classes.InventorClass.SwitchSpellStoringItemSubPower))]
    internal static class StoringSwitchRestartPatch
    {
        internal static bool Prefix() => AllowRuleSwitch();
    }
    private static bool AllowRuleSwitch()
    {
        if (!Active) return true;
        Main.Info("Inventor prototype: rules saved for next launch. Full restart required.");
        return false;
    }
}
