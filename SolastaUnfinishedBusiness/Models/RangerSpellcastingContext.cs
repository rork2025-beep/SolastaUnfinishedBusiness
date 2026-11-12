using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Builders.Features;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;
using static HeroDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class RangerSpellcastingContext
{
    private const string Origin = "Ranger12";
    private const string Event = Origin + "Original";
    private static FeatureDefinitionCastSpell Casting => DatabaseHelper.FeatureDefinitionCastSpells.CastSpellRanger;
    private static KnownSpellsProgressionPlan plan;
    private static bool halfRoundUp;
    private static int[] originalMaximum;
    private static List<FeatureDefinitionCastSpell.SlotsByLevelDuplet> projectedSlots;
    private static int MaximumSpellLevel(int original) => originalMaximum[Math.Max(0, Math.Min(19, original - 1))];
    [HarmonyPatch(typeof(SpellPointsContext), nameof(SpellPointsContext.SwitchFeatureDefinitionCastSpellSlots))]
    internal static class SlotsRefreshPatch
    {
        internal static void Postfix()
        { if (RangerProgressionContext.Active) Casting.slotsPerLevels = projectedSlots; }
    }
    private static readonly Dictionary<FeatureDefinition, FeatureDefinition> Adapted = new();
    private static readonly ConditionalWeakTable<CharacterStageSpellSelectionPanel, List<(string Tag, PointsPoolType Type)>> Steps = new();

    internal static bool IsSupportedHero(RulesetCharacterHero hero) => hero != null &&
        !SharedSpellsContext.IsMulticaster(hero) &&
        !hero.SpellRepertoires.Any(x => x.SpellCastingFeature.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race &&
                                       x.SpellCastingFeature != Casting) &&
        !hero.ClassesAndLevels.Any(x => x.Key != Ranger && IsCaster(hero, x.Key));

    internal static int SingleCasterLevel(RulesetCharacterHero hero) =>
        RangerProgressionContext.Active && hero != null && hero.GetClassLevel(Ranger) > 0 && IsSupportedHero(hero)
            ? RangerProgressionRules.CasterContribution(RangerProgressionContext.EffectiveLevel(hero), halfRoundUp) : 0;

    private static bool IsCaster(RulesetCharacterHero hero, CharacterClassDefinition klass)
    {
        hero.ClassesAndSubclasses.TryGetValue(klass, out var sub);
        return klass.Name == RuleDefinitions.WarlockClass || SharedSpellsContext.GetCasterTypeForClassOrSubclass(
            klass.Name, sub?.Name) != FeatureDefinitionCastSpellBuilder.CasterProgression.None ||
            klass.FeatureUnlocks.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
                .OfType<FeatureDefinitionCastSpell>().Any(x => x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race) ||
            (sub != null && IsCastingSubclass(sub));
    }
    private static bool IsCastingSubclass(CharacterSubclassDefinition sub) => SharedSpellsContext.SubclassCasterType.ContainsKey(sub.Name) ||
        sub.FeatureUnlocks.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionCastSpell>().Any(x => x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race);
    internal static bool CanSelectClass(RulesetCharacterHero hero, CharacterClassDefinition klass)
    {
        if (!RangerProgressionContext.Active) return true;
        if (klass == Ranger) return IsSupportedHero(hero);
        if (hero.GetClassLevel(Ranger) == 0) return true;
        if (IsCaster(hero, klass)) return false;
        if (hero.ClassesAndSubclasses.ContainsKey(klass)) return true;
        var choices = klass.FeatureUnlocks.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().ToArray();
        // Do not lead the player into a class whose only enabled subclass choices are unsafe casters.
        return choices.All(choice => choice.Subclasses.Any(name =>
            DatabaseHelper.TryGetDefinition<CharacterSubclassDefinition>(name, out var sub) && !sub.GuiPresentation.Hidden && !IsCastingSubclass(sub)));
    }

    internal static void Initialize()
    {
        if (Casting.SpellKnowledge != RuleDefinitions.SpellKnowledge.Selection || Casting.SpellReadyness != RuleDefinitions.SpellReadyness.AllKnown)
            throw new InvalidOperationException("Ranger prototype requires the installed Selection/AllKnown definition; external WholeList casting is unsupported.");
        Main.Info("Ranger final source known=" + string.Join(",", Casting.KnownSpells) + "; replacements=" + string.Join(",", Casting.ReplacedSpells) +
                  "; PreparedSpellsTables2024=" + Main.Settings.EnablePreparedSpellsTables2024 + "; spellcasting starts1=" + Main.Settings.EnableRangerSpellCastingAtLevel1);
        halfRoundUp = SharedSpellsContext.ClassCasterType[RuleDefinitions.RangerClass] == FeatureDefinitionCastSpellBuilder.CasterProgression.HalfRoundUp;
        originalMaximum = Casting.SlotsPerLevels.Take(20).Select(x => x.Slots.FindLastIndex(n => n > 0) + 1).ToArray();
        if (originalMaximum.Length != 20 || originalMaximum.Any(x => x > 5))
            throw new InvalidOperationException("Ranger prototype requires twenty half-caster source rows, maximum circle5.");
        plan = new KnownSpellsProgressionPlan(Casting.KnownSpells, Casting.ReplacedSpells);
        var slots = SpellcastingProgressionPlan.ProjectRows(Casting.SlotsPerLevels).Select((row, index) =>
            new FeatureDefinitionCastSpell.SlotsByLevelDuplet { Level = index + 1, Slots = row.Slots.ToList() }).ToList();
        // Retain any cantrips already granted by final UB class rules.
        var cantrips = SpellcastingProgressionPlan.ProjectCumulative(KnownSpellsProgressionPlan.RetainedCantrips(Casting.KnownCantrips));
        var known = SpellcastingProgressionPlan.ProjectCumulative(Casting.KnownSpells);
        var replaced = plan.CompressedReplacements();
        projectedSlots = slots;
        Casting.slotsPerLevels = slots;
        Casting.KnownCantrips.Clear(); Casting.KnownCantrips.AddRange(cantrips);
        Casting.KnownSpells.Clear(); Casting.KnownSpells.AddRange(known);
        Casting.ReplacedSpells.Clear(); Casting.ReplacedSpells.AddRange(replaced);
    }

    internal static FeatureDefinition AdaptAutoPrepared(FeatureDefinition source)
    {
        if (Adapted.TryGetValue(source, out var found)) return found;
        var result = source;
        if (source is FeatureDefinitionAutoPreparedSpells auto && auto.SpellcastingClass == Ranger)
        {
            if (auto.AutoPreparedSpellsGroups.All(x => x.ClassLevel <= 5)) return Adapted[source] = source;
            var groups = auto.AutoPreparedSpellsGroups.Select(x => AutoPreparedSpellsGroupBuilder.BuildSpellGroup(
                FighterProgressionPlan<int>.CompressedLevel(x.ClassLevel), x.SpellsList.ToArray())).ToArray();
            var copy = FeatureDefinitionAutoPreparedSpellsBuilder.Create(auto, Origin + auto.Name).SetPreparedSpellGroups(groups).AddToDB();
            CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(auto, copy); result = copy;
        }
        else if (source is FeatureDefinitionFeatureSet set)
        {
            var children = set.FeatureSet.Select(AdaptAutoPrepared).ToArray();
            if (!children.SequenceEqual(set.FeatureSet))
            {
                var copy = FeatureDefinitionFeatureSetBuilder.Create(set, Origin + "Auto" + set.Name).AddToDB();
                copy.FeatureSet.Clear(); copy.FeatureSet.AddRange(children);
                CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(set, copy); result = copy;
            }
        }
        Adapted[source] = result; return result;
    }

    private static List<(string Tag, int Original, int Learned, int Replaced)> Events(RulesetCharacterHero hero)
    {
        var actual = hero.GetClassLevel(Ranger);
        if (!RangerProgressionContext.Active || actual < 1 || actual > 12) return new();
        var tag = AttributeDefinitions.GetClassTag(Ranger, actual);
        return plan.Events(actual).Select((x, index) =>
            (index == 0 ? tag : tag + Event + x.Original.ToString("D2"), x.Original, x.Learned, x.Replaced)).ToList();
    }
    private static bool TryEvent(RulesetCharacterHero hero, string tag, out int original)
    {
        original = Events(hero).Where(x => x.Tag == tag).Select(x => x.Original).FirstOrDefault();
        return original > 0;
    }
    internal static bool TryGetLearningMaximum(RulesetCharacterHero hero, string tag, out int maximum)
    {
        maximum = 0;
        if (!TryEvent(hero, tag, out var original)) return false;
        maximum = MaximumSpellLevel(original); return true;
    }
    private static List<SpellDefinition> Chosen(Dictionary<string, List<SpellDefinition>> choices, string tag) =>
        choices.TryGetValue(tag, out var spells) ? spells : new();

    private static List<SpellDefinition> KnownBefore(CharacterHeroBuildingData data, int original) => Before(data, original).ToList();
    internal static void FilterSelection(CharacterHeroBuildingData data, string tag, bool unlearn, ref List<SpellDefinition> knownSpells)
    {
        if (!TryEvent(data.HeroCharacter, tag, out var original)) return;
        try { knownSpells = KnownBefore(data, original).Concat(unlearn ? Enumerable.Empty<SpellDefinition>() : Chosen(data.AcquiredSpells, tag)).ToList(); }
        catch (InvalidOperationException) { knownSpells = new(); }
    }

    // No new learning resource: these are native transient selection pools, not spellbooks or power pools.
    internal static void ConfigureLearning(CharacterHeroBuildingData data)
    {
        var hero = data.HeroCharacter;
        if (!RangerProgressionContext.Active || !IsSupportedHero(hero) || LevelUpHelper.GetSelectedClass(hero) != Ranger) return;
        var events = Events(hero);
        if (events.Count == 0) return;
        hero.EnumerateFeaturesToBrowse<ISpellCastingAffinityProvider>(hero.FeaturesToBrowse, null);
        var bonus = hero.FeaturesToBrowse.OfType<ISpellCastingAffinityProvider>().Sum(x => x.AdditionalKnownSpellsCount);
        var repertoire = hero.GetClassSpellRepertoire(Ranger);
        var current = repertoire?.KnownSpells.Count ?? 0;
        var actual = hero.GetClassLevel(Ranger);
        var cantrips = Math.Max(0, Casting.KnownCantrips[actual - 1] - (repertoire?.KnownCantrips.Count ?? 0));
        if (cantrips > 0) SetPool(PointsPoolType.Cantrip, events[0].Tag, cantrips,
            Chosen(data.AcquiredCantrips, events[0].Tag).Count, events[0].Original);
        else data.PointPoolStacks[PointsPoolType.Cantrip].ActivePools.Remove(events[0].Tag);
        var first = Math.Max(0, plan.Known[events[0].Original - 1] + bonus - current);
        foreach (var e in events)
        {
            var removed = Chosen(data.UnlearnedSpells, e.Tag).Count;
            if (e.Replaced > 0 && data.CanUnlearn) SetPool(PointsPoolType.SpellUnlearn, e.Tag, e.Replaced, removed, e.Original);
            var learned = e.Tag == events[0].Tag ? first : e.Learned;
            // Keep zero-budget learning placeholders so unlearning can add a replacement without rebuilding steps.
            if (learned > 0 || e.Replaced > 0)
                SetPool(PointsPoolType.Spell, e.Tag, learned + removed, Chosen(data.AcquiredSpells, e.Tag).Count, e.Original);
        }
        return;
        void SetPool(PointsPoolType type, string tag, int max, int used, int original)
        {
            var pools = data.PointPoolStacks[type].ActivePools;
            if (!pools.TryGetValue(tag, out var pool)) pools[tag] = pool = new PointPool(0, null, false);
            pool.MaxPoints = max; pool.RemainingPoints = Math.Max(0, max - used);
            pool.MinSpellLevel = type == PointsPoolType.Cantrip ? 0 : 1;
            pool.MaxSpellLevel = type == PointsPoolType.Cantrip ? 0 : MaximumSpellLevel(original);
            pool.SpellListOverride = Casting.SpellListDefinition;
            pool.Description = "Ranger: original class level " + original;
        }
    }
    private static void InvalidateLater(CharacterHeroBuildingData data, int original)
    {
        foreach (var e in Events(data.HeroCharacter).Where(x => x.Original > original))
        { data.AcquiredSpells.Remove(e.Tag); data.UnlearnedSpells.Remove(e.Tag); }
        ConfigureLearning(data);
    }

    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.SetupSpellPointPools))]
    internal static class SetupLearningPatch
    {
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UpgradeSpellPointPools))]
    internal static class UpgradeLearningPatch
    {
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.GetSpellFeature))]
    internal static class LearningFeaturePatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, string tag, ref FeatureDefinitionCastSpell __result)
        {
            if (!RangerProgressionContext.Active || !tag.Contains(Origin) || heroBuildingData.HeroCharacter.GetClassLevel(Ranger) == 0) return true;
            __result = Casting; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AcquireSpell))]
    internal static class AcquireLearningPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, SpellDefinition spell, string tag, PointsPoolType poolType)
        {
            var data = heroBuildingData;
            if (poolType != PointsPoolType.Spell || !TryEvent(data.HeroCharacter, tag, out var original)) return true;
            try
            {
                if (!Allowed(data.HeroCharacter, spell, original) ||
                    !data.PointPoolStacks[PointsPoolType.Spell].ActivePools.TryGetValue(tag, out var pool) || pool.RemainingPoints <= 0 ||
                    KnownBefore(data, original).Contains(spell) || Chosen(data.AcquiredSpells, tag).Contains(spell) ||
                    Chosen(data.UnlearnedSpells, tag).Contains(spell)) return false;
                InvalidateLater(data, original); return true;
            }
            catch (InvalidOperationException ex)
            { Main.Info("Ranger choice rejected: " + ex.Message); return false; }
        }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UnlearnSpell))]
    internal static class UnlearnLearningPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, SpellDefinition spell, string tag)
        {
            var data = heroBuildingData;
            if (!TryEvent(data.HeroCharacter, tag, out var original)) return true;
            try
            {
                if (!data.CanUnlearn || !KnownBefore(data, original).Contains(spell) || Chosen(data.UnlearnedSpells, tag).Contains(spell) ||
                    Chosen(data.UnlearnedSpells, tag).Count >= plan.Replacements[original - 1]) return false;
                InvalidateLater(data, original); return true;
            }
            catch (InvalidOperationException ex)
            { Main.Info("Ranger choice rejected: " + ex.Message); return false; }
        }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UnacquireSpell))]
    internal static class UnacquireLearningPatch
    {
        internal static void Prefix(CharacterHeroBuildingData heroBuildingData, string tag)
        { if (TryEvent(heroBuildingData.HeroCharacter, tag, out var original)) InvalidateLater(heroBuildingData, original); }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UndoUnlearnSpell))]
    internal static class UndoLearningPatch
    {
        internal static void Prefix(CharacterHeroBuildingData heroBuildingData, string tag)
        {
            if (!TryEvent(heroBuildingData.HeroCharacter, tag, out var original)) return;
            // Removing a replacement invalidates its replacement acquisition and all subsequent events.
            heroBuildingData.AcquiredSpells.Remove(tag); InvalidateLater(heroBuildingData, original);
        }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.ClearPrevious))]
    internal static class ClearLearningPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero, string tag)
        {
            if (!RangerProgressionContext.Active || string.IsNullOrEmpty(tag) || !hero.TryGetHeroBuildingData(out var data)) return;
            Granted.Remove(data);
            foreach (var key in data.PointPoolStacks.Values.SelectMany(x => x.ActivePools.Keys).Concat(data.AcquiredSpells.Keys)
                         .Concat(data.AcquiredCantrips.Keys).Concat(data.UnlearnedSpells.Keys).Concat(data.BonusCantrips.Keys)
                         .Where(x => x.StartsWith(tag + Origin, StringComparison.Ordinal)).Distinct().ToArray())
            {
                data.AcquiredSpells.Remove(key); data.AcquiredCantrips.Remove(key); data.UnlearnedSpells.Remove(key); data.BonusCantrips.Remove(key);
                __instance.ClearPrevious(hero, key);
            }
            if (!TryEvent(hero, tag, out _)) return;
            data.AcquiredSpells.Remove(tag); data.AcquiredCantrips.Remove(tag); data.UnlearnedSpells.Remove(tag);
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.RemoveActiveFeaturesFromHeroByTag))]
    internal static class RemoveLearningPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero, string tag) => ClearLearningPatch.Prefix(__instance, hero, tag);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.GrantCantripsAndSpellsByTag))]
    internal static class GrantLearningPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, string tag, FeatureDefinitionCastSpell spellcastingFeature) =>
            spellcastingFeature != Casting || !OwnTag(heroBuildingData.HeroCharacter, tag) ||
            Granted.GetValue(heroBuildingData, _ => new RangerSpellGrantLedger()).Grant(AttributeDefinitions.GetClassTag(Ranger, heroBuildingData.HeroCharacter.GetClassLevel(Ranger)), tag);
        internal static void Postfix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData,
            string tag, FeatureDefinitionCastSpell spellcastingFeature, bool __runOriginal)
        {
            if (!__runOriginal || spellcastingFeature != Casting || !TryEvent(heroBuildingData.HeroCharacter, tag, out _) || tag.Contains(Origin)) return;
            foreach (var key in KnownSpellsProgressionPlan.SupplementaryTags(tag, Origin,
                heroBuildingData.AcquiredSpells.Keys.Concat(heroBuildingData.UnlearnedSpells.Keys).Concat(heroBuildingData.AcquiredCantrips.Keys).Concat(heroBuildingData.BonusCantrips.Keys)))
                __instance.GrantCantripsAndSpellsByTag(heroBuildingData, key, Casting, false);
        }
    }

    private static bool UsesPanel(CharacterStageSpellSelectionPanel panel) => RangerProgressionContext.Active &&
        panel.currentHero != null && panel.currentHero.GetClassLevel(Ranger) is >= 1 and <= 12 &&
        LevelUpHelper.GetSelectedClass(panel.currentHero) == Ranger;
    [HarmonyPatch(typeof(CharacterStageSpellSelectionPanel), nameof(CharacterStageSpellSelectionPanel.CollectTags))]
    internal static class CollectLearningStepsPatch
    {
        internal static void Postfix(CharacterStageSpellSelectionPanel __instance)
        {
            var p = __instance;
            Steps.Remove(p);
            if (!UsesPanel(p)) return;
            var all = new List<(string Tag, PointsPoolType Type)>();
            Add(p.cantripTags, PointsPoolType.Cantrip); Add(p.spellUnlearnTags, PointsPoolType.SpellUnlearn);
            Add(p.spellTags, PointsPoolType.Spell); Add(p.cantripOrSpellTags, PointsPoolType.CantripOrSpell);
            all = KnownSpellsProgressionPlan.OrderSteps(all.Select(x => (Step: x,
                Original: x.Type is PointsPoolType.Spell or PointsPoolType.SpellUnlearn && TryEvent(p.currentHero, x.Tag, out var original) ? original : 0,
                Phase: x.Type == PointsPoolType.SpellUnlearn ? 0 : 1)).ToList());
            p.allTags = all.Select(x => x.Tag).ToList(); Steps.Add(p, all);
            return;
            void Add(List<string> tags, PointsPoolType type) { if (tags != null) all.AddRange(tags.Select(x => (x, type))); }
        }
    }
    [HarmonyPatch(typeof(CharacterStageSpellSelectionPanel), nameof(CharacterStageSpellSelectionPanel.GetPoolTypeOfIndex))]
    internal static class LearningStepTypePatch
    {
        internal static bool Prefix(CharacterStageSpellSelectionPanel __instance, int index, ref PointsPoolType __result)
        {
            if (!UsesPanel(__instance) || !Steps.TryGetValue(__instance, out var steps) || index < 0 || index >= steps.Count) return true;
            __result = steps[index].Type; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageSpellSelectionPanel), nameof(CharacterStageSpellSelectionPanel.IsSpellUnlearnStep))]
    internal static class UnlearningStepTypePatch
    {
        internal static bool Prefix(CharacterStageSpellSelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!UsesPanel(__instance) || !Steps.TryGetValue(__instance, out var steps) || stepNumber < 0 || stepNumber >= steps.Count) return true;
            __result = steps[stepNumber].Type == PointsPoolType.SpellUnlearn; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageSpellSelectionPanel), nameof(CharacterStageSpellSelectionPanel.IsCantripStep))]
    internal static class CantripStepTypePatch
    {
        internal static bool Prefix(CharacterStageSpellSelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!UsesPanel(__instance) || !Steps.TryGetValue(__instance, out var steps) || stepNumber < 0 || stepNumber >= steps.Count) return true;
            __result = steps[stepNumber].Type == PointsPoolType.Cantrip; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageSpellSelectionPanel), nameof(CharacterStageSpellSelectionPanel.IsSpellStep))]
    internal static class SpellStepTypePatch
    {
        internal static bool Prefix(CharacterStageSpellSelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!UsesPanel(__instance) || !Steps.TryGetValue(__instance, out var steps) || stepNumber < 0 || stepNumber >= steps.Count) return true;
            __result = steps[stepNumber].Type == PointsPoolType.Spell; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageSpellSelectionPanel), nameof(CharacterStageSpellSelectionPanel.IsCantripOrSpellStep))]
    internal static class CantripOrSpellStepTypePatch
    {
        internal static bool Prefix(CharacterStageSpellSelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!UsesPanel(__instance) || !Steps.TryGetValue(__instance, out var steps) || stepNumber < 0 || stepNumber >= steps.Count) return true;
            __result = steps[stepNumber].Type == PointsPoolType.CantripOrSpell; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageSpellSelectionPanel), nameof(CharacterStageSpellSelectionPanel.CanProceedToNextStage))]
    internal static class ValidateLearningPatch
    {
        internal static void Postfix(CharacterStageSpellSelectionPanel __instance, ref bool __result, ref string failureString)
        {
            var p = __instance;
            if (!__result || !UsesPanel(p)) return;
            var data = p.currentHero.GetHeroBuildingData();
            var pendingCasting = data.levelupTrainedFeats.Values.SelectMany(x => x).SelectMany(x => x.Features)
                .Concat(p.currentHero.TrainedFightingStyles.SelectMany(x => x.Features))
                .SelectMany(CompressedProgressionDefinitions.Flatten).OfType<FeatureDefinitionCastSpell>()
                .Any(x => x != Casting && x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race);
            if (!IsSupportedHero(p.currentHero) || pendingCasting)
            {
                __result = false; failureString = "Ranger prototype: undo the choice granting a second casting repertoire."; return;
            }
            if (Steps.TryGetValue(p, out var steps) && steps.Any(x => x.Type != PointsPoolType.SpellUnlearn &&
                    data.PointPoolStacks[x.Type].ActivePools.TryGetValue(x.Tag, out var pool) && pool.RemainingPoints > 0))
            {
                __result = false; failureString = "Complete all independent Ranger learning events."; return;
            }
            try { ValidateAll(data); }
            catch (InvalidOperationException ex)
            { __result = false; failureString = "Ranger spell choices: " + ex.Message; }
        }
    }
    [HarmonyPatch(typeof(CharacterStageSpellSelectionPanel), nameof(CharacterStageSpellSelectionPanel.BuildLearnSteps))]
    internal static class BuildLearningStepsPatch
    {
        internal static bool Prefix(CharacterStageSpellSelectionPanel __instance)
        {
            var p = __instance;
            if (!UsesPanel(p) || !Steps.TryGetValue(p, out var steps)) return true;
            while (p.learnStepsTable.childCount < steps.Count) Gui.GetPrefabFromPool(p.learnStepPrefab, p.learnStepsTable);
            for (var i = 0; i < p.learnStepsTable.childCount; i++)
            {
                var child = p.learnStepsTable.GetChild(i); child.gameObject.SetActive(i < steps.Count);
                if (i >= steps.Count) continue;
                var step = steps[i];
                var title = TryEvent(p.currentHero, step.Tag, out var original) ? "Ranger: original level " + original : string.Empty;
                child.GetComponent<LearnStepItem>().Bind(i, step.Tag, step.Type, p.OnLearnBack, p.OnLearnReset, p.OnLearnAuto, title);
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageSubclassSelectionPanel), nameof(CharacterStageSubclassSelectionPanel.OnBeginShow))]
    internal static class CasterSubclassSelectionPatch
    {
        internal static void Prefix(CharacterStageSubclassSelectionPanel __instance)
        {
            if (RangerProgressionContext.Active && __instance.currentHero.GetClassLevel(Ranger) > 0)
                __instance.compatibleSubclasses.RemoveAll(IsCastingSubclass);
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AssignSubclass))]
    internal static class CasterSubclassBoundaryPatch
    {
        internal static bool Prefix(RulesetCharacterHero hero, CharacterSubclassDefinition subclassDefinition)
        {
            if (!RangerProgressionContext.Active || hero.GetClassLevel(Ranger) == 0 || !IsCastingSubclass(subclassDefinition)) return true;
            Main.Error("Ranger prototype: a second casting subclass is not supported. Select a noncasting subclass."); return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.IsFeatMatchingPrerequisites))]
    internal static class CastingFeatBoundaryPatch
    {
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData, FeatDefinition feat, ref bool __result)
        {
            if (RangerProgressionContext.Active && heroBuildingData.HeroCharacter.GetClassLevel(Ranger) > 0 &&
                feat.Features.SelectMany(CompressedProgressionDefinitions.Flatten).OfType<FeatureDefinitionCastSpell>()
                    .Any(x => x != Casting && x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race)) __result = false;
        }
    }
}
