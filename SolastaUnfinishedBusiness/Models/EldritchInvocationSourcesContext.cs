using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Behaviors;
using P = SolastaUnfinishedBusiness.Models.EldritchInvocationSourcesPlan;

namespace SolastaUnfinishedBusiness.Models;

// Native publication stays closed until Stage13.7. The native Consume(repertoire) ABI has
// no action identity and marks used before spending a separately selected lowest slot.
internal static class EldritchInvocationSourcesContext
{
    private static P registry;
    internal static P Registry => registry ?? throw new P.Rejection(P.Code.UnknownInvocation,"installed registry unavailable");
    internal static void CaptureInstalledDefinitions()
    {
        if(registry!=null)return;
        var definitions=new List<P.Definition>();
        var warlockCast=DatabaseHelper.FeatureDefinitionCastSpells.CastSpellWarlock;
        foreach(var invocation in DatabaseRepository.GetDatabase<InvocationDefinition>())
        {
            if(invocation is InvocationDefinitionCustom || !EldritchInvocationCatalog.Ids.Contains(invocation.Name))
            {
                definitions.Add(EldritchInvocationCatalog.Classify(invocation.Name,invocation.GrantedSpell?.Name,invocation.GrantedSpell?.SpellLevel??0,
                    invocation.GrantedFeature?.Name,invocation.RequiredLevel,invocation.RequiredPact?.Name,invocation.RequiredKnownSpell?.Name,
                    invocation.ConsumesSpellSlot,invocation.LongRestRecharge,Array.Empty<P.FeatureGrant>(),P.ValidatorRule.Unverified,
                    selectable:false,custom:invocation is InvocationDefinitionCustom));
                continue;
            }

            var nodes=invocation.GrantedFeature==null?Array.Empty<FeatureDefinition>():CompressedProgressionDefinitions.Flatten(invocation.GrantedFeature).ToArray();
            var grants=nodes.Select(f=>f is FeatureDefinitionPower p
                ?new P.FeatureGrant(f.Name,f.GetType().Name,p.RechargeRate.ToString(),p.CostPerUse,p.UsesDetermination.ToString(),p.FixedUsesPerRecharge,p.UsesAbilityScoreName,p.ActivationTime.ToString())
                :new P.FeatureGrant(f.Name,f.GetType().Name,mode:f is FeatureDefinitionFeatureSet set?set.Mode.ToString():null,
                    ownerClass:f.GetFirstSubFeatureOfType<ClassHolder>()?.Class?.Name,trigger:f is FeatureDefinitionAdditionalDamage damage?damage.TriggerCondition.ToString():null)).ToArray();
            var validator=P.ValidatorRule.None;
            if(invocation is InvocationDefinitionWithPrerequisites conditional && conditional.Validators.Count>0)
            {
                validator=P.ValidatorRule.Unverified;
                if(conditional.Validators.Count==1 && conditional.Validators[0].Method.DeclaringType==typeof(Subclasses.Builders.InvocationsBuilders))
                {
                    var name=conditional.Validators[0].Method.Name;
                    if(name=="ValidateHex")validator=P.ValidatorRule.Hex;
                    if(name.StartsWith("<BuildDevouringBlade>g__Validate",StringComparison.Ordinal))validator=P.ValidatorRule.ThirstingBlade;
                }
            }
            AuxiliarySpellSourcesPlan.Source choices=null;
            var pools=nodes.OfType<FeatureDefinitionPointPool>().ToArray();
            if(pools.Length>0 && pools.All(pool=>pool.PoolType is HeroDefinitions.PointsPoolType.Cantrip or HeroDefinitions.PointsPoolType.Spell or HeroDefinitions.PointsPoolType.CantripOrSpell))
            {
                var poolSpecs=pools.Select(pool=>
                {
                    var list=pool.SpellListOverride??warlockCast.SpellListDefinition;
                    var spells=list.SpellsByLevel.SelectMany(r=>r.Spells).Distinct().Select(spell=>new MultipleSpellRepertoiresPlan.Spell(spell.Name,spell.SpellLevel)).ToArray();
                    return new AuxiliarySpellSourcesPlan.Pool(pool.Name,spells,pool.PoolAmount,pool.PoolType switch
                    {
                        HeroDefinitions.PointsPoolType.Cantrip=>AuxiliarySpellSourcesPlan.ChoiceKind.Cantrip,
                        HeroDefinitions.PointsPoolType.Spell=>AuxiliarySpellSourcesPlan.ChoiceKind.Spell,
                        _=>AuxiliarySpellSourcesPlan.ChoiceKind.CantripOrSpell
                    },pool.MinSpellLevel,pool.MaxSpellLevel,pool.ExtraSpellsTag,pool.SpellListOverride?.Name,pool.RitualOnly,
                    list.SpellsByLevel.SelectMany(r=>r.Spells).Where(spell=>spell.Ritual).Select(spell=>spell.Name),authorizedList:list.Name);
                }).ToArray();
                choices=new AuxiliarySpellSourcesPlan.Source("InvocationChoices:"+invocation.Name,AuxiliarySpellSourcesPlan.Origin.Invocation,invocation.Name,
                    invocation.GrantedFeature.Name,"Attachment",warlockCast.SpellcastingAbility,"Warlock",poolSpecs.All(pool=>pool.RitualOnly)?AuxiliarySpellSourcesPlan.Domain.NativeRitual:AuxiliarySpellSourcesPlan.Domain.AttachedClass,
                    Math.Max(1,invocation.RequiredLevel),"NativeClass","ChoicePool",poolSpecs.SelectMany(pool=>pool.Allowed).GroupBy(spell=>spell.Id).Select(g=>g.First()),poolSpecs,
                    castingFeature:warlockCast.Name);
            }
            definitions.Add(EldritchInvocationCatalog.Classify(invocation.Name,invocation.GrantedSpell?.Name,invocation.GrantedSpell?.SpellLevel??0,
                invocation.GrantedFeature?.Name,invocation.RequiredLevel,invocation.RequiredPact?.Name,invocation.RequiredKnownSpell?.Name,
                invocation.ConsumesSpellSlot,invocation.LongRestRecharge,grants,validator,choices,
                !invocation.GuiPresentation.Hidden && !InvocationsContext.DisabledInvocations.Contains(invocation),invocation is InvocationDefinitionCustom));
        }
        var cast=DatabaseHelper.FeatureDefinitionCastSpells.CastSpellWarlock;
        if(!cast.HasAccessToInvocations)throw new P.Rejection(P.Code.MissingRepertoire,cast.Name);
        var candidate=new P(definitions,cast.Name,cast.SpellcastingAbility);
        foreach(var name in EldritchInvocationCatalog.Ids)
            if(candidate.Require(name).Type==P.Kind.Unsupported)Main.Error("Adept source rejected: "+name+"/"+candidate.Require(name).RejectionReason);
        var feat=DatabaseHelper.GetDefinition<FeatDefinition>("FeatEldritchAdept");
        var featPools=feat.Features.OfType<FeatureDefinitionPointPool>().ToArray();
        if(feat.Features.Count!=1 || featPools.Length!=1 || featPools[0].Name!="PointPoolFeatEldritchAdept" ||
            featPools[0].PoolType!=HeroDefinitions.PointsPoolType.Invocation || featPools[0].PoolAmount!=1)
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
