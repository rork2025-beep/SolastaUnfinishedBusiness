using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

internal static class InventorProgressionRules
{
    internal const int MaxClassLevel = 12;
    internal static bool IsCapped(bool active, int actual) => active && actual >= MaxClassLevel;
    internal static int EffectiveLevel(bool active, int actual) => active && actual <= 12
        ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    internal static int CasterContribution(int effective, bool roundUp = true) => (Math.Max(0, Math.Min(20, effective)) + 1) / 2;
    internal static int LegacyPreparedLimit(int effective, int intelligenceModifier, int affinityBonus) => Math.Max(1, effective / 2 + intelligenceModifier + affinityBonus);
    internal static int LearnedAt(int original) => original == 2 ? 4 : original is 6 or 10 or 14 or 18 ? 2 : 0;
    internal static int ReplacementsAt(int original) => original is >= 3 and <= 20 ? 1 : 0;
    internal static int ActiveCapacity(int effective) => effective < 2 ? 0 : 2 + new[] { 6, 10, 14, 18 }.Count(n => effective >= n);
    internal static bool CanInfuse(int active, int maximum, bool duplicate) => active < maximum && !duplicate;
    internal static int CannonLimit(int effective) => effective >= 15 ? 2 : 1;
    internal static bool IsOriginTag(string tag, string parent) => tag != null && parent != null &&
        (tag == parent || (tag.StartsWith(parent, StringComparison.Ordinal) && !char.IsDigit(tag[parent.Length])));
}

internal sealed class InventorSpellGrantLedger
{
    private string level;
    private readonly HashSet<string> tags = new();
    internal bool HasStarted(string currentLevel) => level == currentLevel && tags.Count > 0;
    internal bool Grant(string currentLevel, string tag)
    {
        if (level != currentLevel) { level = currentLevel; tags.Clear(); }
        return tags.Add(tag);
    }
}

// A native power action may be entered again by a callback. The child spell action has its own
// identity and never consumes this journal; no replay of the parent item creation is allowed.
internal sealed class InventorActionLedger
{
    private bool started;
    internal bool TryBegin() { if (started) return false; started = true; return true; }
}
