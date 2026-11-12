using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Behaviors.Specific;
using SolastaUnfinishedBusiness.Subclasses;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionPowers;

namespace SolastaUnfinishedBusiness.Models;

// Own power catalogs, never global monster or CharacterLevel replacement.
internal static class DruidWildshapeContext
{
    private static readonly Dictionary<FeatureDefinitionPower, (MonsterDefinition Monster, int Original)[]> Catalog = new();
    internal static void Initialize()
    {
        foreach (var power in new FeatureDefinitionPower[] { PowerDruidWildShape, CircleOfTheNight.PowerCircleOfTheNightWildShapeCombat })
        {
            var form = power.EffectDescription.EffectForms.Single(x => x.FormType == EffectForm.EffectFormType.ShapeChange).ShapeChangeForm;
            if (form.ShapeChangeType != ShapeChangeForm.Type.ClassLevelListSelection || form.ShapeOptions.Count == 0 ||
                form.ShapeOptions.Any(x => x.RequiredLevel is < 1 or > 20 || !x.SubstituteMonster))
                throw new InvalidOperationException("Unsupported Wild Shape catalog: " + power.Name);
            Catalog.Add(power, form.ShapeOptions.Select(x => (x.SubstituteMonster, x.RequiredLevel)).ToArray());
            Main.Info(power.Name + " original catalogs: " + string.Join(", ", Catalog[power].Select(x => x.Monster.Name + "@" + x.Original)));
        }
    }
    internal static bool IsWildShapePower(BaseDefinition source) => source is FeatureDefinitionPower p && Catalog.ContainsKey(p);
    internal static bool IsSupportedWildShape(RulesetCharacter character) => DruidProgressionContext.Active &&
        character is RulesetCharacterMonster monster && monster.OriginalFormCharacter is RulesetCharacterHero hero &&
        hero.GetClassLevel(Druid) is >= 1 and <= 12 && DruidSpellcastingContext.IsSupportedHero(hero) &&
        monster.HasConditionOfType(DatabaseHelper.ConditionDefinitions.ConditionWildShapeSubstituteForm.Name) &&
        Catalog.Values.Any(x => x.Any(s => s.Monster == monster.MonsterDefinition));

    // Resource ownership only; does not authorize spell access, actions or mixed-class admission.
    internal static RulesetCharacterHero StandardResourceOwner(RulesetCharacter character) =>
        character is RulesetCharacterMonster monster && monster.OriginalFormCharacter is RulesetCharacterHero hero &&
        hero.GetClassLevel(Druid) > 0 &&
        monster.HasConditionOfType(DatabaseHelper.ConditionDefinitions.ConditionWildShapeSubstituteForm.Name) &&
        Catalog.Values.Any(x => x.Any(s => s.Monster == monster.MonsterDefinition)) ? hero : null;

    // Spell Points has one authoritative pool across form/reversion. Rebind on refresh after deserialization too.
    internal static RulesetCharacter SpellPointOwner(RulesetCharacter character)
    {
        if (!IsSupportedWildShape(character)) return character;
        var hero = character.GetOriginalHero();
        var pool = hero.UsablePowers.FirstOrDefault(x => x.PowerDefinition == SpellPointsContext.PowerSpellPoints);
        if (pool != null)
        {
            var index = character.UsablePowers.FindIndex(x => x.PowerDefinition == SpellPointsContext.PowerSpellPoints);
            if (index < 0) character.UsablePowers.Add(pool);
            else if (!ReferenceEquals(character.UsablePowers[index], pool)) character.UsablePowers[index] = pool;
        }
        return hero;
    }
    internal static int RequiredLevel(BaseDefinition source, RulesetCharacter character, int original) =>
        DruidProgressionContext.Active && character.GetClassLevel(Druid) is >= 1 and <= 12 && IsWildShapePower(source)
            ? FighterProgressionPlan<int>.CompressedLevel(original) : original;

