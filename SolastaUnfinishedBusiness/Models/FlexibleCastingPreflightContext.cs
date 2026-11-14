using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using V = SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using F = SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;

namespace SolastaUnfinishedBusiness.Models;

// Identity-only local envelope; no native commit, network transport, save binder or public switch.
internal static partial class FlexibleCastingPreflightContext
{
    private sealed class Origin(RulesetCharacterHero hero,RulesetEffectPower effect,RulesetSpellRepertoire repertoire,V.Authority source)
    {
        internal readonly RulesetCharacterHero Hero=hero;
        internal readonly RulesetEffectPower Effect=effect;
        internal readonly ulong SourceGuid=effect.SourceGuid;
        internal readonly RulesetSpellRepertoire Repertoire=repertoire;
        internal readonly V.Authority Source=source;
        internal readonly F.Authority Authority=source.Preflight(RepertoireId(hero,repertoire));
    }
    private sealed class Session(FlexibleCastingPreflightPlan plan,string rules)
    {internal readonly F Plan=plan;internal readonly string Rules=rules;}
    private static readonly ConditionalWeakTable<FlexibleCastingModal,Origin> Modals=new();
    private static readonly ConditionalWeakTable<RulesetCharacterHero,Session> Sessions=new();
    private static readonly V Invocations=new(()=>Guid.NewGuid().ToString("N"));
    private static readonly System.Collections.Generic.Dictionary<string,Origin> Origins=new(StringComparer.Ordinal);
    private static V.Evidence Evidence(RulesetCharacterHero hero,FeatureDefinitionPower power,bool create)
    {
        var rep=SorcererRepertoire(hero);
        return new V.Evidence(hero.Guid.ToString(),hero,power,rep,power.Name,create,hero.HasAnyFeature(power)&&
            power==Api.DatabaseHelper.GetDefinition<FeatureDefinitionPower>(SpellResourceConsumerCatalog.FlexibleCasting(create).Definition));
    }
    private static V.Evidence Evidence(Origin source)
    {
        var evidence=Evidence(source.Hero,source.Effect.PowerDefinition,source.Authority.CreateSlot);
        return new V.Evidence(evidence.Hero,evidence.Owner,evidence.Power,evidence.Repertoire,evidence.Definition,evidence.Create,
            evidence.Eligible&&source.Effect.SourceGuid==source.SourceGuid&&source.Effect.OriginItem==null&&
            source.Hero.UsablePowers.Contains(source.Effect.UsablePower)&&ReferenceEquals(source.Repertoire,evidence.Repertoire));
    }
    private static bool RegisteredFor(RulesetEffectPower effect,RulesetCharacterHero hero)
    {
        var source=Invocations.Find(effect);
        Invocations.Check(source,Evidence(hero,effect.PowerDefinition,source.Entry.Create),effect);
        return true; // issued registry proof or a bound trusted campaign invocation, never latest-effect lookup
    }
    [ThreadStatic] private static Origin opening;
    private sealed class Scope(Origin previous):IDisposable {public void Dispose()=>opening=previous;}
    private static string RepertoireId(RulesetCharacterHero hero,RulesetSpellRepertoire repertoire)=>hero.Guid+"/"+repertoire.SpellCastingFeature.Name;
    private static RulesetSpellRepertoire SorcererRepertoire(RulesetCharacterHero hero)
    {
        var reps=hero.SpellRepertoires.Where(r=>r.SpellCastingClass==Api.DatabaseHelper.CharacterClassDefinitions.Sorcerer &&
            r.SpellCastingFeature==Api.DatabaseHelper.FeatureDefinitionCastSpells.CastSpellSorcerer).ToArray();
        if(reps.Length!=1||reps[0].GetCaster()?.GetOriginalHero()!=hero)throw new O.Rejection(O.Code.WrongOwner,"Unique actual Sorcerer repertoire required");
        return reps[0];
    }
    private static string Rules(Origin source)
    {
        var powers=new[]{false,true}.Select(slot=>Api.DatabaseHelper.GetDefinition<FeatureDefinitionPower>(SpellResourceConsumerCatalog.FlexibleCasting(slot).Definition));
        var settings=string.Join("|",typeof(Settings).GetProperties().Where(p=>p.PropertyType==typeof(bool)||p.PropertyType==typeof(int))
            .OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>p.Name+"="+p.GetValue(Main.Settings)));
        var affinity=string.Join("|",source.Hero.FeaturesByType<FeatureDefinitionMagicAffinity>().OrderBy(f=>f.Name,StringComparer.Ordinal)
            .Select(f=>f.Name+":"+f.HealingPerSpentSorceryPoint));
        return settings+"/"+string.Join("|",powers.Select(p=>p.Name+":"+p.RechargeRate+":"+p.CostPerUse))+
            "/"+affinity+"/"+string.Join(",",RuleDefinitions.SpellSlotCreationCost.OrderBy(p=>p.Key).Select(p=>p.Key+":"+p.Value))+"/"+string.Join(",",SpellPointsContext.SpellCostByLevel);
    }
    private static F.Live Read(Origin source)
    {
        var hero=source.Hero;var bound=StandardMulticasterResourcesContext.PreflightResources(hero);
        var rep=SorcererRepertoire(hero);
        var pools=hero.UsablePowers.Where(p=>p.PowerDefinition==SpellPointsContext.PowerSpellPoints).ToArray();
        int Capacity(int c)=>rep.spellsSlotCapacities.TryGetValue(c,out var n)?n:0;
        int Used(int c)=>rep.usedSpellsSlots.TryGetValue(c,out var n)?n:0;
        var maximum=hero.TryGetAttributeValue(AttributeDefinitions.SorceryPoints);
        var eligible=hero.HasAnyFeature(source.Effect.PowerDefinition)&&source.Effect.UsablePower.PowerDefinition==source.Effect.PowerDefinition &&
            ReferenceEquals(source.Repertoire,rep)&&source.Effect.PowerDefinition==Api.DatabaseHelper.GetDefinition<FeatureDefinitionPower>(
                SpellResourceConsumerCatalog.FlexibleCasting(source.Authority.CreateSlot).Definition);
        return new F.Live(hero.Guid.ToString(),RepertoireId(hero,rep),rep.SpellCastingFeature.Name,bound.ActualSorcerer,bound.EffectiveSorcerer,bound.CompressedSorcerer,
            eligible,Rules(source),maximum,maximum-hero.UsedSorceryPoints,Enumerable.Range(1,9).Select(Capacity),Enumerable.Range(1,9).Select(Used),Used(SharedSpellsContext.PactMagicSlotsTab),
            pools.Length,pools.Length==1?hero.GetMaxUsesOfPower(pools[0]):0,pools.Length==1?pools[0].RemainingUses:0,
            Enumerable.Range(1,5).Select(c=>RuleDefinitions.SpellSlotCreationCost[c]),hero.AllConditions.Select(c=>c.Guid.ToString()).Concat(new[]{"authority:"+source.Effect.PowerDefinition.Name+":"+source.Effect.UsablePower.MaxUses+":"+source.Effect.UsablePower.RemainingUses}),Main.Settings.AlwaysSpendPactSlotsFirst);
    }
    private static Session SessionFor(Origin source)
    {
        if(Sessions.TryGetValue(source.Hero,out var found))return found;
        var bound=StandardMulticasterResourcesContext.PreflightResources(source.Hero);
        var live=Read(source);var hero=source.Hero.Guid.ToString();
        var counter=new O.Counter(hero+"/SorceryPoints",hero,O.Domain.SorceryPoints,live.SorceryMaximum,live.SorceryRemaining,"LongRest");
        var model=new O(hero,SpellResourceConsumerCatalog.Revision,bound.Resources,new[]{SpellResourceConsumerCatalog.FlexibleCasting(false),SpellResourceConsumerCatalog.FlexibleCasting(true)},
            Array.Empty<O.Proof>(),new[]{counter},bound.ActualClasses,bound.Tables,RuleDefinitions.SpellSlotCreationCost);
        found=new Session(new F(hero,live.Rules,model,counter,bound.Tables),live.Rules);Sessions.Add(source.Hero,found);return found;
    }
    // Capture actual registry proof at action entry. Later termination invalidates this authority.
    [HarmonyPatch(typeof(CharacterActionMagicEffect),nameof(CharacterActionMagicEffect.ExecuteImpl))]
    internal static class RootActionPatch
    {
        [HarmonyPriority(Priority.First+2)]
        internal static void Prefix(CharacterActionMagicEffect __instance)
        {
            if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
            if(__instance.ActionParams?.RulesetEffect is not RulesetEffectPower effect)return;
            if(!new[]{false,true}.Any(slot=>effect.PowerDefinition==Api.DatabaseHelper.GetDefinition<FeatureDefinitionPower>(SpellResourceConsumerCatalog.FlexibleCasting(slot).Definition)))return;
            var actor=__instance.ActingCharacter.RulesetCharacter;var hero=actor.GetOriginalHero();
            if(hero==null||effect.Terminated||effect.Guid==0||!RulesetEntity.TryGetEntity<RulesetEffectPower>(effect.Guid,out var registered)||!ReferenceEquals(registered,effect)||
                effect.SourceGuid!=hero.Guid&&effect.SourceGuid!=actor.Guid||!hero.UsablePowers.Contains(effect.UsablePower))
                throw new O.Rejection(O.Code.MissingIdentity,"Flexible Casting action lacks registered source identity before effects");
            var create=effect.PowerDefinition==Api.DatabaseHelper.GetDefinition<FeatureDefinitionPower>(SpellResourceConsumerCatalog.FlexibleCasting(true).Definition);
            Invocations.Register(Evidence(hero,effect.PowerDefinition,create),effect,effect.Guid.ToString(),true,
                ()=>!effect.Terminated&&RulesetEntity.TryGetEntity<RulesetEffectPower>(effect.Guid,out var current)&&ReferenceEquals(current,effect));
        }
    }
    [HarmonyPatch(typeof(RulesetImplementationManager),nameof(RulesetImplementationManager.ApplySpellSlotsForm))]
    internal static class SourceScopePatch
    {
        [HarmonyPriority(Priority.First+1)]
        internal static void Prefix(EffectForm __0,RulesetImplementationDefinitions.ApplyFormsParams __1,out IDisposable __state)
        {
            __state=null;if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
            var kind=__0.SpellSlotsForm?.Type;
            if(kind is not (SpellSlotsForm.EffectType.CreateSpellSlot or SpellSlotsForm.EffectType.CreateSorceryPoints))return;
            var hero=__1.sourceCharacter?.GetOriginalHero();
            if(hero==null||__1.activeEffect is not RulesetEffectPower effect||effect.Terminated||effect.UsablePower==null||
                effect.SourceGuid!=hero.Guid&&effect.SourceGuid!=__1.sourceCharacter.Guid ||
                !RegisteredFor(effect,hero)||
                !hero.UsablePowers.Contains(effect.UsablePower))
                throw new O.Rejection(O.Code.MissingIdentity,"Registered owned native power-effect provenance missing; transient/campaign effects require an explicit command envelope");
            var source=new Origin(hero,effect,SorcererRepertoire(hero),Invocations.Find(effect));
            var before=opening;__state=new Scope(before);opening=source;
            if(!Read(source).Eligible)throw new O.Rejection(O.Code.Ineligible,"Source is not an owned native Flexible Casting power");
        }
        internal static Exception Finalizer(Exception __exception,IDisposable __state){__state?.Dispose();return __exception;}
    }
    [HarmonyPatch(typeof(FlexibleCastingModal),nameof(FlexibleCastingModal.ShowFlexibleCasting))]
    internal static class ShowPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(FlexibleCastingModal __instance,RulesetCharacter __0,RulesetSpellRepertoire __1,bool __2)
        {
            if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
            var source=opening;
            if(source==null||__instance.Visible||__0!=source.Hero||__1!=source.Repertoire||__2!=source.Authority.CreateSlot)
                throw new O.Rejection(O.Code.MissingIdentity,"Modal cannot guess an earlier effect or overwrite an active source");
            SessionFor(source);Invocations.AttachModal(source.Source,Evidence(source),source.Effect,__instance);
            Origins[source.Source.Id]=source;Modals.Remove(__instance);Modals.Add(__instance,source);
        }
    }
    [HarmonyPatch(typeof(FlexibleCastingModal),nameof(FlexibleCastingModal.OnConvertCb))]
    internal static class ConvertPatch
    {
        [HarmonyPriority(Priority.First+1)]
        internal static bool Prefix(FlexibleCastingModal __instance)
        {
            if(!StandardMulticasterResourcesContext.RuntimeEnabled)return true;
            if(!Modals.TryGetValue(__instance,out var source))throw new O.Rejection(O.Code.MissingIdentity,"Unmapped native conversion callback");
            try
            {
                if(__instance.caster!=source.Hero||__instance.repertoire!=source.Repertoire)throw new O.Rejection(O.Code.WrongOwner,"Modal owner changed");
                var session=SessionFor(source);
                var choice=new F.Choice(__instance.selectedSlotLevel,__instance.createSlotMode,Global.IsShiftPressed,Main.Settings.AlwaysSpendPactSlotsFirst);
                var command=Invocations.Prepare(source.Source,Evidence(source),source.Effect,__instance,session.Plan,choice,Read(source));
                ReceiveLocalCommand(command); // explicit in-band ID, not thread-local/hero-level correlation
                try {BlockNativeCommit(command);}
                catch(O.Rejection ex) when(ex.Diagnostic==O.Code.PendingAtomicJournal)
                {Main.Error("Flexible Casting identity preflight acknowledged; PendingAtomicJournal. Native resources and parent action remain unchanged.");}
            }
            catch(Exception)
            {RejectAndClose(source,__instance,V.Status.Rejected);throw;}
            return false; // no native button/delegate changes, transport, spend or publication

        }
    }
    [HarmonyPatch(typeof(FlexibleCastingModal),nameof(FlexibleCastingModal.OnCancelCb))]
    internal static class CancelPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(FlexibleCastingModal __instance)
        {
            if(!StandardMulticasterResourcesContext.RuntimeEnabled)return true;
            if(!Modals.TryGetValue(__instance,out var source))throw new O.Rejection(O.Code.MissingIdentity,"Cancel source missing");
            RejectAndClose(source,__instance,V.Status.Cancelled);
            return false; // native action refund/GUI lifecycle requires13.7B; no invented refund here
        }
    }
    // Receiver for the identity-only local protocol. Envelope may be queued/copied, but must
    // resolve to this receiver's own issued source/ticket; no fallback to native three-arg calls.
    internal static V.Acknowledgement ReceiveLocalCommand(V.Envelope command)
    {
        if(!StandardMulticasterResourcesContext.RuntimeEnabled)throw new O.Rejection(O.Code.Ineligible,"Preflight gate closed");
        if(command==null||string.IsNullOrWhiteSpace(command.Invocation)||!Origins.TryGetValue(command.Invocation,out var source))throw new O.Rejection(O.Code.MissingIdentity,"Unknown native invocation envelope");
        if(ServiceRepository.GetService<IMagicCommandService>() is not LocalCommandManager)
            throw new O.Rejection(O.Code.PendingAtomicJournal,"Network command transport has no authenticated authority mapping");
        return Invocations.Receive(command,Evidence(source),source.Effect,Read(source));
    }
    private static void BlockNativeCommit(V.Envelope command)
    {
        if(!Origins.TryGetValue(command.Invocation,out var source))throw new O.Rejection(O.Code.MissingIdentity,"No invocation");
        Invocations.BlockCommit(command,Evidence(source),source.Effect,Read(source));
    }
    private static void RejectAndClose(Origin source,FlexibleCastingModal modal,V.Status reason)
    {
        Invocations.Close(source.Source,reason);
        if(Modals.TryGetValue(modal,out var owner)&&ReferenceEquals(owner,source))
        {
            Modals.Remove(modal);
            modal.Hide(true); // audited UI-only hide; never OnCancelCb/RefundActionUse
        }
    }
    [HarmonyPatch(typeof(LocalCommandManager),nameof(LocalCommandManager.CreateSpellSlot))]
    internal static class LocalSlotPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __0)=>RejectUnmappedDispatch(__0);
    }
    [HarmonyPatch(typeof(LocalCommandManager),nameof(LocalCommandManager.CreateSorceryPoints))]
    internal static class LocalSorceryPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacter __0)=>RejectUnmappedDispatch(__0);
    }
    private static void RejectUnmappedDispatch(RulesetCharacter actor)
    {
        if(!StandardMulticasterResourcesContext.RuntimeEnabled)return;
        StandardMulticasterResourcesContext.TryCasterLevel(actor,out _);
        throw new O.Rejection(O.Code.PendingAtomicJournal,"Local/network conversion has no validated in-band command envelope; never match by owner/level alone");
    }
}
