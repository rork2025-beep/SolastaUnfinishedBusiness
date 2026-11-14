using System.Linq;
using HarmonyLib;

namespace SolastaUnfinishedBusiness.Models;

// Deliberately no binder, native sidecar, public switch or automatic save import. Pure backend
// receipts do not authorize effects/conditions/inventory publication. Gate OFF is a no-op.
internal static class SpellResourceOperationsContext
{
    internal static void RequireNativeJournal(RulesetCharacter actor, string source)
    {
        if (!StandardMulticasterResourcesContext.RuntimeEnabled) return;
        if (!StandardMulticasterResourcesContext.TryCasterLevel(actor, out _)) return;
        throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.PendingAtomicJournal,
            source + "/native action identity, source proof, spend and effect publication must commit together");
    }
    // Descriptor capture is called only with explicit registry ownership; never selected-class
    // guessing. Multiple native forms require a composite journal and are not flattened.
    internal static SpellResourceOperationPlan.Source DescribeRecovery(FeatureDefinitionPower power, string actualOwner)
    {
        var forms = power.EffectDescription.EffectForms.Where(f => f.FormType == EffectForm.EffectFormType.SpellSlots).ToArray();
        if (forms.Length != 1) throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.AmbiguousSource, power.Name);
        var form = forms[0].SpellSlotsForm;
        if (form.Type == SpellSlotsForm.EffectType.RecoverHalfLevelUp)
        {
            if (!SharedSpellsContext.RecoverySlots.TryGetValue(power.Name, out var owner) || owner.Name != actualOwner)
                throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.WrongOwner, power.Name);
            return actualOwner == "Warlock" ? SpellResourceConsumerCatalog.PactRecovery(false) :
                SpellResourceConsumerCatalog.Recovery(power.Name, actualOwner, form.MaxSlotLevel);
        }
        if (power.Name == "PowerSorcererHauntedSoulRechargeVengefulSpirits" && form.Type == SpellSlotsForm.EffectType.RechargePower &&
            form.PowerDefinition?.Name == "PowerSorcererHauntedSoulVengefulSpirits" && actualOwner == "Sorcerer")
            return SpellResourceConsumerCatalog.HauntedRecharge();
        if (actualOwner != "Sorcerer") throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.WrongOwner, power.Name);
        if (form.Type == SpellSlotsForm.EffectType.CreateSpellSlot || form.Type == SpellSlotsForm.EffectType.CreateSorceryPoints)
        {
            var result = SpellResourceConsumerCatalog.FlexibleCasting(form.Type == SpellSlotsForm.EffectType.CreateSpellSlot);
            if (power.Name != result.Definition) throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.UnknownSource, power.Name);
            return result;
        }
        return SpellResourceConsumerCatalog.SorceryRecovery(power.Name, form.Type.ToString(), form.SorceryPointsGain);
    }
    internal static SpellResourceOperationPlan.Source DescribeDevice(RulesetItemDevice item, RulesetDeviceFunction function, bool verifiedStoredItem)
    {
        var index = item.UsableFunctions.IndexOf(function);
        if (index < 0 || function.UsableItemDefinition != item.ItemDefinition)
            throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.WrongOwner, "item/function identity");
        return SpellResourceConsumerCatalog.DeviceFunction(item.ItemDefinition.Name + "/function:" + index,
            item.UsableDeviceDescription.Usage.ToString(), function.DeviceFunctionDescription.UseAffinity.ToString(), verifiedStoredItem);
    }
    internal static SpellResourceOperationPlan.Source DescribeStoredCreation(FeatureDefinitionPowerSharedPool power)
    {
        if(power.SharedPool?.Name is not ("PowerInventorSpellStoringItem" or "PowerInventorSpellStoringItem2") ||
            power.SourceDefinition is not SpellDefinition { SpellLevel: 1 or 2 })
            throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.UnknownSource,"unverified Inventor stored-item pool/spell");
        return SpellResourceConsumerCatalog.StoredItemCreation(power.Name);
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.CreateSpellSlot))]
    internal static class CreateSlotPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __instance) => RequireNativeJournal(__instance, "PowerSorcererCreateSpellSlot");
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.CreateSorceryPoints))]
    internal static class CreateSorceryPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __instance) => RequireNativeJournal(__instance, "PowerSorcererCreateSorceryPoints");
    }
    [HarmonyPatch(typeof(RulesetImplementationManager), nameof(RulesetImplementationManager.InstantiateEffectPower))]
    internal static class PreparePowerPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __0, RulesetUsablePower __1)
        {
            if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
            if(FlexibleCastingPreflightContext.AllowCampaignSourceConstructor(__0,__1))return;
            RequireNativeJournal(__0, "power-preflight:" + __1?.PowerDefinition?.Name);
        }
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.UsePower))]
    internal static class SpendPowerPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __instance, RulesetUsablePower __0)
        {
            if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
            RequireNativeJournal(__instance, "power-debit:" + __0?.PowerDefinition?.Name);
        }
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.UseDeviceFunction))]
    internal static class DevicePatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __instance) => RequireNativeJournal(__instance, "item-function/charge-owner");
    }
    [HarmonyPatch(typeof(CharacterActionMagicEffect), nameof(CharacterActionMagicEffect.ExecuteImpl))]
    internal static class ActionPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(CharacterActionMagicEffect __instance)
        {
            if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
            // Explicit native OriginItem, never infer an item from a null repertoire. Covers stored
            // spells before effects; UseDeviceFunction alone is too late for inventory rollback.
            var effect = __instance.ActionParams.RulesetEffect;
            if (__instance is CharacterActionUsePower || effect?.OriginItem != null)
                RequireNativeJournal(__instance.ActingCharacter.RulesetCharacter,
                    effect?.OriginItem != null ? "item-action/pre-effect" : "power-action/pre-effect");
        }
    }
    [HarmonyPatch(typeof(CharacterActionSpendSpellSlot), nameof(CharacterActionSpendSpellSlot.ExecuteImpl))]
    internal static class SlotTriggerPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(CharacterActionSpendSpellSlot __instance)
        {
            if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
            RequireNativeJournal(__instance.ActingCharacter.RulesetCharacter, "slot-trigger/reaction-effect");
        }
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.SpendSorceryPoints))]
    internal static class SorceryDebitPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __instance) => RequireNativeJournal(__instance, "sorcery-debit/native-affinity-callbacks");
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.GainSorceryPoints))]
    internal static class SorceryGainPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __instance) => RequireNativeJournal(__instance, "sorcery-recovery/native-effect-origin");
    }
}