    private static bool Allowed(RulesetCharacter actor, FeatureDefinitionPower power, MonsterDefinition monster, bool checkUses)
    {
        var hero = actor.GetOriginalHero();
        if (hero == null || !DruidSpellcastingContext.IsSupportedHero(hero)) return false;
        if (power == CircleOfTheNight.PowerCircleOfTheNightWildShapeCombat && hero.GetSubclassLevel(Druid, CircleOfTheNight.Name) == 0) return false;
        if (!hero.HasAnyFeature(power)) return false;
        var item = Catalog[power].FirstOrDefault(x => x.Monster == monster);
        var cost = power == CircleOfTheNight.PowerCircleOfTheNightWildShapeCombat && CircleOfTheNight.IsTwoPointsShape(monster) ? 2 : 1;
        return DruidProgressionRules.CanSelectForm(DruidProgressionContext.EffectiveLevel(hero), item.Original,
            checkUses ? hero.GetRemainingPowerUses(PowerDruidWildShape) : cost, cost);
    }
    internal static bool CanApply(EffectForm form, RulesetImplementationDefinitions.ApplyFormsParams args)
    {
        if (!DruidProgressionContext.Active || args.activeEffect is not RulesetEffectPower effect || !IsWildShapePower(effect.PowerDefinition) ||
            args.sourceCharacter is not RulesetCharacter actor || actor.GetClassLevel(Druid) == 0) return true;
        // Usage was validated before the action; application may follow the first native resource charge.
        var valid = Allowed(actor, effect.PowerDefinition, args.targetSubstitute, false);
        if (!valid) Main.Error("Druid prototype: rejected unknown/premature Wild Shape before replacing the hero.");
        return valid;
    }
    internal static bool CanRespec(RulesetCharacterHero hero)
    {
        if (!DruidProgressionContext.Active || hero == null || hero.GetClassLevel(Druid) == 0) return true;
        var activeSummon = hero.SpellsCastByMe.Any(x => x.SpellDefinition.EffectDescription.EffectForms.Any(f => f.FormType == EffectForm.EffectFormType.Summon)) ||
                           hero.PowersUsedByMe.Any(x => x.PowerDefinition.EffectDescription.EffectForms.Any(f => f.FormType == EffectForm.EffectFormType.Summon));
        if (!hero.HasConditionOfType("ConditionShapeChanged") && !activeSummon &&
            !hero.AllConditions.Any(x => DruidProgressionRules.IsStarryCondition(x.ConditionDefinition.Name))) return true;
        Main.Error("Druid prototype: revert/end Starry Form and dismiss active summons before respec; no hero state was changed.");
        return false;
    }
    private static bool CanStart(CharacterAction action)
    {
        var power = action.ActionParams.UsablePower?.PowerDefinition ?? (action.ActionParams.RulesetEffect as RulesetEffectPower)?.PowerDefinition ?? action.ActionDefinition.ActivatedPower;
        var actor = action.ActingCharacter.RulesetCharacter;
        if (!DruidProgressionContext.Active || !IsWildShapePower(power) || actor.GetClassLevel(Druid) == 0) return true;
        if (!DruidProgressionContext.SessionConsistent)
        {
            Main.Error("Druid prototype: rules changed; restart before another transformation."); return false;
        }
        if (Allowed(actor, power, action.ActionParams.TargetSubstitute, true)) return true;
        Main.Error("Druid prototype: Wild Shape form, original-level requirement or resource cost rejected.");
        return false;
    }
    private sealed class ShapeTransaction
    {
        internal RulesetCharacterHero Hero;
        internal MonsterDefinition Monster;
        internal DruidShapeUseJournal Journal;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CharacterAction, ShapeTransaction> ShapeTransactions = new();
    private static bool Capture(CharacterAction action)
    {
        var power = action.ActionParams.UsablePower?.PowerDefinition ?? (action.ActionParams.RulesetEffect as RulesetEffectPower)?.PowerDefinition ?? action.ActionDefinition.ActivatedPower;
        var hero = action.ActingCharacter.RulesetCharacter.GetOriginalHero();
        if (!DruidProgressionContext.Active || !IsWildShapePower(power) || hero == null || hero.GetClassLevel(Druid) == 0) return true;
        if (ShapeTransactions.TryGetValue(action, out _))
        {
            Main.Error("Druid prototype: repeated shape action rejected."); return false;
        }
        ShapeTransactions.Add(action, new ShapeTransaction
        {
            Hero = hero, Monster = action.ActionParams.TargetSubstitute,
            Journal = new DruidShapeUseJournal(hero.GetRemainingPowerUses(PowerDruidWildShape),
                power == CircleOfTheNight.PowerCircleOfTheNightWildShapeCombat && CircleOfTheNight.IsTwoPointsShape(action.ActionParams.TargetSubstitute) ? 2 : 1)
        });
        return true;
    }
    private static RulesetCharacterMonster FindForm(ShapeTransaction state) => ServiceRepository.GetService<IGameLocationCharacterService>()
        .PartyCharacters.Concat(ServiceRepository.GetService<IGameLocationCharacterService>().GuestCharacters)
        .Select(x => x.RulesetCharacter).OfType<RulesetCharacterMonster>()
        .FirstOrDefault(x => ReferenceEquals(x.OriginalFormCharacter, state.Hero) && x.MonsterDefinition == state.Monster &&
                             x.HasConditionOfType(DatabaseHelper.ConditionDefinitions.ConditionWildShapeSubstituteForm.Name));
    private static void SetShapeUses(RulesetCharacter owner, int remaining)
    {
        var pool = PowerProvider.Get(PowerDruidWildShape, owner);
        owner.UpdateUsageForPowerPool(pool.RemainingUses - remaining, pool);
    }
    // UB dispatches only the FIRST IPowerOrSpellFinishedByMe. Night's original callback masks the2024 HP callback.
    // Normalize the single successful action from its pre-charge snapshot and update both native resource mirrors.
    internal static bool FinishShape(CharacterAction action)
    {
        if (!DruidProgressionContext.Active || !ShapeTransactions.TryGetValue(action, out var state)) return false;
        if (state.Journal.Finished) return true;
        var form = FindForm(state);
        if (form == null) return true; // Failed/cancelled application: no invented success/refund.
        var max = state.Hero.GetMaxUsesOfPower(PowerProvider.Get(PowerDruidWildShape, state.Hero));
        if (!state.Journal.TryFinish(max, out var remaining)) return true;
        SetShapeUses(state.Hero, remaining); SetShapeUses(form, remaining);
        if (Main.Settings.EnableDruidWildshape2024)
            form.ReceiveTemporaryHitPoints(DruidProgressionContext.EffectiveLevel(state.Hero), RuleDefinitions.DurationType.UntilAnyRest,
                1, RuleDefinitions.TurnOccurenceType.EndOfTurn, state.Hero.Guid);
        SpellPointOwner(form);
        return true;
    }
    internal static bool RefundLegacyShape(CharacterAction action)
    {
        if (!DruidProgressionContext.Active || !ShapeTransactions.TryGetValue(action, out var state)) return false;
        FinishShape(action);
        var form = FindForm(state);
        if (form == null || !state.Journal.Finished) return true;
        var max = state.Hero.GetMaxUsesOfPower(PowerProvider.Get(PowerDruidWildShape, state.Hero));
        if (!state.Journal.TryRefund(max, out var remaining)) return true;
        SetShapeUses(state.Hero, remaining); SetShapeUses(form, remaining);
        return true;
    }

