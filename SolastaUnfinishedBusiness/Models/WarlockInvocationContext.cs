using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using static HeroDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

// Standard invocation pools keep one native choice/replacement event per original level.
// Virtual inventories and the scoped finalization journal live in the transaction partial.
internal static partial class WarlockInvocationContext
{
    private const string Origin = "Warlock12InvocationOriginal";
    private static KnownSpellsProgressionPlan plan;
    private static readonly ConditionalWeakTable<CharacterStageProficiencySelectionPanel,
        List<(string Tag, PointsPoolType Type)>> Steps = new();
    private static readonly ConditionalWeakTable<RulesetCharacterHero, Selection> Selections = new();
    private sealed class Selection { internal int Original; internal bool Unlearn; }

    internal static void Initialize(IEnumerable<FeatureUnlockByLevel> source)
    {
        var rewards = source.GroupBy(x => x.Level).ToDictionary(x => x.Key, x => x
            .SelectMany(y => CompressedProgressionDefinitions.Flatten(y.FeatureDefinition))
            .OfType<FeatureDefinitionPointPool>().Where(y => y.PoolType == PointsPoolType.Invocation).Sum(y => y.PoolAmount));
        var total = 0;
        plan = new KnownSpellsProgressionPlan(Enumerable.Range(1, 20).Select(n =>
            total += rewards.TryGetValue(n, out var value) ? value : 0),
            Enumerable.Range(1, 20).Select(n => WarlockProgressionRules.InvocationReplacementBudget(
                Main.Settings.EnableWarlockInvocationProgression2024, n)));
    }

    private static bool Uses(RulesetCharacterHero hero) => WarlockProgressionContext.Active && hero != null &&
        hero.GetClassLevel(Warlock) is >= 1 and <= 12 && LevelUpHelper.GetSelectedClass(hero) == Warlock;
    private static string BaseTag(RulesetCharacterHero hero) => AttributeDefinitions.GetClassTag(Warlock, hero.GetClassLevel(Warlock));
    private static string Tag(RulesetCharacterHero hero, int original) => BaseTag(hero) + Origin + original.ToString("D2");
    private static bool TryOriginal(RulesetCharacterHero hero, string tag, out int original)
    {
        original = 0;
        if (!Uses(hero)) return false;
        var prefix = BaseTag(hero) + Origin;
        return tag.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(tag.Substring(prefix.Length), out original) &&
               SpellcastingProgressionPlan.OriginalLevels(hero.GetClassLevel(Warlock)).Contains(original);
    }
    internal static int PrerequisiteLevel(RulesetCharacterHero hero) => Uses(hero) &&
        Selections.TryGetValue(hero, out var selection) ? selection.Original : WarlockProgressionContext.EffectiveLevel(hero);
    private static List<InvocationDefinition> Chosen(Dictionary<string, List<InvocationDefinition>> choices, string tag) =>
        choices.TryGetValue(tag, out var result) ? result.Where(x => x is not InvocationDefinitionCustom).ToList() : new();

    internal static void Configure(CharacterHeroBuildingData data)
    {
        var hero = data.HeroCharacter;
        if (!Uses(hero)) return;
        var baseTag = BaseTag(hero);
        // Remove only class progression pools, preserving feat-origin rewards.
        foreach (var type in new[] { PointsPoolType.Invocation, PointsPoolType.InvocationUnlearn })
            foreach (var key in data.PointPoolStacks[type].ActivePools.Keys.Where(key => key == baseTag ||
                         key.StartsWith(baseTag + "Warlock12WarlockOriginal", StringComparison.Ordinal)).ToArray())
                data.PointPoolStacks[type].ActivePools.Remove(key);
        foreach (var e in plan.Events(hero.GetClassLevel(Warlock)))
        {
            var tag = Tag(hero, e.Original);
            var removed = Chosen(data.UnlearnedInvocations, tag).Count;
            var replacement = data.CanUnlearn ? e.Replaced : 0;
            if (replacement > 0) Set(PointsPoolType.InvocationUnlearn, replacement, removed);
            // Zero placeholders allow a replacement on a level with no additional invocation.
            if (e.Learned > 0 || replacement > 0) Set(PointsPoolType.Invocation, e.Learned + removed,
                Chosen(data.LevelupTrainedInvocations, tag).Count);
            void Set(PointsPoolType type, int maximum, int used)
            {
                var pools = data.PointPoolStacks[type].ActivePools;
                if (!pools.TryGetValue(tag, out var pool)) pools[tag] = pool = new PointPool(0, null, false);
                pool.MaxPoints = maximum; pool.RemainingPoints = Math.Max(0, maximum - used);
                pool.Description = "Warlock invocation: original level " + e.Original;
            }
        }
    }

