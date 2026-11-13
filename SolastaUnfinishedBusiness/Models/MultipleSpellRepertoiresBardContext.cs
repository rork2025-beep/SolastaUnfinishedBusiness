using System;
using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using Plan = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using Secrets = SolastaUnfinishedBusiness.Models.BardMagicalSecretsPlan;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class MultipleSpellRepertoiresContext
{
    private static readonly List<Secrets.Source> BardSources = new();
    internal static IReadOnlyList<Secrets.Source> BardSecretsCoverage => definitions == null
        ? Array.Empty<Secrets.Source>() : BardSources.AsReadOnly();
    private static bool CaptureBardGrant(Definition definition, Source source, out Plan.Grant grant)
    {
        grant = null;
        if (definition.Owner.Name != "Bard") return false;
        Secrets.Source spec;
        if (source.Feature is FeatureDefinitionPointPool pool && pool.ExtraSpellsTag == "MagicalSecrets")
        {
            var spells = Spells(pool.SpellListOverride ?? definition.Casting.SpellListDefinition)
                .Where(s => !pool.RitualOnly || DatabaseHelper.GetDefinition<SpellDefinition>(s.Id).Ritual);
            spec = new Secrets.Source(source.Id + ":" + pool.Name, pool.Name, source.Owner, source.Original,
                Secrets.SourceKind.Selection, spells, pool.PoolAmount, pool.MinSpellLevel,
                Plan.ChoiceMaximum(pool.MaxSpellLevel, pool.PoolType == HeroDefinitions.PointsPoolType.Cantrip));
        }
        else if (source.Feature is FeatureDefinitionMagicAffinity affinity && affinity.ExtendedSpellList &&
                 affinity.Name.StartsWith("MagicAffinityBardMagicalSecrets", StringComparison.Ordinal))
            spec = new Secrets.Source(source.Id + ":" + affinity.Name, affinity.Name, source.Owner, source.Original,
                Secrets.SourceKind.ExpandedList, Spells(affinity.ExtendedSpellList));
        else return false;
        grant = Secrets.Grant(spec, definition.Profile.MaximumCircles);
        BardSources.Add(spec); return true;
    }
    private static void RegisterTransactionTags(Session session, Plan.Transaction transaction)
    {
        foreach (var key in session.Tags.Where(p => p.Value.Transaction == transaction.Id).Select(p => p.Key).ToArray()) session.Tags.Remove(key);
        var owner = transaction.ClassId; var feature = transaction.Definition.FeatureId;
        foreach (var e in transaction.Events)
        {
            var tag = "MultiRepertoire:" + transaction.Id.Length + ":" + transaction.Id + ":" +
                      (owner == "Bard" ? "BardRevision" + transaction.BardRevision + ":" : string.Empty) + e.Original;
            session.Tags.Add(tag, new Tag(transaction.Id, owner, feature, e.Original, revision: transaction.BardRevision));
            foreach (var grant in transaction.Definition.Grants.Where(g =>
                         (g.Kind is Plan.GrantKind.BonusChoice or Plan.GrantKind.MysticArcanum) && g.Original == e.Original))
                session.Tags.Add(tag + ":" + grant.Id.Length + ":" + grant.Id,
                    new Tag(transaction.Id, owner, feature, e.Original, grant.Id, transaction.BardRevision));
        }
    }
    private static void SubmitBardStep(CharacterHeroBuildingData data, Session session, Tag origin, string tag)
    {
        var transaction = session.Transactions[origin.Transaction];
        var token = transaction.BardSteps.Single(s => s.Original == origin.Original && s.GrantId == origin.Grant);
        string[] Chosen(Dictionary<string, List<SpellDefinition>> map) => map.TryGetValue(tag, out var spells) ? spells.Select(s => s.Name).ToArray() : Array.Empty<string>();
        var added = Chosen(data.AcquiredSpells); var cantrips = Chosen(data.AcquiredCantrips);
        if (!data.CanUnlearn && Chosen(data.UnlearnedSpells).Length > 0) throw new InvalidOperationException("Bard replacement is disabled by the native advancement policy.");
        transaction.SelectBardStep(token, origin.Grant == null ? added : added.Concat(cantrips), Chosen(data.UnlearnedSpells),
            origin.Grant == null ? cantrips : Array.Empty<string>());
        RebuildBardNative(data, session, transaction);
    }
    // Only this transaction's entries are removed. Recreate the valid prefix, including ordinary
    // choices before a Secrets edit. Later dependent choices disappear, never their award budgets.
    private static void RebuildBardNative(CharacterHeroBuildingData data, Session session, Plan.Transaction transaction)
    {
        foreach (var key in session.Tags.Where(p => p.Value.Transaction == transaction.Id).Select(p => p.Key).ToArray())
        {
            data.AcquiredSpells.Remove(key); data.AcquiredCantrips.Remove(key); data.UnlearnedSpells.Remove(key);
            foreach (var stack in data.PointPoolStacks.Values) stack.ActivePools.Remove(key);
        }
        RegisterTransactionTags(session, transaction);
        foreach (var step in transaction.BardSteps)
        {
            var tag = session.Tags.Single(p => p.Value.Transaction == transaction.Id && p.Value.Original == step.Original && p.Value.Grant == step.GrantId).Key;
            transaction.BardSelections.TryGetValue(step.Key, out var selected);
            if (selected != null)
            {
                var added = selected.Added.Select(DatabaseHelper.GetDefinition<SpellDefinition>).ToArray();
                data.AcquiredSpells[tag] = added.Where(s => s.SpellLevel > 0).ToList();
                data.AcquiredCantrips[tag] = selected.Cantrips.Select(DatabaseHelper.GetDefinition<SpellDefinition>).Concat(added.Where(s => s.SpellLevel == 0)).ToList();
                data.UnlearnedSpells[tag] = selected.Removed.Select(DatabaseHelper.GetDefinition<SpellDefinition>).ToList();
            }
            var grant = step.GrantId == null ? null : transaction.Definition.Grants.Single(g => g.Id == step.GrantId);
            var type = grant == null ? HeroDefinitions.PointsPoolType.Spell : HeroDefinitions.PointsPoolType.CantripOrSpell;
            Set(type, step.Awards + (selected?.Removed.Count ?? 0), selected?.Added.Count ?? 0, grant);
            if (step.GrantId == null)
            {
                if (step.Replacements > 0) Set(HeroDefinitions.PointsPoolType.SpellUnlearn, data.CanUnlearn ? step.Replacements : 0, selected?.Removed.Count ?? 0, null);
                if (step.Cantrips > 0) Set(HeroDefinitions.PointsPoolType.Cantrip, step.Cantrips, selected?.Cantrips.Count ?? 0, null);
            }
            void Set(HeroDefinitions.PointsPoolType poolType, int budget, int used, Plan.Grant bonus)
            {
                // Native stack creation/finalize orchestration belongs to13.7. Never invent a stack.
                if (!data.PointPoolStacks.TryGetValue(poolType, out var stack)) return;
                var pool = new PointPool(0, null, false)
                {
                    MaxPoints = budget, RemainingPoints = Math.Max(0, budget - used),
                    MinSpellLevel = poolType == HeroDefinitions.PointsPoolType.Cantrip ? 0 : bonus == null ? 1 : bonus.Spells.Count == 0 ? 0 : bonus.Spells.Min(s => s.Circle),
                    MaxSpellLevel = poolType == HeroDefinitions.PointsPoolType.Cantrip ? 0 : transaction.Definition.MaximumCircles[step.Original - 1],
                    Description = "Bard original " + step.Original + (bonus == null ? " ordinary" : " source " + bonus.Id)
                };
                if (bonus != null)
                {
                    pool.ExtraSpellsTag = bonus.ExtraSpellsTag;
                    var spec = BardSources.SingleOrDefault(s => s.Id == bonus.Id);
                    if (spec != null)
                    {
                        var source = DatabaseHelper.GetDefinition<FeatureDefinitionPointPool>(spec.Definition);
                        pool.SpellListOverride = source.SpellListOverride ?? definitions["Bard"].Casting.SpellListDefinition;
                        pool.RitualOnly = source.RitualOnly;
                        pool.MaxSpellLevel = Math.Min(pool.MaxSpellLevel, spec.Maximum);
                    }
                }
                stack.ActivePools[tag] = pool;
            }
        }
    }
    internal static bool BackBardStep(RulesetCharacterHero hero, string tag)
    {
        if (!RuntimeEnabled || !hero.TryGetHeroBuildingData(out var data) || !TryTag(data, tag, out var session, out var origin) || origin.Class != "Bard") return false;
        var tx = session.Transactions[origin.Transaction];
        tx.BackBardStep(tx.BardSteps.Single(s => s.Original == origin.Original && s.GrantId == origin.Grant));
        RebuildBardNative(data, session, tx); return true;
    }
    internal static bool CancelBardAdvancement(RulesetCharacterHero hero, string tag)
    {
        if (!RuntimeEnabled || !hero.TryGetHeroBuildingData(out var data) || !TryTag(data, tag, out var session, out var origin) || origin.Class != "Bard") return false;
        session.Transactions[origin.Transaction].Cancel();
        foreach (var key in session.Tags.Where(p => p.Value.Transaction == origin.Transaction).Select(p => p.Key).ToArray())
        {
            data.AcquiredSpells.Remove(key); data.AcquiredCantrips.Remove(key); data.UnlearnedSpells.Remove(key);
            foreach (var stack in data.PointPoolStacks.Values) stack.ActivePools.Remove(key);
            session.Tags.Remove(key);
        }
        // Keep the closed marker until13.7 rolls native history/features back; no partial finalize.
        return true;
    }
    internal static bool FilterBardAvailable(RulesetCharacterHero hero, string tag, List<SpellDefinition> spells)
    {
        if (!RuntimeEnabled || !hero.TryGetHeroBuildingData(out var data) || !TryTag(data, tag, out var session, out var origin) || origin.Class != "Bard") return false;
        var profile = session.Transactions[origin.Transaction].Definition;
        var grant = origin.Grant == null ? null : profile.Grants.Single(g => g.Id == origin.Grant);
        spells.RemoveAll(s => grant == null ? !profile.Allows(s.Name, origin.Original, s.SpellLevel == 0) : !grant.Spells.Any(p => p.Id == s.Name));
        return true;
    }
}
