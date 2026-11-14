using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using F=SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using O=SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;
using S=SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using P=SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using C=SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using R=SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;

internal static class FlexibleCastingPreflightTests
{
    private static S.Tables tables;private static P.Tables pact;private static int[][] pactRows;
    private static readonly int[] Full={1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
    private static readonly int[] Costs={2,3,5,6,7};
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    internal sealed class Fixture
    {
        internal readonly P.CastingLedger Resources;internal readonly O.Counter Sorcery;internal readonly F Model;
        internal readonly F.Authority Authority;internal string Rules="rules1",Casting="CastSpellSorcerer",Owner="17",Repertoire="17/CastSpellSorcerer";
        internal int Actual=8,Effective=11,NativeSorcery,NativePowerUses=1,NativeRefunds=0;internal bool Eligible=true,Preference;
        internal string[] Conditions={"original-condition"};internal int[] NativeCosts=Costs.ToArray();
        internal Fixture(S.ResourceMode mode,string other,bool create=false,int? sorcery=null)
        {
            var ids=new[]{new C.ClassInput("Sorcerer",8,true),new C.ClassInput(other,12,true)};
            var calculator=new C("A-native-fixture",ids.Select(c=>new C.ClassProfile(c.ClassId,true,new C.CastingProfile("CastSpell"+c.ClassId,c.ClassId=="Warlock"?C.CasterKind.Pact:C.CasterKind.Full,1,true,c.ClassId=="Warlock"?pactRows.Select(r=>Array.FindIndex(r,n=>n>0)+1):Full))));
            var result=calculator.Calculate(new C.Snapshot(20,ids,ids.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel))));
            Resources=new P.CastingLedger(new S.Budget(tables,S.Allocation.StandardPortion(tables,result,mode)),new P.Budget(new P.Allocation(pact,result)));
            NativeSorcery=sorcery??(create?11:6);Sorcery=new O.Counter("17/SorceryPoints","17",O.Domain.SorceryPoints,11,NativeSorcery,"LongRest");
            var op=new O("17",SpellResourceConsumerCatalog.Revision,Resources,new[]{SpellResourceConsumerCatalog.FlexibleCasting(false),SpellResourceConsumerCatalog.FlexibleCasting(true)},Array.Empty<O.Proof>(),new[]{Sorcery},ids.Select(c=>c.ClassId),tables,Costs.Select((n,i)=>new KeyValuePair<int,int>(i+1,n)));
            Model=new F("17","rules1",op,Sorcery,tables);Authority=new F.Authority("17","native-effect42",Repertoire,create,true);
        }
        internal F.Live Live(int? nativeUsedOverride=null)
        {
            var caps=new List<int>();var used=new List<int>();
            for(var c=1;c<=9;c++)
            {var cap=Resources.Standard.Current.Slots[c-1]+Resources.Standard.TemporarySlots(c);var spent=cap-Resources.Standard.RemainingSlots(c);if(c==Resources.Pact.Current.Circle){cap+=Resources.Pact.Current.Slots;spent+=Resources.Pact.Used;}caps.Add(cap);used.Add(spent);}
            if(nativeUsedOverride!=null)used[0]=nativeUsedOverride.Value;
            return new F.Live(Owner,Repertoire,Casting,Actual,Effective,true,Eligible,Rules,11,NativeSorcery,caps,used,Resources.Pact.Used,
                Resources.Standard.Current.Mode==S.ResourceMode.SpellPoints?1:0,Resources.Standard.Current.PointMaximum,Resources.Standard.RemainingPoints,NativeCosts,Conditions.Concat(new[]{"powerUses:"+NativePowerUses}),Preference);
        }
        internal string Balances()=>Resources.Standard.RemainingPoints+"/"+string.Join(",",Enumerable.Range(1,9).Select(c=>Resources.Standard.RemainingSlots(c)+":"+Resources.Standard.TemporarySlots(c)+":"+Resources.Standard.HighCircleSpent(c)))+
            "/"+Resources.Pact.Remaining+"/"+Sorcery.Remaining+"/"+NativeSorcery+"/"+NativePowerUses+"/"+NativeRefunds+"/"+string.Join(",",Conditions)+"/"+string.Join(",",Enumerable.Range(6,4).Select(Resources.Pact.ArcanumUsed));
    }
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        int behavior=0,metadata=0;
        void B(bool ok,string name){behavior++;check(ok,"13.7A production behavior: "+name);}
        void A(bool ok,string name){metadata++;check(ok,"13.7A compiled/definition audit: "+name);}
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();int[] V(string t,string f)=>CompressedMulticasterTests.AddedIntegers(types.Single(x=>x.Name==t),f).Where(n=>n>=0).ToArray();
        var slots=V("FeatureDefinitionCastSpellBuilder","SlotsByCasterLevel");tables=new S.Tables(Enumerable.Range(1,20).Select(n=>slots.Skip(n*10).Take(9)),V("ModifyPowerPoolAmountPowerSpellPoints","SpellPointsByLevel"),V("SpellPointsContext","SpellCostByLevel"));
        var display=V("SharedSpellsContext","WarlockCastingSlots");pactRows=Enumerable.Range(0,20).Select(n=>{var row=display.Skip(n*6).Take(6).ToArray();var r=new int[9];r[Array.IndexOf(row,0)-1]=row[0];return r;}).ToArray();pact=new P.Tables(pactRows,Array.Empty<P.Arcanum>());
        void Rejected(Fixture f,Action action,O.Code expected,string name)
        {
            var before=f.Balances();var rejected=false;
            try{action();}catch(O.Rejection ex){rejected=ex.Diagnostic==expected;}
            B(rejected,name+" correct structured rejection");B(before==f.Balances(),name+" leaves Standard/Pact/Sorcery/native counters/temporary conditions unchanged");
        }
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})foreach(var other in new[]{"Wizard","Warlock"})foreach(var create in new[]{false,true})
        {
            for(var circle=1;circle<=5;circle++)
            {
                var f=new Fixture(mode,other,create);var choice=new F.Choice(circle,create,false,false);var before=f.Balances();
                var ticket=f.Model.Prepare(f.Authority,choice,f.Live());
                B(before==f.Balances(),$"{mode}/Sorcerer+{other}/{create}/{circle}: pre-dispatch Prepare has zero resource/condition mutations");
                B(ticket.Request.Hero=="17"&&ticket.Authority.Effect=="native-effect42"&&ticket.Request.NativeCircle==circle&&ticket.Request.Circle==circle,"original hero/effect/native repertoire/circles bound");
                B(ticket.Request.Input==(create?O.Domain.SorceryPoints:mode==S.ResourceMode.SpellPoints?O.Domain.SpellPoints:O.Domain.StandardSlots),"native source and target domains remain distinct");
                B(!create||ticket.FundingIdentity.Contains(ticket.Request.Operation)&&ticket.FundingIdentity.Contains("AdditionalSpellSlot"+circle),"temporary-slot funding identity is derived from the same command");
                B(ReferenceEquals(ticket,f.Model.Prepare(f.Authority,choice,f.Live())),"retry reuses exact ticket/command identity, no new GUID");
                f.Model.Revalidate(ticket,f.Live());B(before==f.Balances(),"immediate pre-commit revalidation remains read-only");
                Rejected(f,()=>f.Model.BlockNativeCommit(ticket,f.Live()),O.Code.PendingAtomicJournal,"native commit still blocked");
                Rejected(f,()=>f.Model.Prepare(f.Authority,new F.Choice(circle,create,true,false),f.Live()),O.Code.ConflictingOperation,"retry cannot substitute later Shift state");
                f.Model.Cancel(f.Authority);Rejected(f,()=>f.Model.Prepare(f.Authority,choice,f.Live()),O.Code.StaleOperation,"Cancel is terminal for this command");
            }
        }
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})
        {
            foreach(var shift in new[]{false,true})foreach(var preference in new[]{false,true})
            {
                var f=new Fixture(mode,"Warlock"){Preference=preference};var q=new F.Choice(5,false,shift,preference);var t=f.Model.Prepare(f.Authority,q,f.Live());
                B(t.Request.Input==((shift||preference)?O.Domain.PactSlots:mode==S.ResourceMode.SpellPoints?O.Domain.SpellPoints:O.Domain.StandardSlots),"fresh native Shift/priority source policy");
            }
            var lossy=new Fixture(mode,"Warlock");var lt=lossy.Model.Prepare(lossy.Authority,new F.Choice(1,false,true,false),lossy.Live());
            B(lt.Request.Input==O.Domain.PactSlots&&lt.Request.Circle==5&&lt.Request.NativeCircle==1&&lt.Preview.OutputAmount==1,"authorized native Pact5 to Sorcery1 retained without ordinary spell fabrication");
            foreach(var exhausted in new[]{"standard","pact","both"})
            {
                var f=new Fixture(mode,"Warlock");
                if(exhausted is "standard" or "both")for(var n=0;f.Resources.Standard.CanSpend(1);n++)f.Resources.Standard.Spend("empty"+n,1);
                if(exhausted is "pact" or "both")for(var n=0;f.Resources.Pact.Remaining>0;n++)f.Resources.Pact.Spend("empty"+n,P.Source.Pact,5);
                if(exhausted=="both")Rejected(f,()=>f.Model.Prepare(f.Authority,new F.Choice(1,false,false,false),f.Live()),O.Code.Exhausted,"both sources exhausted");
                else {var t=f.Model.Prepare(f.Authority,new F.Choice(1,false,false,false),f.Live());B(t.Request.Input==(exhausted=="standard"?O.Domain.PactSlots:mode==S.ResourceMode.SpellPoints?O.Domain.SpellPoints:O.Domain.StandardSlots),"only installed legal exhaustion fallback");}
            }
            var insufficient=new Fixture(mode,"Wizard",true,1);Rejected(insufficient,()=>insufficient.Model.Prepare(insufficient.Authority,new F.Choice(5,true,false,false),insufficient.Live()),O.Code.Exhausted,"insufficient Sorcery Points");
            var wrongRep=new Fixture(mode,"Wizard"){Casting="CastSpellWizard"};Rejected(wrongRep,()=>wrongRep.Model.Prepare(wrongRep.Authority,new F.Choice(1,false,false,false),wrongRep.Live()),O.Code.WrongOwner,"wrong repertoire");
            var wrongOwner=new Fixture(mode,"Wizard"){Owner="anotherHero"};Rejected(wrongOwner,()=>wrongOwner.Model.Prepare(wrongOwner.Authority,new F.Choice(1,false,false,false),wrongOwner.Live()),O.Code.WrongOwner,"wrong original hero");
            var wrongCost=new Fixture(mode,"Wizard",true);Rejected(wrongCost,()=>wrongCost.Model.Prepare(wrongCost.Authority,new F.Choice(1,true,false,false,requestedCost:1),wrongCost.Live()),O.Code.InvalidCost,"wrong native cost");
            var wrongCircle=new Fixture(mode,"Wizard",true);Rejected(wrongCircle,()=>wrongCircle.Model.Prepare(wrongCircle.Authority,new F.Choice(6,true,false,false),wrongCircle.Live()),O.Code.InvalidCircle,"native UI level beyond1..5");
            var arcanum=new Fixture(mode,"Warlock");Rejected(arcanum,()=>arcanum.Model.Prepare(arcanum.Authority,new F.Choice(1,false,false,false,O.Domain.Arcanum),arcanum.Live()),O.Code.WrongDomain,"Arcanum is never a source");
            var missing=new Fixture(mode,"Wizard");Rejected(missing,()=>missing.Model.Prepare(new F.Authority("17","0",missing.Repertoire,false,false),new F.Choice(1,false,false,false),missing.Live()),O.Code.MissingIdentity,"unverified native effect provenance");
            var eligibility=new Fixture(mode,"Wizard"){Eligible=false};Rejected(eligibility,()=>eligibility.Model.Prepare(eligibility.Authority,new F.Choice(1,false,false,false),eligibility.Live()),O.Code.Ineligible,"native power authority missing");
            var projection=new Fixture(mode,"Warlock");Rejected(projection,()=>projection.Model.Prepare(projection.Authority,new F.Choice(1,false,false,false),projection.Live(99)),O.Code.StaleOperation,"stale native repertoire view");
            foreach(var stale in new[]{"rules","resource","conditions","sorcery","cost","power"})
            {
                var f=new Fixture(mode,"Wizard");var q=new F.Choice(1,false,false,false);var t=f.Model.Prepare(f.Authority,q,f.Live());
                switch(stale){case "rules":f.Rules="rules2";break;case "resource":f.Resources.Standard.Spend("unrelated",1);break;case "conditions":f.Conditions=new[]{"new-condition"};break;case "sorcery":f.NativeSorcery--;break;case "cost":f.NativeCosts[0]++;break;case "power":f.NativePowerUses--;break;}
                Rejected(f,()=>f.Model.Revalidate(t,f.Live()),stale=="rules"?O.Code.RuleChanged:O.Code.StaleOperation,"stale "+stale+" before future commit boundary");
            }
        }
        Finalization(B);Bindings(A,game,types);
        FlexibleCastingInvocationTests.Run(check,game,types);
        FlexibleCastingSlotToSorceryTests.Run(check,game,types);
        Console.WriteLine($"Stage13.7A: {behavior} executed preflight/model checks; {metadata} compiled/definition audits; no Unity execution.");
    }
    private static void Finalization(Action<bool,string> b)
    {
        var spells=new[]{new R.Spell("s1",1),new R.Spell("s2",1),new R.Spell("c1",0),new R.Spell("c2",0)};
        var profile=new R.Profile("Sorcerer","CastSpellSorcerer","Charisma","Sorcerer-list",R.Knowledge.Selection,R.Preparation.None,spells,Full,Enumerable.Repeat(2,20),Enumerable.Repeat(2,20),new int[20],new int[20]);
        var state=new R.HeroState(Array.Empty<R.Repertoire>());var tx=new R.Transaction("native-draft",state,profile,1,true);var grants=0;
        void NativeGrantIfAllowed(bool gate,bool bound,bool draft,R.Transaction transaction){if(SorcererFinalizePreflight.Inspect(gate,bound,draft,transaction)==SorcererFinalizePreflight.Result.PassThrough)grants++;}
        NativeGrantIfAllowed(true,true,true,tx);b(grants==0,"direct incomplete Sorcerer finalization blocked before any native grant delegate");
        b(SorcererFinalizePreflight.Inspect(true,true,true,null)==SorcererFinalizePreflight.Result.IncompleteLearning,"missing transaction cannot bypass direct finalization");
        tx.Select(new R.Choice(1,new[]{"s1","s2"},cantrips:new[]{"c1","c2"}));var before=tx.Preview().Repertoires["Sorcerer"].Known.ToArray();
        b(SorcererFinalizePreflight.Inspect(true,true,true,tx)==SorcererFinalizePreflight.Result.PendingAtomicJournal&&!tx.IsClosed&&tx.Preview().Repertoires["Sorcerer"].Known.SequenceEqual(before),"complete validation is read-only and still requires native finalization journal");
        NativeGrantIfAllowed(false,true,true,tx);b(grants==1,"gate OFF preserves ordinary single-caster finalization path");
        NativeGrantIfAllowed(false,false,true,null);b(grants==2,"gate OFF does not inspect missing mixed drafts");
        b(SorcererFinalizePreflight.Inspect(true,true,false,null)==SorcererFinalizePreflight.Result.PassThrough,"narrow safeguard does not reimplement other-class finalization");
        tx.Cancel();b(!SorcererFinalizePreflight.IsSorcererDraft("Wizard","Wizard",true,new[]{tx}),"later Wizard draft is not a stale Sorcerer draft");
        b(!SorcererFinalizePreflight.IsSorcererDraft("Wizard","Sorcerer",true,new[]{tx}),"current Wizard selection wins over old Sorcerer history");
        b(!SorcererFinalizePreflight.IsSorcererDraft(null,"Wizard",true,new[]{tx}),"known current assigned Wizard ignores closed Sorcerer claim");
        b(SorcererFinalizePreflight.IsSorcererDraft("Sorcerer","Wizard",true,Array.Empty<R.Transaction>()),"Sorcerer advancement without transaction is still guarded");
        b(SorcererFinalizePreflight.IsSorcererDraft(null,null,true,Array.Empty<R.Transaction>()),"missing current-class evidence stays fail closed");b(SorcererFinalizePreflight.Inspect(true,true,true,tx)==SorcererFinalizePreflight.Result.IncompleteLearning,"cancelled Sorcerer draft rejected without publication");
    }
    private static void Bindings(Action<bool,string> a,AssemblyDefinition game,TypeDefinition[] types)
    {
        var context=types.Single(t=>t.Name=="FlexibleCastingPreflightContext");var count=0;
        foreach(var patch in context.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(c=>c.AttributeType.Name=="HarmonyPatch");if(attr==null)continue;
            var type=(TypeReference)attr.ConstructorArguments[0].Value;var name=(string)attr.ConstructorArguments[1].Value;
            var method=game.MainModule.Types.Single(t=>t.FullName==type.FullName).Methods.Single(m=>m.Name==name);
            var prefix=patch.Methods.Single(m=>m.Name=="Prefix");a(true,"native pre-mutation target exists: "+type.Name+"."+name);
            foreach(var p in prefix.Parameters.Where(p=>p.Name.Length==3&&p.Name.StartsWith("__")&&char.IsDigit(p.Name[2])))a(method.Parameters[p.Name[2]-'0'].ParameterType.FullName==p.ParameterType.FullName,"actual native positional ABI "+patch.Name+"/"+p.Name);
            count++;
        }
        a(count==7,"all seven source/UI/local dispatch boundary bindings inspected");
        var callback=game.MainModule.Types.Single(t=>t.Name=="FlexibleCastingModal").Methods.Single(m=>m.Name=="OnConvertCb");
        a(callback.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CreateSpellSlot")&&callback.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CreateSorceryPoints"),"native callback dispatches both directions");
        foreach(var name in new[]{"CreateSpellSlot","CreateSorceryPoints"})
        {
            var native=game.MainModule.Types.Single(t=>t.Name=="LocalCommandManager").Methods.Single(m=>m.Name==name);
            a(native.Parameters.Count==3&&native.Parameters.All(p=>!p.ParameterType.Name.Contains("Guid")),"native local command has no action/effect identity: "+name);
        }
        var convert=context.NestedTypes.Single(t=>t.Name=="ConvertPatch").Methods.Single(m=>m.Name=="Prefix");
        var calls=convert.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
        a(calls.Any(m=>m.Name=="get_IsShiftPressed")&&calls.Any(m=>m.Name=="Prepare")&&calls.Any(m=>m.Name=="BlockNativeCommit"),"native prefix captures current Shift, calls production Prepare and blocks native commit");
        a(!context.Methods.Concat(context.NestedTypes.SelectMany(t=>t.Methods)).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&m.Name=="CommitManaged"),"native adapter never calls managed arithmetic commit");
        var finalizer=types.Single(t=>t.Name=="CharacterBuildingManagerPatcher").NestedTypes.Single(t=>t.Name=="FinalizeCharacter_Patch").Methods.Single(m=>m.Name=="Prefix");
        var names=finalizer.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>((MethodReference)i.Operand).Name).ToArray();
        a(names[0]=="ValidateSorcererFinalizeEntry"&&Array.IndexOf(names,"ValidateSorcererFinalizeEntry")<Array.IndexOf(names,"GrantCustomFeaturesFromFeats"),"direct native finalizer checks Sorcerer draft before feature grants");
        var sourcePrefix=context.NestedTypes.Single(t=>t.Name=="SourceScopePatch").Methods.Single(m=>m.Name=="Prefix");
        a(sourcePrefix.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="RegisteredFor"),"native source requires issued registered/transient authority, not persistent-power membership or a guessed GUID");
        var campaign=game.MainModule.Types.Single(t=>t.Name=="GameCampaignCharacter").Methods.Single(m=>m.Name=="UsePower");
        var cc=campaign.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
        a(cc.Any(r=>r.Name=="InstantiateEffectPower")&&cc.Any(r=>r.Name=="ApplyEffectForms")&&!cc.Any(r=>r.Name.Contains("Register")||r.Name.Contains("Guid")),"campaign source has no registry proof from constructor alone; exact transient entry adapter required");
        var rpc=game.MainModule.Types.Single(t=>t.Name=="NetworkingManager").Methods.Single(m=>m.Name=="CreateSpellSlotRPC");
        a(rpc.Parameters.Count==4&&rpc.Parameters.Last().ParameterType.FullName=="System.String","network payload is tick/hero/level/feature, no effect or unique conversion identity");
        var tick=game.MainModule.Types.Single(t=>t.Name=="NetworkTick").Methods.Single(m=>m.Name=="get_CommandTick");
        a(!tick.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld)&&tick.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="get_MaxTick"),"command tick getter is a clock read, not a unique command allocation");
        var shift=types.Single(t=>t.Name=="FlexibleCastingModalPatcher").NestedTypes.Single().Methods.Single(m=>m.Name=="Postfix");
        var sc=shift.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>((MethodReference)i.Operand).Name).ToArray();
        a(Array.IndexOf(sc,"get_RuntimeEnabled")<Array.IndexOf(sc,"RegisterShiftState"),"bound prototype does not mutate late native Shift counters after blocked callback");
        a(finalizer.CustomAttributes.Any(c=>c.AttributeType.Name=="HarmonyPriority"&&(int)c.ConstructorArguments[0].Value>800),"Sorcerer direct-finalization preflight has explicit priority before other grant prefixes");
        a(!CompressedMulticasterProgressionPlan.MixedGameplayReady,"mixed gameplay remains false, no admission change");
    }
}
