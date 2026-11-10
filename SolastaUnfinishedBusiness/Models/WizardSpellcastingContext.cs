using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Builders.Features;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static class WizardSpellcastingContext
{
    private static readonly Dictionary<FeatureDefinition, FeatureDefinition> Adapted = new();
    private static SpellcastingProgressionPlan learning;
    private static FeatureDefinitionCastSpell Casting => DatabaseHelper.FeatureDefinitionCastSpells.CastSpellWizard;
    private const string Learning = "Wizard12LearningOriginal";

    internal static bool IsSupportedHero(RulesetCharacterHero hero) => hero != null &&
        !SharedSpellsContext.IsMulticaster(hero) &&
        !hero.SpellRepertoires.Any(x => x.SpellCastingFeature.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race &&
                                       x.SpellCastingFeature != Casting) &&
        !hero.ClassesAndLevels.Any(x => x.Key != Wizard && IsCaster(hero, x.Key));

    private static bool IsCaster(RulesetCharacterHero hero, CharacterClassDefinition klass)
    {
        hero.ClassesAndSubclasses.TryGetValue(klass, out var sub);
        return klass.Name == RuleDefinitions.WarlockClass ||
            SharedSpellsContext.GetCasterTypeForClassOrSubclass(klass.Name, sub?.Name) !=
            Builders.Features.FeatureDefinitionCastSpellBuilder.CasterProgression.None;
    }
    internal static bool CanSelectClass(RulesetCharacterHero hero, CharacterClassDefinition klass)
    {
        if (!WizardProgressionContext.Active) return true;
        return klass == Wizard ? IsSupportedHero(hero) : hero.GetClassLevel(Wizard) == 0 || !IsCaster(hero, klass);
    }

    internal static void Initialize()
    {
        learning = new SpellcastingProgressionPlan(Casting.ScribedSpells);
        var slots = Casting.SlotsPerLevels.ToArray();
        if (slots.Length < 20) throw new InvalidOperationException("Wizard slots must cover original 1-20.");
        // Validate/build all tables before changing live definitions; do not mutate shared slot rows.
        var cantrips = SpellcastingProgressionPlan.ProjectCumulative(Casting.KnownCantrips);
        var known = SpellcastingProgressionPlan.ProjectCumulative(Casting.KnownSpells);
        var projectedSlots = SpellcastingProgressionPlan.ProjectRows(slots).Select((row, index) =>
            new FeatureDefinitionCastSpell.SlotsByLevelDuplet
            {
                Level = index + 1,
                Slots = row.Slots.ToList()
            }).ToList();
        Casting.slotsPerLevels = projectedSlots;
        Casting.KnownCantrips.Clear(); Casting.KnownCantrips.AddRange(cantrips);
        Casting.KnownSpells.Clear(); Casting.KnownSpells.AddRange(known);
        Casting.ScribedSpells.Clear(); Casting.ScribedSpells.AddRange(learning.PrimaryScribedEvents());
    }

    internal static FeatureDefinition AdaptAutoPrepared(FeatureDefinition source)
    {
        if (Adapted.TryGetValue(source, out var found)) return found;
        FeatureDefinition result = source;
        if (source is FeatureDefinitionAutoPreparedSpells auto && auto.SpellcastingClass == Wizard)
        {
            var groups = auto.AutoPreparedSpellsGroups.Select(x =>
                AutoPreparedSpellsGroupBuilder.BuildSpellGroup(FighterProgressionPlan<int>.CompressedLevel(x.ClassLevel), x.SpellsList.ToArray())).ToArray();
            var copy = FeatureDefinitionAutoPreparedSpellsBuilder.Create(auto, "Wizard12" + auto.Name)
                .SetPreparedSpellGroups(groups).AddToDB();
            CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(auto, copy); result = copy;
        }
        else if (source is FeatureDefinitionFeatureSet set)
        {
            var children = set.FeatureSet.Select(AdaptAutoPrepared).ToArray();
            if (!children.SequenceEqual(set.FeatureSet))
            {
                var copy = FeatureDefinitionFeatureSetBuilder.Create(set, "Wizard12Auto" + set.Name).AddToDB();
                copy.FeatureSet.Clear(); copy.FeatureSet.AddRange(children);
                CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(set, copy); result = copy;
            }
        }
        Adapted[source] = result; return result;
    }

    // Original event #1 stays in the native class pool. Additional represented levels get independent pools.
    internal static void ConfigureLearning(CharacterBuildingManager manager, CharacterHeroBuildingData data)
    {
        var hero = data.HeroCharacter;
        if (!WizardProgressionContext.Active || !IsSupportedHero(hero) ||
            LevelUpHelper.GetSelectedClass(hero) != Wizard || !data.PointPoolStacks.TryGetValue(HeroDefinitions.PointsPoolType.Spell, out var stack)) return;
        var actual = hero.GetClassLevel(Wizard);
        if (actual < 1 || actual > 12) return;
        var levels = SpellcastingProgressionPlan.OriginalLevels(actual);
        var tag = AttributeDefinitions.GetClassTag(Wizard, actual);
        // Exclude known/autoprepared/other-pool acquisitions, but count this step's own choices back in.
        // Reservation of earlier event budgets prevents an impossible later pool when the book is almost full.
        var ownTags = new HashSet<string>(levels.Select((original, index) => index == 0 ? tag : tag + Learning + original));
        var ownChoices = data.AcquiredSpells.Where(x => ownTags.Contains(x.Key)).SelectMany(x => x.Value).ToArray();
        var known = new HashSet<SpellDefinition>(manager.EnumerateKnownAndAcquiredSpells(data, string.Empty).Except(ownChoices));
        var affinities = hero.FeaturesByType<FeatureDefinitionMagicAffinity>().ToArray();
        var platform = ServiceRepository.GetService<IGamingPlatformService>();
        var available = Casting.SpellListDefinition.SpellsByLevel.SelectMany(x => x.Spells)
            .Concat(affinities.Where(x => x.ExtendedSpellList).SelectMany(x => x.ExtendedSpellList.SpellsByLevel)
                .SelectMany(x => x.Spells))
            .Where(x => x.SpellLevel > 0 && !known.Contains(x) && platform.IsContentPackAvailable(x.ContentPack))
            .Distinct().ToArray();
        var bonus = affinities.Sum(x => x.AdditionalScribedSpells);
        var reserved = 0;
        for (var index = 0; index < levels.Length; index++)
        {
            var original = levels[index];
            var max = SpellcastingProgressionPlan.MaxSpellLevel(original);
            var eventTag = index == 0 ? tag : tag + Learning + original;
            var remaining = available.Count(x => x.SpellLevel <= max);
            if (index == 0)
            {
                if (!stack.ActivePools.TryGetValue(eventTag, out var first)) continue;
                first.MaxSpellLevel = max;
                first.MaxPoints = SpellcastingProgressionPlan.LearningBudget(first.MaxPoints, remaining, 0);
                var acquired = data.AcquiredSpells.TryGetValue(eventTag, out var selected) ? selected.Count : 0;
                first.RemainingPoints = Math.Max(0, first.MaxPoints - acquired);
                reserved = first.MaxPoints;
                continue;
            }
            // SetPointPool resets RemainingPoints even for an existing pool. Never replenish a visited step.
            if (!stack.ActivePools.ContainsKey(eventTag))
            {
                manager.SetPointPool(data, HeroDefinitions.PointsPoolType.Spell, eventTag,
                    SpellcastingProgressionPlan.LearningBudget(learning.LearningAmount(original, bonus), remaining, reserved));
            }
            if (stack.ActivePools.TryGetValue(eventTag, out var pool))
            {
                pool.MinSpellLevel = 1;
                pool.MaxSpellLevel = max;
                pool.SpellListOverride = Casting.SpellListDefinition;
                pool.Description = "Wizard learning: original class level " + original;
                reserved += pool.MaxPoints;
            }
        }
    }

    internal static bool TryGetLearningMaximum(RulesetCharacterHero hero, string tag, out int maximum)
    {
        maximum = 0;
        var actual = hero?.GetClassLevel(Wizard) ?? 0;
        if (!WizardProgressionContext.Active || actual < 1 || actual > 12 || tag == null) return false;
        var prefix = AttributeDefinitions.GetClassTag(Wizard, actual);
        var levels = SpellcastingProgressionPlan.OriginalLevels(actual);
        var original = levels[0];
        if (tag != prefix && (!tag.StartsWith(prefix + Learning, StringComparison.Ordinal) ||
                              !int.TryParse(tag.Substring((prefix + Learning).Length), out original) ||
                              !levels.Skip(1).Contains(original))) return false;
        maximum = SpellcastingProgressionPlan.MaxSpellLevel(original);
        return true;
    }

    // Native finalization knows only standard class tags; route supplementary events through its spellbook logic.
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.GrantCantripsAndSpellsByTag))]
    internal static class GrantLearningPatch
    {
        internal static void Postfix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData,
            string tag, FeatureDefinitionCastSpell spellcastingFeature)
        {
            if (!WizardProgressionContext.Active || spellcastingFeature != Casting || tag.Contains(Learning)) return;
            // Include origin-tagged subclass pools, e.g. Spell Master's two bonus cantrips at original6.
            foreach (var key in heroBuildingData.AcquiredSpells.Keys.Concat(heroBuildingData.AcquiredCantrips.Keys)
                         .Where(x => x.StartsWith(tag + "Wizard12", StringComparison.Ordinal)).Distinct().OrderBy(x => x).ToArray())
                __instance.GrantCantripsAndSpellsByTag(heroBuildingData, key, Casting, false);
        }
    }

    // The selection panel does not enforce MaxSpellLevel for ordinary class pools. Enforce it at acquisition too.
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AcquireSpell))]
    internal static class LearningBoundaryPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, SpellDefinition spell, string tag,
            HeroDefinitions.PointsPoolType poolType)
        {
            var hero = heroBuildingData.HeroCharacter;
            if (poolType != HeroDefinitions.PointsPoolType.Spell || !TryGetLearningMaximum(hero, tag, out var maximum)) return true;
            if (spell.SpellLevel > maximum) return false;
            // Initial creation has not received starting equipment yet. Subsequent levels must fit real books.
            var actual = hero.GetClassLevel(Wizard);
            if (actual <= 1 || !ServiceRepository.GetService<IGameSettingsService>().SpellbookPagesConstraint) return true;
            var repertoire = hero.GetClassSpellRepertoire(Wizard);
            if (repertoire == null) return false;
            repertoire.GatherAndSortSpellbooks();
            var prefix = AttributeDefinitions.GetClassTag(Wizard, actual);
            var costs = SpellcastingProgressionPlan.OriginalLevels(actual).Select((original, index) =>
                index == 0 ? prefix : prefix + Learning + original).SelectMany(key =>
            {
                var selected = heroBuildingData.AcquiredSpells.TryGetValue(key, out var choices)
                    ? choices.AsEnumerable() : Enumerable.Empty<SpellDefinition>();
                if (key == tag && !selected.Contains(spell)) selected = selected.Append(spell);
                return selected.Select(x => x.SpellLevel);
            });
            if (SpellcastingProgressionPlan.CanFitSpellbooks(repertoire.foundSpellbooks.Select(x =>
                    (x.OwnerName, x.RemainingPages)), hero.Name, costs)) return true;
            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Wizard prototype",
                "Not enough blank spellbook pages for these learning events. Cancel level-up and obtain additional spellbooks.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterStageSubclassSelectionPanel), nameof(CharacterStageSubclassSelectionPanel.OnBeginShow))]
    internal static class CasterSubclassSelectionPatch
    {
        internal static void Prefix(CharacterStageSubclassSelectionPanel __instance)
        {
            if (WizardProgressionContext.Active && __instance.currentHero.GetClassLevel(Wizard) > 0)
                __instance.compatibleSubclasses.RemoveAll(x => SharedSpellsContext.SubclassCasterType.ContainsKey(x.Name));
        }
    }

    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UpgradeSpellPointPools))]
    internal static class UpgradeLearningPatch
    {
        internal static void Postfix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(__instance, heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.SetupSpellPointPools))]
    internal static class SetupLearningPatch
    {
        internal static void Postfix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(__instance, heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.GetSpellFeature))]
    internal static class LearningFeaturePatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(string tag, ref FeatureDefinitionCastSpell __result)
        {
            if (!WizardProgressionContext.Active || tag == null || !tag.Contains(Learning)) return true;
            __result = Casting; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.ClearPrevious))]
    internal static class ClearLearningPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero, string tag)
        {
            if (!WizardProgressionContext.Active || string.IsNullOrEmpty(tag) || tag.Contains(Learning) || !hero.TryGetHeroBuildingData(out var data)) return;
            // AcquiredSpells can outlive its pool. Clear both, including cancel/back/reselect.
            foreach (var key in data.PointPoolStacks.Values.SelectMany(x => x.ActivePools.Keys)
                         .Concat(data.AcquiredSpells.Keys).Concat(data.AcquiredCantrips.Keys)
                         .Where(x => x.StartsWith(tag + "Wizard12", StringComparison.Ordinal)).Distinct().ToArray())
            {
                __instance.UnacquireSpells(data, key);
                data.AcquiredCantrips.Remove(key);
                __instance.ClearPrevious(hero, key);
            }
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.RemoveActiveFeaturesFromHeroByTag))]
    internal static class RemoveLearningPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero, string tag) =>
            ClearLearningPatch.Prefix(__instance, hero, tag);
    }

    [HarmonyPatch(typeof(RulesetCharacterHero), nameof(RulesetCharacterHero.ComputeMaxPreparedSpells))]
    internal static class PreparedPatch
    {
        internal static void Postfix(RulesetCharacterHero __instance, RulesetSpellRepertoire spellRepertoire, ref int __result)
        {
            if (!WizardProgressionContext.Active || spellRepertoire.SpellCastingClass != Wizard || Main.Settings.EnablePreparedSpellsTables2024) return;
            if (spellRepertoire.SpellCastingFeature.SpellPreparationCount == RuleDefinitions.SpellPreparationCount.AbilityBonusPlusLevel)
                __result += WizardProgressionContext.EffectiveLevel(__instance) - __instance.GetClassLevel(Wizard);
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AssignSubclass))]
    internal static class CasterSubclassBoundaryPatch
    {
        internal static bool Prefix(RulesetCharacterHero hero, CharacterSubclassDefinition subclassDefinition)
        {
            if (!WizardProgressionContext.Active || hero.GetClassLevel(Wizard) == 0 ||
                !SharedSpellsContext.SubclassCasterType.ContainsKey(subclassDefinition.Name)) return true;
            Gui.GuiService.ShowMessage(MessageModal.Severity.Informative1, "Wizard prototype",
                "A second casting class/subclass is not supported by this prototype. Choose a noncasting subclass.",
                "Message/&MessageOkTitle", string.Empty, null, null);
            return false;
        }
    }
}
