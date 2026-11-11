using System;
namespace SolastaUnfinishedBusiness.Models;
internal static class WarlockProgressionRules
{
    internal const int MaxClassLevel = 12;
    internal static bool IsCapped(bool active, int actual) => active && actual >= MaxClassLevel;
    internal static int EffectiveLevel(bool active, int actual) => active && actual <= MaxClassLevel
        ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    internal static int CasterLevel(bool active, int actual) => active ? Math.Max(0, Math.Min(20, EffectiveLevel(true, actual))) : actual;
    internal static int PactSpellLevel(int original) => Math.Max(0, Math.Min(5, (original + 1) / 2));
    internal static int InvocationReplacementBudget(bool revised, int original) => original >= (revised ? 2 : 3) ? 1 : 0;
    internal static int MaximumArcanumCircle(int original) => Math.Max(0, Math.Min(9, (original + 1) / 2));
}
