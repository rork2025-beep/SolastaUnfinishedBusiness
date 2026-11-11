using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using Tx = SolastaUnfinishedBusiness.Models.WarlockInvocationTransaction<string>;

// Runs the actual production transaction validator/journal. Native adapter integration is inspected, not executed.
internal static class WarlockInvocationTransactionTests
{
    private static Tx.Event Event(int original, string removed = null, string added = null, int awards = 0, int replacements = 1,
        string pool = "Class") => new(pool+original, original, awards, replacements,
            removed == null ? Array.Empty<string>() : new[] { removed }, added == null ? Array.Empty<string>() : new[] { added });
    private static bool Reject(Action action)
    { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    private static Tx Chain() => new(new[] { "A" }, new[] { Event(18,"A","B"), Event(19,"B","C"), Event(20,"C","D") });
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition method, string name, string owner = null) => method.HasBody &&
        method.Body.Instructions.Any(x=>x.Operand is MethodReference r && r.Name == name && (owner == null || r.DeclaringType.Name == owner));
    internal static void Run(Action<bool,string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var tx=Chain(); var frames=tx.Prepare();
        check(frames.Select(x=>x.Event.Original).SequenceEqual(new[] { 18,19,20 }) && frames.Select(x=>x.Event.Origin).SequenceEqual(new[] { "Class18","Class19","Class20" }), "Invocation transaction: three independent origin events, not only a final set");
        check(frames[0].Before.SequenceEqual(new[] { "A" }) && frames[0].After.SequenceEqual(new[] { "B" }) && frames[1].Before.SequenceEqual(new[] { "B" }) && frames[1].After.SequenceEqual(new[] { "C" }) && frames[2].After.SequenceEqual(new[] { "D" }), "Invocation transaction: A to B to C to D virtual inventory at every original event");
        var hero=new List<string> { "A" }; var trace=new List<string>();
        void Apply(Tx.Event e) { trace.Add(e.Origin); foreach(var x in e.Removed) hero.Remove(x); hero.AddRange(e.Added); }
        check(tx.ApplyPrepared(Apply,()=>{hero.Clear();hero.Add("A");}) && hero.SequenceEqual(new[] { "D" }) && trace.SequenceEqual(new[] { "Class18","Class19","Class20" }), "Invocation transaction: prepared journal applies in original order");
        check(!tx.ApplyPrepared(_=>throw new Exception("must not run"),()=>throw new Exception("must not run")) && trace.Count==3 && hero.Count==1, "Invocation transaction: repeated finalization is idempotent with no second grant");
        var back=new Tx(new[] { "A" },new[] { Event(18,"A","B"),Event(19,"B","A"),Event(20) });
        check(back.Prepare().Last().After.SequenceEqual(new[] { "A" }), "Invocation transaction: later legal reacquisition A to B to A");
        check(new Tx(new[] { "A" },new[] { Event(18),Event(19),Event(20) }).Prepare().Last().After.SequenceEqual(new[] { "A" }), "Invocation transaction: Skip optional replacements yields no free invocation");
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,null,"B") }).Prepare()), "Invocation transaction: skipped removal cannot fund an acquisition");
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,"Unknown","B") }).Prepare()), "Invocation transaction: unknown removal rejected at exact event");
        check(Reject(()=>new Tx(new[] { "A","B" },new[] { Event(18,"A","B") }).Prepare()), "Invocation transaction: acquisition of known invocation rejected");
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,"A","B"),Event(19,"A","C") }).Prepare()), "Invocation transaction: cannot remove A again after earlier event removed it");
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,"B","C"),Event(19,"A","B") }).Prepare()), "Invocation transaction: future acquisition cannot fund an earlier removal");
        bool Level(string item,int original,IReadOnlyList<string> known) => item!="Late19" || original>=19;
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,"A","Late19") }).Prepare(Level)), "Invocation transaction: original18 cannot use original19 prerequisite despite effective20");
        check(new Tx(new[] { "A" },new[] { Event(18),Event(19,"A","Late19"),Event(20) }).Prepare(Level).Last().After.Single()=="Late19", "Invocation transaction: prerequisite unlocks on correct original event");
        bool Pact(string item,int original,IReadOnlyList<string> known) => item!="RequiresBlade" || known.Contains("PactBlade");
        var pactEvents=new[] { Event(18,null,"PactBlade",1),Event(19,null,"RequiresBlade",1),Event(20) };
        check(new Tx(new[] { "A" },pactEvents).Prepare(Pact).Last().After.Contains("RequiresBlade"), "Invocation transaction: pending Pact Blade from prior event satisfies dependency");
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,null,"RequiresBlade",1),Event(19,null,"PactBlade",1) }).Prepare(Pact)), "Invocation transaction: future Pact selection cannot satisfy earlier requirement");
        check(Reject(()=>new Tx(new[] { "PactBlade","RequiresBlade" },new[] { Event(18,"PactBlade","Other") }).Prepare(Pact)), "Invocation transaction: removing Pact cannot orphan a retained invocation");
        foreach(var pact in new[] { "PactBlade","PactChain","PactTome" })
            check(new Tx(Array.Empty<string>(),new[] { Event(3,null,pact,1),Event(4,null,"NeedsPact",1) })
                .Prepare((item,n,known)=>item!="NeedsPact" || known.Contains(pact)).Last().After.Count==2, "Invocation transaction: generic journal preserves dependency identity: "+pact);
        var spellEvents=new Dictionary<int,string[]> { [18]=new[] { "Hex" },[19]=new[] { "EldritchBlast" },[20]=new[] { "EldritchBlast" } };
        bool Spell(string item,int original,IReadOnlyList<string> known) => item!="AgonizingBlast" || spellEvents[original].Contains("EldritchBlast");
        check(new Tx(new[] { "A" },new[] { Event(18),Event(19,"A","AgonizingBlast"),Event(20) }).Prepare(Spell).Last().After.Single()=="AgonizingBlast", "Invocation transaction: pending known-spell state checked for original19");
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,"A","AgonizingBlast"),Event(19) }).Prepare(Spell)), "Invocation transaction: future spell learning does not leak into original18");
        var edit=Chain();
        check(edit.InvalidatedBy(18,false).Select(x=>x.Origin).SequenceEqual(new[] { "Class19","Class20" }) && edit.InvalidatedBy(19,true).Select(x=>x.Origin).SequenceEqual(new[] { "Class19","Class20" }), "Invocation transaction: production invalidation identifies dependent later events and optional current acquisitions");
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,"A","Changed"),Event(19,"B","C"),Event(20,"C","D") }).Prepare()), "Invocation transaction: changing earlier acquisition invalidates dependent later replacements");
        var choices=new Dictionary<string,Tx.Event>(edit.Events.ToDictionary(x=>x.Origin));
        foreach(var e in edit.InvalidatedBy(18,false)) choices[e.Origin]=Event(e.Original);
        choices["Class18"]=Event(18,"A","Changed");
        check(new Tx(new[] { "A" },choices.Values).Prepare().Last().After.Single()=="Changed", "Invocation transaction: Back/Reset clears dependent events instead of committing stale removals");
        check(Chain().Prepare().Last().After.Single()=="D", "Invocation transaction: Undo then redo reconstructs legal dependent replacements");
        var initial=new List<string> { "A" }; var selected=new List<string> { "B" };
        var immutable=new Tx(initial,new[] { new Tx.Event("Class18",18,0,1,new[] { "A" },selected) });
        selected.Clear();initial.Add("Unrelated");
        check(immutable.Prepare().Single().After.SequenceEqual(new[] { "B" }) && initial.SequenceEqual(new[] { "A","Unrelated" }), "Invocation transaction: selection/source snapshots copied; cancellation without Apply does not mutate hero");
        var incomplete=new Tx(new[] { "A" },new[] { Event(18,"A"),Event(19),Event(20) });
        var mutations=0;
        check(Reject(()=>incomplete.Prepare()) && Reject(()=>incomplete.ApplyPrepared(_=>mutations++,()=>mutations++)) && mutations==0, "Invocation transaction: incomplete cancellation/finalization validation causes zero hero mutation");
        check(new Tx(new[] { "A" },new[] { Event(18,"A") }).Replay(false).Single().After.Count==0, "Invocation transaction: incomplete current event can be previewed before choosing replacement");
        check(Reject(()=>incomplete.Before(19)), "Invocation transaction: cannot advance past incomplete prior event");
        var multi=new Tx(new[] { "A" },new[] { new Tx.Event("Class18",18,2,1,new[] { "A" },new[] { "B","Award1","Award2" }),Event(19,"B","C"),Event(20,"C","D") });
        check(multi.Prepare().Last().After.SequenceEqual(new[] { "Award1","Award2","D" }), "Invocation transaction: independent multiple awards retained while replacement chain advances");
        foreach(var revised in new[] { false,true })
        {
            var awards=new int[20]; awards[1]=2;awards[4]=revised?2:1;
            foreach(var n in new[] { 7,9,12,15,18 }) awards[n-1]=1;
            if(revised) awards[0]=1;
            var events=new List<Tx.Event>();
            for(var original=1;original<=20;original++) events.Add(new Tx.Event("Class"+original,original,awards[original-1],WarlockProgressionRules.InvocationReplacementBudget(revised,original),Array.Empty<string>(),Enumerable.Range(0,awards[original-1]).Select(i=>"Invocation"+original+"_"+i)));
            var journal=new Tx(Array.Empty<string>(),events).Prepare();
            check(journal.Last().After.Count==(revised?10:8) && journal.Count==20, "Invocation transaction: real2014/2024 budgets retain all twenty events: "+revised);
            check(events.Sum(x=>x.Replacements)==(revised?19:18), "Invocation transaction: optional replacement budgets preserved: "+revised);
        }
        var featKnown=new Tx(new[] { "FeatEldritchAdeptInvocation","A" },new[] { Event(18,"A","B"),Event(19,"B","C"),Event(20) });
        check(featKnown.Prepare().Last().After.Contains("FeatEldritchAdeptInvocation"), "Invocation transaction: already confirmed Eldritch Adept choice remains independent inventory");
        var classAndFeat=new Tx(new[] { "A" },new[] { Event(18,"A","B"),Event(19),Event(20,null,"FeatChoice",1,0,"FeatEldritchAdept") });
        check(classAndFeat.Prepare().Last().After.SequenceEqual(new[] { "B","FeatChoice" }), "Invocation transaction: feat-origin award has distinct origin/budget and is not replacement-funded");
        check(Reject(()=>new Tx(new[] { "A" },new[] { Event(18,"A","B"),Event(20,null,"B",1,0,"FeatEldritchAdept") }).Prepare()), "Invocation transaction: same invocation cannot be granted from class and feat origins");
        var custom=new Tx(new[] { "VersatilityA" },new[] { Event(18,"VersatilityA","VersatilityB",0,1,"EldritchSurge"),Event(19,"VersatilityB","VersatilityC",0,1,"EldritchSurge") });
        check(custom.Prepare().Last().After.Single()=="VersatilityC" && custom.Events.All(x=>x.Origin.StartsWith("EldritchSurge")), "Invocation transaction: production journal supports independent custom pool events without merging ordinary pool");
        check(Reject(()=>new Tx(new[] { "OrdinaryA" },custom.Events).Prepare()), "Invocation transaction: custom pool cannot consume unrelated ordinary inventory");
        var actualTotal=12;
        check(Reject(()=>new Tx(Array.Empty<string>(),new[] { Event(20,null,"CustomRequiresActual15",1,0,"Custom") }).Prepare((item,original,known)=>actualTotal>=15)),
            "Invocation transaction: character-level custom prerequisite is not silently compressed to effective20");
        var failing=Chain(); failing.Prepare(); var target=new List<string> { "A" }; var extras=new Dictionary<string,List<string>> { ["Tome"]=new() { "Cantrip" } }; var rolls=0;
        check(Reject(()=>failing.ApplyPrepared(e=>{target.Clear();target.AddRange(e.Added);extras.Clear();if(e.Original==19)throw new InvalidOperationException("Injected adapter failure");},()=>{target.Clear();target.Add("A");extras["Tome"]=new() { "Cantrip" };rolls++;})) && target.SequenceEqual(new[] { "A" }) && extras["Tome"].Single()=="Cantrip" && rolls==1,
            "Invocation transaction: injected application failure rolls back inventory and extra spell allocation");
        check(failing.ApplyPrepared(e=>{target.Clear();target.AddRange(e.Added);},()=>{target.Clear();target.Add("A");}) && target.Single()=="D", "Invocation transaction: journal supports clean retry after successful rollback (runtime uses stricter cancel/reload guard)");
        var poisoned=Chain();poisoned.Prepare();
        check(Reject(()=>poisoned.ApplyPrepared(_=>throw new InvalidOperationException(),()=>throw new InvalidOperationException())) && Reject(()=>poisoned.ApplyPrepared(_=>{},()=>{})), "Invocation transaction: failed rollback poisons journal and forbids another application");
        check(Reject(()=>tx.Prepare()), "Invocation transaction: closed journal cannot be re-prepared against a different draft");
        var badBudget=new Tx(new[] { "A" },new[] { new Tx.Event("Class18",18,0,1,new[] { "A","A" },new[] { "B","C" }) });
        check(Reject(()=>badBudget.Prepare()), "Invocation transaction: repeated removal cannot exceed one event budget");
        var distinct=new Tx(new[] { "A" },new[] { Event(18,"A","B"),Event(19,"B","A"),Event(20,"A","B") });
        check(distinct.Prepare().Last().After.Single()=="B" && distinct.Events.Sum(x=>x.Added.Count)==3,
            "Invocation transaction: repeated same definition across events retains three acquisitions and correct final identity");
        var revalidated=Chain();revalidated.Prepare();var staleMutations=0;
        check(Reject(()=>revalidated.Prepare((item,n,known)=>false)) && Reject(()=>revalidated.ApplyPrepared(_=>staleMutations++,()=>staleMutations++)) && staleMutations==0,
            "Invocation transaction: failed re-validation invalidates previously prepared journal before any mutation");
        check(Reject(()=>new Tx(Array.Empty<string>(),new[] { new Tx.Event("Class18",18,2,0,Array.Empty<string>(),new[] { "B","B" }) }).Prepare()),
            "Invocation transaction: duplicate new acquisitions within one original event rejected");
        check(Reject(()=>new Tx(new[] { "A" },new[] { new Tx.Event("Class18",18,0,2,new[] { "A","A" },new[] { "B","C" }) }).Prepare()),
            "Invocation transaction: same known invocation cannot fund two replacements even with a larger pool");
        var off=new FighterProgressionInitialization(); var ordinary=new List<string> { "A" };
        if(off.ShouldInitialize(false)) Chain().ApplyPrepared(_=>ordinary.Clear(),()=>{});
        check(ordinary.SequenceEqual(new[] { "A" }) && !off.ShouldInitialize(true), "Invocation transaction: disabled initialization remains latched and untouched");
        CheckBindings(check,game,mod);
    }
    private static void CheckBindings(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();
        var context=types.Single(x=>x.Name=="WarlockInvocationContext");
        check(Calls(context.Methods.Single(x=>x.Name=="BeginFinalize"),"ValidateComplete") && Calls(context.Methods.Single(x=>x.Name=="BeginFinalize"),"Prepare"), "Invocation transaction: native acceptance uses the tested production journal/validator (metadata)");
        check(Calls(types.Single(x=>x.Name=="FinalizeCharacter_Patch").Methods.Single(x=>x.Name=="Prefix"),"BeginFinalize"), "Invocation transaction: acceptance guard precedes existing UB finalization prefix work (metadata)");
        var native=game.MainModule.Types.Single(x=>x.Name=="CharacterBuildingManager").Methods.Single(x=>x.Name=="FinalizeCharacter");
        check(native.Body.Instructions.Count(x=>x.Operand is MethodReference m && m.Name=="TrainInvocations" && m.DeclaringType.Name=="RulesetCharacterHero")==1 && native.Body.Instructions.Count(x=>x.Operand is MethodReference m && m.Name=="UnlearnInvocations" && m.DeclaringType.Name=="RulesetCharacterHero")==1, "Invocation transaction: native finalization has exactly the two adapter call sites (metadata)");
        check(Calls(context.Methods.Single(x=>x.Name=="ApplyOrPass"),"ApplyPrepared") && types.Single(x=>x.Name=="FinalizationPatch").Methods.Any(x=>x.Name=="Finalizer"), "Invocation transaction: ordered once-only adapter and rollback finalizer present (metadata)");
        check(Calls(context.Methods.Single(x=>x.Name=="HasPact"),"GetInvocationTag") && Calls(context.Methods.Single(x=>x.Name=="KnowsSpell"),"KnowsAtOriginal"), "Invocation transaction: pending Pact and spell prerequisites have scoped production inventory adapters (metadata)");
        check(Types(context).SelectMany(x=>x.Methods).Any(x=>Calls(x,"InvalidatedBy")), "Invocation transaction: Back/Undo invalidation uses tested production origin selection (metadata)");
        check(Calls(context.Methods.Single(x=>x.Name=="CanTrain"),"ChoiceCollision") &&
            Calls(context.Methods.Single(x=>x.Name=="BeforeUntrain"),"ClearChoiceSelections"),
            "Invocation transaction: shared native choice-pool collision checked before allocation; undo cleans typed pending selections (metadata)");
        var builder=types.Single(x=>x.Name=="InvocationsBuilders");
        check(builder.Methods.Any(x=>x.Name=="ValidateHex") && builder.Methods.Any(x=>x.Name.StartsWith("<BuildDevouringBlade>g__Validate")),
            "Invocation transaction: inspected Hex/Devouring Blade validator delegates have exact compiled identities (metadata)");
        check(Calls(context.Methods.Single(x=>x.Name=="ValidateCustomPools"),"Prepare") && Calls(context.Methods.Single(x=>x.Name=="ValidateCustomPools"),"get_TrainedInvocations"),
            "Invocation transaction: custom pool acceptance validates actual pool inventory with production engine (metadata)");
        check(Calls(context.Methods.Single(x=>x.Name=="CustomPrerequisites"),"TryGetAttributeValue") && Calls(context.Methods.Single(x=>x.Name=="CustomPrerequisites"),"GetClassOrSubclassLevel"),
            "Invocation transaction: custom prerequisites retain actual character vs class rank distinction (metadata)");
        var prefix=types.Single(x=>x.Name=="FinalizeCharacter_Patch").Methods.Single(x=>x.Name=="Prefix");
        var calls=prefix.Body.Instructions.Where(x=>x.Operand is MethodReference).Select(x=>(MethodReference)x.Operand).ToList();
        check(calls.FindIndex(x=>x.Name=="BeginFinalize")<calls.FindIndex(x=>x.Name=="GrantRaceFeatures") &&
            types.Single(x=>x.Name=="FinalizeCharacter_Patch").Methods.Single(x=>x.Name=="Postfix").Parameters.Any(x=>x.Name=="__runOriginal"),
            "Invocation transaction: failed pre-validation precedes UB mutations; skipped original also skips UB postfix grants (metadata)");
        check(Calls(context.Methods.Single(x=>x.Name=="ValidateComplete"),"get_UnlearnedInvocations"), "Invocation transaction: custom/feat selections participate in final safety checks (metadata)");
    }
}
