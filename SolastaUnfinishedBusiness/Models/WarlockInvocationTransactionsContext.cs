using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Interfaces;
using SolastaUnfinishedBusiness.Subclasses;
using SolastaUnfinishedBusiness.Subclasses.Builders;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.SpellDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.InvocationDefinitions;
using static HeroDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class WarlockInvocationContext
{
    private static int internalReset;
    private static bool adapterAvailable;
    private static readonly ConditionalWeakTable<RulesetCharacterHero, Finalization> Finalizations = new();
    private static readonly HashSet<RulesetCharacterHero> FailedFinalizations = new();

    private static WarlockInvocationTransaction<InvocationDefinition> Transaction(CharacterHeroBuildingData data,
        int through = 20, int edited = 0, InvocationDefinition adding = null, InvocationDefinition removing = null)
    {
        var events = plan.Events(data.HeroCharacter.GetClassLevel(Warlock)).Where(x => x.Original <= through).Select(e =>
        {
            var tag = Tag(data.HeroCharacter, e.Original);
            var added = Chosen(data.LevelupTrainedInvocations, tag);
            var removed = Chosen(data.UnlearnedInvocations, tag);
            if (e.Original == edited)
            {
                if (adding != null) added.Add(adding);
                if (removing != null) removed.Add(removing);
            }
            return new WarlockInvocationTransaction<InvocationDefinition>.Event(tag, e.Original, e.Learned,
                data.CanUnlearn ? e.Replaced : 0, removed, added);
        });
        return new WarlockInvocationTransaction<InvocationDefinition>(data.HeroCharacter.TrainedInvocations
            .Where(x => x is not InvocationDefinitionCustom), events);
    }

    private static bool HasChoicePool(InvocationDefinition invocation) => invocation.GrantedFeature != null &&
        CompressedProgressionDefinitions.Flatten(invocation.GrantedFeature).Any(x =>
            x is FeatureDefinitionPointPool or FeatureDefinitionCustomInvocationPool or FeatureDefinitionCastSpell or FeatureDefinitionGrantInvocations ||
            x is FeatureDefinitionProficiency { ProficiencyType: RuleDefinitions.ProficiencyType.Feat or RuleDefinitions.ProficiencyType.FightingStyle } ||
            x.GetAllSubFeaturesOfType<ICustomLevelUpLogic>().Any());

    private static bool HasImmediateGrant(InvocationDefinition invocation) => invocation.GrantedFeature != null &&
        CompressedProgressionDefinitions.Flatten(invocation.GrantedFeature).Any(x =>
            x is FeatureDefinitionCastSpell or FeatureDefinitionGrantInvocations ||
            x is FeatureDefinitionProficiency { ProficiencyType: RuleDefinitions.ProficiencyType.Feat or RuleDefinitions.ProficiencyType.FightingStyle } ||
            x.GetAllSubFeaturesOfType<ICustomLevelUpLogic>().Any());

    private static IEnumerable<PointsPoolType> ChoiceChannels(InvocationDefinition invocation) =>
        invocation.GrantedFeature == null ? Enumerable.Empty<PointsPoolType>() :
        CompressedProgressionDefinitions.Flatten(invocation.GrantedFeature).OfType<FeatureDefinitionPointPool>()
            .SelectMany(x => x.PoolType == PointsPoolType.CantripOrSpell ?
                new[] { PointsPoolType.Cantrip, PointsPoolType.Spell } : new[] { x.PoolType });

    private static bool ChoiceCollision(CharacterHeroBuildingData data, InvocationDefinition invocation, string tag)
    {
        if (!Uses(data.HeroCharacter)) return false;
        var channels = ChoiceChannels(invocation).ToArray();
        if (channels.Length == 0) return false;
        var classOrigin = TryOriginal(data.HeroCharacter, tag, out _);
        return data.LevelupTrainedInvocations.Any(entry =>
            (classOrigin || TryOriginal(data.HeroCharacter, entry.Key, out _)) && entry.Value
                .Where(x => x is not InvocationDefinitionCustom).Any(x => ChoiceChannels(x).Intersect(channels).Any()));
    }

    private static void CheckResourceChoices(WarlockInvocationTransaction<InvocationDefinition> transaction)
    {
        var seen = new HashSet<InvocationDefinition>();
        foreach (var e in transaction.Events)
        {
            if (e.Removed.Any(HasImmediateGrant))
                throw new InvalidOperationException("Removing invocation with immediate custom/feat/repertoire grants requires a full hero rollback adapter.");
            // Native transient spell pools do not identify multiple incarnations of the same invocation.
            // Keep a safe guard for Tome/other choice grants; never silently keep an intermediate spell reward.
            foreach (var item in e.Removed.Where(HasChoicePool))
                if (seen.Contains(item)) throw new InvalidOperationException("Invocation choice-pool replacement/reacquisition requires game validation: " + item.Name);
            foreach (var item in e.Added.Where(HasChoicePool))
                if (!seen.Add(item) || transaction.Events.Any(prior => prior.Original <= e.Original && prior.Removed.Contains(item)))
                    throw new InvalidOperationException("Repeated invocation spell/feature choice grants are not supported: " + item.Name);
        }
    }

    private static bool Prerequisites(CharacterHeroBuildingData data, InvocationDefinition invocation, int original,
        IReadOnlyList<InvocationDefinition> inventory)
    {
        var hero = data.HeroCharacter;
        if (invocation.RequiredLevel > original) return false;
        if (invocation.RequiredKnownSpell && !KnowsSpell(data, invocation.RequiredKnownSpell, original, inventory)) return false;
        if (invocation.RequiredPact && !HasPact(hero, invocation.RequiredPact, inventory)) return false;
        return Validators(data, invocation, original, inventory);
    }

    private static bool Validators(CharacterHeroBuildingData data, InvocationDefinition invocation, int original,
        IReadOnlyList<InvocationDefinition> inventory)
    {
        if (invocation is not InvocationDefinitionWithPrerequisites complex || complex.Validators.Count == 0) return true;
        var hero = data.HeroCharacter;
        // These adapters match the inspected source delegates, not arbitrary invocations with similar names.
        if (complex.Validators.Count == 1 && complex.Validators[0].Method.DeclaringType == typeof(InvocationsBuilders))
        {
            var method = complex.Validators[0].Method.Name;
            if (method == "ValidateHex")
                return KnowsSpell(data, Malediction, original, inventory) || KnowsSpell(data, BestowCurse, original, inventory) ||
                       inventory.Contains(SignIllOmen) || hero.GetSubclassLevel(Warlock, PatronSoulBlade.FullName) > 0;
            if (method.StartsWith("<BuildDevouringBlade>g__Validate", StringComparison.Ordinal))
                return inventory.Contains(ThirstingBlade);
        }
        // Unknown validators may inspect live powers/conditions/invocations. Never fabricate that hero state.
        if (original != hero.GetClassLevel(Warlock) || data.AcquiredSpells.Values.Any(x => x.Count > 0) ||
            data.UnlearnedSpells.Values.Any(x => x.Count > 0) ||
            data.LevelupTrainedInvocations.Any(x => TryOriginal(hero, x.Key, out var level) && level > original && x.Value.Count > 0)) return false;
        if (!inventory.Where(x => x != invocation).OrderBy(x => x.Name).SequenceEqual(hero.TrainedInvocations
                .Where(x => x is not InvocationDefinitionCustom && x != invocation).OrderBy(x => x.Name))) return false;
        return complex.Validate(complex, hero).result;
    }

    internal static bool TryPendingValidator(RulesetCharacterHero hero, InvocationDefinition invocation, out bool valid)
    {
        valid = false;
        if (!TrySelection(hero, out var data, out var selection)) return false;
        try { valid = Validators(data, invocation, selection.Original, Inventory(data, selection.Original, false)); }
        catch (InvalidOperationException) { }
        return true;
    }

    private static bool HasPact(RulesetCharacterHero hero, FeatureDefinition pact, IReadOnlyList<InvocationDefinition> inventory)
    {
        if (inventory.Any(x => x.Name == pact.Name.Replace("FeatureSet", "Invocation") ||
                               (x.GrantedFeature != null && CompressedProgressionDefinitions.Flatten(x.GrantedFeature).Contains(pact)))) return true;
        // Invocation-granted active features can be stale during selection; derive them from the virtual inventory instead.
        var invocationTag = AttributeDefinitions.GetInvocationTag();
        return hero.ActiveFeatures.Where(x => !x.Key.StartsWith(invocationTag, StringComparison.Ordinal))
            .SelectMany(x => x.Value).SelectMany(CompressedProgressionDefinitions.Flatten).Contains(pact);
    }

    private static bool KnowsSpell(CharacterHeroBuildingData data, SpellDefinition spell, int original,
        IReadOnlyList<InvocationDefinition> inventory)
    {
        if (WarlockSpellcastingContext.KnowsAtOriginal(data, spell, original)) return true;
        foreach (var repertoire in data.HeroCharacter.SpellRepertoires)
        {
            if (repertoire.SpellCastingClass != Warlock)
            { if (repertoire.HasKnowledgeOfSpell(spell)) return true; continue; }
            var removedTags = data.HeroCharacter.TrainedInvocations.Except(inventory)
                .Where(x => x is not InvocationDefinitionCustom && x.GrantedFeature != null)
                .SelectMany(x => CompressedProgressionDefinitions.Flatten(x.GrantedFeature)).OfType<FeatureDefinitionPointPool>()
                .Select(x => x.ExtraSpellsTag).Where(x => !string.IsNullOrEmpty(x)).ToArray();
            if (repertoire.ExtraSpellsByTag.Any(x => !removedTags.Contains(x.Key) && x.Value.Contains(spell))) return true;
        }
        return false;
    }

    internal static bool TryPendingSpell(RulesetCharacterHero hero, SpellDefinition spell, out bool known)
    {
        known = false;
        if (!TrySelection(hero, out var data, out var selection)) return false;
        try { known = KnowsSpell(data, spell, selection.Original, Inventory(data, selection.Original, false)); }
        catch (InvalidOperationException) { }
        return true;
    }

    internal static bool TryPendingPact(RulesetCharacterHero hero, FeatureDefinition pact, out bool hasPact)
    {
        hasPact = false;
        if (!TrySelection(hero, out var data, out var selection)) return false;
        try { hasPact = HasPact(hero, pact, Inventory(data, selection.Original, false)); }
        catch (InvalidOperationException) { }
        return true;
    }

    private static bool TrySelection(RulesetCharacterHero hero, out CharacterHeroBuildingData data, out Selection selection)
    {
        data = null; selection = null;
        return Uses(hero) && hero.TryGetHeroBuildingData(out data) && Selections.TryGetValue(hero, out selection);
    }

    private static IReadOnlyList<InvocationDefinition> Inventory(CharacterHeroBuildingData data, int original, bool unlearn)
    {
        var tx = Transaction(data, original);
        var known = tx.Before(original, (x, n, inventory) => Prerequisites(data, x, n, inventory)).ToList();
        var e = tx.Events.Last();
        if (!unlearn)
        {
            foreach (var removed in e.Removed) known.Remove(removed);
            foreach (var added in e.Added) if (!known.Contains(added)) known.Add(added);
        }
        return known;
    }

    internal static bool TryVirtualKnown(RulesetCharacterHero hero, out IReadOnlyList<InvocationDefinition> known)
    {
        known = null;
        if (!TrySelection(hero, out var data, out var selection)) return false;
        try { known = Inventory(data, selection.Original, selection.Unlearn); }
        catch (InvalidOperationException) { known = Array.Empty<InvocationDefinition>(); }
        return true;
    }

    private static bool ValidateEdit(CharacterHeroBuildingData data, InvocationDefinition invocation, string tag, bool unlearn)
    {
        if (!TryOriginal(data.HeroCharacter, tag, out var original)) return true;
        var type = unlearn ? PointsPoolType.InvocationUnlearn : PointsPoolType.Invocation;
        if (!data.PointPoolStacks[type].ActivePools.TryGetValue(tag, out var pool) || pool.RemainingPoints <= 0) return false;
        try
        {
            var tx = Transaction(data, original, original, unlearn ? null : invocation, unlearn ? invocation : null);
            CheckResourceChoices(tx);
            tx.Replay(false, original, (x, n, inventory) => Prerequisites(data, x, n, inventory));
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    private static void InternalReset(Action action)
    { internalReset++; try { action(); } finally { internalReset--; } }

    internal static void BeforeChange(CharacterBuildingManager manager, CharacterHeroBuildingData data, string tag, bool clearCurrent = false)
    {
        if (internalReset > 0 || !TryOriginal(data.HeroCharacter, tag, out var original)) return;
        Finalizations.Remove(data.HeroCharacter);
        InternalReset(() =>
        {
            foreach (var e in Transaction(data).InvalidatedBy(original, clearCurrent).OrderByDescending(x => x.Original))
            {
                var key = e.Origin;
                if (data.LevelupTrainedInvocations.TryGetValue(key, out var selected))
                {
                    ClearChoiceSelections(data, selected);
                    manager.UntrainInvocations(data, key);
                }
                if (e.Original > original) data.UnlearnedInvocations.Remove(key);
            }
        });
        Configure(data);
    }

    internal static bool BeforeUntrain(CharacterBuildingManager manager, CharacterHeroBuildingData data, InvocationDefinition invocation, string tag)
    {
        if (invocation is InvocationDefinitionCustom || !TryOriginal(data.HeroCharacter, tag, out _)) return true;
        if (!Chosen(data.LevelupTrainedInvocations, tag).Contains(invocation)) return false;
        BeforeChange(manager, data, tag);
        ClearChoiceSelections(data, new[] { invocation });
        return true;
    }

    private static void ValidateComplete(CharacterHeroBuildingData data, bool includeCustom = false)
    {
        var choices = data.LevelupTrainedInvocations.SelectMany(entry => entry.Value.Where(x => x is not InvocationDefinitionCustom)
            .Select(x => (entry.Key, Invocation: x))).ToArray();
        for (var i = 0; i < choices.Length; i++)
        for (var j = i + 1; j < choices.Length; j++)
            if ((TryOriginal(data.HeroCharacter, choices[i].Key, out _) || TryOriginal(data.HeroCharacter, choices[j].Key, out _)) &&
                ChoiceChannels(choices[i].Invocation).Intersect(ChoiceChannels(choices[j].Invocation)).Any())
                throw new InvalidOperationException("Invocation choices share a native transient pool; undo one choice before continuing.");
        var tx = Transaction(data);
        CheckResourceChoices(tx);
        var frames = tx.Prepare((x, n, inventory) => Prerequisites(data, x, n, inventory));
        var known = frames.Count == 0 ? data.HeroCharacter.TrainedInvocations.ToList() : frames.Last().After.ToList();
        if (data.UnlearnedInvocations.Any(x => !TryOriginal(data.HeroCharacter, x.Key, out _) && x.Value.Any(y => y is not InvocationDefinitionCustom)))
            throw new InvalidOperationException("Unrecognized ordinary invocation replacement origin.");
        // Feat selections remain outside the class journal; prevent a cross-origin duplicate at acceptance.
        foreach (var entry in data.LevelupTrainedInvocations.Where(x => !TryOriginal(data.HeroCharacter, x.Key, out _)))
            foreach (var item in entry.Value.Where(x => x is not InvocationDefinitionCustom))
            {
                if (known.Contains(item) || !Prerequisites(data, item, WarlockProgressionContext.EffectiveLevel(data.HeroCharacter), known))
                    throw new InvalidOperationException("Conflicting or unsupported feat-origin invocation choice: " + item.Name);
                known.Add(item);
            }
        if (includeCustom) ValidateCustomPools(data);
        // Custom pools remain native. Refuse a chain that grouped native finalization would misapply.
        var customAdded = new HashSet<InvocationDefinition>();
        foreach (var entry in data.LevelupTrainedInvocations)
            customAdded.UnionWith(entry.Value.OfType<InvocationDefinitionCustom>());
        if (data.UnlearnedInvocations.Values.SelectMany(x => x).OfType<InvocationDefinitionCustom>().Any(customAdded.Contains))
            throw new InvalidOperationException("EldritchSurge/custom invocation replacement chains require a separate pool adapter; cancel this chain.");
    }

    internal static bool CanAccept(CharacterHeroBuildingData data, out string reason)
    {
        reason = string.Empty;
        if (!Uses(data.HeroCharacter)) return true;
        try { ValidateComplete(data); return true; }
        catch (InvalidOperationException ex) { reason = ex.Message; return false; }
    }

    private static void ValidateCustomPools(CharacterHeroBuildingData data)
    {
        var hero = data.HeroCharacter;
        var actual = hero.GetClassLevel(Warlock);
        var sources = new List<(string Tag, IEnumerable<FeatureUnlockByLevel> Unlocks)> { (BaseTag(hero), Warlock.FeatureUnlocks) };
        if (hero.ClassesAndSubclasses.TryGetValue(Warlock, out var sub))
            sources.Add((AttributeDefinitions.GetSubclassTag(Warlock, actual, sub), sub.FeatureUnlocks));
        var pools = sources.SelectMany(source => source.Unlocks.Where(x => x.Level == actual)
            .SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionCustomInvocationPool>()
            .Where(x => x.PoolType != null).Select(pool =>
            {
                var original = WarlockProgressionContext.OriginalCustomPoolLevel(pool, actual);
                var tag = source.Tag + (original > 5 ? "Warlock12Original" + original.ToString("D2") : string.Empty);
                return (Tag: tag, Original: original, Pool: pool);
            })).ToArray();
        var standard = Transaction(data).Prepare((x, n, inventory) => Prerequisites(data, x, n, inventory));
        foreach (var entry in data.LevelupTrainedInvocations.Concat(data.UnlearnedInvocations))
        {
            var allowed = pools.Where(x => x.Tag == entry.Key).Select(x => x.Pool.PoolType).ToArray();
            if (allowed.Length > 0 && entry.Value.OfType<InvocationDefinitionCustom>().Any(x => !allowed.Contains(x.PoolType)))
                throw new InvalidOperationException("Custom invocation selected from another pool type.");
        }
        foreach (var type in pools.GroupBy(x => x.Pool.PoolType))
        {
            var events = type.GroupBy(x => (x.Tag, x.Original)).OrderBy(x => x.Key.Original).Select(group =>
                new WarlockInvocationTransaction<InvocationDefinition>.Event(group.Key.Tag, group.Key.Original,
                    group.Where(x => !x.Pool.IsUnlearn).Sum(x => x.Pool.Points), group.Where(x => x.Pool.IsUnlearn).Sum(x => x.Pool.Points),
                    data.UnlearnedInvocations.TryGetValue(group.Key.Tag, out var removed) ? removed.OfType<InvocationDefinitionCustom>().Where(x => x.PoolType == type.Key) : Enumerable.Empty<InvocationDefinitionCustom>(),
                    data.LevelupTrainedInvocations.TryGetValue(group.Key.Tag, out var added) ? added.OfType<InvocationDefinitionCustom>().Where(x => x.PoolType == type.Key) : Enumerable.Empty<InvocationDefinitionCustom>()));
            // Validate each independent custom pool with the same production engine. Runtime chain application stays guarded.
            new WarlockInvocationTransaction<InvocationDefinition>(hero.TrainedInvocations.OfType<InvocationDefinitionCustom>()
                .Where(x => x.PoolType == type.Key), events).Prepare((item, original, _) =>
                    CustomPrerequisites(data, (InvocationDefinitionCustom)item, original,
                        standard.LastOrDefault(x => x.Event.Original <= original)?.After ??
                        hero.TrainedInvocations.Where(x => x is not InvocationDefinitionCustom).ToList()));
        }
    }

    private static bool CustomPrerequisites(CharacterHeroBuildingData data, InvocationDefinitionCustom invocation,
        int original, IReadOnlyList<InvocationDefinition> standard)
    {
        var hero = data.HeroCharacter;
        var requiredClass = invocation.PoolType.RequireClassLevels;
        var rank = requiredClass == null ? hero.TryGetAttributeValue(AttributeDefinitions.CharacterLevel) :
            requiredClass == Warlock.Name || (hero.ClassesAndSubclasses.TryGetValue(Warlock, out var sub) && sub.Name == requiredClass)
                ? original : InvocationPoolTypeCustom.GetClassOrSubclassLevel(hero, requiredClass);
        return invocation.RequiredLevel <= rank &&
               (!invocation.RequiredKnownSpell || KnowsSpell(data, invocation.RequiredKnownSpell, original, standard)) &&
               (!invocation.RequiredPact || HasPact(hero, invocation.RequiredPact, standard));
    }

    private static void ClearChoiceSelections(CharacterHeroBuildingData data, IEnumerable<InvocationDefinition> invocations)
    {
        foreach (var invocation in invocations.Where(x => x.GrantedFeature != null))
            foreach (var pool in CompressedProgressionDefinitions.Flatten(invocation.GrantedFeature).OfType<FeatureDefinitionPointPool>())
            {
                var extra = pool.ExtraSpellsTag;
                var tags = string.IsNullOrEmpty(extra) ? new[] { AttributeDefinitions.GetInvocationTag() } :
                    new[] { AttributeDefinitions.GetInvocationTag(), BaseTag(data.HeroCharacter) + extra, BaseTag(data.HeroCharacter) + extra + extra };
                foreach (var tag in tags)
                {
                    if (pool.PoolType is PointsPoolType.Spell or PointsPoolType.CantripOrSpell) data.AcquiredSpells.Remove(tag);
                    if (pool.PoolType is PointsPoolType.Cantrip or PointsPoolType.CantripOrSpell) data.AcquiredCantrips.Remove(tag);
                }
            }
    }

    private sealed class Finalization
    {
        internal WarlockInvocationTransaction<InvocationDefinition> Transaction;
        internal HashSet<List<InvocationDefinition>> OwnedLists;
        internal List<InvocationDefinition> Before;
        internal List<(RulesetSpellRepertoire Repertoire, Dictionary<string, List<SpellDefinition>> Extra)> Extra;
        internal bool Touched;
        internal string Origin;
        internal void Rollback(RulesetCharacterHero hero)
        {
            if (!Touched) return;
            hero.TrainedInvocations.Clear(); hero.TrainedInvocations.AddRange(Before);
            foreach (var (repertoire, extra) in Extra)
            {
                repertoire.ExtraSpellsByTag.Clear();
                foreach (var entry in extra) repertoire.ExtraSpellsByTag.Add(entry.Key, entry.Value.ToList());
            }
            Touched = false;
        }
    }

    internal static bool BeginFinalize(RulesetCharacterHero hero)
    {
        if (!Uses(hero)) return true;
        if (FailedFinalizations.Contains(hero))
        { Main.Error("Warlock invocation finalization previously failed. Cancel/reload the test hero; retry is blocked."); return false; }
        try
        {
            if (!adapterAvailable) throw new InvalidOperationException("Native invocation finalization adapter is unavailable.");
            var data = hero.GetHeroBuildingData();
            ValidateComplete(data, true);
            if (Finalizations.TryGetValue(hero, out var previous))
            {
                if (previous.Origin == BaseTag(hero)) throw new InvalidOperationException("Duplicate invocation finalization.");
                Finalizations.Remove(hero);
            }
            var tx = Transaction(data);
            tx.Prepare((x, n, inventory) => Prerequisites(data, x, n, inventory));
            var owned = data.LevelupTrainedInvocations.Concat(data.UnlearnedInvocations)
                .Where(x => TryOriginal(hero, x.Key, out _)).Select(x => x.Value).ToArray();
            if (owned.Any(x => x.Any(y => y is InvocationDefinitionCustom))) throw new InvalidOperationException("Mixed invocation origin pool.");
            Finalizations.Add(hero, new Finalization { Origin = BaseTag(hero), Transaction = tx, OwnedLists = new HashSet<List<InvocationDefinition>>(owned) });
            return true;
        }
        catch (InvalidOperationException ex)
        { Main.Error("Warlock invocation transaction rejected before finalization: " + ex.Message); return false; }
    }

    private static void ApplyOrPass(RulesetCharacterHero hero, List<InvocationDefinition> invocations, bool unlearn)
    {
        if (!Finalizations.TryGetValue(hero, out var state) || !state.OwnedLists.Contains(invocations))
        {
            if (unlearn) hero.UnlearnInvocations(invocations); else hero.TrainInvocations(invocations);
            return;
        }
        state.Transaction.ApplyPrepared(e =>
        {
            if (!state.Touched)
            {
                state.Before = hero.TrainedInvocations.ToList();
                state.Extra = hero.SpellRepertoires.Select(r => (r, r.ExtraSpellsByTag.ToDictionary(x => x.Key, x => x.Value.ToList()))).ToList();
                state.Touched = true;
            }
            hero.UnlearnInvocations(e.Removed.ToList());
            // Native cleanup only covers nested pools; 2024 Tome grants its pool directly.
            foreach (var removed in e.Removed.Where(x => x.GrantedFeature is FeatureDefinitionPointPool))
            {
                var pool = (FeatureDefinitionPointPool)removed.GrantedFeature;
                if (!string.IsNullOrEmpty(pool.ExtraSpellsTag))
                    foreach (var repertoire in hero.SpellRepertoires.Where(x => x.SpellCastingClass == Warlock))
                        repertoire.ExtraSpellsByTag.Remove(pool.ExtraSpellsTag);
            }
            hero.TrainInvocations(e.Added.ToList());
        }, () => state.Rollback(hero));
    }
    private static void UnlearnOrdered(RulesetCharacterHero hero, List<InvocationDefinition> invocations) => ApplyOrPass(hero, invocations, true);
    private static void TrainOrdered(RulesetCharacterHero hero, List<InvocationDefinition> invocations) => ApplyOrPass(hero, invocations, false);

    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.FinalizeCharacter))]
    internal static class FinalizationPatch
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var train = AccessTools.Method(typeof(RulesetCharacterHero), nameof(RulesetCharacterHero.TrainInvocations));
            var unlearn = AccessTools.Method(typeof(RulesetCharacterHero), nameof(RulesetCharacterHero.UnlearnInvocations));
            adapterAvailable = code.Count(x => x.Calls(train)) == 1 && code.Count(x => x.Calls(unlearn)) == 1;
            if (!adapterAvailable) return code;
            foreach (var instruction in code)
            {
                if (instruction.Calls(train)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(WarlockInvocationContext), nameof(TrainOrdered)); }
                else if (instruction.Calls(unlearn)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(WarlockInvocationContext), nameof(UnlearnOrdered)); }
            }
            return code;
        }
        internal static Exception Finalizer(RulesetCharacterHero hero, Exception __exception)
        {
            if (__exception != null && Finalizations.TryGetValue(hero, out var state))
            {
                state.Rollback(hero);
                FailedFinalizations.Add(hero);
                Main.Error("Warlock invocation finalization failed; invocation snapshot restored. Cancel/reload: other native level-up state is not transactional.");
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.IsInvocationTrained))]
    internal static class VirtualTrainedPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, InvocationDefinition invocation, ref bool __result)
        {
            if (invocation is InvocationDefinitionCustom || !TryVirtualKnown(heroBuildingData.HeroCharacter, out var known)) return true;
            __result = known.Contains(invocation); return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.IsInvocationKnownOrTrained))]
    internal static class VirtualKnownPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, InvocationDefinition invocation, ref bool __result) =>
            VirtualTrainedPatch.Prefix(heroBuildingData, invocation, ref __result);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UntrainInvocations))]
    internal static class ResetAllPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData, string tag) =>
            BeforeChange(__instance, heroBuildingData, tag);
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) { if (internalReset == 0) Configure(heroBuildingData); }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UndoUnlearnInvocations))]
    internal static class UndoAllPatch
    {
        internal static void Prefix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData, string tag) =>
            BeforeChange(__instance, heroBuildingData, tag, true);
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => Configure(heroBuildingData);
    }
}
