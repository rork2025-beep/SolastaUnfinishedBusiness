using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static class PaladinPowerSafetyContext
{
    private static readonly ConditionalWeakTable<CharacterActionMagicEffect, PaladinRechargeLedger> RechargeActions = new();
    internal static bool CanRecharge(RulesetCharacter character, FeatureDefinitionPower recharge, FeatureDefinitionPower capstone)
    {
        var repertoire = character.GetClassSpellRepertoire(Paladin);
        var slots = 0;
        repertoire?.GetSlotsNumber(5, out slots, out _);
        var resourceAvailable = character.IsSpellPointsEnabled()
            ? SpellPointsContext.CanCastSpellOfLevel(character, 5) : slots > 0;
        return PaladinProgressionRules.CanRecharge(character.GetClassLevel(Paladin),
            PaladinProgressionContext.CanUseClassMechanics(character),
            character.HasAnyFeature(recharge) && character.HasAnyFeature(capstone),
            character.GetRemainingPowerUses(capstone), repertoire != null && resourceAvailable);
    }
    internal static bool BeginRecharge(CharacterActionMagicEffect action, FeatureDefinitionPower recharge, FeatureDefinitionPower capstone)
    {
        var hero = action.ActingCharacter.RulesetCharacter;
        if (!PaladinProgressionContext.Active || hero.GetClassLevel(Paladin) == 0) return true;
        return RechargeActions.GetValue(action, _ => new PaladinRechargeLedger()).TryFinish(
            !action.Countered && !action.ExecutionFailed && CanRecharge(hero, recharge, capstone));
    }
    internal static bool CanRespec(RulesetCharacterHero hero)
    {
        if (!PaladinProgressionContext.Active || hero == null || hero.GetClassLevel(Paladin) == 0) return true;
        if (!PaladinProgressionContext.CanUseClassMechanics(hero)) return false;
        // Temporary class effects must finish before respec. Permanent auras can remain: the source
        // finalizer terminates old effects and the transfer gate below excludes stale self-owned conditions.
        var effects = EffectHelpers.GetAllEffectsBySourceGuid(hero.Guid);
        var conditions = EffectHelpers.GetAllConditionsBySourceGuid(hero.Guid);
        var permanentConditions = hero.UsablePowers.Select(x => x.PowerDefinition)
            .Where(x => PaladinProgressionContext.HoldsPower(x) && PaladinProgressionRules.IsPersistentActivation(x.ActivationTime.ToString()))
            .SelectMany(x => x.EffectDescription.EffectForms).Where(x => x.FormType == EffectForm.EffectFormType.Condition)
            .Select(x => x.ConditionForm.ConditionDefinition).ToArray();
        if (!effects.Any(x => x.SourceDefinition is FeatureDefinitionPower power && PaladinProgressionContext.HoldsPower(power) &&
                              !PaladinProgressionRules.IsPersistentActivation(power.ActivationTime.ToString())) &&
            !conditions.Any(x => PaladinProgressionRules.IsClassCondition(x.ConditionDefinition.Name) &&
                                 !permanentConditions.Contains(x.ConditionDefinition))) return true;
        Main.Error("Paladin prototype: end temporary Paladin/oath effects and Smite conditions before respec. No hero state changed.");
        return false;
    }
    internal static bool ShouldTransferCondition(RulesetActor oldActor, RulesetActor newActor, RulesetCondition condition)
    {
        if (oldActor is not RulesetCharacterHero oldHero) return true;
        // SwapEntities can exchange GUIDs before transfer. Both IDs still identify only the respec pair.
        return PaladinProgressionRules.ShouldTransferCondition(PaladinProgressionContext.Active,
            oldHero.GetClassLevel(Paladin), condition.SourceGuid == oldActor.Guid || condition.SourceGuid == newActor.Guid,
            PaladinProgressionRules.IsClassCondition(condition.ConditionDefinition.Name));
    }
    private static IEnumerator Empty() { yield break; }
    [HarmonyPatch(typeof(CharacterActionMagicEffect), nameof(CharacterActionMagicEffect.ExecuteImpl))]
    internal static class ActionSafetyPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(CharacterActionMagicEffect __instance, ref IEnumerator __result)
        {
            var hero = __instance.ActingCharacter.RulesetCharacter;
            if (PaladinProgressionContext.CanUseClassMechanics(hero)) return true;
            __instance.ExecutionFailed = true; __result = Empty(); return false;
        }
    }
}
