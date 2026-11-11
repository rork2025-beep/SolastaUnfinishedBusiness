using System;
using System.Collections.Generic;

namespace SolastaUnfinishedBusiness.Models;

internal static class PaladinProgressionRules
{
    internal const int MaxClassLevel = 12;
    internal static bool IsCapped(bool active, int actual) => active && actual >= MaxClassLevel;
    internal static int EffectiveLevel(bool active, int actual) =>
        active && actual <= MaxClassLevel ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    // Native Paladin preparation divides class level by two (integer floor), even in HalfRoundUp casting mode.
    // UB SINGLE-caster slot/Spell Points tables round up in both variants; Half starts at2.
    // This deliberately does not implement the floor-based mixed-caster aggregation (still blocked).
    internal static int CasterContribution(int effective, bool roundUp)
    {
        effective = Math.Max(0, Math.Min(20, effective));
        return effective <= 1 && !roundUp ? 0 : (effective + 1) / 2;
    }
    internal static bool Use2024ChannelRecovery(bool compressedPaladin, bool paladinRule, bool fallback) =>
        compressedPaladin ? paladinRule : fallback;
    internal static int LegacyPreparedLimit(int effective, int charismaModifier, int affinityBonus) =>
        Math.Max(1, effective / 2 + charismaModifier + affinityBonus);
    internal static bool CanRecharge(int actual, bool validMode, bool ownsBoth, int remaining, bool hasResource) =>
        actual == MaxClassLevel && validMode && ownsBoth && remaining == 0 && hasResource;
    internal static bool ShouldTransferCondition(bool active, int actual, bool ownSource, bool classCondition) =>
        !active || actual == 0 || !ownSource || !classCondition;
    internal static bool IsPersistentActivation(string activation) => activation is "Permanent" or "PermanentUnlessIncapacitated";
    internal static bool IsClassCondition(string name) => name.StartsWith("ConditionPaladin", StringComparison.Ordinal) ||
        name.StartsWith("ConditionOathOf", StringComparison.Ordinal) || name.StartsWith("ConditionHatred", StringComparison.Ordinal) ||
        name is "ConditionDivineSmiteSpell" or "ConditionMarkUsedFreeSmite" or "ConditionAuraOfProtection" or "ConditionAuraOfCourage";
    internal static bool IsOriginTag(string tag, string parent) => tag != null && parent != null &&
        (tag == parent || (tag.StartsWith(parent, StringComparison.Ordinal) && !char.IsDigit(tag[parent.Length])));
}

// The source callback may be repeated; never charge/recharge twice for the same completed action.
internal sealed class PaladinRechargeLedger
{
    private bool finished;
    internal bool TryFinish(bool valid)
    {
        if (finished || !valid) return false;
        finished = true; return true;
    }
}

// Data objects can be reused by consecutive advancements; deduplicate within an actual Paladin level.
internal sealed class PaladinSpellGrantLedger
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
