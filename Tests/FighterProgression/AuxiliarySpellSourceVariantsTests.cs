using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using P = SolastaUnfinishedBusiness.Models.AuxiliarySpellSourcesPlan;
using G = SolastaUnfinishedBusiness.Models.AuxiliarySpellSourceGraphPlan;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;

// Actual blueprint graphs where present; synthetic conditional/multi-caster fixtures are labelled.
// Production graph walker, pool policy and existing Selection execute. Native game is not executed.
internal static class AuxiliarySpellSourceVariantsTests
{
    private static Dictionary<string,JsonElement> db;
    private static Dictionary<string,JsonElement> spellsByName;
    private static readonly R.Spell[] Options={new("Light",0),new("Sparkle",0),new("Shield",1),new("Identify",1),new("Slow",3)};
    private static bool Reject(Action action,P.Code code)
    {try{action();return false;}catch(P.Rejection ex){return ex.Diagnostic==code;}}
    private static string Ref(string value)=>value?.Split(':')[1];
    private static IEnumerable<string> Features(JsonElement d)=>d.TryGetProperty("features",out var f)?f.EnumerateArray().Select(x=>Ref(x.GetString())):
        d.TryGetProperty("featureUnlocks",out var u)?u.EnumerateArray().Select(x=>Ref(x.GetProperty("featureDefinition").GetString())):
        d.TryGetProperty("grantedFeature",out var g) && g.ValueKind!=JsonValueKind.Null?new[]{Ref(g.GetString())}:Array.Empty<string>();
    private static G.Node Node(string id)
    {
        var d=db[id];if(!d.TryGetProperty("featureSet",out var children))return new G.Node(id);
        return new G.Node(id,d.GetProperty("mode").GetString() switch{"Union"=>G.Mode.Union,"Exclusion"=>G.Mode.Exclusion,_=>G.Mode.Unsupported},children.EnumerateArray().Select(x=>Node(Ref(x.GetString()))));
    }
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    private static P.Source Source(string id,string root,P.Variant variant=null,P.Pool[] pools=null,string feature="CastFixture",string ability="Intelligence")=>new(
        id,P.Origin.Feat,root,feature,"Race",ability,null,P.Domain.NativeSlots,1,"LongRest","Selection",Options,
        pools??new[]{new P.Pool("choice",Options,2,P.ChoiceKind.CantripOrSpell,0,1,"NativeTag","FixtureList")},variant:variant);
    private static P.VariantState State(string root,string group=null,string branch=null,string condition=null,bool valid=true)=>new(new[]{root},
        group==null?null:new[]{new KeyValuePair<string,string>(group,branch)},condition==null?null:new[]{new KeyValuePair<string,bool>(condition,valid)});
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var behavior=0;var metadata=0;
        void B(bool ok,string name){behavior++;check(ok,"E-2 production behavior: "+name);}
        void M(bool ok,string name){metadata++;check(ok,"E-2 fixture/compiled audit: "+name);}
        db=new Dictionary<string,JsonElement>(StringComparer.Ordinal);spellsByName=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
        foreach(var folder in new[]{"OfficialBlueprints","UnfinishedBusinessBlueprints"})foreach(var file in Directory.GetFiles("Diagnostics/"+folder,"*.json",SearchOption.AllDirectories))
        {using var doc=JsonDocument.Parse(File.ReadAllText(file));var name=Path.GetFileNameWithoutExtension(file);var definition=doc.RootElement.Clone();db.TryAdd(name,definition);
            if(Path.GetFileName(Path.GetDirectoryName(file))=="SpellDefinition")spellsByName.TryAdd(name,definition);}
        M(spellsByName["Shield"].GetProperty("$type").GetString().StartsWith("SpellDefinition,",StringComparison.Ordinal),"typed SpellDefinition fixtures distinguish Shield spell from same-name item, not guessed by name alone");
        ActualGraphs(B,M); SharedBudgets(B); ConditionalFixtures(B); BindingFixtures(B); CrossSystems(B,M,mod); NativeBindings(M,game,mod);
        Console.WriteLine($"Stage13.5E-2: {behavior} executed production behavior checks; {metadata} fixture/compiled audits; conditional/multi-caster synthetic fixtures explicitly labelled; no Unity execution.");
    }
    private static void ActualGraphs(Action<bool,string> check,Action<bool,string> audit)
    {
        var shared=new List<string>();var reuse=new Dictionary<string,List<string>>();var sources=0;var archer=false;var complex=0;
        foreach(var item in db)
        {
            var type=item.Value.GetProperty("$type").GetString().Split(',')[0];
            if(type is not ("CharacterRaceDefinition" or "FeatDefinition" or "FeatDefinitionWithPrerequisites" or "FightingStyleDefinition" or "CharacterBackgroundDefinition" or "CharacterSubclassDefinition" or "InvocationDefinition" or "InvocationDefinitionCustom"))continue;
            var roots=Features(item.Value).ToArray();if(roots.Length==0)continue;
            var graph=G.Walk(item.Key,new G.Node("root",G.Mode.Union,roots.Select(Node)));
            var casts=graph.Where(o=>db.TryGetValue(o.Feature,out var d) && d.GetProperty("$type").GetString().StartsWith("FeatureDefinitionCastSpell,",StringComparison.Ordinal)).ToArray();
            var native=casts.Where(c=>db[c.Feature].GetProperty("spellCastingOrigin").GetString()=="Race").ToArray();
            if(native.Length>1 || native.Any(c=>c.Variant.Constrained))complex++;
            foreach(var occurrence in native)
            {
                sources++;var d=db[occurrence.Feature];var root=item.Key;var rows=d.GetProperty("slotsPerLevels").EnumerateArray().Select(r=>r.GetProperty("slots").EnumerateArray().Select(n=>n.GetInt32()).Concat(new int[9]).Take(9).ToArray()).ToArray();
                var list=Ref(d.GetProperty("spellListDefinition").GetString());
                var spells=db[list].GetProperty("spellsByLevel").EnumerateArray().SelectMany(r=>r.GetProperty("spells").EnumerateArray().Select(x=>new R.Spell(Ref(x.GetString()),r.GetProperty("level").GetInt32()))).GroupBy(x=>x.Id).Select(g=>g.First()).ToArray();
                var source=new P.Source(root+":"+occurrence.Variant.Id,type=="CharacterRaceDefinition"?P.Origin.Race:type=="FightingStyleDefinition"?P.Origin.Style:type=="CharacterSubclassDefinition"?P.Origin.Subclass:type=="CharacterBackgroundDefinition"?P.Origin.Background:P.Origin.Feat,
                    root,occurrence.Feature,"Race",d.GetProperty("spellcastingAbility").GetString(),type=="CharacterSubclassDefinition"?"Fighter":null,P.Domain.NativeSlots,1,d.GetProperty("slotsRecharge").GetString(),d.GetProperty("spellKnowledge").GetString(),spells,
                    slots:Enumerable.Range(0,20).Select(i=>rows.Length==0?new int[9]:rows[Math.Min(i,rows.Length-1)]),variant:occurrence.Variant);
                source.ValidateOwner(root,occurrence.Feature,source.Ability,null,20,State(root));
                var tx=new P.Selection(new P(new[]{source}),new[]{source.Id},variants:State(root));
                check(tx.Commit().Count==0 && source.Variant.Path.Last()==source.CastingFeature,root+" actual graph: typed path and explicit native feature, no extra choices fabricated");
                check(source.NativeSlotRows.Count==20 && source.CalculatorInput().OwnerClassId==(type=="CharacterSubclassDefinition"?"Fighter":null) && source.RestoresOn("LongRest",false)==(source.Recharge=="LongRest" || source.Recharge=="ShortRest"),root+" actual graph: ability/origin/native slot/recharge policy preserved");
                if(!reuse.TryGetValue(occurrence.Feature,out var owners))reuse[occurrence.Feature]=owners=new();owners.Add(root);
                if(root=="MartialArcaneArcher")
                {archer=true;check(!occurrence.Variant.Constrained && graph.Any(o=>o.Feature=="FeatureSetMartialArcaneArcherArcaneLore"),"actual ArcaneArcher: independent skill exclusion does not condition cantrip source");}
            }
            foreach(var o in graph.Where(o=>db.TryGetValue(o.Feature,out var definition) && definition.TryGetProperty("poolType",out var p) && p.GetString()=="CantripOrSpell"))shared.Add(o.Feature);
        }
        audit(sources==60 && complex==0 && archer,"available registered snapshots:60 Race-origin casting root occurrences;0 conditional/multiple-caster spell branches; ArcaneArcher independent skill exclusion");
        var reused=reuse.Where(p=>p.Value.Count>1).ToArray();
        audit(reused.Length==2 && reuse["CastSpellElfHigh"].OrderBy(x=>x).SequenceEqual(new[]{"ElfHigh","RaceHalfElfHigh"}) &&
            reuse["CastSpellDarkelfMagic"].OrderBy(x=>x).SequenceEqual(new[]{"RaceDarkelf","RaceHalfElfDark"}),"exact reused casts: High Elf/Half Elf High and Darkelf/Half Elf Dark roots");
        foreach(var pair in reused)
        {
            var a=Source("source:"+pair.Value[0],pair.Value[0],feature:pair.Key);var b=Source("source:"+pair.Value[1],pair.Value[1],feature:pair.Key);
            var journal=new P.Selection(new P(new[]{a,b}),new[]{a.Id,b.Id},variants:new P.VariantState(pair.Value));
            journal.Select(a.Id,"choice",new[]{"Light","Shield"});journal.Select(b.Id,"choice",new[]{"Light","Shield"});
            check(journal.Commit().Count==2 && a.CastingFeature==b.CastingFeature && a.ChoiceOrigin!=b.ChoiceOrigin,pair.Key+" actual shared ID: independent root origins with same spell retained");
        }
        var allShared=db.Where(p=>p.Value.TryGetProperty("poolType",out var pool) && pool.GetString()=="CantripOrSpell").Select(p=>p.Key).OrderBy(x=>x).ToArray();
        audit(allShared.SequenceEqual(new[]{"PointPoolBardMagicalSecrets10","PointPoolBardMagicalSecrets14","PointPoolBardMagicalSecrets18","PointPoolCollegeLoreAdditionalMagicalSecrets"}),"exact installed CantripOrSpell inventory: four Bard Secrets definitions, no observed auxiliary shared pool");
        foreach(var id in allShared)
        {
            var d=db[id];var spells=db[Ref(d.GetProperty("spellListOverride").GetString())].GetProperty("spellsByLevel").EnumerateArray().SelectMany(r=>r.GetProperty("spells").EnumerateArray().Select(v=>new R.Spell(Ref(v.GetString()),r.GetProperty("level").GetInt32()))).ToArray();
            var pool=new P.Pool(id,spells,d.GetProperty("poolAmount").GetInt32(),P.ChoiceKind.CantripOrSpell,d.GetProperty("minSpellLevel").GetInt32(),d.GetProperty("maxSpellLevel").GetInt32(),d.GetProperty("extraSpellsTag").GetString(),Ref(d.GetProperty("spellListOverride").GetString()));
            check(pool.Shared && pool.Amount==2 && pool.Allowed.Any(s=>s.Circle==0) && pool.Allowed.Any(s=>s.Circle>0),id+" actual pool fixture: one shared2 budget; production Bard adapter remains separate");
        }
        foreach(var root in new[]{"FeatureSetPactTome","InvocationPactTome","BookAncientSecrets"})
        {
            var feature=db[root].TryGetProperty("grantedFeature",out var granted)?Ref(granted.GetString()):root;
            var graph=G.Walk(root,new G.Node("root",G.Mode.Union,new[]{Node(feature)}));
            var occurrence=graph.Single(o=>db.TryGetValue(o.Feature,out var d) && d.TryGetProperty("poolType",out var type) && type.GetString() is "Cantrip" or "Spell");
            var d=db[occurrence.Feature];var list=Ref(d.GetProperty("spellListOverride").GetString());
            var spells=db[list].GetProperty("spellsByLevel").EnumerateArray().SelectMany(r=>r.GetProperty("spells").EnumerateArray().Select(v=>new R.Spell(Ref(v.GetString()),r.GetProperty("level").GetInt32()))).GroupBy(s=>s.Id).Select(g=>g.First()).ToArray();
            var ritual=d.GetProperty("ritualOnly").GetBoolean();
            var pool=new P.Pool(occurrence.Feature,spells,d.GetProperty("poolAmount").GetInt32(),d.GetProperty("poolType").GetString()=="Cantrip"?P.ChoiceKind.Cantrip:P.ChoiceKind.Spell,
                d.GetProperty("minSpellLevel").GetInt32(),d.GetProperty("maxSpellLevel").GetInt32(),d.GetProperty("extraSpellsTag").GetString(),list,ritual,
                spells.Where(s=>spellsByName[s.Id].GetProperty("ritual").GetBoolean()).Select(s=>s.Id),variant:occurrence.Variant);
            var source=new P.Source(root+":"+occurrence.Variant.Id,P.Origin.Invocation,root,occurrence.Feature,"Attachment","Charisma","Warlock",
                ritual?P.Domain.NativeRitual:P.Domain.AttachedClass,1,"NativeClass","ChoicePool",pool.Allowed,new[]{pool},castingFeature:"CastSpellWarlock",variant:occurrence.Variant);
            var journal=new P.Selection(new P(new[]{source}),new[]{source.Id});journal.Select(source.Id,pool.Id,pool.Allowed.Take(pool.Amount).Select(s=>s.Id));
            check(journal.Commit().Values.Single().Count==pool.Amount && pool.Amount==(ritual?2:3),root+" actual graph retains independent pool origin and native award count");
            check(pool.Variant.Path.Last()==pool.Id && (!ritual || pool.Allowed.All(s=>s.Circle==1 && spellsByName[s.Id].GetProperty("ritual").GetBoolean())),root+" actual graph preserves ritual/list/circle constraints and attached Warlock feature");
        }
    }
    private static void SharedBudgets(Action<bool,string> check)
    {
        foreach(var chosen in new[]{new[]{"Light","Sparkle"},new[]{"Shield","Identify"},new[]{"Light","Shield"}})
        {
            var source=Source("fixture-shared","FixtureFeat");var registry=new P(new[]{source});var tx=new P.Selection(registry,new[]{source.Id});
            tx.Select(source.Id,"choice",chosen);var result=tx.Commit();
            check(result.Values.Single().Count==2 && ReferenceEquals(result,tx.Commit()),"synthetic auxiliary shared pool: "+string.Join("+",chosen)+" spends total2, repeated commit idempotent");
        }
        var a=Source("A","FeatA");var b=Source("B","FeatB");var reg=new P(new[]{a,b});
        foreach(var choice in new[]{Array.Empty<string>(),new[]{"Light"},new[]{"Light","Shield","Identify"},new[]{"Light","Light"},new[]{"Light","Slow"},new[]{"Light","Unlisted"}})
        {var tx=new P.Selection(reg,new[]{a.Id});check(Reject(()=>tx.Select(a.Id,"choice",choice),P.Code.InvalidChoice),"shared pool rejects insufficient/excess/duplicate/wrong-circle/list: "+string.Join(",",choice));}
        var ritual=new P.Pool("ritual",Options,1,P.ChoiceKind.CantripOrSpell,0,1,"RitualTag","ListOverride",true,new[]{"Identify"});
        check(ritual.Allowed.Select(s=>s.Id).SequenceEqual(new[]{"Identify"}) && ritual.ExtraSpellsTag=="RitualTag" && ritual.RitualOnly,"ritual-only/list override/min/max metadata filters shared budget");
        var zero=new P.Pool("native-zero",Options,2,P.ChoiceKind.CantripOrSpell,0,0);
        check(zero.NativeMaximum==0 && zero.Maximum==9 && zero.Allowed.Any(s=>s.Circle==3),"installed native zero-maximum convention preserved, not an invented cantrip-only pool");
        var confirmed=new P.Selection(reg,new[]{a.Id});confirmed.Select(a.Id,"choice",new[]{"Light","Shield"});var old=confirmed.Commit();
        var editConfirmed=new P.Selection(reg,new[]{a.Id},old);
        check(Reject(()=>editConfirmed.Select(a.Id,"choice",new[]{"Sparkle","Identify"}),P.Code.PendingAtomicJournal),"confirmed acquisitions cannot be freely replaced without installed policy/native journal");
        var pending=new P.Selection(reg,new[]{a.Id});pending.Select(a.Id,"choice",new[]{"Light","Shield"});pending.Select(a.Id,"choice",new[]{"Sparkle","Identify"});
        check(pending.Commit().Values.Single().SequenceEqual(new[]{"Sparkle","Identify"}),"pending reselection replaces preview within same budget, no extra award");
        var later=new P.Selection(reg,new[]{b.Id},old);later.Select(b.Id,"choice",new[]{"Light","Shield"});later.Back(b.Id);
        check(later.Preview().Single().Key==old.Single().Key,"Back on new source preserves confirmed other feat/race origins");
        later.Select(b.Id,"choice",new[]{"Light","Shield"});later.Reset();
        check(later.Preview().Count==1 && later.Cancel().Count==1,"Reset and Cancel preserve confirmed origins without granting pending choices");
        check(Reject(()=>new P.Selection(reg,new[]{"missing"}),P.Code.UnknownSource),"unknown/stale source registry ID rejected");
        check(Reject(()=>new P.Selection(reg,new[]{a.Id,b.Id}).Select("A","foreign-pool",new[]{"Light"}),P.Code.InvalidChoice),"unknown pool rejected");
        var pools=new[]{new P.Pool("one",Options,1,P.ChoiceKind.CantripOrSpell),new P.Pool("two",Options,1,P.ChoiceKind.CantripOrSpell)};
        var many=Source("many","FeatMany",pools:pools);var journal=new P.Selection(new P(new[]{many}),new[]{many.Id});journal.Select(many.Id,"one",new[]{"Light"});
        check(Reject(()=>journal.Select(many.Id,"two",new[]{"Light"}),P.Code.InvalidChoice),"same spell cannot satisfy two independent rewards of one source");
        journal.Select(many.Id,"two",new[]{"Shield"});check(journal.Commit().Count==2,"distinct pools retain independent budgets");
        var invalid=old.ToDictionary(x=>x.Key,_=>(IReadOnlyList<string>)new[]{"Slow","Light"});
        check(Reject(()=>new P.Selection(reg,new[]{a.Id},invalid).Commit(),P.Code.InvalidChoice),"invalid confirmed circle cannot bypass managed final validation");
    }
    private static void ConditionalFixtures(Action<bool,string> check)
    {
        var graph=G.Walk("FixtureRace",new G.Node("Choice",G.Mode.Exclusion,new[]{new G.Node("CastINT"),new G.Node("CastWIS")}));
        var leaves=graph.Skip(1).ToArray();var group=leaves[0].Variant.Branches.Single().Key;
        var a=Source("race-INT","FixtureRace",leaves[0].Variant,feature:"CastINT");var b=Source("race-WIS","FixtureRace",leaves[1].Variant,feature:"CastWIS",ability:"Wisdom");
        var c=Source("other","OtherFeat");var reg=new P(new[]{a,b,c});
        P.VariantState Proof(string branch)=>new(new[]{"FixtureRace","OtherFeat"},new[]{new KeyValuePair<string,string>(group,branch)});
        var tx=new P.Selection(reg,new[]{a.Id,b.Id,c.Id},variants:Proof("0:CastINT"));
        tx.Select(c.Id,"choice",new[]{"Light","Shield"});tx.Select(a.Id,"choice",new[]{"Light","Shield"},tx.Revision);
        check(Reject(()=>tx.Select(b.Id,"choice",new[]{"Light","Shield"},tx.Revision),P.Code.InactiveVariant),"synthetic conditional race: inactive branch cannot grant second caster");
        var revision=tx.Revision;tx.SetVariants(Proof("0:CastWIS"));
        check(tx.Preview().Count==1 && Reject(()=>tx.Select(b.Id,"choice",new[]{"Light","Shield"},revision),P.Code.StaleSelection),"variant change invalidates dependent choices, preserves other origin and rejects stale UI revision");
        tx.Select(b.Id,"choice",new[]{"Light","Shield"},tx.Revision);var committed=tx.Commit();
        check(committed.Count==2 && b.Ability=="Wisdom" && a.Ability=="Intelligence","exclusive variant commit grants only chosen source with own ability");
        check(Reject(()=>new P.Selection(reg,new[]{a.Id,b.Id},variants:State("FixtureRace",group,"invalid")),P.Code.InvalidChoice),"unknown branch selection rejected");
        check(Reject(()=>new P.VariantState(new[]{"FixtureRace"},new[]{new KeyValuePair<string,string>(group,"0:CastINT"),new KeyValuePair<string,string>(group,"0:CastWIS")}),P.Code.InvalidChoice),"invalid double-branch proof rejected");
        check(Reject(()=>new P.Selection(reg,new[]{a.Id,b.Id}),P.Code.PendingAtomicJournal),"unbound native branch never defaults to both casters");
        tx=new P.Selection(reg,new[]{a.Id,b.Id,c.Id},variants:Proof("0:CastINT"));tx.Select(a.Id,"choice",new[]{"Light","Shield"},tx.Revision);tx.SetVariants(Proof("0:CastWIS"));tx.Reset();
        check(Reject(()=>tx.Select(b.Id,"choice",new[]{"Light","Shield"},tx.Revision),P.Code.InactiveVariant),"Reset restores initial variant and clears dependent selections");
        var confirmed=new P.Selection(reg,new[]{a.Id,b.Id},variants:State("FixtureRace",group,"0:CastINT"));confirmed.Select(a.Id,"choice",new[]{"Light","Shield"},confirmed.Revision);var old=confirmed.Commit();
        var next=new P.Selection(reg,new[]{a.Id,b.Id},old,State("FixtureRace",group,"0:CastINT"));
        check(Reject(()=>next.SetVariants(State("FixtureRace",group,"0:CastWIS")),P.Code.PendingAtomicJournal),"confirmed branch change requires native rollback, never silently migrates saves");
        var condition=G.Walk("FixtureFeat",new G.Node("Conditional",G.Mode.Conditional,new[]{new G.Node("Cast")},"NativePredicateFixture")).Last().Variant;
        var conditional=Source("conditional","FixtureFeat",condition);reg=new P(new[]{conditional});
        check(Reject(()=>new P.Selection(reg,new[]{conditional.Id},variants:State("FixtureFeat")),P.Code.UnverifiedCondition),"conditional feature requires verified predicate proof");
        var inactive=new P.Selection(reg,new[]{conditional.Id},variants:State("FixtureFeat",condition:"NativePredicateFixture",valid:false));
        check(inactive.Commit().Count==0,"false native predicate creates no casting choices");
        var active=new P.Selection(reg,new[]{conditional.Id},variants:State("FixtureFeat",condition:"NativePredicateFixture"));active.Select(conditional.Id,"choice",new[]{"Light","Shield"},active.Revision);
        check(active.Commit().Count==1,"true predicate grants only its registered variant");
        var unknown=G.Walk("UnknownRoot",new G.Node("Unknown",G.Mode.Unsupported,new[]{new G.Node("Cast")})).Last().Variant;
        check(Reject(()=>new P.Selection(new P(new[]{Source("unknown","UnknownRoot",unknown)}),new[]{"unknown"}),P.Code.UnsupportedSource),"unknown ancestry/conditional mode fails closed");
        var union=G.Walk("FixtureMultiFeat",new G.Node("Union",G.Mode.Union,new[]{new G.Node("CastOne"),new G.Node("CastTwo")})).Skip(1).ToArray();
        var sources=union.Select((o,i)=>Source("multi"+i,"FixtureMultiFeat",o.Variant,feature:o.Feature,ability:i==0?"Intelligence":"Wisdom")).ToArray();reg=new P(sources);
        var multiple=new P.Selection(reg,sources.Select(s=>s.Id),variants:State("FixtureMultiFeat"));foreach(var source in sources)multiple.Select(source.Id,"choice",new[]{"Light","Shield"});
        check(multiple.Commit().Count==2 && sources.Select(s=>s.Variant.Id).Distinct().Count()==2,"synthetic legitimate Union grants both independent native casts, distinct paths/origins");
    }
    private static void BindingFixtures(Action<bool,string> check)
    {
        var poolGraph=G.Walk("FixturePoolBranches",new G.Node("root",G.Mode.Union,new[]{new G.Node("Cast"),new G.Node("PoolChoice",G.Mode.Exclusion,new[]{new G.Node("PoolA"),new G.Node("PoolB")})}));
        var poolVariants=poolGraph.Where(o=>o.Feature is "PoolA" or "PoolB").ToArray();var group=poolVariants[0].Variant.Branches.Single().Key;
        var onlyCast=poolGraph.Single(o=>o.Feature=="Cast");
        var pools=poolVariants.Select(v=>new P.Pool(v.Feature,Options,2,P.ChoiceKind.CantripOrSpell,0,1,variant:v.Variant)).ToArray();
        var single=Source("pool-branches","FixturePoolBranches",onlyCast.Variant,pools);
        var journal=new P.Selection(new P(new[]{single}),new[]{single.Id},variants:State("FixturePoolBranches",group,"0:PoolA"));
        journal.Select(single.Id,"PoolA",new[]{"Light","Shield"},journal.Revision);journal.SetVariants(State("FixturePoolBranches",group,"0:PoolB"));
        check(journal.Preview().Count==0 && Reject(()=>journal.Select(single.Id,"PoolA",new[]{"Light","Shield"},journal.Revision),P.Code.InactiveVariant),"conditional pool change invalidates dependent budget even when casting source stays active");
        journal.Select(single.Id,"PoolB",new[]{"Light","Shield"},journal.Revision);check(journal.Commit().Count==1,"only selected pool branch consumes shared N, not both branches");
        check(Reject(()=>Source("wrong-pool-owner","OtherRoot",pools:pools),P.Code.WrongOwner),"pool variant cannot be attached to another source root");
        var orderA=G.Walk("Stable",new G.Node("Root",G.Mode.Exclusion,new[]{new G.Node("CastA"),new G.Node("CastB")}));
        var orderB=G.Walk("Stable",new G.Node("Root",G.Mode.Exclusion,new[]{new G.Node("CastB"),new G.Node("CastA")}));
        check(orderA.Single(o=>o.Feature=="CastA").Variant.Id==orderB.Single(o=>o.Feature=="CastA").Variant.Id &&
            orderA.Single(o=>o.Feature=="CastA").Variant.Branches.Single().Value==orderB.Single(o=>o.Feature=="CastA").Variant.Branches.Single().Value,
            "source/choice IDs stable under title/localization sibling reordering; branch ID uses definition plus same-definition occurrence, not UI index");

        var graph=G.Walk("FixtureTags",new G.Node("Union",G.Mode.Union,new[]{new G.Node("CastA"),new G.Node("CastB"),new G.Node("PoolA"),new G.Node("PoolB")}));
        var casts=graph.Where(o=>o.Feature.StartsWith("Cast",StringComparison.Ordinal)).ToArray();var pool=graph.Single(o=>o.Feature=="PoolA");
        check(G.BindPool(pool,casts,c=>c=="CastA"?"TagA":"TagB","TagA").Feature=="CastA","two native pool tags bind explicit casting features");
        check(Reject(()=>G.BindPool(pool,casts,_=>"same","same"),P.Code.AmbiguousTag),"merged identical tags do not guess casting owner");
        check(Reject(()=>G.BindPool(pool,casts,_=>null,""),P.Code.AmbiguousTag),"multiple casters without native tag evidence fail before grant");
        var repeated=G.Walk("RepeatedRoot",new G.Node("Root",G.Mode.Union,new[]{new G.Node("CastA"),new G.Node("CastA"),new G.Node("Pool")}));
        check(Reject(()=>G.BindPool(repeated.Single(o=>o.Feature=="Pool"),repeated.Where(o=>o.Feature=="CastA"),_=>"same","same"),P.Code.AmbiguousTag),"repeated casting occurrences are siblings, not guessed owner scopes by occurrence ordinal");
        var foreign=G.Walk("OtherRoot",new G.Node("CastA"));
        check(Reject(()=>G.BindPool(pool,foreign,_=>"TagA","TagA"),P.Code.UnknownSource),"same casting ID/tag from another root cannot own this pool");
        var shared=new P.Pool("shared",Options,2,P.ChoiceKind.CantripOrSpell,tag:"Merged");
        var spell=new P.Pool("spell",Options,1,P.ChoiceKind.Spell,tag:"Merged");
        var cantrip=new P.Pool("cantrip",Options,2,P.ChoiceKind.Cantrip,tag:"Merged");
        var split=new[]{new KeyValuePair<string,P.Pool>("A",spell),new KeyValuePair<string,P.Pool>("A",cantrip)};
        P.ValidateNativeTags(split);check(true,"native separate spell/cantrip pools may share a tag when resource maps are distinct");
        check(Reject(()=>P.ValidateNativeTags(split.Concat(new[]{new KeyValuePair<string,P.Pool>("B",shared)})),P.Code.AmbiguousTag),"shared pool overlaps both native maps; reject merged source rewards before selections");
        check(Reject(()=>P.ValidateNativeTags(new[]{new KeyValuePair<string,P.Pool>("A",spell),new KeyValuePair<string,P.Pool>("B",spell)}),P.Code.AmbiguousTag),"two independent source rewards cannot reuse indistinguishable native tag");
        check(Reject(()=>P.ValidateNativeTags(new[]{new KeyValuePair<string,P.Pool>("A",new P.Pool("empty",Options,1,false))}),P.Code.PendingAtomicJournal),"empty native execution tag requires journal, never guessed from selected class");
        var nested=G.Walk("FixtureNested",new G.Node("Root",G.Mode.Union,new[]{new G.Node("Local",G.Mode.Union,new[]{new G.Node("CastA"),new G.Node("Pool")}),new G.Node("CastB")}));
        check(G.BindPool(nested.Single(o=>o.Feature=="Pool"),nested.Where(o=>o.Feature.StartsWith("Cast",StringComparison.Ordinal)),_=>null,"").Feature=="CastA","nearest explicit child scope binds pool without ability/list inference");
    }
    private static void CrossSystems(Action<bool,string> check,Action<bool,string> audit,AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();var full=new[]{1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
        var calc=new C("E2-three-class-fixture",new[]{new C.ClassProfile("Fighter",true,null),new C.ClassProfile("Sorcerer",true,new C.CastingProfile("CastSorcerer",C.CasterKind.Full,1,true,full)),new C.ClassProfile("Rogue",true,null)});
        var inputs=new[]{new C.ClassInput("Fighter",6,true),new C.ClassInput("Sorcerer",8,true),new C.ClassInput("Rogue",6,true)};
        var source=Source("aux","FixtureRace");var noAux=calc.Calculate(new C.Snapshot(20,inputs,inputs.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel))));
        var withAux=calc.Calculate(new C.Snapshot(20,inputs,inputs.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel)),new[]{source.CalculatorInput()}));
        check(withAux.IsValid && withAux.Classes.Count==3 && withAux.ResourceTableIndex==noAux.ResourceTableIndex && withAux.Input.ActualCharacterLevel==20,"three real classes plus auxiliary: no class/contribution/history inflation");
        var slots=CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="FeatureDefinitionCastSpellBuilder"),"SlotsByCasterLevel");
        var tables=new S.Tables(Enumerable.Range(1,20).Select(n=>slots.Skip(n*10).Take(9)),CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel"),CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="SpellPointsContext"),"SpellCostByLevel"));
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})
        {
            var budget=new S.Budget(tables,new S.Allocation(tables,withAux,mode));var before=budget.RemainingPoints;var count=budget.RemainingSlots(1);
            var tx=new P.Selection(new P(new[]{source}),new[]{source.Id});tx.Select(source.Id,"choice",new[]{"Light","Shield"});tx.Commit();source.Route("Shield",false);
            check(budget.RemainingPoints==before && budget.RemainingSlots(1)==count && source.RestoresOn("LongRest",false) && !source.RestoresOn("ShortRest",false),mode+" auxiliary choice/free resource route does not debit/refill shared class budget");
            check(Reject(()=>source.Route("Shield",true),P.Code.WrongOwner),mode+" native free source cannot charge guessed class repertoire");
        }
        check(!C.MixedGameplayReady,"old single-class/native paths keep mixed prototype OFF");
        audit(types.Single(t=>t.Name=="BardMagicalSecretsPlan").Methods.Any(m=>m.Name=="Steps") && types.Single(t=>t.Name=="EldritchInvocationSourcesPlan").NestedTypes.Any(t=>t.Name=="PaidLedger"),"existing Bard and paid-invocation production modules still compiled; complete earlier suites executed by runner");
    }
    private static void NativeBindings(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();bool Calls(MethodDefinition m,string owner,string name)=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name==owner && r.Name==name);
        var context=types.Single(t=>t.Name=="AuxiliarySpellSourcesContext");var generated=context.Methods.Concat(context.NestedTypes.SelectMany(Types).SelectMany(t=>t.Methods)).ToArray();
        check(generated.Any(m=>Calls(m,"AuxiliarySpellSourceGraphPlan","Walk")) && generated.Any(m=>Calls(m,"AuxiliarySpellSourceGraphPlan","BindPool")),"native capture executes same production path/tag planner as tests");
        check(Calls(context.Methods.Single(m=>m.Name=="Independent"),"DruidWildshapeContext","StandardResourceOwner"),"racial/Wild Shape native owner resolves original hero; no new form pool");
        check(Calls(context.Methods.Single(m=>m.Name=="ValidateRootChoice"),"Variant","Active"),"pending feat/style preflight verifies branch proof before grants");
        check(Calls(context.Methods.Single(m=>m.Name=="ValidateBeforeFinalize"),"AuxiliarySpellSourcesContext","Preflight") && Calls(context.Methods.Single(m=>m.Name=="Preflight"),"AuxiliarySpellSourcesPlan","ValidateNativeTags"),"native finalization retains preflight guard");
        check(game.MainModule.Types.Single(t=>t.Name=="FeatureDefinitionFeatureSet").Properties.Any(p=>p.Name=="Mode"),"native union/exclusion mode ABI exists");
    }
}
