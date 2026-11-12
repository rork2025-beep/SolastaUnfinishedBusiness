using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Independent original custom-pool acquisitions. No merging by actual level or pool type.
internal static class RangerInvocationPlan
{
    internal static string Origin(string owner, int original, int index) => owner + "Original" + original.ToString("D2") + "Event" + index;
    internal static void Validate<T>(IEnumerable<T> known,
        IEnumerable<(string Origin, string Type, int Awards, IEnumerable<T> Selected)> events, Func<T, string> typeOf)
    {
        var inventory = new HashSet<T>(known);
        var origins = new HashSet<string>();
        foreach (var e in events)
        {
            var selected = e.Selected.ToArray();
            if (!origins.Add(e.Origin) || e.Awards < 0 || selected.Length != e.Awards ||
                selected.Any(x => typeOf(x) != e.Type || !inventory.Add(x)))
                throw new InvalidOperationException("Incomplete/duplicate custom invocation origin: " + e.Origin);
        }
    }
}
