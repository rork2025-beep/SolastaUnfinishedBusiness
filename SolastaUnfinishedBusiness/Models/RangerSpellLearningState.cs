using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Pure production state used at every original Ranger spell event by UI and finalization.
internal sealed class RangerSpellLearningState<T>
{
    internal readonly IReadOnlyList<T> Known;
    internal RangerSpellLearningState(IEnumerable<T> known)
    {
        Known = Array.AsReadOnly(known.ToArray());
        if (Known.Count != Known.Distinct().Count()) throw new InvalidOperationException("Duplicate existing Ranger spell.");
    }
    internal RangerSpellLearningState<T> Apply(IEnumerable<T> removed, IEnumerable<T> added, int awards,
        int replacementBudget, bool complete, Func<T, bool> allowed)
    {
        var removals = removed.ToArray(); var additions = added.ToArray();
        if (awards < 0 || replacementBudget < 0 || removals.Length > replacementBudget || removals.Distinct().Count() != removals.Length ||
            additions.Length > awards + removals.Length || (complete && additions.Length != awards + removals.Length) ||
            additions.Any(x => !allowed(x) || removals.Contains(x))) throw new InvalidOperationException("Incomplete/invalid original Ranger spell event.");
        return new(KnownSpellsProgressionPlan.ApplyEvent(Known, removals, additions));
    }
}
