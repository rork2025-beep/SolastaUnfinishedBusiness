using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// This schedule comes from FINAL registered custom pools, not from hardcoded Warlock budgets.
// The same planner and transaction are executed by the Inventor UI/finalization adapter and tests.
internal sealed class InventorInfusionPlan<T>
{
    private readonly (int Original, int Awards, int Replacements)[] schedule;
    internal InventorInfusionPlan(IEnumerable<(int Original, int Awards, int Replacements)> events)
    {
        schedule = events.OrderBy(e => e.Original).ToArray();
        if (schedule.Any(e => e.Original is < 1 or > 20 || e.Awards < 0 || e.Replacements < 0) ||
            schedule.Select(e => e.Original).Distinct().Count() != schedule.Length)
            throw new ArgumentException("Invalid Inventor infusion schedule.");
    }
    internal IEnumerable<(int Original, int Awards, int Replacements)> Events(int actual) =>
        schedule.Where(e => SpellcastingProgressionPlan.OriginalLevels(actual).Contains(e.Original));
    internal WarlockInvocationTransaction<T> Transaction(int actual, IEnumerable<T> known, Func<int, string> tag,
        Func<string, IEnumerable<T>> added, Func<string, IEnumerable<T>> removed) => new(known,
        Events(actual).Select(e => new WarlockInvocationTransaction<T>.Event(tag(e.Original), e.Original,
            e.Awards, e.Replacements, removed(tag(e.Original)), added(tag(e.Original)))));
}
