using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using static SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using static SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using Progression = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;

// Behavioral assertions execute the production calculator/ledger/owner store. Native binding
// assertions only inspect compiled IL; no Harmony, combat, rest or save/load is executed here.
internal static class StandardCasterResourceTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static int Integer(Instruction i) => i.OpCode.Code switch
    {
        Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1, Code.Ldc_I4_2 => 2, Code.Ldc_I4_3 => 3, Code.Ldc_I4_4 => 4,
        Code.Ldc_I4_5 => 5, Code.Ldc_I4_6 => 6, Code.Ldc_I4_7 => 7, Code.Ldc_I4_8 => 8,
        Code.Ldc_I4_S => Convert.ToInt32(i.Operand), Code.Ldc_I4 => (int)i.Operand, _ => int.MinValue
    };
    private static int[] AddedIntegers(TypeDefinition type, string field)
    {
        var body = type.Methods.Single(x => x.IsConstructor && x.IsStatic).Body.Instructions;
        var end = body.ToList().FindIndex(i => i.OpCode.Code == Code.Stsfld && ((FieldReference)i.Operand).Name == field);
        var start = end - 1; while (start >= 0 && body[start].OpCode.Code != Code.Stsfld) start--;
        return body.Skip(start + 1).Take(end - start - 1).Where(i => i.Operand is MethodReference m && m.Name == "Add" && m.DeclaringType.Name == "List`1")
            .Select(i => Integer(i.Previous)).ToArray();
    }
    private static bool Reject(Action action)
    {
        try { action(); return false; } catch (ArgumentException) { return true; } catch (InvalidOperationException) { return true; }
    }
    private static readonly int[] Access = { 1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9 };
    private static ClassProfile Profile(string name, CasterKind kind) => new(name, true, kind == CasterKind.None ? null :
        new CastingProfile("Cast" + name, kind, kind == CasterKind.Half ? 2 : kind == CasterKind.OneThird ? 3 : 1,
            kind != CasterKind.OneThird, Enumerable.Range(1,20).Select(n => kind switch
            {
                CasterKind.Full => Access[n-1], CasterKind.Pact => Math.Min(5,(n+1)/2),
                CasterKind.OneThird => n < 3 ? 0 : Math.Min(4,(n+5)/6), _ => Math.Min(5,(n+3)/4)
            })));
    private static Progression Registry() => new("resource-tests-original-v1", new[] {
        Profile("Wizard",CasterKind.Full), Profile("Cleric",CasterKind.Full), Profile("Sorcerer",CasterKind.Full),
        Profile("Druid",CasterKind.Full), Profile("Bard",CasterKind.Full), Profile("Paladin",CasterKind.Half),
        Profile("Ranger",CasterKind.Half), Profile("Inventor",CasterKind.HalfRoundUp), Profile("Warlock",CasterKind.Pact),
        Profile("Fighter",CasterKind.None), new ClassProfile("Rogue",true,null,3,new[] {
            new SubclassProfile("ShadowCaster",true,true,Profile("Third",CasterKind.OneThird).Casting) }) });
    private static Result Calculate(params ClassInput[] classes) => Registry().Calculate(new Snapshot(classes.Sum(c=>c.ActualLevel), classes,
        classes.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel))));
    private static ClassInput C(string id,int n,bool compressed = true,string subclass = null) => new(id,n,compressed,subclass);
    private static Allocation Allocate(Tables tables, ResourceMode mode, params ClassInput[] classes) => new(tables,Calculate(classes),mode);
    private sealed class Actor { internal Actor Owner; internal bool WildShape; }
    internal static void Run(Action<bool,string> check, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        var flat = AddedIntegers(types.Single(t=>t.Name=="FeatureDefinitionCastSpellBuilder"),"SlotsByCasterLevel");
        var points = AddedIntegers(types.Single(t=>t.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel");
        var costs = AddedIntegers(types.Single(t=>t.Name=="SpellPointsContext"),"SpellCostByLevel");
        var rows = Enumerable.Range(1,20).Select(n=>flat.Skip(n*10).Take(9).ToArray()).ToArray();
        check(flat.Length==210 && points.Length==21 && costs.SequenceEqual(new[]{0,2,3,5,6,7,9,10,11,13}),"Standard resources compiled-table fixture: installed slot/SP/cost tables, not tabletop substitutes");
        var tables = new Tables(rows,points,costs);
        Examples(check,tables);
        EveryRow(check,tables,rows,points);
        PointTransitions(check,tables);
        SlotTransitions(check,tables);
        Modifiers(check,tables);
        Ownership(check,tables);
        Invalid(check,tables,rows,points,costs);
        Bindings(check,types);
    }
    private static void Examples(Action<bool,string> check,Tables tables)
    {
        var examples = new[] {
            ("two full",new[]{C("Sorcerer",12),C("Wizard",8)},31,20),
            ("full plus half",new[]{C("Sorcerer",8),C("Paladin",12)},21,20),
            ("two half",new[]{C("Ranger",12),C("Inventor",8)},16,16),
            ("full plus third",new[]{C("Wizard",8),C("Rogue",12,true,"ShadowCaster")},15,15),
            ("compressed/uncompressed",new[]{C("Cleric",12),C("Wizard",8,false)},28,20),
            ("three classes",new[]{C("Bard",8),C("Cleric",6),C("Fighter",6)},18,18),
            ("half without martial contribution",new[]{C("Ranger",12),C("Fighter",8)},10,10),
            ("no caster",new[]{C("Fighter",12)},0,0) };
        foreach(var x in examples)
        {
            var result=Calculate(x.Item2);
            foreach(var mode in new[]{ResourceMode.Slots,ResourceMode.SpellPoints})
            {
                var a=new Allocation(tables,result,mode); var b=new Budget(tables,a);
                check(result.IsValid && result.RawSharedCasterLevel==x.Item3 && a.CasterLevel==x.Item4,
                    $"Standard resources behavior {x.Item1}/{mode}: calculator contribution and bounded installed index");
                check(a.PointMaximum==tables.Points(x.Item4) && b.RemainingPoints==a.PointMaximum,
                    $"Standard resources behavior {x.Item1}/{mode}: one table-derived budget, no sum of class pools");
                check(result.Classes.All(c=>c.ActualClassLevel==x.Item2.Single(i=>i.ClassId==c.ClassId).ActualLevel) &&
                    result.Classes.Where(c=>c.Kind==CasterKind.None).All(c=>c.EffectiveCastingLevel==0),
                    $"Standard resources behavior {x.Item1}/{mode}: actual levels and noncaster contribution preserved");
            }
        }
        check(Allocate(tables,ResourceMode.SpellPoints,C("Paladin",1,false)).PointMaximum==0 &&
            Allocate(tables,ResourceMode.SpellPoints,C("Inventor",1,false)).PointMaximum==4,
            "Standard resources behavior unlock: original Half1 has no casting; HalfRoundUp1 uses first resource row");
        var saturation=Calculate(C("Cleric",12),C("Wizard",8));
        check(new Allocation(tables,saturation,ResourceMode.SpellPoints).PointMaximum==133 && saturation.Classes.Single(c=>c.ClassId=="Wizard").MaximumSpellCircle==6,
            "Standard resources behavior saturation: shared133 maximum does not raise Wizard8 compressed independent circle6 access");
        check(Allocate(tables,ResourceMode.SpellPoints,C("Ranger",12)).PointMaximum==64 &&
            Allocate(tables,ResourceMode.SpellPoints,C("Inventor",12)).MaximumCircle==5,
            "Standard resources behavior half-caster20:64 SP and circle5, no full-caster budget");
        check(Reject(()=>Allocate(tables,ResourceMode.Slots,C("Warlock",12),C("Wizard",8))),
            "Standard resources behavior: Pact participant explicitly rejected, never silently merged");
    }
    private static void EveryRow(Action<bool,string> check,Tables tables,int[][] rows,int[] points)
    {
        for(var n=0;n<=20;n++)
        foreach(var mode in new[]{ResourceMode.Slots,ResourceMode.SpellPoints})
        {
            var a=Allocate(tables,mode,C("Wizard",n,false)); var b=new Budget(tables,a);
            check(a.CasterLevel==n && a.PointMaximum==points[n],$"Standard resources behavior row{n}/{mode}: exact installed resource row");
            for(var c=1;c<=9;c++)
            {
                var count=n==0?0:rows[n-1][c-1];
                check(a.Slots[c-1]==(mode==ResourceMode.Slots?count:count>0?1:0),$"Standard resources behavior row{n}/{mode}/circle{c}: capacity");
                var expected=mode==ResourceMode.Slots?count:(count>0 && points[n]>=tables.Cost(c)?1:0);
                check(b.RemainingSlots(c)==expected,$"Standard resources behavior row{n}/{mode}/circle{c}: affordability/slot availability");
            }
        }
    }
    private static void PointTransitions(Action<bool,string> check,Tables tables)
    {
        var a=Allocate(tables,ResourceMode.SpellPoints,C("Cleric",12),C("Wizard",8)); var b=new Budget(tables,a);
        check(b.Spend("ninth",9) && b.RemainingPoints==120 && b.HighCircleSpent(9) && !b.CanSpend(9),"Standard resources behavior SP: ninth-circle expenditure13 and once-per-long-rest restriction");
        check(b.Spend("ninth",9) && b.RemainingPoints==120,"Standard resources behavior SP: one spell action observed twice debits once");
        check(Reject(()=>b.Spend("ninth",8)),"Standard resources behavior SP: contradictory operation identity rejected");
        foreach(var c in new[]{6,7,8})
            check(b.Spend("high"+c,c) && !b.CanSpend(c),$"Standard resources behavior SP: independent high-circle{c} limit");
        var before=b.RemainingPoints;
        b.Rest("short",false); b.Rest("simulated",true,true);
        check(b.RemainingPoints==before && !b.CanSpend(9),"Standard resources behavior SP: ordinary short rest and simulated long rest do not recover");
        b.RecoverPoints("recovery",100); b.RecoverPoints("recovery",100);
        check(b.RemainingPoints==133 && !b.CanSpend(9) && !b.CanSpend(6),"Standard resources behavior SP: capped recovery cannot reset high-circle permissions");
        for(var i=0;i<66;i++) check(b.Spend("low"+i,1),$"Standard resources behavior SP low expenditure{i}: affordable");
        check(b.RemainingPoints==1 && !b.Spend("exhausted",1) && b.RemainingPoints==1 && !b.CanSpend(2),"Standard resources behavior SP: exhaustion fails without negative budget/free upcast");
        b.Refresh(a); b.Refresh(a);
        check(b.RemainingPoints==1 && !b.CanSpend(9),"Standard resources behavior SP: repeated refresh preserves expenditure and high-circle history");
        b.Rest("long",true);
        check(b.RemainingPoints==133 && b.CanSpend(9) && b.CanSpend(6),"Standard resources behavior SP: long rest restores one standard pool and all high-circle permissions");
        b.Spend("after-rest",1); b.Rest("long",true);
        check(b.RemainingPoints==131 && b.Spend("ninth",9) && b.RemainingPoints==131,"Standard resources behavior SP: repeated rest/action callbacks do not refill/debit after a rest");
        check(b.Spend("cantrip",0) && b.RemainingPoints==131,"Standard resources behavior: cantrip expenditure is free");
        check(b.Spend("cancelled-high",9) && b.Refund("cancelled-high") && !b.Refund("cancelled-high") && b.CanSpend(9) && b.RemainingPoints==131,
            "Standard resources behavior cancellation: exactly one rollback restores points and high-circle permission");
        check(!b.Spend("cancelled-high",9) && !b.Refund("unknown") && !b.Refund("ninth"),
            "Standard resources behavior cancellation: cancelled action cannot recommit; unknown or previous-rest refund rejected");
        b.Recover("slot-recovery",new Dictionary<int,int>{{1,1}});
        check(b.RemainingPoints==133,"Standard resources behavior SP: native selected-slot recovery converts via installed costs exactly once");
        var affordable=new Budget(tables,Allocate(tables,ResourceMode.SpellPoints,C("Wizard",8)));
        for(var i=0;i<36;i++) affordable.Spend("affordability"+i,1);
        check(affordable.RemainingPoints==1 && !affordable.CanSpend(6) && !affordable.HighCircleSpent(6),
            "Standard resources behavior SP: unaffordable high circle is not recorded as actually spent");
        affordable.Recover("recover-affordability",new Dictionary<int,int>{{3,2}});
        affordable.Refresh(Allocate(tables,ResourceMode.SpellPoints,C("Wizard",8)));
        check(affordable.RemainingPoints==11 && affordable.CanSpend(6),
            "Standard resources behavior SP: recovery reopens affordable unused high circle without restoring an already-used one");
        affordable.Rest("simulation-then-real",true,true); affordable.Rest("simulation-then-real",true);
        check(affordable.RemainingPoints==73,"Standard resources behavior rest: simulated callback does not consume the real-rest identity");
    }
    private static void SlotTransitions(Action<bool,string> check,Tables tables)
    {
        var a=Allocate(tables,ResourceMode.Slots,C("Wizard",12),C("Cleric",8)); var b=new Budget(tables,a);
        check(b.RemainingSlots(1)==4 && b.RemainingSlots(6)==2 && b.RemainingSlots(9)==1,"Standard resources behavior slots: shared level20 installed capacity, ordinary high slots retain count2");
        check(b.Spend("upcast",9) && b.RemainingSlots(9)==0 && b.RemainingSlots(1)==4 && !b.CanSpend(9),"Standard resources behavior slots: upcasting spends selected resource circle only");
        check(b.Spend("upcast",9) && b.RemainingSlots(9)==0 && !b.Spend("another-ninth",9),"Standard resources behavior slots: repeated action idempotent and exhausted slot rejected");
        check(b.Spend("six1",6) && b.Spend("six2",6) && !b.Spend("six3",6),"Standard resources behavior slots: ordinary circle6 allows native two slots, no SP once-per-circle rule");
        b.Rest("short",false); b.Refresh(a);
        check(b.RemainingSlots(6)==0 && b.RemainingSlots(9)==0,"Standard resources behavior slots: short rest/refresh do not refill standard slots");
        b.Recover("arcane-recovery",new Dictionary<int,int>{{6,1}}); b.Recover("arcane-recovery",new Dictionary<int,int>{{6,1}});
        check(b.RemainingSlots(6)==1 && b.RemainingSlots(9)==0,"Standard resources behavior slots: selected recovery applied once, other circle unchanged");
        check(b.Spend("cancelled-slot",6) && b.Refund("cancelled-slot") && !b.Refund("cancelled-slot") && b.RemainingSlots(6)==1,
            "Standard resources behavior slots: explicit cancellation refunds once without adding a slot");
        check(Reject(()=>b.Recover("arcane-recovery",new Dictionary<int,int>{{6,2}})),
            "Standard resources behavior recovery: conflicting duplicate callback rejected");
        b.Rest("sim",true,true);
        check(b.RemainingSlots(6)==1,"Standard resources behavior slots: rest simulation preserves budget");
        b.Rest("long",true);
        check(b.RemainingSlots(6)==2 && b.RemainingSlots(9)==1,"Standard resources behavior slots: long rest restores shared capacities");
        var lower=Allocate(tables,ResourceMode.Slots,C("Wizard",8,false)); var growing=new Budget(tables,lower);
        growing.Spend("spent-before-level",1); growing.Refresh(a);
        check(growing.RemainingSlots(1)==3,"Standard resources behavior slots: advancement increases capacities without forgetting original expenditure");
        growing.Spend("spent-ninth",9); growing.Refresh(lower); growing.Refresh(a);
        check(growing.RemainingSlots(9)==0,"Standard resources behavior slots: temporarily unavailable circle retains debt on refresh");
    }
    private static void Modifiers(Action<bool,string> check,Tables tables)
    {
        var p=Calculate(C("Wizard",12),C("Cleric",8));
        var extra=new Modifier("item-slot",2,1); var a=new Allocation(tables,p,ResourceMode.Slots,new[]{extra,extra}); var b=new Budget(tables,a);
        var distinct=new Allocation(tables,p,ResourceMode.Slots,new[]{new Modifier("condition:GUID1",2,1),new Modifier("condition:GUID2",2,1)});
        check(distinct.Slots[1]==5,"Standard resources behavior temporary slots: two independent condition GUIDs preserve both grants");
        check(a.Slots[1]==4,"Standard resources behavior modifier: identical source seen in two repertoires grants one additional slot");
        for(var i=0;i<4;i++) b.Spend("modified"+i,2);
        b.Refresh(new Allocation(tables,p,ResourceMode.Slots)); b.Refresh(a);
        check(b.RemainingSlots(2)==0,"Standard resources behavior modifier: removing/readding capacity does not erase spent slots");
        check(Reject(()=>new Allocation(tables,p,ResourceMode.Slots,new[]{extra,new Modifier("item-slot",2,2)})),"Standard resources behavior modifier: conflicting source identity rejected");
        var sp=new Budget(tables,new Allocation(tables,p,ResourceMode.SpellPoints,new[]{new Modifier("power-pool",pointCapacity:5)}));
        check(sp.RemainingPoints==138 && sp.Current.BasePointMaximum==133,"Standard resources behavior modifier: explicit native power modifier added once to the one base pool");
        sp.GrantTemporarySlots("condition-guid:slot",3,2); sp.GrantTemporarySlots("condition-guid:slot",3,2);
        check(sp.Current.PointMaximum==138 && sp.RemainingPoints==148,"Standard resources behavior temporary slots: once-only10 point credit above maximum, not another permanent pool");
        sp.Spend("spend-credit",3); sp.Spend("spend-credit2",3);
        check(sp.RemainingPoints==138,"Standard resources behavior temporary slots: bonus credit spent before base pool");
        sp.Spend("spend-base",3); sp.Refresh(new Allocation(tables,p,ResourceMode.SpellPoints));
        check(sp.RemainingPoints==128,"Standard resources behavior modifier: capacity removal preserves base expenditure");
        sp.Rest("long",true); sp.GrantTemporarySlots("condition-guid:slot",3,2);
        check(sp.RemainingPoints==133,"Standard resources behavior temporary slots: stale removed-condition callback cannot regrant after long rest");
        check(Reject(()=>sp.GrantTemporarySlots("condition-guid:slot",3,3)),"Standard resources behavior temporary slots: conflicting condition identity rejected");
        check(Reject(()=>sp.Refresh(new Allocation(tables,p,ResourceMode.Slots))),"Standard resources behavior settings: resource-mode change rejected rather than migrating live state");
    }
    private static void Ownership(Action<bool,string> check,Tables tables)
    {
        var hero=new Actor(); var shape=new Actor{Owner=hero,WildShape=true}; var summon=new Actor{Owner=hero};
        var other=new Actor(); var store=new OwnerStore<Actor>(a=>a.Owner==null?a:a.WildShape?a.Owner:null);
        var b=new Budget(tables,Allocate(tables,ResourceMode.SpellPoints,C("Druid",12),C("Wizard",8)));
        store.Bind(hero,b);
        check(store.TryGet(shape,out var same) && ReferenceEquals(same,b),"Standard resources behavior owner store: recognized Wild Shape resolves original hero ledger");
        same.Spend("shape-spell",5);
        check(store.TryGet(hero,out var original) && original.RemainingPoints==126 && ReferenceEquals(same,original),"Standard resources behavior owner store: form expenditure survives reversion without a second SP pool");
        check(!store.TryGet(summon,out _) && !store.TryGet(other,out _) && !store.TryGet(null,out _),"Standard resources behavior owner store: unrelated summon/hero/null cannot borrow the budget");
        check(Reject(()=>store.Bind(shape,b)) && Reject(()=>store.Bind(hero,b)),"Standard resources behavior owner store: form binding and repeated hero initialization rejected");
        for(var i=0;i<10;i++) same.Refresh(Allocate(tables,ResourceMode.SpellPoints,C("Druid",12),C("Wizard",8)));
        check(store.TryGet(shape,out var refreshed) && ReferenceEquals(refreshed,b) && refreshed.RemainingPoints==126,"Standard resources behavior owner store: repeated repertoire refresh cannot duplicate/refill shared resource");
    }
    private static void Invalid(Action<bool,string> check,Tables tables,int[][] rows,int[] points,int[] costs)
    {
        check(Reject(()=>new Tables(rows.Take(19),points,costs)) && Reject(()=>new Tables(rows,points.Take(20),costs)),"Standard resources behavior validation: missing resource rows rejected");
        var invalid=Registry().Calculate(new Snapshot(1,new[]{C("Unknown",1)},new[]{"Unknown"}));
        check(Reject(()=>new Allocation(tables,invalid,ResourceMode.SpellPoints)),"Standard resources behavior validation: invalid progression cannot allocate resources");
        var b=new Budget(tables,Allocate(tables,ResourceMode.SpellPoints,C("Wizard",8)));
        check(Reject(()=>b.Spend("",1)) && Reject(()=>b.Spend("circle10",10)) && Reject(()=>b.RecoverPoints("negative",-1)),"Standard resources behavior validation: missing operation identity/invalid circle/negative recovery rejected");
        var copiedRows=rows.Select(r=>r.ToArray()).ToArray(); var copiedPoints=points.ToArray(); var copiedCosts=costs.ToArray();
        var immutable=new Tables(copiedRows,copiedPoints,copiedCosts); copiedRows[19][0]=99; copiedPoints[20]=999; copiedCosts[9]=999;
        var a=Allocate(immutable,ResourceMode.SpellPoints,C("Wizard",12),C("Cleric",8));
        check(a.PointMaximum==133 && immutable.Cost(9)==13 && immutable.SlotRow(20)[0]==4,"Standard resources behavior: resource tables defensively copied, no shared mutable native rows");
    }
    private static void Bindings(Action<bool,string> check,TypeDefinition[] types)
    {
        var context=types.Single(t=>t.Name=="StandardMulticasterResourcesContext");
        bool Calls(MethodDefinition m,string type,string method) => m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name==type && r.Name==method);
        var gate=context.Methods.Single(m=>m.Name=="get_RuntimeEnabled");
        check(gate.Body.Instructions.Any(i=>i.OpCode.Code==Code.Ldc_I4_0) && !gate.Body.Instructions.Any(i=>i.Operand is MethodReference),"Standard resources compiled binding: runtime gate permanently false; no setting enables mixed gameplay in13.3");
        check(context.Methods.Single(m=>m.Name=="TryBinding").Body.Instructions.Take(8).Any(i=>i.Operand is MethodReference r && r.Name=="get_RuntimeEnabled"),"Standard resources compiled binding: native adapter exits before touching hero state with gate off");
        var caller=types.SelectMany(t=>t.Methods).Where(m=>Calls(m,context.Name,"BindNewHero")).ToArray();
        check(caller.Length==0,"Standard resources compiled binding: no existing save/new class admission automatically binds or converts a hero");
        var boot=Types(types.Single(t=>t.Name=="BootContext")).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions)
            .Select(i=>i.Operand).OfType<MethodReference>().Where(m=>m.Name=="LateLoad" || m.Name=="CaptureInstalledTables").Select(m=>m.DeclaringType.Name).ToArray();
        check(Array.IndexOf(boot,"SpellPointsContext")<Array.IndexOf(boot,context.Name) && Array.IndexOf(boot,context.Name)<Array.IndexOf(boot,"FighterProgressionContext"),"Standard resources compiled binding: original tables captured after final SP/rule switches, before class compression");
        var character=types.Single(t=>t.Name=="RulesetCharacterPatcher"); var repertoire=types.Single(t=>t.Name=="RulesetSpellRepertoirePatcher");
        check(character.NestedTypes.Single(t=>t.Name=="RecoverMissingSlots_Patch").Methods.Single(m=>m.Name=="Prefix").ReturnType.FullName=="System.Void",
            "Standard resources compiled binding OFF regression: original void recovery prefix semantics retained for Harmony cooperation");
        check(character.NestedTypes.Single(t=>t.Name=="RecoverMissingSlots_Patch").Methods.Single(m=>m.Name=="ResourcePrefix").Body.Instructions
            .Any(i=>i.Operand is MethodReference r && r.Name=="get_SpellRepertoireSlotsRecovered"),
            "Standard resources compiled binding: native owner recovery notification retained without a second recovery loop");
        var hooks=new[]{(character,"RefreshSpellRepertoires_Patch","PrepareRefresh"),(character,"RefreshSpellRepertoires_Patch","Refresh"),
            (character,"SpendSpellSlot_Patch","EnterSpell"),(repertoire,"SpendSpellSlot_Patch","TrySpend"),
            (character,"RecoverMissingSlots_Patch","Recover"),(repertoire,"RecoverMissingSlots_Patch","Recover"),
            (character,"ApplyRest_Patch","FinishRest"),(character,"ApplyRest_Patch","SuppressNativeRestoration")};
        foreach(var h in hooks) check(h.Item1.NestedTypes.Single(t=>t.Name==h.Item2).Methods.Any(m=>Calls(m,context.Name,h.Item3)),
            $"Standard resources compiled binding: {h.Item1.Name}/{h.Item2} routes {h.Item3} through closed adapter");
        var shared=types.Single(t=>t.Name=="SharedSpellsContext"); var sp=types.Single(t=>t.Name=="SpellPointsContext");
        check(Calls(shared.Methods.Single(m=>m.Name=="GetSharedCasterLevel"),context.Name,"TryCasterLevel") && Calls(shared.Methods.Single(m=>m.Name=="GetSharedSpellLevel"),context.Name,"TryMaximumCircle"),"Standard resources compiled binding: bounded shared caster level and circle queries integrated");
        foreach(var name in new[]{"GetMaxSpellPoints","GetRemainingSpellPoints","AddSpellPoints","CanCastSpellOfLevel"})
            check(sp.Methods.Single(m=>m.Name==name).Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name==context.Name),$"Standard resources compiled binding: {name} adapter, legacy body retained");
        var druid=types.Single(t=>t.Name=="DruidWildshapeContext").Methods.Single(m=>m.Name=="StandardResourceOwner");
        check(!druid.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.Name=="IsSupportedHero") && druid.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="Catalog"),"Standard resources compiled binding: owner resolver uses Wild Shape catalog, not single-caster admission or arbitrary summons");
        check(!context.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.OpCode.Code==Code.Newobj && i.Operand is MethodReference r && (r.DeclaringType.Name=="RulesetUsablePower" || r.DeclaringType.Name=="FeatureDefinitionPower")),"Standard resources compiled binding: no new PowerSpellPoints definition/usable pool constructed by adapter");
    }
}
