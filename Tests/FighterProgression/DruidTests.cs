using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;

// Production planning/rules run with managed doubles. Cecil/blueprint assertions do NOT execute Unity/Harmony.
internal static class DruidTests
{
    private static readonly string[] Official = { "CircleBalance", "CircleKindred", "CircleLand", "CircleWinds" };
    private static readonly string[] Ub = { "CircleOfTheAncientForest", "CircleOfTheCosmos", "CircleOfTheForestGuardian", "CircleOfTheLife", "CircleOfTheNight", "CircleOfTheWildfire" };
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m, string name, string owner = null) => m.HasBody && m.Body.Instructions.Any(i =>
        i.Operand is MethodReference r && r.Name == name && (owner == null || r.DeclaringType.Name == owner));
    private static JsonElement Read(string name, string folder = "OfficialBlueprints")
    {
        var path = Directory.GetFiles("Diagnostics/" + folder, name + ".json", SearchOption.AllDirectories).FirstOrDefault() ??
                   Directory.GetFiles("Diagnostics/" + (folder == "OfficialBlueprints" ? "UnfinishedBusinessBlueprints" : "OfficialBlueprints"), name + ".json", SearchOption.AllDirectories).FirstOrDefault();
        if (path == null) return default;
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); return doc.RootElement.Clone();
    }
    private static string Name(string reference) => reference?.Split(':').ElementAtOrDefault(1);
    private static FeatureDefinition Feature(string name, string folder, Dictionary<string, FeatureDefinition> cache)
    {
        if (cache.TryGetValue(name, out var result)) return result;
        var json = Read(name, folder);
        if (json.ValueKind == JsonValueKind.Undefined) return cache[name] = new FeatureDefinition { Name = name };
        if (json.TryGetProperty("poolType", out var poolType))
            return cache[name] = new FeatureDefinitionPointPool { Name = name, PoolType = poolType.GetString(), PoolAmount = json.GetProperty("poolAmount").GetInt32() };
        if (json.TryGetProperty("featureSet", out var children))
        {
            var set = new FeatureDefinitionFeatureSet { Name = name, Mode = json.GetProperty("mode").GetString() == "Exclusion"
                ? FeatureDefinitionFeatureSet.FeatureSetMode.Exclusion : FeatureDefinitionFeatureSet.FeatureSetMode.Union };
            cache[name] = set; set.FeatureSet.AddRange(children.EnumerateArray().Select(x => Feature(Name(x.GetString()), folder, cache))); return set;
        }
        if (json.TryGetProperty("overriddenPower", out var previous))
        {
            var power = new FeatureDefinitionPower { Name = name }; cache[name] = power;
            if (previous.ValueKind == JsonValueKind.String) power.OverriddenPower = Feature(Name(previous.GetString()), folder, cache) as FeatureDefinitionPower;
            return power;
        }
        return cache[name] = new FeatureDefinition { Name = name };
    }
    private static List<FeatureUnlockByLevel> Fixture(string name, string folder)
    {
        var cache = new Dictionary<string, FeatureDefinition>();
        var source = Read(name, folder).GetProperty("featureUnlocks").EnumerateArray().Select(x =>
            new FeatureUnlockByLevel(Feature(Name(x.GetProperty("featureDefinition").GetString()), folder, cache), x.GetProperty("level").GetInt32())).ToList();
        if (name == "Druid")
        {
            foreach (var (feature, original) in new[] { ("SummoningAffinityDruidWildshape",2), ("FeatureDruidBeastSpells",18), ("FeatureSetAbilityScoreChoice",19), ("MagicAffinityArchDruid",20) })
                source.Add(new(Feature(feature, folder, cache), original));
        }
        return source.OrderBy(x => x.Level).ToList();
    }
    private static IEnumerable<FeatureDefinition> Leaves(IEnumerable<FeatureUnlockByLevel> source) =>
        source.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x => x is not FeatureDefinitionFeatureSet);
    private static string Signature(FeatureDefinition f) => f is FeatureDefinitionPointPool p ? $"{p.PoolType}:{p.PoolAmount}" : f.Name;
    internal static void Run(Action<bool,string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        check(Enumerable.Range(1,12).Select(n => DruidProgressionRules.EffectiveLevel(true,n)).SequenceEqual(new[] { 1,2,3,4,5,7,9,11,13,15,17,20 }), "Druid: exact mapping20-to12");
        check(Enumerable.Range(1,12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).SequenceEqual(Enumerable.Range(1,20)), "Druid: every original advancement event exactly once");
        check(DruidProgressionRules.IsCapped(true,12) && DruidProgressionRules.IsCapped(true,13) && !DruidProgressionRules.IsCapped(true,11) && !DruidProgressionRules.IsCapped(false,12), "Druid: opt-in class cap12");
        check(Enumerable.Range(0,21).All(n => DruidProgressionRules.EffectiveLevel(false,n)==n) && DruidProgressionRules.EffectiveLevel(true,13)==13, "Druid: disabled and unsupported old levels retain actual rank");
        var init = new FighterProgressionInitialization(); var disabled = new FighterProgressionInitialization();
        check(init.ShouldInitialize(true) && !init.ShouldInitialize(true) && !disabled.ShouldInitialize(false) && !disabled.ShouldInitialize(true), "Druid: once-only initialization including disabled latch");
        foreach (var name in new[] { "Druid" }.Concat(Official).Concat(Ub))
        {
            var folder = Ub.Contains(name) ? "UnfinishedBusinessBlueprints" : "OfficialBlueprints";
            var source = Fixture(name,folder); var snapshot = source.Select(x => (x.FeatureDefinition,x.Level)).ToArray();
            var powers = Leaves(source).OfType<FeatureDefinitionPower>().Select(x => (Power:x,Previous:x.OverriddenPower)).ToArray();
            var transformer = new CompressedProgressionDefinitions("DruidFixture"+name); var result = transformer.Prepare(source,name);
            check(Leaves(result).Select(Signature).SequenceEqual(Leaves(source).Select(Signature)), "Druid: complete ordered source inventory: "+name);
            check(result.All(x=>x.Level<=12) && source.Select(x=>(x.FeatureDefinition,x.Level)).SequenceEqual(snapshot), "Druid: immutable source and capped result: "+name);
            check(transformer.PoolOrigins.Values.Distinct().Count()==transformer.PoolOrigins.Count && powers.All(x=>Leaves(result).Contains(x.Power) && ReferenceEquals(x.Power.OverriddenPower,x.Previous)), "Druid: repeated choices/upgrade references retained: "+name);
            foreach (var definition in source.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Select(x=>Read(x.Name,folder))
                         .Where(x=>x.ValueKind!=JsonValueKind.Undefined && x.TryGetProperty("autoPreparedSpellsGroups",out _)))
            {
                var groups = definition.GetProperty("autoPreparedSpellsGroups").EnumerateArray().ToArray();
                var projected = groups.Select(x=>(Level:FighterProgressionPlan<int>.CompressedLevel(x.GetProperty("classLevel").GetInt32()),Spells:x.GetProperty("spellsList").EnumerateArray().Select(s=>s.GetString()).ToArray())).ToArray();
                check(Name(definition.GetProperty("spellcastingClass").GetString())=="Druid" && Enumerable.Range(1,12).All(n=>
                    projected.Where(x=>x.Level<=n).SelectMany(x=>x.Spells).SequenceEqual(groups.Where(x=>x.GetProperty("classLevel").GetInt32()<=DruidProgressionRules.EffectiveLevel(true,n)).SelectMany(x=>x.GetProperty("spellsList").EnumerateArray().Select(s=>s.GetString())))),
                    "Druid: own circle repertoire and exact original threshold availability: "+definition.GetProperty("name").GetString());
            }
        }
        CheckCasting(check,types); CheckShapes(check); CheckVariants(check); CheckMetadata(check,types,game);
    }
    private static void CheckCasting(Action<bool,string> check, TypeDefinition[] types)
    {
        var casting = Read("CastSpellDruid");
        check(casting.GetProperty("spellKnowledge").GetString()=="WholeList" && casting.GetProperty("spellReadyness").GetString()=="Prepared" && new[] { "knownSpells","scribedSpells","replacedSpells" }.All(k=>casting.GetProperty(k).EnumerateArray().All(x=>x.GetInt32()==0)), "Druid: real WholeList/Prepared source, no book/known-spell engine");
        var cantrips = casting.GetProperty("knownCantrips").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        check(SpellcastingProgressionPlan.ProjectCumulative(cantrips).Take(12).SequenceEqual(new[] { 2,2,2,3,3,3,3,4,4,4,5,5 }), "Druid: native cantrip2/3/4/5 at actual1/4/8/11, not assumed external table");
        var raw = AddedIntegers(types.Single(x=>x.Name=="FeatureDefinitionCastSpellBuilder"),"SlotsByCasterLevel");
        var rows = Enumerable.Range(1,20).Select(n=>raw.Skip(n*10).Take(10).ToArray()).ToArray();
        var output = SpellcastingProgressionPlan.ProjectRows(rows);
        for(var n=1;n<=12;n++)check(output[n-1].SequenceEqual(rows[DruidProgressionRules.EffectiveLevel(true,n)-1]),"Druid: full-caster slot row at actual"+n);
        check(output[10][8]==1 && output[11].SequenceEqual(new[] {4,3,3,3,3,2,2,1,1,0}),"Druid: ninth circle11 and original20 slots12");
        var points = AddedIntegers(types.Single(x=>x.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel");
        check(Enumerable.Range(1,12).Select(n=>points[SpellcastingProgressionPlan.CasterLevel(n)]).SequenceEqual(new[] {4,6,14,17,27,38,57,73,83,94,107,133}),"Druid: real UB Spell Points through133 before modifiers");
        var prepared = AddedIntegers(types.Single(x=>x.Name=="ComputeMaxPreparedSpells_Patch"),"ClericOrDruidOneDndPreparedSpellsTable");
        check(SpellcastingProgressionPlan.ProjectCumulative(prepared).Take(12).SequenceEqual(new[] {4,5,6,7,9,11,14,16,17,18,19,22}),"Druid: actual2024 prepared table, own effective index");
        check(DruidProgressionRules.LegacyPreparedLimit(20,5,0)==25 && DruidProgressionRules.LegacyPreparedLimit(17,4,3)==24 && DruidProgressionRules.LegacyPreparedLimit(1,-5,0)==1,"Druid: production WIS/affinity preparation and final minimum");
        var ledger = new DruidSpellGrantLedger();
        check(ledger.Grant("Druid6","A") && !ledger.Grant("Druid6","A") && ledger.Grant("Druid6","B") && !ledger.HasStarted("Druid7") && ledger.Grant("Druid7","A"),"Druid: duplicate grant rejected, separate origins and next level permitted");
        check(DruidProgressionRules.IsOriginTag("03ClassDruid1PrimalOrder","03ClassDruid1") && !DruidProgressionRules.IsOriginTag("03ClassDruid12","03ClassDruid1"),"Druid: exact origin boundary prevents stale choices leaking across levels");
    }
    private static void CheckShapes(Action<bool,string> check)
    {
        foreach(var (power,folder) in new[] {("PowerDruidWildShape","OfficialBlueprints"),("PowerCircleOfTheNightWildShapeCombat","UnfinishedBusinessBlueprints")})
        {
            var data = Read(power,folder); var effect = data.GetProperty("effectDescription");
            var form = effect.GetProperty("effectForms").EnumerateArray().Single(x=>x.GetProperty("formType").GetString()=="ShapeChange").GetProperty("shapeChangeForm");
            var options = form.GetProperty("shapeOptions").EnumerateArray().Select(x=>(Original:x.GetProperty("requiredLevel").GetInt32(),Name:Name(x.GetProperty("substituteMonster").GetString()))).ToArray();
            check(form.GetProperty("shapeChangeType").GetString()=="ClassLevelListSelection" && effect.GetProperty("durationType").GetString()=="HalfClassLevelHours", "Druid: installed catalog and half-class duration: "+power);
            check(options.Length==(power=="PowerDruidWildShape"?10:15), "Druid: actual form inventory count: "+power);
            for(var actual=1;actual<=12;actual++)
            {
                var effective = DruidProgressionRules.EffectiveLevel(true,actual);
                check(options.Where(x=>DruidProgressionRules.CanSelectForm(effective,x.Original,4,1)).Select(x=>x.Name).SequenceEqual(options.Where(x=>x.Original<=effective).Select(x=>x.Name)),"Druid: production form eligibility versus source event at actual"+actual+": "+power);
            }
            foreach(var shape in options)
            {
                var monster = Read(shape.Name,folder);
                check(monster.ValueKind!=JsonValueKind.Undefined && monster.GetProperty("standardHitPoints").GetInt32()>0 && monster.GetProperty("challengeRating").GetDouble()>=0,
                    "Druid: form retains actual creature HP/CR rather than rewriting monster stats: "+shape.Name);
            }
        }
        check(Enumerable.Range(1,12).Select(n=>2+DruidProgressionRules.AdditionalWildShapeUses(DruidProgressionRules.EffectiveLevel(true,n))).SequenceEqual(new[] {2,2,2,2,2,3,3,3,3,3,4,4}),"Druid: production2024 pool uses2/3/4 at actual2/6/11; acquisition still2");
        check(!DruidProgressionRules.CanSelectForm(9,10,4,2) && !DruidProgressionRules.CanSelectForm(11,10,1,2) && DruidProgressionRules.CanSelectForm(11,10,2,2) && DruidProgressionRules.CanSelectForm(15,14,2,2),"Druid: Night elemental/monstrous requirements and exact two-use budget");
        check(new[] {"ConditionCircleOfTheCosmosArcher", "ConditionCircleOfTheCosmosChalice14", "ConditionCircleOfTheCosmosDragon10"}.All(DruidProgressionRules.IsStarryCondition) &&
            !DruidProgressionRules.IsStarryCondition("ConditionRaging"), "Druid: production respec/level-up guard recognizes Starry Form variants without blocking unrelated conditions");
        var transaction = new DruidShapeUseJournal(3,2);
        check(!transaction.TryRefund(3,out _), "Druid: unsuccessful/unapplied shape cannot refund resources");
        check(transaction.TryFinish(3,out var charged) && charged==1, "Druid: production Night transaction charges two uses from pre-native snapshot");
        check(!transaction.TryFinish(3,out _), "Druid: repeated native completion cannot charge or grant temporary HP twice");
        check(transaction.TryRefund(3,out var restored) && restored==3 && !transaction.TryRefund(3,out _), "Druid: legacy Archdruid refunds full pool once, not a second independent pool");
        var ordinary = new DruidShapeUseJournal(4,1);
        check(ordinary.TryFinish(4,out var after) && after==3, "Druid: ordinary2024 shape consumes exactly one use");
        check(!DruidProgressionRules.CanSelectForm(20,0,4,1) && !DruidProgressionRules.CanSelectForm(20,2,0,1),"Druid: unknown catalog entry and exhausted pool rejected");
        check(DruidProgressionRules.CanRegainShape(0,1,2,2,true) && !DruidProgressionRules.CanRegainShape(0,1,1,2,true) && !DruidProgressionRules.CanRegainShape(1,1,10,2,true) && !DruidProgressionRules.CanRegainShape(0,0,10,0,true) && DruidProgressionRules.CanRegainShape(0,1,0,2,false),"Druid: production Resurgence zero-shapes gate, paid points/slot and no free conversion");
    }
    private static void CheckVariants(Action<bool,string> check)
    {
        for(var flags=0;flags<32;flags++)
        {
            bool On(int bit)=>(flags&(1<<bit))!=0;
            var source = Fixture("Druid","OfficialBlueprints");
            source.RemoveAll(x=>x.FeatureDefinition.Name=="MagicAffinityArchDruid");
            source.Add(new(new FeatureDefinition { Name=On(0)?"FeatureSetDruidArchDruid":"MagicAffinityArchDruid" },20));
            if(On(1)){ source.Add(new(Feature("FeatureSetDruidElementalFury","UnfinishedBusinessBlueprints",new()),7));source.Add(new(new FeatureDefinition {Name="FeatureDruidImprovedElementalFury"},15)); }
            if(On(2))source.Add(new(Feature("FeatureSetDruidPrimalOrder","UnfinishedBusinessBlueprints",new()),1));
            source = source.Select(x=>x.FeatureDefinition.Name=="SubclassChoiceDruidCircle"?new FeatureUnlockByLevel(x.FeatureDefinition,On(3)?3:2):x).ToList();
            if(On(4))source.Add(new(new FeatureDefinition {Name="FeatureSetDruidWildResurgence"},5));
            foreach(var asi in source.Select(x=>x.FeatureDefinition).OfType<FeatureDefinitionFeatureSet>().Where(x=>x.Name=="FeatureSetAbilityScoreChoice"))asi.Mode=FeatureDefinitionFeatureSet.FeatureSetMode.Union;
            var feat = new FeatureDefinitionPointPool {Name="ExtraFeat",PoolType="Feat",PoolAmount=1}; source.AddRange(new[] {2,6,10,14,18}.Select(n=>new FeatureUnlockByLevel(feat,n)));
            var transformer = new CompressedProgressionDefinitions("DruidVariant"+flags);var output = transformer.Prepare(source,"Druid");
            var leaves=Leaves(output).ToArray();var pools=leaves.OfType<FeatureDefinitionPointPool>().ToArray();
            check(pools.Where(x=>x.PoolType=="AbilityScore").Sum(x=>x.PoolAmount)==10 && pools.Where(x=>x.PoolType=="Feat").Sum(x=>x.PoolAmount)==10 && transformer.PoolOrigins.Values.Distinct().Count()==transformer.PoolOrigins.Count,"Druid: variant"+flags+" retains ASI+feat and repeated extra rewards/origins");
            check(output.Single(x=>x.FeatureDefinition.Name=="SubclassChoiceDruidCircle").Level==(On(3)?3:2) && leaves.Any(x=>x.Name=="FeatureSetDruidWildResurgence")==On(4),"Druid: variant"+flags+" selected circle timing and Resurgence5");
            var high = output.Single(x=>x.Level==12).FeatureDefinition as FeatureDefinitionFeatureSet;
            check(high.FeatureSet.Select(x=>x.Name).Where(x=>x is "FeatureDruidBeastSpells" or "MagicAffinityArchDruid" or "FeatureSetDruidArchDruid").SequenceEqual(new[] {"FeatureDruidBeastSpells",On(0)?"FeatureSetDruidArchDruid":"MagicAffinityArchDruid"}) && leaves.Count(x=>x.Name is "FeatureSetDruidArchDruid" or "MagicAffinityArchDruid")==1,"Druid: variant"+flags+" Beast18 before ASI19/capstone20, exclusive Archdruid");
            check(On(1)?output.Where(x=>x.Level==6).SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Any(x=>x.Name=="FeatureSetDruidElementalFury") && output.Where(x=>x.Level==10).SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Any(x=>x.Name=="FeatureDruidImprovedElementalFury"):!leaves.Any(x=>x.Name=="FeatureDruidImprovedElementalFury"),"Druid: variant"+flags+" Fury7/15 maps6/10 without removed feature resurrection");
            var order = output.SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionFeatureSet>().FirstOrDefault(x=>x.Name=="FeatureSetDruidPrimalOrder");
            check(On(2)?order?.Mode==FeatureDefinitionFeatureSet.FeatureSetMode.Exclusion && order.FeatureSet.Count==2:order==null,"Druid: variant"+flags+" Magician/Warden remains exclusive");
        }
    }
    private static void CheckMetadata(Action<bool,string> check, TypeDefinition[] types, AssemblyDefinition game)
    {
        var context=types.Single(x=>x.Name=="DruidProgressionContext");var casting=types.Single(x=>x.Name=="DruidSpellcastingContext");var wild=types.Single(x=>x.Name=="DruidWildshapeContext");
        var startup=types.SelectMany(x=>x.Methods).Single(x=>Calls(x,"LateLoad",context.Name));
        int Position(string owner)=>startup.Body.Instructions.Select((x,i)=>(x,i)).Single(x=>x.x.Operand is MethodReference m && m.Name=="LateLoad" && m.DeclaringType.Name==owner).i;
        check(new[] {"ClericProgressionContext","BardProgressionContext","WarlockProgressionContext","Tabletop2024Context","SpellPointsContext"}.All(x=>Position(context.Name)>Position(x)),"Druid metadata: final initialization follows previous classes and final rules");
        var settings=types.Single(x=>x.FullName=="SolastaUnfinishedBusiness.Settings");
        check(settings.Methods.Where(x=>x.IsConstructor && !x.IsStatic).All(m=>!m.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name.Contains("EnableDruidCompressedProgressionPrototype"))),"Druid metadata: CLR false opt-in default");
        var builds=types.Where(x=>x.BaseType?.Name=="AbstractSubclass" && x.Methods.Any(m=>m.Name=="get_Klass" && Calls(m,"get_Druid"))).ToArray();
        check(builds.Select(x=>x.Name).OrderBy(x=>x).SequenceEqual(Ub.OrderBy(x=>x)),"Druid metadata: all6 actual registered UB Circle builders");
        check(Read("SubclassChoiceDruidCircle").GetProperty("subclasses").EnumerateArray().Select(x=>x.GetString()).OrderBy(x=>x).SequenceEqual(Official.OrderBy(x=>x)),"Druid: all4 official Circles are actual choices");
        check(Calls(context.Methods.Single(x=>x.Name=="DiscoverSubclasses"),"get_KlassListSubclassEnabled"),"Druid metadata: disabled/hidden Circle choices are not forcibly enabled");
        var rules=types.Single(x=>x.Name=="Tabletop2024Context");
        foreach(var (method,flag) in new[] {("ArchDruid","EnableDruidArchDruid2024"),("ElementalFury","EnableDruidElementalFury2024"),("MetalArmor","EnableDruidMetalArmor2024"),("PrimalOrder","EnableDruidPrimalOrder2024"),("WeaponProficiency","EnableDruidWeaponProficiency2024"),("WildResurgence","EnableDruidWildResurgence2024"),("Wildshape","EnableDruidWildshape2024"),("CircleLearningLevel","EnableDruidToLearnCircleAtLevel3")})
            check(Calls(rules.Methods.Single(x=>x.Name=="SwitchDruid"+method),"get_"+flag),"Druid metadata: enabled/disabled source rule remains authoritative: "+flag);
        check(context.NestedTypes.Count(x=>x.Name.EndsWith("RestartPatch"))==8 && Calls(casting.Methods.Single(x=>x.Name=="BeginFinalize"),"get_SessionConsistent"),"Druid metadata: eight rule switches require full restart and stale finalization is rejected");
        var late=types.Single(x=>x.Name=="Level20Context");var load=late.Methods.Single(x=>x.Name=="DruidLoad");
        check(load.Body.Instructions.Any(x=>x.Operand as string=="FeatureDruidBeastSpells") && new[] {18,19,20}.All(n=>load.Body.Instructions.Any(x=>Integer(x)==n)) && Calls(load,"EnumerateSlotsPerLevel"),"Druid metadata: installed Beast18/ASI19/Arch20 source and full-caster rows");
        check(Calls(rules.Methods.Single(x=>x.Name=="SwitchDruidArchDruid"),"RemoveAll") && late.NestedTypes.Any(x=>x.Name=="ActionFinishedByMeArchDruid") && rules.NestedTypes.Any(x=>x.Name=="InitiativeEndListenerDruidEvergreenWildShape"),"Druid metadata: exclusive legacy refund versus2024 Evergreen/Nature Magician, not both");
        check(Types(rules).SelectMany(x=>x.Methods).Any(x=>Calls(x,"EffectiveLevel",context.Name)),"Druid metadata:2024 Wild Shape temporary HP uses scoped effective class rank");
        foreach(var name in new[] {"CircleOfTheLife","CircleOfTheCosmos","CircleOfTheForestGuardian","CircleOfTheAncientForest"})
            check(Types(types.Single(x=>x.Name==name)).SelectMany(x=>x.Methods).Any(x=>Calls(x,"EffectiveLevel",context.Name)),"Druid metadata: direct Circle class-rank callback adapted: "+name);
        var life=types.Single(x=>x.Name=="CircleOfTheLife").Methods.Single(x=>x.Name=="GetDruidLevel");
        check(Calls(life,"GetCharacterByGuid") && Calls(life,"EffectiveLevel",context.Name),"Druid metadata: Life GUID resolves originating Druid before class-rank scaling");
        check(Types(types.Single(x=>x.Name=="ModifyPowerPoolAmount")).SelectMany(x=>x.Methods).Any(x=>Calls(x,"AdditionalWildShapeUses","DruidProgressionRules")),"Druid metadata:2024 Wild Shape and Cosmos pool use tested production extras");
        check(Calls(types.Single(x=>x.Name=="RulesetEffectPower_Ctor_Patch").Methods.Single(x=>x.Name=="Custom"),"IsWildShapePower",wild.Name) && Calls(types.Single(x=>x.Name=="RulesetEffectPower_Ctor_Patch").Methods.Single(x=>x.Name=="Custom"),"EffectiveLevel",context.Name),"Druid metadata: native duration class-rank override scoped to Wild Shape source powers");
        var canCast=types.Single(x=>x.Name=="CanCastSpells_Patch").Methods.Single(x=>x.Name=="Postfix");
        check(Calls(canCast,"IsSupportedWildShape",wild.Name) && Calls(canCast,"EffectiveLevel",context.Name),"Druid metadata: Beast Spells applies only to supported Wild Shape, preserves native spell/rage restrictions");
        var formLevel=wild.NestedTypes.Single(x=>x.Name=="FormCastingLevelPatch").Methods.Single(x=>x.Name=="Postfix");
        check(Calls(formLevel,"GetClassLevel") && !Calls(formLevel,"EffectiveLevel",context.Name),"Druid metadata: form casting indexes projected tables by actual Druid instead of total character level or double compression");
        check(Calls(wild.Methods.Single(x=>x.Name=="Allowed"),"CanSelectForm","DruidProgressionRules") && Calls(wild.Methods.Single(x=>x.Name=="CanApply"),"Allowed") && Calls(wild.Methods.Single(x=>x.Name=="CanStart"),"Allowed"),"Druid metadata: same source catalog and production rank/cost rule at action and application boundaries");
        check(Calls(types.Single(x=>x.Name=="ShapeSelectionPanelPatcher").NestedTypes.Single(x=>x.Name=="Show_Patch").Methods.Single(x=>x.Name=="Prefix"),"RequiredLevel",wild.Name),"Druid metadata: shape UI uses source-specific compressed requirement without changing monster definitions");
        check(Calls(wild.Methods.Single(x=>x.Name=="SpellPointOwner"),"set_Item") && Calls(types.Single(x=>x.Name=="RulesetSpellRepertoireExtensions").Methods.Single(x=>x.Name=="GetCaster"),"SpellPointOwner",wild.Name),"Druid metadata: form uses authoritative hero Spell Points pool/repertoire owner");
        check(Calls(types.Single(x=>x.Name=="SpellPointsContext").Methods.Single(x=>x.Name=="IsSpellPointsEnabled"),"IsSupportedWildShape",wild.Name) && Calls(wild.NestedTypes.Single(x=>x.Name=="FormRefreshPatch").Methods.Single(x=>x.Name=="Postfix"),"SpellPointOwner",wild.Name),"Druid metadata: points enabled only for supported forms and rebound on form refresh/load");
        check(Calls(types.Single(x=>x.Name=="ModifyPowerPoolAmountPowerSpellPoints").Methods.Single(x=>x.Name=="GetCasterLevel"),"IsSupportedHero",casting.Name) && Calls(types.Single(x=>x.Name=="SharedSpellsContext").Methods.Single(x=>x.Name=="GetSharedCasterLevel"),"SingleCasterLevel",casting.Name),"Druid metadata: own full-caster points helper, no mixed-caster sum");
        var regen=rules.NestedTypes.Single(x=>x.Name=="CustomBehaviorPowerDruidWildResurgenceShape");
        check(regen.Methods.Any(x=>Calls(x,"CanRegainShape","DruidProgressionRules")) && Types(regen).SelectMany(x=>x.Methods).Any(x=>Calls(x,"SpendSpellSlot")),"Druid metadata: Resurgence checks paid resource and still charges native slot/points path");
        var shapePower=Read("PowerDruidWildResurgenceShape","UnfinishedBusinessBlueprints");var slotPower=Read("PowerDruidWildResurgenceSlot","UnfinishedBusinessBlueprints");
        check(shapePower.GetProperty("rechargeRate").GetString()=="TurnStart" && slotPower.GetProperty("rechargeRate").GetString()=="LongRest", "Druid: native Resurgence once-per-turn buy versus once-per-long-rest slot grant bounds conversion loop");
        check(Types(types.Single(x=>x.Name=="FunctorRespec")).SelectMany(x=>x.Methods).Any(x=>Calls(x,"CanRespec",wild.Name)),"Druid metadata: respec rejects active form/summons before creating replacement hero");
        var helper=types.Single(x=>x.Name=="MulticlassInOutRulesHelper");
        check(Types(helper).SelectMany(x=>x.Methods).Any(x=>new[] {"Wizard","Sorcerer","Warlock","Bard","Cleric","Druid"}.All(k=>Calls(x,"CanSelectClass",k+"SpellcastingContext"))) && Calls(context.Methods.Single(x=>x.Name=="CanContinue"),"EnumerateHeroAllowedClassDefinitions"),"Druid metadata: previous five caster guards retained, explicit cap deadlock, native ability/max-class rules");
        var actionPrefix = wild.NestedTypes.Single(x=>x.Name=="UsePowerActionPatch").Methods.Single(x=>x.Name=="Prefix");
        check(actionPrefix.CustomAttributes.Any(x=>x.AttributeType.Name=="HarmonyPriority") && Calls(actionPrefix,"Capture"),
            "Druid metadata: pre-charge journal runs before UB's replacement ExecuteImpl prefix");
        check(Calls(wild.Methods.Single(x=>x.Name=="FinishShape"),"TryFinish","DruidShapeUseJournal") && Calls(wild.Methods.Single(x=>x.Name=="RefundLegacyShape"),"TryRefund","DruidShapeUseJournal"),
            "Druid metadata: runtime completion/refund use tested once-only production journal");
        check(Types(types.Single(x=>x.Name=="CircleOfTheNight")).SelectMany(x=>x.Methods).Any(x=>Calls(x,"FinishShape",wild.Name)) &&
            Types(rules).SelectMany(x=>x.Methods).Any(x=>Calls(x,"FinishShape",wild.Name)), "Druid metadata: Night and2024 first-callback paths share cost/tempHP handling");
        var night=types.Single(x=>x.Name=="CircleOfTheNight");
        check(Calls(night.Methods.Single(x=>x.Name=="BuildWildShapePower"),"SetSharedPool") && night.Methods.Single(x=>x.Name=="IsTwoPointsShape").Body.Instructions.Count(x=>x.Operand is string)>=6,"Druid metadata: installed Night shared resource and six special two-use shapes preserved");
        foreach(var owner in new[] {context,casting,wild})foreach(var patch in owner.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(x=>x.AttributeType.Name=="HarmonyPatch");if(attr==null)continue;
            var type=(TypeReference)attr.ConstructorArguments[0].Value;var name=(string)attr.ConstructorArguments[1].Value;
            var target=game.MainModule.Types.FirstOrDefault(x=>x.FullName==type.FullName)??types.Single(x=>x.FullName==type.FullName);
            var methods=target.Methods.Where(x=>x.Name==name || x.Name=="get_"+name).ToArray();
            check(methods.Length>0,"Druid metadata: Harmony target: "+patch.Name);
            foreach(var p in patch.Methods.Where(x=>x.Name is "Prefix" or "Postfix").SelectMany(x=>x.Parameters).Where(x=>!x.Name.StartsWith("__")))
                check(methods.Any(x=>x.Parameters.Any(q=>q.Name==p.Name)),"Druid metadata: Harmony argument: "+patch.Name+"."+p.Name);
        }
    }
    private static int[] AddedIntegers(TypeDefinition type, string field)
    {
        var code = type.Methods.Single(x => x.Name == ".cctor").Body.Instructions;
        var end = code.Select((x, i) => (x, i)).Single(x => x.x.OpCode.Code == Code.Stsfld && x.x.Operand is FieldReference f && f.Name == field).i;
        var start = code.Take(end).Select((x, i) => (x, i)).Where(x => x.x.OpCode.Code == Code.Stsfld).Select(x => x.i).DefaultIfEmpty(-1).Last() + 1;
        return code.Skip(start).Take(end - start).Where(x => x.Operand is MethodReference m && m.Name == "Add" && m.DeclaringType is GenericInstanceType g && g.GenericArguments.Single().MetadataType == MetadataType.Int32).Select(x => Integer(x.Previous).Value).ToArray();
    }
    private static int? Integer(Instruction i) => i.OpCode.Code switch { Code.Ldc_I4_M1 => -1, Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1, Code.Ldc_I4_2 => 2, Code.Ldc_I4_3 => 3, Code.Ldc_I4_4 => 4, Code.Ldc_I4_5 => 5, Code.Ldc_I4_6 => 6, Code.Ldc_I4_7 => 7, Code.Ldc_I4_8 => 8, Code.Ldc_I4 or Code.Ldc_I4_S => Convert.ToInt32(i.Operand), _ => null };
}
