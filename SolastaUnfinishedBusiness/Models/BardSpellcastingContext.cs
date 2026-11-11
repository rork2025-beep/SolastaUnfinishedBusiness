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

internal static partial class BardSpellcastingContext
{
    private const string Origin = "Bard12";
    private const string Event = Origin + "Original";
    private static FeatureDefinitionCastSpell Casting => DatabaseHelper.FeatureDefinitionCastSpells.CastSpellBard;
    private static KnownSpellsProgressionPlan plan;
    private static readonly Dictionary<FeatureDefinition, FeatureDefinition> Adapted = new();
    private static readonly ConditionalWeakTable<CharacterStageSpellSelectionPanel, List<(string Tag, PointsPoolType Type)>> Steps = new();

    internal static bool IsSupportedHero(RulesetCharacterHero hero) => hero != null &&
        !SharedSpellsContext.IsMulticaster(hero) &&
        !hero.SpellRepertoires.Any(x => x.SpellCastingFeature.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race &&
                                       x.SpellCastingFeature != Casting) &&
        !hero.ClassesAndLevels.Any(x => x.Key != Bard && IsCaster(hero, x.Key));

    internal static int SingleCasterLevel(RulesetCharacterHero hero) =>
        BardProgressionContext.Active && hero != null && hero.GetClassLevel(Bard) > 0 && IsSupportedHero(hero)
            ? SpellcastingProgressionPlan.CasterLevel(hero.GetClassLevel(Bard)) : 0;

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
        if (!BardProgressionContext.Active) return true;
        if (klass == Bard) return IsSupportedHero(hero);
        if (hero.GetClassLevel(Bard) == 0) return true;
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
        plan = new KnownSpellsProgressionPlan(Casting.KnownSpells, Casting.ReplacedSpells);
        Main.Info("Bard final source known table: " + string.Join(",", plan.Known) +
                  "; EnablePreparedSpellsTables2024=" + Main.Settings.EnablePreparedSpellsTables2024 +
                  "; UseAlternateSpellPointsSystem=" + Main.Settings.UseAlternateSpellPointsSystem);
        var slots = SpellcastingProgressionPlan.ProjectRows(Casting.SlotsPerLevels).Select((row, index) =>
            new FeatureDefinitionCastSpell.SlotsByLevelDuplet { Level = index + 1, Slots = row.Slots.ToList() }).ToList();
        // Final Bard definitions have zero placeholders after16; retain acquired cantrips at17-20.
        var cantrips = SpellcastingProgressionPlan.ProjectCumulative(KnownSpellsProgressionPlan.RetainedCantrips(Casting.KnownCantrips));
        var known = SpellcastingProgressionPlan.ProjectCumulative(Casting.KnownSpells);
        var replaced = plan.CompressedReplacements();
        Casting.slotsPerLevels = slots;
        Casting.KnownCantrips.Clear(); Casting.KnownCantrips.AddRange(cantrips);
        Casting.KnownSpells.Clear(); Casting.KnownSpells.AddRange(known);
        Casting.ReplacedSpells.Clear(); Casting.ReplacedSpells.AddRange(replaced);
    }

    internal static FeatureDefinition AdaptAutoPrepared(FeatureDefinition source)
    {
        if (Adapted.TryGetValue(source, out var found)) return found;
        var result = source;
        if (source is FeatureDefinitionAutoPreparedSpells auto && auto.SpellcastingClass == Bard)
        {
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
        var actual = hero.GetClassLevel(Bard);
        if (!BardProgressionContext.Active || actual < 1 || actual > 12) return new();
        var tag = AttributeDefinitions.GetClassTag(Bard, actual);
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
        if (TryEvent(hero, tag, out var original))
        { maximum = SpellcastingProgressionPlan.MaxSpellLevel(original); return true; }
        if (!TrySecret(hero, tag, out var secret)) return false;
        maximum = secret.Pool.MaxSpellLevel; return true;
    }
    private static List<SpellDefinition> Chosen(Dictionary<string, List<SpellDefinition>> choices, string tag) =>
        choices.TryGetValue(tag, out var spells) ? spells : new();

    internal static void FilterSelection(CharacterHeroBuildingData data, string tag, bool unlearn,
        ref List<SpellDefinition> knownSpells)
    {
        if (!TryEvent(data.HeroCharacter, tag, out _) && !TrySecret(data.HeroCharacter, tag, out _)) return;
        try
        {
            var before = Before(data, tag);
            if (unlearn) { knownSpells = before.Ordinary.ToList(); return; }
            // The native widget also receives known/acquired lists; replace that snapshot for this original event.
            knownSpells = before.All.Concat(Chosen(data.AcquiredSpells, tag))
                .Concat(Chosen(data.AcquiredCantrips, tag)).Distinct().ToList();
        }
        catch (InvalidOperationException) { knownSpells = new(); }
    }

    // No new learning resource: these are native transient selection pools, not spellbooks or power pools.
    internal static void ConfigureLearning(CharacterHeroBuildingData data)
    {
        var hero = data.HeroCharacter;
        if (!BardProgressionContext.Active || !IsSupportedHero(hero) || LevelUpHelper.GetSelectedClass(hero) != Bard) return;
        var events = Events(hero);
        if (events.Count == 0) return;
        hero.EnumerateFeaturesToBrowse<ISpellCastingAffinityProvider>(hero.FeaturesToBrowse, null);
        var bonus = hero.FeaturesToBrowse.OfType<ISpellCastingAffinityProvider>().Sum(x => x.AdditionalKnownSpellsCount);
        var repertoire = hero.GetClassSpellRepertoire(Bard);
        var current = repertoire?.KnownSpells.Count ?? 0;
        var actual = hero.GetClassLevel(Bard);
        var cantrips = Math.Max(0, Casting.KnownCantrips[actual - 1] - (repertoire?.KnownCantrips.Count ?? 0));
        if (cantrips > 0) SetPool(PointsPoolType.Cantrip, events[0].Tag, cantrips,
            Chosen(data.AcquiredCantrips, events[0].Tag).Count, events[0].Original);
        else data.PointPoolStacks[PointsPoolType.Cantrip].ActivePools.Remove(events[0].Tag);
        var first = Math.Max(0, plan.Known[events[0].Original - 1] + bonus - current);
        foreach (var e in events)
        {
            var removed = Chosen(data.UnlearnedSpells, e.Tag).Count;
            if (e.Replaced > 0) SetPool(PointsPoolType.SpellUnlearn, e.Tag, e.Replaced, removed, e.Original);
            var learned = e.Tag == events[0].Tag ? first : e.Learned;
            // Keep zero-budget learning placeholders so unlearning can add a replacement without rebuilding steps.
            if (learned > 0 || e.Replaced > 0)
                SetPool(PointsPoolType.Spell, e.Tag, learned + removed, Chosen(data.AcquiredSpells, e.Tag).Count, e.Original);
        }
        foreach (var choice in Choices(data).Where(x => x.Secret != null))
        {
            var pool = data.PointPoolStacks[PointsPoolType.CantripOrSpell].ActivePools[choice.Tag];
            pool.MaxPoints = choice.Awards;
            pool.RemainingPoints = Math.Max(0, choice.Awards - Chosen(data.AcquiredSpells, choice.Tag).Count - Chosen(data.AcquiredCantrips, choice.Tag).Count);
        }
        return;
        void SetPool(PointsPoolType type, string tag, int max, int used, int original)
        {
            var pools = data.PointPoolStacks[type].ActivePools;
            if (!pools.TryGetValue(tag, out var pool)) pools[tag] = pool = new PointPool(0, null, false);
            pool.MaxPoints = max; pool.RemainingPoints = Math.Max(0, max - used);
            pool.MinSpellLevel = type == PointsPoolType.Cantrip ? 0 : 1;
            pool.MaxSpellLevel = type == PointsPoolType.Cantrip ? 0 : SpellcastingProgressionPlan.MaxSpellLevel(original);
            pool.SpellListOverride = Casting.SpellListDefinition;
            pool.Description = "Bard: original class level " + original;
        }
    }
    private static void InvalidateLater(CharacterHeroBuildingData data, int original, string editedTag = null)
    {
        var editingSecret = editedTag != null && TrySecret(data.HeroCharacter, editedTag, out _);
        foreach (var e in Choices(data).Where(x => x.Original > original ||
                     (x.Original == original && x.Secret != null && (!editingSecret || string.CompareOrdinal(x.Tag, editedTag) > 0))))
        { data.AcquiredSpells.Remove(e.Tag); data.AcquiredCantrips.Remove(e.Tag); data.UnlearnedSpells.Remove(e.Tag); }
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
            if (!BardProgressionContext.Active || !tag.Contains(Origin) || heroBuildingData.HeroCharacter.GetClassLevel(Bard) == 0) return true;
            __result = Casting; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AcquireSpell))]
    internal static class AcquireLearningPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, SpellDefinition spell, string tag, PointsPoolType poolType)
        {
            var data = heroBuildingData;
            var original = StepOriginal(data.HeroCharacter, tag);
            if (original == 0) return true;
            if (!data.PointPoolStacks[poolType].ActivePools.TryGetValue(tag, out var pool) || pool.RemainingPoints <= 0) return false;
            try
            {
                var secret = TrySecret(data.HeroCharacter, tag, out var choice) ? choice : null;
                if (poolType == PointsPoolType.Cantrip && secret == null)
                {
                    if (spell.SpellLevel != 0 || !InClassLists(data.HeroCharacter, spell, original) || Initial(data).All.Contains(spell)) return false;
                }
                else
                {
                    if (poolType != (secret == null ? PointsPoolType.Spell : PointsPoolType.CantripOrSpell) ||
                        !Allowed(data.HeroCharacter, spell, original, secret) || Before(data, tag).All.Contains(spell) ||
                        Chosen(data.AcquiredSpells, tag).Contains(spell) || Chosen(data.AcquiredCantrips, tag).Contains(spell) ||
                        Chosen(data.UnlearnedSpells, tag).Contains(spell)) return false;
                }
                InvalidateLater(data, original, tag); return true;
            }
            catch (InvalidOperationException) { return false; }
        }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UnlearnSpell))]
    internal static class UnlearnLearningPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, SpellDefinition spell, string tag)
        {
            var data = heroBuildingData;
            if (TrySecret(data.HeroCharacter, tag, out _)) return false;
            if (!TryEvent(data.HeroCharacter, tag, out var original)) return true;
            try
            {
                if (!Before(data, tag).Ordinary.Contains(spell) || Chosen(data.UnlearnedSpells, tag).Contains(spell) ||
                    Chosen(data.UnlearnedSpells, tag).Count >= plan.Replacements[original - 1]) return false;
                InvalidateLater(data, original, tag); return true;
            }
            catch (InvalidOperationException) { return false; }
        }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UnacquireSpell))]
    internal static class UnacquireLearningPatch
    {
        internal static void Prefix(CharacterHeroBuildingData heroBuildingData, string tag)
        { var original = StepOriginal(heroBuildingData.HeroCharacter, tag); if (original > 0) InvalidateLater(heroBuildingData, original, tag); }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UndoUnlearnSpell))]
    internal static class UndoLearningPatch
    {
        internal static void Prefix(CharacterHeroBuildingData heroBuildingData, string tag)
        {
            if (!TryEvent(heroBuildingData.HeroCharacter, tag, out var original)) return;
            // Removing a replacement invalidates its replacement acquisition and all subsequent events.
            heroBuildingData.AcquiredSpells.Remove(tag); InvalidateLater(heroBuildingData, original, tag);
        }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => ConfigureLearning(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.ClearPrevious))]
    internal static class ClearLearningPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero, string tag)
        {
            if (!BardProgressionContext.Active || string.IsNullOrEmpty(tag) || !hero.TryGetHeroBuildingData(out var data)) return;
            Granted.Remove(data);
            var primary = Events(hero).FirstOrDefault().Tag == tag;
            foreach (var key in data.PointPoolStacks.Values.SelectMany(x => x.ActivePools.Keys).Concat(data.AcquiredSpells.Keys)
                         .Concat(data.AcquiredCantrips.Keys).Concat(data.UnlearnedSpells.Keys)
                         .Where(x => x.StartsWith(tag + Origin, StringComparison.Ordinal) || (primary && TrySecret(hero, x, out _))).Distinct().ToArray())
            {
                data.AcquiredSpells.Remove(key); data.AcquiredCantrips.Remove(key); data.UnlearnedSpells.Remove(key);
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
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, string tag, FeatureDefinitionCastSpell spellcastingFeature)
        {
            if (!BardProgressionContext.Active || spellcastingFeature != Casting || StepOriginal(heroBuildingData.HeroCharacter, tag) == 0) return true;
            return Granted.GetValue(heroBuildingData, _ => new BardSpellGrantLedger()).Grant(Events(heroBuildingData.HeroCharacter)[0].Tag, tag);
        }
        internal static void Postfix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData,
            string tag, FeatureDefinitionCastSpell spellcastingFeature, bool __runOriginal)
        {
            if (!__runOriginal || !BardProgressionContext.Active || spellcastingFeature != Casting ||
                Events(heroBuildingData.HeroCharacter).FirstOrDefault().Tag != tag) return;
            // Ordinary first event was just granted. Merge class/subclass Secrets before the NEXT ordinary event.
            foreach (var e in Choices(heroBuildingData).Where(x => x.Tag != tag))
                __instance.GrantCantripsAndSpellsByTag(heroBuildingData, e.Tag, Casting, false);
        }
    }

    private static bool UsesPanel(CharacterStageSpellSelectionPanel panel) => BardProgressionContext.Active &&
        panel.currentHero != null && panel.currentHero.GetClassLevel(Bard) is >= 1 and <= 12 &&
        LevelUpHelper.GetSelectedClass(panel.currentHero) == Bard;
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
                Original: x.Type is PointsPoolType.Spell or PointsPoolType.SpellUnlearn || TrySecret(p.currentHero, x.Tag, out _) ? StepOriginal(p.currentHero, x.Tag) : 0,
                Phase: StepPhase(p.currentHero, x.Tag, x.Type))).ToList());
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
                __result = false; failureString = "Bard prototype: undo the choice granting a second casting repertoire."; return;
            }
            if (Steps.TryGetValue(p, out var steps) && steps.Any(x => x.Type != PointsPoolType.SpellUnlearn &&
                    data.PointPoolStacks[x.Type].ActivePools.TryGetValue(x.Tag, out var pool) && pool.RemainingPoints > 0))
            {
                __result = false; failureString = "Complete all independent Bard learning events."; return;
            }
            try { ValidateAll(data); }
            catch (InvalidOperationException ex)
            { __result = false; failureString = "Bard spell event: " + ex.Message; }
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
                var original = StepOriginal(p.currentHero, step.Tag);
                var title = original > 0 ? (TrySecret(p.currentHero, step.Tag, out _) ? "Magical Secrets: original level " : "Bard: original level ") + original : string.Empty;
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
            if (BardProgressionContext.Active && __instance.currentHero.GetClassLevel(Bard) > 0)
                __instance.compatibleSubclasses.RemoveAll(IsCastingSubclass);
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AssignSubclass))]
    internal static class CasterSubclassBoundaryPatch
    {
        internal static bool Prefix(RulesetCharacterHero hero, CharacterSubclassDefinition subclassDefinition)
        {
            if (!BardProgressionContext.Active || hero.GetClassLevel(Bard) == 0 || !IsCastingSubclass(subclassDefinition)) return true;
            Main.Error("Bard prototype: a second casting subclass is not supported. Select a noncasting subclass."); return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.IsFeatMatchingPrerequisites))]
    internal static class CastingFeatBoundaryPatch
    {
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData, FeatDefinition feat, ref bool __result)
        {
            if (BardProgressionContext.Active && heroBuildingData.HeroCharacter.GetClassLevel(Bard) > 0 &&
                feat.Features.SelectMany(CompressedProgressionDefinitions.Flatten).OfType<FeatureDefinitionCastSpell>()
                    .Any(x => x != Casting && x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race)) __result = false;
        }
    }
}
