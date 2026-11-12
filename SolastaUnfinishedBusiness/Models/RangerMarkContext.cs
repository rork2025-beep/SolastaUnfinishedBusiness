using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using static RuleDefinitions;
using SolastaUnfinishedBusiness.Interfaces;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static class RangerMarkContext
{
    private static bool relentless;
    private static bool noConcentration;
    private static FeatureDefinition precise;
    private static FeatureDefinitionPower FreeMark => DatabaseHelper.GetDefinition<FeatureDefinitionPower>("PowerFavoredEnemyHuntersMark");
    private static readonly ConditionalWeakTable<CharacterActionMagicEffect, RangerMarkActionLedger> FreeActions = new();
    internal static bool IsReady()
    {
        var action = Harmony.GetPatchInfo(AccessTools.Method(typeof(CharacterActionMagicEffect), nameof(CharacterActionMagicEffect.ExecuteImpl)));
        var panel = Harmony.GetPatchInfo(AccessTools.Method(typeof(CustomInvocationSelectionPanel), "UpdateGrantedFeatures"));
        return action != null && action.Prefixes.Any(x => x.PatchMethod.DeclaringType == typeof(ActionSafetyPatch)) &&
               panel != null && panel.Postfixes.Any(x => x.PatchMethod.DeclaringType == typeof(RangerInvocationContext.OriginPatch));
    }
    internal static void Initialize()
    {
        relentless = Main.Settings.EnableRangerRelentlessHunter2024;
        noConcentration = Main.Settings.EnableRangerRelentlessHunter2024AsNoConcentration;
        precise = FeatureDefinitionBuilder.Create("Ranger12PreciseHunter")
            .SetGuiPresentation(DatabaseHelper.GetDefinition<FeatureDefinitionCombatAffinity>("CombatAffinityRangerPreciseHunter").GuiPresentation)
            .AddCustomSubFeatures(new PreciseHunter()).AddToDB();
    }
    internal static FeatureDefinition AdaptFeature(FeatureDefinition source) => source.Name == "CombatAffinityRangerPreciseHunter" ? precise : source;
    internal static bool RequiresConcentration(RulesetCharacter hero, RulesetEffectSpell spell) =>
        RangerProgressionRules.RequiresConcentration(spell.SpellDefinition.RequiresConcentration,
            spell.SpellDefinition == DatabaseHelper.SpellDefinitions.HuntersMark, RangerProgressionContext.EffectiveLevel(hero), relentless, noConcentration);
    internal static bool ProtectConcentration(RulesetCharacter hero) =>
        RangerProgressionRules.ProtectConcentration(RangerProgressionContext.EffectiveLevel(hero), relentless, noConcentration);
    internal static bool CompleteFreeMark(CharacterActionMagicEffect action)
    {
        var hero = action.ActingCharacter.RulesetCharacter;
        if (!RangerProgressionContext.Active || hero.GetClassLevel(Ranger) == 0) return true;
        return FreeActions.TryGetValue(action, out var ledger) && ledger.TryComplete(!action.ExecutionFailed && !action.Countered &&
            RangerProgressionContext.CanUseClassMechanics(hero));
    }
    private static IEnumerator Empty() { yield break; }
    [HarmonyPatch(typeof(CharacterActionMagicEffect), nameof(CharacterActionMagicEffect.ExecuteImpl))]
    internal static class ActionSafetyPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(CharacterActionMagicEffect __instance, ref IEnumerator __result)
        {
            var hero = __instance.ActingCharacter.RulesetCharacter;
            if (!RangerProgressionContext.Active || hero.GetClassLevel(Ranger) == 0) return true;
            // CastNoCost clones parent parameters, including UsablePower. The child spell is NOT
            // another free-power activation; in particular the last paid power use must still cast.
            var powerAction = __instance is CharacterActionUsePower;
            var power = powerAction ? (__instance.ActionParams.RulesetEffect as RulesetEffectPower)?.PowerDefinition ??
                __instance.ActionParams.UsablePower?.PowerDefinition ?? __instance.ActionDefinition.ActivatedPower : null;
            var valid = RangerProgressionContext.CanUseClassMechanics(hero) && RangerCompanionContext.CanSummon(__instance, power);
            if (RangerProgressionRules.IsFreeMarkUse(powerAction, power == FreeMark)) valid = FreeActions.GetValue(__instance, _ => new RangerMarkActionLedger()).TryBegin(valid &&
                hero.HasAnyFeature(FreeMark) && hero.GetRemainingPowerUses(FreeMark) > 0 && __instance.ActionParams.TargetCharacters.Count > 0);
            if (valid) return true;
            __instance.ExecutionFailed = true; __result = Empty(); return false;
        }
    }
    private sealed class PreciseHunter : IModifyAttackActionModifier
    {
        public void OnAttackComputeModifier(RulesetCharacter myself, RulesetCharacter defender,
            BattleDefinitions.AttackProximity attackProximity, RulesetAttackMode attackMode, string effectName, ref ActionModifier attackModifier)
        {
            if (defender == null || !RangerProgressionContext.Active) return;
            var owner = myself.GetOriginalHero()?.Guid ?? myself.Guid;
            var marked = defender.AllConditions.Any(c => c.SourceGuid == owner &&
                c.ConditionDefinition.IsSubtypeOf(DatabaseHelper.ConditionDefinitions.ConditionMarkedByHunter.Name));
            if (RangerProgressionRules.PreciseAdvantage(RangerProgressionContext.EffectiveLevel(myself), marked))
                attackModifier.AttackAdvantageTrends.Add(new TrendInfo(1, RuleDefinitions.FeatureSourceType.CharacterFeature, precise.Name, precise));
        }
    }
}
