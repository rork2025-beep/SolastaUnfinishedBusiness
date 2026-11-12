using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;

// Pure production rules/transformer run against managed doubles. Cecil checks inspect metadata only.
// No Harmony execution, prepared-spell GUI, rest, combat, respec or save/load is claimed.
internal static class RangerTests
{
    private static readonly string[] Official = { "RangerHunter", "RangerMarksman", "RangerShadowTamer", "RangerSwiftBlade" };
    private static readonly string[] Ub = { "RangerArcanist", "RangerFeyWanderer", "RangerGloomStalker", "RangerHellWalker", "RangerLightBearer", "RangerSkyWarrior", "RangerSurvivalist", "RangerWildMaster" };
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m, string name, string owner = null) => m.HasBody && m.Body.Instructions.Any(i =>
        i.Operand is MethodReference r && r.Name == name && (owner == null || r.DeclaringType.Name == owner));
    private static readonly Dictionary<(string,string),JsonElement> JsonCache=new();
    private static JsonElement Read(string name, string folder = "OfficialBlueprints")
    {
        if(JsonCache.TryGetValue((name,folder),out var cached))return cached;
        var path = Directory.GetFiles("Diagnostics/" + folder, name + ".json", SearchOption.AllDirectories).FirstOrDefault() ??
                   Directory.GetFiles("Diagnostics/" + (folder == "OfficialBlueprints" ? "UnfinishedBusinessBlueprints" : "OfficialBlueprints"), name + ".json", SearchOption.AllDirectories).FirstOrDefault();
        if (path == null) return default;
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); return JsonCache[(name,folder)]=doc.RootElement.Clone();
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


    private static List<FeatureUnlockByLevel> Fixture(string name)
    {
        var folder=Ub.Contains(name)?"UnfinishedBusinessBlueprints":"OfficialBlueprints";var cache=new Dictionary<string,FeatureDefinition>();
        var source=Read(name,folder).GetProperty("featureUnlocks").EnumerateArray().Select(x=>new FeatureUnlockByLevel(Feature(Name(x.GetProperty("featureDefinition").GetString()),folder,cache),x.GetProperty("level").GetInt32())).ToList();
        if(name=="Ranger")
        {
            source=source.Select(x=>x.FeatureDefinition.Name=="AdditionalDamageRangerFavoredEnemyChoice"?new FeatureUnlockByLevel(new FeatureDefinition{Name="InvocationPoolRangerPreferredEnemy"},x.Level):
                x.FeatureDefinition.Name=="TerrainTypeAffinityRangerNaturalExplorerChoice"?new FeatureUnlockByLevel(new FeatureDefinition{Name="InvocationPoolRangerTerrainType"},x.Level):x).ToList();
            source.Add(new(Feature("SenseRangerFeralSenses",folder,cache),18));source.Add(new(Feature("FeatureSetAbilityScoreChoice",folder,cache),19));source.Add(new(Feature("FeatureRangerFoeSlayer",folder,cache),20));
        }
        return source.OrderBy(x=>x.Level).ToList();
    }
    private static IEnumerable<FeatureDefinition> Leaves(IEnumerable<FeatureUnlockByLevel> source)=>source.SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x=>x is not FeatureDefinitionFeatureSet);
    private static string Signature(FeatureDefinition f)=>f is FeatureDefinitionPointPool p?$"{p.PoolType}:{p.PoolAmount}":f.Name;
    private static bool Reject(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();
        check(Enumerable.Range(1,12).Select(n=>RangerProgressionRules.EffectiveLevel(true,n)).SequenceEqual(new[]{1,2,3,4,5,7,9,11,13,15,17,20}),"Ranger production rules: exact mapping");
        check(Enumerable.Range(1,12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).SequenceEqual(Enumerable.Range(1,20)),"Ranger production planner: all original events exactly once");
        foreach(var actual in Enumerable.Range(0,21))
            check(RangerProgressionRules.EffectiveLevel(false,actual)==actual && RangerProgressionRules.IsCapped(true,actual)==(actual>=12) && !RangerProgressionRules.IsCapped(false,actual),"Ranger production rules: disabled mode and class12 cap:"+actual);
        var on=new FighterProgressionInitialization();var off=new FighterProgressionInitialization();
        check(on.ShouldInitialize(true)&&!on.ShouldInitialize(true)&&!off.ShouldInitialize(false)&&!off.ShouldInitialize(true),"Ranger production initialization: enabled and disabled latch exactly once");
        foreach(var name in new[]{"Ranger"}.Concat(Official).Concat(Ub))
        {
            var source=Fixture(name);var before=source.Select(x=>(x.FeatureDefinition,x.Level)).ToArray();var transformer=new CompressedProgressionDefinitions("RangerFixture"+name);var result=transformer.Prepare(source,name);
            check(Leaves(result).Select(Signature).SequenceEqual(Leaves(source).Select(Signature)),"Ranger production transformer: complete ordered base/subclass leaves:"+name);
            check(before.SequenceEqual(source.Select(x=>(x.FeatureDefinition,x.Level)))&&result.All(x=>x.Level<=12),"Ranger production transformer: input unchanged and all output capped:"+name);
            check(transformer.PoolOrigins.Count==transformer.PoolOrigins.Values.Distinct().Count(),"Ranger production transformer: independent point-pool origins:"+name);
            foreach(var actual in Enumerable.Range(1,12))check(Leaves(result.Where(x=>x.Level<=actual)).Select(Signature).SequenceEqual(Leaves(source.Where(x=>x.Level<=RangerProgressionRules.EffectiveLevel(true,actual))).Select(Signature)),"Ranger production transformer: full inventory through actual"+actual+":"+name);
            foreach(var power in Leaves(source).OfType<FeatureDefinitionPower>())check(Leaves(result).Contains(power),"Ranger production transformer: power/upgrade identity retained:"+power.Name);
            if(name=="Ranger")continue;
            var folder=Ub.Contains(name)?"UnfinishedBusinessBlueprints":"OfficialBlueprints";
            foreach(var auto in Leaves(source).Select(x=>Read(x.Name,folder)).Where(x=>x.ValueKind!=JsonValueKind.Undefined&&x.TryGetProperty("autoPreparedSpellsGroups",out _)))
            {
                check(Name(auto.GetProperty("spellcastingClass").GetString())=="Ranger","Ranger source blueprint: bonus spell ownership:"+name);
                foreach(var actual in Enumerable.Range(1,12))
                {
                    var groups=auto.GetProperty("autoPreparedSpellsGroups").EnumerateArray().ToArray();
                    check(groups.Where(x=>FighterProgressionPlan<int>.CompressedLevel(x.GetProperty("classLevel").GetInt32())<=actual).SelectMany(x=>x.GetProperty("spellsList").EnumerateArray().Select(s=>s.GetString())).SequenceEqual(groups.Where(x=>x.GetProperty("classLevel").GetInt32()<=RangerProgressionRules.EffectiveLevel(true,actual)).SelectMany(x=>x.GetProperty("spellsList").EnumerateArray().Select(s=>s.GetString()))),"Ranger source blueprint/projection: bonus spells/counts/thresholds:"+name+"@"+actual);
                }
            }
        }
        Casting(check,types);Transactions(check);Invocations(check);Variants(check);Mechanics(check);Metadata(check,types,game);
    }
    private static void Casting(Action<bool,string> check,TypeDefinition[] types)
    {
        var slotTable=AddedIntegers(types.Single(x=>x.Name=="FeatureDefinitionCastSpellBuilder"),"SlotsByCasterLevel");
        var points=AddedIntegers(types.Single(x=>x.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel");
        foreach(var roundUp in new[]{false,true})
        {
            var rows=Enumerable.Range(1,20).Select(n=>slotTable.Skip(10*RangerProgressionRules.CasterContribution(n,roundUp)).Take(10).ToArray()).ToArray();var projected=SpellcastingProgressionPlan.ProjectRows(rows);
            foreach(var actual in Enumerable.Range(1,12))
            {
                var effective=RangerProgressionRules.EffectiveLevel(true,actual);var contribution=RangerProgressionRules.CasterContribution(effective,roundUp);
                check(projected[actual-1].SequenceEqual(rows[effective-1])&&projected[actual-1].Skip(5).All(x=>x==0),$"Ranger production half projection: exact own row and no sixth circle: start1={roundUp}@{actual}");
                check(contribution is >=0 and <=10 && points[contribution]<=64,$"Ranger production contribution/compiled budget: bounded single half caster: start1={roundUp}@{actual}");
            }
            check(projected[0].Any(x=>x>0)==roundUp&&projected[11].Take(5).SequenceEqual(new[]{4,3,3,3,2})&&points[RangerProgressionRules.CasterContribution(20,roundUp)]==64,"Ranger production half projection: source start1/2, final fifth circle and64 points:"+roundUp);
            check(projected[6][2]==2&&projected[10][4]==1,"Ranger production half projection: circle3 at actual7 and circle5 at actual11:"+roundUp);
        }
        var text=File.ReadAllText("SolastaUnfinishedBusiness/Models/2024SpellsContext.cs");
        var tables=Regex.Matches(text,@"CastSpellRanger\.knownSpells\s*=\s*\[([^]]+)\]").Select(m=>m.Groups[1].Value.Split(',').Select(x=>int.Parse(x.Trim())).ToArray()).ToArray();
        check(tables.Length==4&&tables.All(x=>x.Length==20),"Ranger source tables: all four authoritative2024/legacy and start1/2 known variants found");
        for(var i=0;i<tables.Length;i++)
        {
            var plan=new KnownSpellsProgressionPlan(tables[i],Enumerable.Range(1,20).Select(n=>n>=3?1:0));
            var projected=SpellcastingProgressionPlan.ProjectCumulative(plan.Known);
            foreach(var actual in Enumerable.Range(1,12))check(projected[actual-1]==tables[i][RangerProgressionRules.EffectiveLevel(true,actual)-1]&&plan.Events(actual).Sum(x=>x.Learned)==SpellcastingProgressionPlan.OriginalLevels(actual).Sum(plan.Learned),$"Ranger production known plan: final source total and every individual learning event: table={i}@{actual}");
            var state=new RangerSpellLearningState<string>(Enumerable.Range(0,plan.Known[16]).Select(n=>"base"+n));
            foreach(var e in plan.Events(12))state=state.Apply(e.Original==20?new[]{"learned19"}:Array.Empty<string>(),e.Original==19?new[]{"learned19"}:e.Original==20?new[]{"replaced20"}:Array.Empty<string>(),e.Learned,e.Replaced,true,_=>true);
            check(state.Known.Count==tables[i][19]&&state.Known.Contains("replaced20")&&!state.Known.Contains("learned19"),"Ranger production source-planned actual12: replace ordinary spell learned at original19 during original20 in same advancement: table="+i);
            check(projected[11]==(i<2?15:11)&&tables[i][0]==(i%2==0?2:0),"Ranger production known plan: source2024 reaches15, legacy11; correct start1/2: table="+i);
            check(plan.Events(12).Select(x=>x.Original).SequenceEqual(new[]{18,19,20})&&plan.Events(12).Sum(x=>x.Replaced)==3&&plan.CompressedReplacements().Take(12).Sum()==18,"Ranger production known plan: optional replacements retain all18 events, three at12: table="+i);
        }
        var costs=AddedIntegers(types.Single(x=>x.Name=="SpellPointsContext"),"SpellCostByLevel");check(costs.Take(6).SequenceEqual(new[]{0,2,3,5,6,7}),"Ranger compiled resource table: normal circle1..5 costs preserved");
    }
    private static void Transactions(Action<bool,string> check)
    {
        var initial=new RangerSpellLearningState<string>(new[]{"old"});
        var six=initial.Apply(Array.Empty<string>(),new[]{"learned6"},1,1,true,_=>true);
        var seven=six.Apply(new[]{"learned6"},new[]{"learned7"},0,1,true,_=>true);
        check(seven.Known.SequenceEqual(new[]{"old","learned7"})&&initial.Known.SequenceEqual(new[]{"old"}),"Ranger production transaction: replace a spell learned at earlier original event in same advancement; previous state immutable");
        check(Reject(()=>initial.Apply(new[]{"future9"},new[]{"new"},0,1,true,_=>true)),"Ranger production transaction: cannot replace unknown/future spell");
        check(Reject(()=>initial.Apply(Array.Empty<string>(),new[]{"old"},1,1,true,_=>true)),"Ranger production transaction: duplicate known spell rejected");
        check(Reject(()=>initial.Apply(Array.Empty<string>(),new[]{"free"},0,1,true,_=>true)),"Ranger production transaction: replacement budget alone cannot grant a free spell");
        check(Reject(()=>initial.Apply(new[]{"old"},new[]{"replacement"},0,0,true,_=>true)),"Ranger production transaction: no free replacement when native unlearning disabled");
        check(Reject(()=>initial.Apply(new[]{"old"},new[]{"old"},0,1,true,_=>true)),"Ranger production transaction: reselecting same removed spell rejected");
        check(Reject(()=>initial.Apply(Array.Empty<string>(),new[]{"circle3"},1,1,true,s=>s!="circle3")),"Ranger production transaction: earlier original event cannot borrow future higher circle");
        check(Reject(()=>initial.Apply(Array.Empty<string>(),new[]{"foreignList"},1,1,true,s=>s!="foreignList")),"Ranger production transaction: source list restrictions enforced");
        check(Reject(()=>initial.Apply(Array.Empty<string>(),Array.Empty<string>(),1,1,true,_=>true)),"Ranger production transaction: mandatory learning event cannot disappear");
        check(initial.Apply(Array.Empty<string>(),Array.Empty<string>(),0,1,true,_=>true).Known.SequenceEqual(initial.Known),"Ranger production transaction: optional replacement may be skipped");
        var at18=initial.Apply(new[]{"old"},new[]{"a"},0,1,true,_=>true);var at19=at18.Apply(new[]{"a"},new[]{"b","learn19"},1,1,true,_=>true);var at20=at19.Apply(new[]{"b"},new[]{"old"},0,1,true,_=>true);
        check(at20.Known.SequenceEqual(new[]{"learn19","old"}),"Ranger production transaction: all three original18/19/20 replacements with original19 learning, ordered at actual12");
        var back=initial.Apply(Array.Empty<string>(),new[]{"different6"},1,1,true,_=>true);check(Reject(()=>back.Apply(new[]{"learned6"},new[]{"new7"},0,1,true,_=>true)),"Ranger production transaction: Back/reset invalidates stale later replacement");
        var steps=KnownSpellsProgressionPlan.OrderSteps(new[]{("unlearn9",9,0),("unlearn8",8,0),("learn8",8,1),("learn9",9,1)});
        check(steps.SequenceEqual(new[]{"unlearn8","learn8","unlearn9","learn9"}),"Ranger production UI planner: chronological original events, not all removals before learning");
        var ledger=new RangerSpellGrantLedger();check(ledger.Grant("Ranger12","18")&&!ledger.Grant("Ranger12","18")&&ledger.Grant("Ranger12","19")&&ledger.Grant("Ranger12","20")&&ledger.HasStarted("Ranger12"),"Ranger production grant ledger: repeated callbacks once and three independent events preserved");
        check(ledger.Grant("Next","18")&&!ledger.HasStarted("Ranger12"),"Ranger production grant ledger: reused data starts a fresh advancement");
    }
    private static void Invocations(Action<bool,string> check)
    {
        var events=new (string Type,int Original)[]{("terrain",1),("enemy",1),("terrain",6),("enemy",6),("terrain",10),("enemy",14)};
        var origins=events.Select((x,i)=>RangerInvocationPlan.Origin("Ranger",x.Original,i)).ToArray();
        check(origins.Distinct().Count()==6&&events.Where(x=>x.Type=="terrain").Select(x=>FighterProgressionPlan<int>.CompressedLevel(x.Original)).SequenceEqual(new[]{1,6,8})&&events.Where(x=>x.Type=="enemy").Select(x=>FighterProgressionPlan<int>.CompressedLevel(x.Original)).SequenceEqual(new[]{1,6,10}),"Ranger production invocation origins: three independent terrains and three enemies at original thresholds");
        var choices=events.Select((x,i)=>(Origin:origins[i],Type:x.Type,Awards:1,Selected:(IEnumerable<string>)new[]{x.Type+i})).ToArray();
        RangerInvocationPlan.Validate(Array.Empty<string>(),choices,item=>item.StartsWith("terrain")?"terrain":"enemy");check(true,"Ranger production invocation validation: all six legal independent selections accepted");
        check(Reject(()=>RangerInvocationPlan.Validate(new[]{"terrain0"},choices,item=>item.StartsWith("terrain")?"terrain":"enemy")),"Ranger production invocation validation: already owned selection rejected");
        check(Reject(()=>RangerInvocationPlan.Validate(Array.Empty<string>(),choices.Select((x,i)=>i==5?(x.Origin,x.Type,x.Awards,(IEnumerable<string>)Array.Empty<string>()):x),item=>item.StartsWith("terrain")?"terrain":"enemy")),"Ranger production invocation validation: missing original14 reward rejected");
        check(Reject(()=>RangerInvocationPlan.Validate(Array.Empty<string>(),choices,_=>"wrong")),"Ranger production invocation validation: cross-pool choice rejected");
        check(Reject(()=>RangerInvocationPlan.Validate(Array.Empty<string>(),choices.Concat(choices.Take(1)),item=>item.StartsWith("terrain")?"terrain":"enemy")),"Ranger production invocation validation: duplicate origin cannot double a reward");
        foreach(var revised in new[]{false,true})foreach(var legacy in new[]{false,true})foreach(var equivalent in new[]{false,true})check(RangerProgressionRules.KeepLegacySelection(revised,legacy,equivalent)==(!legacy||!revised&&!equivalent),$"Ranger production legacy normalization: revised={revised},oldChoice={legacy},customEquivalent={equivalent}");
    }

    private static void Variants(Action<bool,string> check)
    {
        // Source-switch model followed by real production transformation; this does not execute native switches.
        for(var flags=0;flags<256;flags++)
        {
            bool On(int bit)=>(flags&(1<<bit))!=0;
            var source=Fixture("Ranger");
            if(On(0)){source.RemoveAll(x=>x.FeatureDefinition.Name.StartsWith("InvocationPoolRanger"));source.Add(new(new FeatureDefinition{Name="FeatureSetRangerFavoredEnemy"},1));}
            if(On(1))source.Add(new(new FeatureDefinition{Name="FeatureSetRangerDeftExplorer"},2));
            if(On(2)){source.Add(new(new FeatureDefinition{Name="MovementAffinityRangerRoving"},6));source.Add(new(new FeatureDefinitionPointPool{Name="PointPoolRangerExpertise",PoolType="Expertise",PoolAmount=2},9));}
            if(On(3)){source.RemoveAll(x=>x.FeatureDefinition.Name=="PowerRangerHideInPlainSight");source.Add(new(new FeatureDefinitionPower{Name="PowerRangerTireless"},10));}
            if(On(4))source.Add(new(new FeatureDefinition{Name="FeatureRangerRelentlessHunter"},13));
            if(On(5)){source.RemoveAll(x=>x.FeatureDefinition.Name=="ActionAffinityRangerVanish");source.Add(new(new FeatureDefinitionPower{Name="FeatureDefinitionPowerNatureShroud"},14));}
            if(On(6))source.Add(new(new FeatureDefinition{Name="CombatAffinityRangerPreciseHunter"},17));
            if(On(7)){source.RemoveAll(x=>x.FeatureDefinition.Name=="SenseRangerFeralSenses"||x.FeatureDefinition.Name=="FeatureRangerFoeSlayer");source.Add(new(new FeatureDefinition{Name="SenseRangerFeralSenses2024"},18));source.Add(new(new FeatureDefinition{Name="FeatureRangerFoeSlayer2024"},20));}
            source=source.OrderBy(x=>x.Level).ToList();var result=new CompressedProgressionDefinitions("RangerVariant"+flags).Prepare(source,"Ranger");
            check(Leaves(result).Select(Signature).SequenceEqual(Leaves(source).Select(Signature)),"Ranger production transformer on modeled variants: every original leaf/reward/order:"+flags);
            check(!On(0)||!Leaves(result).Any(x=>x.Name.StartsWith("InvocationPoolRanger")),"Ranger modeled variant: Favored Enemy2024 never also grants legacy pools:"+flags);
            check(!On(4)||Leaves(result.Where(x=>x.Level<=9)).Any(x=>x.Name=="FeatureRangerRelentlessHunter"),"Ranger modeled variant: Relentless13 maps to9:"+flags);
            check(!On(6)||Leaves(result.Where(x=>x.Level<=11)).Any(x=>x.Name=="CombatAffinityRangerPreciseHunter"),"Ranger modeled variant: Precise17 maps to11:"+flags);
        }
        foreach(var senses2024 in new[]{false,true})foreach(var slayer2024 in new[]{false,true})
        {
            var pair=new List<FeatureUnlockByLevel>{new(new FeatureDefinition{Name=senses2024?"SenseRangerFeralSenses2024":"SenseRangerFeralSenses"},18),new(new FeatureDefinition{Name=slayer2024?"FeatureRangerFoeSlayer2024":"FeatureRangerFoeSlayer"},20)};
            var result=new CompressedProgressionDefinitions("RangerIndependent"+senses2024+slayer2024).Prepare(pair,"Ranger");
            check(result.All(x=>x.Level==12)&&Leaves(result).Select(x=>x.Name).SequenceEqual(pair.Select(x=>x.FeatureDefinition.Name)),$"Ranger production transformer: Feral/Foe rules independently selected: senses2024={senses2024},slayer2024={slayer2024}");
        }
        var target=Fixture("Ranger");foreach(var set in target.Select(x=>x.FeatureDefinition).OfType<FeatureDefinitionFeatureSet>().Where(x=>x.Name=="FeatureSetAbilityScoreChoice"))set.Mode=FeatureDefinitionFeatureSet.FeatureSetMode.Union;
        var feat=new FeatureDefinitionPointPool{Name="BonusFeat",PoolType="Feat",PoolAmount=1};target.AddRange(new[]{2,6,10,14,18}.Select(n=>new FeatureUnlockByLevel(feat,n)));target=target.OrderBy(x=>x.Level).ToList();var transformer=new CompressedProgressionDefinitions("RangerTargetFeats");var output=transformer.Prepare(target,"Ranger");
        check(Leaves(output).OfType<FeatureDefinitionPointPool>().Count(x=>x.PoolType=="Feat")==10&&Leaves(output).OfType<FeatureDefinitionPointPool>().Count(x=>x.PoolType=="AbilityScore")==5,"Ranger production transformer on target settings: five ASI, ten class feats; first racial feat unaffected");
        check(Leaves(output.Where(x=>x.Level==12)).OfType<FeatureDefinitionPointPool>().Count(x=>x.PoolType=="Feat")==2&&transformer.PoolOrigins.Values.Distinct().Count()==transformer.PoolOrigins.Count,"Ranger production transformer: original18 extra feat and19 ASI feat remain independent at12");
    }
    private static void Mechanics(Action<bool,string> check)
    {
        check(Enumerable.Range(1,12).Select(n=>RangerProgressionRules.MarkUses(RangerProgressionRules.EffectiveLevel(true,n))).SequenceEqual(new[]{2,2,2,2,3,3,4,4,5,5,6,6}),"Ranger production mark budget: original thresholds5/9/13/17 map to5/7/9/11, six at12");
        foreach(var actual in Enumerable.Range(1,12))foreach(var relentless in new[]{false,true})foreach(var noConcentration in new[]{false,true})foreach(var mark in new[]{false,true})
        {
            var effective=RangerProgressionRules.EffectiveLevel(true,actual);var free=mark&&effective>=13&&relentless&&noConcentration;
            check(RangerProgressionRules.RequiresConcentration(true,mark,effective,relentless,noConcentration)==!free&&RangerProgressionRules.ProtectConcentration(effective,relentless,noConcentration)==(effective>=13&&relentless&&!noConcentration),$"Ranger production concentration variants are exclusive: actual={actual},relentless={relentless},noConcentration={noConcentration},mark={mark}");
        }
        check(!RangerProgressionRules.RequiresConcentration(false,false,20,true,false),"Ranger production concentration: originally non-concentration spells remain so");
        check(RangerProgressionRules.TirelessBonus(20)==10&&RangerProgressionRules.TirelessBonus(11)==6,"Ranger production Tireless: source half-round-up class bonus, real WIS uses remain native");
        foreach(var actual in Enumerable.Range(1,12))foreach(var marked in new[]{false,true})check(RangerProgressionRules.PreciseAdvantage(RangerProgressionRules.EffectiveLevel(true,actual),marked)==(actual>=11&&marked),"Ranger production Precise: own mark required, no universal advantage:"+actual+marked);
        foreach(var actual in Enumerable.Range(1,12))check(RangerProgressionRules.UpgradedCompanion(RangerProgressionRules.EffectiveLevel(true,actual))==(actual>=8),"Ranger production companion choice: original11 forms at actual8:"+actual);
        check(!RangerProgressionRules.IsFreeMarkUse(false,true)&&RangerProgressionRules.IsFreeMarkUse(true,true)&&!RangerProgressionRules.IsFreeMarkUse(true,false),"Ranger production free-mark routing: inherited parent UsablePower does not turn child CastNoCost into a second power use, including last charge");
        var markLedger=new RangerMarkActionLedger();check(!markLedger.TryBegin(false)&&markLedger.TryBegin(true)&&!markLedger.TryBegin(true)&&!markLedger.TryComplete(false)&&markLedger.TryComplete(true)&&!markLedger.TryComplete(true),"Ranger production free-mark journal: pre-charge validation, one action start and one child cast, cancellation gives no grant");
        check(!new RangerMarkActionLedger().TryComplete(true),"Ranger production free-mark journal: unvalidated callback cannot grant free spell");
        foreach(var active in new[]{false,true})foreach(var level in new[]{0,12})foreach(var own in new[]{false,true})foreach(var klass in new[]{false,true})check(RangerProgressionRules.ShouldTransferCondition(active,level,own,klass)==(!active||level==0||!own||!klass),$"Ranger production respec condition rule: mode={active},level={level},own={own},class={klass}");
    }

    private static void Metadata(Action<bool,string> check,TypeDefinition[] types,AssemblyDefinition game)
    {
        var context=types.Single(x=>x.Name=="RangerProgressionContext");var casting=types.Single(x=>x.Name=="RangerSpellcastingContext");var mark=types.Single(x=>x.Name=="RangerMarkContext");var inv=types.Single(x=>x.Name=="RangerInvocationContext");var companion=types.Single(x=>x.Name=="RangerCompanionContext");
        MethodDefinition Method(string owner,string name)=>types.Single(x=>x.Name==owner).Methods.Single(x=>x.Name==name);
        bool Anywhere(TypeDefinition owner,string method,string target=null)=>Types(owner).SelectMany(x=>x.Methods).Any(x=>Calls(x,method,target));
        var startup=types.SelectMany(x=>x.Methods).Single(x=>Calls(x,"LateLoad",context.Name));
        int Position(string owner)=>startup.Body.Instructions.Select((x,i)=>(x,i)).Single(x=>x.x.Operand is MethodReference m&&m.Name=="LateLoad"&&m.DeclaringType.Name==owner).i;
        check(new[]{"FeatsContext","Tabletop2024Context","SpellPointsContext","FighterProgressionContext","BarbarianProgressionContext","MonkProgressionContext","RogueProgressionContext","WizardProgressionContext","SorcererProgressionContext","WarlockProgressionContext","BardProgressionContext","ClericProgressionContext","DruidProgressionContext","PaladinProgressionContext"}.All(x=>Position(context.Name)>Position(x)),"Ranger metadata: final transformation follows every previous class and source2024/points/feat rules");
        var settings=types.Single(x=>x.FullName=="SolastaUnfinishedBusiness.Settings");check(settings.Methods.Where(x=>x.IsConstructor&&!x.IsStatic).All(x=>!x.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name.Contains("EnableRangerCompressedProgressionPrototype"))),"Ranger metadata: CLR false opt-in default");
        check(Anywhere(types.Single(x=>x.Name=="ClassesDisplay"),"set_EnableRangerCompressedProgressionPrototype"),"Ranger metadata: existing Classes UI setting");
        check(Anywhere(context,"get_KlassListSubclassEnabled")&&Anywhere(context,"get_Subclasses")&&Anywhere(context,"get_Hidden"),"Ranger metadata: actual choices plus active UB registrations, hidden excluded");
        check(Read("SubclassChoiceRangerArchetypes").GetProperty("subclasses").EnumerateArray().Select(x=>x.GetString()).OrderBy(x=>x).SequenceEqual(Official.OrderBy(x=>x)),"Ranger blueprints: all four actual official archetypes");
        var builders=types.Where(x=>x.BaseType?.Name=="AbstractSubclass"&&x.Methods.Any(m=>m.Name=="get_Klass"&&Calls(m,"get_Ranger"))).ToArray();
        check(builders.Select(x=>x.Name).OrderBy(x=>x).SequenceEqual(Ub.OrderBy(x=>x)),"Ranger metadata: all eight actual UB subclass builders discovered");
        foreach(var builder in builders)check(Calls(builder.Methods.Single(x=>x.Name=="get_SubclassChoice"),"get_SubclassChoiceRangerArchetypes"),"Ranger metadata: source builder registered to Ranger:"+builder.Name);
        var rules=types.Single(x=>x.Name=="Tabletop2024Context");
        foreach(var (method,flag) in new[]{("SpellCastingAtOne","EnableRangerSpellCastingAtLevel1"),("DeftExplorer","EnableRangerDeftExplorer2024"),("Expertise","EnableRangerExpertise2024"),("FavoredEnemy","EnableRangerFavoredEnemy2024"),("FeralSenses","EnableRangerFeralSenses2024"),("FoeSlayers","EnableRangerFoeSlayers2024"),("NatureShroud","EnableRangerNatureShroud2024"),("PreciseHunter","EnableRangerPreciseHunter2024"),("RelentlessHunter","EnableRangerRelentlessHunter2024"),("Roving","EnableRangerRoving2024"),("Tireless","EnableRangerTireless2024"),("PrimevalAwareness","RemoveRangerPrimevalAwareness2024"),("AnyFightingStyle","EnableRangerAnyFightingStyle2024")})
            check(Calls(Method(rules.Name,"SwitchRanger"+method),"get_"+flag)&&context.NestedTypes.Any(x=>x.Name=="SwitchRanger"+method+"RestartPatch"),"Ranger metadata: authoritative source flag and guarded switch:"+flag);
        check(context.NestedTypes.Count(x=>x.Name.EndsWith("RestartPatch"))==20,"Ranger metadata: all13 class switches plus7 source/global definition switches require restart");
        check(Calls(Method(context.Name,"LateLoad"),"IsReady",mark.Name)&&Calls(Method(mark.Name,"IsReady"),"GetPatchInfo"),"Ranger metadata: critical action/custom-panel Harmony bindings required before activating");
        check(Calls(Method(context.Name,"CanContinue"),"EnumerateHeroAllowedClassDefinitions")&&Calls(Method(context.Name,"CanContinue"),"CanAdvance",companion.Name),"Ranger metadata: cap deadlock and active companion safety have explicit diagnostics");
        foreach(var owner in new[]{"MulticlassInOutRulesHelper","AddClassLevel_Patch","AssignClassLevel_Patch"})check(Anywhere(types.Single(x=>x.Name==owner),"CanSelectClass",casting.Name)&&Anywhere(types.Single(x=>x.Name==owner),"IsCapped",context.Name),"Ranger metadata: class selection and cap boundary:"+owner);
        var multiclass=types.Single(x=>x.Name=="MulticlassInOutRulesHelper");check(new[]{"Wizard","Sorcerer","Warlock","Bard","Cleric","Druid","Paladin"}.All(k=>Anywhere(multiclass,"CanSelectClass",k+"SpellcastingContext")),"Ranger metadata: every prior mixed-caster guard remains");
        check(Calls(Method(casting.Name,"IsSupportedHero"),"IsMulticaster")&&Anywhere(casting,"get_SpellCastingOrigin")&&Anywhere(casting,"IsCastingSubclass"),"Ranger metadata: additional class/subclass/feat casting boundary retained; race origin stays separate");
        check(Calls(Method(casting.Name,"SingleCasterLevel"),"CasterContribution","RangerProgressionRules")&&!Calls(Method(casting.Name,"SingleCasterLevel"),"TryGetAttributeValue"),"Ranger metadata: own class contribution, no character or martial level input");
        check(Calls(Method("SharedSpellsContext","GetSharedCasterLevel"),"SingleCasterLevel",casting.Name)&&Calls(Method("ModifyPowerPoolAmountPowerSpellPoints","GetCasterLevel"),"SingleCasterLevel",casting.Name),"Ranger metadata: correct half-caster helpers feed reaction circle and Spell Points");
        check(Calls(Method(casting.Name,"Initialize"),"get_KnownSpells")&&Calls(Method(casting.Name,"Initialize"),"get_ReplacedSpells")&&Anywhere(casting,"FindLastIndex"),"Ranger metadata: final installed known/replacement budgets and original slot circles captured");
        check(Anywhere(casting,"OriginalUnlockLevel",context.Name)&&Calls(Method(casting.Name,"Allowed"),"MaximumSpellLevel"),"Ranger metadata: selection uses source circle/list and affinity acquisition threshold");
        check(Calls(Method(casting.Name,"Apply"),"Apply","RangerSpellLearningState`1")&&Calls(Method(casting.Name,"ValidateAll"),"Apply")&&Calls(Method(casting.Name,"BeginFinalize"),"ValidateAll"),"Ranger metadata: actual runtime selection/finalization uses tested production transaction state");
        check(Calls(casting.NestedTypes.Single(x=>x.Name=="GrantLearningPatch").Methods.Single(x=>x.Name=="Prefix"),"Grant","RangerSpellGrantLedger")&&Calls(casting.NestedTypes.Single(x=>x.Name=="ClearLearningPatch").Methods.Single(x=>x.Name=="Prefix"),"Remove"),"Ranger metadata: grant once per level/origin, reset on Back/Cancel");
        check(Anywhere(casting,"OrderSteps","KnownSpellsProgressionPlan")&&Calls(Method(casting.Name,"BeginFinalize"),"Validate",inv.Name),"Ranger metadata: chronological learning UI and invocation completeness checked before hero mutation");
        check(inv.Methods.Where(x=>x.Name=="Adapt").Any(x=>Calls(x,"Origin","RangerInvocationPlan")),"Ranger metadata: invocation origin constructed by tested production plan");
        check(Calls(Method(inv.Name,"Validate"),"Validate","RangerInvocationPlan")&&Anywhere(inv,"Setup","CustomInvocationPoolDefinitionBuilder")&&Anywhere(inv,"CopyCustomSubFeatures"),"Ranger metadata: clone pool type/points/behavior and validate original choices via production plan");
        var poolField=types.Single(x=>x.Name=="CustomInvocationSelectionPanel").Fields.Single(x=>x.Name=="_gainedCustomFeatures");check(poolField.FieldType.FullName.Contains("FeatureDefinitionCustomInvocationPool")&&inv.NestedTypes.Single(x=>x.Name=="OriginPatch").Methods.Single(x=>x.Name=="Postfix").Parameters.Single().Name=="____gainedCustomFeatures","Ranger metadata: private custom-panel field injection exact name/type");
        check(Anywhere(rules.NestedTypes.Single(x=>x.Name=="CustomBehaviorFavoredEnemyHuntersMark"),"MarkUses","RangerProgressionRules")&&Anywhere(rules.NestedTypes.Single(x=>x.Name=="CustomBehaviorTireless"),"TirelessBonus","RangerProgressionRules"),"Ranger metadata: direct Mark uses and Tireless class scaling use production rules");
        check(Anywhere(rules.NestedTypes.Single(x=>x.Name=="CustomBehaviorRelentlessHunter"),"RequiresConcentration",mark.Name)&&Anywhere(rules.NestedTypes.Single(x=>x.Name=="CustomBehaviorRelentlessHunter"),"ProtectConcentration",mark.Name),"Ranger metadata: no-concentration and protected checks use exclusive production variants");
        var precise=mark.NestedTypes.Single(x=>x.Name=="PreciseHunter");check(Anywhere(precise,"get_SourceGuid")&&Anywhere(precise,"PreciseAdvantage","RangerProgressionRules"),"Ranger metadata: Precise requires the attacker's own mark GUID");
        check(Anywhere(rules.NestedTypes.Single(x=>x.Name=="CustomBehaviorFavoredEnemyHuntersMark"),"CompleteFreeMark",mark.Name)&&Anywhere(rules.NestedTypes.Single(x=>x.Name=="CustomBehaviorFavoredEnemyHuntersMark"),"MyExecuteActionCastNoCost"),"Ranger metadata: free mark guarded exactly once then original CastNoCost child");
        check(!Types(mark).SelectMany(x=>x.Methods).Any(x=>Calls(x,"SpendSpellSlot")||Calls(x,"UpdateUsageForPowerPool")),"Ranger metadata: Mark adapter adds no independent points/slot charge or refund");
        var action=mark.NestedTypes.Single(x=>x.Name=="ActionSafetyPatch").Methods.Single(x=>x.Name=="Prefix");check(action.CustomAttributes.Any(x=>x.AttributeType.Name=="HarmonyPriority")&&Calls(action,"TryBegin","RangerMarkActionLedger"),"Ranger metadata: pre-charge action journal before UB replacement ExecuteImpl");
        var nativeGrant=game.MainModule.Types.Single(x=>x.Name=="CharacterBuildingManager").Methods.Single(x=>x.Name=="GrantCantripsAndSpellsByTag");
        var nativeCalls=nativeGrant.Body.Instructions.Where(x=>x.Operand is MethodReference).Select(x=>(MethodReference)x.Operand).Select(x=>x.Name).ToList();
        check(nativeCalls.IndexOf("LoseSpell")>=0&&nativeCalls.IndexOf("LoseSpell")<nativeCalls.IndexOf("GrantSpell"),"Ranger native metadata: each original tag loses its old spell before granting new spell");
        var rangerSource=File.ReadAllText("SolastaUnfinishedBusiness/Models/2024RangerContext.cs");
        check(rangerSource.Contains("SetUsesFixed(ActivationTime.BonusAction, RechargeRate.LongRest, 1, 0)")&&rangerSource.Contains("MyExecuteActionCastNoCost(HuntersMark, 0, action.ActionParams)"),"Ranger source definition inspection: independent long-rest Mark pool costs one own use; child spell slot0 adds no Spell Points charge");
        var late=types.Single(x=>x.Name=="Level20Context");check(Anywhere(late.NestedTypes.Single(x=>x.Name=="ModifyAdditionalDamageRangerFoeSlayer2024"),"EffectiveLevel",context.Name),"Ranger metadata: original20 Hunter's Mark D10 guard compressed to actual12");
        check(Calls(Method("Level20Context","RangerLoad"),"EnumerateKnownSpells")&&Calls(Method("Level20Context","RangerLoad"),"EnumerateReplacedSpells"),"Ranger metadata: source Level20 extends both known and replacement tables");
        check(action.Body.Instructions.Any(x=>x.OpCode.Code==Code.Isinst&&x.Operand is TypeReference t&&t.Name=="CharacterActionUsePower")&&Calls(action,"IsFreeMarkUse","RangerProgressionRules"),"Ranger metadata: actual power action type checked before capturing free-mark activation");
        var castingJson=Read("CastSpellRanger");check(castingJson.GetProperty("spellKnowledge").GetString()=="Selection"&&castingJson.GetProperty("spellReadyness").GetString()=="AllKnown", "Ranger installed blueprint:2024 prepared-named option updates known budget, does not create WholeList daily preparation");
        var source=File.ReadAllText("SolastaUnfinishedBusiness/Models/Level20Context.cs");check(source.Contains("SetSense(SenseMode.Type.DetectInvisibility, 6)")&&source.Contains("SetSense(SenseMode.Type.Blindsight, 6)"),"Ranger source definition inspection: legacy Feral DetectInvisibility6 versus2024 Blindsight6");
        foreach(var name in new[]{"RangerFeyWanderer","RangerLightBearer","RangerWildMaster"})check(Anywhere(types.Single(x=>x.Name==name),"EffectiveLevel",context.Name),"Ranger metadata: direct subclass class-rank calculation adapted:"+name);
        check(Anywhere(types.Single(x=>x.Name=="Executioner"),"EffectiveSubclassLevel",context.Name)&&Anywhere(types.Single(x=>x.Name=="RangerFeyWanderer"),"EffectiveSubclassLevel",context.Name),"Ranger metadata: Survivalist11 and Fey15 subclass predicates adapted");
        var wild=types.Single(x=>x.Name=="RangerWildMaster");check(Anywhere(wild,"UpgradedCompanion","RangerProgressionRules")&&Anywhere(wild,"SetSharedPool"),"Ranger metadata: native Wild Master forms and shared summon resource preserved");
        var wildSource=File.ReadAllText("SolastaUnfinishedBusiness/Subclasses/RangerWildMaster.cs");check(wildSource.Contains("SetAmountOrigin(ExtraOriginOfAmount.SourceClassLevel, RangerClass)")&&wildSource.Contains("SourceProficiencyAndAbilityBonus, AttributeDefinitions.Wisdom")&&wildSource.Contains("SetFeatures(HpBonus, HpBonus, HpBonus, HpBonus, HpBonus)"),"Ranger source definition inspection: companion HP from class5x, accuracy/damage from actual PB+WIS");
        check(Anywhere(types.Single(x=>x.Name=="RulesetActorPatcher"),"EffectiveLevel",context.Name)&&Anywhere(types.Single(x=>x.Name=="RulesetActorPatcher"),"EffectiveSubclassLevel",context.Name),"Ranger metadata: condition source class amount and Light Bearer healing use scoped effective Ranger");
        check(Calls(Method(companion.Name,"CanSummon"),"Any")&&Calls(Method(companion.Name,"CanRespec"),"GetAllConditionsBySourceGuid"),"Ranger metadata: existing companion and active mark/summon respec guards");
        check(Types(types.Single(x=>x.Name=="FunctorRespec")).SelectMany(x=>x.Methods).Any(x=>Calls(x,"CanRespec",companion.Name))&&Types(types.Single(x=>x.Name=="FunctorRespec")).SelectMany(x=>x.Methods).Any(x=>Calls(x,"ShouldTransferCondition",companion.Name)),"Ranger metadata: respec guard before replacement, stale own conditions not transferred");
        foreach(var owner in new[]{context,casting,mark,inv})foreach(var patch in owner.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(x=>x.AttributeType.Name=="HarmonyPatch");if(attr==null)continue;
            var targetType=(TypeReference)attr.ConstructorArguments[0].Value;var name=(string)attr.ConstructorArguments[1].Value;
            var target=game.MainModule.Types.FirstOrDefault(x=>x.FullName==targetType.FullName)??types.Single(x=>x.FullName==targetType.FullName);
            var methods=target.Methods.Where(x=>x.Name==name||x.Name=="get_"+name).ToArray();check(methods.Length>0,"Ranger metadata: Harmony target:"+patch.Name);
            foreach(var p in patch.Methods.Where(x=>x.Name is "Prefix" or "Postfix").SelectMany(x=>x.Parameters).Where(x=>!x.Name.StartsWith("__")))check(methods.Any(x=>x.Parameters.Any(q=>q.Name==p.Name)),"Ranger metadata: Harmony named argument:"+patch.Name+"."+p.Name);
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
