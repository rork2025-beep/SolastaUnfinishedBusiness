using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using P = SolastaUnfinishedBusiness.Models.EldritchInvocationSourcesPlan;

namespace SolastaUnfinishedBusiness.Models;

// Native publication stays closed until Stage13.7. The native Consume(repertoire) ABI has
// no action identity and marks used before spending a separately selected lowest slot.
internal static class EldritchInvocationSourcesContext
{
    private static readonly Dictionary<string,string> PaidSpells = new(StringComparer.Ordinal)
    {
        {"BewitchingWhispers","DreadfulOmen"}, {"DreadfulWord","Confusion"},
        {"MinionsChaos","ConjureElemental"}, {"MireMind","Slow"},
        {"SignIllOmen","BestowCurse"}, {"ThiefFiveFates","Bane"}
    };
    private static readonly Dictionary<string,string> PassiveFeatures = new(StringComparer.Ordinal)
    {
        {"DevilsSight","FeatureSetInvocationDevilsSight"},
        {"BeguilingInfluence","ProficiencyBeguilingInfluence"}, {"EyesRuneKeeper","ProficiencyAllLanguages"}
    };
    private static P registry;
    internal static P Registry => registry ?? throw new P.Rejection(P.Code.UnknownInvocation,"installed registry unavailable");
    internal static void CaptureInstalledDefinitions()
    {
        if(registry!=null)return;
        var definitions=new List<P.Definition>();
        foreach(var invocation in DatabaseRepository.GetDatabase<InvocationDefinition>())
        {
            var kind=P.Kind.Unsupported;
            if(PaidSpells.TryGetValue(invocation.Name,out var spell) && invocation.GrantedSpell?.Name==spell &&
                invocation.ConsumesSpellSlot && invocation.LongRestRecharge && !invocation.GrantedFeature)
                kind=P.Kind.PaidSpell;
            else if(PassiveFeatures.TryGetValue(invocation.Name,out var feature) && invocation.GrantedFeature?.Name==feature &&
                    !invocation.GrantedSpell && !invocation.ConsumesSpellSlot && !invocation.LongRestRecharge)
                kind=P.Kind.Passive;
            definitions.Add(new P.Definition(invocation.Name,kind,invocation.GrantedSpell?.Name,invocation.GrantedSpell?.SpellLevel??0,
                invocation.GrantedFeature?.Name,invocation.RequiredLevel,invocation.RequiredPact?.Name,invocation.RequiredKnownSpell?.Name,
                invocation.ConsumesSpellSlot,invocation.LongRestRecharge));
        }
        var cast=DatabaseHelper.FeatureDefinitionCastSpells.CastSpellWarlock;
        if(!cast.HasAccessToInvocations)throw new P.Rejection(P.Code.MissingRepertoire,cast.Name);
        var candidate=new P(definitions,cast.Name,cast.SpellcastingAbility);
        foreach(var name in PaidSpells.Keys)
            if(candidate.Require(name).Type!=P.Kind.PaidSpell)throw new P.Rejection(P.Code.UnsupportedOption,name+"/installed paid definition changed");
        var feat=DatabaseHelper.GetDefinition<FeatDefinition>("FeatEldritchAdept");
        var pools=feat.Features.OfType<FeatureDefinitionPointPool>().ToArray();
        if(feat.Features.Count!=1 || pools.Length!=1 || pools[0].Name!="PointPoolFeatEldritchAdept" ||
            pools[0].PoolType!=HeroDefinitions.PointsPoolType.Invocation || pools[0].PoolAmount!=1)
            throw new P.Rejection(P.Code.UnsupportedOption,"FeatEldritchAdept/native selection graph changed");
        registry=candidate;
    }
    internal static bool IsRegisteredPaid(InvocationDefinition definition) =>
        definition && registry!=null && registry.Definitions.TryGetValue(definition.Name,out var source) && source.Type==P.Kind.PaidSpell;
    internal static void RequireNativeJournal(InvocationDefinition definition,string boundary)
    {
        if(!StandardMulticasterResourcesContext.RuntimeEnabled || !definition || !definition.ConsumesSpellSlot)return;
        var code=IsRegisteredPaid(definition)?P.Code.PendingAtomicJournal:P.Code.UnsupportedOption;
        throw new P.Rejection(code,definition.Name+"/"+boundary+"/native invocation expenditure has no verified atomic action journal");
    }
    internal static void RequireAdeptJournal()
    {
        // No import of source-less native Invocations into class/feat claims, no new repertoire.
        throw new AuxiliarySpellSourcesPlan.Rejection(AuxiliarySpellSourcesPlan.Code.PendingAtomicJournal,
            "FeatEldritchAdept/source-scoped native feat selection and removal require Stage13.7");
    }
    internal static void BeforeTraining(CharacterHeroBuildingData data)
    {
        if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
        // Native class tag + empty ExtraSpellsTag merges feat and class pools. Do not guess
        // which award pays for a pending selection, even if this invocation is passive.
        if(data.LevelupTrainedFeats.Values.SelectMany(f=>f).Any(f=>f.Name=="FeatEldritchAdept"))RequireAdeptJournal();
    }
    [HarmonyPatch(typeof(RulesetImplementationManager),nameof(RulesetImplementationManager.InstantiateEffectInvocation))]
    internal static class InstantiateGuard
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetInvocation __1)=>RequireNativeJournal(__1?.InvocationDefinition,"InstantiateEffectInvocation");
    }
    [HarmonyPatch(typeof(RulesetCharacter),nameof(RulesetCharacter.CastSpell))]
    internal static class CastGuard
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetEffectSpell __0)=>RequireNativeJournal(__0?.RulesetInvocation?.InvocationDefinition,"CastSpell before SpellCast events");
    }
    [HarmonyPatch(typeof(RulesetCharacter),nameof(RulesetCharacter.UseInvocation))]
    internal static class UseGuard
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetInvocation __0)=>RequireNativeJournal(__0?.InvocationDefinition,"UseInvocation");
    }
    [HarmonyPatch(typeof(RulesetInvocation),nameof(RulesetInvocation.Consume))]
    internal static class ConsumeGuard
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetInvocation __instance)=>RequireNativeJournal(__instance.InvocationDefinition,"Consume before used=true");
    }
}