    internal static void Clear(CharacterBuildingManager manager, CharacterHeroBuildingData data, string parent)
    {
        foreach (var key in data.LevelupTrainedInvocations.Keys.Concat(data.UnlearnedInvocations.Keys)
                     .Where(x => x.StartsWith(parent + "Warlock12", StringComparison.Ordinal)).Distinct().ToArray())
        {
            if (Chosen(data.LevelupTrainedInvocations, key).Count > 0)
            {
                // Native undo also unregisters invocation-granted spell/cantrip pools.
                var pools = data.PointPoolStacks[PointsPoolType.Invocation].ActivePools;
                if (!pools.ContainsKey(key)) pools[key] = new PointPool(0, null, false);
                ClearChoiceSelections(data, Chosen(data.LevelupTrainedInvocations, key));
                InternalReset(() => manager.UntrainInvocations(data, key));
            }
            data.LevelupTrainedInvocations.Remove(key);
            data.UnlearnedInvocations.Remove(key);
        }
        Selections.Remove(data.HeroCharacter);
        Finalizations.Remove(data.HeroCharacter);
    }

    internal static bool CanTrain(CharacterHeroBuildingData heroBuildingData, InvocationDefinition invocation, string tag)
    {
        if (invocation is InvocationDefinitionCustom) return true;
        if (ChoiceCollision(heroBuildingData, invocation, tag)) return false;
        if (!TryOriginal(heroBuildingData.HeroCharacter, tag, out _)) return true;
        return ValidateEdit(heroBuildingData, invocation, tag, false);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.TrainInvocation))]
    internal static class TrainPatch
    {
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => Configure(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UnlearnInvocation))]
    internal static class UnlearnPatch
    {
        internal static bool Prefix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData, InvocationDefinition invocation, string tag)
        {
            if (invocation is InvocationDefinitionCustom || !TryOriginal(heroBuildingData.HeroCharacter, tag, out _)) return true;
            if (!ValidateEdit(heroBuildingData, invocation, tag, true)) return false;
            BeforeChange(__instance, heroBuildingData, tag);
            return true;
        }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => Configure(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UndoUnlearnInvocation))]
    internal static class UndoPatch
    {
        internal static bool Prefix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData, InvocationDefinition invocation, string tag)
        {
            if (invocation is InvocationDefinitionCustom || !TryOriginal(heroBuildingData.HeroCharacter, tag, out _)) return true;
            if (!Chosen(heroBuildingData.UnlearnedInvocations, tag).Contains(invocation)) return false;
            BeforeChange(__instance, heroBuildingData, tag, true);
            return true;
        }
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => Configure(heroBuildingData);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UntrainInvocation))]
    internal static class UntrainPatch
    {
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData) => Configure(heroBuildingData);
    }

    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.CollectTags))]
    internal static class CollectStepsPatch
    {
        internal static void Postfix(CharacterStageProficiencySelectionPanel __instance)
        {
            var p = __instance; Steps.Remove(p);
            if (!Uses(p.currentHero)) return;
            var all = p.allTags.Select((tag, index) => (Tag: tag, Type: p.GetPoolTypeOfIndex(index))).ToList();
            all = KnownSpellsProgressionPlan.OrderSteps(all.Select(x => (Step: x,
                Original: TryOriginal(p.currentHero, x.Tag, out var original) ? original : 0,
                Phase: x.Type == PointsPoolType.InvocationUnlearn ? 0 : 1)).ToList());
            p.allTags = all.Select(x => x.Tag).ToList(); Steps.Add(p, all);
        }
    }
    private static bool Step(CharacterStageProficiencySelectionPanel p, int index, out PointsPoolType type)
    {
        type = default;
        if (!Uses(p.currentHero) || !Steps.TryGetValue(p, out var steps) || index < 0 || index >= steps.Count) return false;
        type = steps[index].Type; return true;
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.GetPoolTypeOfIndex))]
    internal static class PoolTypePatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int index, ref PointsPoolType __result) =>
            !Step(__instance, index, out __result);
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.IsSkillStep))]
    internal static class SkillStepPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!Step(__instance, stepNumber, out var type)) return true;
            __result = type == PointsPoolType.Skill; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.IsToolTypeStep))]
    internal static class ToolTypeStepPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!Step(__instance, stepNumber, out var type)) return true;
            __result = type == PointsPoolType.Tool; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.IsExpertiseStep))]
    internal static class ExpertiseStepPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!Step(__instance, stepNumber, out var type)) return true;
            __result = type == PointsPoolType.Expertise; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.IsFeatStep))]
    internal static class FeatStepPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!Step(__instance, stepNumber, out var type)) return true;
            __result = type == PointsPoolType.Feat; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.IsInvocationUnlearnStep))]
    internal static class InvocationUnlearnStepPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!Step(__instance, stepNumber, out var type)) return true;
            __result = type == PointsPoolType.InvocationUnlearn; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.IsInvocationStep))]
    internal static class InvocationStepPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!Step(__instance, stepNumber, out var type)) return true;
            __result = type == PointsPoolType.Invocation; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.IsMetamagicStep))]
    internal static class MetamagicStepPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!Step(__instance, stepNumber, out var type)) return true;
            __result = type == PointsPoolType.Metamagic; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.IsLanguageStep))]
    internal static class LanguageStepPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance, int stepNumber, ref bool __result)
        {
            if (!Step(__instance, stepNumber, out var type)) return true;
            __result = type == PointsPoolType.Language; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.BuildLearnSteps))]
    internal static class BuildStepsPatch
    {
        internal static bool Prefix(CharacterStageProficiencySelectionPanel __instance)
        {
            var p = __instance;
            if (!Uses(p.currentHero) || !Steps.TryGetValue(p, out var steps)) return true;
            while (p.learnStepsTable.childCount < steps.Count) Gui.GetPrefabFromPool(p.learnStepPrefab, p.learnStepsTable);
            for (var i = 0; i < p.learnStepsTable.childCount; i++)
            {
                var child = p.learnStepsTable.GetChild(i); child.gameObject.SetActive(i < steps.Count);
                if (i >= steps.Count) continue;
                var step = steps[i];
                var title = TryOriginal(p.currentHero, step.Tag, out var original) ? "Warlock invocation: original level " + original : string.Empty;
                child.GetComponent<LearnStepItem>().Bind(i, step.Tag, step.Type, p.OnLearnBack, p.OnLearnReset, p.OnLearnAuto, title);
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.Refresh))]
    internal static class SelectionRankPatch
    {
        internal static void Prefix(CharacterStageProficiencySelectionPanel __instance)
        {
            var p = __instance;
            if (p.currentHero == null) return;
            Selections.Remove(p.currentHero);
            if (Uses(p.currentHero) && Steps.TryGetValue(p, out var steps) && p.currentLearnStep >= 0 && p.currentLearnStep < steps.Count &&
                TryOriginal(p.currentHero, steps[p.currentLearnStep].Tag, out var original))
                Selections.Add(p.currentHero, new Selection { Original = original, Unlearn = steps[p.currentLearnStep].Type == PointsPoolType.InvocationUnlearn });
        }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.OnEndHide))]
    internal static class ClearSelectionRankPatch
    {
        internal static void Prefix(CharacterStageProficiencySelectionPanel __instance)
        { if (__instance.currentHero != null) Selections.Remove(__instance.currentHero); }
    }
    [HarmonyPatch(typeof(CharacterStageProficiencySelectionPanel), nameof(CharacterStageProficiencySelectionPanel.CanProceedToNextStage))]
    internal static class ValidatePatch
    {
        internal static void Postfix(CharacterStageProficiencySelectionPanel __instance, ref bool __result, ref string failureString)
        {
            var p = __instance;
            if (!__result || !Uses(p.currentHero) || !Steps.TryGetValue(p, out var steps)) return;
            var data = p.currentHero.GetHeroBuildingData();
            try { ValidateComplete(data); }
            catch (InvalidOperationException ex)
            { __result = false; failureString = "Warlock invocation transaction: " + ex.Message; return; }
            if (steps.Any(x => x.Type == PointsPoolType.Invocation && TryOriginal(p.currentHero, x.Tag, out _) &&
                data.PointPoolStacks[x.Type].ActivePools.TryGetValue(x.Tag, out var pool) && pool.RemainingPoints > 0))
            { __result = false; failureString = "Complete every original Warlock invocation event."; }
        }
    }
}
