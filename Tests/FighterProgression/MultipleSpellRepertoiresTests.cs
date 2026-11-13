using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using static SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using Caster = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;

// Production model/transactions are executed. Native adapters are inspected through Cecil only.
// Blueprint casting metadata + explicit final UB2014/2024 table fixtures, not a running game DB.
internal static class MultipleSpellRepertoiresTests
{
    private static StandardCasterResourcePlan.Tables resourceTables;
    private static readonly string[] Classes = { "Wizard", "Sorcerer", "Bard", "Cleric", "Druid", "Paladin", "Ranger", "Inventor" };
    private static bool Reject(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } catch (InvalidOperationException) { return true; } }
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static readonly int[] Full = {1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
    private static readonly int[] Bard2014 = {4,5,6,7,8,9,10,11,12,14,15,15,16,18,19,19,20,22,22,22};
    private static readonly int[] Sorcerer2014 = {2,3,4,5,6,7,8,9,10,11,12,12,13,13,14,14,15,15,15,15};
    private static readonly int[] Bard2024 = {4,5,6,7,9,10,11,12,14,15,16,16,17,17,18,18,19,20,21,22};
    private static readonly int[] Sorcerer2024 = {2,4,6,7,9,10,11,12,14,15,16,16,17,17,18,18,19,20,21,22};
    private static readonly int[] Ranger2024 = {2,3,4,5,6,6,7,7,9,9,10,10,11,11,12,12,14,14,15,15};
    private static readonly int[] Ranger2014 = {2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11};
    private static readonly int[] Prepared2024 = {4,5,6,7,9,10,11,12,14,15,16,16,17,17,18,18,19,20,21,22};
    private static readonly int[] Wizard2024 = {4,5,6,7,9,10,11,12,14,15,16,16,17,18,19,21,22,23,24,25};
    private static PlanData Metadata(string id)
    {
        var path = id == "Inventor" ? "Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellsInventor.json" :
            "Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpell"+id+".json";
        using var doc=JsonDocument.Parse(File.ReadAllText(path)); var d=doc.RootElement;
        return new PlanData(d.GetProperty("name").GetString(),d.GetProperty("spellcastingAbility").GetString(),
            d.GetProperty("spellListDefinition").GetString().Split(':')[1],d.GetProperty("spellKnowledge").GetString(),d.GetProperty("spellReadyness").GetString(),
            d.GetProperty("knownCantrips").EnumerateArray().Select(x=>x.GetInt32()).ToArray(),
            d.GetProperty("scribedSpells").EnumerateArray().Select(x=>x.GetInt32()).ToArray());
    }
    private record PlanData(string Feature,string Ability,string List,string Knowledge,string Readiness,int[] Cantrips,int[] Scribed);
    private static Profile Fixture(string id,bool rules2024=false,IEnumerable<Grant> extra=null)
    {
        var d=Metadata(id);
        var mode=Enum.Parse<Knowledge>(d.Knowledge);
        var prepared=d.Readiness!="Prepared"?Preparation.None:id is "Paladin" or "Inventor"?Preparation.AbilityPlusHalfLevel:Preparation.AbilityPlusLevel;
        int[] table=null;
        if(rules2024 && id is "Wizard" or "Cleric" or "Druid" or "Paladin")
        { prepared=Preparation.InstalledTable; table=id=="Wizard"?Wizard2024:id=="Paladin"?Ranger2024:Prepared2024; }
        var known=id switch {"Sorcerer"=>rules2024?Sorcerer2024:Sorcerer2014,"Bard"=>rules2024?Bard2024:Bard2014,
            "Ranger"=>rules2024?Ranger2024:Ranger2014,_=>new int[20]};
        var max=id is "Paladin" or "Inventor" or "Ranger"?Enumerable.Range(1,20).Select(n=>Full[(n+1)/2-1]).ToArray():Full;
        var spells=Enumerable.Range(0,10).SelectMany(circle=>Enumerable.Range(0,60).Select(n=>new Spell(id+"_"+circle+"_"+n,circle)))
            .Append(new Spell("SharedShield",1)).ToArray();
        var grants=(extra??Array.Empty<Grant>()).ToArray();
        var cantrips=Enumerable.Range(0,20).Select(i=>d.Cantrips[Math.Min(i,d.Cantrips.Length-1)]);
        return new Profile(id,d.Feature,d.Ability,d.List,mode,prepared,spells,max,cantrips,known,
            Enumerable.Range(1,20).Select(n=>mode==Knowledge.Selection && n>=(id=="Ranger"?3:2)?1:0),
            Enumerable.Range(0,20).Select(i=>i<d.Scribed.Length?d.Scribed[i]:0),table,grants);
    }
    private static Spell Available(Profile p,int original,IEnumerable<string> existing,bool cantrip=false) => p.List.Values.First(s=>
        !existing.Contains(s.Id) && p.Allows(s.Id,original,cantrip));
    private static void Complete(Transaction tx,bool replaceEarly=false)
    {
        foreach(var e in tx.Events)
        {
            var own=tx.Preview().Repertoires[tx.ClassId]; var occupied=own.Known.Concat(own.Cantrips).Concat(own.Bonus.Values.SelectMany(s=>s)).ToList();
            var removals=replaceEarly && e.Replacements>0 && own.Known.Count>0?new[]{own.Known.Last()}:Array.Empty<string>();
            var added=new List<string>();
            for(var i=0;i<e.Learned+removals.Length;i++) { var s=Available(tx.Definition,e.Original,occupied.Concat(added));added.Add(s.Id); }
            var cantrips=new List<string>();
            for(var i=0;i<e.Cantrips;i++) {var s=Available(tx.Definition,e.Original,occupied.Concat(cantrips),true);cantrips.Add(s.Id);}
            var bonuses=tx.Definition.Grants.Where(g=>g.Kind==GrantKind.BonusChoice && g.Original==e.Original).Select(g=>new KeyValuePair<string,IReadOnlyList<string>>(g.Id,
                g.Spells.Where(s=>!occupied.Concat(added).Concat(cantrips).Contains(s.Id) && s.Circle<=tx.Definition.MaximumCircles[e.Original-1]).Take(g.Choices).Select(s=>s.Id).ToArray())).ToArray();
            tx.Select(new Choice(e.Original,added,removals,cantrips,bonuses));
        }
    }
    private static HeroState Level(HeroState state,Profile profile,int actual,bool compressed=true,bool replace=false)
    { var tx=new Transaction(profile.ClassId+actual+":"+compressed,state,profile,actual,compressed);Complete(tx,replace);return tx.Commit(); }
    internal static void Run(Action<bool,string> check,AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        var slots = CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="FeatureDefinitionCastSpellBuilder"),"SlotsByCasterLevel");
        var points = CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel");
        var costs = CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="SpellPointsContext"),"SpellCostByLevel");
        resourceTables = new StandardCasterResourcePlan.Tables(Enumerable.Range(1,20).Select(n=>slots.Skip(n*10).Take(9)),points,costs);
        foreach(var modern in new[]{false,true})
        foreach(var id in Classes)
        {
            var p=Fixture(id,modern); var state=new HeroState(Array.Empty<Repertoire>());
            for(var n=1;n<=12;n++)
            {
                state=Level(state,p,n,true,true);var r=state.Repertoires[id];var effective=FighterProgressionPlan<int>.EffectiveLevel(n);
                check(r.ActualLevel==n && r.EffectiveLevel==effective && r.MaximumCircle==p.MaximumCircles[effective-1],$"Repertoire behavior {id}/{modern}/actual{n}: own effective level/access");
                check(r.Known.Count==(p.Mode==Knowledge.Selection?p.Known[effective-1]:p.Mode==Knowledge.Spellbook?p.Scribed.Take(effective).Sum():0),$"Repertoire behavior {id}/{modern}/actual{n}: exact original spell acquisition total");
                check(r.Cantrips.Count==p.Cantrips[effective-1] && r.History.All(h=>h.ClassId==id && h.FeatureId==p.FeatureId),$"Repertoire behavior {id}/{modern}/actual{n}: retained cantrips and owner-tagged history");
                check(r.Known.Distinct().Count()==r.Known.Count && r.History.Where(h=>h.Removed).All(h=>h.Source.EndsWith(":replacement",StringComparison.Ordinal)),
                    $"Repertoire behavior {id}/{modern}/actual{n}: no duplicate known spells; replacement provenance retained");
            }
            check(p.Ability==Metadata(id).Ability && p.ListId==Metadata(id).List && p.Mode.ToString()==Metadata(id).Knowledge,
                $"Repertoire blueprint metadata fixture {id}/{modern}: actual installed ability/list/knowledge retained");
        }
        Combinations(check); Transactions(check); BonusAndPrepared(check); WizardBooks(check); Isolation(check,mod);
    }
    private static void Combinations(Action<bool,string> check)
    {
        foreach(var modern in new[]{false,true})
        foreach(var pair in new[]{("Sorcerer","Wizard"),("Bard","Cleric"),("Druid","Cleric"),("Paladin","Sorcerer"),("Ranger","Inventor")})
        foreach(var secondCompressed in new[]{false,true})
        {
            var first=Fixture(pair.Item1,modern);var second=Fixture(pair.Item2,modern);var state=new HeroState(Array.Empty<Repertoire>());
            for(var n=1;n<=12;n++)state=Level(state,first,n);
            var protectedView=state.Repertoires[first.ClassId];
            for(var n=1;n<=8;n++)state=Level(state,second,n,secondCompressed,true);
            var a=state.Repertoires[first.ClassId];var b=state.Repertoires[second.ClassId];
            check(ReferenceEquals(a,protectedView) && a.ActualLevel+b.ActualLevel==20 && state.Repertoires.Count==2,
                $"Repertoire behavior combination {pair}/{modern}/{secondCompressed}: other class unchanged, actual total20");
            var casterProfiles=new[]{first,second}.Select(p=>new Caster.ClassProfile(p.ClassId,true,new Caster.CastingProfile(p.FeatureId,
                p.ClassId=="Inventor"?Caster.CasterKind.HalfRoundUp:p.ClassId is "Paladin" or "Ranger"?Caster.CasterKind.Half:Caster.CasterKind.Full,
                1,true,p.MaximumCircles))).ToArray();
            var calculator=new Caster("repertoire-combination-v1",casterProfiles);
            var result=calculator.Calculate(new Caster.Snapshot(20,new[]{new Caster.ClassInput(first.ClassId,12,true),new Caster.ClassInput(second.ClassId,8,secondCompressed)},
                Enumerable.Repeat(first.ClassId,12).Concat(Enumerable.Repeat(second.ClassId,8)),new[]{new Caster.AuxiliaryInput("Race",Caster.AuxiliaryRole.Race,1),new Caster.AuxiliaryInput("Feat",Caster.AuxiliaryRole.Feat,1)}));
            check(result.IsValid && result.ResourceTableIndex<=20 && result.Classes.Single(c=>c.ClassId==b.Definition.ClassId).MaximumSpellCircle==b.MaximumCircle,
                $"Repertoire behavior combination {pair}/{modern}/{secondCompressed}: shared capped resource index, independent access, race/feat no class contribution");
            foreach(var mode in new[]{StandardCasterResourcePlan.ResourceMode.Slots,StandardCasterResourcePlan.ResourceMode.SpellPoints})
            {
                var allocation=new StandardCasterResourcePlan.Allocation(resourceTables,result,mode);
                var budget=new StandardCasterResourcePlan.Budget(resourceTables,allocation);
                check(budget.Spend("cast-from-class-A",1) && budget.Spend("cast-from-class-B",2) &&
                    (mode==StandardCasterResourcePlan.ResourceMode.SpellPoints?budget.RemainingPoints==allocation.PointMaximum-5:budget.RemainingSlots(1)==3 && budget.RemainingSlots(2)==2),
                    $"Repertoire behavior resource integration {pair}/{modern}/{secondCompressed}/{mode}: both owners debit the same installed-table ledger");
                check(ReferenceEquals(a,protectedView) && b.MaximumCircle==second.MaximumCircles[b.EffectiveLevel-1],
                    $"Repertoire behavior resource integration {pair}/{mode}: spending/upcasting does not mutate spell origins/access");
            }
            check(a.Definition.Ability==first.Ability && b.Definition.Ability==second.Ability && a.Definition.ListId!=b.Definition.ListId,
                $"Repertoire behavior combination {pair}/{modern}/{secondCompressed}: separate casting attributes/lists");
            var prepared=b.Definition.PreparedLimit(b.EffectiveLevel,3,0);
            check(prepared==(b.Definition.PreparedRule==Preparation.None?0:b.Definition.PreparedRule==Preparation.InstalledTable?b.Definition.PreparedTable[b.EffectiveLevel-1]:
                3+(b.Definition.PreparedRule==Preparation.AbilityPlusHalfLevel?b.EffectiveLevel/2:b.EffectiveLevel)),
                $"Repertoire behavior combination {pair}/{modern}/{secondCompressed}: own preparation; INT/CHA/WIS not replaced by shared caster level");
            if(pair.Item2=="Wizard") check(b.MaximumCircle==(secondCompressed?6:4) && !b.Definition.Allows("Wizard_9_0",b.EffectiveLevel),
                "Repertoire behavior critical distinction: shared circle9 never permits low Wizard ninth-circle learning");
        }
        {
            var sorc=Fixture("Sorcerer");var wizard=Fixture("Wizard");
            var own=new Repertoire(sorc,1,true,new[]{"SharedShield"},new[]{"Sorcerer_0_0"});
            var state=new HeroState(new[]{own});var tx=new Transaction("overlap",state,wizard,1,true);
            var spells=new[]{"SharedShield"}.Concat(Enumerable.Range(0,5).Select(n=>"Wizard_1_"+n));
            tx.Select(new Choice(1,spells,cantrips:Enumerable.Range(0,3).Select(n=>"Wizard_0_"+n)));
            var final=tx.Commit();
            check(final.Repertoires["Wizard"].Known.Contains("SharedShield") && ReferenceEquals(final.Repertoires["Sorcerer"],own),
                "Repertoire behavior owner scope: legal overlap does not steal foreign spell ownership; GUI flag behavior is not executed");
        }
    }
    private static void Transactions(Action<bool,string> check)
    {
        foreach(var id in new[]{"Sorcerer","Bard","Ranger"})
        foreach(var actual in new[]{6,12})
        {
            var p=Fixture(id);var protectedView=new Repertoire(Fixture("Cleric"),8,true);var state=new HeroState(new[]{protectedView});
            for(var n=1;n<actual;n++)state=Level(state,p,n);
            var before=state;var tx=new Transaction("sequence"+id+actual,state,p,actual,true);
            check(tx.Events.Select(e=>e.Original).SequenceEqual(actual==6?new[]{6,7}:new[]{18,19,20}),$"Repertoire behavior events {id}/{actual}: chronological original-level sequence");
            var first=tx.Events.First();var earlier=Available(p,first.Original,state.Repertoires[id].Known).Id;
            var additions=new List<string>{earlier};
            while(additions.Count<first.Learned)additions.Add(Available(p,first.Original,state.Repertoires[id].Known.Concat(additions)).Id);
            if(first.Learned==0)additions.Clear();
            tx.Select(new Choice(first.Original,additions));
            var second=tx.Events[1];var removed=additions.LastOrDefault()??state.Repertoires[id].Known.Last();var learned=new List<string>();
            for(var i=0;i<second.Learned+1;i++)learned.Add(Available(p,second.Original,tx.Preview().Repertoires[id].Known.Concat(learned)).Id);
            tx.Select(new Choice(second.Original,learned,new[]{removed}));
            check(!tx.Preview().Repertoires[id].Known.Contains(removed) && tx.Preview().Repertoires[id].History.Any(h=>h.SpellId==removed && h.Removed),
                $"Repertoire behavior replacement {id}/{actual}: earlier same-advancement learning can be replaced with origin retained");
            tx.Back(second.Original);
            check((first.Learned==0 || tx.Preview().Repertoires[id].Known.Contains(earlier)) && ReferenceEquals(tx.Preview().Repertoires["Cleric"],protectedView),
                $"Repertoire behavior Back {id}/{actual}: downstream choices invalidated, other class untouched");
            tx.Reset();check(tx.Preview().Repertoires[id].Known.SequenceEqual(before.Repertoires[id].Known),$"Repertoire behavior Reset {id}/{actual}: provisional learning removed");
            Complete(tx,true);var final=tx.Commit();check(ReferenceEquals(final,tx.Commit()) && ReferenceEquals(final.Repertoires["Cleric"],protectedView),
                $"Repertoire behavior repeated finalize {id}/{actual}: idempotent result, no foreign changes");
            var cancelled=new Transaction("cancel"+id+actual,before,p,actual,true);Complete(cancelled,true);
            check(ReferenceEquals(cancelled.Cancel(),before) && Reject(()=>cancelled.Commit()),$"Repertoire behavior Cancel {id}/{actual}: original state returned, cancelled transaction cannot grant spells");
            var repeated=new Transaction("repeat"+id+actual,before,p,actual,true);Complete(repeated,true);
            check(repeated.Commit().Repertoires[id].Known.Count==final.Repertoires[id].Known.Count,$"Repertoire behavior repeat advancement {id}/{actual}: original event budgets retained without duplication");
        }
        var source=Fixture("Sorcerer");var foreign=new Repertoire(Fixture("Wizard"),8,true,new[]{"Wizard_1_0"});
        var baseline=new HeroState(new[]{new Repertoire(source,5,true,new[]{"Sorcerer_1_0"}),foreign});
        var invalid=new Transaction("foreign-replacement",baseline,source,6,true);
        check(Reject(()=>invalid.Select(new Choice(6,new[]{"Sorcerer_1_1","Sorcerer_1_2"},new[]{"Wizard_1_0"}))) &&
            ReferenceEquals(invalid.Preview().Repertoires["Wizard"],foreign),
            "Repertoire behavior foreign replacement: another class's spell cannot fund a replacement or be removed");
        var ranger=Fixture("Ranger");var rstate=new HeroState(new[]{new Repertoire(ranger,5,true,new[]{"SharedShield"})});
        var skip=new Transaction("skip",rstate,ranger,6,true);skip.Skip(6);
        check(skip.Preview().Repertoires["Ranger"].Known.Contains("SharedShield"),"Repertoire behavior Skip: optional replacement event consumes no mandatory spell choice");
        check(Reject(()=>skip.Commit()),"Repertoire behavior Skip: later mandatory learning cannot be silently skipped");
        var firstLevel=new Transaction("mandatory",new HeroState(Array.Empty<Repertoire>()),Fixture("Sorcerer"),1,true);
        check(Reject(()=>firstLevel.Skip(1)) && Reject(()=>firstLevel.Select(new Choice(2))),"Repertoire behavior validation: mandatory awards/foreign event not skipped or merged");
    }
    private static void BonusAndPrepared(Action<bool,string> check)
    {
        var foreign=new Spell("BorrowedClericSpell",1);
        var grants=new[]{new Grant("legacy-secrets",18,GrantKind.BonusChoice,new[]{foreign},1),new Grant("2024-expanded",10,GrantKind.ExpandedList,new[]{new Spell("ExpandedWizardSpell",5)}),
            new Grant("domain-one",3,GrantKind.AutoPrepared,new[]{new Spell("DomainSpell",2)}),new Grant("domain-two",5,GrantKind.AutoPrepared,new[]{new Spell("DomainSpell",2)})};
        var p=Fixture("Bard",true,grants);var state=new HeroState(Array.Empty<Repertoire>());
        for(var n=1;n<=12;n++)state=Level(state,p,n);
        var bard=state.Repertoires["Bard"];
        check(state.Repertoires.Count==1 && bard.Bonus["legacy-secrets"].Single()==foreign.Id && bard.History.Any(h=>h.SpellId==foreign.Id && h.ClassId=="Bard"),
            "Repertoire behavior Magical Secrets: selecting Cleric spell grants Bard origin, no Cleric/Wizard/Druid repertoire or budget");
        check(!p.Allows("ExpandedWizardSpell",9) && p.Allows("ExpandedWizardSpell",10),"Repertoire behavior2024 Secrets: original10 expanded-list access, independent from shared slots");
        check(bard.AutoPrepared.Count()==1 && bard.Definition.Grants.Count(g=>g.Kind==GrantKind.AutoPrepared)==2,
            "Repertoire behavior bonus origin: duplicate executable auto-spell deduplicated, both original source events retained");
        foreach(var modern in new[]{false,true})foreach(var id in new[]{"Wizard","Cleric","Druid","Paladin","Inventor"})
        {
            var definition=Fixture(id,modern);var level=new Repertoire(definition,12,true,id=="Wizard"?new[]{"Wizard_1_0"}:null);
            var spell=id+"_1_0";var prepared=level.Prepare(new[]{spell},4,2);
            check(prepared.Prepared.Single()==spell && prepared.Definition.Ability==Metadata(id).Ability && level.Prepared.Count==0,
                $"Repertoire behavior preparation {id}/{modern}: owner-scoped nonmutating selection, original ability");
            check(Reject(()=>level.Prepare(new[]{"OtherClass_1_0"},4,2)),$"Repertoire behavior preparation {id}/{modern}: foreign list rejected");
            check(definition.PreparedLimit(20,4,2)-definition.PreparedLimit(20,4,0)==2,
                $"Repertoire behavior preparation {id}/{modern}: scoped affinity added once, no second class modifier");
        }
        check(Fixture("Ranger",true).Mode==Knowledge.Selection && Fixture("Ranger",true).PreparedRule==Preparation.None && Fixture("Ranger",true).Known.Last()==15,
            "Repertoire behavior Ranger2024: cumulative known15, Selection/AllKnown, never WholeList/Prepared");
    }
    private static void WizardBooks(Action<bool,string> check)
    {
        var wizard=Fixture("Wizard");var other=new Repertoire(Fixture("Sorcerer"),12,true,new[]{"SharedShield"});var state=new HeroState(new[]{other});
        for(var n=1;n<6;n++)state=Level(state,wizard,n);
        var tx=new Transaction("book-events",state,wizard,6,true);Complete(tx);
        var costs=tx.Events.Sum(e=>e.Learned);
        check(costs==4 && tx.Events.Select(e=>e.Original).SequenceEqual(new[]{6,7}),"Repertoire behavior Wizard: source scribing events original6/7 stay separate");
        var books=new[]{("OtherOwner",2),("Hero",2),("",2)};
        check(tx.FitsSpellbooks(books,"Hero",true,false) && books[0].Item2==2,"Repertoire behavior Wizard books: native owner/page fitting across books without inventory mutation");
        check(!tx.FitsSpellbooks(new[]{("Hero",1)},"Hero",true,false) && ReferenceEquals(tx.Cancel(),state),"Repertoire behavior Wizard books: insufficient pages rejects finalization without losing spells/pages");
        var initial=new Transaction("initial",new HeroState(new[]{other}),wizard,1,true);Complete(initial);
        check(initial.FitsSpellbooks(Array.Empty<(string,int)>(),"Hero",true,true) && !initial.FitsSpellbooks(Array.Empty<(string,int)>(),"Hero",true,false),
            "Repertoire behavior Wizard books: starting-equipment exception only when explicitly pending");
        check(MultipleSpellRepertoiresPlan.ChoiceMaximum(0,false)==9 && MultipleSpellRepertoiresPlan.ChoiceMaximum(0,true)==0,
            "Repertoire behavior native pool convention: zero maximum means dynamic spell cap but cantrip-only pool retains circle0");
        var savant=Fixture("Wizard",false,new[]{new Grant("savant-book",1,GrantKind.BonusChoice,new[]{new Spell("SchoolSpell",1)},1,"")});
        var savantTx=new Transaction("savant-book",new HeroState(Array.Empty<Repertoire>()),savant,1,true);Complete(savantTx);
        var savantView=savantTx.Preview().Repertoires["Wizard"];
        check(savantView.Known.Contains("SchoolSpell") && !savantView.Bonus.ContainsKey("savant-book") &&
            !savantTx.FitsSpellbooks(new[]{("Hero",6)},"Hero",true,false) && savantTx.FitsSpellbooks(new[]{("Hero",7)},"Hero",true,false),
            "Repertoire behavior Wizard Savant: empty extra tag means native spellbook acquisition with page cost, not free bonus repertoire");
        var bonus=Fixture("Wizard",false,new[]{new Grant("school-scribing",6,GrantKind.ScribingBonus,Array.Empty<Spell>(),1)});
        var schoolState=new HeroState(Array.Empty<Repertoire>());for(var n=1;n<6;n++)schoolState=Level(schoolState,bonus,n);
        var school=new Transaction("school",schoolState,bonus,6,true);
        check(school.Events.All(e=>e.Learned==3),"Repertoire behavior Wizard school: own scribing affinity applied independently at each original event");
    }
    private static void Isolation(Action<bool,string> check,AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();var context=types.Single(t=>t.Name=="MultipleSpellRepertoiresContext");
        bool Calls(MethodDefinition m,string owner,string name)=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name==owner && r.Name==name);
        check(Calls(context.Methods.Single(m=>m.Name=="get_RuntimeEnabled"),"StandardMulticasterResourcesContext","get_RuntimeEnabled"),"Repertoire compiled binding: same closed resource/admission gate, no new feature switch");
        var boot=Types(types.Single(t=>t.Name=="BootContext")).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        var capture=Array.FindIndex(boot,m=>m.DeclaringType.Name==context.Name && m.Name=="CaptureInstalledDefinitions");
        check(capture>Array.FindIndex(boot,m=>m.DeclaringType.Name=="SpellPointsContext" && m.Name=="LateLoad") && capture<Array.FindIndex(boot,m=>m.DeclaringType.Name=="FighterProgressionContext" && m.Name=="LateLoad"),
            "Repertoire compiled binding: original class metadata captured after rule switches and before compression");
        check(Calls(context.Methods.Single(m=>m.Name=="CaptureCore"),context.Name,"CaptureGrants"),"Repertoire compiled binding: auto/bonus/extended spell grants frozen before source definitions are projected");
        var resources=types.Single(t=>t.Name=="StandardMulticasterResourcesContext");
        check(Calls(resources.Methods.Single(m=>m.Name=="BindNewHero"),context.Name,"BindNewHero") && Calls(resources.Methods.Single(m=>m.Name=="UpdateProgression"),context.Name,"Synchronize"),
            "Repertoire compiled binding: explicit resource owner binder supplies same validated progression, no new budget");
        check(Calls(types.Single(t=>t.Name=="SharedSpellsContext").Methods.Single(m=>m.Name=="MaxSpellLevelOfSpellCastingLevel"),context.Name,"TryAccess"),
            "Repertoire compiled binding: class-local max query uses captured own access; ordinary resource max getter remains shared");
        check(Calls(types.Single(t=>t.Name=="LevelUpHelper").Methods.Single(m=>m.Name=="EnumerateKnownAndAcquiredSpells"),context.Name,"EnumerateKnown"),
            "Repertoire compiled binding: own known/acquired filter bypasses legacy global union under gate");
        var prepared=context.NestedTypes.Single(t=>t.Name=="PreparedPatch");
        check(prepared.Methods.Any(m=>Calls(m,context.Name,"TryPrepared")) && prepared.Methods.Single(m=>m.Name=="Postfix").CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPriority" && Convert.ToInt32(a.ConstructorArguments[0].Value)==0),
            "Repertoire compiled binding: final prepared limit applies after legacy/class-specific patches");
        check(context.NestedTypes.Single(t=>t.Name=="AcquirePatch").Methods.Any(m=>Calls(m,context.Name,"CanAcquire")) && context.NestedTypes.Single(t=>t.Name=="FeaturePatch").Methods.Any(m=>Calls(m,context.Name,"TryFeature")),
            "Repertoire compiled binding: original-event acquisition ceiling/list and casting-feature tag ownership guarded");
        check(context.NestedTypes.Single(t=>t.Name=="ResetPatch").Methods.Any(m=>Calls(m,context.Name,"ResetChoices")),"Repertoire compiled binding: reset clears only registered transaction acquisition tags");
        var finalize=types.Single(t=>t.Name=="CharacterBuildingManagerPatcher").NestedTypes.Single(t=>t.Name=="FinalizeCharacter_Patch").Methods.Single(m=>m.Name=="Prefix");
        check(Calls(finalize,context.Name,"ValidateBeforeFinalize"),"Repertoire compiled binding: spellbook/pending transaction validation before native finalization mutation");
        check(!context.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.OpCode.Code==Code.Newobj && i.Operand is MethodReference r && r.DeclaringType.Name is "RulesetSpellRepertoire" or "RulesetUsablePower" or "RulesetItemSpellbook"),
            "Repertoire compiled binding: no auxiliary repertoire, second PowerSpellPoints or fabricated spellbook constructed");
        check(Types(context).SelectMany(t=>t.Methods).Where(m=>m.HasBody).Any(m=>m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.Name=="HasAnyFeature")),
            "Repertoire compiled binding: Exclusion options require native active-feature ownership, never all alternatives by flattening");
        var planTypes=Types(types.Single(t=>t.Name=="MultipleSpellRepertoiresPlan")).ToArray();
        check(!planTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference r && r.DeclaringType.Namespace.StartsWith("Unity",StringComparison.Ordinal)),
            "Repertoire compiled binding: production transaction/access model has no Unity dependency");
    }
}
