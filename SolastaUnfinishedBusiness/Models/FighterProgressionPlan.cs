using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Pure planning layer: never writes into input definitions or deduplicates acquisition events.
internal sealed class FighterProgressionPlan<T>
{
    internal static readonly int[] OriginalLevels = { 0, 1, 2, 3, 4, 5, 7, 9, 11, 13, 15, 17, 20 };
    internal readonly List<(T Feature, int OriginalLevel, int OriginalIndex, int Level)> Events;

    internal FighterProgressionPlan(IEnumerable<(T Feature, int Level)> source)
    {
        Events = source.Select((x, i) => (x.Feature, OriginalLevel: x.Level, OriginalIndex: i,
                Level: CompressedLevel(x.Level)))
            .OrderBy(x => x.OriginalLevel).ThenBy(x => x.OriginalIndex).ToList();
    }

    internal static int CompressedLevel(int level)
    {
        if (level < 1 || level > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        return Array.FindIndex(OriginalLevels, x => x >= level);
    }

    internal static int EffectiveLevel(int level) => OriginalLevels[Math.Max(0, Math.Min(12, level))];
}

// Latches opt-in once per process, including the disabled state. No hot mutation of live characters.
internal sealed class FighterProgressionInitialization
{
    private bool initialized;

    internal bool ShouldInitialize(bool enabled)
    {
        if (initialized)
        {
            return false;
        }

        initialized = true;
        return enabled;
    }
}
