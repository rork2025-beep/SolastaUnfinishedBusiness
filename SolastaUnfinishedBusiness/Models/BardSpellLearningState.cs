using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Production selection model: ordinary spells, cantrips and extra-tag bonus spells stay distinct.
internal sealed class BardSpellLearningState<T>
{
    internal readonly IReadOnlyList<T> Ordinary;
    internal readonly IReadOnlyList<T> Cantrips;
    internal readonly IReadOnlyList<T> Secrets;
    internal BardSpellLearningState(IEnumerable<T> ordinary, IEnumerable<T> cantrips, IEnumerable<T> secrets, bool allowExistingOverlap = false)
    {
        Ordinary = Array.AsReadOnly(ordinary.ToArray()); Cantrips = Array.AsReadOnly(cantrips.ToArray());
        Secrets = Array.AsReadOnly(secrets.ToArray());
        if (Ordinary.Distinct().Count() != Ordinary.Count || Cantrips.Distinct().Count() != Cantrips.Count ||
            Secrets.Distinct().Count() != Secrets.Count || (!allowExistingOverlap && All.Distinct().Count() != All.Count()))
            throw new InvalidOperationException("Duplicate Bard spell across selection origins.");
    }
    internal static List<TStep> Ordered<TStep>(IEnumerable<(TStep Step, int Original, int Phase, string Origin)> source) =>
        source.OrderBy(x => x.Original).ThenBy(x => x.Phase).ThenBy(x => x.Origin, StringComparer.Ordinal).Select(x => x.Step).ToList();

    internal IEnumerable<T> All => Ordinary.Concat(Cantrips).Concat(Secrets);
    internal BardSpellLearningState<T> LearnOrdinary(IEnumerable<T> removed, IEnumerable<T> added)
    {
        var additions = added.ToArray();
        if (additions.Any(x => Cantrips.Contains(x) || Secrets.Contains(x)))
            throw new InvalidOperationException("Bard already knows this bonus spell or cantrip.");
        return new BardSpellLearningState<T>(KnownSpellsProgressionPlan.ApplyEvent(Ordinary, removed, additions), Cantrips, Secrets, true);
    }
    internal BardSpellLearningState<T> LearnCantrips(IEnumerable<T> added)
    {
        var additions = added.ToArray();
        if (additions.Any(All.Contains)) throw new InvalidOperationException("Bard already knows this cantrip.");
        return new(Ordinary, Cantrips.Concat(additions), Secrets, true);
    }
    internal BardSpellLearningState<T> LearnSecrets(IEnumerable<T> added)
    {
        var additions = added.ToArray();
        if (additions.Any(All.Contains)) throw new InvalidOperationException("Bard already knows this bonus spell.");
        return new(Ordinary, Cantrips, Secrets.Concat(additions), true);
    }
}

// A building-data object may be reused between advancements. Deduplicate within one actual Bard level only.
internal sealed class BardSpellGrantLedger
{
    private string level;
    private readonly HashSet<string> tags = new();
    internal bool HasStarted(string currentLevel) => level == currentLevel && tags.Count > 0;
    internal bool Grant(string currentLevel, string tag)
    {
        if (level != currentLevel) { level = currentLevel; tags.Clear(); }
        return tags.Add(tag);
    }
}
