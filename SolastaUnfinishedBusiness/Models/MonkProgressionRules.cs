using System.Collections.Generic;

namespace SolastaUnfinishedBusiness.Models;

internal static class MonkProgressionRules
{
    internal const int MaxClassLevel = 12;
    internal const int BodyAndMindBonus = 4;
    internal static bool IsCapped(bool active, int actual) => active && actual >= MaxClassLevel;
    internal static int EffectiveLevel(bool active, int actual) =>
        active && actual <= MaxClassLevel ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
}

internal sealed class BodyAndMindGrantState
{
    private readonly Dictionary<string, bool> grants = new();

    internal int Apply(string tag)
    {
        if (grants.TryGetValue(tag, out var applied) && applied)
        {
            return 0;
        }

        grants[tag] = true;
        return MonkProgressionRules.BodyAndMindBonus;
    }

    internal int Remove(string tag)
    {
        // First removal may follow loading a saved hero whose maximum already includes this bonus.
        if (grants.TryGetValue(tag, out var applied) && !applied)
        {
            return 0;
        }

        grants[tag] = false;
        return -MonkProgressionRules.BodyAndMindBonus;
    }
}
