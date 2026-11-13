using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using P = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using Inv = SolastaUnfinishedBusiness.Models.WarlockInvocationTransaction<string>;

// Production calculator, separate budgets, dispatch, repertoire and Stage6.1 invocation journal run
// here. Native game/Harmony/GUI/rest/save execution is NOT performed; IL checks are labelled.
internal static class PactMagicMulticasterTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    private static readonly int[] Full={1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
    private static S.Tables standardTables;
    private static P.Tables pactTables;
    private static int[] known;
    private static int[][] pactRows;
    private static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}catch(InvalidOperationException){return true;}}
    private static C.ClassInput Class(string id,int level,bool compressed=true)=>new(id,level,compressed);
    private static C.Result Calculate(params C.ClassInput[] inputs)
    {
        var ids=inputs.Select(i=>i.ClassId).Distinct().ToArray();
        var registry=new C("Pact13.5-source-1",ids.Select(id=>new C.ClassProfile(id,true,id=="Fighter"?null:new C.CastingProfile("CastSpell"+id,
            id=="Warlock"?C.CasterKind.Pact:id=="Paladin"?C.CasterKind.Half:C.CasterKind.Full,1,true,
            id=="Warlock"?pactRows.Select(r=>Array.FindIndex(r,n=>n>0)+1):id=="Paladin"?Enumerable.Range(1,20).Select(n=>Full[(n+1)/2-1]):Full))));
        return registry.Calculate(new C.Snapshot(inputs.Sum(i=>i.ActualLevel),inputs,inputs.SelectMany(i=>Enumerable.Repeat(i.ClassId,i.ActualLevel))));
    }
    private static P.CastingLedger Ledger(C.Result result,S.ResourceMode mode=S.ResourceMode.SpellPoints,int extra=0) => new(
        new S.Budget(standardTables,S.Allocation.StandardPortion(standardTables,result,mode)),new P.Budget(new P.Allocation(pactTables,result,extra)));
    private static P.Cast Cast(string op,string owner,int circle=5,P.Source source=P.Source.Automatic,string spell="Shield",int baseCircle=1,bool arcOnly=false,bool authorized=true,
        string ability="Charisma",int dc=17)=>new(op,owner,"CastSpell"+owner,spell,baseCircle,circle,ability,dc,authorized,source,arcOnly);
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();
        int[] Added(string type,string field)=>CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name==type),field).Where(n=>n>=0).ToArray();
        var ordinary=Added("FeatureDefinitionCastSpellBuilder","SlotsByCasterLevel");
        standardTables=new S.Tables(Enumerable.Range(1,20).Select(n=>ordinary.Skip(n*10).Take(9)),Added("ModifyPowerPoolAmountPowerSpellPoints","SpellPointsByLevel"),Added("SpellPointsContext","SpellCostByLevel"));
        var flat=Added("SharedSpellsContext","WarlockCastingSlots");known=Added("SharedSpellsContext","WarlockKnownSpells");
        check(flat.Length==120 && known.Length==20,"Pact compiled-table fixture:20 UB rows of6 display indicators,20 Warlock known rows");
        // UB display rows repeat count at circles1..P. Native CastSpellWarlock rows have ONLY P.
        pactRows=Enumerable.Range(0,20).Select(n=>{var display=flat.Skip(n*6).Take(6).ToArray();var circle=Array.IndexOf(display,0);var row=new int[9];row[circle-1]=display[0];return row;}).ToArray();
        var arcana=new List<P.Arcanum>();
        foreach(var entry in new[]{(11,6),(13,7),(15,8),(17,9)})
        {
            using var doc=JsonDocument.Parse(File.ReadAllText("Diagnostics/"+(entry.Item2==9?"UnfinishedBusinessBlueprints":"OfficialBlueprints")+"/FeatureDefinitionPointPool/PointPoolWarlockMysticArcanum"+entry.Item2+".json"));
            var p=doc.RootElement;
            check(p.GetProperty("poolAmount").GetInt32()==1 && p.GetProperty("minSpellLevel").GetInt32()==entry.Item2 && p.GetProperty("maxSpellLevel").GetInt32()==entry.Item2 && p.GetProperty("extraSpellsTag").GetString()=="MysticArcanum",
                "Pact blueprint metadata: exact independent Arcanum pool circle"+entry.Item2);
            arcana.Add(new P.Arcanum("NativeArcanum"+entry.Item2,entry.Item1,entry.Item2,new[]{"Arc"+entry.Item2,"Alternate"+entry.Item2}));
        }
        pactTables=new P.Tables(pactRows,arcana);
        Examples(check); RoundingAndRows(check); Expenditure(check); Arcanum(check); RestAndModifiers(check); RepertoiresAndInvocations(check); Bindings(check,game,types);
    }
    private static void Examples(Action<bool,string> check)
    {
        var examples=new[]{("Sorcerer12/Warlock8",new[]{Class("Sorcerer",12),Class("Warlock",8)},20,133,3),
            ("Warlock12/Sorcerer8",new[]{Class("Warlock",12),Class("Sorcerer",8)},11,73,4),
            ("Bard12/Warlock8",new[]{Class("Bard",12),Class("Warlock",8)},20,133,3),
            ("Paladin12/Warlock8",new[]{Class("Paladin",12),Class("Warlock",8)},10,64,3),
            ("Warlock12/Fighter8",new[]{Class("Warlock",12),Class("Fighter",8)},0,0,4)};
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})foreach(var x in examples)
        {
            var result=Calculate(x.Item2);var ledger=Ledger(result,mode);
            check(result.IsValid && result.ResourceTableIndex==x.Item3 && ledger.Standard.Current.BasePointMaximum==x.Item4 && ledger.Pact.Current.Slots==x.Item5 && ledger.Pact.Current.Circle==5,
                $"Pact behavior {x.Item1}/{mode}: independent original Pact and bounded standard resource budgets");
            check(result.Input.ActualCharacterLevel==20 && result.PactProgressions.Single().ContributionNumerator==0 && result.Classes.Where(c=>c.Kind==C.CasterKind.Pact).All(c=>c.MaximumSpellCircle==5),
                $"Pact behavior {x.Item1}/{mode}: actual total20, no Pact shared contribution or generic circle6-9 access");
            check(ledger.Standard.RemainingPoints==x.Item4 && ledger.Pact.Remaining==x.Item5 && ledger.Pact.Selected.Count==0,
                $"Pact behavior {x.Item1}/{mode}: no implicit Arcanum selection, no duplicated133-point pool");
        }
    }
    private static void RoundingAndRows(Action<bool,string> check)
    {
        foreach(var compression in new[]{false,true})
        for(var actual=1;actual<=(compression?12:20);actual++)
        {
            var result=Calculate(Class("Warlock",actual,compression));var ledger=Ledger(result);
            var e=compression?FighterProgressionPlan<int>.EffectiveLevel(actual):actual;
            check(ledger.Pact.Current.EffectiveWarlockLevel==e && ledger.Pact.Current.Slots==pactRows[e-1].Sum() && ledger.Pact.Current.Circle==Array.FindIndex(pactRows[e-1],n=>n>0)+1,
                $"Pact behavior row actual{actual}/compression{compression}: native original count/circle preserved");
            check(ledger.Standard.Current.CasterLevel==0 && ledger.Standard.Current.PointMaximum==0 && ledger.Pact.Current.Arcana.Count==new[]{11,13,15,17}.Count(n=>n<=e),
                $"Pact behavior row actual{actual}/compression{compression}: Pact-only has no ordinary pool; independent Arcanum milestones");
        }
        var half=Calculate(Class("Paladin",8),Class("Warlock",12));
        check(half.ResourceTableIndex==5 && Ledger(half).Standard.RemainingPoints==27,"Pact behavior rounding: keep original UB grouped floor Half11/2 when Pact participates; no solo re-rounding to6");
        var mixed=Calculate(Class("Sorcerer",12),Class("Warlock",8,false));
        check(Ledger(mixed).Pact.Current.EffectiveWarlockLevel==8 && Ledger(mixed).Pact.Current.Circle==4 && Ledger(mixed).Pact.Remaining==2 && mixed.ResourceTableIndex==20,
            "Pact behavior independent flags: uncompressed Warlock8/Pact2x4 alongside compressed Sorcerer20");
        var triple=Calculate(Class("Warlock",8),Class("Sorcerer",6),Class("Paladin",6));
        check(triple.ResourceTableIndex==10 && Ledger(triple).Pact.Remaining==3,"Pact behavior three classes: full7+floor(Half7/2)=10, Pact11 remains3x5");
        check(Reject(()=>new S.Allocation(standardTables,mixed,S.ResourceMode.SpellPoints)),"Pact behavior isolation: naked standard allocation still rejects Pact; explicit StandardPortion coordinator required");
    }
    private static void Expenditure(Action<bool,string> check)
    {
        foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})
        {
            var ledger=Ledger(Calculate(Class("Warlock",12),Class("Sorcerer",8)),mode);
            var maximum=ledger.Standard.RemainingPoints;
            var warlock=Cast("warlock-standard","Warlock",1,P.Source.Standard);
            check(ledger.Spend(warlock).Allowed && ledger.Pact.Remaining==4 && (mode==S.ResourceMode.SpellPoints?ledger.Standard.RemainingPoints==maximum-2:ledger.Standard.RemainingSlots(1)==3),
                $"Pact behavior cross-cast {mode}: Warlock spell can pay ordinary resource without spending Pact");
            var sorcerer=Cast("sorcerer-pact","Sorcerer",5,P.Source.Pact,ability:"Charisma",dc:19);
            var paid=ledger.Spend(sorcerer);
            check(paid.Allowed && paid.Resource==P.Source.Pact && paid.Circle==5 && sorcerer.Ability=="Charisma" && sorcerer.SaveDc==19 && ledger.Pact.Remaining==3,
                $"Pact behavior cross-cast {mode}: other repertoire uses Pact at correct upcast5, unchanged original DC/ability");
            var before=ledger.Standard.RemainingPoints;
            check(ledger.Spend(sorcerer,true,true).Allowed && ledger.Pact.Remaining==3 && ledger.Standard.RemainingPoints==before,
                $"Pact behavior receipt {mode}: repeated action/Shift observation never reroutes or double-spends");
            check(!ledger.Spend(Cast("sorcerer-pact","Sorcerer",4,P.Source.Standard)).Allowed,"Pact behavior conflicting action: new circle/source rejected for same action identity");
            check(ledger.Resolve(Cast("wrong-pact","Sorcerer",1,P.Source.Pact),false,false).Error==P.Failure.SelectPactCircle,
                $"Pact behavior correct level {mode}: cannot pay fifth-level Pact for first-level effect without choosing upcast");
            check(ledger.Refund("sorcerer-pact") && !ledger.Refund("sorcerer-pact") && ledger.Pact.Remaining==4,
                $"Pact behavior cancellation {mode}: refund returns only the originally spent Pact resource once");
            check(!ledger.Spend(sorcerer).Allowed,"Pact behavior cancellation: refunded action cannot recommit under the old token");
        }
        foreach(var warlock in new[]{false,true})foreach(var shift in new[]{false,true})foreach(var prefer in new[]{false,true})
        {
            var ledger=Ledger(Calculate(Class("Warlock",12),Class("Sorcerer",8)));
            var result=ledger.Spend(Cast("policy",warlock?"Warlock":"Sorcerer"),prefer,shift);
            var expected=prefer || warlock&&!shift || !warlock&&shift?P.Source.Pact:P.Source.Standard;
            check(result.Resource==expected && result.Allowed,$"Pact behavior installed selection policy: Warlock{warlock}/Shift{shift}/Always{prefer}");
        }
        var exhausted=Ledger(Calculate(Class("Warlock",12),Class("Sorcerer",8)));
        for(var i=0;i<4;i++)exhausted.Spend(Cast("pact"+i,"Sorcerer",5,P.Source.Pact));
        check(exhausted.Pact.Remaining==0 && exhausted.Standard.RemainingPoints==73 && exhausted.Spend(Cast("ordinary-remains","Warlock",5)).Resource==P.Source.Standard,
            "Pact behavior exhaustion: empty Pact falls back to eligible standard budget while standard points remain");
        var standardEmpty=Ledger(Calculate(Class("Warlock",12),Class("Sorcerer",8)));
        standardEmpty.Standard.Spend("cost3",2);
        for(var i=0;i<35;i++)standardEmpty.Standard.Spend("cost2-"+i,1);
        check(standardEmpty.Standard.RemainingPoints==0 && standardEmpty.Pact.Remaining==4 && standardEmpty.Spend(Cast("pact-remains","Sorcerer")).Resource==P.Source.Pact,
            "Pact behavior exhaustion: empty ordinary Spell Points does not hide or consume remaining Pact incorrectly");
        for(var i=0;i<3;i++)standardEmpty.Spend(Cast("remaining-pact"+i,"Warlock",5,P.Source.Pact));
        check(standardEmpty.Pact.Remaining==0 && !standardEmpty.Spend(Cast("both-empty","Warlock")).Allowed && standardEmpty.Standard.RemainingPoints==0,
            "Pact behavior exhaustion: both empty blocks spell, neither resource goes negative or grants free points");
        check(!standardEmpty.Resolve(Cast("unauthorized","Warlock",1,authorized:false),false,false).Allowed,
            "Pact behavior authorization: available shared resources never authorize an unowned spell");
    }
    private static void Arcanum(Action<bool,string> check)
    {
        var ledger=Ledger(Calculate(Class("Warlock",12),Class("Sorcerer",8)));
        foreach(var circle in Enumerable.Range(6,4))
        {
            ledger.Pact.SelectArcanum(circle,"Arc"+circle);
            check(Reject(()=>ledger.Pact.SelectArcanum(circle,"Alternate"+circle)),"Pact behavior Arcanum"+circle+": confirmed native choice cannot be silently retrained");
            var request=Cast("arcanum"+circle,"Warlock",circle,P.Source.Arcanum,"Arc"+circle,circle,true);
            var points=ledger.Standard.RemainingPoints;var pact=ledger.Pact.Remaining;
            check(ledger.Spend(request).Allowed && ledger.Pact.ArcanumUsed(circle) && !ledger.Pact.CanArcanum(circle,"Arc"+circle) && ledger.Standard.RemainingPoints==points && ledger.Pact.Remaining==pact,
                "Pact behavior Arcanum"+circle+": one own use, no ordinary slot/SP/Pact expenditure");
            check(ledger.Spend(request).Allowed && !ledger.Spend(Cast("again"+circle,"Warlock",circle,P.Source.Arcanum,"Arc"+circle,circle,true)).Allowed,
                "Pact behavior Arcanum"+circle+": repeated receipt idempotent, second distinct cast exhausted");
            check(!ledger.Spend(Cast("generic"+circle,"Warlock",circle,P.Source.Standard,"Arc"+circle,circle,true)).Allowed,
                "Pact behavior Arcanum"+circle+": cannot become a generic standard spell or resource");
            check(!ledger.Spend(Cast("upcastArc"+circle,"Warlock",Math.Min(9,circle+1),P.Source.Arcanum,"Arc"+circle,circle,true)).Allowed,
                "Pact behavior Arcanum"+circle+": fixed spell/circle cannot be freely upcast or bypass used charge");
        }
        check(ledger.Spend(Cast("same-spell-other-owner","Sorcerer",6,P.Source.Standard,"Arc6",6)).Allowed && ledger.Standard.RemainingPoints==64,
            "Pact behavior same spell/other repertoire: independently known Sorcerer spell may use its standard budget; Warlock Arcanum stays spent");
        var lower=Ledger(Calculate(Class("Sorcerer",12),Class("Warlock",8)));
        check(Reject(()=>lower.Pact.SelectArcanum(7,"Arc7")) && lower.Pact.Current.Arcana.Single().Circle==6,
            "Pact behavior Arcanum access: shared caster20 cannot grant Warlock original11 its seventh-to-ninth Arcanum");
        var before=ledger.Standard.RemainingPoints;ledger.Standard.RecoverPoints("standard-recovery",100);
        check(ledger.Pact.ArcanumUsed(6) && ledger.Standard.RemainingPoints==73 && before==64,
            "Pact behavior standard recovery: restoring Spell Points never resets Arcanum uses");
    }
    private static void RestAndModifiers(Action<bool,string> check)
    {
        foreach(var mode in new[]{S.ResourceMode.SpellPoints,S.ResourceMode.Slots})
        {
            var result=Calculate(Class("Warlock",12),Class("Sorcerer",8));var ledger=Ledger(result,mode,1);
            ledger.Pact.SelectArcanum(6,"Arc6");ledger.Spend(Cast("arc","Warlock",6,P.Source.Arcanum,"Arc6",6,true));
            ledger.Spend(Cast("pact","Warlock",5,P.Source.Pact));ledger.Spend(Cast("standard","Warlock",1,P.Source.Standard));
            var points=ledger.Standard.RemainingPoints;var slots=ledger.Standard.RemainingSlots(1);
            ledger.Pact.Refresh(new P.Allocation(pactTables,result,1));
            check(ledger.Pact.Current.Slots==5 && ledger.Pact.Remaining==4 && ledger.Standard.RemainingPoints==points && ledger.Pact.ArcanumUsed(6),
                $"Pact behavior modifier/refresh {mode}: native extra Pact slot, no refill or SP conversion");
            ledger.Rest("simulation",true,true);
            check(ledger.Pact.Remaining==4 && ledger.Pact.ArcanumUsed(6) && ledger.Standard.RemainingSlots(1)==slots,
                $"Pact behavior rest simulation {mode}: all budgets unchanged");
            ledger.Rest("short",false);
            check(ledger.Pact.Remaining==5 && ledger.Pact.ArcanumUsed(6) && ledger.Standard.RemainingPoints==points && ledger.Standard.RemainingSlots(1)==slots,
                $"Pact behavior short rest {mode}: restores Pact only, not ordinary or Arcanum");
            check(!ledger.Refund("pact"),"Pact behavior old receipt: cannot refund a pre-short-rest Pact debit into new capacity");
            ledger.Rest("long",true);
            check(ledger.Pact.Remaining==5 && !ledger.Pact.ArcanumUsed(6) && ledger.Standard.RemainingSlots(1)==(mode==S.ResourceMode.Slots?4:1) && ledger.Standard.RemainingPoints==73,
                $"Pact behavior long rest {mode}: independent budgets recover, no duplicated pool");
            ledger.Spend(Cast("after-rest","Warlock",5,P.Source.Pact));ledger.Rest("long",true);
            check(ledger.Pact.Remaining==4,"Pact behavior repeated rest receipt: does not erase later spending");
            var standardBefore=ledger.Standard.RemainingPoints;ledger.Pact.Recover("cunning",(ledger.Pact.Current.Slots+1)/2);ledger.Pact.Recover("cunning",(ledger.Pact.Current.Slots+1)/2);
            check(ledger.Pact.Remaining==5 && ledger.Standard.RemainingPoints==standardBefore && !ledger.Pact.ArcanumUsed(6),
                $"Pact behavior owned power recovery {mode}: capped/idempotent Pact recovery, no standard or Arcanum mutation");
        }
        var modifier=Ledger(Calculate(Class("Warlock",12),Class("Fighter",8)),extra:1);
        for(var n=0;n<5;n++)modifier.Pact.Spend("spent-modifier"+n,P.Source.Pact,5);
        modifier.Pact.Refresh(new P.Allocation(pactTables,Calculate(Class("Warlock",12),Class("Fighter",8))));
        modifier.Pact.Refresh(new P.Allocation(pactTables,Calculate(Class("Warlock",12),Class("Fighter",8)),1));
        check(modifier.Pact.Remaining==0 && modifier.Standard.RemainingPoints==0,"Pact behavior modifier removal/re-add: preserve debt, no extra uses or ordinary points");
        modifier.Pact.Refresh(new P.Allocation(pactTables,Calculate(Class("Warlock",12),Class("Fighter",8))));
        modifier.Pact.RecoverAll("master-all");modifier.Pact.RecoverAll("master-all");
        check(modifier.Pact.Remaining==4 && modifier.Standard.RemainingPoints==0,"Pact behavior full recovery: clears all Pact debt after modifier removal, capped current capacity, no SP creation");
    }
    private static void RepertoiresAndInvocations(Action<bool,string> check)
    {
        var circles=pactRows.Select(r=>Array.FindIndex(r,n=>n>0)+1).ToArray();
        var spellList=Enumerable.Range(0,6).SelectMany(c=>Enumerable.Range(0,40).Select(n=>new R.Spell("W"+c+"-"+n,c))).ToArray();
        var arcana=Enumerable.Range(6,4).Select(c=>new R.Grant("Arcanum"+c,2*c-1,R.GrantKind.MysticArcanum,new[]{new R.Spell("Arc"+c,c)},1,"MysticArcanum")).ToArray();
        var profile=new R.Profile("Warlock","CastSpellWarlock","Charisma","SpellListWarlock",R.Knowledge.Selection,R.Preparation.None,
            spellList,circles,Enumerable.Range(1,20).Select(n=>n>=10?4:n>=4?3:2),known,Enumerable.Range(1,20).Select(n=>n==1?0:1),new int[20],grants:arcana);
        var foreign=new R.Repertoire(new R.Profile("Sorcerer","CastSpellSorcerer","Charisma","SpellListSorcerer",R.Knowledge.Selection,R.Preparation.None,
            new[]{new R.Spell("S",1)},Full,new int[20],new int[20],new int[20],new int[20]),8,true,new[]{"S"});
        check(Reject(()=>new R.Profile("Sorcerer","CastSpellSorcerer","Charisma","SorcererList",R.Knowledge.Selection,R.Preparation.None,
                spellList,circles,new int[20],known,new int[20],new int[20],grants:arcana)),
            "Pact behavior Arcanum source isolation: generic/full-caster class cannot register Warlock high-circle bypass grants");
        check(Reject(()=>new R.Profile("Warlock","CastSpellWarlock","Charisma","WarlockList",R.Knowledge.Selection,R.Preparation.None,
                spellList,circles,new int[20],known,new int[20],new int[20],grants:new[]{new R.Grant("invalid-arc",11,R.GrantKind.MysticArcanum,new[]{new R.Spell("Ordinary5",5)},1,"MysticArcanum")})),
            "Pact behavior Arcanum source isolation: low-circle ordinary spell cannot masquerade as an Arcanum grant");
        var state=new R.HeroState(new[]{foreign});
        for(var actual=1;actual<=12;actual++)
        {
            var tx=new R.Transaction("Warlock"+actual,state,profile,actual,true);
            foreach(var e in tx.Events)
            {
                var own=tx.Preview().Repertoires["Warlock"];
                var added=spellList.Where(s=>s.Circle>0 && s.Circle<=circles[e.Original-1] && !own.Known.Contains(s.Id)).Take(e.Learned).Select(s=>s.Id).ToArray();
                var cantrips=spellList.Where(s=>s.Circle==0 && !own.Cantrips.Contains(s.Id)).Take(e.Cantrips).Select(s=>s.Id).ToArray();
                var extras=arcana.Where(a=>a.Original==e.Original).Select(a=>new KeyValuePair<string,IReadOnlyList<string>>(a.Id,a.Spells.Select(s=>s.Id).ToArray()));
                tx.Select(new R.Choice(e.Original,added,cantrips:cantrips,bonus:extras));
            }
            state=tx.Commit();var warlock=state.Repertoires["Warlock"];
            check(warlock.MaximumCircle<=5 && ReferenceEquals(state.Repertoires["Sorcerer"],foreign) && warlock.History.All(h=>h.ClassId=="Warlock"),
                "Pact behavior repertoire actual"+actual+": independent Warlock list/cantrips/origins, other repertoire unchanged");
        }
        check(state.Repertoires["Warlock"].Bonus.Count==4 && state.Repertoires["Warlock"].Known.Count==15 && !profile.Allows("Arc9",20),
            "Pact behavior Arcanum selections: four independent high-circle bonus choices, ordinary known15 and access5");
        foreach(var revised in new[]{false,true})
        {
            var invocations=new[]{"PactTome","A"};
            var before=Calculate(Class("Warlock",12),Class("Sorcerer",7));var after=Calculate(Class("Warlock",12),Class("Sorcerer",8));var ledger=Ledger(before);
            ledger.Spend(Cast("before-other-class","Warlock",5,P.Source.Pact));
            ledger.Standard.Refresh(S.Allocation.StandardPortion(standardTables,after,S.ResourceMode.SpellPoints));ledger.Pact.Refresh(new P.Allocation(pactTables,after));
            check(invocations.SequenceEqual(new[]{"PactTome","A"}) && ledger.Pact.Remaining==3 && ledger.Standard.Current.CasterLevel==11,
                "Pact behavior other-class advancement/2024="+revised+": no invocation rewards/retraining or Pact refill from martial/standard advancement");
            var tx=new Inv(invocations,new[]{
                new Inv.Event("W18",18,1,WarlockProgressionRules.InvocationReplacementBudget(revised,18),new[]{"A"},new[]{"B","C"}),
                new Inv.Event("W19",19,0,WarlockProgressionRules.InvocationReplacementBudget(revised,19),new[]{"B"},new[]{"D"}),
                new Inv.Event("W20",20,0,WarlockProgressionRules.InvocationReplacementBudget(revised,20),new[]{"D"},new[]{"E"})});
            var frames=tx.Prepare((x,n,inventory)=>x!="E"||n>=20&&inventory.Contains("PactTome"));
            check(frames.Last().After.SequenceEqual(new[]{"PactTome","C","E"}) && invocations.SequenceEqual(new[]{"PactTome","A"}),
                "Pact behavior Stage6.1/2024="+revised+": reuse ordered invocation retraining through18/19/20, same-advancement replacement and Pact prerequisite");
            check(WarlockProgressionRules.InvocationReplacementBudget(revised,2)==(revised?1:0),"Pact behavior2014/2024: original invocation replacement boundary remains unchanged");
        }
    }
    private static void Bindings(Action<bool,string> check,AssemblyDefinition game,TypeDefinition[] types)
    {
        var context=types.Single(t=>t.Name=="PactMagicMulticasterContext");var standard=types.Single(t=>t.Name=="StandardMulticasterResourcesContext");
        bool Calls(MethodDefinition m,string owner,string name)=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.DeclaringType.Name==owner&&r.Name==name);
        check(Calls(context.Methods.Single(m=>m.Name=="get_RuntimeEnabled"),standard.Name,"get_RuntimeEnabled") && types.Single(t=>t.Name=="CompressedMulticasterProgressionPlan").Fields.Single(f=>f.Name=="MixedGameplayReady").Constant is false,
            "Pact compiled binding OFF: same permanently closed gate, no settings/UI activation");
        check(Calls(standard.Methods.Single(m=>m.Name=="BindNewHero"),context.Name,"BindNewHero")&&Calls(standard.Methods.Single(m=>m.Name=="UpdateProgression"),context.Name,"Synchronize"),
            "Pact compiled binding: same validated new hero and actual standard ledger, no second standard owner");
        foreach(var method in new[]{"TrySpend","TryCanSpend","FinishRest","Project"})
            check(standard.Methods.Single(m=>m.Name==method).Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.DeclaringType.Name==context.Name),"Pact compiled binding: standard/Pact coordination at "+method);
        var shared=types.Single(t=>t.Name=="SharedSpellsContext");
        foreach(var name in new[]{"GetWarlockSpellLevel","GetWarlockMaxSlots","GetWarlockUsedSlots"})
            check(Calls(shared.Methods.Single(m=>m.Name==name),context.Name,"TryNumbers"),"Pact compiled binding: authoritative native query "+name);
        check(shared.Fields.Single(f=>f.Name=="PactMagicSlotsTab").Constant is -1,"Pact compiled binding: sentinel-1 preserved for presentation, not a caster level/SP index");
        check(context.NestedTypes.Single(t=>t.Name=="CastScopePatch").CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPatch") &&
            context.NestedTypes.Single(t=>t.Name=="ArcanumPatch").Methods.Any(m=>Calls(m,context.Name,"Spend")),
            "Pact compiled binding: native CastSpell effect identity and separate SpendMysticArcanum interception");
        var nativeCast=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter").Methods.Single(m=>m.Name=="CastSpell");
        check(nativeCast.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="SpendMysticArcanum"),"Pact native IL audit: Arcanum spend occurs inside scoped CastSpell, independent of ordinary SpendSpellSlot");
        var conversion=game.MainModule.Types.Single(t=>t.Name=="RulesetCharacter").Methods.Single(m=>m.Name=="CreateSorceryPoints");
        check(conversion.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="SpendSpellSlot") && context.NestedTypes.Single(t=>t.Name=="ConversionPatch").Methods.Any(m=>Calls(m,context.Name,"BlockConversion")),
            "Pact native IL/binding audit: existing slot-to-sorcery conversion retains old cost path OFF, gated unsafe path blocked before native debit/credit");
        check(Types(context).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).All(i=>!(i.OpCode.Code==Code.Newobj && i.Operand is MethodReference r && r.DeclaringType.Name is "RulesetUsablePower" or "RulesetSpellRepertoire")),
            "Pact compiled binding: no duplicate PowerSpellPoints or fabricated repertoire");
        var invocation=types.Single(t=>t.Name=="WarlockInvocationContext");
        check(Calls(invocation.Methods.Single(m=>m.Name=="Uses"),"LevelUpHelper","GetSelectedClass") && !context.Methods.Any(m=>m.Name=="TrainInvocation"),
            "Pact compiled binding: original Stage6.1 selected-Warlock guard/transaction ownership retained, no grants after other-class advancement");
        var tables=context.Methods.Single(m=>m.Name=="CaptureCore");
        check(Calls(tables,"DatabaseHelper/FeatureDefinitionCastSpells","get_CastSpellWarlock") || tables.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="get_CastSpellWarlock"),
            "Pact compiled binding: snapshot uses native CastSpellWarlock single-circle rows, not repeated display capacities");
    }
}
