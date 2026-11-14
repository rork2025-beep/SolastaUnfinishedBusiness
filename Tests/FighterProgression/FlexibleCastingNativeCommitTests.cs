using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using N=SolastaUnfinishedBusiness.Models.FlexibleCastingNativeCommitPlan;
using F=SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using V=SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using T=SolastaUnfinishedBusiness.Models.FlexibleCastingSlotToSorceryPlan;
using O=SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;
using S=SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using Fixture=FlexibleCastingPreflightTests.Fixture;

// This is the actual production commit executor with fake native field/notification ports.
// The Unity field host is compiled and inspected separately, never described as executed here.
internal static class FlexibleCastingNativeCommitTests
{
    private sealed class Rig(string other="Wizard",S.ResourceMode mode=S.ResourceMode.Slots,int remaining=6)
    {
        internal readonly Fixture F=new(mode,other,sorcery:remaining);
        private int next;
        internal readonly V Registry=new(()=>Guid.NewGuid().ToString("N"));
        internal readonly object Hero=new(),Power=new(),Rep=new(),Modal=new();
        internal int[][] Used,Caps;
        internal readonly int[] AuxiliaryUsed={7,8,9};
        internal int Writes,Parents,Hides,UiRecovery,Logs,Events;
        internal bool Gate=true,MainThread=true,Local=true,ProjectionGuard;
        internal readonly List<string> Order=new();
        internal Action Observe,OnPrepared;
        internal string ThrowAt;
        internal void Initialize(){var live=F.Live();Used=Enumerable.Range(0,3).Select(_=>live.Used.ToArray()).ToArray();Caps=Enumerable.Range(0,3).Select(_=>live.Capacities.ToArray()).ToArray();}
        internal Host Begin(int circle=1,bool create=false,bool shift=false,bool registered=false)
        {
            if(Used==null)Initialize();return new Host(this,++next,circle,create,shift,registered);
        }
        internal string Snapshot()=>F.Balances()+"/"+string.Join(";",Used.Select(row=>string.Join(",",row)))+"/"+Writes+"/"+Parents+"/"+Hides+"/"+Events;
        internal sealed class Host:N.IHost
        {
            private readonly Rig rig;
            internal readonly V.Authority Source;
            internal readonly V.Envelope Command;
            internal readonly object SourceEffect=new();
            internal bool Alive=true,ParentDone,Closed,InputDisabled;
            internal readonly int Circle;
            internal T.Draft Draft;
            public object Owner=>rig.Hero;
            public object Repertoire=>rig.Rep;
            public object Effect=>SourceEffect;
            public object Modal=>rig.Modal;
            private readonly bool create;
            internal Host(Rig owner,int id,int circle,bool createSlot,bool shift,bool registered)
            {
                rig=owner;Circle=circle;create=createSlot;
                Source=registered?rig.Registry.Register(Evidence(),Effect,id.ToString(),true,()=>Alive):rig.Registry.BeginCampaign(Evidence());
                if(!registered)rig.Registry.BindCampaign(Source,Evidence(),Effect,()=>Alive);
                rig.Registry.AttachModal(Source,Evidence(),Effect,Modal);
                Command=rig.Registry.Prepare(Source,Evidence(),Effect,Modal,rig.F.Model,new F.Choice(circle,create,shift,false),Read());
            }
            public V.Evidence Evidence()=>new("17",Owner,rig.Power,Repertoire,SpellResourceConsumerCatalog.FlexibleCasting(create).Definition,create,true);
            public F.Live Read()
            {
                var live=rig.F.Live();return new F.Live(live.Hero,live.Repertoire,live.Casting,live.Actual,live.Effective,live.Compressed,live.Eligible,live.Rules,
                    live.SorceryMaximum,rig.F.NativeSorcery,rig.Caps[0],rig.Used[0],live.PactUsed,live.PointPools,live.PointMaximum,live.PointRemaining,live.Costs,live.Conditions,live.PreferPact);
            }
            public void RequireAccess(bool replay)
            {
                if(!rig.Gate||!rig.MainThread)throw new O.Rejection(O.Code.Ineligible,"gate or main thread");
                if(!rig.Local)throw new O.Rejection(O.Code.PendingAtomicJournal,"network blocked");
                if(!replay&&(Source.Kind!=V.SourceKind.TrustedCampaignInvocation||ParentDone||Closed))throw new O.Rejection(O.Code.PendingAtomicJournal,"parent not supported/held");
            }
            public void Log(Exception error){rig.Logs++;if(rig.ThrowAt=="log")throw new InvalidOperationException("logger");}
            private void Notify(string name)
            {
                rig.Events++;rig.Order.Add(name);rig.Observe?.Invoke();
                if(rig.ThrowAt==name||rig.ThrowAt=="log"&&name=="r0")throw new InvalidOperationException(name);
            }
            public N.Publication Prepare(T.Draft draft)
            {
                Draft=draft;
                if(rig.Used.Any(row=>!row.SequenceEqual(draft.Before.Used))||rig.Caps.Any(row=>!row.SequenceEqual(draft.Before.Capacities)))throw new O.Rejection(O.Code.StaleOperation,"divergent repertoire views");
                if(rig.ThrowAt=="prepare")throw new InvalidOperationException("prepare");
                var before=rig.Used;var beforeSorcery=rig.F.NativeSorcery;
                var after=rig.Used.Select(row=>row.ToArray()).ToArray();foreach(var row in after)row[Circle-1]++;
                rig.OnPrepared?.Invoke();
                return new N.Publication(
                    ()=>{if(beforeSorcery!=rig.F.NativeSorcery||!ReferenceEquals(before,rig.Used)||rig.ThrowAt=="validate")throw new O.Rejection(O.Code.StaleOperation,"fields changed before write");},
                    ()=>{rig.ProjectionGuard=true;rig.Writes++;rig.F.NativeSorcery=draft.After.SorceryRemaining;rig.Used=after;},
                    ()=>{if(rig.F.Sorcery.Remaining!=rig.F.NativeSorcery||rig.Used.Any(row=>!row.SequenceEqual(draft.After.Used)))throw new InvalidOperationException("incoherent fields");},
                    Enumerable.Range(0,3).Select(i=>(Action)(()=>Notify("r"+i))).ToArray(),
                    new Action[]{()=>Notify("s0"),()=>Notify("s1"),()=>Notify("modal-refresh")},
                    ()=>{rig.Parents++;ParentDone=true;Source.CompleteParent();rig.Order.Add("parent-finish");},
                    new Action[]{()=>Notify("p0"),()=>Notify("p1")},
                    ()=>{rig.Hides++;rig.Order.Add("hide");if(rig.ThrowAt=="hide")throw new InvalidOperationException("hide");Closed=true;},
                    ()=>{rig.UiRecovery++;InputDisabled=true;},()=>rig.ProjectionGuard=false);
            }
        }
    }
    private sealed class SwappedHost(N.IHost host,string field):N.IHost
    {
        private readonly object replacement=new();
        public object Owner=>field=="owner"?replacement:host.Owner;
        public object Repertoire=>field=="repertoire"?replacement:host.Repertoire;
        public object Effect=>field=="effect"?replacement:host.Effect;
        public object Modal=>field=="modal"?replacement:host.Modal;
        public void RequireAccess(bool replay)=>host.RequireAccess(replay);
        public V.Evidence Evidence()=>host.Evidence();
        public F.Live Read()=>host.Read();
        public N.Publication Prepare(T.Draft draft)=>host.Prepare(draft);
        public void Log(Exception error)=>host.Log(error);
    }
    private static V.Envelope Conflict(V.Envelope c)=>new(c.Command,c.Invocation,c.Hero,c.Repertoire,c.Definition,c.Kind,c.Effect,c.Create,c.NativeCircle+1,c.ResourceCircle+1,c.Input,c.Output,c.Cost,c.Fingerprint);
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,TypeDefinition[] types)
    {
        var behavioral=0;var metadata=0;
        void B(bool ok,string name){behavioral++;check(ok,"13.7B.2 executed commit adapter/fake native port: "+name);}
        void A(bool ok,string name){metadata++;check(ok,"13.7B.2 compiled/native signature audit: "+name);}
        void Rejected(Rig rig,Action action,O.Code code,string name)
        {
            var before=rig.Snapshot();var rejected=false;try{action();}catch(O.Rejection ex){rejected=ex.Diagnostic==code;}
            B(rejected,name+" structured "+code);B(before==rig.Snapshot(),name+" exact budget/counter/views/events/parent/modal unchanged");
        }
        foreach(var other in new[]{"Wizard","Warlock"})foreach(var points in new[]{0,6,10,11})for(var circle=1;circle<=5;circle++)
        {
            var rig=new Rig(other,remaining:points);var host=rig.Begin(circle);var slots=rig.F.Resources.Standard.RemainingSlots(circle);var pact=rig.F.Resources.Pact.Remaining;var observed=0;
            rig.Observe=()=>
            {
                observed++;
                B(rig.F.Sorcery.Remaining==points+Math.Min(circle,11-points)&&rig.F.NativeSorcery==rig.F.Sorcery.Remaining&&rig.F.Resources.Standard.RemainingSlots(circle)==slots-1,"every observer sees ledger + native Sorcery committed together");
                B(rig.ProjectionGuard&&rig.Used.All(row=>row.SequenceEqual(host.Draft.After.Used))&&rig.Registry.NativeCommits.TryReceipt(host.Command.Command,out var receipt)&&receipt.Committed,"every observer sees all views and same committed receipt");
            };
            var result=rig.Registry.NativeCommits.Execute(host.Command,host);
            B(result.Committed&&!result.Indeterminate&&!rig.ProjectionGuard&&result.ResourceReceipt.Operation==host.Command.Command,"one same-identity native receipt");
            B(rig.Writes==1&&rig.F.Resources.Standard.RemainingSlots(circle)==slots-1&&rig.F.NativeSorcery==points+Math.Min(circle,11-points)&&rig.F.Resources.Pact.Remaining==pact,"one standard debit, native clamp/credit, Pact unchanged");
            B(observed==8&&rig.Order.SequenceEqual(new[]{"r0","r1","r2","s0","s1","modal-refresh","parent-finish","p0","p1","hide"}),"notifications after coherent writes, repertoire -> Sorcery -> parent -> modal in order");
            B(rig.Parents==1&&rig.Hides==1&&result.ParentCompleted&&result.ModalCompleted&&host.Source.ParentCompleted,"owned parent and modal finish once");
            var after=rig.Snapshot();B(ReferenceEquals(result,rig.Registry.NativeCommits.Execute(host.Command,host))&&after==rig.Snapshot(),"duplicate receipt with zero resource/notification/parent/UI replay");
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(Conflict(host.Command),host),O.Code.ConflictingOperation,"conflicting delivered command");
            B(rig.AuxiliaryUsed.SequenceEqual(new[]{7,8,9})&&rig.F.NativeRefunds==0&&rig.F.Conditions.SequenceEqual(new[]{"original-condition"}),"independent auxiliary state, refunds and conditions untouched");
            var ack=new V.Acknowledgement(host.Command.Command,host.Source.Id,result);B(ack.NativeCompleted&&ack.ParentCompleted&&ack.ModalCompleted&&ack.Result=="NativeCommitted","native acknowledgement issued only after both resources committed");
            host.Alive=false;B(ReferenceEquals(result,rig.Registry.NativeCommits.Execute(host.Command,host)),"historical exact receipt survives source termination; never reauthorizes a conversion");
        }
        foreach(var failure in new[]{"r0","s0","modal-refresh","p0","hide","log"})
        {
            var rig=new Rig();var host=rig.Begin(3);rig.ThrowAt=failure;
            var result=rig.Registry.NativeCommits.Execute(host.Command,host);
            B(result.Committed&&result.NotificationFailures==1&&rig.F.NativeSorcery==9&&rig.F.Sorcery.Remaining==9,"listener/UI failure after commit preserves both resources and receipt: "+failure);
            B(result.ParentCompleted&&rig.Parents==1&&rig.Hides==1&&rig.Events==8,"remaining notifications/parent/modal attempted once after failure: "+failure);
            B(failure!="hide"?result.ModalCompleted:!result.ModalCompleted&&rig.UiRecovery==1&&host.InputDisabled,"safe UI recovery without parent refund or repeated hide: "+failure);
            var after=rig.Snapshot();B(ReferenceEquals(result,rig.Registry.NativeCommits.Execute(host.Command,host))&&after==rig.Snapshot(),"failed notification cannot replay debit/grant/observers: "+failure);
            B(new V.Acknowledgement(host.Command.Command,host.Source.Id,result).Result=="NativeCommittedWithNotificationFailures","failure acknowledgement describes committed resources");
        }
        foreach(var fault in new[]{"prepare","validate","cancel","rules"})
        {
            var rig=new Rig();var host=rig.Begin();var before=rig.Snapshot();var rejected=false;
            if(fault is "prepare" or "validate")rig.ThrowAt=fault;
            if(fault=="cancel")rig.OnPrepared=()=>rig.Registry.Close(host.Source,V.Status.Cancelled);
            if(fault=="rules")rig.OnPrepared=()=>rig.F.Rules="changed";
            try{rig.Registry.NativeCommits.Execute(host.Command,host);}catch(InvalidOperationException){rejected=true;}
            B(rejected&&before==rig.Snapshot()&&!host.ParentDone&&!host.Closed,"failure before publication has zero resource/view/event/parent mutations: "+fault);
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(host.Command,host),O.Code.PendingAtomicJournal,"rejected command remains terminal: "+fault);
        }
        {
            var rig=new Rig();var host=rig.Begin();var before=rig.Snapshot();rig.Registry.Close(host.Source,V.Status.Cancelled);
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(host.Command,host),O.Code.StaleOperation,"cancel before native entry");
            B(before==rig.Snapshot(),"pre-commit cancel has no spending or completion");
        }
        {
            var rig=new Rig();var host=rig.Begin();var rejected=0;
            rig.Observe=()=>{try{rig.Registry.NativeCommits.Execute(host.Command,host);}catch(O.Rejection ex){if(ex.Diagnostic==O.Code.PendingAtomicJournal)rejected++;}};
            var result=rig.Registry.NativeCommits.Execute(host.Command,host);
            B(result.Committed&&rejected==8&&rig.Writes==1&&rig.Parents==1&&rig.Hides==1,"reentrant observer rejected at every publication stage");
            // A fresh legitimate invocation can advance after the prior reservation/modal is retired.
            rig.Observe=null;var second=rig.Begin();var next=rig.Registry.NativeCommits.Execute(second.Command,second);
            B(next.Committed&&!ReferenceEquals(result,next)&&rig.Writes==2&&rig.F.NativeSorcery==8&&rig.Parents==2&&rig.Hides==2,"two distinct issued invocations each commit once, no lifetime class lock");
        }
        {
            var rig=new Rig();var host=rig.Begin();rig.Used[1][0]++;
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(host.Command,host),O.Code.StaleOperation,"divergent secondary repertoire refused before publication");
        }
        {
            var rig=new Rig();var host=rig.Begin();
            while(rig.F.Resources.Standard.CanSpend(1))rig.F.Resources.Standard.Spend(Guid.NewGuid().ToString(),1);
            rig.Initialize();
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(host.Command,host),O.Code.StaleOperation,"exhausted competing standard budget invalidates ticket without new debit");
        }
        {
            var rig=new Rig();var host=rig.Begin();var receipt=rig.Registry.NativeCommits.Execute(host.Command,host);
            Rejected(rig,()=>rig.Registry.Close(host.Source,V.Status.Cancelled),O.Code.PendingAtomicJournal,"committed cancellation never refunds");
            rig.F.Rules="new-rules";
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(host.Command,host),O.Code.RuleChanged,"recorded identity cannot be replayed under changed rules");
            B(receipt.Committed&&rig.Writes==1,"refused replay retains confirmed receipt/debt");
        }
        {
            var rig=new Rig();var host=rig.Begin();var c=host.Command;
            var arcanum=new V.Envelope(c.Command,c.Invocation,c.Hero,c.Repertoire,c.Definition,c.Kind,c.Effect,c.Create,c.NativeCircle,c.ResourceCircle,O.Domain.Arcanum,c.Output,c.Cost,c.Fingerprint);
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(arcanum,host),O.Code.ConflictingOperation,"Arcanum cannot replace sealed standard source");
        }
        foreach(var field in new[]{"owner","repertoire","effect","modal"})
        {
            var rig=new Rig();var host=rig.Begin();
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(host.Command,new SwappedHost(host,field)),O.Code.WrongOwner,"wrong exact native host "+field);
        }
        foreach(var mode in new[]{"gate-off","worker","network","reverse","points","pact","registered-parent"})
        {
            var rig=new Rig(mode=="pact"?"Warlock":"Wizard",mode=="points"?S.ResourceMode.SpellPoints:S.ResourceMode.Slots);
            var host=rig.Begin(create:mode=="reverse",shift:mode=="pact",registered:mode=="registered-parent");
            if(mode=="gate-off")rig.Gate=false;if(mode=="worker")rig.MainThread=false;if(mode=="network")rig.Local=false;
            Rejected(rig,()=>rig.Registry.NativeCommits.Execute(host.Command,host),mode is "gate-off" or "worker"?O.Code.Ineligible:mode is "network" or "registered-parent"?O.Code.PendingAtomicJournal:O.Code.WrongDomain,"excluded native path "+mode);
        }
        Audit(A,game,types);
        Console.WriteLine($"Stage13.7B.2: {behavioral} executed production commit-adapter checks with fake native ports; {metadata} compiled/native audits; Unity field host NOT executed.");
    }
    private static void Audit(Action<bool,string> a,AssemblyDefinition game,TypeDefinition[] types)
    {
        MethodReference[] Calls(MethodDefinition m)=>m.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
        var context=types.Single(t=>t.Name=="FlexibleCastingPreflightContext");
        var fields=context.Methods.Single(m=>m.Name=="ApplyNativeFields");
        a(fields.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="usedSorceryPoints")&&fields.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="remainingUses"),"actual raw hero Sorcery and AtWill power fields are assigned");
        a(Calls(fields).All(m=>m.Name=="ApplyFields"),"raw native field batch has no observer/spend/gain/heal/refund API");
        var slot=types.Single(t=>t.Name=="StandardMulticasterResourcesContext").NestedTypes.Single(t=>t.Name=="FlexibleSlotViews");
        var view=slot.NestedTypes.Single(t=>t.Name=="View").Methods.Single(m=>m.Name=="Write");
        a(Calls(view).Length==0&&view.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="usedSpellsSlots")&&view.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="spellsSlotCapacities"),"each actual repertoire projection write is fields only");
        var host=context.NestedTypes.Single(t=>t.Name=="NativeHost");
        a(host.Interfaces.Any(i=>i.InterfaceType.FullName.Contains("FlexibleCastingNativeCommitPlan/IHost")),"game-field host implements the executed production adapter port");
        a(Calls(context.Methods.Single(m=>m.Name=="ReceiveLocalCommand")).Any(m=>m.DeclaringType.Name=="FlexibleCastingNativeCommitPlan"&&m.Name=="Execute"),"actual local envelope receiver dispatches to native executor");
        var methods=context.Methods.Concat(context.NestedTypes.SelectMany(t=>t.Methods)).Where(m=>m.HasBody).SelectMany(Calls).ToArray();
        a(!methods.Any(m=>m.Name is "CreateSorceryPoints" or "SpendSpellSlot" or "GainSorceryPoints" or "Heal" or "RefundActionUse"),"native route never calls old non-atomic executor or healing/refund");
        a(Calls(context.Methods.Single(m=>m.Name=="RequireLocalSinglePlayer")).Any(m=>m.Name=="get_IsMultiplayerGame"),"actual native host rejects multiplayer even with a local command manager");
        var original=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter").Methods.Single(m=>m.Name=="set_UsedSorceryPoints");
        a(Calls(original).Length==0,"native Sorcery setter has no publication callback");
        var usable=game.MainModule.Types.Single(t=>t.Name=="RulesetUsablePower");
        a(Calls(usable.Methods.Single(m=>m.Name=="Consume")).Length==0&&Calls(usable.Methods.Single(m=>m.Name=="Recharge")).All(m=>m.Name=="get_MaxUses"),"AtWill consume/recharge final remaining field can be staged without observer");
        using var unity=AssemblyDefinition.ReadAssembly("SolastaFiles/Solasta_Data/Managed/UnityEngine.CoreModule.dll");
        a(unity.MainModule.Types.Single(t=>t.Name=="UnitySynchronizationContext").Fields.Any(f=>f.Name=="m_MainThreadID"&&f.FieldType.FullName=="System.Int32"),"installed Unity main-thread identity field exists");
        a(Calls(context.Methods.Single(m=>m.Name=="RequireUnityMainThread")).Any(m=>m.Name=="get_ManagedThreadId"),"actual native host verifies thread identity instead of trusting first caller");
        foreach(var delegateName in new[]{"SorceryPointsAlteredHandler","PowerActivatedHandler"})
            a(game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter").NestedTypes.Single(t=>t.Name==delegateName).Methods.Single(m=>m.Name=="Invoke").Parameters.Count==3,"native delegate ABI "+delegateName);
        a(!CompressedMulticasterProgressionPlan.MixedGameplayReady,"gameplay gate remains OFF; no Unity execution inferred");
    }
}
