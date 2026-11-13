using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using E = SolastaUnfinishedBusiness.Models.EldritchInvocationSourcesPlan;
using P = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using A = SolastaUnfinishedBusiness.Models.AuxiliarySpellSourcesPlan;

internal static class EldritchInvocationSourcesTests
{
    private static S.Tables standard;
    private static P.Tables pact;
    private static int[][] pactRows;
    private static E registry;
    private static readonly (string Id,string Spell,int Circle,int Level)[] Paid = {
        ("BewitchingWhispers","DreadfulOmen",4,7),("DreadfulWord","Confusion",4,7),
        ("MinionsChaos","ConjureElemental",5,9),("MireMind","Slow",3,5),
        ("SignIllOmen","BestowCurse",3,5),("ThiefFiveFates","Bane",1,1)};
    private static string Ref(JsonElement d,string key)=>d.GetProperty(key).ValueKind==JsonValueKind.Null?null:d.GetProperty(key).GetString().Split(':')[1];
    private static JsonElement Read(string kind,string id)
    {using var doc=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/"+kind+"/"+id+".json"));return doc.RootElement.Clone();}
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Reject(Action action,E.Code code)
    {try{action();return false;}catch(E.Rejection ex){return ex.Diagnostic==code;}}
    private static C.Result Progress(string other=null)
    {
        var full=new[]{1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
        var profiles=new List<C.ClassProfile>{new("Warlock",true,new C.CastingProfile("CastSpellWarlock",C.CasterKind.Pact,1,true,
            pactRows.Select(r=>Array.FindIndex(r,n=>n>0)+1)))};
        var inputs=new List<C.ClassInput>{new("Warlock",other=="Paladin"?8:12,true)};
        if(other!=null)
        {
            profiles.Add(new C.ClassProfile(other,true,new C.CastingProfile("CastSpell"+other,
                other=="Paladin"?C.CasterKind.Half:C.CasterKind.Full,1,true,
                other=="Paladin"?Enumerable.Range(1,20).Select(n=>full[(n+1)/2-1]):full)));
            inputs.Add(new C.ClassInput(other,other=="Paladin"?12:8,true));
        }
        return new C("paid-native-table-fixture",profiles).Calculate(new C.Snapshot(inputs.Sum(c=>c.ActualLevel),inputs,
            inputs.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel))));
    }
    private static P.CastingLedger Resources(string other,S.ResourceMode mode)
    {var r=Progress(other);return new(new S.Budget(standard,S.Allocation.StandardPortion(standard,r,mode)),new P.Budget(new P.Allocation(pact,r)));}
    private static E.Owner Owner(string invocation,E.Origin origin=E.Origin.Warlock,int level=20,string feature="CastSpellWarlock",
        string ability="Charisma",bool valid=true,string pactName=null,string known=null,int character=20)=>new("hero-1",character,level,feature,ability,17,valid,
            new[]{new E.Claim("event-1",origin,invocation)},pactName==null?null:new[]{pactName},known==null?null:new[]{known});
    private static E.Activation Action(string invocation,string spell,int circle,P.Source source=P.Source.Automatic,string op="action-1",
        string hero="hero-1",string origin="event-1",bool shift=false)=>new(op,hero,origin,invocation,spell,circle,source,shift:shift);
    private static E.PaidLedger Ledger(string invocation,string other,S.ResourceMode mode,E.Origin origin=E.Origin.Warlock)
    {
        var progression=Progress(other);
        return new E.PaidLedger(registry,Owner(invocation,origin,level:progression.PactProgressions.Single().EffectiveCastingLevel,
            character:progression.Input.ActualCharacterLevel),Resources(other,mode));
    }
    private static void EmptyStandard(P.CastingLedger resources)
    {for(var circle=1;circle<=9;circle++)for(var i=0;resources.Standard.CanSpend(circle);i++)resources.Standard.Spend("drain-standard-"+circle+"-"+i,circle);}
    private static void EmptyPact(P.CastingLedger resources)
    {for(var i=0;resources.Pact.Remaining>0;i++)resources.Pact.Spend("drain-pact-"+i,P.Source.Pact,resources.Pact.Current.Circle);}
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var behavior=0;var audit=0;
        void B(bool ok,string name){behavior++;check(ok,"E-1 production behavior: "+name);}
        void M(bool ok,string name){audit++;check(ok,"E-1 blueprint/compiled audit: "+name);}
        var compiled=mod.MainModule.Types.SelectMany(Types).ToArray();
        int[] Values(string type,string field)=>CompressedMulticasterTests.AddedIntegers(compiled.Single(t=>t.Name==type),field).Where(n=>n>=0).ToArray();
        var full=Values("FeatureDefinitionCastSpellBuilder","SlotsByCasterLevel");
        standard=new S.Tables(Enumerable.Range(1,20).Select(n=>full.Skip(n*10).Take(9)),
            Values("ModifyPowerPoolAmountPowerSpellPoints","SpellPointsByLevel"),Values("SpellPointsContext","SpellCostByLevel"));
        var display=Values("SharedSpellsContext","WarlockCastingSlots");
        pactRows=Enumerable.Range(0,20).Select(i=>{var d=display.Skip(i*6).Take(6).ToArray();var c=Array.IndexOf(d,0);var r=new int[9];r[c-1]=d[0];return r;}).ToArray();
        pact=new P.Tables(pactRows,Array.Empty<P.Arcanum>());
        var defs=new List<E.Definition>();
        foreach(var expected in Paid)
        {
            var d=Read("InvocationDefinition",expected.Id);var spell=Ref(d,"grantedSpell");
            var circle=Read("SpellDefinition",spell).GetProperty("spellLevel").GetInt32();
            M(spell==expected.Spell && circle==expected.Circle && d.GetProperty("requiredLevel").GetInt32()==expected.Level,
                expected.Id+" installed spell/circle/prerequisite");
            M(Ref(d,"grantedFeature")==null && Ref(d,"requiredPact")==null && Ref(d,"requiredKnownSpell")==null &&
                d.GetProperty("consumesSpellSlot").GetBoolean() && d.GetProperty("longRestRecharge").GetBoolean(),expected.Id+" slot + once per long rest, no invented pact/known-spell prerequisite");
            defs.Add(new E.Definition(expected.Id,E.Kind.PaidSpell,spell,circle,Ref(d,"grantedFeature"),d.GetProperty("requiredLevel").GetInt32(),
                Ref(d,"requiredPact"),Ref(d,"requiredKnownSpell"),d.GetProperty("consumesSpellSlot").GetBoolean(),d.GetProperty("longRestRecharge").GetBoolean()));
        }
        foreach(var id in new[]{"DevilsSight","BeguilingInfluence","EyesRuneKeeper"})
        {
            var d=Read("InvocationDefinition",id);
            defs.Add(new E.Definition(id,E.Kind.Passive,null,0,Ref(d,"grantedFeature"),d.GetProperty("requiredLevel").GetInt32(),Ref(d,"requiredPact"),Ref(d,"requiredKnownSpell"),false,false));
        }
        defs.Add(new E.Definition("UnverifiedPower",E.Kind.Unsupported,null,0,"UnverifiedPower",1,null,null,false,false));
        // Synthetic prerequisites exercise preserved constraints, not a claim that these six require them.
        defs.Add(new E.Definition("PrerequisiteFixture",E.Kind.PaidSpell,"Bane",1,null,5,"PactFixture","KnownFixture",true,true));
        registry=new E(defs,"CastSpellWarlock","Charisma");
        PaidBehavior(B); Adept(B); NativeAudit(M,game,compiled);
        Console.WriteLine($"Stage13.5E-1: {behavior} executed production behavior checks; {audit} blueprint/compiled-binding audits; no native gameplay execution.");
    }
    private static void PaidBehavior(Action<bool,string> check)
    {
        foreach(var x in Paid)foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})
        foreach(var other in new[]{(string)null,"Sorcerer","Paladin"})foreach(var origin in new[]{E.Origin.Warlock,E.Origin.EldritchAdept})
        {
            var label=x.Id+"/"+mode+"/Warlock+"+(other??"none")+"/"+origin;
            var l=Ledger(x.Id,other,mode,origin);var r=l.Resources;var before=r.Standard.RemainingPoints;var slots=r.Pact.Remaining;
            var a=Action(x.Id,x.Spell,5,P.Source.Pact);
            check(l.Prepare(a).Allowed && r.Pact.Remaining==slots && r.Standard.RemainingPoints==before,label+" prepare is read-only");
            check(l.Commit(a).Resource==P.Source.Pact && r.Pact.Remaining==slots-1 && r.Standard.RemainingPoints==before && l.Used(x.Id),label+" exact Pact5 debit and invocation use");
            check(l.Commit(a).Allowed && r.Pact.Remaining==slots-1,label+" repeated action never charges twice");
            check(Reject(()=>l.Commit(Action(x.Id,x.Spell,5,P.Source.Standard)),E.Code.ConflictingAction),label+" conflicting resource identity");
            check(Reject(()=>l.Commit(Action(x.Id,x.Spell,5,op:"new-action")),E.Code.AlreadyUsed),label+" one use shared by invocation definition");
            check(Reject(()=>l.CancelPrepared(a.Operation),E.Code.PendingAtomicJournal) && r.Pact.Remaining==slots-1,label+" no invented native post-spend refund");
            l.Rest("short",false);check(r.Pact.Remaining==slots && l.Used(x.Id),label+" short rest restores Pact but not invocation charge");
            l.Rest("long",true,true);check(l.Used(x.Id),label+" rest simulation changes nothing");
            l.Rest("long",true);check(!l.Used(x.Id) && r.Pact.Remaining==slots,label+" long rest restores both independent resources");
            l.Commit(Action(x.Id,x.Spell,5,op:"post-rest"));l.Rest("long",true);
            check(l.Used(x.Id) && r.Pact.Remaining==slots-1 && l.Commit(a).Allowed && r.Pact.Remaining==slots-1,label+" repeated rest/action receipts never refill or redebit");
            if(other!=null)
            {
                l=Ledger(x.Id,other,mode,origin);r=l.Resources;slots=r.Pact.Remaining;before=r.Standard.RemainingPoints;
                var count=r.Standard.RemainingSlots(x.Circle);var cost=standard.Cost(x.Circle);
                var decision=l.Commit(Action(x.Id,x.Spell,x.Circle,P.Source.Standard));
                check(decision.Resource==P.Source.Standard && r.Pact.Remaining==slots &&
                    (mode==S.ResourceMode.SpellPoints?r.Standard.RemainingPoints==before-cost:r.Standard.RemainingSlots(x.Circle)==count-1),label+" actual minimum circle/installed cost, no Pact debit");
                var remainingPoints=r.Standard.RemainingPoints;var remainingSlots=r.Standard.RemainingSlots(x.Circle);
                l.Rest("standard-short",false);
                check(r.Standard.RemainingPoints==remainingPoints && r.Standard.RemainingSlots(x.Circle)==remainingSlots && l.Used(x.Id),label+" short rest leaves standard debt and invocation use");
                l.Rest("standard-long",true);
                check(r.Standard.RemainingPoints==before && r.Standard.RemainingSlots(x.Circle)==count && !l.Used(x.Id),label+" long rest restores standard resource and invocation use");
                l=Ledger(x.Id,other,mode,origin);EmptyPact(l.Resources);
                check(l.Commit(Action(x.Id,x.Spell,x.Circle)).Resource==P.Source.Standard,label+" Pact exhausted, legal standard resource remains");
                l=Ledger(x.Id,other,mode,origin);EmptyStandard(l.Resources);
                check(l.Commit(Action(x.Id,x.Spell,5)).Resource==P.Source.Pact,label+" standard exhausted, Pact remains");
            }
            else check(r.Standard.Current.CasterLevel==0 && before==0,label+" invocation adds no ordinary pool");
            l=Ledger(x.Id,other,mode,origin);EmptyStandard(l.Resources);EmptyPact(l.Resources);
            check(Reject(()=>l.Commit(Action(x.Id,x.Spell,5)),E.Code.Exhausted),label+" both exhausted: no free invocation");
        }
        foreach(var x in Paid)
        {
            var l=Ledger(x.Id,"Sorcerer",S.ResourceMode.SpellPoints);
            check(Reject(()=>l.Commit(Action(x.Id,"Shield",5)),E.Code.InvalidSpell),x.Id+" cannot cast substituted spell");
            check(Reject(()=>l.Commit(Action(x.Id,x.Spell,5,hero:"other-hero")),E.Code.WrongOwner),x.Id+" wrong hero");
            check(Reject(()=>l.Commit(Action(x.Id,x.Spell,5,origin:"foreign-origin")),E.Code.WrongOwner),x.Id+" wrong acquisition origin");
            check(Reject(()=>l.Commit(Action(x.Id,x.Spell,5,op:"")),E.Code.MissingActionIdentity),x.Id+" identity-free native action blocked");
            check(Reject(()=>l.Commit(Action(x.Id,x.Spell,x.Circle-1)),E.Code.InvalidCircle),x.Id+" cannot bypass required spell circle");
            check(Reject(()=>l.Commit(Action(x.Id,x.Spell,5,P.Source.Arcanum)),E.Code.InvalidCircle),x.Id+" cannot spend Arcanum");
            var a=Action(x.Id,x.Spell,5);var remain=l.Resources.Pact.Remaining;l.Prepare(a);l.CancelPrepared(a.Operation);
            check(l.Resources.Pact.Remaining==remain && !l.Used(x.Id) && Reject(()=>l.Commit(a),E.Code.ClosedSelection),x.Id+" safe precommit cancellation, stale action rejected");
            var invalid=new E.PaidLedger(registry,Owner(x.Id,valid:false),Resources("Sorcerer",S.ResourceMode.Slots));
            check(Reject(()=>invalid.Commit(a),E.Code.MissingPrerequisite),x.Id+" unverified native prerequisites rejected before debit");
            if(x.Level>1)
            {
                invalid=new E.PaidLedger(registry,Owner(x.Id,level:x.Level-1),Resources(null,S.ResourceMode.Slots));
                check(Reject(()=>invalid.Commit(a),E.Code.MissingPrerequisite),x.Id+" original Warlock level prerequisite");
            }
        }

        foreach(var wrong in new[]{Owner("ThiefFiveFates",feature:"CastSpellWizard"),Owner("ThiefFiveFates",ability:"Intelligence")})
        {
            var invalid=new E.PaidLedger(registry,wrong,Resources("Sorcerer",S.ResourceMode.SpellPoints));
            check(Reject(()=>invalid.Commit(Action("ThiefFiveFates","Bane",1)),E.Code.WrongOwner),"paid invocation cannot guess another class ability/feature");
        }
        var owner=Owner("PrerequisiteFixture",pactName:"PactFixture",known:"KnownFixture");
        check(registry.Owned(owner,"event-1","PrerequisiteFixture").ConsumesSlot,"native pact and required known spell both preserved");
        foreach(var missing in new[]{Owner("PrerequisiteFixture",known:"KnownFixture"),Owner("PrerequisiteFixture",pactName:"PactFixture")})
            check(Reject(()=>registry.Owned(missing,"event-1","PrerequisiteFixture"),E.Code.MissingPrerequisite),"missing native pact/known-spell proof rejected");
        var shared=new E.Owner("hero-1",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("event-1",E.Origin.Warlock,"ThiefFiveFates"),new E.Claim("feat-1",E.Origin.EldritchAdept,"ThiefFiveFates")});
        var ledger=new E.PaidLedger(registry,shared,Resources("Sorcerer",S.ResourceMode.SpellPoints));
        ledger.Commit(Action("ThiefFiveFates","Bane",1,P.Source.Standard));
        check(Reject(()=>ledger.Commit(Action("ThiefFiveFates","Bane",1,P.Source.Standard,"feat-cast",origin:"feat-1")),E.Code.AlreadyUsed),"legitimate duplicate origins share native used flag, not extra free charges");
        ledger=new E.PaidLedger(registry,Owner("ThiefFiveFates"),Resources("Sorcerer",S.ResourceMode.Slots),new[]{"ThiefFiveFates"});
        check(Reject(()=>ledger.Commit(Action("ThiefFiveFates","Bane",1)),E.Code.AlreadyUsed),"restore existing native used debt, no initialization refill");
        // Prepare must revalidate when a different action exhausts the shared coordinator.
        ledger=Ledger("MireMind","Sorcerer",S.ResourceMode.SpellPoints);var pending=Action("MireMind","Slow",3,P.Source.Standard);ledger.Prepare(pending);EmptyStandard(ledger.Resources);
        check(Reject(()=>ledger.Commit(pending),E.Code.Exhausted) && !ledger.Used("MireMind"),"stale resource availability fails without marking invocation used");
        ledger=Ledger("ThiefFiveFates","Sorcerer",S.ResourceMode.SpellPoints);pending=Action("ThiefFiveFates","Bane",5);ledger.Prepare(pending);ledger.Rest("rest-before-commit",false);
        check(Reject(()=>ledger.Commit(pending),E.Code.ClosedSelection),"rest invalidates uncommitted activation identity");
        ledger=Ledger("ThiefFiveFates","Sorcerer",S.ResourceMode.SpellPoints,E.Origin.EldritchAdept);
        pending=Action("ThiefFiveFates","Bane",1,P.Source.Standard);ledger.Prepare(pending);
        var noClaims=new E.Owner("hero-1",20,20,"CastSpellWarlock","Charisma",17,true,Array.Empty<E.Claim>());
        ledger.RefreshOwner(noClaims);
        check(Reject(()=>ledger.Commit(pending),E.Code.ClosedSelection) && Reject(()=>ledger.Commit(Action("ThiefFiveFates","Bane",1,P.Source.Standard,"after-removal")),E.Code.WrongOwner),"feat removal invalidates pending selection and stale invocation ownership");
        ledger.RefreshOwner(Owner("ThiefFiveFates",E.Origin.EldritchAdept));ledger.Commit(Action("ThiefFiveFates","Bane",1,P.Source.Standard,"reacquired"));
        var points=ledger.Resources.Standard.RemainingPoints;
        ledger.RefreshOwner(noClaims);ledger.RefreshOwner(Owner("ThiefFiveFates",E.Origin.EldritchAdept));
        check(ledger.Used("ThiefFiveFates") && ledger.Resources.Standard.RemainingPoints==points &&
            Reject(()=>ledger.Commit(Action("ThiefFiveFates","Bane",1,P.Source.Standard,"no-refill")),E.Code.AlreadyUsed),"feat removal/reselection/refresh does not refill used invocation or pool");
        ledger=Ledger("ThiefFiveFates","Sorcerer",S.ResourceMode.SpellPoints);
        pending=Action("ThiefFiveFates","Bane",5,shift:true);
        check(ledger.Commit(pending).Resource==P.Source.Standard,"Automatic Shift keeps installed standard versus Pact preference");
    }
    private static void Adept(Action<bool,string> check)
    {
        E.Owner NoWarlock(int level=2)=>new("non-warlock",level,0,null,null,0,true,Array.Empty<E.Claim>());
        foreach(var passive in new[]{"DevilsSight","BeguilingInfluence","EyesRuneKeeper"})
        {
            var owner=NoWarlock();var tx=new E.AdeptSelection(registry,owner,"feat:adept:original4");tx.Select(passive);var result=tx.Commit();
            check(result.Count==1 && result[0].Source==E.Origin.EldritchAdept && result[0].Invocation==passive && ReferenceEquals(result,tx.Commit()),passive+" feat-owned single grant, idempotent backend commit");
            check(owner.WarlockPrerequisiteLevel==0 && owner.CastingFeature==null && owner.Claims.Count==0,passive+" no class, repertoire or resource added");
            tx=new E.AdeptSelection(registry,owner,"feat:pending");tx.Select(passive);tx.Back();
            check(Reject(()=>tx.Commit(),E.Code.MissingPrerequisite),passive+" Back clears only pending award; Skip cannot invent grant");
            tx.Select(passive);tx.Reset();tx.Select(passive);check(tx.Cancel().Count==0 && Reject(()=>tx.Commit(),E.Code.ClosedSelection),passive+" Reset/Cancel preserve initial claims");
        }
        var l1=new E.AdeptSelection(registry,NoWarlock(1),"feat:too-early");
        check(Reject(()=>l1.Select("DevilsSight"),E.Code.MissingPrerequisite),"native feat character-level2 prerequisite");
        var tx2=new E.AdeptSelection(registry,NoWarlock(),"feat:paid");
        check(Reject(()=>tx2.Select("ThiefFiveFates"),E.Code.MissingRepertoire),"non-Warlock Bane invocation not assigned to Wizard or free Pact Magic");
        check(Reject(()=>tx2.Select("MireMind"),E.Code.MissingPrerequisite),"non-Warlock cannot use character level for Warlock5 requirement");
        check(Reject(()=>tx2.Select("UnverifiedPower"),E.Code.UnsupportedOption),"unsupported feature/power ownership fails closed");
        check(Reject(()=>tx2.Select("Unknown"),E.Code.UnknownInvocation),"unknown invocation rejected");
        var paidOwner=new E.Owner("hero-1",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("class-event",E.Origin.Warlock,"DevilsSight")});
        tx2=new E.AdeptSelection(registry,paidOwner,"feat:adept");tx2.Select("ThiefFiveFates");var claims=tx2.Commit();
        check(claims.Count==2 && claims[0].Source==E.Origin.Warlock && claims[1].Source==E.Origin.EldritchAdept,"class invocation and feat invocation retain independent origins");
        tx2=new E.AdeptSelection(registry,paidOwner,"feat:duplicate");
        check(Reject(()=>tx2.Select("DevilsSight"),E.Code.DuplicateOrigin),"native duplicate selection not fabricated as additional award");
        check(Reject(()=>new E.AdeptSelection(registry,paidOwner,"class-event"),E.Code.DuplicateOrigin),"origin collision rejected");
        var confirmed=new E.Owner("hero-1",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("class-event",E.Origin.Warlock,"DevilsSight"),new E.Claim("feat-event",E.Origin.EldritchAdept,"DevilsSight")});
        check(Reject(()=>registry.RemoveAdept(confirmed,"feat-event",false),E.Code.CleanupRequired),"feat removal requires feature/condition cleanup proof");
        var left=registry.RemoveAdept(confirmed,"feat-event",true);
        check(left.Count==1 && left[0].Id=="class-event","removing feat claim retains confirmed class origin for same definition");
        check(Reject(()=>registry.RemoveAdept(confirmed,"class-event",true),E.Code.WrongOwner),"feat removal cannot remove class claim");
        foreach(var domain in new[]{A.Domain.PaidInvocation,A.Domain.InvocationSelection})
        {
            var source=new A.Source("typed:"+domain,A.Origin.Feat,"FeatEldritchAdept","choice","Invocation","NativeChoice",null,domain,2,"NativeInvocation","InvocationChoice",new[]{new MultipleSpellRepertoiresPlan.Spell("Bane",1)});
            var pending=false;try{source.Route("Bane",false);}catch(A.Rejection ex){pending=ex.Diagnostic==A.Code.PendingAtomicJournal;}
            check(pending && source.CalculatorInput().OwnerClassId==null && !source.CanUseNativeInvocation(false),domain+" cannot fall through to free invocation or fake class");
        }
        check(!C.MixedGameplayReady,"mixed gate remains false, no class admission change");
    }
    private static void NativeAudit(Action<bool,string> check,AssemblyDefinition game,TypeDefinition[] compiled)
    {
        bool Calls(MethodDefinition m,string type,string name)=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name==type && r.Name==name);
        var consume=game.MainModule.Types.Single(t=>t.Name=="RulesetInvocation").Methods.Single(m=>m.Name=="Consume");
        var writes=consume.Body.Instructions.Select((i,n)=>(i,n)).Where(p=>p.i.Operand is FieldReference f && f.Name=="used").Select(p=>p.n).ToArray();
        var spend=consume.Body.Instructions.Select((i,n)=>(i,n)).Single(p=>p.i.Operand is MethodReference r && r.Name=="SpendSpellSlot").n;
        check(consume.Parameters.Count==1 && consume.Parameters[0].ParameterType.Name=="RulesetSpellRepertoire" && writes.Any(n=>n<spend) && Calls(consume,"RulesetSpellRepertoire","GetLowestAvailableSlotLevel"),
            "Consume has no action ID, marks used before lowest-slot debit: not atomic");
        var manager=game.MainModule.Types.Single(t=>t.Name=="RulesetImplementationManager");
        var instantiate=manager.Methods.Single(m=>m.Name=="InstantiateEffectInvocation");
        check(Calls(instantiate,"RulesetCharacter","GetSpellRepertoireForInvocations") && Calls(instantiate,"RulesetSpellRepertoire","GetLowestAvailableSlotLevel"),"invocation effect uses native invocation-access repertoire/lowest circle");
        var location=game.MainModule.Types.Single(t=>t.Name=="RulesetImplementationManagerLocation").Methods.Single(m=>m.Name=="InstantiateEffectInvocation");
        check(Calls(location,"RulesetImplementationManager","InstantiateEffectInvocation"),"Location override enters guarded base instantiate before effect registration");
        var character=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter");
        check(Calls(character.Methods.Single(m=>m.Name=="UseInvocation"),"RulesetInvocation","Consume") &&
            Calls(character.Methods.Single(m=>m.Name=="CastSpell"),"RulesetCharacter","UseInvocation"),"actual spend path: CastSpell -> UseInvocation -> Consume (not ordinary known spell path)");
        var context=compiled.Single(t=>t.Name=="EldritchInvocationSourcesContext");
        foreach(var patch in context.NestedTypes.Where(t=>t.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPatch")))
        {
            var attr=patch.CustomAttributes.Single(a=>a.AttributeType.Name=="HarmonyPatch");var type=(TypeReference)attr.ConstructorArguments[0].Value;var name=(string)attr.ConstructorArguments[1].Value;
            var methods=game.MainModule.Types.Single(t=>t.FullName==type.FullName).Methods.Where(m=>m.Name==name).ToArray();
            check(methods.Length==1,patch.Name+" unique actual Harmony target");
            var prefix=patch.Methods.Single(m=>m.Name=="Prefix");
            foreach(var arg in prefix.Parameters.Where(p=>p.Name.StartsWith("__") && int.TryParse(p.Name.Substring(2),out _)))
            {var index=int.Parse(arg.Name.Substring(2));check(index<methods[0].Parameters.Count && methods[0].Parameters[index].ParameterType.FullName==arg.ParameterType.FullName,patch.Name+" native positional ABI");}
            check(Calls(prefix,"EldritchInvocationSourcesContext","RequireNativeJournal"),patch.Name+" guard before native side effects");
        }
        check(context.NestedTypes.Count(t=>t.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPatch"))==4,"four native boundaries guarded, no replacement Pact engine");
        var guard=context.Methods.Single(m=>m.Name=="RequireNativeJournal");
        check(Calls(guard,"StandardMulticasterResourcesContext","get_RuntimeEnabled") &&
            Calls(guard,"InvocationDefinition","get_ConsumesSpellSlot") &&
            !Calls(guard,"CastingLedger","Spend") && !Calls(guard,"RulesetInvocation","Consume"),"gate OFF/free invocations exit without debit or native use; guard does not implement native payment");
        foreach(var id in new[]{"ArmorOfShadows","FiendishVigor","EldritchSight"})
        {var d=Read("InvocationDefinition",id);check(!d.GetProperty("consumesSpellSlot").GetBoolean(),id+" native at-will definition untouched");}
        var cast=Read("FeatureDefinitionCastSpell","CastSpellWarlock");
        check(cast.GetProperty("hasAccessToInvocations").GetBoolean() && cast.GetProperty("spellcastingAbility").GetString()=="Charisma","native invocation-access Warlock feature/ability captured explicitly");
        check(Calls(compiled.Single(t=>t.Name=="TrainInvocation_Patch").Methods.Single(m=>m.Name=="Prefix"),"EldritchInvocationSourcesContext","BeforeTraining"),"pending feat origin guard before invocation grant/pool helper");
        var build=compiled.Single(t=>t.Name=="OtherFeats").Methods.Single(m=>m.Name=="BuildEldritchAdept");
        check(Calls(build,"FeatureDefinitionPointPoolBuilder","SetPool") && !Calls(build,"FeatureDefinitionCastSpellBuilder","Create"),"native Adept owns invocation point pool, not new casting feature");
        var aux=compiled.Single(t=>t.Name=="AuxiliarySpellSourcesContext");
        check(Calls(aux.Methods.Single(m=>m.Name=="ValidateRootChoice"),"EldritchInvocationSourcesContext","RequireAdeptJournal"),"feat guard precedes native TrainFeat grants");
        check(Calls(aux.Methods.Single(m=>m.Name=="CaptureInstalledDefinitions"),"EldritchInvocationSourcesContext","CaptureInstalledDefinitions"),"production source registry captured from installed definitions");
    }
}
