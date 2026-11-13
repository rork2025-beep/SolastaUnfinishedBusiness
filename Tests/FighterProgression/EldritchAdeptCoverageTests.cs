using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using E = SolastaUnfinishedBusiness.Models.EldritchInvocationSourcesPlan;
using A = SolastaUnfinishedBusiness.Models.AuxiliarySpellSourcesPlan;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using P = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;

internal static class EldritchAdeptCoverageTests
{
    private static readonly Dictionary<string,JsonElement> Db=new();
    private static E catalog;
    private static S.Tables standard;
    private static P.Tables pact;
    private static int[][] pactRows;
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Reject(Action action,E.Code code){try{action();return false;}catch(E.Rejection ex){return ex.Diagnostic==code;}}
    private static string Ref(JsonElement d,string key)=>d.GetProperty(key).ValueKind==JsonValueKind.Null?null:d.GetProperty(key).GetString().Split(':')[1];
    private static JsonElement Read(string kind,string id)=>Db[kind+":"+id];
    private static JsonElement Feature(string id)=>Db.Single(p=>p.Key.EndsWith(":"+id,StringComparison.Ordinal) && p.Key.StartsWith("FeatureDefinition",StringComparison.Ordinal)).Value;
    private static IEnumerable<(string Id,JsonElement Json)> Graph(string feature)
    {if(feature==null)yield break;var d=Feature(feature);yield return(feature,d);if(d.TryGetProperty("featureSet",out var children))foreach(var child in children.EnumerateArray())foreach(var node in Graph(child.GetString().Split(':')[1]))yield return node;}
    private static string[] Invocations=>Db.Where(p=>p.Key.StartsWith("InvocationDefinition:",StringComparison.Ordinal) || p.Key.StartsWith("InvocationDefinitionWithPrerequisites:",StringComparison.Ordinal)).Select(p=>p.Value.GetProperty("name").GetString()).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    private static JsonElement Invocation(string id)=>Db.First(p=>(p.Key.StartsWith("InvocationDefinition:",StringComparison.Ordinal) || p.Key.StartsWith("InvocationDefinitionWithPrerequisites:",StringComparison.Ordinal)) && p.Key.EndsWith(":"+id,StringComparison.Ordinal)).Value;
    private static E.Definition Fixture(string id,bool rules2024=false,bool changed=false,bool unknownValidator=false)
    {
        var d=Invocation(id);var feature=Ref(d,"grantedFeature");var spell=Ref(d,"grantedSpell");
        if(id=="OneWithShadows" && rules2024)feature="PowerInvocationOneWithShadows";
        var nodes=Graph(feature).ToArray();
        var grants=nodes.Select(n=>n.Json.GetProperty("$type").GetString().StartsWith("FeatureDefinitionPower,",StringComparison.Ordinal)
            ?new E.FeatureGrant(n.Id,"FeatureDefinitionPower",n.Json.GetProperty("rechargeRate").GetString(),n.Json.GetProperty("costPerUse").GetInt32(),n.Json.GetProperty("usesDetermination").GetString(),n.Json.GetProperty("fixedUsesPerRecharge").GetInt32(),n.Json.GetProperty("usesAbilityScoreName").GetString(),n.Json.GetProperty("activationTime").GetString())
            :new E.FeatureGrant(n.Id,n.Json.GetProperty("$type").GetString().Split(',')[0],mode:n.Json.TryGetProperty("mode",out var m)?m.GetString():null,ownerClass:n.Id=="AdditionalDamageInvocationEldritchSmite"?"Warlock":null,
                trigger:n.Json.TryGetProperty("triggerCondition",out var trigger)?trigger.GetString():null)).ToArray();
        var level=d.GetProperty("requiredLevel").GetInt32();
        // Source-verified 2024 switch adjusts only level1/2 standard/registered UB invocations;
        // the three CE Pact definitions are not in InvocationsContext.Invocations.
        if(id is not ("ArmorOfShadows" or "InvocationEldritchMind" or "InvocationPactBlade" or "InvocationPactChain" or "InvocationPactTome") && level is 1 or 2)level=rules2024?2:1;
        if(id=="OtherworldlyLeap")level=rules2024?2:9;
        var validator=id is "InvocationBurningHex" or "InvocationChillingHex" or "InvocationVexingHex" or "InvocationInexorableHex"?E.ValidatorRule.Hex:id=="InvocationDevouringBlade"?E.ValidatorRule.ThirstingBlade:E.ValidatorRule.None;
        A.Source source=null;var pools=new List<A.Pool>();
        foreach(var n in nodes.Where(n=>n.Json.TryGetProperty("poolType",out _)))
        {
            var p=n.Json;var list=Ref(p,"spellListOverride");var spells=Read("SpellListDefinition",list).GetProperty("spellsByLevel").EnumerateArray().SelectMany(r=>r.GetProperty("spells").EnumerateArray().Select(s=>new R.Spell(s.GetString().Split(':')[1],r.GetProperty("level").GetInt32()))).GroupBy(s=>s.Id).Select(g=>g.First()).ToArray();
            pools.Add(new A.Pool(n.Id,spells,p.GetProperty("poolAmount").GetInt32(),p.GetProperty("poolType").GetString()=="Cantrip"?A.ChoiceKind.Cantrip:A.ChoiceKind.Spell,p.GetProperty("minSpellLevel").GetInt32(),p.GetProperty("maxSpellLevel").GetInt32(),p.GetProperty("extraSpellsTag").GetString(),list,p.GetProperty("ritualOnly").GetBoolean(),spells.Where(s=>Read("SpellDefinition",s.Id).GetProperty("ritual").GetBoolean()).Select(s=>s.Id)));
        }
        if(pools.Count>0)source=new A.Source("InvocationChoices:"+id,A.Origin.Invocation,id,feature,"Attachment","Charisma","Warlock",pools.All(pool=>pool.RitualOnly)?A.Domain.NativeRitual:A.Domain.AttachedClass,Math.Max(1,level),"NativeClass","ChoicePool",pools.SelectMany(p=>p.Allowed).GroupBy(s=>s.Id).Select(g=>g.First()),pools,castingFeature:"CastSpellWarlock");
        return EldritchInvocationCatalog.Classify(id,spell,spell==null?0:Read("SpellDefinition",spell).GetProperty("spellLevel").GetInt32(),changed?"ChangedFeature":feature,level,Ref(d,"requiredPact"),Ref(d,"requiredKnownSpell"),d.GetProperty("consumesSpellSlot").GetBoolean(),d.GetProperty("longRestRecharge").GetBoolean(),grants,unknownValidator?E.ValidatorRule.Unverified:validator,source,
            id!="InvocationStasis" && (rules2024 || id is not ("InvocationPactBlade" or "InvocationPactChain" or "InvocationPactTome")));
    }
    private static E.Owner Owner(E.Definition d,E.Origin origin=E.Origin.EldritchAdept,bool warlock=true,bool casting=true,IEnumerable<E.Claim> claims=null,bool valid=true)=>new(
        "F-hero",20,warlock?20:0,casting?"CastSpellWarlock":null,casting?"Charisma":null,17,valid,
        claims??new[]{new E.Claim("origin",origin,d.Id)},new[]{"FeatureSetPactTome","FeatureSetPactChain","FeatureSetPactBlade"},new[]{"EldritchBlast","Malediction","BestowCurse"},warlock?12:0,
        prerequisiteInvocations:new[]{"ThirstingBlade","SignIllOmen"});
    private static C.Result Progress()
    {
        var full=new[]{1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};var inputs=new[]{new C.ClassInput("Warlock",12,true),new C.ClassInput("Sorcerer",8,true)};
        return new C("F-actual-fixture",new[]{new C.ClassProfile("Warlock",true,new C.CastingProfile("CastSpellWarlock",C.CasterKind.Pact,1,true,pactRows.Select(r=>Array.FindIndex(r,n=>n>0)+1))),new C.ClassProfile("Sorcerer",true,new C.CastingProfile("CastSpellSorcerer",C.CasterKind.Full,1,true,full))}).Calculate(new C.Snapshot(20,inputs,inputs.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel))));
    }
    private static P.CastingLedger Resources(S.ResourceMode mode){var r=Progress();return new(new S.Budget(standard,S.Allocation.StandardPortion(standard,r,mode)),new P.Budget(new P.Allocation(pact,r)));}
    private static E.Activation Cast(E.Definition d,string op="cast",P.Source source=P.Source.Automatic,int? circle=null)=>new(op,"F-hero","origin",d.Id,d.Spell,circle??d.Circle,source);
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var behavior=0;var audit=0;
        void B(bool ok,string name){behavior++;check(ok,"F production behavior: "+name);}
        void M(bool ok,string name){audit++;check(ok,"F definition/compiled audit: "+name);}
        foreach(var root in new[]{"OfficialBlueprints","UnfinishedBusinessBlueprints"})foreach(var file in Directory.GetFiles("Diagnostics/"+root,"*.json",SearchOption.AllDirectories))
        {using var doc=JsonDocument.Parse(File.ReadAllText(file));Db.TryAdd(Path.GetFileName(Path.GetDirectoryName(file))+":"+Path.GetFileNameWithoutExtension(file),doc.RootElement.Clone());}
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();int[] V(string type,string field)=>CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name==type),field).Where(n=>n>=0).ToArray();
        var slots=V("FeatureDefinitionCastSpellBuilder","SlotsByCasterLevel");standard=new S.Tables(Enumerable.Range(1,20).Select(n=>slots.Skip(n*10).Take(9)),V("ModifyPowerPoolAmountPowerSpellPoints","SpellPointsByLevel"),V("SpellPointsContext","SpellCostByLevel"));
        var display=V("SharedSpellsContext","WarlockCastingSlots");pactRows=Enumerable.Range(0,20).Select(i=>{var row=display.Skip(i*6).Take(6).ToArray();var p=new int[9];p[Array.IndexOf(row,0)-1]=row[0];return p;}).ToArray();pact=new P.Tables(pactRows,Array.Empty<P.Arcanum>());
        M(Invocations.Length==68 && Invocations.SequenceEqual(EldritchInvocationCatalog.Ids.OrderBy(x=>x,StringComparer.Ordinal)),"exact68 installed standard definitions including5 UB validator definitions; no custom-pool leakage");
        foreach(var rules2024 in new[]{false,true})
        {
            var definitions=Invocations.Select(id=>Fixture(id,rules2024)).ToArray();catalog=new E(definitions,"CastSpellWarlock","Charisma");
            foreach(var d in definitions)
            {
                B(d.Type!=E.Kind.Unsupported,d.Id+" classified with installed spell/feature graph, rules2024="+rules2024);
                foreach(var origin in new[]{E.Origin.Warlock,E.Origin.EldritchAdept})
                {
                    var owner=Owner(d,origin);if(!d.Selectable){B(Reject(()=>new E.AdeptSelection(catalog,Owner(d,claims:Array.Empty<E.Claim>()),"hidden").Select(d.Id),E.Code.UnavailableOption),d.Id+" hidden/disabled native candidate rejected for new selection");continue;}
                    B(catalog.Owned(owner,"origin",d.Id)==d,d.Id+" valid real Warlock "+origin+" ownership/prerequisites");
                    var empty=Owner(d,origin,claims:Array.Empty<E.Claim>());var tx=new E.AdeptSelection(catalog,empty,"new-feat");tx.Select(d.Id);
                    if(d.Choices!=null)
                    {
                        foreach(var pool in d.Choices.Pools)tx.Choose(pool.Id,pool.Allowed.Take(pool.Amount).Select(s=>s.Id));
                        B(tx.ChoicePreview.Values.Sum(v=>v.Count)==d.Choices.Pools.Sum(p=>p.Amount),d.Id+" actual nested native choice budget and origin retained");
                    }
                    var claims=tx.Commit();B(ReferenceEquals(claims,tx.Commit()) && claims.Count==1 && claims[0].Source==E.Origin.EldritchAdept,d.Id+" feat journal managed commit idempotent");
                    tx=new E.AdeptSelection(catalog,empty,"cancel-feat");tx.Select(d.Id);tx.Back();tx.Select(d.Id);tx.Reset();B(tx.Cancel().Count==0,d.Id+" Back/Reset/Cancel leave other origins intact");
                    if(d.Choices!=null)
                    {
                        tx=new E.AdeptSelection(catalog,empty,"cancel-choices");tx.Select(d.Id);
                        foreach(var pool in d.Choices.Pools)tx.Choose(pool.Id,pool.Allowed.Take(pool.Amount).Select(s=>s.Id));
                        tx.Cancel();B(tx.ChoicePreview.Count==empty.ConfirmedChoices.Count,d.Id+" cancel discards pending native spell choices without losing confirmed unrelated choices");
                    }
                    B(catalog.EffectiveGrants(Owner(d,claims:new[]{new E.Claim("class",E.Origin.Warlock,d.Id),new E.Claim("feat",E.Origin.EldritchAdept,d.Id)})).Count==1,d.Id+" two legitimate origins, one effective native feature/resource grant");
                    B(Reject(()=>catalog.Owned(Owner(d,origin,valid:false),"origin",d.Id),E.Code.MissingPrerequisite),d.Id+" failed native validators cannot train or activate");
                }
                if(!d.Selectable)continue;
                var noWarlock=Owner(d,warlock:false,casting:false);
                if(d.RequiredLevel>1 || d.RequiresWarlock)B(Reject(()=>catalog.Owned(noWarlock,"origin",d.Id),E.Code.MissingPrerequisite),d.Id+" non-Warlock cannot substitute character20 for Warlock prerequisite");
                else if(d.RequiresRepertoire)B(Reject(()=>catalog.Owned(noWarlock,"origin",d.Id),E.Code.MissingRepertoire),d.Id+" non-Warlock receives no fabricated invocation repertoire");
                else B(catalog.Owned(noWarlock,"origin",d.Id)==d && noWarlock.ActualWarlockLevel==0,d.Id+" non-Warlock feat allowed with real native known spell/pact proof, no class inflation");
                if(d.RequiredKnownSpell!=null)
                {
                    var missing=new E.Owner("F-hero",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("origin",E.Origin.EldritchAdept,d.Id)},Owner(d).Pacts,actualWarlockLevel:12);
                    B(Reject(()=>catalog.Owned(missing,"origin",d.Id),E.Code.MissingPrerequisite),d.Id+" required known spell remains mandatory");
                }
                if(d.RequiredPact!=null)
                {
                    var missing=new E.Owner("F-hero",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("origin",E.Origin.EldritchAdept,d.Id)},knownSpells:Owner(d).KnownSpells,actualWarlockLevel:12);
                    B(Reject(()=>catalog.Owned(missing,"origin",d.Id),E.Code.MissingPrerequisite),d.Id+" required pact remains mandatory");
                }
                Activation(B,d);
            }
        }
        catalog=new E(Invocations.Select(id=>Fixture(id,true)),"CastSpellWarlock","Charisma");
        ArmorAndOwnership(B);Unknown(B);Bindings(M,game,types);
        Console.WriteLine($"Stage13.5F: {behavior} executed production behavior checks; {audit} installed-definition/compiled audits; no Unity/native mutation execution.");
    }
    private static void Activation(Action<bool,string> check,E.Definition d)
    {
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})
        {
            var r=Resources(mode);var owner=Owner(d);var before=r.Standard.RemainingPoints;var pactBefore=r.Pact.Remaining;
            if(d.Spell!=null)
            {
                var ledger=new E.PaidLedger(catalog,owner,r);var action=Cast(d,circle:d.Type==E.Kind.PaidSpell?5:d.Circle);
                var domain=ledger.Route(action);var result=ledger.Commit(action);
                check(domain==(d.Type==E.Kind.PaidSpell?E.ResourceDomain.Pact:d.Type==E.Kind.AtWillSpell?E.ResourceDomain.AtWillInvocation:E.ResourceDomain.LimitedInvocation) && result.Allowed,d.Id+" typed "+domain+" route, "+mode);
                check(ledger.Commit(action).Allowed && r.Pact.Remaining==pactBefore-(d.ConsumesSlot?1:0) && r.Standard.RemainingPoints==before,d.Id+" repeat action cannot double-debit or reinterpret free casting as SP/Pact cost");
                check(Reject(()=>ledger.Commit(new E.Activation("cast","other","origin",d.Id,d.Spell,action.Circle,P.Source.Automatic)),E.Code.ConflictingAction),d.Id+" conflicting action identity rejected");
                if(d.Type==E.Kind.AtWillSpell)
                {
                    check(ledger.Commit(Cast(d,"second")).Allowed && !ledger.Used(d.Id),d.Id+" at-will remains repeatable without resource spend");
                    var empty=Resources(mode);for(var c=1;c<=9;c++)for(var i=0;empty.Standard.CanSpend(c);i++)empty.Standard.Spend("drain"+c+":"+i,c);
                    for(var i=0;empty.Pact.Remaining>0;i++)empty.Pact.Spend("pact"+i,P.Source.Pact,empty.Pact.Current.Circle);
                    var free=new E.PaidLedger(catalog,owner,empty);
                    check(free.Commit(Cast(d,"empty")).Allowed && empty.Pact.Remaining==0,d.Id+" both standard/Pact exhausted still permits native at-will without resources");
                }
                else check(Reject(()=>ledger.Commit(Cast(d,"second",circle:action.Circle)),E.Code.AlreadyUsed),d.Id+" native long-rest used flag preserved");
                ledger.Rest("short",false);check(ledger.Used(d.Id)==(d.Type!=E.Kind.AtWillSpell),d.Id+" short rest does not refill long-rest invocation charge");
                ledger.Rest("long",true);check(!ledger.Used(d.Id),d.Id+" long rest restores invocation use");
                ledger.Commit(Cast(d,"after-rest",circle:action.Circle));var debt=ledger.Used(d.Id);ledger.Rest("long",true);ledger.RefreshOwner(owner);
                check(ledger.Used(d.Id)==debt,d.Id+" repeated rest/owner refresh does not refill use debt");
                check(Reject(()=>ledger.CancelPrepared("after-rest"),E.Code.PendingAtomicJournal),d.Id+" no unsafe post-commit refund");
                ledger=new E.PaidLedger(catalog,owner,Resources(mode));action=Cast(d,circle:d.Type==E.Kind.PaidSpell?5:d.Circle);ledger.Prepare(action);ledger.CancelPrepared(action.Operation);
                check(Reject(()=>ledger.Commit(action),E.Code.ClosedSelection) && !ledger.Used(d.Id),d.Id+" safe prepared cancellation does not grant or spend");
                check(Reject(()=>ledger.Prepare(Cast(d,"")),E.Code.MissingActionIdentity),d.Id+" missing action identity rejected before expense");
            }
            else
            {
                var powers=d.Features.Where(f=>f.Type=="FeatureDefinitionPower").ToArray();
                if(powers.Length==0)
                {
                    var plan=catalog.PrepareNative(owner,"origin",d.Id,"native");
                    check(plan.Domain==(d.RequiresWarlock?E.ResourceDomain.NativeSlotTrigger:d.Type==E.Kind.Choice?E.ResourceDomain.Choice:E.ResourceDomain.NativeFeature),d.Id+" native feature/choice/slot-trigger identity, never ordinary spell");
                    check(Reject(plan.Commit,E.Code.PendingAtomicJournal),d.Id+" native mutation remains blocked, backend plan is not a native grant");
                }
                foreach(var power in powers)
                {
                    var proof=new E.NativePowerProof(owner.Hero,power.Id,d.Feature,"actual-native-pool",3,power.Cost,true);
                    var plan=catalog.PrepareNative(owner,"origin",d.Id,"native",proof);
                    check(plan.Domain==E.ResourceDomain.NativePower && plan.Power==power && plan.Proof==proof && power.Recharge!=null,d.Id+" actual power/uses/cost/recharge/action metadata retained");
                    check(Reject(plan.Commit,E.Code.PendingAtomicJournal) && r.Standard.RemainingPoints==before && r.Pact.Remaining==pactBefore,d.Id+" native power activation plans never manufacture spell cost or resources");
                    check(Reject(()=>catalog.PrepareNative(owner,"origin",d.Id,"native",new E.NativePowerProof("wrong",power.Id,d.Feature,"pool",3,1,true)),E.Code.WrongOwner),d.Id+" native power owner GUID mismatch rejected");
                    check(Reject(()=>catalog.PrepareNative(owner,"origin",d.Id,""),E.Code.MissingActionIdentity),d.Id+" native activation requires action identity before mutation");
                    if(power.Recharge!="AtWill")check(Reject(()=>catalog.PrepareNative(owner,"origin",d.Id,"native",new E.NativePowerProof(owner.Hero,power.Id,d.Feature,"pool",0,1,true)),E.Code.Exhausted),d.Id+" exhausted native power pool stays exhausted");
                }
            }
        }
    }
    private static void ArmorAndOwnership(Action<bool,string> check)
    {
        var armor=catalog.Require("ArmorOfShadows");check(armor.Type==E.Kind.AtWillSpell && armor.Spell=="MageArmorInvocationArmorShadows","ArmorOfShadows exact native at-will spell now classified");
        check(Reject(()=>catalog.Owned(Owner(armor,casting:false),"origin",armor.Id),E.Code.MissingRepertoire),"ArmorOfShadows cannot invent missing casting access even for Warlock owner");
        var wrongClass=new E.Owner("F-hero",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("origin",E.Origin.Warlock,armor.Id)},actualWarlockLevel:0);
        check(Reject(()=>catalog.Owned(wrongClass,"origin",armor.Id),E.Code.MissingPrerequisite),"class-origin claim cannot override explicit proof of zero actual Warlock levels");
        var d=catalog.Require("DevilsSight");var classClaim=new E.Claim("class",E.Origin.Warlock,d.Id);var owner=Owner(d,claims:new[]{classClaim});
        var tx=new E.AdeptSelection(catalog,owner,"feat");check(Reject(()=>tx.Select(d.Id),E.Code.DuplicateOrigin),"native duplicate choice blocked without confirmed additional-origin evidence");
        owner=new E.Owner(owner.Hero,20,20,"CastSpellWarlock","Charisma",17,true,new[]{classClaim},actualWarlockLevel:12,additionalOrigins:new[]{new E.Claim("feat",E.Origin.EldritchAdept,d.Id)});
        tx=new E.AdeptSelection(catalog,owner,"feat");tx.Select(d.Id);var claims=tx.Commit();var both=Owner(d,claims:claims);
        check(claims.Count==2 && catalog.EffectiveGrants(both).Count==1 && catalog.RemoveAdept(both,"feat",true).Single().Id=="class","confirmed class+feat origins merge effective grant; class survives feat removal");
        var noClaims=Owner(armor,claims:Array.Empty<E.Claim>());tx=new E.AdeptSelection(catalog,noClaims,"fresh");tx.Select(armor.Id);tx.RefreshOwner(noClaims);
        check(Reject(()=>tx.Select(armor.Id,0),E.Code.StaleOwner) && Reject(()=>tx.Commit(),E.Code.MissingPrerequisite),"owner refresh invalidates pending selection and stale revision");
        tx.Select(armor.Id,tx.Revision);check(tx.Commit().Count==1,"fresh owner proof permits new feat selection");
        var hidden=catalog.Require("InvocationStasis");
        check(catalog.Owned(Owner(hidden),"origin",hidden.Id)==hidden,"hiding an invocation removes new UI selection, not a legitimate confirmed grant");
        var book=catalog.Require("BookAncientSecrets");var replacementChoice=catalog.BeginReplacement(Owner(armor),Owner(armor,claims:Array.Empty<E.Claim>()),"origin",true);
        replacementChoice.Select(book.Id);foreach(var pool in book.Choices.Pools)replacementChoice.Choose(pool.Id,pool.Allowed.Take(pool.Amount).Select(s=>s.Id));
        check(replacementChoice.Commit().Single().Invocation==book.Id,"choice-bearing replacement uses full existing native-budget planner before any feature mutation");
        var ritualPaid=false;try{book.Choices.Route(book.Choices.Pools[0].Allowed[0].Id,true,true);}catch(A.Rejection rejection){ritualPaid=rejection.Diagnostic==A.Code.WrongOwner;}
        check(ritualPaid && book.Choices.Resource==A.Domain.NativeRitual,"Book of Ancient Secrets stays ritual-only, not a free ordinary known spell or shared paid grant");
        var pendingChoice=new E.AdeptSelection(catalog,Owner(book,claims:new[]{classClaim}),"pending-choice");pendingChoice.Select(book.Id);
        foreach(var pool in book.Choices.Pools)pendingChoice.Choose(pool.Id,pool.Allowed.Take(pool.Amount).Select(spell=>spell.Id));
        var duplicateRejected=Reject(()=>pendingChoice.Select(d.Id),E.Code.DuplicateOrigin);
        foreach(var pool in book.Choices.Pools)pendingChoice.Choose(pool.Id,pool.Allowed.Take(pool.Amount).Select(spell=>spell.Id));
        check(duplicateRejected && pendingChoice.Commit().Any(claim=>claim.Invocation==book.Id),"invalid duplicate selection does not poison prior legitimate pending choice budget");
        var bane=catalog.Require("ThiefFiveFates");owner=Owner(armor);var after=Owner(armor,claims:Array.Empty<E.Claim>());
        check(Reject(()=>catalog.ReplaceAdept(owner,after,"origin",bane.Id,false),E.Code.CleanupRequired),"replacement cannot bypass cleanup/journal evidence");
        var replacement=catalog.ReplaceAdept(owner,after,"origin",bane.Id,true);check(replacement.Single().Invocation==bane.Id,"valid managed replacement preserves origin, no native feature mutation");
        var invalid=new E.Owner(owner.Hero,20,0,null,null,0,true,Array.Empty<E.Claim>());
        check(Reject(()=>catalog.ReplaceAdept(owner,invalid,"origin",bane.Id,true),E.Code.MissingPrerequisite),"replacement revalidates native level prerequisite before mutation");
        // Keep the same authoritative coordinator and invocation definition debt across A -> B -> A.
        var ledger=new E.PaidLedger(catalog,Owner(armor),Resources(S.ResourceMode.SpellPoints));ledger.Commit(Cast(armor));ledger.RefreshOwner(Owner(bane));ledger.Commit(Cast(bane,"bane",circle:5));var remaining=ledger.Resources.Pact.Remaining;
        ledger.RefreshOwner(Owner(armor));ledger.RefreshOwner(Owner(bane));
        check(ledger.Used(bane.Id) && ledger.Resources.Pact.Remaining==remaining && Reject(()=>ledger.Commit(Cast(bane,"cycle",circle:5)),E.Code.AlreadyUsed),"feat replacement cycling never clears confirmed resource debt");
        var hexId="InvocationBurningHex";var signId="SignIllOmen";
        var beforeRemoval=new E.Owner("F-hero",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("feat",E.Origin.EldritchAdept,signId),new E.Claim("class-hex",E.Origin.Warlock,hexId)},actualWarlockLevel:12);
        var afterRemoval=new E.Owner("F-hero",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("class-hex",E.Origin.Warlock,hexId)},actualWarlockLevel:12);
        check(Reject(()=>catalog.PrepareRemoval(beforeRemoval,afterRemoval,"feat",true),E.Code.MissingPrerequisite),"removal revalidates surviving custom prerequisites against fresh post-removal owner proof");
        foreach(var id in new[]{"InvocationBurningHex","InvocationChillingHex","InvocationVexingHex","InvocationInexorableHex","InvocationDevouringBlade"})
        {
            var definition=catalog.Require(id);var missing=new E.Owner("F-hero",20,20,"CastSpellWarlock","Charisma",17,true,new[]{new E.Claim("origin",E.Origin.EldritchAdept,id)},actualWarlockLevel:12);
            check(Reject(()=>catalog.Owned(missing,"origin",id),E.Code.MissingPrerequisite),id+" custom validator cannot be waived by generic native-proof bool");
        }
    }
    private static void Unknown(Action<bool,string> check)
    {
        check(Fixture("ArmorOfShadows",changed:true).Type==E.Kind.Unsupported,"changed audited native grant identity fails closed");
        check(Fixture("InvocationBurningHex",unknownValidator:true).Type==E.Kind.Unsupported,"unverified native validator fails closed");
        var unknown=EldritchInvocationCatalog.Classify("Unknown",null,0,"Feature",1,null,null,false,false,Array.Empty<E.FeatureGrant>(),E.ValidatorRule.None);
        check(unknown.Type==E.Kind.Unsupported && unknown.RejectionReason!=null,"unknown definitions have specific rejection reason");
        var custom=EldritchInvocationCatalog.Classify("ArmorOfShadows","MageArmorInvocationArmorShadows",1,null,1,null,null,false,false,Array.Empty<E.FeatureGrant>(),E.ValidatorRule.None,custom:true);
        check(custom.Type==E.Kind.Unsupported && !custom.Selectable,"custom invocation pools do not enter native Adept candidate list");
        var rejected=false;try{_ =new E(new[]{new E.Definition("Invalid",E.Kind.AtWillSpell,"Shield",1,null,1,null,null,true,false)},"CastSpellWarlock","Charisma");}catch(ArgumentException){rejected=true;}
        check(rejected,"deliberately invalid at-will descriptor cannot turn ConsumesSpellSlot into free casting");
        check(!C.MixedGameplayReady,"mixed gameplay stays false; no new public setting/admission");
    }
    private static void Bindings(Action<bool,string> check,AssemblyDefinition game,TypeDefinition[] types)
    {
        bool Calls(MethodDefinition m,string type,string name)=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name==type && r.Name==name);
        var builders=types.Single(t=>t.Name=="InvocationsBuilders");
        foreach(var name in new[]{"BuildBurningHex","BuildChillingHex","BuildVexingHex","BuildInexorableHex","BuildDevouringBlade"})
        {
            var method=builders.Methods.Single(m=>m.Name==name);
            check(method.Body.Instructions.Any(i=>i.Operand is MethodReference r && (r.Name=="ValidateHex" || r.Name.StartsWith("<BuildDevouringBlade>g__Validate",StringComparison.Ordinal))),name+" installed validator delegate verified in compiled builder, not inferred from invocation title");
        }
        check(builders.Methods.Single(m=>m.Name=="BuildEldritchSmite").Body.Instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name=="ClassHolder" && f.Name=="Warlock"),"Eldritch Smite native ClassHolder.Warlock resource owner verified in compiled builder");
        var context=types.Single(t=>t.Name=="EldritchInvocationSourcesContext");
        check(Calls(context.Methods.Single(m=>m.Name=="CaptureInstalledDefinitions"),"EldritchInvocationCatalog","Classify"),"native capture uses same audited production classifier as fixture tests");
        var native=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacterHero").Methods.Single(m=>m.Name=="GrantInvocations");
        check(Calls(native,"FeatureDefinitionCastSpell","get_UniqueLevelSlots"),"actual native invocation construction chooses a real unique-slot repertoire, not selected class");
        var ctor=game.MainModule.Types.Single(t=>t.Name=="RulesetInvocation").Methods.Single(m=>m.IsConstructor && m.Parameters.Count==2);
        check(Calls(ctor,"RulesetSpellRepertoire","get_SpellCastingFeature"),"native constructor copies real repertoire feature; no repertoire invention for feats");
        var selection=types.Single(t=>t.Name=="AdeptSelection");
        check(Calls(selection.Methods.Single(m=>m.Name=="Select"),"Selection",".ctor") && Calls(selection.Methods.Single(m=>m.Name=="Commit"),"Selection","Commit"),"choice-bearing invocations use existing auxiliary Selection, not a second choice engine");
        check(types.Single(t=>t.Name=="CustomInvocationSubPanel").Methods.Single(m=>m.Name=="UpdateRelevantInvocations").HasBody && types.Single(t=>t.Name=="GuiInvocationDefinitionPatcher").NestedTypes.Any(),"actual candidate visibility/custom filter and native prerequisite patches retained");
    }
}
