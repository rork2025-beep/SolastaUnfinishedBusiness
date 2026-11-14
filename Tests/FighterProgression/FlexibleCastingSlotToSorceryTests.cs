using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using F = SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using V = SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using T = SolastaUnfinishedBusiness.Models.FlexibleCastingSlotToSorceryPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using Fixture = FlexibleCastingPreflightTests.Fixture;

internal static class FlexibleCastingSlotToSorceryTests
{
    private sealed class Use(string other="Wizard",S.ResourceMode mode=S.ResourceMode.Slots,bool create=false,int? sorcery=null,bool registered=false)
    {
        internal readonly Fixture F=new(mode,other,create,sorcery);
        internal readonly V Registry=new(()=>"trusted-b1-entry");
        internal readonly object Hero=new(),Power=new(),Rep=new(),Effect=new(),Modal=new();
        internal bool Alive=true,Eligible=true;
        internal V.Authority Source;
        internal T Plan=>Registry.StandardSlots;
        internal V.Evidence Evidence=>new("17",Hero,Power,Rep,SpellResourceConsumerCatalog.FlexibleCasting(create).Definition,create,Eligible);
        internal V.Envelope Command(int circle=1,bool shift=false)
        {
            if(Source==null)
            {
                if(registered)Source=Registry.Register(Evidence,Effect,"42",true,()=>Alive);
                else {Source=Registry.BeginCampaign(Evidence);Registry.BindCampaign(Source,Evidence,Effect,()=>Alive);}
                Registry.AttachModal(Source,Evidence,Effect,Modal);
            }
            return Registry.Prepare(Source,Evidence,Effect,Modal,F.Model,new F.Choice(circle,create,shift,false),F.Live());
        }
        internal T.Draft Draft(int circle=1)=>Plan.Prepare(Command(circle),Evidence,Effect,F.Live());
        internal T.Draft Commit(T.Draft draft,Action<T.Checkpoint> checkpoint=null)=>Plan.CommitManaged(draft,()=>Evidence,Effect,()=>F.Live(),checkpoint);
    }
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,TypeDefinition[] types)
    {
        var behavior=0;var metadata=0;var counterexamples=0;
        void B(bool ok,string name){behavior++;check(ok,"13.7B.1 executed production model: "+name);}
        void A(bool ok,string name){metadata++;check(ok,"13.7B.1 Cecil/definition audit: "+name);}
        void Reject(Use u,Action action,O.Code code,string name)
        {
            var before=u.F.Balances();var caught=false;
            try{action();}catch(O.Rejection ex){caught=ex.Diagnostic==code;}
            B(caught,name+" structured "+code);B(before==u.F.Balances(),name+" exact Standard/Pact/Arcanum/Sorcery/native counters/conditions unchanged");
        }
        foreach(var other in new[]{"Wizard","Warlock"})foreach(var registered in new[]{false,true})
        foreach(var remaining in new[]{0,6,10,11})for(var circle=1;circle<=5;circle++)
        {
            var u=new Use(other,sorcery:remaining,registered:registered);var before=u.F.Balances();var slots=u.F.Resources.Standard.RemainingSlots(circle);
            var pact=u.F.Resources.Pact.Remaining;var draft=u.Draft(circle);var gain=Math.Min(circle,11-remaining);
            B(ReferenceEquals(u.Plan,u.Registry.StandardSlots),"one transaction facade per issued invocation registry");
            B(before==u.F.Balances(),"preparing/staging has no resource/native mutation");
            B(draft.SlotDebit==1&&draft.SlotCircle==circle&&draft.SorceryGain==gain&&draft.After.Used[circle-1]==draft.Before.Used[circle-1]+1,"exact one-slot debit and native clamped gain");
            B(draft.ExpectedSorceryEventValue==remaining+gain&&draft.NativeUsedSorceryAfter==Math.Max(0,11-remaining-circle),"event contract uses final remaining value; actual used counter clamps to zero");
            B(ReferenceEquals(draft,u.Plan.Prepare(u.Command(circle),u.Evidence,u.Effect,u.F.Live())),"identical pending command returns same draft");
            Reject(u,()=>u.Plan.CommitNative(draft,u.Evidence,u.Effect,u.F.Live()),O.Code.PendingAtomicJournal,"native path remains blocked before debit/event/parent");
            var result=u.Commit(draft);
            B(ReferenceEquals(draft,result)&&result.Status==T.State.ManagedCommitted&&result.Receipt.OutputAmount==gain,"successful existing-ledger arithmetic once, explicit managed disposition");
            B(u.F.Resources.Standard.RemainingSlots(circle)==slots-1&&u.F.Sorcery.Remaining==remaining+gain&&u.F.Resources.Pact.Remaining==pact,"one authoritative model budget charged once; Pact unchanged");
            B(u.F.NativeSorcery==remaining&&u.F.NativePowerUses==1&&u.F.NativeRefunds==0&&u.F.Conditions.SequenceEqual(new[]{"original-condition"}),"managed success does not write native Sorcery, trigger power, refund or add conditions");
            B(!result.NativeCompleted&&!result.ParentCompleted&&!result.Intent.Source.ParentCompleted,"managed receipt never acknowledges native/parent completion");
            if(gain>0)Reject(u,()=>u.Commit(draft),O.Code.StaleOperation,"native fixture not projected: cannot silently treat arithmetic as publication");
            // Test-side projection only. No game objects, native callbacks or projection adapter are executed.
            u.F.NativeSorcery=result.After.SorceryRemaining;var after=u.F.Balances();var receipt=result.Receipt;
            B(ReferenceEquals(u.Commit(draft).Receipt,receipt)&&after==u.F.Balances(),"duplicate delivery after explicit fixture projection reuses receipt without second debit/gain");
            B(ReferenceEquals(u.Plan.Prepare(u.CommandAfterCommit(draft),u.Evidence,u.Effect,u.F.Live()),draft),"same envelope model receipt lookup remains stable after commit");
            Reject(u,()=>u.Plan.Cancel(draft),O.Code.PendingAtomicJournal,"unsafe committed cancellation never refunds debt");
        }
        foreach(var checkpoint in new[]{T.Checkpoint.BeforeStaging,T.Checkpoint.DebitStaged,T.Checkpoint.CreditStaged,T.Checkpoint.BeforePublish})
        {
            var u=new Use();var draft=u.Draft(3);var before=u.F.Balances();var failed=false;
            try{u.Commit(draft,p=>{if(p==checkpoint)throw new InvalidOperationException("injected staging fault");});}
            catch(InvalidOperationException){failed=true;}
            B(failed&&draft.Status==T.State.Rejected&&draft.Receipt==null,"fault "+checkpoint+" rejects unpublished model proposal");
            B(before==u.F.Balances()&&!draft.NativeCompleted&&!draft.ParentCompleted,"fault "+checkpoint+" has zero actual debits/grants/events/parent/refund");
            Reject(u,()=>u.Commit(draft),O.Code.StaleOperation,"rejected staged draft cannot retry with replacement identity");
        }
        {
            var u=new Use();var draft=u.Draft(3);var slots=u.F.Resources.Standard.RemainingSlots(3);
            var committed=u.Commit(draft,p=>{if(p==T.Checkpoint.AfterManagedPublish)throw new InvalidOperationException("lost model receipt delivery");});
            B(committed.Status==T.State.ManagedCommitted&&committed.DeliveryPending&&committed.Receipt!=null,"after model commit point delivery failure is committed/pending delivery, never failed/refunded");
            B(u.F.Resources.Standard.RemainingSlots(3)==slots-1&&u.F.Sorcery.Remaining==9&&!committed.NativeCompleted&&!committed.ParentCompleted,"lost delivery still exact one model conversion with no native/parent acknowledgement");
            u.F.NativeSorcery=9;var after=u.F.Balances();u.Commit(draft);
            B(!draft.DeliveryPending&&after==u.F.Balances(),"lost delivery retry confirms same model receipt without repeating deltas");
        }
        foreach(var stale in new[]{"rules","source","eligibility","sorcery","power","cost"})
        {
            var u=new Use();var draft=u.Draft();var slots=u.F.Resources.Standard.RemainingSlots(1);var points=u.F.Sorcery.Remaining;
            var caught=false;
            try{u.Commit(draft,p=>
            {
                if(p!=T.Checkpoint.BeforePublish)return;
                switch(stale){case "rules":u.F.Rules="changed";break;case "source":u.Alive=false;break;case "eligibility":u.Eligible=false;break;case "sorcery":u.F.NativeSorcery--;break;case "power":u.F.NativePowerUses--;break;case "cost":u.F.NativeCosts[0]++;break;}
            });}catch(O.Rejection){caught=true;}
            B(caught&&draft.Receipt==null,"fresh revalidation rejects changed "+stale+" immediately before model mutation");
            B(slots==u.F.Resources.Standard.RemainingSlots(1)&&points==u.F.Sorcery.Remaining&&!draft.ParentCompleted,"changed "+stale+" cannot debit/grant/complete parent");
        }
        {
            var u=new Use();var command=u.Command();var draft=u.Plan.Prepare(command,u.Evidence,u.Effect,u.F.Live());
            var conflict=new V.Envelope(command.Command,command.Invocation,command.Hero,command.Repertoire,command.Definition,command.Kind,command.Effect,false,2,2,command.Input,command.Output,1,command.Fingerprint);
            Reject(u,()=>u.Plan.Prepare(conflict,u.Evidence,u.Effect,u.F.Live()),O.Code.ConflictingOperation,"conflicting delivery");
            u.Plan.Cancel(draft);u.Plan.Cancel(draft);
            B(draft.Status==T.State.Cancelled&&draft.Receipt==null&&!draft.ParentCompleted,"pre-commit cancellation terminal/idempotent without native refund");
            Reject(u,()=>u.Commit(draft),O.Code.StaleOperation,"cancel then late managed commit");
            Reject(u,()=>u.Plan.CommitNative(draft,u.Evidence,u.Effect,u.F.Live()),O.Code.StaleOperation,"cancel then late native attempt");
        }
        {
            var u=new Use();while(u.F.Resources.Standard.CanSpend(1))u.F.Resources.Standard.Spend(Guid.NewGuid().ToString(),1);
            Reject(u,()=>u.Draft(),O.Code.Exhausted,"insufficient standard slot before ticket issuance");
            var v=new Use();var draft=v.Draft();while(v.F.Resources.Standard.CanSpend(1))v.F.Resources.Standard.Spend(Guid.NewGuid().ToString(),1);
            Reject(v,()=>v.Commit(draft),O.Code.StaleOperation,"slot exhausted by competing operation after Prepare");
        }
        {
            var u=new Use();var draft=u.Draft();var before=u.F.Balances();
            // Actual native CancelPatch closes the authority registry, not the model facade.
            u.Registry.Close(u.Source,V.Status.Cancelled);
            Reject(u,()=>u.Commit(draft),O.Code.StaleOperation,"native-style authority cancellation invalidates bound draft");
            B(before==u.F.Balances()&&!draft.ParentCompleted,"native-style cancel has no debit/gain/refund/parent completion");
            var v=new Use();var second=v.Draft();var snapshot=v.F.Balances();var rejected=false;
            try{v.Commit(second,p=>{if(p==T.Checkpoint.BeforePublish)v.Registry.Close(v.Source,V.Status.Cancelled);});}
            catch(O.Rejection ex){rejected=ex.Diagnostic==O.Code.StaleOperation;}
            B(rejected&&snapshot==v.F.Balances()&&second.Receipt==null,"cancellation during staging is rechecked at publication boundary");
        }
        {
            var u=new Use();var draft=u.Draft();u.F.Rules="changed";
            Reject(u,()=>u.Commit(draft),O.Code.RuleChanged,"detected changed rules before staging");
            u.F.Rules="rules1";
            Reject(u,()=>u.Commit(draft),O.Code.StaleOperation,"restoring settings cannot resurrect rejected command");
            B(draft.Status==T.State.Rejected&&draft.Receipt==null,"stale pending proof terminal as in original envelope receiver");
        }
        foreach(var stale in new[]{"rules","source","owner-level"})
        {
            var u=new Use();var draft=u.Draft();u.Commit(draft);u.F.NativeSorcery=draft.After.SorceryRemaining;
            switch(stale){case "rules":u.F.Rules="changed";break;case "source":u.Alive=false;break;case "owner-level":u.F.Actual=9;u.F.Effective=13;break;}
            Reject(u,()=>u.Commit(draft),stale=="rules"?O.Code.RuleChanged:stale=="source"?O.Code.PendingAtomicJournal:O.Code.StaleOperation,"committed duplicate with stale "+stale+" refuses further arithmetic");
        }
        foreach(var kind in new[]{"reverse","points","pact"})
        {
            var u=new Use(kind=="pact"?"Warlock":"Wizard",kind=="points"?S.ResourceMode.SpellPoints:S.ResourceMode.Slots,kind=="reverse");
            var command=u.Command(1,kind=="pact");
            Reject(u,()=>u.Plan.Prepare(command,u.Evidence,u.Effect,u.F.Live()),O.Code.WrongDomain,"excluded direction/domain "+kind);
        }
        {
            var u=new Use();var command=u.Command();var draft=u.Plan.Prepare(command,u.Evidence,u.Effect,u.F.Live());
            Reject(u,()=>u.Plan.Prepare(command,u.Evidence,u.Effect,u.F.Live(),true),O.Code.PendingAtomicJournal,"network prepare rejected");
            Reject(u,()=>u.Plan.CommitNative(draft,u.Evidence,u.Effect,u.F.Live(),true),O.Code.PendingAtomicJournal,"network commit rejected");
            var wrong=new V.Evidence("different",u.Hero,u.Power,u.Rep,u.Evidence.Definition,false,true);
            Reject(u,()=>u.Plan.CommitNative(draft,wrong,u.Effect,u.F.Live()),O.Code.WrongOwner,"wrong original hero");
            Reject(u,()=>u.Plan.CommitNative(draft,u.Evidence,new object(),u.F.Live()),O.Code.WrongOwner,"wrong effect reference");
            var foreign=new T(u.Registry);
            Reject(u,()=>foreign.CommitNative(draft,u.Evidence,u.Effect,u.F.Live()),O.Code.MissingIdentity,"foreign transaction facade");
            var nested=false;
            u.Commit(draft,p=>{if(p==T.Checkpoint.DebitStaged)try{u.Commit(draft);}catch(O.Rejection ex){nested=ex.Diagnostic==O.Code.PendingAtomicJournal;}});
            B(nested&&u.F.Sorcery.Remaining==7,"reentrant arithmetic refused; outer command commits once");
        }
        // Executable COUNTEREXAMPLE fixtures of the audited native order, not production or Unity tests.
        foreach(var failure in new[]{"slot-event","sorcery-event"})
        {
            var usedSlots=0;var usedSorcery=5;var parent=false;var ack=false;var failed=false;
            try
            {
                usedSlots++;
                if(failure=="slot-event")throw new InvalidOperationException("RepertoireRefreshed listener failed");
                usedSorcery=Math.Max(0,usedSorcery-3);
                throw new InvalidOperationException("SorceryPointsAltered listener failed");
            }catch(InvalidOperationException){failed=true;}
            counterexamples++;check(failed&&usedSlots==1&&usedSorcery==(failure=="slot-event"?5:2)&&!parent&&!ack,
                "13.7B.1 native-order COUNTEREXAMPLE fixture: "+failure+" demonstrates no safe generic rollback/ack boundary (not native execution)");
        }
        Audit(A,game,types);
        Console.WriteLine($"Stage13.7B.1: {behavior} executed production-model checks; {metadata} Cecil/definition audits; {counterexamples} native-order counterexample fixtures; no Unity execution.");
    }
    private static V.Envelope CommandAfterCommit(this Use u,T.Draft draft)=>draft.Intent.Command;
    private static void Audit(Action<bool,string> a,AssemblyDefinition game,TypeDefinition[] types)
    {
        MethodReference[] Calls(MethodDefinition m)=>m.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
        var actor=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter");
        var create=actor.Methods.Single(m=>m.Name=="CreateSorceryPoints");var body=create.Body.Instructions;
        var spend=body.Single(i=>i.Operand is MethodReference m&&m.Name=="SpendSpellSlot");
        var write=body.Single(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld&&i.Operand is FieldReference f&&f.Name=="usedSorceryPoints");
        var emit=body.Single(i=>i.Operand is MethodReference m&&m.Name=="Invoke");
        a(spend.Offset<write.Offset&&write.Offset<emit.Offset,"native debit -> Sorcery field write -> event, with no atomic receipt");
        a(create.Body.ExceptionHandlers.Count==0&&Calls(create).Any(m=>m.Name=="Max"),"CreateSorceryPoints clamps used counter but has no exception/rollback journal");
        a(!Calls(create).Any(m=>m.Name is "Heal" or "SpendSorceryPoints" or "AccountUsedMagicAndPower" or "GainSorceryPoints"),"this direction does not heal, spend Sorcery or call GainSorceryPoints");
        a(emit.Previous.OpCode.Code==Mono.Cecil.Cil.Code.Ldnull&&Calls(create).Any(m=>m.Name=="get_RemainingSorceryPoints"),"SorceryPointsAltered receives final remaining points and feature=null");
        var gain=actor.Methods.Single(m=>m.Name=="GainSorceryPoints");
        a(Calls(gain).Any(m=>m.Name=="Invoke")&&!Calls(gain).Any(m=>m.Name=="Heal"),"GainSorceryPoints is also an event-publishing counter mutation, not an atomic slot conversion");
        a(Calls(actor.Methods.Single(m=>m.Name=="SpendSorceryPoints")).Any(m=>m.Name=="Heal"),"healing belongs to spending Sorcery; no fabricated healing on slot-to-Sorcery");
        var rep=game.MainModule.Types.Single(t=>t.Name=="RulesetSpellRepertoire").Methods.Single(m=>m.Name=="SpendSpellSlot");
        var usedWrite=rep.Body.Instructions.First(i=>i.Operand is MethodReference m&&m.Name is "Add" or "set_Item");
        var refresh=rep.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="Invoke");
        a(usedWrite.Offset<refresh.Offset&&rep.Body.ExceptionHandlers.Count==0,"native slot count changes before RepertoireRefreshed; event exception can precede Sorcery write");
        var slots=types.Single(t=>t.Name=="StandardMulticasterResourcesContext");
        var project=slots.Methods.Single(m=>m.Name=="Project");
        a(project.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="usedSpellsSlots")&&Calls(project).Any(m=>m.Name=="Invoke"),"bound budget projects repertoire usage then publishes callbacks; view is not an independent second cost");
        var guard=types.Single(t=>t.Name=="SpellResourceOperationsContext").NestedTypes.Single(t=>t.Name=="CreateSorceryPatch").Methods.Single(m=>m.Name=="Prefix");
        a(Calls(guard).Any(m=>m.Name=="RequireNativeJournal"),"original native pre-mutation journal guard remains");
        var plan=types.Single(t=>t.Name=="FlexibleCastingSlotToSorceryPlan");
        var native=plan.Methods.Single(m=>m.Name=="CommitNative");
        a(!Calls(native).Any(m=>m.Name.Contains("CommitManaged")||m.Name is "SpendSpellSlot" or "CreateSorceryPoints" or "GainSorceryPoints" or "UsePower" or "Heal" or "RefundActionUse"),"new native boundary rejects without invoking debit/gain/parent/refund/managed arithmetic");
        a(Calls(plan.Methods.Single(m=>m.Name=="CommitManaged")).Any(m=>m.Name=="CommitManagedStandardSlot"),"model arithmetic uses existing operation coordinator, no second slot budget");
        var facade=types.Single(t=>t.Name=="FlexibleCastingPreflightPlan").Methods.Single(m=>m.Name=="CommitManagedStandardSlot");
        var order=Calls(facade).Select(m=>m.Name).ToArray();
        a(Array.IndexOf(order,"RequireStandardSlotToSorcery")<Array.IndexOf(order,"Revalidate")&&Array.IndexOf(order,"Revalidate")<Array.IndexOf(order,"CommitManaged"),"typed domain restriction and full ticket revalidation precede existing-ledger arithmetic");
        a(!types.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(Calls).Any(m=>m.DeclaringType.Name==plan.Name&&m.Name=="CommitManaged"),"managed commit has no native-runtime caller anywhere in compiled mod");
        var context=types.Single(t=>t.Name=="FlexibleCastingPreflightContext");
        a(Calls(context.Methods.Single(m=>m.Name=="BlockNativeCommit")).Any(m=>m.DeclaringType.Name==plan.Name&&m.Name=="CommitNative"),"gated native bridge reaches narrow standard-slot boundary");
        var callback=context.NestedTypes.Single(t=>t.Name=="ConvertPatch").Methods.Single(m=>m.Name=="Prefix");
        a(Calls(callback).First().Name=="get_RuntimeEnabled","gate-OFF ordinary callback exits before new code");
        a(!CompressedMulticasterProgressionPlan.MixedGameplayReady&&!T.NativeBoundaryAvailable,"mixed gameplay and native transaction remain closed");
    }
}
