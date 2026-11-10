namespace SolastaUnfinishedBusiness.Models;
internal static class SorcererProgressionRules
{
    internal const int MaxClassLevel = 12;
    internal static bool IsCapped(bool active, int actual) => active && actual >= MaxClassLevel;
    internal static int EffectiveLevel(bool active, int actual) =>
        active && actual <= MaxClassLevel ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
}
