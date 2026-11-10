using System.Collections.Generic;

namespace SolastaUnfinishedBusiness.Models;

// Pure state/formula helpers shared with executable tests. No character or attribute mutation here.
internal static class BarbarianProgressionRules
{
    internal const int ClassCap = 12;
    internal const int ChampionBonus = 4;
    internal static bool IsCapped(bool active, int actual) => active && actual >= ClassCap;
    internal static int EffectiveLevel(bool active, int actual) =>
        active && actual <= ClassCap ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    internal static int BrutalStrikeChoices(int effectiveLevel) => effectiveLevel >= 17 ? 2 : 1;
}

internal sealed class PrimalChampionGrantState
{
    private readonly Dictionary<string, bool> grants = new();

    internal int Apply(string tag)
    {
        if (grants.TryGetValue(tag, out var applied) && applied)
        {
            return 0;
        }

        grants[tag] = true;
        return BarbarianProgressionRules.ChampionBonus;
    }

    internal int Remove(string tag)
    {
        // An absent entry may be a hero loaded from a save, whose maximum already includes the bonus.
        if (grants.TryGetValue(tag, out var applied) && !applied)
        {
            return 0;
        }

        grants[tag] = false;
        return -BarbarianProgressionRules.ChampionBonus;
    }
}
