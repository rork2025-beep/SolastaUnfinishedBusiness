using System;
using System.Collections.Generic;

namespace SolastaUnfinishedBusiness.Models;

internal static class DruidProgressionRules
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
    internal static bool IsStarryCondition(string name) => name != null &&
        (name.StartsWith("ConditionCircleOfTheCosmosArcher", StringComparison.Ordinal) ||
         name.StartsWith("ConditionCircleOfTheCosmosChalice", StringComparison.Ordinal) ||
         name.StartsWith("ConditionCircleOfTheCosmosDragon", StringComparison.Ordinal));
    internal static int FinishShapeUses(int before, int cost, int maximum, bool legacyRefund)
    {
        if (cost <= 0 || before < cost || maximum < 0) throw new InvalidOperationException("Invalid Druid shape resource transaction.");
        return legacyRefund ? maximum : Math.Min(maximum, before - cost);
    }
    internal static int AdditionalWildShapeUses(int effective) => effective >= 17 ? 2 : effective >= 6 ? 1 : 0;
    internal static bool CanSelectForm(int effective, int required, int remaining, int cost) =>
        required > 0 && effective >= required && cost > 0 && remaining >= cost;
    internal static bool CanRegainShape(int remainingShapes, int slotLevel, int remainingPoints, int pointCost, bool pointsMode) =>
        remainingShapes == 0 && slotLevel > 0 && (!pointsMode || remainingPoints >= pointCost);

}

// Data objects can be reused by consecutive advancements; deduplicate within an actual Druid level.
internal sealed class DruidSpellGrantLedger
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

// Successful shape charge/refund is applied at most once, independently of duplicated native callbacks.
internal sealed class DruidShapeUseJournal
{
    private readonly int before;
    private readonly int cost;
    internal bool Finished { get; private set; }
    private bool refunded;
    internal DruidShapeUseJournal(int before, int cost)
    {
        _ = DruidProgressionRules.FinishShapeUses(before, cost, before, false);
        this.before = before; this.cost = cost;
    }
    internal bool TryFinish(int maximum, out int remaining)
    {
        remaining = 0;
        if (Finished) return false;
        remaining = DruidProgressionRules.FinishShapeUses(before, cost, maximum, false);
        Finished = true; return true;
    }
    internal bool TryRefund(int maximum, out int remaining)
    {
        remaining = 0;
        if (!Finished || refunded) return false;
        remaining = DruidProgressionRules.FinishShapeUses(before, cost, maximum, true);
        refunded = true; return true;
    }
}
