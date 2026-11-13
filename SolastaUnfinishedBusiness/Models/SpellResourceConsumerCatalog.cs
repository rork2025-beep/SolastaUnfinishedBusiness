using System;
using System.Collections.Generic;
using System.Linq;
using P = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;

namespace SolastaUnfinishedBusiness.Models;

// Source-specific adapters, not a fallback based on circle, selected class or null repertoire.
// Costs/pool identities/eligibility are fresh native proofs; source rules are immutable per session.
internal static class SpellResourceConsumerCatalog
{
    internal const string Revision = "13.6-native-1";
    private static readonly O.Domain[] Standard = { O.Domain.StandardSlots, O.Domain.SpellPoints };
    private static readonly O.Domain[] Paid = { O.Domain.StandardSlots, O.Domain.SpellPoints, O.Domain.PactSlots };
    private static readonly O.Domain[] Effect = { O.Domain.Effect };
    private static O.Source Rule(string id, string definition, string owner, O.Kind kind, IEnumerable<O.Domain> input, IEnumerable<O.Domain> output,
        int min = 0, int max = 9, int level = 0, O.Scale scale = O.Scale.Fixed, int amount = 1, bool repertoire = false, bool item = false, IEnumerable<KeyValuePair<int, int>> dice = null, bool activation = false, int activationCost = 0, bool costFromRule = false, string nativeRecharge = null) =>
        new(id, definition, owner, kind, input, output, Revision, min, max, level, scale, amount, repertoire, item, dice, activation, activationCost, costFromRule, nativeRecharge);
    internal sealed class FlexibleInput(O.Domain domain,int circle,int nativeCircle,bool lossy)
    { internal O.Domain Domain { get; }=domain;internal int Circle { get; }=circle;internal int NativeCircle { get; }=nativeCircle;internal bool Lossy { get; }=lossy; }
    // Exact installed SpendMulticasterWarlockSlots conversion policy, using existing coordinator
    // availability. No fictitious known spell/RulesetEffectSpell is created for a conversion.
    internal static FlexibleInput ResolveFlexibleInput(P.CastingLedger resources,int nativeCircle,bool alwaysPactFirst,bool shift,bool warlockRepertoire)
    {
        if(nativeCircle is <1 or >9)throw new O.Rejection(O.Code.InvalidCircle,"Flexible Casting");
        var pactCircle=resources.Pact.Current.Circle;
        var standard=resources.Standard.CanSpend(nativeCircle);
        var canPact=resources.Pact.Remaining>0 && ((!alwaysPactFirst&&nativeCircle<=pactCircle)||nativeCircle==pactCircle);
        var force=!standard||alwaysPactFirst||warlockRepertoire&&!shift||!warlockRepertoire&&shift;
        if(canPact&&force)return new FlexibleInput(O.Domain.PactSlots,pactCircle,nativeCircle,pactCircle!=nativeCircle);
        if(standard)return new FlexibleInput(resources.Standard.Current.Mode==S.ResourceMode.SpellPoints?O.Domain.SpellPoints:O.Domain.StandardSlots,nativeCircle,nativeCircle,false);
        throw new O.Rejection(O.Code.Exhausted,"Flexible Casting has no legal source under installed policy");
    }
    internal static O.Source FlexibleCasting(bool createSlot) => createSlot
        ? Rule("create-slot", "PowerSorcererCreateSpellSlot", "Sorcerer", O.Kind.SorceryToSlot, new[] { O.Domain.SorceryPoints }, Standard, 1, 5, 2, repertoire: true)
        : Rule("create-sorcery", "PowerSorcererCreateSorceryPoints", "Sorcerer", O.Kind.SlotToSorcery, Paid, new[] { O.Domain.SorceryPoints }, 1, 9, 2, repertoire: true);
    internal static O.Source Smite(bool eldritch, IEnumerable<KeyValuePair<int, int>> nativeDice) =>
        Rule(eldritch ? "eldritch-smite" : "divine-smite", eldritch ? "AdditionalDamageInvocationEldritchSmite" : "AdditionalDamagePaladinDivineSmite",
            eldritch ? "Warlock" : "Paladin", O.Kind.SlotEffect, eldritch ? new[] { O.Domain.PactSlots } : Paid, Effect, 1, 9, repertoire: true, dice: nativeDice);
    // Effect-form descriptors must come from the actually owned power, after all rule switches.
    internal static O.Source Recovery(string definition, string actualOwner, int maximumCircle)
    {
        if (definition is not ("PowerWizardArcaneRecovery" or "PowerCircleLandNaturalRecovery" or "PowerSpellMasterBonusRecovery"))
            throw new O.Rejection(O.Code.UnknownSource, definition + "/recovery form");
        var expected = definition == "PowerWizardArcaneRecovery" ? "Wizard" : definition == "PowerCircleLandNaturalRecovery" ? "Druid" : "Wizard";
        if (string.IsNullOrWhiteSpace(expected) || actualOwner != expected || maximumCircle is < 1 or > 9) throw new O.Rejection(O.Code.WrongOwner, definition);
        return Rule(definition, definition, actualOwner, O.Kind.StandardRecovery, new[] { O.Domain.ClassPower }, Standard, 0, maximumCircle, scale: O.Scale.HalfUp, repertoire: true);
    }
    internal static O.Source PactRecovery(bool cunning) => Rule(cunning ? "cunning" : "master", cunning ? "PowerWarlockMagicalCunning" : "PowerWarlockEldritchMaster",
        "Warlock", O.Kind.PactRecovery, new[] { O.Domain.ClassPower }, new[] { O.Domain.PactSlots }, 0, 0, cunning ? 2 : 20, scale: cunning ? O.Scale.Fixed : O.Scale.AllMissing);
    internal static O.Source SlotTrade(string id, string nativeDefinition)
    {
        return id switch
        {
            "wild-shape-refund" when nativeDefinition == "PowerDruidWildResurgenceShape" => Rule(id, nativeDefinition, "Druid", O.Kind.SlotToNative, Paid, new[] { O.Domain.ClassPower }, 1, 9, 5, repertoire: true, activation: true, activationCost: 1),
            "paladin-capstone-refund" when nativeDefinition.StartsWith("PowerRechargePower", StringComparison.Ordinal) => Rule(id, nativeDefinition, "Paladin", O.Kind.SlotToNative, Paid, new[] { O.Domain.ClassPower }, 5, 5, 20, repertoire: true, activation: true),
            "cannon-refund" when nativeDefinition == "PowerInnovationArtilleristEldritchCannonRefund" => Rule(id, nativeDefinition, "Inventor", O.Kind.SlotToNative, Paid, new[] { O.Domain.ClassPower }, 1, 5, repertoire: true, activation: true),
            "alchemy-refund" when AlchemyCircle(nativeDefinition) > 0 => Rule(id, nativeDefinition, "Inventor", O.Kind.SlotToNative, Paid, new[] { O.Domain.ClassPower }, AlchemyCircle(nativeDefinition), AlchemyCircle(nativeDefinition), scale: O.Scale.Circle, repertoire: true, activation: true),
            "mixture-refund" when nativeDefinition == "PowerInnovationVitriolistRefundMixture" => Rule(id, nativeDefinition, "Inventor", O.Kind.SlotToNative, Paid, new[] { O.Domain.ClassPower }, 1, 5, scale: O.Scale.Circle, repertoire: true, activation: true),
            _ => throw new O.Rejection(O.Code.UnknownSource, id)
        };
    }
    private static int AlchemyCircle(string id) => Enumerable.Range(1, 5).FirstOrDefault(n => id == "PowerInnovationAlchemyRefundFromSlot" + n);
    internal static O.Source WildShapeToSlot(string nativeDefinition) => Rule("wild-shape-slot", nativeDefinition, "Druid", O.Kind.NativeToSlot,
        new[] { O.Domain.ClassPower }, Standard, 1, 1, 5, activation: true, activationCost: 1);
    internal static O.Source SlotEffect(string id, string nativeDefinition, string actualOwner, IEnumerable<KeyValuePair<int, int>> nativeRanks = null) => id switch
    {
        "slay-enemies" when Enumerable.Range(1, 3).Any(n => nativeDefinition == "PowerFeatSlayTheEnemies" + n) => Rule(id, nativeDefinition, "Ranger", O.Kind.SlotEffect, Paid, Effect, int.Parse(nativeDefinition.Substring(nativeDefinition.Length - 1)), int.Parse(nativeDefinition.Substring(nativeDefinition.Length - 1)), repertoire: true, activation: true, activationCost: 1),
        "blade-dance-reduction" => Rule(id, nativeDefinition, actualOwner, O.Kind.SlotEffect, Paid, Effect, 1, 9, repertoire: true, dice: nativeRanks),
        _ => throw new O.Rejection(O.Code.UnknownSource, id)
    };
    internal static O.Source WardRefill(string nativeDefinition) => Rule("ward-refill", nativeDefinition, "Wizard", O.Kind.SlotToNative, Paid, new[] { O.Domain.ClassPower }, 1, 9, scale: O.Scale.Circle, amount: 2, repertoire: true);
    internal static O.Source SorceryRecovery(string definition, string effect, int gain)
    {
        var legal = definition switch
        {
            "PowerSorcererChildRiftOffering" or "PowerSorcererHauntedSoulSoulDrain" or "PowerSorcererManaPainterDrain" or "PowerDivineHeartDivineFount" or "PowerSorcererManaPainterManaOverflow" or "PowerSorcererSorcerousRestoration" => effect == "GainSorceryPoints",
            "PowerSorcererManaPainterTap" => effect == "RecovererSorceryHalfLevelUp",
            "PowerSorcerousRestoration" => effect == "9000",
            _ => false
        };
        if (!legal || gain < 0) throw new O.Rejection(O.Code.UnknownSource, definition + "/" + effect);
        return Rule(definition, definition, "Sorcerer", O.Kind.GainNative, new[] { O.Domain.ClassPower }, new[] { O.Domain.SorceryPoints }, 0, 0,
            scale: effect == "RecovererSorceryHalfLevelUp" ? O.Scale.HalfUp : effect == "9000" ? O.Scale.HalfDown : O.Scale.Fixed, amount: gain);
    }
    internal static O.Source HauntedRecharge() => Rule("haunted-recharge", "PowerSorcererHauntedSoulRechargeVengefulSpirits", "Sorcerer", O.Kind.GainNative,
        new[] { O.Domain.SorceryPoints }, new[] { O.Domain.ClassPower }, 0, 0, scale: O.Scale.AllMissing);
    internal static O.Source SlotPower(string nativePowerId, string actualOwner) => Rule("slot-power:" + nativePowerId, nativePowerId, actualOwner, O.Kind.SlotEffect, Paid, Effect, 1, 9, repertoire: true);
    internal static O.Source FreeMark() => Rule("free-mark", "PowerFavoredEnemyHuntersMark", "Ranger", O.Kind.SpendNative, new[] { O.Domain.FreeSpellUse }, Effect, 0, 0);
    internal static O.Source StoredItemCreation(string powerId) => powerId.StartsWith("PowerCreateSpellStoringWandOf",StringComparison.Ordinal) ? Rule("store:" + powerId, powerId, "Inventor", O.Kind.SpendNative, new[] { O.Domain.ClassPower }, Effect, 0, 0, 11) : throw new O.Rejection(O.Code.UnknownSource,powerId+"/not a stored-item creation power");
    internal static O.Source Item(string nativeFunctionId, bool storedSpell) => Rule("item:" + nativeFunctionId, nativeFunctionId, null, O.Kind.SpendNative,
        new[] { storedSpell ? O.Domain.StoredSpellUse : O.Domain.ItemCharges }, Effect, 0, 9, item: true);
    // Device-level Usage is authoritative; Function.ParentUsage is NOT its effective domain.
    internal static O.Source DeviceFunction(string nativeFunctionId, string usage, string affinity, bool stored = false)
    {
        if (stored && (usage != "Charges" || affinity != "ChargeCost")) throw new O.Rejection(O.Code.UnknownSource, nativeFunctionId + "/changed stored-item shape");
        if (usage == "Charges" && affinity is "ChargeCost" or "AtWill") return Item(nativeFunctionId, stored);
        if (usage == "Single" && affinity == "AtWill") return Rule("item:" + nativeFunctionId, nativeFunctionId, null, O.Kind.SpendNative, new[] { O.Domain.ItemQuantity }, Effect, 0, 9, item: true);
        if (usage == "ByFunction" && affinity is "IterationPerRecharge" or "RechargeDuration") return Rule("item:" + nativeFunctionId, nativeFunctionId, null, O.Kind.SpendNative, new[] { O.Domain.ItemFunctionUses }, Effect, 0, 9, item: true);
        if (usage == "ByFunction" && affinity == "AtWill") return Rule("item:" + nativeFunctionId, nativeFunctionId, null, O.Kind.SpendNative, Effect, Effect, 0, 9, item: true);
        throw new O.Rejection(O.Code.UnknownSource, nativeFunctionId + "/device usage=" + usage + "/affinity=" + affinity);
    }
    // The captured native roll/time rule bounds the result. Proof carries the one verified outcome
    // and event identity, so different events can have different rolls without replacing the facade.
    internal static O.Source DeviceRecovery(string nativeFunctionId, O.Domain domain, string nativeRecharge, int maximumNativeGain)
    {
        if (domain is not (O.Domain.ItemCharges or O.Domain.ItemFunctionUses) || maximumNativeGain < 0 ||
            nativeRecharge is not ("Dawn" or "ShortRest" or "LongRest" or "RechargeDuration")) throw new O.Rejection(O.Code.Ineligible, nativeFunctionId + "/no authorized native recharge");
        return Rule("item-recovery:" + nativeFunctionId, nativeFunctionId, null, O.Kind.GainNative, Effect, new[] { domain }, 0, 0, scale: O.Scale.NativeResult,amount: maximumNativeGain, item: true, nativeRecharge: nativeRecharge);
    }
    internal static O.Source NativePowerRecharge(string definition, string actualOwner, O.Domain domain, string nativeRecharge)
    {
        if (domain is not (O.Domain.ClassPower or O.Domain.InvocationUse or O.Domain.FreeSpellUse or O.Domain.SorceryPoints) ||
            nativeRecharge is not ("ShortRest" or "LongRest" or "TurnStart")) throw new O.Rejection(O.Code.UnknownSource, definition + "/unverified native counter recovery");
        return Rule("native-recharge:" + definition, definition, actualOwner, O.Kind.GainNative, Effect, new[] { domain }, 0, 0, scale: O.Scale.AllMissing, nativeRecharge: nativeRecharge);
    }
    // Native UsePower cost/recharge can be subclass/shared-pool dependent: the actual usable-power
    // proof supplies it. This descriptor never makes a power into a known spell or a caster class.
    internal static O.Source Power(string nativePowerId, string actualOwner, O.Domain nativeCostDomain) =>
        nativeCostDomain is O.Domain.ClassPower or O.Domain.InvocationUse or O.Domain.FreeSpellUse or O.Domain.SorceryPoints
        ? Rule("power:" + nativePowerId, nativePowerId, actualOwner, O.Kind.SpendNative, new[] { nativeCostDomain }, Effect, 0, 9)
        : throw new O.Rejection(O.Code.WrongDomain, nativePowerId);
    internal static O.Source EmbodimentOfLaw() => Rule("embodiment-law","MetamagicEmbodimentOfTheLaw","Cleric",O.Kind.SpendNative,
        new[]{O.Domain.ClassPower},Effect,0,9,6,amount:1,repertoire:true,costFromRule:true);
    internal static O.Source Sorcery(string sourceId, string nativeDefinition, O.Scale scale, int amount, bool debit) =>
        sourceId is "metamagic" or "mind-sculpt" or "supreme-will" or "magical-guidance" or "transmuted-refund" or "seeking" or "wild-magic-reset" or "mana-overflow" or "sorcery-effect-form"
        ? Rule(sourceId, nativeDefinition, sourceId is "metamagic" or "transmuted-refund" or "seeking" ? null : "Sorcerer", debit ? O.Kind.SpendNative : O.Kind.GainNative, debit ? new[] { O.Domain.SorceryPoints } : Effect,
            debit ? Effect : new[] { O.Domain.SorceryPoints }, sourceId == "supreme-will" ? 1 : 0, 9, scale: sourceId == "supreme-will" ? O.Scale.Circle : scale, amount: sourceId is "supreme-will" or "seeking" ? 2 : amount, activation: sourceId == "supreme-will", activationCost: sourceId == "supreme-will" ? 1 : 0, costFromRule: debit && sourceId != "metamagic")
        : throw new O.Rejection(O.Code.UnknownSource, sourceId);
}
