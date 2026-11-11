using System;
using System.Collections.Generic;

namespace SolastaUnfinishedBusiness.Models;

internal static class ClericProgressionRules
{
    internal const int MaxClassLevel = 12;
    internal static bool IsCapped(bool active, int actual) => active && actual >= MaxClassLevel;
    internal static int EffectiveLevel(bool active, int actual) =>
        active && actual <= MaxClassLevel ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    // Affinities and WIS remain native; only the full class-level term is projected.
    internal static int LegacyPreparedLimit(int effective, int wisdomModifier, int affinityBonus) =>
        Math.Max(1, effective + wisdomModifier + affinityBonus);
    internal static bool IsOriginTag(string tag, string parent) => tag != null && parent != null &&
        (tag == parent || (tag.StartsWith(parent, StringComparison.Ordinal) && !char.IsDigit(tag[parent.Length])));
    internal static bool Use2024ChannelRecovery(bool compressedCleric, bool clericRule, bool paladinRule) =>
        compressedCleric ? clericRule : clericRule || paladinRule;
    internal static int DivineSparkDice(int effective) => effective >= 18 ? 4 : effective >= 13 ? 3 : effective >= 7 ? 2 : 1;
}

// Data objects can be reused by consecutive advancements; deduplicate within an actual Cleric level.
internal sealed class ClericSpellGrantLedger
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