    private static IEnumerator Empty() { yield break; }
    [HarmonyPatch(typeof(CharacterActionWildShape), nameof(CharacterActionWildShape.ExecuteImpl))]
    internal static class ShapeActionPatch
    {
        internal static bool Prefix(CharacterActionWildShape __instance, ref IEnumerator __result)
        {
            if (CanStart(__instance)) return true;
            __result = Empty(); return false;
        }
    }
    [HarmonyPatch(typeof(CharacterActionMagicEffect), nameof(CharacterActionMagicEffect.ExecuteImpl))]
    internal static class UsePowerActionPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(CharacterActionMagicEffect __instance, ref IEnumerator __result)
        {
            if (__instance is not CharacterActionUsePower) return true;
            if (CanStart(__instance) && Capture(__instance)) return true;
            __instance.ExecutionFailed = true;
            __result = Empty(); return false;
        }
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.GetSpellcastingLevel))]
    internal static class FormCastingLevelPatch
    {
        internal static void Postfix(RulesetCharacter __instance, RulesetSpellRepertoire spellRepertoire, ref int __result)
        {
            if (IsSupportedWildShape(__instance) && spellRepertoire.SpellCastingClass == Druid)
                __result = __instance.GetClassLevel(Druid); // Projected tables require ACTUAL index, not effective again.
        }
    }
    [HarmonyPatch(typeof(RulesetCharacterMonster), nameof(RulesetCharacterMonster.RefreshAll))]
    internal static class FormRefreshPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetCharacterMonster __instance) => SpellPointOwner(__instance);
    }
}
