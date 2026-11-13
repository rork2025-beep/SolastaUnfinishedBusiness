using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;
using K = SolastaUnfinishedBusiness.Models.SpellResourceConsumerCatalog;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using P = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;

internal static class SpellResourceOperationTests
{
    private static S.Tables tables;
    private static P.Tables pact;
    private static int[][] pactRows;
    private static Dictionary<int,int> creation;
    private static readonly int[] Full={1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    private static C.Result Progress(params (string Id,int Level)[] classes)
    {
        var inputs=classes.Select(c=>new C.ClassInput(c.Id,c.Level,true)).ToArray();
        return new C("M06-installed",classes.Select(c=>new C.ClassProfile(c.Id,true,c.Id is "Fighter" or "Rogue"?null:
            new C.CastingProfile("CastSpell"+c.Id,c.Id=="Warlock"?C.CasterKind.Pact:c.Id=="Inventor"?C.CasterKind.HalfRoundUp:c.Id is "Paladin" or "Ranger"?C.CasterKind.Half:C.CasterKind.Full,1,true,
                c.Id=="Warlock"?pactRows.Select(r=>Array.FindIndex(r,n=>n>0)+1):c.Id is "Inventor" or "Paladin" or "Ranger"?Enumerable.Range(1,20).Select(n=>Full[(n+1)/2-1]):Full))))
            .Calculate(new C.Snapshot(inputs.Sum(c=>c.ActualLevel),inputs,inputs.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel))));
    }
    private static P.CastingLedger Resources(S.ResourceMode mode,params (string Id,int Level)[] classes)
    {var result=Progress(classes.Length==0?new[]{("Warlock",12),("Sorcerer",8)}:classes);return new(new S.Budget(tables,S.Allocation.StandardPortion(tables,result,mode)),new P.Budget(new P.Allocation(pact,result)));}
    private static O.Counter Counter(string id,O.Domain d,int max=20,int remaining=10,string rest="LongRest",string owner="hero")=>new(id,owner,d,max,remaining,rest);
    private static O.Proof Proof(O.Source s,string input=null,IEnumerable<string> outputs=null,int cost=0,int level=20,bool eligible=true,bool repertoire=true,bool item=true,bool pact=true,bool lossy=false,bool activation=true,IEnumerable<O.NativeDebit> debits=null,string nativeEvent="operation",int? nativeResult=null,O.Domain? resolvedInput=O.Domain.PactSlots,int? resolvedCircle=5,int? resolvedNativeCircle=5)=>
        new(s.Id,s.Definition,s.Revision,repertoire?"native-repertoire":null,item?"item-guid":null,input,outputs,level,cost,eligible,pact,lossy,activation,debits,nativeEvent,nativeResult,resolvedInput,resolvedCircle,resolvedNativeCircle);
    private static O Plan(P.CastingLedger r,O.Source s,O.Proof p,params O.Counter[] counters)=>new("hero",K.Revision,r,new[]{s},new[]{p},counters,
        new[]{"Warlock","Sorcerer",s.OwnerClass}.Where(x=>x!=null).Distinct(),tables,creation);
    private static O.Request Request(O p,O.Source s,O.Domain input,O.Domain output,int circle,int cost,int result=0,string id="operation",int? native=null,IEnumerable<KeyValuePair<int,int>> recovery=null)=>
        new(id,"hero",s.Id,s.Definition,s.Revision,s.Operation,input,output,circle,native??circle,1,cost,result,p.State,recovery);
    private static bool Reject(Action action,O.Code code){try{action();return false;}catch(O.Rejection e){return e.Diagnostic==code;}}
    private static JsonElement Json(string type,string id)
    {var p=new[]{"OfficialBlueprints","UnfinishedBusinessBlueprints"}.Select(root=>"Diagnostics/"+root+"/"+type+"/"+id+".json").First(File.Exists);using var d=JsonDocument.Parse(File.ReadAllText(p));return d.RootElement.Clone();}
    private static Dictionary<int,int> Dice(string id)=>Json("FeatureDefinitionAdditionalDamage",id).GetProperty("diceByRankTable").EnumerateArray().ToDictionary(x=>x.GetProperty("rank").GetInt32(),x=>x.GetProperty("diceNumber").GetInt32());
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        int behavior=0,audit=0;
        void B(bool ok,string name){behavior++;check(ok,"M06 production behavior: "+name);}
        void A(bool ok,string name){audit++;check(ok,"M06 compiled/definition audit: "+name);}
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();
        int[] V(string t,string f)=>CompressedMulticasterTests.AddedIntegers(types.Single(x=>x.Name==t),f).Where(n=>n>=0).ToArray();
        var slots=V("FeatureDefinitionCastSpellBuilder","SlotsByCasterLevel");tables=new S.Tables(Enumerable.Range(1,20).Select(n=>slots.Skip(n*10).Take(9)),V("ModifyPowerPoolAmountPowerSpellPoints","SpellPointsByLevel"),V("SpellPointsContext","SpellCostByLevel"));
        var display=V("SharedSpellsContext","WarlockCastingSlots");pactRows=Enumerable.Range(0,20).Select(n=>{var row=display.Skip(n*6).Take(6).ToArray();var r=new int[9];r[Array.IndexOf(row,0)-1]=row[0];return r;}).ToArray();
        pact=new P.Tables(pactRows,new[]{(11,6),(13,7),(15,8),(17,9)}.Select(x=>new P.Arcanum("Arcanum"+x.Item2,x.Item1,x.Item2,new[]{"Arc"+x.Item2})));
        var il=game.MainModule.Types.Single(t=>t.Name=="RuleDefinitions").Methods.Single(m=>m.Name==".cctor").Body.Instructions;
        var end=il.Select((i,n)=>(i,n)).Single(x=>x.i.OpCode.Code==Code.Stsfld&&x.i.Operand is FieldReference f&&f.Name=="SpellSlotCreationCost").n;
        var begin=Enumerable.Range(0,end).Last(n=>il[n].OpCode.Code==Code.Newobj);creation=new();
        int Integer(Instruction i)=>i.OpCode.Code switch {Code.Ldc_I4_1=>1,Code.Ldc_I4_2=>2,Code.Ldc_I4_3=>3,Code.Ldc_I4_4=>4,Code.Ldc_I4_5=>5,Code.Ldc_I4_6=>6,Code.Ldc_I4_7=>7,_=>throw new Exception("Unexpected native creation cost IL")};
        for(var n=begin;n<end;n++)if(il[n].Operand is MethodReference m&&m.Name=="Add")creation.Add(Integer(il[n-2]),Integer(il[n-1]));
        A(creation.OrderBy(p=>p.Key).Select(p=>p.Value).SequenceEqual(new[]{2,3,5,6,7}),"installed native creation costs extracted from actual cctor, not tabletop fixture");
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})
        {
            Examples(B,mode);Flexible(B,mode);Consumers(B,A,mode);Failures(B,mode);Advanced(B,A,mode);RecoveryForms(B,A,mode);
        }
        Bindings(A,game,types);
        Console.WriteLine($"Stage13.6: {behavior} executed production-model behavior checks; {audit} compiled/definition audits; Unity/native execution not performed.");
    }
    private static void Examples(Action<bool,string> b,S.ResourceMode mode)
    {
        foreach(var x in new[]{("Wizard",11,73),("Inventor",6,32),("Sorcerer",11,73),("Fighter",0,0),("Paladin",5,27)})
        {
            var r=Resources(mode,("Warlock",12),(x.Item1,8));
            b(r.Standard.Current.CasterLevel==x.Item2&&r.Standard.Current.BasePointMaximum==x.Item3&&r.Pact.Current.Slots==4&&r.Pact.Current.Circle==5&&r.Pact.Current.Arcana.Count==4,$"W12/{x.Item1}8/{mode}: one standard budget,4x5 Pact, four distinct Arcanum grants");
        }
        var solo=Resources(mode,("Warlock",12));b(solo.Standard.Current.CasterLevel==0&&solo.Pact.Remaining==4,"Warlock only: no ordinary contribution");
        var triple=Resources(mode,("Warlock",8),("Sorcerer",6),("Paladin",6));b(triple.Standard.Current.CasterLevel==10&&triple.Pact.Remaining==3,"three-class existing grouped model unchanged");
        var ownerStore=new S.OwnerStore<object>(a=>a);var hero=new object();ownerStore.Bind(hero,solo.Standard);b(ownerStore.TryGet(hero,out var stored)&&ReferenceEquals(stored,solo.Standard),"owner store uses same authoritative budget, not cloned pool");
    }
    private static void Flexible(Action<bool,string> b,S.ResourceMode mode)
    {
        var standard=mode==S.ResourceMode.Slots?O.Domain.StandardSlots:O.Domain.SpellPoints;
        foreach(var input in new[]{standard,O.Domain.PactSlots})for(var circle=1;circle<=5;circle++)
        {
            var r=Resources(mode);var s=K.FlexibleCasting(false);var sorcery=Counter("sorcery",O.Domain.SorceryPoints,11,0);
            var p=Plan(r,s,Proof(s,outputs:new[]{sorcery.Id},lossy:true,resolvedInput:input,resolvedCircle:input==O.Domain.PactSlots?5:circle,resolvedNativeCircle:circle),sorcery);
            var actual=input==O.Domain.PactSlots?5:circle;var cost=input==O.Domain.SpellPoints?tables.Cost(actual):1;
            var q=Request(p,s,input,O.Domain.SorceryPoints,actual,cost,circle,native:circle);
            var before=p.State;var preview=p.Prepare(q);b(before==p.State&&preview.OutputAmount==circle,$"{mode}/{input}/{circle} prepare is read-only");
            var receipt=p.CommitManaged(q);b(sorcery.Remaining==circle&&receipt.EffectCircle==circle&&ReferenceEquals(p.Resources.Standard,r.Standard),"slot to Sorcery uses same coordinator and explicit actual/UI circle");
            var after=p.State;b(ReferenceEquals(receipt,p.CommitManaged(q))&&after==p.State,"repeat conversion exactly once");
            b(Reject(()=>p.Cancel(q.Operation),O.Code.PendingAtomicJournal),"no unsafe rollback after commit");
            b(Reject(()=>p.CommitManaged(Request(p,s,input,O.Domain.SorceryPoints,actual,cost,circle,id:q.Operation,native:circle)),O.Code.ConflictingOperation),"same ID with different snapshot conflicts");
            var gain=K.FlexibleCasting(false);var cap=Counter("cap",O.Domain.SorceryPoints,11,10);var capped=Plan(Resources(mode),gain,Proof(gain,outputs:new[]{cap.Id}),cap);
            capped.CommitManaged(Request(capped,gain,O.Domain.PactSlots,O.Domain.SorceryPoints,5,1,1));b(cap.Remaining==11,"native Sorcery gain clamps at capacity");
        }
        for(var circle=1;circle<=5;circle++)
        {
            var r=Resources(mode,("Sorcerer",6));var s=K.FlexibleCasting(true);var sorcery=Counter("sorcery",O.Domain.SorceryPoints,7,7);var p=Plan(r,s,Proof(s,sorcery.Id,cost:creation[circle]),sorcery);
            var outDomain=mode==S.ResourceMode.Slots?O.Domain.StandardSlots:O.Domain.SpellPoints;var outAmount=mode==S.ResourceMode.Slots?1:tables.Cost(circle);var points=r.Standard.RemainingPoints;
            var q=Request(p,s,O.Domain.SorceryPoints,outDomain,circle,creation[circle],outAmount);p.CommitManaged(q);
            b(sorcery.Remaining==7-creation[circle]&&(mode==S.ResourceMode.Slots?r.Standard.TemporarySlots(circle)==1:r.Standard.RemainingPoints==points+outAmount),$"{mode}/created{circle}: actual native cost and temporary capacity/bonus");
            b(circle<=4||mode==S.ResourceMode.Slots||!r.Standard.CanSpend(circle),"SP credits never unlock a higher resource circle; no change to class spell access");
            if(circle==5 && mode==S.ResourceMode.Slots)
            {
                var cast=new P.Cast("created-upcast","Sorcerer","CastSpellSorcerer","Shield",1,5,"Charisma",18,true,P.Source.Standard);
                var result=r.Spend(cast);b(result.Allowed&&result.Circle==5&&cast.Ability=="Charisma"&&cast.SaveDc==18,"created slot can upcast an owned lower-circle spell without changing class access or ability/DC");
            }
            var debt=sorcery.Remaining;p.Rest("short",false);b(sorcery.Remaining==debt&&(mode==S.ResourceMode.SpellPoints||r.Standard.TemporarySlots(circle)==1),"short rest keeps created slot and Sorcery debt");
            p.Rest("simulate",true,true);b(sorcery.Remaining==debt,"simulated long rest cannot refill");
            p.Rest("long",true);var rested=p.State;b(sorcery.Remaining==7&&r.Standard.TemporarySlots(circle)==0&&r.Standard.RemainingPoints==r.Standard.Current.PointMaximum,"long rest expires created capacity/credit and restores only native rest domains");
            p.Rest("long",true);b(p.State==rested&&ReferenceEquals(p.CommitManaged(q),p.CommitManaged(q)),"repeat rest and old callback cannot recreate slots or spend again");
        }
    }
    private static void Consumers(Action<bool,string> b,Action<bool,string> a,S.ResourceMode mode)
    {
        var ordinary=mode==S.ResourceMode.Slots?O.Domain.StandardSlots:O.Domain.SpellPoints;
        foreach(var eldritch in new[]{false,true})foreach(var circle in new[]{1,2,3,4,5})
        {
            var s=K.Smite(eldritch,Dice(eldritch?"AdditionalDamageInvocationEldritchSmite":"AdditionalDamagePaladinDivineSmite"));
            var r=Resources(mode);var p=Plan(r,s,Proof(s));var domain=eldritch?O.Domain.PactSlots:ordinary;var selected=eldritch?5:circle;
            var q=Request(p,s,domain,O.Domain.Effect,selected,domain==O.Domain.SpellPoints?tables.Cost(selected):1);var result=p.CommitManaged(q);
            b(result.BaseDice==(eldritch?6:Math.Min(circle+1,5))&&result.EffectCircle==selected,$"{mode} Smite/eldritch{eldritch}/{circle}: native slot-rank dice, no combat-effect simulation");
            b(ReferenceEquals(result,p.CommitManaged(q)),"Smite trigger is once per action");
            if(eldritch)b(Reject(()=>p.Prepare(Request(p,s,ordinary,O.Domain.Effect,1,mode==S.ResourceMode.SpellPoints?2:1,id:"wrong-domain")),O.Code.WrongDomain),"Eldritch Smite cannot use ordinary slots/SP");
        }
        foreach(var s in new[]{K.FreeMark(),K.StoredItemCreation("PowerCreateSpellStoringWandOfAid"),K.Item("SpellStoringWandOfAid:0",true),K.Item("WandMagicMissile:0",false),K.Power("PowerNativeClass","Druid",O.Domain.ClassPower),K.Power("PowerNativeInvocation","Warlock",O.Domain.InvocationUse)})
        {
            var d=s.Inputs.Single();var item=d is O.Domain.ItemCharges or O.Domain.StoredSpellUse;
            var counter=Counter("uses",d,6,6,"None",item?"item-guid":"hero");var r=Resources(mode);var p=Plan(r,s,Proof(s,"uses",cost:1),counter);
            var before=r.Standard.RemainingPoints;var pactBefore=r.Pact.Remaining;
            var q=Request(p,s,d,O.Domain.Effect,0,1);p.CommitManaged(q);
            b(counter.Remaining==5&&r.Standard.RemainingPoints==before&&r.Pact.Remaining==pactBefore,s.Id+" debits ONLY owned native domain");
            p.Rest("short",false);p.Rest("long",true);b(counter.Remaining==5,s.Id+" None-recharge charges not refilled by rest");
            b(ReferenceEquals(p.CommitManaged(q),p.CommitManaged(q))&&counter.Remaining==5,"old native-use callback after rest is not a second debit");
            b(Reject(()=>p.CommitNative(Request(p,s,d,O.Domain.Effect,0,1,id:"native")),O.Code.PendingAtomicJournal),"native action stays blocked before any model mutation");
        }
        foreach(var trade in new[]{("wild-shape-refund","PowerDruidWildResurgenceShape",1), ("paladin-capstone-refund","PowerRechargePowerPaladinCapstone",5), ("cannon-refund","PowerInnovationArtilleristEldritchCannonRefund",1), ("alchemy-refund","PowerInnovationAlchemyRefundFromSlot2",2), ("mixture-refund","PowerInnovationVitriolistRefundMixture",3)})
        {
            var s=K.SlotTrade(trade.Item1,trade.Item2);var target=Counter("target",O.Domain.ClassPower,6,0);var r=Resources(mode);var activation=Counter("activation",O.Domain.ClassPower,1,1,"TurnStart");var debits=trade.Item1=="wild-shape-refund"?new[]{new O.NativeDebit(activation.Id,O.Domain.ClassPower,1)}:Array.Empty<O.NativeDebit>();
            var p=Plan(r,s,Proof(s,outputs:new[]{target.Id},debits:debits),target,activation);
            var output=trade.Item1=="cannon-refund"?1:trade.Item1 is "alchemy-refund" or "mixture-refund"?trade.Item3:1;
            p.CommitManaged(Request(p,s,ordinary,O.Domain.ClassPower,trade.Item3,mode==S.ResourceMode.SpellPoints?tables.Cost(trade.Item3):1,output));
            b(target.Remaining==output,trade.Item1+" typed native target recovery/actual circle; pre-effect boundary still deferred");
        }
        var ward=K.WardRefill("PowerWizardAbjurationRechargeArcaneWard");var wardCounter=Counter("ward",O.Domain.ClassPower,20,0);var wardPlan=Plan(Resources(mode),ward,Proof(ward,outputs:new[]{"ward"}),wardCounter);
        wardPlan.CommitManaged(Request(wardPlan,ward,ordinary,O.Domain.ClassPower,3,mode==S.ResourceMode.SpellPoints?5:1,6));b(wardCounter.Remaining==6,"Arcane Ward source multiplier2 is independent of Sorcery/Spell Points");
        foreach(var id in new[]{"slay-enemies","blade-dance-reduction"})
        {var s=K.SlotEffect(id,id=="slay-enemies"?"PowerFeatSlayTheEnemies2":"NativeFeature",id=="slay-enemies"?"Ranger":"Bard");var activation=Counter("activation",O.Domain.ClassPower,6,6);var extra=id=="slay-enemies"?new[]{new O.NativeDebit(activation.Id,O.Domain.ClassPower,1)}:Array.Empty<O.NativeDebit>();var p=Plan(Resources(mode),s,Proof(s,debits:extra),activation);p.CommitManaged(Request(p,s,ordinary,O.Domain.Effect,2,mode==S.ResourceMode.SpellPoints?3:1));b(true,id+" selected circle is paid before managed effect receipt");}
        var wild=K.WildShapeToSlot("PowerDruidWildResurgenceSlot");var shape=Counter("shape",O.Domain.ClassPower,2,2,"ShortRest");var activationUse=Counter("activation",O.Domain.ClassPower,1,1);var wp=Plan(Resources(mode),wild,Proof(wild,shape.Id,cost:1,debits:new[]{new O.NativeDebit(activationUse.Id,O.Domain.ClassPower,1)}),shape,activationUse);
        wp.CommitManaged(Request(wp,wild,O.Domain.ClassPower,ordinary,1,1,mode==S.ResourceMode.SpellPoints?2:1));b(shape.Remaining==1,"Wild Resurgence typed opposite conversion spends shape without another spell pool");
        // Actual generated SSI definitions, including innovation spells; same production adapter.
        var items=Directory.GetFiles("Diagnostics/UnfinishedBusinessBlueprints/ItemDefinition","SpellStoringWandOf*.json");
        foreach(var file in items)
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(file));var root=doc.RootElement;var device=root.GetProperty("usableDeviceDescription");var fn=device.GetProperty("deviceFunctions")[0];
            var id=root.GetProperty("name").GetString();var source=K.Item(id+":0",true);var count=device.GetProperty("chargesCapitalNumber").GetInt32();var cost=fn.GetProperty("useAmount").GetInt32();
            var counter=Counter("item",O.Domain.StoredSpellUse,count,count,device.GetProperty("rechargeRate").GetString(),"item-guid");var resources=Resources(mode);var p=Plan(resources,source,Proof(source,counter.Id,cost:cost),counter);
            var standard=resources.Standard.RemainingPoints;
            for(var n=0;n<count;n++)p.CommitManaged(Request(p,source,O.Domain.StoredSpellUse,O.Domain.Effect,0,cost,id:"item-use:"+n));
            b(counter.Remaining==0&&resources.Standard.RemainingPoints==standard&&resources.Pact.Remaining==4,id+" charge exhaustion independent from creator/user casting");
            b(Reject(()=>p.Prepare(Request(p,source,O.Domain.StoredSpellUse,O.Domain.Effect,0,cost,id:"empty")),O.Code.Exhausted),id+" cannot mint charge via normal-casting fallback");
            a(count==6&&cost==1&&device.GetProperty("rechargeRate").GetString()=="None"&&device.GetProperty("outOfChargesConsequence").GetString()=="Destroy",id+" native charge/lifetime blueprint (not native inventory test)");
        }
        b(items.Length>0,"all generated SSI inventory fixtures exercised");
    }
    private static void Failures(Action<bool,string> b,S.ResourceMode mode)
    {
        var r=Resources(mode);var s=K.FlexibleCasting(false);var sorcery=Counter("sorcery",O.Domain.SorceryPoints);var p=Plan(r,s,Proof(s,outputs:new[]{"sorcery"}),sorcery);
        O.Request Q(string id="operation",O.Domain input=O.Domain.PactSlots,int circle=5,int? native=null,int result=5)=>Request(p,s,input,O.Domain.SorceryPoints,circle,1,result,id,native);
        var before=p.State;b(Reject(()=>p.Prepare(Q(native:1)),O.Code.InvalidCircle)&&p.State==before,"lossy Pact conversion requires explicit verified source policy");
        b(Reject(()=>p.Prepare(Q(input:O.Domain.Arcanum)),O.Code.WrongDomain),"Arcanum never convertible to Sorcery Points");
        b(Reject(()=>p.Prepare(Q(id:" ")),O.Code.MissingIdentity),"native action without stable identity rejected");
        var q=Q();p.Prepare(q);p.Cancel(q.Operation);b(Reject(()=>p.CommitManaged(q),O.Code.StaleOperation)&&p.State==before,"cancel preflight leaves resources unchanged");
        var stale=Q("stale");r.Pact.Spend("unrelated",P.Source.Pact,5);b(Reject(()=>p.Prepare(stale),O.Code.StaleOperation),"external resource mutation invalidates snapshot");
        p.RefreshProofs(K.Revision,new[]{Proof(s,outputs:new[]{sorcery.Id},eligible:false)});b(Reject(()=>p.Prepare(Q("invalid")),O.Code.Ineligible),"fresh owner prerequisite rejected before debit");
        b(Reject(()=>p.RefreshProofs("changed",Array.Empty<O.Proof>()),O.Code.RuleChanged),"rule changes do not migrate a bound owner");
        var noRep=Plan(Resources(mode),s,Proof(s,outputs:new[]{sorcery.Id},repertoire:false),sorcery);b(Reject(()=>noRep.Prepare(Request(noRep,s,O.Domain.PactSlots,O.Domain.SorceryPoints,5,1,5)),O.Code.WrongOwner),"null repertoire cannot guess selected class");
        var item=K.Item("native-item:0",false);var noItem=Plan(Resources(mode),item,Proof(item,"charges",cost:1,item:false),Counter("charges",O.Domain.ItemCharges,6,6,owner:"item-guid"));
        b(Reject(()=>noItem.Prepare(Request(noItem,item,O.Domain.ItemCharges,O.Domain.Effect,0,1)),O.Code.WrongOwner),"item identity required before charges");
        var source=K.FlexibleCasting(true);var empty=Counter("sorcery",O.Domain.SorceryPoints,11,0);var plan=Plan(Resources(mode),source,Proof(source,empty.Id,cost:7),empty);
        b(Reject(()=>plan.Prepare(Request(plan,source,O.Domain.SorceryPoints,mode==S.ResourceMode.Slots?O.Domain.StandardSlots:O.Domain.SpellPoints,5,7,1)),O.Code.Exhausted),"insufficient Sorcery cannot create slot");
        var input=mode==S.ResourceMode.Slots?O.Domain.StandardSlots:O.Domain.SpellPoints;
        foreach(var d in new[]{O.Domain.PactSlots,input})
        {
            var res=Resources(mode);var account=Counter("sorcery",O.Domain.SorceryPoints,20,0);var model=Plan(res,s,Proof(s,outputs:new[]{account.Id},resolvedInput:d),account);
            for(var n=0;n<50;n++){if(d==O.Domain.PactSlots){if(res.Pact.Remaining==0)break;res.Pact.Spend("empty"+n,P.Source.Pact,5);}else {if(!res.Standard.CanSpend(5))break;res.Standard.Spend("empty"+n,5);}}
            var state=model.State;var cost=d==O.Domain.SpellPoints?tables.Cost(5):1;
            b(Reject(()=>model.CommitManaged(Request(model,s,d,O.Domain.SorceryPoints,5,cost,5)),O.Code.Exhausted)&&model.State==state,d+" exhausted: cannot silently use other budget");
        }
        var free=K.FreeMark();var wrong=Counter("mark",O.Domain.FreeSpellUse,6,6,owner:"someone-else");var wrongPlan=Plan(Resources(mode),free,Proof(free,wrong.Id,cost:1),wrong);
        b(Reject(()=>wrongPlan.CommitManaged(Request(wrongPlan,free,O.Domain.FreeSpellUse,O.Domain.Effect,0,1)),O.Code.WrongOwner),"wrong native counter owner rejected before effect");
        var unknown=new O.Request("bad","hero","unknown","unknown",K.Revision,O.Kind.SlotEffect,O.Domain.PactSlots,O.Domain.Effect,5,5,1,1,0,p.State);
        b(Reject(()=>p.Prepare(unknown),O.Code.UnknownSource),"unknown source fails closed");
        var wrongOwner=new O.Request("bad","other",s.Id,s.Definition,s.Revision,s.Operation,O.Domain.PactSlots,O.Domain.SorceryPoints,5,5,1,1,5,p.State);
        b(Reject(()=>p.Prepare(wrongOwner),O.Code.WrongOwner),"request hero identity not selected class");
        var invalidCircle=K.FlexibleCasting(true);var bad=Plan(Resources(mode),invalidCircle,Proof(invalidCircle,"sorcery",cost:9),Counter("sorcery",O.Domain.SorceryPoints));
        b(Reject(()=>bad.Prepare(Request(bad,invalidCircle,O.Domain.SorceryPoints,input,6,9,1)),O.Code.InvalidCircle),"no creation of sixth to ninth circles");
    }
    private static void RecoveryForms(Action<bool,string> b,Action<bool,string> a,S.ResourceMode mode)
    {
        var domain=mode==S.ResourceMode.Slots?O.Domain.StandardSlots:O.Domain.SpellPoints;
        foreach(var power in new[]{("PowerWizardArcaneRecovery","Wizard",5),("PowerCircleLandNaturalRecovery","Druid",5),("PowerSpellMasterBonusRecovery","Wizard",9)})
        {
            var r=Resources(mode);r.Standard.Spend("first",1);r.Standard.Spend("second",2);r.Pact.Spend("pact",P.Source.Pact,5);r.Pact.SelectArcanum(6,"Arc6");r.Pact.Spend("arcanum",P.Source.Arcanum,6,"Arc6");
            var s=K.Recovery(power.Item1,power.Item2,power.Item3);var use=Counter("recovery-use",O.Domain.ClassPower,1,1);var p=Plan(r,s,Proof(s,use.Id,cost:1),use);
            var selected=new Dictionary<int,int>{{1,1},{2,1}};var q=Request(p,s,O.Domain.ClassPower,domain,0,1,mode==S.ResourceMode.Slots?2:5,recovery:selected);var receipt=p.CommitManaged(q);
            b(use.Remaining==0&&r.Pact.Remaining==3&&r.Pact.ArcanumUsed(6)&&r.Standard.RemainingSlots(1)>0,power.Item1+" restores only ordinary budget and costs native power use");
            b(ReferenceEquals(receipt,p.CommitManaged(q)),power.Item1+" repeat recovery never refunds power cost");
            b(Reject(()=>p.CommitManaged(Request(p,s,O.Domain.ClassPower,domain,0,1,0,id:"again",recovery:selected)),O.Code.Exhausted),power.Item1+" one-use limit enforced");
            a(Json("FeatureDefinitionPower",power.Item1).GetProperty("effectDescription").GetProperty("effectForms").EnumerateArray().Any(f=>f.GetProperty("formType").GetString()=="SpellSlots"&&f.GetProperty("spellSlotsForm").GetProperty("maxSlotLevel").GetInt32()==power.Item3),power.Item1+" actual native form maximum");
        }
        foreach(var cunning in new[]{false,true})foreach(var level in new[]{2,11,20})
        {
            if(!cunning&&level<20)continue;
            var r=Resources(mode);for(var n=0;n<4;n++)r.Pact.Spend("p"+n,P.Source.Pact,5);r.Standard.Spend("ordinary",1);var before=r.Standard.RemainingPoints;
            var s=K.PactRecovery(cunning);var use=Counter("pact-recovery",O.Domain.ClassPower,1,1,"ShortRest");var p=Plan(r,s,Proof(s,use.Id,cost:1,level:level),use);var output=cunning&&level<20?2:4;
            p.CommitManaged(Request(p,s,O.Domain.ClassPower,O.Domain.PactSlots,0,1,output));b(r.Pact.Remaining==output&&r.Standard.RemainingPoints==before,"existing Cunning/Master allocation, no ordinary refund");
            var target=r.Pact.Remaining;p.Rest("short",false);b(r.Pact.Remaining==4&&use.Remaining==1,"short rest restores Pact and only native ShortRest powers");p.Rest("short",false);b(r.Pact.Remaining==4,"repeat short rest unchanged");
        }
        // Enumerate every actually serialized SpellSlots power form. Native effect-family data goes
        // through the production factory; activation rolls/hits/costs remain explicit fresh proofs.
        var seen=new HashSet<string>();
        foreach(var root in new[]{"OfficialBlueprints","UnfinishedBusinessBlueprints"})foreach(var file in Directory.GetFiles("Diagnostics/"+root+"/FeatureDefinitionPower","*.json"))
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(file));var power=doc.RootElement;var id=power.GetProperty("name").GetString();
            foreach(var form in power.GetProperty("effectDescription").GetProperty("effectForms").EnumerateArray().Where(f=>f.GetProperty("formType").GetString()=="SpellSlots"))
            {
                seen.Add(id);var f=form.GetProperty("spellSlotsForm");var effect=f.GetProperty("type").ToString();O.Source s;
                switch(effect)
                {
                    case "CreateSpellSlot":case "CreateSorceryPoints":s=K.FlexibleCasting(effect=="CreateSpellSlot");b(s.Definition==id,"installed flexible form mapped to exact owner definition");continue;
                    case "RecoverHalfLevelUp":s=id=="PowerWarlockEldritchMaster"?K.PactRecovery(false):K.Recovery(id,id=="PowerCircleLandNaturalRecovery"?"Druid":"Wizard",f.GetProperty("maxSlotLevel").GetInt32());continue;
                    case "RechargePower":s=K.HauntedRecharge();break;
                    default:s=K.SorceryRecovery(id,effect,f.GetProperty("sorceryPointsGain").GetInt32());break;
                }
                var target=s.Outputs.Single();var counter=Counter("target",target,20,0);var input=s.Inputs.Single();var cost=input==O.Domain.SorceryPoints?power.GetProperty("costPerUse").GetInt32():1;var uses=Counter("source",input,cost,cost);var p=Plan(Resources(mode),s,Proof(s,uses.Id,new[]{counter.Id},cost,level:11),uses,counter);
                var amount=s.Scaling==O.Scale.AllMissing?20:s.Scaling==O.Scale.HalfDown?5:s.Scaling==O.Scale.HalfUp?6:s.Amount;
                p.CommitManaged(Request(p,s,input,target,0,cost,amount));b(counter.Remaining==amount&&uses.Remaining==0,id+" typed source-native recovery amount and independent power use");
                p.Rest("long",true);b(counter.Remaining==20,"native Sorcery/class counters retain their declared recharge, not shared pool mode");
            }
        }
        a(seen.Count==15,"complete installed15 SpellSlots-form definition inventory (actual JSON, not guessed powers)");
        foreach(var id in new[]{"mind-sculpt","supreme-will","magical-guidance","seeking","metamagic","transmuted-refund","wild-magic-reset","mana-overflow","sorcery-effect-form"})
        {
            var debit=id is "mind-sculpt" or "supreme-will" or "magical-guidance" or "seeking" or "metamagic";var scale=id=="wild-magic-reset"?O.Scale.AllMissing:O.Scale.Fixed;
            var s=K.Sorcery(id,"NativeDefinition:"+id,scale,1,debit);var c=Counter("sorcery",O.Domain.SorceryPoints,20,10);var activation=Counter("activation",O.Domain.ClassPower,1,1,"ShortRest");var cost=debit?(id is "seeking" or "supreme-will" or "metamagic"?2:1):0;var extras=id=="supreme-will"?new[]{new O.NativeDebit(activation.Id,O.Domain.ClassPower,1)}:Array.Empty<O.NativeDebit>();var p=Plan(Resources(mode),s,Proof(s,debit?c.Id:null,debit?null:new[]{c.Id},cost,debits:extras),c,activation);
            p.CommitManaged(Request(p,s,debit?O.Domain.SorceryPoints:O.Domain.Effect,debit?O.Domain.Effect:O.Domain.SorceryPoints,id=="supreme-will"?1:0,cost,debit?0:id=="wild-magic-reset"?10:1));
            b(c.Remaining==(debit?10-cost:id=="wild-magic-reset"?20:11),id+" explicitly uses Sorcery Points, not alternate Spell Points");
        }
    }
    private static void Advanced(Action<bool,string> b,Action<bool,string> a,S.ResourceMode mode)
    {
        var paid=mode==S.ResourceMode.Slots?O.Domain.StandardSlots:O.Domain.SpellPoints;
        var trade=K.SlotTrade("wild-shape-refund","PowerDruidWildResurgenceShape");var r=Resources(mode);
        var target=Counter("wild-shape",O.Domain.ClassPower,2,0,"ShortRest");var activation=Counter("activation",O.Domain.ClassPower,1,1,"TurnStart");
        var proof=Proof(trade,outputs:new[]{target.Id},debits:new[]{new O.NativeDebit(activation.Id,O.Domain.ClassPower,1)});
        var p=Plan(r,trade,proof,target,activation);var q=Request(p,trade,paid,O.Domain.ClassPower,1,mode==S.ResourceMode.SpellPoints?2:1,1);
        p.CommitManaged(q);var before=p.State;
        b(target.Remaining==1&&activation.Remaining==0,"composite slot+TurnStart power cost is an explicit native debit, not a free resource refund");
        b(Reject(()=>p.Prepare(Request(p,trade,paid,O.Domain.ClassPower,1,mode==S.ResourceMode.SpellPoints?2:1,1,id:"again")),O.Code.Exhausted)&&p.State==before,"exhausted initiating power rejects before any slot debit");
        b(Reject(()=>new O("hero",K.Revision,r,new[]{trade},new[]{proof},new[]{target,activation},new[]{"Druid"},tables,creation),O.Code.ConflictingOperation),"one conversion facade per coordinator, no receipt reset by reinitialization");
        var s=K.FlexibleCasting(false);var c=Counter("sorcery",O.Domain.SorceryPoints,11,0);var model=Plan(Resources(mode),s,Proof(s,outputs:new[]{c.Id}),c);
        var request=Request(model,s,O.Domain.PactSlots,O.Domain.SorceryPoints,5,1,5);var receipt=model.CommitManaged(request);
        model.RefreshOwner(K.Revision,new[]{"Warlock","Sorcerer"},new[]{Counter(c.Id,O.Domain.SorceryPoints,12,6)},new[]{Proof(s,outputs:new[]{c.Id})});
        b(c.Maximum==12&&c.Remaining==6&&ReferenceEquals(receipt,model.CommitManaged(request)),"owner advancement adds capacity but retains expenditure and old receipts");
        var state=model.State;
        b(Reject(()=>model.RefreshOwner(K.Revision,new[]{"Warlock","Sorcerer"},new[]{Counter(c.Id,O.Domain.SorceryPoints,12,12)},new[]{Proof(s,outputs:new[]{c.Id})}),O.Code.StaleOperation)&&state==model.State,"fresh native owner cannot refill confirmed debt");
        b(Reject(()=>model.RefreshOwner(K.Revision,new[]{"Sorcerer"},Array.Empty<O.Counter>(),Array.Empty<O.Proof>()),O.Code.PendingAtomicJournal),"class removal is not an implicit respec/migration");
        var recovery=K.Recovery("PowerWizardArcaneRecovery","Wizard",5);var resources=Resources(mode);resources.Standard.Spend("debt",3);resources.Standard.GrantTemporarySlots("bonus",1,3);
        var power=Counter("power",O.Domain.ClassPower,1,1);var plan=Plan(resources,recovery,Proof(recovery,power.Id,cost:1),power);
        var value=mode==S.ResourceMode.SpellPoints?2:1;
        plan.CommitManaged(Request(plan,recovery,O.Domain.ClassPower,paid,0,1,value,recovery:new Dictionary<int,int>{{mode==S.ResourceMode.SpellPoints?1:3,1}}));
        b(power.Remaining==0,"recovery previews authoritative debt even when bonus SP exceeds the visible deficit");
        var noActivation=Plan(Resources(mode),trade,Proof(trade,outputs:new[]{"shape"},activation:false),Counter("shape",O.Domain.ClassPower,2,0));
        b(Reject(()=>noActivation.Prepare(Request(noActivation,trade,paid,O.Domain.ClassPower,1,mode==S.ResourceMode.SpellPoints?2:1,1)),O.Code.Ineligible),"unknown initiating power cost cannot use a slot-only plan");
        var alchemy=K.SlotTrade("alchemy-refund","PowerInnovationAlchemyRefundFromSlot2");var al=Plan(Resources(mode),alchemy,Proof(alchemy,outputs:new[]{"al"}),Counter("al",O.Domain.ClassPower,6,5));
        b(Reject(()=>al.Prepare(Request(al,alchemy,paid,O.Domain.ClassPower,2,mode==S.ResourceMode.SpellPoints?3:1,2)),O.Code.Ineligible),"Alchemy CanUsePower requires enough debt despite unclamped callback addition");
        b(Reject(()=>al.Prepare(Request(al,alchemy,paid,O.Domain.ClassPower,1,mode==S.ResourceMode.SpellPoints?2:1,1)),O.Code.InvalidCircle),"fixed native Alchemy power cannot select a cheaper slot circle");
        foreach(var shape in new[]{("Single","AtWill"),("Charges","ChargeCost"),("Charges","AtWill"),("ByFunction","IterationPerRecharge"),("ByFunction","RechargeDuration"),("ByFunction","AtWill")})
        {
            var item=K.DeviceFunction("native-function:"+shape.Item1+":"+shape.Item2,shape.Item1,shape.Item2);var input=item.Inputs.Single();
            var cost=input==O.Domain.Effect||shape.Item2=="AtWill"&&shape.Item1=="Charges"?0:1;
            var counter=input==O.Domain.Effect?null:Counter("function",input,2,2,"None","item-guid");var m=Plan(Resources(mode),item,Proof(item,counter?.Id,cost:cost),counter==null?Array.Empty<O.Counter>():new[]{counter});
            m.CommitManaged(Request(m,item,input,O.Domain.Effect,0,cost));
            b(counter==null||counter.Remaining==2-cost,shape+" device-level native usage controls typed domain, no borrowed class cost");
        }
        foreach(var d in new[]{O.Domain.ItemCharges,O.Domain.ItemFunctionUses})foreach(var recharge in new[]{"Dawn","RechargeDuration"})
        {
            var item=K.DeviceRecovery("native-item-function",d,recharge,2);var counter=Counter("item",d,6,3,recharge,"item-guid");var m=Plan(Resources(mode),item,Proof(item,outputs:new[]{counter.Id},nativeEvent:"verified-native-time-or-roll",nativeResult:2),counter);
            var eventRequest=Request(m,item,O.Domain.Effect,d,0,0,2,id:"verified-native-time-or-roll");m.CommitManaged(eventRequest);m.CommitManaged(eventRequest);
            b(counter.Remaining==5,"verified "+recharge+" native result applied once, no reroll or long-rest substitution");
            m.Rest("long",true);b(counter.Remaining==5,"long rest is not Dawn or duration expiry");
        }
        b(Reject(()=>K.DeviceRecovery("SSI",O.Domain.StoredSpellUse,"None",6),O.Code.Ineligible),"SSI None recharge cannot manufacture charges");
        var turn=K.NativePowerRecharge("PowerDruidWildResurgenceShape","Druid",O.Domain.ClassPower,"TurnStart");var turnCounter=Counter("turn",O.Domain.ClassPower,1,0,"TurnStart");var tp=Plan(Resources(mode),turn,Proof(turn,outputs:new[]{turnCounter.Id},nativeEvent:"verified-next-turn"),turnCounter);
        var turnRequest=Request(tp,turn,O.Domain.Effect,O.Domain.ClassPower,0,0,1,id:"verified-next-turn");tp.CommitManaged(turnRequest);tp.CommitManaged(turnRequest);
        b(turnCounter.Remaining==1,"explicit native TurnStart receipt restores only the proper power counter once");
        var itemRecovery=K.DeviceRecovery("long-rest-charges",O.Domain.ItemCharges,"LongRest",1);var ic=Counter("charges",O.Domain.ItemCharges,6,2,"LongRest","item-guid");var ip=Plan(Resources(mode),itemRecovery,Proof(itemRecovery,outputs:new[]{ic.Id},nativeResult:1),ic);
        ip.Rest("long",true);b(ic.Remaining==2,"item LongRest recharge is a native roll, not automatic full class-power recovery");ip.CommitManaged(Request(ip,itemRecovery,O.Domain.Effect,O.Domain.ItemCharges,0,0,1));b(ic.Remaining==3,"native charge roll applied independently after rest");
        var conflictSource=K.FreeMark();var nativeCounter=Counter("mark",O.Domain.FreeSpellUse,6,6);var conflictResources=Resources(mode);var cm=Plan(conflictResources,conflictSource,Proof(conflictSource,nativeCounter.Id,cost:1),nativeCounter);
        var normal=new P.Cast("same-native-operation","Sorcerer","CastSpellSorcerer","Shield",1,1,"Charisma",18,true,P.Source.Standard);conflictResources.Spend(normal);
        b(Reject(()=>cm.Prepare(Request(cm,conflictSource,O.Domain.FreeSpellUse,O.Domain.Effect,0,1,id:normal.Operation)),O.Code.ConflictingOperation)&&nativeCounter.Remaining==6,"ordinary cast identity cannot be reused as a free/paid consumer");
        var consumer=Request(cm,conflictSource,O.Domain.FreeSpellUse,O.Domain.Effect,0,1,id:"native-consumer");cm.Prepare(consumer);
        b(conflictResources.Spend(new P.Cast(consumer.Operation,"Sorcerer","CastSpellSorcerer","Shield",1,1,"Charisma",18,true,P.Source.Standard)).Error==P.Failure.ConflictingAction,"prepared consumer cannot become a second ordinary casting debit");
        var recoverySource=K.DeviceRecovery("Dawn-fixture",O.Domain.ItemCharges,"Dawn",3);var charges=Counter("charges",O.Domain.ItemCharges,10,0,"Dawn","item-guid");var rechargePlan=Plan(Resources(mode),recoverySource,Proof(recoverySource,outputs:new[]{charges.Id},nativeEvent:"dawn1",nativeResult:2),charges);
        var dawn1=Request(rechargePlan,recoverySource,O.Domain.Effect,O.Domain.ItemCharges,0,0,2,id:"dawn1");rechargePlan.CommitManaged(dawn1);
        b(Reject(()=>rechargePlan.Prepare(Request(rechargePlan,recoverySource,O.Domain.Effect,O.Domain.ItemCharges,0,0,2,id:"fake-retry-id")),O.Code.MissingIdentity),"native recharge outcome cannot be reused with an unrelated retry identity");
        rechargePlan.RefreshProofs(K.Revision,new[]{Proof(recoverySource,outputs:new[]{charges.Id},nativeEvent:"dawn2",nativeResult:1)});
        rechargePlan.CommitManaged(Request(rechargePlan,recoverySource,O.Domain.Effect,O.Domain.ItemCharges,0,0,1,id:"dawn2"));
        b(charges.Remaining==3&&rechargePlan.CommitManaged(dawn1).OutputAmount==2,"different native recharge rolls reuse one facade/profile and retain earlier receipts");
        rechargePlan.RefreshProofs(K.Revision,new[]{Proof(recoverySource,outputs:new[]{charges.Id},nativeEvent:"dawn3",nativeResult:4)});
        b(Reject(()=>rechargePlan.Prepare(Request(rechargePlan,recoverySource,O.Domain.Effect,O.Domain.ItemCharges,0,0,4,id:"dawn3")),O.Code.InvalidResult)&&charges.Remaining==3,"native outcome outside captured recharge bounds rejected before resource mutation");
        var createSource=K.FlexibleCasting(true);var sorceryCounter=Counter("sorcery",O.Domain.SorceryPoints,11,10);var createPlan=Plan(Resources(mode),createSource,Proof(createSource,sorceryCounter.Id,cost:7),sorceryCounter);
        b(Reject(()=>createPlan.CommitManaged(Request(createPlan,createSource,O.Domain.SorceryPoints,paid,1,2,mode==S.ResourceMode.SpellPoints?2:1)),O.Code.InvalidCost)&&sorceryCounter.Remaining==10,"native creation proof must agree with exact installed creation cost");
        foreach(var id in new[]{"metamagic","seeking","transmuted-refund"})
        {
            var debit=id!="transmuted-refund";var source=K.Sorcery(id,"MetamagicNativeOption",O.Scale.Fixed,1,debit);var native=Counter("feat-sorcery",O.Domain.SorceryPoints,3,2);var featResources=Resources(mode,("Wizard",12),("Fighter",8));
            var fresh=Proof(source,debit?native.Id:null,debit?null:new[]{native.Id},cost:debit?2:0);var m=new O("hero",K.Revision,featResources,new[]{source},new[]{fresh},new[]{native},new[]{"Wizard","Fighter"},tables,creation);
            m.CommitManaged(Request(m,source,debit?O.Domain.SorceryPoints:O.Domain.Effect,debit?O.Domain.Effect:O.Domain.SorceryPoints,0,debit?2:0,debit?0:1));
            b(native.Remaining==(debit?0:3)&&featResources.Standard.Current.CasterLevel==20&&featResources.Pact.Current.Slots==0,"Metamagic Adept/"+id+" uses actual non-Sorcerer native attribute, no class/repertoire/Pact fabrication");
        }
        var embodiment=K.EmbodimentOfLaw();var lawUses=Counter("law-uses",O.Domain.ClassPower,4,4);var lawResources=Resources(mode,("Cleric",12));var law=new O("hero",K.Revision,lawResources,new[]{embodiment},new[]{Proof(embodiment,lawUses.Id,cost:1)},new[]{lawUses},new[]{"Cleric"},tables,creation);
        var standardBefore=lawResources.Standard.RemainingPoints;law.CommitManaged(Request(law,embodiment,O.Domain.ClassPower,O.Domain.Effect,1,1));
        b(lawUses.Remaining==3&&lawResources.Standard.RemainingPoints==standardBefore,"Domain Order free metamagic consumes native WIS-based power use, not fabricated Sorcery or Spell Points");
        a(Json("FeatureDefinitionAttributeModifier","AttributeModifierSorcererSorceryPointsBonus2").GetProperty("modifierOperation").GetString()=="AddHalfProficiencyBonus","actual Metamagic Adept bonus is native half-PB, not inferred fixed2/effective level20");
        a(Json("FeatureDefinitionAttributeModifier","AttributeModifierDomainOrderSorceryPointsMinimum").GetProperty("modifierOperation").GetString()=="ForceIfBetter","Domain Order UI minimum is native ForceIfBetter; does not imply Sorcerer class ownership");
        foreach(var fromWarlock in new[]{false,true})foreach(var shift in new[]{false,true})foreach(var preference in new[]{false,true})
        {
            var res=Resources(mode);var choice=K.ResolveFlexibleInput(res,5,preference,shift,fromWarlock);var expected=preference||fromWarlock&&!shift||!fromWarlock&&shift;
            b(choice.Domain==(expected?O.Domain.PactSlots:paid)&&choice.Circle==5&&!choice.Lossy,"Flexible Casting actual native source selection/Shift/preference "+fromWarlock+"/"+shift+"/"+preference);
        }
        var lossy=K.ResolveFlexibleInput(Resources(mode),1,false,true,false);b(lossy.Domain==O.Domain.PactSlots&&lossy.Circle==5&&lossy.NativeCircle==1&&lossy.Lossy,"native policy explicitly authorizes lossy Pact5 to Sorcery1 only when selected");
        var shapes=new HashSet<string>();var powerCount=0;
        foreach(var root in new[]{"OfficialBlueprints","UnfinishedBusinessBlueprints"})
        {
            foreach(var file in Directory.GetFiles("Diagnostics/"+root+"/ItemDefinition","*.json"))
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(file));var e=doc.RootElement;if(!e.GetProperty("isUsableDevice").GetBoolean())continue;
                var dev=e.GetProperty("usableDeviceDescription");var index=0;
                foreach(var fn in dev.GetProperty("deviceFunctions").EnumerateArray())
                {var usage=dev.GetProperty("usage").GetString();var affinity=fn.GetProperty("useAffinity").GetString();shapes.Add(usage+"/"+affinity);_=K.DeviceFunction(e.GetProperty("name").GetString()+":"+index++,usage,affinity);}
            }
            foreach(var file in Directory.GetFiles("Diagnostics/"+root+"/FeatureDefinitionPower","*.json"))
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(file));var e=doc.RootElement;if(e.GetProperty("rechargeRate").GetString()!="SorceryPoints")continue;
                powerCount++;var id=e.GetProperty("name").GetString();var nativeCost=e.GetProperty("costPerUse").GetInt32();var source=K.Power(id,"Sorcerer",O.Domain.SorceryPoints);var native=Counter("sorcery",O.Domain.SorceryPoints,20,20);var m=Plan(Resources(mode),source,Proof(source,native.Id,cost:nativeCost),native);
                m.CommitManaged(Request(m,source,O.Domain.SorceryPoints,O.Domain.Effect,0,nativeCost));b(native.Remaining==20-nativeCost,id+" actual native power Sorcery cost and owner");
            }
        }
        a(shapes.Count==6,"all registered native device usage/affinity shapes classified, not Function.ParentUsage guesses");
        a(powerCount==11,"all11 native SorceryPoints-recharge power definitions exercised");
    }
    private static void Bindings(Action<bool,string> a,AssemblyDefinition game,TypeDefinition[] types)
    {
        var ctx=types.Single(t=>t.Name=="SpellResourceOperationsContext");var count=0;
        foreach(var patch in ctx.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(c=>c.AttributeType.Name=="HarmonyPatch");if(attr==null)continue;
            var type=(TypeReference)attr.ConstructorArguments[0].Value;var name=(string)attr.ConstructorArguments[1].Value;
            var methods=game.MainModule.Types.Single(t=>t.FullName==type.FullName).Methods.Where(m=>m.Name==name).ToArray();
            a(methods.Length==1,"unique native signature "+type.Name+"."+name);
            var prefix=patch.Methods.Single(m=>m.Name=="Prefix");
            foreach(var p in prefix.Parameters.Where(p=>p.Name.Length==3&&p.Name.StartsWith("__")&&char.IsDigit(p.Name[2])))
            {var index=p.Name[2]-'0';a(methods[0].Parameters[index].ParameterType.FullName==p.ParameterType.FullName,"native positional binding "+patch.Name+"/"+p.Name);}
            a(prefix.CustomAttributes.Any(c=>c.AttributeType.Name=="HarmonyPriority"),"guard priority is explicit "+patch.Name);count++;
        }
        var actionPrefix=ctx.NestedTypes.Single(t=>t.Name=="ActionPatch").Methods.Single(m=>m.Name=="Prefix").Body.Instructions;
        var calls=actionPrefix.Where(i=>i.Operand is MethodReference).Select(i=>((MethodReference)i.Operand).Name).ToArray();
        a(Array.IndexOf(calls,"get_RuntimeEnabled")<Array.IndexOf(calls,"get_ActionParams"),"gate OFF exits before native action/source lookup");
        a(count==9,"all nine narrow native guard bindings audited, no Harmony execution claimed");
        var native=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter");
        var create=native.Methods.Single(m=>m.Name=="CreateSpellSlot").Body.Instructions;
        var spendIndex=create.Select((i,n)=>(i,n)).First(x=>x.i.Operand is MethodReference m&&m.Name=="SpendSorceryPoints").n;
        var conditionIndex=create.Select((i,n)=>(i,n)).First(x=>x.i.Operand is MethodReference m&&m.Name=="InflictCondition").n;
        a(spendIndex<conditionIndex,"native creation spends Sorcery before condition publication; no atomic rollback assumed");
        var gain=native.Methods.Single(m=>m.Name=="CreateSorceryPoints").Body.Instructions;
        a(gain.Any(i=>i.Operand is MethodReference m&&m.Name=="SpendSpellSlot"),"native conversion really calls repertoire slot debit");
        var standard=types.Single(t=>t.Name=="StandardMulticasterResourcesContext");
        foreach(var name in new[]{"Recover","AddPoints"})
        {var il=standard.Methods.Single(m=>m.Name==name).Body.Instructions;a(!il.Any(i=>i.Operand is MethodReference m&&m.Name=="NewGuid")&&il.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="Rejection"),name+" rejects missing journal instead of minting random retry IDs");}
        a(ctx.Methods.Single(m=>m.Name=="DescribeRecovery").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="SpellResourceConsumerCatalog"),"native recovery descriptor uses tested production catalog");
        a(!CompressedMulticasterProgressionPlan.MixedGameplayReady,"mixed gameplay remains OFF; no public switch introduced");
    }
}
