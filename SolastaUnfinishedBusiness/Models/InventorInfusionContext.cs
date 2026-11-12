using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.CustomUI;
using static SolastaUnfinishedBusiness.Classes.InventorClass;
using Pool = SolastaUnfinishedBusiness.CustomUI.CustomInvocationSelectionPanel.FeaturePool;
using Id = SolastaUnfinishedBusiness.CustomUI.CustomInvocationSelectionPanel.PoolId;
using Transaction = SolastaUnfinishedBusiness.Models.WarlockInvocationTransaction<InvocationDefinition>;

namespace SolastaUnfinishedBusiness.Models;

// Custom invocation pools do not use native Warlock point pools. Keep the custom UI and
// native command service, but give each original event its own tag and ordered inventory.
internal static class InventorInfusionContext
{
    private const string Marker = "Inventor12InfusionOriginal";
    private static readonly Dictionary<FeatureDefinitionCustomInvocationPool, int> Origins = new();
    private static readonly Dictionary<int, FeatureDefinitionCustomInvocationPool> EmptyLearn = new();
    internal static bool IsInfusion(InvocationDefinition item) => item is InvocationDefinitionCustom custom && custom.PoolType == InvocationPoolTypeCustom.Pools.Infusion;
    private static bool Uses(RulesetCharacterHero hero) => InventorProgressionContext.Active && hero != null && hero.GetClassLevel(Class) is >= 1 and <= 12 && LevelUpHelper.GetSelectedClass(hero) == Class;
    internal static FeatureDefinition Adapt(FeatureDefinition source, string owner, int original, int index, string path = "")
    {
        if (source is FeatureDefinitionCustomInvocationPool pool && pool.PoolType == InvocationPoolTypeCustom.Pools.Infusion)
        {
            var copy = CustomInvocationPoolDefinitionBuilder.Create(pool, Marker + owner + original + "Event" + index + path + pool.Name)
                .Setup(pool.PoolType, pool.Points, pool.IsUnlearn).AddToDB();
            CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(pool, copy); Origins.Add(copy, original);
            return copy;
        }
        if (source is not FeatureDefinitionFeatureSet set || !CompressedProgressionDefinitions.Flatten(set)
                .OfType<FeatureDefinitionCustomInvocationPool>().Any(p => p.PoolType == InvocationPoolTypeCustom.Pools.Infusion)) return source;
        var clone = FeatureDefinitionFeatureSetBuilder.Create(set, Marker + owner + original + "Event" + index + path + set.Name).AddToDB();
        clone.FeatureSet.Clear(); clone.FeatureSet.AddRange(set.FeatureSet.Select((f, i) => Adapt(f, owner, original, index, path + "Child" + i)));
        CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(set, clone); return clone;
    }
    private static string Parent(RulesetCharacterHero hero) => AttributeDefinitions.GetClassTag(Class, hero.GetClassLevel(Class));
    private static string Tag(RulesetCharacterHero hero, int original) => Parent(hero) + Marker + original.ToString("D2");
    private static bool TryOriginal(RulesetCharacterHero hero, string tag, out int original)
    {
        original = 0;
        return Uses(hero) && tag != null && tag.StartsWith(Parent(hero) + Marker, StringComparison.Ordinal) &&
            int.TryParse(tag.Substring((Parent(hero) + Marker).Length), out original) &&
            SpellcastingProgressionPlan.OriginalLevels(hero.GetClassLevel(Class)).Contains(original);
    }
    private static InventorInfusionPlan<InvocationDefinition> plan;
    internal static void Capture(IEnumerable<FeatureUnlockByLevel> source)
    {
        plan = new InventorInfusionPlan<InvocationDefinition>(source.SelectMany(u => CompressedProgressionDefinitions.Flatten(u.FeatureDefinition)
            .OfType<FeatureDefinitionCustomInvocationPool>().Where(p => p.PoolType == InvocationPoolTypeCustom.Pools.Infusion)
            .Select(p => (Original: u.Level, Pool: p))).GroupBy(e => e.Original)
            .Select(g => (g.Key, g.Where(e => !e.Pool.IsUnlearn).Sum(e => e.Pool.Points), g.Where(e => e.Pool.IsUnlearn).Sum(e => e.Pool.Points))));
    }
    private static IEnumerable<(int Original, int Awards, int Replacements)> Events(RulesetCharacterHero hero) => plan.Events(hero.GetClassLevel(Class));
    private static IEnumerable<InvocationDefinition> Choices(Dictionary<string, List<InvocationDefinition>> choices, string tag) =>
        choices.TryGetValue(tag, out var value) ? value : Enumerable.Empty<InvocationDefinition>();
    private static Transaction Create(RulesetCharacterHero hero, Dictionary<string, List<InvocationDefinition>> added,
        Dictionary<string, List<InvocationDefinition>> removed) => plan.Transaction(hero.GetClassLevel(Class), hero.TrainedInvocations.Where(IsInfusion),
            n => Tag(hero, n), tag => Choices(added, tag), tag => Choices(removed, tag));
    private static Transaction Create(RulesetCharacterHero hero, Dictionary<Id, List<InvocationDefinitionCustom>> choices)
    {
        var added = choices.Where(p => !p.Key.Unlearn && p.Key.Name == InvocationPoolTypeCustom.Pools.Infusion.Name)
            .ToDictionary(p => p.Key.Tag, p => p.Value.Cast<InvocationDefinition>().ToList());
        var removed = choices.Where(p => p.Key.Unlearn && p.Key.Name == InvocationPoolTypeCustom.Pools.Infusion.Name)
            .ToDictionary(p => p.Key.Tag, p => p.Value.Cast<InvocationDefinition>().ToList());
        return Create(hero, added, removed);
    }
    private sealed class Scope(RulesetCharacterHero hero, int original, HashSet<InvocationDefinition> known) : IDisposable
    {
        internal readonly RulesetCharacterHero Hero = hero;
        internal readonly int Original = original;
        internal readonly HashSet<InvocationDefinition> Known = known;
        internal readonly Scope Previous = selection;
        public void Dispose() => selection = Previous;
    }
    [ThreadStatic] private static Scope selection;
    internal static int? SelectionLevel(RulesetCharacterHero hero, string name) => selection?.Hero == hero && name == ClassName ? selection.Original : null;
    internal static bool Known(RulesetCharacterHero hero, InvocationDefinition item) => IsInfusion(item) && selection?.Hero == hero && selection.Known != null
        ? selection.Known.Contains(item) : hero.TrainedInvocations.Contains(item);
    private static bool Prerequisite(RulesetCharacterHero hero, InvocationDefinition item, int original)
    {
        if (item is not InvocationDefinitionCustom custom || !IsInfusion(item) || item.RequiredLevel > original) return false;
        using var scope = new Scope(hero, original, null); selection = scope;
        return PowerBundle.ValidatePrerequisites(hero, item, custom.Validators, out _);
    }
    internal static IDisposable EnterSelection(RulesetCharacterHero hero, List<Pool> pools,
        Dictionary<Id, List<InvocationDefinitionCustom>> choices, int step)
    {
        if (!Uses(hero) || pools.Count == 0 || !TryOriginal(hero, pools[Math.Min(step, pools.Count - 1)].Id.Tag, out var original)) return null;
        var pool = pools[Math.Min(step, pools.Count - 1)];
        HashSet<InvocationDefinition> known;
        try
        {
            known = new HashSet<InvocationDefinition>(Create(hero, choices).Before(original, (x, n, _) => Prerequisite(hero, x, n)));
            if (!pool.IsUnlearn && choices.TryGetValue(new Id(pool.Id.Name, pool.Id.Tag, true), out var removed)) known.ExceptWith(removed);
        }
        catch (InvalidOperationException) { known = new HashSet<InvocationDefinition>(); }
        var scope = new Scope(hero, original, known); selection = scope; return scope;
    }
    internal static bool ValidateAt(RulesetCharacterHero hero, InvocationDefinitionCustom item, string tag) =>
        TryOriginal(hero, tag, out var original) ? Prerequisite(hero, item, original) : PowerBundle.ValidatePrerequisites(hero, item, item.Validators, out _);
    [HarmonyPatch(typeof(CustomInvocationSelectionPanel), "UpdateGrantedFeatures")]
    internal static class OriginPatch
    {
        internal static void Postfix(List<(string, FeatureDefinitionCustomInvocationPool)> ____gainedCustomFeatures)
        {
            if (!InventorProgressionContext.Active) return;
            var list = ____gainedCustomFeatures;
            for (var i = 0; i < list.Count; i++)
            {
                var (tag, pool) = list[i];
                if (Origins.TryGetValue(pool, out var original)) list[i] = (tag + Marker + original.ToString("D2"), pool);
            }
            // Replacement-only levels still need a zero-budget learn step; native UI increments it
            // after an unlearn. Precreate it so adding a pool never resorts the current step.
            foreach (var (tag, pool) in list.ToArray().Where(e => Origins.ContainsKey(e.Item2) && e.Item2.IsUnlearn))
            {
                if (list.Any(e => e.Item1 == tag && e.Item2.PoolType == pool.PoolType && !e.Item2.IsUnlearn)) continue;
                var original = Origins[pool];
                if (!EmptyLearn.TryGetValue(original, out var zero))
                    EmptyLearn[original] = zero = CustomInvocationPoolDefinitionBuilder.Create(Marker + original + "ReplacementOnly")
                        .Setup(pool.PoolType, 0).AddToDB();
                list.Add((tag, zero));
            }
        }
    }
    internal static void SyncPools(RulesetCharacterHero hero, List<Pool> pools, Dictionary<Id, List<InvocationDefinitionCustom>> choices)
    {
        if (!Uses(hero)) return;
        var events = Events(hero).ToDictionary(e => e.Original);
        foreach (var pool in pools)
        {
            if (!TryOriginal(hero, pool.Id.Tag, out var original) || !events.TryGetValue(original, out var e)) continue;
            var removedId = new Id(pool.Id.Name, pool.Id.Tag, true);
            var removed = choices.TryGetValue(removedId, out var r) ? r.Count : 0;
            pool.Max = pool.IsUnlearn ? e.Replacements : e.Awards + removed;
            pool.Used = choices.TryGetValue(pool.Id, out var items) ? items.Count : 0;
        }
    }
    internal static void ResetFrom(RulesetCharacterHero hero, List<Pool> pools, Dictionary<Id, List<InvocationDefinitionCustom>> choices, int step)
    {
        if (!Uses(hero) || step < 0 || step >= pools.Count || !TryOriginal(hero, pools[step].Id.Tag, out var original)) return;
        foreach (var pool in pools.Where(p => TryOriginal(hero, p.Id.Tag, out var n) &&
                     (n > original || n == original && pools[step].IsUnlearn)))
        { if (choices.TryGetValue(pool.Id, out var items)) items.Clear(); pool.Used = 0; pool.Skipped = false; }
        SyncPools(hero, pools, choices);
    }
    internal static bool CanSelect(RulesetCharacterHero hero, Pool pool, InvocationDefinitionCustom item,
        Dictionary<Id, List<InvocationDefinitionCustom>> choices)
    {
        if (!TryOriginal(hero, pool.Id.Tag, out var original)) return true;
        if (!InventorProgressionContext.CanUseClassMechanics(hero) || !InventorItemSafetyContext.CanAdvance(hero) || !IsInfusion(item)) return false;
        if (choices.TryGetValue(pool.Id, out var selected) && selected.Contains(item)) return true;
        if (pool.Remaining <= 0) return false;
        try
        {
            var known = Create(hero, choices).Before(original, (x, n, _) => Prerequisite(hero, x, n));
            if (pool.IsUnlearn) return known.Contains(item);
            var removed = choices.TryGetValue(new Id(pool.Id.Name, pool.Id.Tag, true), out var r) ? r : new List<InvocationDefinitionCustom>();
            // Do not offer undo/relearn of the same infusion as a free replacement.
            return !known.Contains(item) && !removed.Contains(item) && Prerequisite(hero, item, original);
        }
        catch (InvalidOperationException) { return false; }
    }
    private sealed class Finalization
    {
        internal string Parent;
        internal Transaction Transaction;
        internal HashSet<List<InvocationDefinition>> Lists;
        internal InvocationDefinition[] Before;
        internal bool Failed;
    }
    private static readonly ConditionalWeakTable<RulesetCharacterHero, Finalization> Finalizations = new();
    internal static bool BeginFinalize(RulesetCharacterHero hero)
    {
        if (!Uses(hero)) return true;
        try
        {
            if (!InventorProgressionContext.CanUseClassMechanics(hero) || !InventorItemSafetyContext.CanAdvance(hero))
                throw new InvalidOperationException("Unsafe active items/constructs or session changed.");
            if (Finalizations.TryGetValue(hero, out var previous))
            {
                if (previous.Parent == Parent(hero) || previous.Failed) throw new InvalidOperationException("Finalization already started; cancel/reload after failure.");
                Finalizations.Remove(hero);
            }
            if (!WarlockInvocationContext.FinalizationAdapterAvailable) throw new InvalidOperationException("Native invocation finalization binding is unavailable.");
            var data = hero.GetHeroBuildingData();
            var tx = Create(hero, data.LevelupTrainedInvocations, data.UnlearnedInvocations);
            tx.Prepare((x, n, _) => Prerequisite(hero, x, n));
            var lists = data.LevelupTrainedInvocations.Concat(data.UnlearnedInvocations)
                .Where(p => TryOriginal(hero, p.Key, out _)).Select(p => p.Value).ToArray();
            if (lists.Any(l => l.Any(x => !IsInfusion(x)))) throw new InvalidOperationException("Mixed custom invocation origin.");
            // Reject infusion drafts without a registered per-event origin (including generic native tags).
            if (data.LevelupTrainedInvocations.Concat(data.UnlearnedInvocations)
                .Any(p => p.Value.Any(IsInfusion) && !TryOriginal(hero, p.Key, out _)))
                throw new InvalidOperationException("Unknown infusion origin.");
            Finalizations.Add(hero, new Finalization { Parent = Parent(hero), Transaction = tx,
                Lists = new HashSet<List<InvocationDefinition>>(lists), Before = hero.TrainedInvocations.Where(IsInfusion).ToArray() });
            return true;
        }
        catch (InvalidOperationException ex)
        { Main.Error("Inventor infusion finalization rejected before mutation: " + ex.Message); return false; }
    }
    // Called by the existing native finalization adapter, before its Warlock handling. Own lists only.
    internal static bool TryApply(RulesetCharacterHero hero, List<InvocationDefinition> list)
    {
        if (!InventorProgressionContext.Active || !Finalizations.TryGetValue(hero, out var state) || !state.Lists.Contains(list)) return false;
        state.Transaction.ApplyPrepared(e => { hero.UnlearnInvocations(e.Removed.ToList()); hero.TrainInvocations(e.Added.ToList()); },
            () => { hero.UnlearnInvocations(hero.TrainedInvocations.Where(IsInfusion).ToList()); hero.TrainInvocations(state.Before.ToList()); });
        return true;
    }
    internal static void Failed(RulesetCharacterHero hero)
    {
        if (!Finalizations.TryGetValue(hero, out var state)) return;
        state.Failed = true;
        hero.UnlearnInvocations(hero.TrainedInvocations.Where(IsInfusion).ToList()); hero.TrainInvocations(state.Before.ToList());
        Main.Error("Inventor finalization failed: infusion snapshot restored; cancel/reload. Other native level-up state is not transactional.");
    }
    internal static void Clear(CharacterHeroBuildingData data, string parent)
    {
        if (!InventorProgressionContext.Active) return;
        foreach (var key in data.LevelupTrainedInvocations.Keys.Concat(data.UnlearnedInvocations.Keys)
                     .Where(k => k.StartsWith(parent + Marker, StringComparison.Ordinal)).Distinct().ToArray())
        { data.LevelupTrainedInvocations.Remove(key); data.UnlearnedInvocations.Remove(key); }
        Finalizations.Remove(data.HeroCharacter);
    }
}
