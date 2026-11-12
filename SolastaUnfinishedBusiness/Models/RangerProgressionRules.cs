using System;
using System.Collections.Generic;

namespace SolastaUnfinishedBusiness.Models;

internal static class RangerProgressionRules
{
    internal const int MaxClassLevel = 12;
    internal static bool IsCapped(bool active, int actual) => active && actual >= MaxClassLevel;
    internal static int EffectiveLevel(bool active, int actual) =>
        active && actual <= MaxClassLevel ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    // The existing UB SINGLE half-caster slot/points tables round up; legacy Half starts at2.
    internal static int CasterContribution(int effective, bool roundUp)
    {
        effective = Math.Max(0, Math.Min(20, effective));
        return effective <= 1 && !roundUp ? 0 : (effective + 1) / 2;
    }
    internal static bool KeepLegacySelection(bool revised, bool legacyChoice, bool customEquivalent) =>
        !legacyChoice || !revised && !customEquivalent;
    internal static bool IsFreeMarkUse(bool powerAction, bool markDefinition) => powerAction && markDefinition;
    internal static int MarkUses(int effective) => effective < 5 ? 2 : effective < 9 ? 3 : effective < 13 ? 4 : effective < 17 ? 5 : 6;
    internal static int TirelessBonus(int effective) => (effective + 1) / 2;
    internal static bool RequiresConcentration(bool original, bool mark, int effective, bool relentless, bool noConcentration) =>
        original && !(mark && effective >= 13 && relentless && noConcentration);
    internal static bool ProtectConcentration(int effective, bool relentless, bool noConcentration) =>
        effective >= 13 && relentless && !noConcentration;
    internal static bool PreciseAdvantage(int effective, bool markedByMe) => effective >= 17 && markedByMe;
    internal static bool UpgradedCompanion(int effective) => effective >= 11;
    internal static bool IsOriginTag(string tag, string parent) => tag != null && parent != null &&
        (tag == parent || (tag.StartsWith(parent, StringComparison.Ordinal) && !char.IsDigit(tag[parent.Length])));
    internal static bool IsClassCondition(string name) => name.StartsWith("ConditionRanger", StringComparison.Ordinal) ||
        name is "ConditionMarkedByHunter";
    internal static bool ShouldTransferCondition(bool active, int actual, bool ownSource, bool classCondition) =>
        !active || actual == 0 || !ownSource || !classCondition;
}

internal sealed class RangerMarkActionLedger
{
    private bool started;
    private bool completed;
    internal bool TryBegin(bool valid)
    {
        if (started || !valid) return false;
        started = true; return true;
    }
    internal bool TryComplete(bool valid)
    {
        if (!started || completed || !valid) return false;
        completed = true; return true;
    }
}

internal sealed class RangerSpellGrantLedger
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
