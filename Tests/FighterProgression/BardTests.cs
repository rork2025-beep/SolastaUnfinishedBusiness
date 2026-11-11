using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;

// Production planners, transformer and spell selection state run with managed doubles.
// Native/Harmony checks below inspect compiled metadata only; no Unity gameplay is executed.
internal static class BardTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m,string name,string owner=null) => m.HasBody && m.Body.Instructions.Any(x=>x.Operand is MethodReference r && r.Name==name && (owner==null || r.DeclaringType.Name==owner));
    private static bool Reject(Action action) { try { action();return false; } catch(InvalidOperationException) { return true; } }
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();
        var context=types.Single(x=>x.Name=="BardProgressionContext");
        var casting=types.Single(x=>x.Name=="BardSpellcastingContext");
        check(Enumerable.Range(1,12).Select(n=>BardProgressionRules.EffectiveLevel(true,n)).SequenceEqual(new[] { 1,2,3,4,5,7,9,11,13,15,17,20 }),"Bard: exact full20-to12 mapping");
        check(Enumerable.Range(1,12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).SequenceEqual(Enumerable.Range(1,20)),"Bard: all twenty original advancement events exactly once");
        check(Enumerable.Range(0,21).All(n=>BardProgressionRules.EffectiveLevel(false,n)==n) && BardProgressionRules.EffectiveLevel(true,13)==13,"Bard: disabled and unsupported existing levels keep actual rank");
        check(BardProgressionRules.IsCapped(true,12) && BardProgressionRules.IsCapped(true,13) && !BardProgressionRules.IsCapped(true,11) && !BardProgressionRules.IsCapped(false,12),"Bard: opt-in class13 boundary");
        var init=new FighterProgressionInitialization();var off=new FighterProgressionInitialization();var ninit=0;
        for(var n=0;n<4;n++)if(init.ShouldInitialize(true))ninit++;
        check(ninit==1 && !off.ShouldInitialize(false) && !off.ShouldInitialize(true),"Bard: repeated initialization and disabled latch preserve ordinary definitions");
        var expected=new[] { "CollegeOfAudacity","CollegeOfElegance","CollegeOfGuts","CollegeOfLife","CollegeOfThespian","CollegeOfValiance" };
        var builders=types.Where(x=>x.BaseType?.Name=="AbstractSubclass" && x.Methods.Any(m=>m.Name=="get_Klass" && Calls(m,"get_Bard"))).ToArray();
        check(builders.Select(x=>x.Name).OrderBy(x=>x).SequenceEqual(expected.OrderBy(x=>x)),"Bard: six actual compiled UB college registrations");
        var official=new[] { "CollegeOfHeroism","CollegeOfHope","CollegeOfLore","CollegeOfTraditions" };
        var fixtures=new List<(string Name,string Folder)> { ("Bard","OfficialBlueprints") };
        fixtures.AddRange(official.Select(x=>(x,"OfficialBlueprints")));fixtures.AddRange(expected.Select(x=>(x,"UnfinishedBusinessBlueprints")));
        foreach(var (name,folder) in fixtures)
        {
            using var j=JsonDocument.Parse(File.ReadAllText("Diagnostics/"+folder+"/"+(name=="Bard"?"CharacterClassDefinition/":"CharacterSubclassDefinition/")+name+".json"));
            var source=j.RootElement.GetProperty("featureUnlocks").EnumerateArray().Select(x=>new FeatureUnlockByLevel(new FeatureDefinition { Name=x.GetProperty("featureDefinition").GetString() },x.GetProperty("level").GetInt32())).ToList();
            var before=source.Select(x=>(x.FeatureDefinition,x.Level)).ToArray();
            var output=new CompressedProgressionDefinitions("BardFixture"+name).Prepare(source,name);
            check(output.SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x=>x is not FeatureDefinitionFeatureSet).SequenceEqual(source.OrderBy(x=>x.Level).Select(x=>x.FeatureDefinition)),"Bard: complete fixture event order: "+name);
            check(output.All(x=>x.Level<=12) && source.Select(x=>(x.FeatureDefinition,x.Level)).SequenceEqual(before),"Bard: fixture original unchanged/output capped: "+name);
        }
        CheckTables(check,types);CheckLearning(check);CheckVariants(check);CheckMetadata(check,types,game,context,casting);
        foreach(var owner in new[] { context,casting })
        foreach(var patch in owner.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(x=>x.AttributeType.Name=="HarmonyPatch");if(attr==null)continue;
            var type=(TypeReference)attr.ConstructorArguments[0].Value;var name=(string)attr.ConstructorArguments[1].Value;
            var target=game.MainModule.Types.FirstOrDefault(x=>x.FullName==type.FullName) ?? types.Single(x=>x.FullName==type.FullName);
            var targets=target.Methods.Where(x=>x.Name==name || x.Name=="get_"+name).ToArray();
            check(targets.Length>0,"Bard: native Harmony target: "+patch.Name);
            foreach(var p in patch.Methods.Where(x=>x.Name is "Prefix" or "Postfix").SelectMany(x=>x.Parameters).Where(x=>!x.Name.StartsWith("__")))
                check(targets.Any(x=>x.Parameters.Any(y=>y.Name==p.Name)),"Bard: native Harmony argument: "+patch.Name+"."+p.Name);
        }
    }
    private static int[][] BardKnownTables(TypeDefinition rules)
    {
        var m=rules.Methods.Single(x=>x.Name=="SwitchOneDndPreparedSpellsTables");
        var code=m.Body.Instructions.ToArray();var arrays=new List<int[]>();
        for(var i=0;i<code.Length;i++)
        {
            if(code[i].Operand is not MethodReference getter || getter.Name!="get_CastSpellBard")continue;
            var section=code.Skip(i+1).TakeWhile(x=>!(x.Operand is FieldReference f && f.Name=="knownSpells")).ToArray();
            arrays.Add(section.Where(x=>x.Operand is MethodReference add && add.Name=="Add").Select(x=>Integer(x.Previous).Value).ToArray());
        }
        return arrays.ToArray();
    }
    private static void CheckTables(Action<bool,string> check,TypeDefinition[] types)
    {
        var tables=BardKnownTables(types.Single(x=>x.Name=="Tabletop2024Context"));
        var modern=new[] { 4,5,6,7,9,10,11,12,14,15,16,16,17,17,18,18,19,20,21,22 };
        var legacy=new[] { 4,5,6,7,8,9,10,11,12,14,15,15,16,18,19,19,20,22,22,22 };
        check(tables.Length==2 && tables[0].SequenceEqual(modern) && tables[1].SequenceEqual(legacy),"Bard: final compiled2024 prepared-table switch supplies exact two Bard known arrays, both ending22");
        foreach(var table in tables)
        {
            var plan=new KnownSpellsProgressionPlan(table,Enumerable.Range(1,20).Select(n=>n==1?0:1));
            check(Enumerable.Range(1,12).SelectMany(plan.Events).Sum(x=>x.Learned)==22 && Enumerable.Range(1,12).SelectMany(plan.Events).Sum(x=>x.Replaced)==19,"Bard: all22 ordinary acquisitions and19 optional replacements retained: "+table[4]);
            check(plan.Events(8).Select(x=>x.Original).SequenceEqual(new[] { 10,11 }) && plan.Events(12).Select(x=>x.Original).SequenceEqual(new[] { 18,19,20 }),"Bard: original10 before11 and18/19/20 individual learning events: "+table[4]);
        }
        check(SpellcastingProgressionPlan.ProjectCumulative(legacy).Take(12).SequenceEqual(new[] { 4,5,6,7,8,10,12,15,16,19,20,22 }),"Bard: exact legacy known totals at actual1-12");
        check(SpellcastingProgressionPlan.ProjectCumulative(modern).Take(12).SequenceEqual(new[] { 4,5,6,7,9,11,14,16,17,18,19,22 }),"Bard: exact prepared2024 known totals at actual1-12");
        var builder=types.Single(x=>x.Name=="FeatureDefinitionCastSpellBuilder");var slots=ReadAddedIntegers(builder,"SlotsByCasterLevel");
        var rows=Enumerable.Range(1,20).Select(n=>slots.Skip(n*10).Take(10).ToArray()).ToArray();
        var projected=SpellcastingProgressionPlan.ProjectRows(rows);var circles=new[] { 1,1,2,2,3,4,5,6,7,8,9,9 };
        for(var actual=1;actual<=12;actual++)check(projected[actual-1].SequenceEqual(rows[BardProgressionRules.EffectiveLevel(true,actual)-1]) && Array.IndexOf(projected[actual-1],0)==circles[actual-1],"Bard: exact full-caster slot row/circle at actual"+actual);
        check(projected[11].SequenceEqual(new[] { 4,3,3,3,3,2,2,1,1,0 }),"Bard: original20 spell slots at actual12; not Wizard spellbook mechanics");
        var points=ReadAddedIntegers(types.Single(x=>x.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel");
        check(Enumerable.Range(1,12).Select(n=>points[SpellcastingProgressionPlan.CasterLevel(n)]).SequenceEqual(new[] { 4,6,14,17,27,38,57,73,83,94,107,133 }),"Bard: source UB Spell Points budget reaches133 at actual12");
        check(Enumerable.Range(-3,28).All(n=>SpellcastingProgressionPlan.CasterLevel(n) is >=0 and <=20),"Bard: caster table indices bounded0-20");
        var pointRows=rows.Select(x=>x.Select(v=>v==0?0:1).ToArray()).ToArray();
        check(SpellcastingProgressionPlan.ProjectRows(pointRows)[11].Take(9).All(x=>x==1),"Bard: Spell Points mode preserves one limiting slot per high circle");
        using var json=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpellBard.json"));
        var raw=json.RootElement.GetProperty("knownCantrips").EnumerateArray().Select(x=>x.GetInt32()).Concat(new int[5]).ToArray();
        check(SpellcastingProgressionPlan.ProjectCumulative(KnownSpellsProgressionPlan.RetainedCantrips(raw)).Take(12).SequenceEqual(new[] { 2,2,2,3,3,3,3,4,4,4,4,4 }),"Bard: late zero cantrip placeholders cannot erase acquired cantrips");
        check(json.RootElement.GetProperty("slotsRecharge").GetString()=="LongRest" && json.RootElement.GetProperty("spellKnowledge").GetString()=="Selection", "Bard: source native long-rest known-spell casting retained");
        foreach(var (name,original,circle) in new[] { ("PointPoolBardMagicalSecrets10",10,5),("PointPoolBardMagicalSecrets14",14,7),("PointPoolBardMagicalSecrets18",18,9),("PointPoolCollegeLoreAdditionalMagicalSecrets",6,3) })
        {
            var folder=original==18?"UnfinishedBusinessBlueprints":"OfficialBlueprints";
            using var pool=JsonDocument.Parse(File.ReadAllText("Diagnostics/"+folder+"/FeatureDefinitionPointPool/"+name+".json"));var p=pool.RootElement;
            check(p.GetProperty("poolAmount").GetInt32()==2 && p.GetProperty("poolType").GetString()=="CantripOrSpell" && p.GetProperty("extraSpellsTag").GetString()=="MagicalSecrets" && p.GetProperty("spellListOverride").GetString().Contains("SpellListAllSpells"),"Bard: real independent all-list secret budget and repertoire tag: "+name);
            check(Math.Min(p.GetProperty("maxSpellLevel").GetInt32(),SpellcastingProgressionPlan.MaxSpellLevel(original))==circle,"Bard: secret original-event circle, not final compressed circle: "+name);
        }
    }
    private static void CheckLearning(Action<bool,string> check)
    {
        var initial=new BardSpellLearningState<string>(new[] { "OldA","OldB" },new[] { "Cantrip" },new[] { "OldSecret" });
        var ten=initial.LearnOrdinary(new[] { "OldA" },new[] { "New10","Replacement10" }).LearnSecrets(new[] { "WizardSpell","ClericSpell" });
        var eleven=ten.LearnOrdinary(new[] { "New10" },new[] { "OldA","New11" });
        check(eleven.Ordinary.SequenceEqual(new[] { "OldB","Replacement10","OldA","New11" }) && eleven.Secrets.SequenceEqual(new[] { "OldSecret","WizardSpell","ClericSpell" }),"Bard: actual production state supports original10 then11 replacement of freshly learned spell; bonus origins stay separate");
        check(initial.Ordinary.SequenceEqual(new[] { "OldA","OldB" }) && initial.Secrets.SequenceEqual(new[] { "OldSecret" }),"Bard: preview/cancel leaves input inventory unchanged");
        check(Reject(()=>ten.LearnOrdinary(Array.Empty<string>(),new[] { "WizardSpell" })) && Reject(()=>ten.LearnSecrets(new[] { "New10" })),"Bard: ordinary and Magical Secrets selections cannot duplicate one another");
        check(Reject(()=>ten.LearnSecrets(new[] { "Cantrip" })) && Reject(()=>ten.LearnCantrips(new[] { "WizardSpell" })),"Bard: secret cantrips and ordinary cantrips cannot double-grant a spell");
        check(Reject(()=>ten.LearnSecrets(new[] { "Duplicate","Duplicate" })) && Reject(()=>ten.LearnOrdinary(new[] { "Unknown" },new[] { "New" })),"Bard: duplicate new secret and unknown ordinary removal rejected");
        check(Reject(()=>ten.LearnOrdinary(new[] { "WizardSpell" },new[] { "Other" })),"Bard: native extra-tag Magical Secrets are not silently removed as ordinary spells");
        check(initial.LearnOrdinary(Array.Empty<string>(),Array.Empty<string>()).All.SequenceEqual(initial.All),"Bard: Skip optional replacement has zero free acquisition");
        var autoAlias=new BardSpellLearningState<string>(new[] { "HealingWord" },Array.Empty<string>(),new[] { "HealingWord","Aid" },true);
        var removeOrdinary=autoAlias.LearnOrdinary(new[] { "HealingWord" },new[] { "OtherSpell" });
        check(removeOrdinary.Secrets.Contains("HealingWord") && removeOrdinary.Ordinary.Single()=="OtherSpell", "Bard: replacing an ordinary spell preserves its independent auto-prepared bonus alias");
        check(Reject(()=>removeOrdinary.LearnSecrets(new[] { "HealingWord" })) && Reject(()=>removeOrdinary.LearnOrdinary(Array.Empty<string>(),new[] { "Aid" })), "Bard: confirmed auto-prepared/bonus aliases cannot be selected again in ordinary or Secrets pools");
        var parent="03ClassBard12";var secret=parent+"Bard12BardOriginal18Event1MagicalSecrets";
        var order=BardSpellLearningState<string>.Ordered(new[] { ("Learn19",19,1,parent+"Bard12Original19"),("Secret18",18,2,secret),("Learn18",18,1,parent),("Learn20",20,1,parent+"Bard12Original20") });
        check(order.SequenceEqual(new[] { "Learn18","Secret18","Learn19","Learn20" }),"Bard: production grant ordering interleaves Secrets18 before ordinary19/20");
        check(BardSpellLearningState<string>.Ordered(new[] { ("Learn7",7,1,"03Class"),("Lore6",6,2,"06Subclass"),("Learn6",6,1,"03Class") }).SequenceEqual(new[] { "Learn6","Lore6","Learn7" }),"Bard: subclass Lore6 Secrets precede next class spell event7 despite native class-before-subclass finalization");
        check(KnownSpellsProgressionPlan.OrderSteps(new[] { ("Cantrip",0,0),("Unlearn10",10,0),("Unlearn11",11,0),("Learn10",10,1),("Learn11",11,1),("Secret10",10,2) }).SequenceEqual(new[] { "Cantrip","Unlearn10","Learn10","Secret10","Unlearn11","Learn11" }),"Bard: production UI ordering places normal learning, Secrets, next replacement chronologically");
        var ledger=new BardSpellGrantLedger();
        check(!ledger.HasStarted("Bard6") && ledger.Grant("Bard6","Spell6") && !ledger.Grant("Bard6","Spell6"), "Bard: production native-grant ledger grants each origin once within one advancement");
        check(ledger.HasStarted("Bard6") && !ledger.HasStarted("Bard7") && ledger.Grant("Bard7","Spell7"), "Bard: reused building-data ledger permits the next actual Bard level");
        check(!ledger.Grant("Bard7","Spell7") && ledger.Grant("Bard7","Secrets7"), "Bard: repeated native call cannot duplicate grants while independent pools still apply");
        var selected=new List<string> { "CrossList" };var frozen=initial.LearnSecrets(selected);selected.Clear();
        check(frozen.Secrets.Last()=="CrossList" && initial.Secrets.Count==1,"Bard: copied choice state cannot be changed by later Reset/Back mutation");
        var changed=initial.LearnOrdinary(new[] { "OldA" },new[] { "Changed10","Replacement10" });
        check(Reject(()=>changed.LearnOrdinary(new[] { "New10" },new[] { "Other" })),"Bard: changing earlier event invalidates stale dependent replacement");
        check(initial.LearnOrdinary(new[] { "OldA" },new[] { "New10","Replacement10" }).All.SequenceEqual(initial.LearnOrdinary(new[] { "OldA" },new[] { "New10","Replacement10" }).All),"Bard: Undo/redo from unchanged inventory is deterministic");
    }
    private static void CheckVariants(Action<bool,string> check)
    {
        for(var variant=0;variant<128;variant++)
        {
            bool On(int bit)=>(variant&(1<<bit))!=0;
            var asi=new FeatureDefinitionFeatureSet { Name="ASIAndFeat",Mode=FeatureDefinitionFeatureSet.FeatureSetMode.Union };
            asi.FeatureSet.Add(new FeatureDefinitionPointPool { Name="ASI",PoolType="AbilityScore",PoolAmount=2 });
            var feat=new FeatureDefinitionPointPool { Name="Feat",PoolType="Feat",PoolAmount=1 };asi.FeatureSet.Add(feat);
            var expertise=new FeatureDefinitionPointPool { Name="Expertise",PoolType="Expertise",PoolAmount=2 };
            var ms=new FeatureDefinitionPointPool { Name="MagicalSecrets",PoolType="Secrets",PoolAmount=2 };
            var source=new List<FeatureUnlockByLevel> { new(new FeatureDefinition { Name="CounterCharm" },On(0)?7:6),new(expertise,On(1)?2:3),new(expertise,On(1)?9:10),
                new(new FeatureDefinition { Name=On(2)?"InspirationHour":"Inspiration10Minutes" },1),new(new FeatureDefinition { Name=On(4)?"Superior2024":"SuperiorLegacy" },On(4)?18:20) };
            if(On(3))source.Add(new(new FeatureDefinition { Name="ExtendedClericDruidWizardLists" },10));
            else foreach(var n in new[] { 10,14,18 })source.Add(new(ms,n));
            if(On(5))source.Add(new(new FeatureDefinition { Name="WordsOfCreation" },20));
            if(!On(6))source.Add(new(new FeatureDefinition { Name="SongOfRest" },2));
            source.Add(new(ms,6)); // independent Lore reward, unaffected by the base class switch
            source.AddRange(new[] { 4,8,12,16,19 }.Select(n=>new FeatureUnlockByLevel(asi,n)));
            source.AddRange(new[] { 2,6,10,14,18 }.Select(n=>new FeatureUnlockByLevel(feat,n)));
            var transform=new CompressedProgressionDefinitions("BardVariant"+variant);var before=source.Select(x=>(x.FeatureDefinition,x.Level)).ToArray();
            var output=transform.Prepare(source,"Bard");var leaves=output.SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x=>x is not FeatureDefinitionFeatureSet).ToArray();
            var pools=leaves.OfType<FeatureDefinitionPointPool>().ToArray();
            check(pools.Where(x=>x.PoolType=="Expertise").Sum(x=>x.PoolAmount)==4 && pools.Where(x=>x.PoolType=="Secrets").Sum(x=>x.PoolAmount)==(On(3)?2:8),"Bard: variant"+variant+" expertise4 and legacy/Lore Secrets budgets without both Magical Secrets variants");
            check(pools.Where(x=>x.PoolType=="AbilityScore").Sum(x=>x.PoolAmount)==10 && pools.Where(x=>x.PoolType=="Feat").Sum(x=>x.PoolAmount)==10,"Bard: variant"+variant+" ASI+feat and both extra feat schedules retain all awards; origin feat remains independent");
            check(leaves.Any(x=>x.Name=="SongOfRest")==!On(6) && leaves.Count(x=>x.Name.StartsWith("Superior"))==1 && leaves.Any(x=>x.Name=="WordsOfCreation")==On(5),"Bard: variant"+variant+" no removed Song resurrection or duplicate capstone");
            check(output.Where(x=>x.Level==6).SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Any(x=>x.Name=="CounterCharm") && output.Where(x=>x.Level==12).SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Any(x=>x.Name.StartsWith("Superior")),"Bard: variant"+variant+" CounterCharm6/7 bothactual6; Superior18/20 bothactual12");
            check(transform.PoolOrigins.Values.Distinct().Count()==transform.PoolOrigins.Count && source.Select(x=>(x.FeatureDefinition,x.Level)).SequenceEqual(before),"Bard: variant"+variant+" repeated choice origins distinct and source unchanged");
        }
        using var cls=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/CharacterClassDefinition/Bard.json"));
        var unlocks=cls.RootElement.GetProperty("featureUnlocks").EnumerateArray().ToArray();
        var dice=new List<(int Original,int Value)>();
        foreach(var die in new[] { "D6","D8","D10","D12" })
        {
            using var j=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionAttributeModifier/AttributeModifierBardicInspirationDie"+die+".json"));
            var original=die=="D6"?1:unlocks.Single(x=>x.GetProperty("featureDefinition").GetString().Contains("AttributeModifierBardicInspirationDie"+die+":")).GetProperty("level").GetInt32();
            check(j.RootElement.GetProperty("modifierOperation").GetString()=="Set" && j.RootElement.GetProperty("modifiedAttribute").GetString()=="BardicInspirationDie","Bard: native Inspiration die setter preserved: "+die);
            dice.Add((original,j.RootElement.GetProperty("modifierValue").GetInt32()));
        }
        check(Enumerable.Range(1,12).Select(n=>dice.Last(x=>x.Original<=BardProgressionRules.EffectiveLevel(true,n)).Value).SequenceEqual(new[] { 4,4,4,4,5,5,5,6,6,7,7,7 }),"Bard: real inspiration attribute values project D6/D8/D10/D12 at actual1/5/8/10");
        using var count=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionAttributeModifier/AttributeModifierBardicInspirationNumber.json"));
        check(count.RootElement.GetProperty("modifierOperation").GetString()=="AddAbilityScoreBonus" && count.RootElement.GetProperty("modifierAbilityScore").GetString()=="Charisma" && count.RootElement.GetProperty("minimum1").GetBoolean(),"Bard: Inspiration counts remain CHA with native minimum, not effective20 or PB substitution");
        using var font=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionCampAffinity/CampAffinityBardFontOfInspiration.json"));
        check(font.RootElement.GetProperty("fastBardicInspirationRecovery").GetBoolean(),"Bard: native Font short-rest recovery metadata retained");
        using var song=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionRestHealingModifier/RestHealingModifierBardSongOfRest.json"));
        var songDice=song.RootElement.GetProperty("bonusDieTypeByRankTable").EnumerateArray().ToDictionary(x=>x.GetProperty("rank").GetInt32(),x=>x.GetProperty("dieType").GetString());
        check(new[] { 7,9,11,12 }.Select(n=>songDice[BardProgressionRules.EffectiveLevel(true,n)]).SequenceEqual(new[] { "D8","D10","D12","D12" }) && song.RootElement.GetProperty("applyDuringShortRest").GetBoolean(),"Bard: Song of Rest source ranks map to compressed Bard class without replacing actual CharacterLevel");
    }
    private static void CheckMetadata(Action<bool,string> check,TypeDefinition[] types,AssemblyDefinition game,TypeDefinition context,TypeDefinition casting)
    {
        var startup=types.SelectMany(x=>x.Methods).Single(x=>Calls(x,"LateLoad","BardProgressionContext"));
        int Position(string owner)=>startup.Body.Instructions.Select((x,i)=>(x,i)).Single(x=>x.x.Operand is MethodReference m && m.Name=="LateLoad" && m.DeclaringType.Name==owner).i;
        check(new[] { "SorcererProgressionContext","WarlockProgressionContext","Tabletop2024Context","SpellPointsContext" }.All(x=>Position("BardProgressionContext")>Position(x)),"Bard: initialization follows all final rules and previous modules (metadata)");
        check(Calls(context.Methods.Single(x=>x.Name=="LateLoad"),"RegisterSecrets") && Calls(context.Methods.Single(x=>x.Name=="DiscoverSubclasses"),"get_KlassListSubclassEnabled"),"Bard: final transformed pools registered and enabled subclass registrations respected (metadata)");
        var settings=types.Single(x=>x.FullName=="SolastaUnfinishedBusiness.Settings");
        check(settings.Methods.Where(x=>x.IsConstructor && !x.IsStatic).All(m=>!m.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name.Contains("EnableBardCompressedProgressionPrototype"))),"Bard: CLR false opt-in default (metadata)");
        var rules=types.Single(x=>x.Name=="Tabletop2024Context");
        foreach(var (method,flag) in new[] { ("SwitchBardCounterCharm","EnableBardCounterCharm2024"),("SwitchBardExpertiseOneLevelBefore","EnableBardExpertiseOneLevelBefore2024"),("SwitchBardBardicInspiration","EnableBardicInspiration2024"),("SwitchBardBardMagicalSecrets","EnableBardMagicalSecrets2024"),("SwitchBardSuperiorInspiration","EnableBardSuperiorInspiration2024"),("SwitchBardWordsOfCreation","EnableBardWordsOfCreation2024"),("SwitchBardSongOfRest","RemoveBardSongOfRest2024") })
            check(Calls(rules.Methods.Single(x=>x.Name==method),"get_"+flag),"Bard: authoritative enabled/disabled2024 switch: "+flag);
        var cctor=rules.Methods.Single(x=>x.Name==".cctor").Body.Instructions.ToArray();
        var stop=cctor.Select((x,i)=>(x,i)).Single(x=>x.x.OpCode.Code==Code.Stsfld && x.x.Operand is FieldReference f && f.Name=="FeatureSetBardMagicalSecrets").i;
        var start=cctor.Take(stop).Select((x,i)=>(x,i)).Where(x=>x.x.OpCode.Code==Code.Stsfld).Select(x=>x.i).DefaultIfEmpty(-1).Last()+1;
        var listCalls=cctor.Skip(start).Take(stop-start).Where(x=>x.Operand is MethodReference).Select(x=>(MethodReference)x.Operand).ToArray();
        check(listCalls.Count(x=>x.Name=="SetExtendedSpellList")==3 && new[] { "Cleric","Druid","Wizard" }.All(name=>listCalls.Any(x=>x.Name=="get_SpellList"+name)),
            "Bard: real2024 feature has exactly Cleric/Druid/Wizard extended lists (metadata)");
        check(!listCalls.Any(x=>x.DeclaringType.Name.Contains("FeatureDefinitionCastSpellBuilder")),
            "Bard: real2024 Secrets feature contains affinities, not foreign casting definitions (metadata)");
        check(context.NestedTypes.Count(x=>x.Name.EndsWith("RestartPatch"))==7 && Calls(casting.Methods.Single(x=>x.Name=="BeginFinalize"),"get_SessionConsistent"),
            "Bard: all seven Bard rule toggles require restart; finalization rejects changed session (metadata)");
        check(Calls(casting.NestedTypes.Single(x=>x.Name=="AcquireLearningPatch").Methods.Single(x=>x.Name=="Prefix"),"InClassLists") &&
            Calls(casting.Methods.Single(x=>x.Name=="InClassLists"),"get_SpellListDefinition"),
            "Bard: ordinary cantrip choices also respect active native extended lists, without consuming legacy Secrets budgets (metadata)");
        var secrets=rules.Methods.Single(x=>x.Name=="SwitchBardBardMagicalSecrets");
        check(Calls(secrets,"RemoveAll") && Calls(secrets,"get_PointPoolBardMagicalSecrets14"),"Bard: actual legacy switch reuses level14 definition at original10; independent clone origins prevent lost reward (metadata)");
        check(Calls(casting.Methods.Single(x=>x.Name=="RegisterSecrets"),"MaxSpellLevel") && casting.Methods.Single(x=>x.Name=="RegisterSecrets").Body.Instructions.Any(x=>x.Operand is FieldReference f && f.Name=="maxSpellLevel"),"Bard: Secrets bounded by original-event maximum (metadata)");
        check(Calls(casting.Methods.Single(x=>x.Name=="Initial"),"get_ExtraSpellsByTag") && !Calls(casting.Methods.Single(x=>x.Name=="Initial"),"GrantSpellRepertoire"),"Bard: Magical Secrets share Bard repertoire with separate bonus inventory, no foreign repertoire (metadata)");
        check(Types(casting).SelectMany(x=>x.Methods).Any(x=>Calls(x,"LearnOrdinary","BardSpellLearningState`1")) && Types(casting).SelectMany(x=>x.Methods).Any(x=>Calls(x,"Ordered","BardSpellLearningState`1")),"Bard: runtime validation/grant ordering use tested production selection state (metadata)");
        check(Calls(casting.NestedTypes.Single(x=>x.Name=="GrantLearningPatch").Methods.Single(x=>x.Name=="Postfix"),"GrantCantripsAndSpellsByTag") && Calls(casting.NestedTypes.Single(x=>x.Name=="GrantLearningPatch").Methods.Single(x=>x.Name=="Prefix"),"Grant"),"Bard: native tagged grants with once-only tracking; class/subclass events interleave (metadata)");
        check(Calls(types.Single(x=>x.Name=="FinalizeCharacter_Patch").Methods.Single(x=>x.Name=="Prefix"),"BeginFinalize","BardSpellcastingContext") && Calls(casting.Methods.Single(x=>x.Name=="BeginFinalize"),"ValidateAll"),"Bard: all spell events validated before native finalization mutations (metadata)");
        check(Calls(types.Single(x=>x.Name=="ModifyPowerPoolAmountPowerSpellPoints").Methods.Single(x=>x.Name=="GetCasterLevel"),"get_Active","BardProgressionContext") && Calls(types.Single(x=>x.Name=="SharedSpellsContext").Methods.Single(x=>x.Name=="GetSharedCasterLevel"),"SingleCasterLevel","BardSpellcastingContext"),"Bard: own Spell Points and solo reaction/upcast helpers use effective class level (metadata)");
        check(Calls(casting.Methods.Single(x=>x.Name=="IsSupportedHero"),"IsMulticaster") && Calls(casting.Methods.Single(x=>x.Name=="CanSelectClass"),"IsCaster"),"Bard: mixed caster guard retains noncasting martial choices (metadata)");
        var helper=types.Single(x=>x.Name=="MulticlassInOutRulesHelper");
        check(Types(helper).SelectMany(x=>x.Methods).Any(x=>Calls(x,"IsCapped","BardProgressionContext") && Calls(x,"CanSelectClass","WizardSpellcastingContext") && Calls(x,"CanSelectClass","SorcererSpellcastingContext") && Calls(x,"CanSelectClass","WarlockSpellcastingContext") && Calls(x,"CanSelectClass","BardSpellcastingContext")),"Bard: all four caster guards and class cap compose without weakening earlier safety (metadata)");
        check(helper.Methods.Any(x=>x.Name=="ApproveMultiClassInOut") && Calls(context.Methods.Single(x=>x.Name=="CanContinue"),"EnumerateHeroAllowedClassDefinitions"),"Bard: native ability/max-class requirements and explicit deadlock check retained (metadata)");
        check(Calls(types.Single(x=>x.Name=="WizardProgressionContext").Methods.Single(x=>x.Name=="ScalingLevel"),"EffectiveLevel","BardProgressionContext"),"Bard: targeted feature class-rank dispatcher, no global GetClassLevel override (metadata)");
        foreach(var sub in new[] { "CollegeOfThespian","CollegeOfValiance" })check(Types(types.Single(x=>x.Name==sub)).SelectMany(x=>x.Methods).Any(x=>Calls(x,"EffectiveLevel","BardProgressionContext") || Calls(x,"EffectiveSubclassLevel","BardProgressionContext")),"Bard: direct class/subclass threshold adapted: "+sub);
        check(context.NestedTypes.Any(x=>x.Name=="SongOfRestRankPatch"),"Bard: scoped native Song of Rest rank adapter present (metadata)");
        var repertoire=game.MainModule.Types.Single(x=>x.Name=="RulesetSpellRepertoire");
        check(repertoire.Methods.Single(x=>x.Name=="GrantCantrip").Body.Instructions.Any(x=>x.Operand is FieldReference f && f.Name=="extraSpellsByTag") &&
            !repertoire.Methods.Single(x=>x.Name=="LoseSpell").Body.Instructions.Any(x=>x.Operand is FieldReference f && f.Name=="extraSpellsByTag"),
            "Bard: native secret cantrips remain bonus-tagged; ordinary LoseSpell cannot retrain confirmed bonus Secrets (metadata)");
        var native=game.MainModule.Types.Single(x=>x.Name=="RulesetCharacterHero").Methods.Single(x=>x.Name=="GetBardicInspirationDieValue");
        check(native.Body.Instructions.Any(x=>x.Operand as string=="BardicInspirationDie"),"Bard: native inspiration reads retained die attribute instead of virtualizing character/PB (metadata)");
        var lvl20=types.Single(x=>x.Name=="Level20Context");
        check(Types(lvl20).SelectMany(x=>x.Methods).Any(x=>Calls(x,"get_RemainingBardicInspirations") && Calls(x,"get_EnableBardSuperiorInspiration2024")),"Bard: native Superior Inspiration1/2 recovery handler retained (metadata)");
        check(types.Any(x=>x.Name=="CustomBehaviorBardWordOfCreation") && rules.Methods.Single(x=>x.Name=="SwitchBardWordsOfCreation").Body.Instructions.Any(x=>x.Operand is FieldReference f && f.Name=="AutoPreparedSpellsBardWordOfCreation"),"Bard: selected Words of Creation feature and native two-target spell modifier retained (metadata)");
    }
    private static int[] ReadAddedIntegers(TypeDefinition type,string field)
    {
        var list=type.Methods.Single(x=>x.Name==".cctor").Body.Instructions;
        var end=list.Select((x,i)=>(x,i)).Single(x=>x.x.OpCode.Code==Code.Stsfld && x.x.Operand is FieldReference f && f.Name==field).i;
        var start=list.Take(end).Select((x,i)=>(x,i)).Where(x=>x.x.OpCode.Code==Code.Stsfld).Select(x=>x.i).DefaultIfEmpty(-1).Last();
        return list.Skip(start+1).Take(end-start-1).Where(x=>x.Operand is MethodReference m && m.Name=="Add" && m.DeclaringType is GenericInstanceType g && g.GenericArguments.Single().MetadataType==MetadataType.Int32).Select(x=>Integer(x.Previous).Value).ToArray();
    }
    private static int? Integer(Instruction i)=>i.OpCode.Code switch { Code.Ldc_I4_M1=>-1,Code.Ldc_I4_0=>0,Code.Ldc_I4_1=>1,Code.Ldc_I4_2=>2,Code.Ldc_I4_3=>3,Code.Ldc_I4_4=>4,Code.Ldc_I4_5=>5,Code.Ldc_I4_6=>6,Code.Ldc_I4_7=>7,Code.Ldc_I4_8=>8,Code.Ldc_I4 or Code.Ldc_I4_S=>Convert.ToInt32(i.Operand),_=>null };
}
