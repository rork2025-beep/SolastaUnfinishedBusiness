using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;

// Executes shared production planners with managed doubles; Cecil reads shipped/native IL.
// Unity, Harmony application, actual rest/selection/respec and save roundtrips are not executed.
internal static class WarlockTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m, string name, string owner = null) => m.HasBody &&
        m.Body.Instructions.Any(x => x.Operand is MethodReference r && r.Name == name && (owner == null || r.DeclaringType.Name == owner));
    internal static void Run(Action<bool,string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        var context = types.Single(x => x.Name == "WarlockProgressionContext");
        var casting = types.Single(x => x.Name == "WarlockSpellcastingContext");
        var invocations = types.Single(x => x.Name == "WarlockInvocationContext");
        check(Enumerable.Range(1,12).Select(n => WarlockProgressionRules.EffectiveLevel(true,n)).SequenceEqual(new[] { 1,2,3,4,5,7,9,11,13,15,17,20 }), "Warlock: exact complete effective-level map");
        check(Enumerable.Range(1,12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).SequenceEqual(Enumerable.Range(1,20)), "Warlock: every intermediate original advancement occurs exactly once");
        check(Enumerable.Range(0,21).All(n => WarlockProgressionRules.EffectiveLevel(false,n)==n) && WarlockProgressionRules.EffectiveLevel(true,13)==13,
            "Warlock: off and unsupported existing levels preserve actual rank");
        check(!WarlockProgressionRules.IsCapped(true,11) && WarlockProgressionRules.IsCapped(true,12) && WarlockProgressionRules.IsCapped(true,13) && !WarlockProgressionRules.IsCapped(false,12), "Warlock: opt-in class13 boundary");
        check(Enumerable.Range(-2,29).All(n => WarlockProgressionRules.CasterLevel(true,n) is >=0 and <=20), "Warlock: pact table indices bounded to0-20");
        var builders = types.Where(x => x.BaseType?.Name == "AbstractSubclass" && x.Methods.Any(m => m.Name == "get_Klass" && Calls(m,"get_Warlock"))).ToArray();
        var expected = new[] { "PatronArchfey","PatronCelestial","PatronEldritchSurge","PatronElementalist","PatronMoonlitScion","PatronMountain","PatronRiftWalker","PatronSoulBlade" };
        check(builders.Select(x => x.Name).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), "Warlock: eight real UB patron registrations discovered from assembly class ownership");
        var official = new[] { "PatronFiend","PatronHive","PatronTimekeeper","PatronTree" };
        var fixtures = new List<(string Name,string Folder)> { ("Warlock","OfficialBlueprints") };
        fixtures.AddRange(official.Select(x => (x,"OfficialBlueprints"))); fixtures.AddRange(expected.Select(x => (x,"UnfinishedBusinessBlueprints")));
        foreach(var (name,folder) in fixtures)
        {
            using var j = JsonDocument.Parse(File.ReadAllText("Diagnostics/"+folder+"/"+(name=="Warlock"?"CharacterClassDefinition/":"CharacterSubclassDefinition/")+name+".json"));
            var source = j.RootElement.GetProperty("featureUnlocks").EnumerateArray().Select(x => new FeatureUnlockByLevel(
                new FeatureDefinition { Name=x.GetProperty("featureDefinition").GetString() },x.GetProperty("level").GetInt32())).ToList();
            var before = source.Select(x => (x.FeatureDefinition,x.Level)).ToArray();
            var output = new CompressedProgressionDefinitions("WarlockTest"+name).Prepare(source,name);
            check(output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x => x is not FeatureDefinitionFeatureSet)
                .SequenceEqual(source.OrderBy(x => x.Level).Select(x => x.FeatureDefinition)), "Warlock: fixture event order/completeness: "+name);
            check(source.Select(x => (x.FeatureDefinition,x.Level)).SequenceEqual(before) && output.All(x => x.Level<=12), "Warlock: fixture original untouched/output capped: "+name);
        }
        var init = new FighterProgressionInitialization(); var applied=0;
        for(var n=0;n<4;n++) if(init.ShouldInitialize(true)) applied++;
        var off = new FighterProgressionInitialization();
        check(applied==1 && !off.ShouldInitialize(false) && !off.ShouldInitialize(true), "Warlock: one-shot initialization incl disabled latch");
        var startup = types.SelectMany(x => x.Methods).Single(x => Calls(x,"LateLoad","WarlockProgressionContext"));
        int Position(string owner) => startup.Body.Instructions.Select((x,i)=>(x,i)).Single(x => x.x.Operand is MethodReference m && m.Name=="LateLoad" && m.DeclaringType.Name==owner).i;
        check(new[] { "FighterProgressionContext","BarbarianProgressionContext","MonkProgressionContext","RogueProgressionContext","WizardProgressionContext","SorcererProgressionContext","Tabletop2024Context","SpellPointsContext" }
            .All(x => Position("WarlockProgressionContext")>Position(x)), "Warlock: initialization follows previous six modules and final2024/Spell Points switches (metadata)");
        check(Calls(context.Methods.Single(x=>x.Name=="DiscoverSubclasses"),"get_KlassListSubclassEnabled") &&
            Types(context).SelectMany(x=>x.Methods).Any(x=>Calls(x,"get_Hidden")), "Warlock: actual enabled registration/visibility filters retained (metadata)");
        var settings = types.Single(x=>x.FullName=="SolastaUnfinishedBusiness.Settings");
        check(settings.Methods.Where(x=>x.IsConstructor && !x.IsStatic).All(m=>!m.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name.Contains("EnableWarlockCompressedProgressionPrototype"))), "Warlock: CLR false default (metadata)");
        CheckPact(check,types,game); CheckRewards(check,types); CheckBoundaries(check,types);
        check(Calls(casting.NestedTypes.Single(x=>x.Name=="ClearLearningPatch").Methods.Single(x=>x.Name=="Prefix"),"Clear","WarlockInvocationContext") &&
            Calls(invocations.Methods.Single(x=>x.Name=="Clear"),"get_UnlearnedInvocations") && Calls(invocations.Methods.Single(x=>x.Name=="Clear"),"InternalReset") && Types(invocations).SelectMany(x=>x.Methods).Any(x=>Calls(x,"UntrainInvocations")), "Warlock: cancellation clears pending original invocation selections and native granted pools (metadata)");
        check(Calls(types.Single(x=>x.Name=="TrainInvocation_Patch").Methods.Single(x=>x.Name=="Prefix"),"CanTrain","WarlockInvocationContext"), "Warlock: train command validated before existing prefix creates granted pools (metadata)");
        foreach(var owner in new[] { context,casting,invocations })
        foreach(var patch in owner.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(x=>x.AttributeType.Name=="HarmonyPatch"); if(attr==null) continue;
            var type=(TypeReference)attr.ConstructorArguments[0].Value; var name=(string)attr.ConstructorArguments[1].Value;
            var targets=game.MainModule.Types.Single(x=>x.FullName==type.FullName).Methods.Where(x=>x.Name==name || x.Name=="get_"+name).ToArray();
            check(targets.Length>0,"Warlock: native Harmony target: "+patch.Name);
            foreach(var p in patch.Methods.Where(x=>x.Name is "Prefix" or "Postfix").SelectMany(x=>x.Parameters).Where(x=>!x.Name.StartsWith("__")))
                check(targets.Any(x=>x.Parameters.Any(y=>y.Name==p.Name)),"Warlock: native Harmony argument: "+patch.Name+"."+p.Name);
        }
    }
    private static void CheckPact(Action<bool,string> check,TypeDefinition[] types,AssemblyDefinition game)
    {
        var shared=types.Single(x=>x.Name=="SharedSpellsContext");
        var flat=ReadAddedIntegers(shared,"WarlockCastingSlots");
        check(flat.Length==120,"Warlock: compiled UB pact table has20 rows of6 indicators");
        var rows=Enumerable.Range(0,20).Select(n=>flat.Skip(n*6).Take(6).ToArray()).ToArray();
        var projected=SpellcastingProgressionPlan.ProjectRows(rows);
        var circles=new[] { 1,1,2,2,3,4,5,5,5,5,5,5 };
        var counts=new[] { 1,2,2,2,2,2,2,3,3,3,4,4 };
        for(var n=1;n<=12;n++)
            check(projected[n-1].SequenceEqual(rows[WarlockProgressionRules.EffectiveLevel(true,n)-1]) &&
                Array.IndexOf(projected[n-1],0)==circles[n-1] && projected[n-1][0]==counts[n-1] &&
                WarlockProgressionRules.PactSpellLevel(WarlockProgressionRules.EffectiveLevel(true,n))==circles[n-1], "Warlock: actual"+n+" exact compiled pact row/circle/count");
        check(projected[11].SequenceEqual(new[] { 4,4,4,4,4,0 }) && ReferenceEquals(projected[11],rows[19]), "Warlock: original20 four fifth-level pact uses; projection copies row reference only");
        // ProjectRows deliberately returns the original row reference; runtime makes a deep Slots list copy.
        var casting=types.Single(x=>x.Name=="WarlockSpellcastingContext");
        check(Types(casting).SelectMany(x=>x.Methods).Any(x=>Calls(x,"ToList")), "Warlock: runtime slot definitions deep-copy final slot lists (metadata)");
        using var j=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpellWarlock.json"));
        var json=j.RootElement;
        check(json.GetProperty("slotsRecharge").GetString()=="ShortRest" && json.GetProperty("uniqueLevelSlots").GetBoolean() && json.GetProperty("spellKnowledge").GetString()=="Selection", "Warlock: native unique Pact Magic/short rest/known selection metadata");
        var known=ReadAddedIntegers(shared,"WarlockKnownSpells");
        var plan=new KnownSpellsProgressionPlan(known,Enumerable.Range(1,20).Select(n=>n==1?0:1));
        check(SpellcastingProgressionPlan.ProjectCumulative(known).Take(12).SequenceEqual(new[] { 2,3,4,5,6,8,10,11,12,13,14,15 }), "Warlock: known spell totals through15 at actual12 from compiled final UB table");
        check(Enumerable.Range(1,12).SelectMany(plan.Events).Sum(x=>x.Learned)==15 && Enumerable.Range(1,12).SelectMany(plan.Events).Sum(x=>x.Replaced)==19 && plan.Events(12).Sum(x=>x.Replaced)==3 && plan.Events(12).Sum(x=>x.Learned)==1, "Warlock: fifteen known acquisitions and nineteen independent optional spell replacements retained");
        var baseKnown=new[] { "A","B" };
        var first=KnownSpellsProgressionPlan.ApplyEvent(baseKnown,new[] { "A" },new[] { "C","D" });
        var next=KnownSpellsProgressionPlan.ApplyEvent(first,new[] { "C" },new[] { "A","E" });
        check(next.SequenceEqual(new[] { "B","D","A","E" }) && baseKnown.SequenceEqual(new[] { "A","B" }), "Warlock: spell chain replaces earlier-event acquisition/relearns old spell without mutating initial repertoire");
        var inputs=new[] { ("Cantrip",0,0),("Unlearn6",6,0),("Unlearn7",7,0),("Learn6",6,1),("Learn7",7,1),("Arcanum",0,1) };
        check(KnownSpellsProgressionPlan.OrderSteps(inputs).SequenceEqual(new[] { "Cantrip","Unlearn6","Learn6","Unlearn7","Learn7","Arcanum" }), "Warlock: normal spell replacement pairs interleave and independent Arcanum remains separate");
        foreach(var (original,actual,circle) in new[] { (11,8,6),(13,9,7),(15,10,8),(17,11,9) })
        {
            check(FighterProgressionPlan<int>.CompressedLevel(original)==actual && WarlockProgressionRules.MaximumArcanumCircle(original)==circle && WarlockProgressionRules.PactSpellLevel(original)==5,"Warlock: Arcanum"+circle+" milestone without raising pact circle");
            using var pool=JsonDocument.Parse(File.ReadAllText("Diagnostics/"+(circle==9?"UnfinishedBusinessBlueprints":"OfficialBlueprints")+"/FeatureDefinitionPointPool/PointPoolWarlockMysticArcanum"+circle+".json"));
            var p=pool.RootElement;
            check(p.GetProperty("poolAmount").GetInt32()==1 && p.GetProperty("minSpellLevel").GetInt32()==circle && p.GetProperty("maxSpellLevel").GetInt32()==circle && p.GetProperty("extraSpellsTag").GetString()=="MysticArcanum", "Warlock: exact native extra-spell pool for Arcanum"+circle);
        }
        check(WarlockProgressionRules.MaximumArcanumCircle(20)==9,"Warlock: effective20 cannot produce an out-of-range tenth circle");
        var nativeBuilder=game.MainModule.Types.Single(x=>x.Name=="CharacterBuildingManager");
        var nativeGrant=nativeBuilder.Methods.Single(x=>x.Name=="GrantCantripsAndSpellsByTag");
        check(Calls(nativeGrant,"GetPointPoolOfTypeAndTag") && Calls(nativeGrant,"get_ExtraSpellsTag") && Calls(nativeGrant,"GrantSpell"),
            "Warlock: native supplementary grant reads actual pool ExtraSpellsTag, keeping Arcanum outside ordinary known spells (metadata)");
        var activeTags=nativeBuilder.Methods.Single(x=>x.Name=="GetActivePoolTags");
        check(Calls(activeTags,"get_Key") && !Calls(activeTags,"get_MaxPoints") && !Calls(activeTags,"get_RemainingPoints"),
            "Warlock: native pool enumeration retains zero-budget replacement placeholders (metadata)");
        var parent="03ClassWarlock12";
        var tags=new[] { parent+"Warlock12Original19",parent+"Warlock12Original20",parent+"Warlock12WarlockOriginal17Event7MysticArcanum",parent };
        check(KnownSpellsProgressionPlan.SupplementaryTags(parent,"Warlock12",tags).Length==3 &&
            KnownSpellsProgressionPlan.SupplementaryTags(tags[2],"Warlock12",tags).Length==0,
            "Warlock: actual12 native tag and Arcanum origin route once without recursive duplicate grants");
        var repertoire=game.MainModule.Types.Single(x=>x.Name=="RulesetSpellRepertoire");
        bool Field(MethodDefinition m,string field)=>m.Body.Instructions.Any(x=>x.Operand is FieldReference f && f.Name==field);
        check(Field(repertoire.Methods.Single(x=>x.Name=="SpendMysticArcanum"),"usedMysticArcanum") && !Field(repertoire.Methods.Single(x=>x.Name=="SpendMysticArcanum"),"usedSpellsSlots"),"Warlock: native Arcanum use has a separate counter (metadata)");
        check(Field(repertoire.Methods.Single(x=>x.Name=="RestoreAllSpellSlots"),"usedSpellsSlots") && !Field(repertoire.Methods.Single(x=>x.Name=="RestoreAllSpellSlots"),"usedMysticArcanum") &&
            Field(repertoire.Methods.Single(x=>x.Name=="RestoreAllMysticArcanum"),"usedMysticArcanum"),"Warlock: native slot and Arcanum restoration remain separate (metadata)");
        check(Calls(shared.Methods.Single(x=>x.Name=="GetWarlockCasterLevel"),"CasterLevel","WarlockProgressionRules") && Types(shared).SelectMany(x=>x.Methods).Any(x=>Calls(x,"get_AdditionalSlots")),"Warlock: central pact index helper adapted while Hive temporary/additional slots preserved (metadata)");
        check(!Calls(shared.Methods.Single(x=>x.Name=="GetSharedCasterLevel"),"EffectiveLevel","WarlockProgressionContext") && !casting.Methods.Any(x=>x.Name=="SingleCasterLevel"),"Warlock: no ordinary full-caster override introduced (metadata)");
        var points=types.Single(x=>x.Name=="SpellPointsContext");
        check(!Calls(types.Single(x=>x.Name=="ModifyPowerPoolAmountPowerSpellPoints").Methods.Single(x=>x.Name=="GetCasterLevel"),"EffectiveLevel","WarlockProgressionContext") && !Calls(points.Methods.Single(x=>x.Name=="SwitchFeatureDefinitionCastSpellSlots"),"get_CastSpellWarlock"),"Warlock: Spell Points full-caster budget/table excludes Warlock (metadata)");
        var recovery=types.Single(x=>x.Name=="WarlockProgressionContext").Methods.Single(x=>x.Name=="RecoverPactSlots");
        check(Calls(recovery,"GetWarlockSpellLevel") && !Calls(recovery,"RestoreAllMysticArcanum") && !Calls(recovery,"AddSpellPoints"),"Warlock: legacy Master recovery is scoped to pact counter (metadata)");
        check(Calls(types.Single(x=>x.Name=="ApplySpellSlotsForm_Patch").Methods.Single(x=>x.Name=="Prefix"),"RecoverPactSlots"),"Warlock: legacy capstone uses dedicated recovery before generic Arcane Recovery (metadata)");
        var cunning=types.Single(x=>x.Name=="PowerOrSpellFinishedByMeMagicalCunning");
        check(Types(cunning).SelectMany(x=>x.Methods).Any(x=>Calls(x,"EffectiveLevel","WarlockProgressionContext") && Calls(x,"GetWarlockMaxSlots")),"Warlock: 2024 Cunning/Master consults effective rank and native pact maximum (metadata)");
    }
    private static void CheckRewards(Action<bool,string> check, TypeDefinition[] types)
    {
        foreach(var revised in new[] { false,true })
        foreach(var capstone in new[] { false,true })
        foreach(var patron3 in new[] { false,true })
        {
            var source=new List<FeatureUnlockByLevel>();
            var shared=new FeatureDefinitionPointPool { Name="RepeatedInvocation",PoolType="Invocation",PoolAmount=1 };
            foreach(var n in new[] { 7,9,12,15,18 }) source.Add(new(shared,n));
            source.Add(new(new FeatureDefinitionPointPool { Name="InitialInvocation",PoolType="Invocation",PoolAmount=2 },2));
            source.Add(new(new FeatureDefinitionPointPool { Name="FifthInvocation",PoolType="Invocation",PoolAmount=revised?2:1 },5));
            if(revised) source.Add(new(shared,1));
            var asi=new FeatureDefinitionFeatureSet { Name="ASIAndFeat",Mode=FeatureDefinitionFeatureSet.FeatureSetMode.Union };
            asi.FeatureSet.Add(new FeatureDefinitionPointPool { Name="Ability",PoolType="AbilityScore",PoolAmount=2 });
            asi.FeatureSet.Add(new FeatureDefinitionPointPool { Name="Feat",PoolType="Feat",PoolAmount=1 });
            source.AddRange(new[] { 4,8,12,16,19 }.Select(x=>new FeatureUnlockByLevel(asi,x)));
            source.AddRange(new[] { 2,6,10,14,18 }.Select(x=>new FeatureUnlockByLevel(new FeatureDefinitionPointPool { Name="MiddleFeat",PoolType="Feat",PoolAmount=1 },x)));
            source.Add(new(new FeatureDefinition { Name="PatronChoice" },patron3?3:1));
            source.Add(new(new FeatureDefinition { Name=revised?"InvocationPacts":"PactSelection" },revised?1:3));
            source.Add(new(new FeatureDefinitionPower { Name=capstone?"ImprovedMaster20":"LegacyMaster20" },20));
            if(capstone) source.Add(new(new FeatureDefinitionPower { Name="MagicalCunning" },2));
            foreach(var n in new[] { 11,13,15,17 }) source.Add(new(new FeatureDefinitionPointPool { Name="Arcanum"+n,PoolType="Arcanum",PoolAmount=1 },n));
            var before=source.Select(x=>(x.FeatureDefinition,x.Level)).ToArray();
            var transformer=new CompressedProgressionDefinitions("WarlockRewards"+revised+capstone+patron3);
            var output=transformer.Prepare(source,"Warlock");
            var pools=output.SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPointPool>().ToArray();
            var variant=revised+"/"+capstone+"/"+patron3;
            check(pools.Where(x=>x.PoolType=="Invocation").Sum(x=>x.PoolAmount)==(revised?10:8), "Warlock: repeated invocation awards retain2014 eight/2024 ten choices: "+variant);
            check(pools.Where(x=>x.PoolType=="AbilityScore").Sum(x=>x.PoolAmount)==10 && pools.Where(x=>x.PoolType=="Feat").Sum(x=>x.PoolAmount)==10,
                "Warlock: five ASIs and both extra-feat schedules retain total rewards (origin feat remains separate): "+variant);
            check(pools.Where(x=>x.PoolType=="Arcanum").Sum(x=>x.PoolAmount)==4 && new[] { 8,9,10,11 }.All(n=>output.Where(x=>x.Level==n).SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPointPool>().Any(x=>x.PoolType=="Arcanum")), "Warlock: four independent Arcanum choices survive: "+variant);
            check(output.Single(x=>x.FeatureDefinition.Name=="PatronChoice").Level==(patron3?3:1) && output.Where(x=>x.Level==12).SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPower>().Single().Name==(capstone?"ImprovedMaster20":"LegacyMaster20"), "Warlock: original/2024 timing and mutually exclusive20 capstone: "+variant);
            var origins=pools.Where(transformer.PoolOrigins.ContainsKey).Select(x=>transformer.PoolOrigins[x]).ToArray();
            check(origins.Distinct().Count()==origins.Length && source.Select(x=>(x.FeatureDefinition,x.Level)).SequenceEqual(before), "Warlock: cloned repeated rewards have independent origins; original definitions unchanged: "+variant);
        }
        check(Enumerable.Range(1,20).Sum(n=>WarlockProgressionRules.InvocationReplacementBudget(false,n))==18 && Enumerable.Range(1,20).Sum(n=>WarlockProgressionRules.InvocationReplacementBudget(true,n))==19, "Warlock: standard replacement opportunities begin after acquiring invocations (2014 level3/2024 level2)");
        check(SpellcastingProgressionPlan.OriginalLevels(12).Sum(n=>WarlockProgressionRules.InvocationReplacementBudget(false,n))==3, "Warlock: original18/19/20 each retain optional invocation replacement");
        var rules2024=types.Single(x=>x.Name=="Tabletop2024Context");
        foreach(var (method,flag) in new[] { ("SwitchWarlockMagicalCunningAndImprovedEldritchMaster","EnableWarlockMagicalCunningAndImprovedEldritchMaster2024"),
                     ("SwitchWarlockPatronLearningLevel","EnableWarlockToLearnPatronAtLevel3"),("SwitchWarlockInvocationsProgression","EnableWarlockInvocationProgression2024") })
            check(Calls(rules2024.Methods.Single(x=>x.Name==method),"get_"+flag),"Warlock: real2024 switch remains authoritative: "+flag);
        check(Calls(rules2024.Methods.Single(x=>x.Name=="SwitchWarlockMagicalCunningAndImprovedEldritchMaster"),"RemoveAll"),"Warlock: native2014/2024 capstone switch removes other variant (metadata)");
        check(Calls(rules2024.Methods.Single(x=>x.Name=="SwitchWarlockInvocationsProgression"),"get_FeatureSetPactSelection") &&
            Calls(rules2024.Methods.Single(x=>x.Name=="SwitchWarlockInvocationsProgression"),"get_OtherworldlyLeap"), "Warlock: source2024 switch retains Pact selection/invocation and prerequisite changes (metadata)");
        foreach(var sub in new[] { "PatronSoulBlade","PatronMoonlitScion","PatronCelestial","PatronMountain" })
            check(Types(types.Single(x=>x.Name==sub)).SelectMany(x=>x.Methods).Any(x=>Calls(x,"EffectiveLevel","WarlockProgressionContext")), "Warlock: scoped class-rank calculations: "+sub+" (metadata)");
        var soul=types.Single(x=>x.Name=="PatronSoulBlade");
        check(Types(soul).SelectMany(x=>x.Methods).Any(x=>Calls(x,"SetOverriddenPower")), "Warlock: SoulBlade Hex/Master Hex replacement link retained (metadata)");
        var rift=types.Single(x=>x.Name=="PatronRiftWalker");
        check(Types(rift).SelectMany(x=>x.Methods).Any(x=>Calls(x,"SetOverriddenPower")) && types.Any(x=>x.Name=="PreventRemoveConcentrationRiftWalker"), "Warlock: RiftWalker Portal replaces Step; concentration preservation remains (metadata)");
        var builder=types.Single(x=>x.Name=="EldritchVersatilityBuilders");
        check(Types(builder).SelectMany(x=>x.Methods).Any(x=>Calls(x,"EffectiveLevel","WarlockProgressionContext")), "Warlock: EldritchSurge support condition uses effective rank (metadata)");
        var beam=types.Single(x=>x.Name=="PatronEldritchSurge");
        check(Types(beam).SelectMany(x=>x.Methods).Any(x=>Calls(x,"EffectiveLevel","WarlockProgressionContext") && Calls(x,"ComputeAdditionalBeamCount")), "Warlock: specialized beam scaling is scoped; actual total adjusted only for this feature (metadata)");
        var invocation=types.Single(x=>x.Name=="WarlockInvocationContext");
        check(Calls(invocation.Methods.Single(x=>x.Name=="Configure"),"get_CanUnlearn") &&
            Calls(invocation.NestedTypes.Single(x=>x.Name=="UnlearnPatch").Methods.Single(x=>x.Name=="Prefix"),"ValidateEdit"), "Warlock: optional replacement pools and event transaction command validation present (metadata)");
        check(Calls(types.Single(x=>x.Name=="CustomInvocationSelectionPanel").Methods.Single(x=>x.Name=="UpdateGrantedFeatures"),"AssignCustomPoolOrigins"), "Warlock: EldritchSurge custom pools retain original-event tags (metadata)");
        var prereq=types.Single(x=>x.Name=="IsInvocationMacthingPrerequisites_Patch").Methods.Single(x=>x.Name=="IsInvocationMatchingPrerequisites");
        check(Calls(prereq,"PrerequisiteLevel","WarlockInvocationContext") && Calls(prereq,"get_RequiredKnownSpell") && Calls(prereq,"get_RequiredPact"), "Warlock: original event's invocation rank plus known-spell/Pact requirements preserved (metadata)");
    }
    private static void CheckBoundaries(Action<bool,string> check,TypeDefinition[] types)
    {
        var helper=types.Single(x=>x.Name=="MulticlassInOutRulesHelper");
        check(Types(helper).SelectMany(x=>x.Methods).Any(x=>Calls(x,"IsCapped","WarlockProgressionContext") &&
            Calls(x,"CanSelectClass","WizardSpellcastingContext") && Calls(x,"CanSelectClass","SorcererSpellcastingContext") && Calls(x,"CanSelectClass","WarlockSpellcastingContext")), "Warlock: cap and all three caster guards compose in class eligibility (metadata)");
        check(helper.Methods.Any(x=>x.Name=="ApproveMultiClassInOut") && Types(helper).SelectMany(x=>x.Methods).Any(x=>Calls(x,"get_MaxAllowedClasses") && Calls(x,"get_EnableMinInOutAttributes")), "Warlock: existing ability/max-class policy preserved (metadata)");
        foreach(var name in new[] { "AssignClassLevel_Patch","AddClassLevel_Patch" })
            check(types.Where(x=>x.Name==name).SelectMany(x=>x.Methods).Any(x=>Calls(x,"IsCapped","WarlockProgressionContext")), "Warlock: class13 command boundary: "+name);
        var casting=types.Single(x=>x.Name=="WarlockSpellcastingContext");
        check(Calls(casting.Methods.Single(x=>x.Name=="IsSupportedHero"),"IsMulticaster"), "Warlock: second casting repertoire blocked (metadata)");
        check(Calls(casting.Methods.Single(x=>x.Name=="CanSelectClass"),"IsCaster") && Types(casting).SelectMany(x=>x.Methods).Any(x=>Calls(x,"get_Subclasses")), "Warlock: noncasting martial alternatives retained; all-caster subclass dead ends excluded (metadata)");
        check(casting.NestedTypes.Any(x=>x.Name=="CastingFeatBoundaryPatch") && casting.NestedTypes.Any(x=>x.Name=="CasterSubclassBoundaryPatch"), "Warlock: casting feats and future caster subclasses guarded (metadata)");
        var context=types.Single(x=>x.Name=="WarlockProgressionContext");
        check(Calls(context.Methods.Single(x=>x.Name=="CanContinue"),"EnumerateHeroAllowedClassDefinitions") && Calls(context.Methods.Single(x=>x.Name=="CanContinue"),"Error"), "Warlock: capped hero without eligible alternative is explicitly rejected/logged (metadata)");
        check(WarlockProgressionRules.EffectiveLevel(true,12)==20 && WarlockProgressionRules.EffectiveLevel(true,8)==11 && 12+8==20 && 8+6+6==20, "Warlock: actual totals independent of compressed ranks; future mixed-casting examples do not alter history");
        check(Calls(types.Single(x=>x.Name=="WizardProgressionContext").Methods.Single(x=>x.Name=="ScalingLevel"),"EffectiveLevel","WarlockProgressionContext"), "Warlock: existing targeted feature rank dispatch extended; no global GetClassLevel patch (metadata)");
    }
    private static int[] ReadAddedIntegers(TypeDefinition type,string field)
    {
        var list=type.Methods.Single(x=>x.Name==".cctor").Body.Instructions;
        var end=list.Select((x,i)=>(x,i)).Single(x=>x.x.OpCode.Code==Code.Stsfld && x.x.Operand is FieldReference f && f.Name==field).i;
        var start=list.Take(end).Select((x,i)=>(x,i)).Where(x=>x.x.OpCode.Code==Code.Stsfld).Select(x=>x.i).DefaultIfEmpty(-1).Last();
        return list.Skip(start+1).Take(end-start-1).Where(x=>x.Operand is MethodReference m && m.Name=="Add" &&
            m.DeclaringType is GenericInstanceType g && g.GenericArguments.Single().MetadataType==MetadataType.Int32).Select(x=>Integer(x.Previous).Value).ToArray();
    }
    private static int? Integer(Instruction i)=>i.OpCode.Code switch { Code.Ldc_I4_M1=>-1,Code.Ldc_I4_0=>0,Code.Ldc_I4_1=>1,Code.Ldc_I4_2=>2,Code.Ldc_I4_3=>3,
        Code.Ldc_I4_4=>4,Code.Ldc_I4_5=>5,Code.Ldc_I4_6=>6,Code.Ldc_I4_7=>7,Code.Ldc_I4_8=>8,Code.Ldc_I4 or Code.Ldc_I4_S=>Convert.ToInt32(i.Operand),_=>null };
}
