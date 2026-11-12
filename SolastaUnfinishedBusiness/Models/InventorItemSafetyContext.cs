using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using static SolastaUnfinishedBusiness.Classes.InventorClass;

namespace SolastaUnfinishedBusiness.Models;

internal static class InventorItemSafetyContext
{
    // Discover the dynamically constructed powers after Spell-Storing Item and Innovation late-load.
    internal static void RegisterPowers(HashSet<FeatureDefinitionPower> powers)
    {
        foreach (var power in DatabaseRepository.GetDatabase<FeatureDefinitionPower>())
            if (power.Name.StartsWith("PowerInventor", StringComparison.Ordinal) ||
                power.Name.StartsWith("PowerInfusion", StringComparison.Ordinal) ||
                power.Name.StartsWith("PowerInnovation", StringComparison.Ordinal) ||
                power.Name.StartsWith("PowerCreateSpellStoringWandOf", StringComparison.Ordinal) ||
                power.GetFirstSubFeatureOfType<LimitEffectInstances>()?.Name is "Infusion" or "EldritchCannon" ||
                power.GetFirstSubFeatureOfType<ClassHolder>()?.Class == Class)
                powers.Add(power);
        ForceGlobalUniqueEffects.AddToGroup(ForceGlobalUniqueEffects.Group.InventorSpellStoringItem,
            powers.Where(p => p.Name.StartsWith("PowerCreateSpellStoringWandOf", StringComparison.Ordinal)).Cast<BaseDefinition>().ToArray());
    }
    private static FeatureDefinitionPower InfusionOrigin(FeatureDefinitionPower power) =>
        SolastaUnfinishedBusiness.Behaviors.Specific.PowerBundle.GetMasterPowersBySubPower(power)
            .FirstOrDefault(p => p.Name.StartsWith("PowerInfusion", StringComparison.Ordinal)) ?? power;
    private static bool Uses(RulesetCharacter hero) => InventorProgressionContext.Active && hero != null && InventorProgressionContext.SourceOwner(hero).GetClassLevel(Class) > 0;
    private static bool ItemOrSummonEffect(RulesetEffect effect) =>
        effect.SourceDefinition is FeatureDefinitionPower power && InventorProgressionContext.HoldsPower(power) &&
        (effect.TrackedSummonedItemGuids.Count > 0 || effect.TrackedItemPropertyGuids.Count > 0 ||
         power.GetFirstSubFeatureOfType<LimitEffectInstances>()?.Name is "Infusion" or "EldritchCannon" ||
         power.EffectDescription.EffectForms.Any(f => f.FormType == EffectForm.EffectFormType.Summon));
    internal static bool HasPersistentItemsOrConstructs(RulesetCharacterHero hero) =>
        hero.PowersUsedByMe.Any(ItemOrSummonEffect) ||
        (ServiceRepository.GetService<IRulesetEntityService>() != null &&
         EffectHelpers.GetAllEffectsBySourceGuid(hero.Guid).Any(ItemOrSummonEffect));
    internal static bool CanAdvance(RulesetCharacterHero hero)
    {
        if (!Uses(hero) || !HasPersistentItemsOrConstructs(hero)) return true;
        Main.Error("Inventor prototype: cancel active infusions and stored items, dismiss constructs before advancement/retraining. Items/effects were not changed. Use native cancellation/dismissal, including items held by allies.");
        return false;
    }
    internal static bool CanRespec(RulesetCharacterHero hero)
    {
        if (!Uses(hero)) return true;
        if (!InventorProgressionContext.CanUseClassMechanics(hero) || !CanAdvance(hero)) return false;
        if (hero.ConcentratedSpell == null && !(hero.CharacterInventory?.items?.Any(x => x.AttunedToCharacter == hero.Name) ?? false) &&
            !EffectHelpers.GetAllEffectsBySourceGuid(hero.Guid).Any(e => e.SourceDefinition is FeatureDefinitionPower p &&
                InventorProgressionContext.HoldsPower(p) && p.ActivationTime is not (RuleDefinitions.ActivationTime.Permanent or RuleDefinitions.ActivationTime.PermanentUnlessIncapacitated))) return true;
        Main.Error("Inventor prototype: unattune items, end concentration and temporary Innovation effects before respec. No inventory mutation performed.");
        return false;
    }
    internal static bool ShouldTransferCondition(RulesetActor oldActor, RulesetActor newActor, RulesetCondition condition)
    {
        if (oldActor is not RulesetCharacterHero hero || !Uses(hero) ||
            condition.SourceGuid != oldActor.Guid && condition.SourceGuid != newActor.Guid) return true;
        return !condition.ConditionDefinition.Name.Contains("Inventor") && !condition.ConditionDefinition.Name.Contains("Innovation");
    }
    private static readonly ConditionalWeakTable<CharacterActionMagicEffect, InventorActionLedger> Actions = new();
    private static IEnumerator Empty() { yield break; }
    [HarmonyPatch(typeof(CharacterActionMagicEffect), nameof(CharacterActionMagicEffect.ExecuteImpl))]
    internal static class ActionSafetyPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(CharacterActionMagicEffect __instance, ref IEnumerator __result)
        {
            var hero = __instance.ActingCharacter.RulesetCharacter;
            if (!Uses(hero)) return true;
            // Cleanup must remain available even if settings changed; this native rest power cancels infusions.
            var power = __instance is CharacterActionUsePower ? __instance.ActionParams.UsablePower?.PowerDefinition : null;
            if (power?.Name == "PowerAfterRestStopInfusions") return true;
            var valid = InventorProgressionContext.CanUseClassMechanics(hero);
            if (valid && power != null && InventorProgressionContext.HoldsPower(power))
            {
                var journal = Actions.GetValue(__instance, _ => new InventorActionLedger());
                valid = journal.TryBegin();
                if (power.Name.StartsWith("PowerCreateSpellStoringWandOf", StringComparison.Ordinal))
                {
                    var spell = (power as FeatureDefinitionPowerSharedPool)?.SourceDefinition as SpellDefinition;
                    var inList = spell != null && spell.SpellLevel is 1 or 2 && spell.ActivationTime == RuleDefinitions.ActivationTime.Action &&
                        SpellCasting.SpellListDefinition.SpellsByLevel.Any(row => row.Spells.Contains(spell));
                    valid &= hero.GetClassLevel(Class) >= 8 && hero.GetRemainingPowerUses(power) > 0 &&
                        (inList || hero.HasAnyFeature(power)) &&
                        !hero.PowersUsedByMe.Any(e => !ReferenceEquals(e, __instance.ActionParams.RulesetEffect) &&
                            e.PowerDefinition.Name.StartsWith("PowerCreateSpellStoringWandOf", StringComparison.Ordinal));
                }
                // Retain native resource/uniqueness handling, but reject a second active copy of an infusion
                // rather than implicitly removing an ally's item through LimitEffectInstances.
                if (power.GetFirstSubFeatureOfType<LimitEffectInstances>()?.Name == "Infusion")
                {
                    var current = __instance.ActionParams.RulesetEffect;
                    var active = hero.PowersUsedByMe.Where(e => !ReferenceEquals(e, current) &&
                        e.PowerDefinition.GetFirstSubFeatureOfType<LimitEffectInstances>()?.Name == "Infusion" &&
                        e.PowerDefinition.Name != "PowerAfterRestStopInfusions").ToArray();
                    valid &= hero.GetRemainingPowerUses(InfusionPool) > 0 &&
                        (hero.GetOriginalHero()?.TrainedInvocations.Any(i => InventorInfusionContext.IsInfusion(i) && i.GrantedFeature == InfusionOrigin(power)) ?? false) &&
                        InventorProgressionRules.CanInfuse(active.Length,
                        SolastaUnfinishedBusiness.Behaviors.Specific.PowerBundle.GetMaxUsesForPool(hero, InfusionPool), active.Any(e => InfusionOrigin(e.PowerDefinition) == InfusionOrigin(power)));
                }
            }
            if (valid) return true;
            Main.Error("Inventor prototype: invalid session, repeated action, duplicate infusion or active capacity reached. Action rejected before native resource use.");
            __instance.ExecutionFailed = true; __result = Empty(); return false;
        }
    }
}
