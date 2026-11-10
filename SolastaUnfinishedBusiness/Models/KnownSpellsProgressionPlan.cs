using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Known casters have cumulative totals and OPTIONAL replacement events, unlike Wizard scribing.
internal sealed class KnownSpellsProgressionPlan
{
    internal readonly int[] Known;
    internal readonly int[] Replacements;

    internal KnownSpellsProgressionPlan(IEnumerable<int> known, IEnumerable<int> replacements)
    {
        Known = known.ToArray();
        Replacements = replacements.ToArray();
        if (Known.Length != 20 || Replacements.Length != 20 || Known.Any(x => x < 0) || Replacements.Any(x => x < 0) ||
            Known.Skip(1).Where((value, index) => value < Known[index]).Any())
            throw new ArgumentException("Expected twenty nondecreasing known totals and nonnegative replacement events.");
    }

    internal int Learned(int original) => Known[original - 1] - (original == 1 ? 0 : Known[original - 2]);
    internal IEnumerable<(int Original, int Learned, int Replaced)> Events(int actual) =>
        SpellcastingProgressionPlan.OriginalLevels(actual).Select(x => (x, Learned(x), Replacements[x - 1]));
    internal List<int> CompressedReplacements() => Enumerable.Range(1, 20)
        .Select(actual => actual <= 12 ? Events(actual).Sum(x => x.Replaced) : Replacements[actual - 1]).ToList();

    // Used by runtime selection and finalization: later events can replace a spell learned in an earlier event.
    internal static List<T> ApplyEvent<T>(IEnumerable<T> known, IEnumerable<T> removed, IEnumerable<T> added)
    {
        var result = known.ToList();
        foreach (var item in removed)
            if (!result.Remove(item)) throw new InvalidOperationException("Replacing a spell not known at this original event.");
        foreach (var item in added)
        {
            if (result.Contains(item)) throw new InvalidOperationException("Duplicate known spell acquisition.");
            result.Add(item);
        }
        return result;
    }

    // Native UI groups all unlearning before all learning. Reorder only class events, retaining unrelated steps.
    internal static List<T> OrderSteps<T>(IReadOnlyList<(T Step, int Original, int Phase)> source)
    {
        var position = source.ToList().FindIndex(x => x.Original > 0);
        if (position < 0) return source.Select(x => x.Step).ToList();
        var events = source.Where(x => x.Original > 0).OrderBy(x => x.Original).ThenBy(x => x.Phase).Select(x => x.Step);
        var result = source.Where(x => x.Original == 0).Select(x => x.Step).ToList();
        result.InsertRange(position, events); return result;
    }
    internal static string[] SupplementaryTags(string parent, string prefix, IEnumerable<string> tags) =>
        tags.Where(x => x.StartsWith(parent + prefix, StringComparison.Ordinal)).Distinct().OrderBy(x => x).ToArray();

    internal static List<int> RetainedCantrips(IEnumerable<int> source)
    {
        var highest = 0;
        return source.Select(x => highest = Math.Max(highest, x)).ToList();
    }
}
