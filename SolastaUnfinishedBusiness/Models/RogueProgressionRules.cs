namespace SolastaUnfinishedBusiness.Models;

internal static class RogueProgressionRules
{
    internal const int MaxClassLevel = 12;
    internal static bool IsCapped(bool active, int actual) => active && actual >= MaxClassLevel;
    internal static int EffectiveLevel(bool active, int actual) =>
        active && actual <= MaxClassLevel ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    internal static int CunningStrikeChoices(int effectiveLevel) => effectiveLevel >= 11 ? 2 : 1;
}
