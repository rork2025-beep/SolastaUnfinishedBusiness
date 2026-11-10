using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Distinguish cumulative state from per-original-level learning events. No game types.
internal sealed class SpellcastingProgressionPlan
{
    internal readonly int[] Scribed;
    internal SpellcastingProgressionPlan(IEnumerable<int> scribed)
    {
        Scribed = scribed.ToArray();
        if (Scribed.Length < 20 || Scribed.Any(x => x < 0))
            throw new ArgumentException("Expected nonnegative scribing events for original levels 1-20.");
    }
    internal static int Effective(int actual) => WizardProgressionRules.EffectiveLevel(true, actual);
    internal static int CasterLevel(int actual) => Math.Max(0, Math.Min(20, Effective(actual)));
    internal static int MaxSpellLevel(int original) => Math.Min(9, (original + 1) / 2);
    internal static int[] OriginalLevels(int actual)
    {
        if (actual < 1 || actual > 12) throw new ArgumentOutOfRangeException(nameof(actual));
        var previous = actual == 1 ? 0 : Effective(actual - 1);
        return Enumerable.Range(previous + 1, Effective(actual) - previous).ToArray();
    }
    internal static List<T> ProjectRows<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count < 20) throw new ArgumentException("Expected original rows 1-20.");
        return Enumerable.Range(1, 20).Select(actual => source[Effective(actual) - 1]).ToList();
    }
    internal static List<int> ProjectCumulative(IReadOnlyList<int> source)
    {
        if (source == null || source.Count == 0) throw new ArgumentException("Expected a cumulative progression table.");
        return Enumerable.Range(1, 20).Select(actual => source[Math.Min(source.Count - 1, Effective(actual) - 1)]).ToList();
    }
    internal List<int> PrimaryScribedEvents() => Enumerable.Range(1, 20)
        .Select(actual => Scribed[(actual <= 12 ? OriginalLevels(actual)[0] : actual) - 1]).ToList();
    // Simulates native GatherAndSortSpellbooks + first fitting book, without changing inventory.
    internal static bool CanFitSpellbooks(IEnumerable<(string Owner, int Pages)> books, string hero,
        IEnumerable<int> spellLevels)
    {
        var remaining = books.ToList();
        foreach (var cost in spellLevels)
        {
            remaining.Sort((left, right) =>
            {
                if (!string.IsNullOrEmpty(left.Owner) && string.IsNullOrEmpty(right.Owner)) return -1;
                if (string.IsNullOrEmpty(left.Owner) && !string.IsNullOrEmpty(right.Owner)) return 1;
                if (left.Owner != right.Owner)
                {
                    if (left.Owner == hero) return -1;
                    if (right.Owner == hero) return 1;
                }
                return left.Pages.CompareTo(right.Pages);
            });
            var index = remaining.FindIndex(x => x.Pages >= cost);
            if (index < 0) return false;
            remaining[index] = (remaining[index].Owner, remaining[index].Pages - cost);
        }
        return true;
    }
    internal static int LearningBudget(int requested, int available, int reserved) =>
        Math.Max(0, Math.Min(requested, available - reserved));
    internal int LearningAmount(int originalLevel, int additionalPerLevel) =>
        Scribed[originalLevel - 1] + additionalPerLevel;
}
