using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using F=SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using V=SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using N=SolastaUnfinishedBusiness.Models.FlexibleCastingNativeCommitPlan;
using T=SolastaUnfinishedBusiness.Models.FlexibleCastingSlotToSorceryPlan;
using O=SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;
using S=SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using P=SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using Fixture=FlexibleCastingPreflightTests.Fixture;

internal static class FlexibleCastingMatrixTests
{
    private sealed class Rig(string other,S.ResourceMode mode,bool three=false,int sorcery=11)
    {
        internal readonly Fixture F=new(mode,other,sorcery:sorcery,threeClasses:three);
        internal readonly V Registry=new(()=>Guid.NewGuid().ToString("N"));
        internal readonly object Hero=new(),PowerGain=new(),PowerCreate=new(),Rep=new(),Modal=new(),PointPool=new();
        internal int NativePoints,NativePact,Events,Parents,Hides,Writes;
        internal int Conditions=0;
        internal int[][] Used,Caps;
        internal bool Guard,Local=true,Gate=true,Main=true;
        internal string Fault;
        internal Action Observe,BeforeWrite;
        internal void Project()
        {
            var live=F.Live();NativePoints=live.PointRemaining;NativePact=live.PactUsed;
            Used=Enumerable.Range(0,3).Select(_=>live.Used.ToArray()).ToArray();Caps=Enumerable.Range(0,3).Select(_=>live.Capacities.ToArray()).ToArray();
        }
        internal Host Begin(int circle,bool create=false,bool shift=false)
        {if(Used==null)Project();return new Host(this,circle,create,shift);}
        internal string Snapshot()=>F.Balances()+"/"+NativePoints+"/"+NativePact+"/"+string.Join(";",Used.Select(x=>string.Join(",",x)))+"/"+Events+"/"+Parents+"/"+Hides+"/"+Writes+"/"+Conditions;
        internal sealed class Host:N.IHost
        {
            private readonly Rig rig;private readonly bool create;private readonly int circle;
            internal readonly V.Authority Source;
            internal readonly V.Envelope Command;
            internal readonly object EffectRef=new();
            internal bool Alive=true,Done,Hidden;
            internal T.Draft Draft;
            public object Owner=>rig.Hero;public object Repertoire=>rig.Rep;public object Modal=>rig.Modal;public object Effect=>EffectRef;
            internal Host(Rig owner,int level,bool creation,bool shift)
            {
                rig=owner;circle=level;create=creation;Source=rig.Registry.BeginCampaign(Evidence());rig.Registry.BindCampaign(Source,Evidence(),Effect,()=>Alive);
                rig.Registry.AttachModal(Source,Evidence(),Effect,Modal);
                Command=rig.Registry.Prepare(Source,Evidence(),Effect,Modal,rig.F.Model,new F.Choice(circle,create,shift,rig.F.Preference),Read());
            }
            public V.Evidence Evidence()=>new("17",Owner,create?rig.PowerCreate:rig.PowerGain,Repertoire,SpellResourceConsumerCatalog.FlexibleCasting(create).Definition,create,true);
            public F.Live Read()
            {
                var live=rig.F.Live();return new F.Live(live.Hero,live.Repertoire,live.Casting,live.Actual,live.Effective,live.Compressed,live.Eligible,live.Rules,
                    live.SorceryMaximum,rig.F.NativeSorcery,rig.Caps[0],rig.Used[0],rig.NativePact,live.PointPools,live.PointMaximum,rig.NativePoints,live.Costs,live.Conditions,rig.F.Preference);
            }
            public void RequireAccess(bool replay)
            {
                if(!rig.Gate||!rig.Main)throw new O.Rejection(O.Code.Ineligible,"gate/main thread");
                if(!rig.Local)throw new O.Rejection(O.Code.PendingAtomicJournal,"network");
                if(!replay&&(Done||Hidden||rig.Guard))throw new O.Rejection(O.Code.PendingAtomicJournal,"parent not held");
            }
            public void Log(Exception error){}
            public N.Publication Prepare(T.Draft draft)
            {
                Draft=draft;var previous=rig.Snapshot();
                if(rig.Fault=="prepare")throw new InvalidOperationException("prepare");
                var used=Enumerable.Range(0,3).Select(_=>draft.After.Used.ToArray()).ToArray();
                rig.BeforeWrite?.Invoke();
                void Notify(){rig.Events++;rig.Observe?.Invoke();if(rig.Fault=="notify")throw new InvalidOperationException("notify");}
                return new N.Publication(
                    ()=>{if(rig.Snapshot()!=previous)throw new O.Rejection(O.Code.StaleOperation,"native snapshot changed");},
                    ()=>{rig.Guard=true;rig.Writes++;rig.NativePoints=draft.After.PointRemaining;rig.NativePact=draft.After.PactUsed;rig.F.NativeSorcery=draft.After.SorceryRemaining;rig.Used=used;},
                    ()=>{var oracle=rig.F.Live();if(!rig.Used.All(row=>row.SequenceEqual(oracle.Used))||rig.NativePoints!=oracle.PointRemaining||rig.NativePact!=oracle.PactUsed||rig.F.NativeSorcery!=rig.F.Sorcery.Remaining)throw new InvalidOperationException("oracle coherence");},
                    new Action[]{Notify,Notify,Notify},new Action[]{Notify},
                    ()=>{rig.Parents++;Done=true;Source.CompleteParent();},new Action[]{Notify},()=>{rig.Hides++;Hidden=true;},()=>{},()=>rig.Guard=false);
            }
        }
    }
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,TypeDefinition[] types)
    {
        var count=0;var metadata=0;
        void B(bool ok,string name){count++;check(ok,"13.7B.3 production adapter/fake native fields: "+name);}
        void A(bool ok,string name){metadata++;check(ok,"13.7B.3 native/definition audit: "+name);}
        var costs=CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="SpellPointsContext"),"SpellCostByLevel").Where(n=>n>=0).ToArray();
        void Reject(Rig rig,Action action,O.Code code,string name)
        {
            var before=rig.Snapshot();var yes=false;try{action();}catch(O.Rejection e){yes=e.Diagnostic==code;}
            B(yes,name+" structured "+code);B(before==rig.Snapshot(),name+" no debit/credit/native view/condition/event/parent mutation");
        }
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})foreach(var other in new[]{"Wizard","Warlock"})
        foreach(var three in new[]{false,true})foreach(var create in new[]{false,true})for(var circle=1;circle<=5;circle++)
        {
            var rig=new Rig(other,mode,three);var host=rig.Begin(circle,create);
            if(create&&mode==S.ResourceMode.Slots)
            {
                Reject(rig,()=>rig.Registry.NativeCommits.Execute(host.Command,host),O.Code.PendingAtomicJournal,"funded slot condition lifecycle remains blocked circle"+circle);continue;
            }
            var before=rig.F.Live();var points=rig.NativePoints;var pact=rig.F.Resources.Pact.Remaining;var available=rig.F.Resources.Standard.RemainingSlots(circle);var sorcery=rig.F.NativeSorcery;var observations=0;
            rig.Observe=()=>
            {
                observations++;var oracle=rig.F.Live();
                B(rig.Guard&&rig.NativePoints==oracle.PointRemaining&&rig.NativePact==oracle.PactUsed&&rig.Used.All(row=>row.SequenceEqual(oracle.Used))&&rig.F.NativeSorcery==rig.F.Sorcery.Remaining,"all-domain authoritative oracle agrees at every observer");
            };
            var result=rig.Registry.NativeCommits.Execute(host.Command,host);
            B(result.Committed&&result.ParentCompleted&&result.ModalCompleted&&rig.Parents==1&&rig.Hides==1&&rig.Writes==1&&observations==5&&!rig.Guard,"all supported directions complete native fields/receipt/owned parent/modal once");
            B(rig.F.NativeSorcery==(create?sorcery-host.Command.Cost:Math.Min(11,sorcery+circle)),"Sorcery uses actual installed cost or native clamped UI-circle gain");
            B(rig.F.Resources.Pact.Remaining==pact,"ordinary Standard/SP selection never debits Pact");
            if(mode==S.ResourceMode.SpellPoints)B(rig.NativePoints==points+(create?costs[circle]:-costs[circle]),"installed SP cost/temporary bonus value, no duplicate pool");
            else B(rig.F.Resources.Standard.RemainingSlots(circle)==available-1,"B.2 standard slot path retained");
            if(create)
            {
                B(result.FundingIdentity==O.CoordinatorKey(host.Command.Hero,host.Command.Command)&&rig.F.Resources.Standard.HasTemporaryCredit(result.FundingIdentity,circle),"temporary point credit has same exact operation funding identity");
                B(rig.NativePoints>rig.F.Resources.Standard.Current.PointMaximum&&rig.Conditions==0,"bonus points may exceed base maximum without any slot condition");
                for(var n=0;n<3;n++)rig.F.Resources.Standard.Refresh(rig.F.Resources.Standard.Current);
                var unchanged=rig.NativePoints;rig.Project();B(rig.NativePoints==unchanged,"repeated projection/refresh does not regrant temporary credit");
            }
            var after=rig.Snapshot();B(ReferenceEquals(result,rig.Registry.NativeCommits.Execute(host.Command,host))&&after==rig.Snapshot(),"duplicate all-domain receipt cannot spend/grant/publish again");
            var c=host.Command;var conflict=new V.Envelope(c.Command,c.Invocation,c.Hero,c.Repertoire,c.Definition,c.Kind,c.Effect,c.Create,c.NativeCircle,c.ResourceCircle,c.Input,c.Output,c.Cost+1,c.Fingerprint);
            Reject(rig,()=>rig.Registry.NativeCommits.Execute(conflict,host),O.Code.ConflictingOperation,"changed cost under same ID");
            B(Enumerable.Range(6,4).All(n=>!rig.F.Resources.Pact.ArcanumUsed(n)),"no Arcanum conversion or false caster contribution");
        }
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})foreach(var priority in new[]{false,true})foreach(var shift in new[]{false,true})foreach(var circle in new[]{1,5})
        {
            var rig=new Rig("Warlock",mode,sorcery:0);rig.F.Preference=priority;var host=rig.Begin(circle,shift:shift);
            var pactBefore=rig.F.Resources.Pact.Remaining;var points=rig.NativePoints;var std=rig.F.Resources.Standard.RemainingSlots(circle);
            var result=rig.Registry.NativeCommits.Execute(host.Command,host);var usePact=circle==5?(priority||shift):!priority&&shift;
            B(host.Command.Input==(usePact?O.Domain.PactSlots:mode==S.ResourceMode.SpellPoints?O.Domain.SpellPoints:O.Domain.StandardSlots),"installed Shift/priority selects exact domain");
            B(rig.F.Resources.Pact.Remaining==pactBefore-(usePact?1:0)&&rig.F.NativeSorcery==circle,"Pact actual5 can award UI1 without Arcanum/free SP conversion");
            B(!usePact||host.Command.ResourceCircle==5&&host.Command.NativeCircle==circle&&rig.NativePoints==points&&rig.F.Resources.Standard.RemainingSlots(circle)==std,"lossy Pact debits one actual slot5; Standard/SP untouched");
            B(result.Committed&&rig.NativePact==rig.F.Resources.Pact.Used,"Pact sentinel and all views committed together");
        }
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})foreach(var exhaust in new[]{"pact","standard","both"})
        {
            var rig=new Rig("Warlock",mode);rig.Project();
            if(exhaust is "pact" or "both")while(rig.F.Resources.Pact.Remaining>0)rig.F.Resources.Pact.Spend(Guid.NewGuid().ToString(),P.Source.Pact,5);
            if(exhaust is "standard" or "both")while(rig.F.Resources.Standard.CanSpend(1))rig.F.Resources.Standard.Spend(Guid.NewGuid().ToString(),1);
            rig.Project();
            if(exhaust=="both")Reject(rig,()=>rig.Begin(1),O.Code.Exhausted,"both resources exhausted before native ticket");
            else
            {
                var host=rig.Begin(1,shift:exhaust=="pact");var result=rig.Registry.NativeCommits.Execute(host.Command,host);
                B(result.Committed&&host.Command.Input==(exhaust=="standard"?O.Domain.PactSlots:mode==S.ResourceMode.SpellPoints?O.Domain.SpellPoints:O.Domain.StandardSlots),"native availability fallback without invented resource");
            }
        }
        foreach(var other in new[]{"Wizard","Warlock"})foreach(var create in new[]{false,true})
        {
            var rig=new Rig(other,S.ResourceMode.SpellPoints);var host=rig.Begin(2,create);rig.Fault="notify";
            var result=rig.Registry.NativeCommits.Execute(host.Command,host);var after=rig.Snapshot();
            B(result.Committed&&result.NotificationFailures==5&&result.ParentCompleted&&rig.Hides==1,"notification failures do not undo any domain or repeat parent");
            B(ReferenceEquals(result,rig.Registry.NativeCommits.Execute(host.Command,host))&&after==rig.Snapshot(),"failed listeners never replay paid resources/bonus");
            var cancelled=new Rig(other,S.ResourceMode.SpellPoints);var c=cancelled.Begin(2,create);cancelled.Registry.Close(c.Source,V.Status.Cancelled);
            Reject(cancelled,()=>cancelled.Registry.NativeCommits.Execute(c.Command,c),O.Code.StaleOperation,"cancelled all-domain native command");
            var stale=new Rig(other,S.ResourceMode.SpellPoints);var s=stale.Begin(2,create);stale.BeforeWrite=()=>stale.F.Rules="changed";
            Reject(stale,()=>stale.Registry.NativeCommits.Execute(s.Command,s),O.Code.RuleChanged,"rules revalidated immediately before native write");
        }
        {
            var rig=new Rig("Wizard",S.ResourceMode.SpellPoints);var host=rig.Begin(1,true);var nested=0;
            rig.Observe=()=>{try{rig.Registry.NativeCommits.Execute(host.Command,host);}catch(O.Rejection e){if(e.Diagnostic==O.Code.PendingAtomicJournal)nested++;}};
            rig.Registry.NativeCommits.Execute(host.Command,host);B(nested==5&&rig.Writes==1,"reverse SP reentrant listeners cannot repeat Sorcery debit/bonus");
        }
        {
            var rig=new Rig("Warlock",S.ResourceMode.SpellPoints);var host=rig.Begin(1,true);var result=rig.Registry.NativeCommits.Execute(host.Command,host);var credit=rig.NativePoints;
            // Exercise high-circle exhaustion before temporary credit/refresh: credit does not reset it.
            rig.F.Resources.Standard.Spend("high-circle",6);rig.Project();var highUsed=rig.F.Resources.Standard.HighCircleSpent(6);
            rig.F.Model.SynchronizeNativeRest("short-rest",false,11,rig.F.NativeSorcery);rig.Project();
            B(highUsed&&rig.F.Resources.Standard.HighCircleSpent(6)&&rig.NativePoints==credit-costs[6],"short rest preserves temporary SP balance/high-circle restriction");
            rig.F.NativeSorcery=11;rig.F.Model.SynchronizeNativeRest("long-rest",true,11,11);rig.Project();
            B(rig.NativePoints==rig.F.Resources.Standard.Current.PointMaximum&&!rig.F.Resources.Standard.HighCircleSpent(6)&&rig.NativePact==0,"long rest expires bonus credit and restores existing domains once");
            var after=rig.Snapshot();rig.F.Model.SynchronizeNativeRest("long-rest",true,11,11);rig.Project();B(after==rig.Snapshot(),"repeat same rest no bonus regeneration");
            B(ReferenceEquals(result,rig.Registry.NativeCommits.Execute(host.Command,host))&&after==rig.Snapshot(),"late pre-rest conversion receipt cannot recreate expired bonus");
        }
        foreach(var create in new[]{false,true})foreach(var changed in new[]{"points","sorcery","source"})
        {
            var rig=new Rig("Wizard",S.ResourceMode.SpellPoints);var host=rig.Begin(2,create);
            var std=rig.F.Resources.Standard.RemainingPoints;var sorc=rig.F.Sorcery.Remaining;var caught=false;
            rig.BeforeWrite=()=>{if(changed=="points")rig.NativePoints--;else if(changed=="sorcery")rig.F.NativeSorcery--;else host.Alive=false;};
            try{rig.Registry.NativeCommits.Execute(host.Command,host);}catch(O.Rejection){caught=true;}
            B(caught&&std==rig.F.Resources.Standard.RemainingPoints&&sorc==rig.F.Sorcery.Remaining&&rig.Writes==0&&rig.Parents==0&&rig.Events==0,"fresh native "+changed+" invalidation before all-domain publication");
        }
        {
            var rig=new Rig("Warlock",S.ResourceMode.SpellPoints);rig.Project();rig.F.Preference=true;
            while(rig.F.Resources.Standard.CanSpend(1))rig.F.Resources.Standard.Spend(Guid.NewGuid().ToString(),1);
            rig.Project();
            Reject(rig,()=>rig.Begin(1,shift:true),O.Code.Exhausted,"installed priority=true does not authorize lossy Pact5/UI1 fallback");
        }
        foreach(var excluded in new[]{"network","gate","worker","insufficient-sorcery"})
        {
            var rig=new Rig("Wizard",S.ResourceMode.SpellPoints,sorcery:excluded=="insufficient-sorcery"?0:11);rig.Project();
            if(excluded=="insufficient-sorcery"){Reject(rig,()=>rig.Begin(5,true),O.Code.Exhausted,"native source lacks actual Sorcery, no shared-slot substitution");continue;}
            var h=rig.Begin(1,true);if(excluded=="network")rig.Local=false;if(excluded=="gate")rig.Gate=false;if(excluded=="worker")rig.Main=false;
            Reject(rig,()=>rig.Registry.NativeCommits.Execute(h.Command,h),excluded=="network"?O.Code.PendingAtomicJournal:O.Code.Ineligible,"blocked native route "+excluded);
        }
        Audit(A,game,types);
        Console.WriteLine($"Stage13.7B.3: {count} executed production matrix-adapter/fake native field checks; {metadata} compiled/native/condition-definition audits; no Unity execution; slot-condition route BLOCKED.");
    }
    private static void Audit(Action<bool,string> a,AssemblyDefinition game,TypeDefinition[] types)
    {
        MethodReference[] Calls(MethodDefinition m)=>m.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
        var actor=game.MainModule.Types.Single(t=>t.Name=="RulesetActor");
        var add=actor.Methods.Single(m=>m.Name=="AddConditionOfCategory");var names=Calls(add).Select(m=>m.Name).ToArray();
        a(Array.IndexOf(names,"OnConditionAdded")>=0&&Array.IndexOf(names,"OnConditionAdded")<Array.IndexOf(names,"RefreshAll")&&names.Contains("Invoke"),"native attach publishes condition callbacks before final refresh; needs dedicated funded lifecycle");
        var remove=actor.Methods.Single(m=>m.Name=="RemoveConditionOfCategory");names=Calls(remove).Select(m=>m.Name).ToArray();
        a(Array.IndexOf(names,"Remove")<Array.IndexOf(names,"OnConditionRemoved")&&Array.IndexOf(names,"OnConditionRemoved")<Array.IndexOf(names,"Unregister")&&Array.IndexOf(names,"Unregister")<Array.IndexOf(names,"RefreshAll"),"native detach publishes before unregister/refresh; no grant funding identity");
        var create=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter").Methods.Single(m=>m.Name=="CreateSpellSlot");names=Calls(create).Select(m=>m.Name).ToArray();
        a(Array.IndexOf(names,"SpendSorceryPoints")<Array.IndexOf(names,"InflictCondition"),"legacy native creation spends before condition grant and cannot serve as executor");
        for(var circle=1;circle<=5;circle++)
        {
            using var json=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/ConditionDefinition/ConditionAdditionalSpellSlot"+circle+".json"));var root=json.RootElement;
            a(root.GetProperty("allowMultipleInstances").GetBoolean()&&root.GetProperty("durationType").GetString()=="UntilLongRest","native additional slot"+circle+" lifetime/multiplicity inspected, not implemented");
        }
        var host=types.Single(t=>t.Name=="FlexibleCastingPreflightContext").NestedTypes.Single(t=>t.Name=="NativeHost");
        a(Calls(host.Methods.Single(m=>m.Name=="RequireAccess")).All(m=>m.Name is not "GrantPowerSpellPoints"),"native host never creates replacement SP pools");
        var packet=types.Single(t=>t.Name=="StandardMulticasterResourcesContext").NestedTypes.Single(t=>t.Name=="FlexibleSlotViews");
        a(packet.Methods.Single(m=>m.Name=="ApplyFields").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="remainingUses"),"canonical actual SP power remaining field participates in callback-free publication");
        a(!packet.Methods.Where(m=>m.HasBody).SelectMany(Calls).Any(m=>m.Name is "InflictCondition" or "AddConditionOfCategory"),"SP grant never creates native slot conditions");
        var finish=types.Single(t=>t.Name=="StandardMulticasterResourcesContext").Methods.Single(m=>m.Name=="FinishRest");
        a(Calls(finish).Any(m=>m.Name=="SynchronizeNativeRest"),"native rest updates only existing active conversion mirror after native rest");
        var spend=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter").Methods.Single(m=>m.Name=="SpendSorceryPoints");
        a(Calls(spend).Any(m=>m.Name=="Heal")&&Calls(spend).Any(m=>m.Name=="AccountUsedMagicAndPower"),"native spending-side healing/accounting inspected; gaining path does not heal");
        a(!CompressedMulticasterProgressionPlan.MixedGameplayReady,"gameplay gate remains OFF");
    }
}
