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
internal static class PaladinTests
{
    private static readonly string[] Official = { "OathOfDevotion", "OathOfJugement", "OathOfTheMotherland", "OathOfTirmar" };
    private static readonly string[] Ub = { "OathOfAltruism", "OathOfAncients", "OathOfDemonHunterOld", "OathOfDemonHunter", "OathOfDread", "OathOfHatred", "OathOfThunder" };
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

    private static List<FeatureUnlockByLevel> Fixture(string name)
    {
        var folder = name.StartsWith("Oath") && !Official.Contains(name) ? "UnfinishedBusinessBlueprints" : "OfficialBlueprints";
        var cache = new Dictionary<string, FeatureDefinition>();
        var source = Read(name, folder).GetProperty("featureUnlocks").EnumerateArray().Select(x =>
            new FeatureUnlockByLevel(Feature(Name(x.GetProperty("featureDefinition").GetString()), folder, cache), x.GetProperty("level").GetInt32())).ToList();
        if (name == "Paladin")
        {
            source.Add(new(Feature("PowerPaladinAuraOfCourage18", folder, cache), 18));
            source.Add(new(Feature("PowerPaladinAuraOfProtection18", folder, cache), 18));
            source.Add(new(Feature("FeatureSetAbilityScoreChoice", folder, cache), 19));
        }
        var index = Array.IndexOf(Official, name);
        if (index >= 0)
        {
            var aura = new[] { "PowerOathOfDevotionAuraDevotion18", "PowerOathOfJugementAuraRightenousness18", "PowerOathOfMotherlandVolcanicAura18", "PowerOathOfTirmarAuraTruth18" }[index];
            var capstone = new[] { "PowerOathOfDevotionHolyNimbus", "PowerOathOfJugementFinalJudgement", "PowerOathOfMotherlandFlamesOfMotherland", "PowerOathOfTirmarInquisitorZeal" }[index];
            source.Add(new(Feature(aura, folder, cache), 18)); source.Add(new(Feature(capstone, folder, cache), 20));
        }
        return source.OrderBy(x => x.Level).ToList();
    }
    private static IEnumerable<FeatureDefinition> Leaves(IEnumerable<FeatureUnlockByLevel> source) =>
        source.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x => x is not FeatureDefinitionFeatureSet);
    private static string Signature(FeatureDefinition f) => f is FeatureDefinitionPointPool p ? $"{p.PoolType}:{p.PoolAmount}" : f.Name;
    private static void Inventory(Action<bool, string> check, string name, List<FeatureUnlockByLevel> source)
    {
        var before = source.Select(x => (x.FeatureDefinition, x.Level)).ToArray();
        var transformer = new CompressedProgressionDefinitions("PaladinFixture" + name);
        var output = transformer.Prepare(source, name);
        check(Leaves(output).Select(Signature).SequenceEqual(Leaves(source).Select(Signature)), "Paladin production transformer: every ordered leaf and reward retained: " + name);
        check(source.Select(x => (x.FeatureDefinition, x.Level)).SequenceEqual(before), "Paladin production transformer: source definitions unchanged: " + name);
        check(output.All(x => x.Level <= 12), "Paladin production transformer: no feature beyond12: " + name);
        check(transformer.PoolOrigins.Count == transformer.PoolOrigins.Values.Distinct().Count(), "Paladin production transformer: repeated choices have unique origins: " + name);
        foreach (var actual in Enumerable.Range(1, 12))
            check(Leaves(output.Where(x => x.Level <= actual)).Select(Signature).SequenceEqual(Leaves(source.Where(x => x.Level <= PaladinProgressionRules.EffectiveLevel(true, actual))).Select(Signature)),
                $"Paladin production transformer: complete inventory through actual{actual}: {name}");
        foreach (var power in Leaves(source).OfType<FeatureDefinitionPower>())
            check(Leaves(output).Contains(power), "Paladin production transformer: preserve identity and overridden-power chain: " + power.Name);
    }
    internal static void Run(Action<bool, string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        check(Enumerable.Range(1,12).Select(n => PaladinProgressionRules.EffectiveLevel(true,n)).SequenceEqual(new[] {1,2,3,4,5,7,9,11,13,15,17,20}), "Paladin production rules: exact20-to12 map");
        check(Enumerable.Range(1,12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).SequenceEqual(Enumerable.Range(1,20)), "Paladin production planner: every original advancement event retained");
        foreach (var actual in Enumerable.Range(0,21))
        {
            check(PaladinProgressionRules.EffectiveLevel(false,actual) == actual, "Paladin production rules: disabled rank unchanged:" + actual);
            check(PaladinProgressionRules.IsCapped(true,actual) == (actual>=12) && !PaladinProgressionRules.IsCapped(false,actual), "Paladin production rules: cap only when active:" + actual);
        }
        check(PaladinProgressionRules.EffectiveLevel(true,13)==13, "Paladin production rules: old13 is unsupported, never migrated to20");
        var on = new FighterProgressionInitialization(); var off = new FighterProgressionInitialization();
        check(on.ShouldInitialize(true) && !on.ShouldInitialize(true) && !off.ShouldInitialize(false) && !off.ShouldInitialize(true), "Paladin production initialization: exactly once, including disabled latch");
        foreach (var name in new[] { "Paladin" }.Concat(Official).Concat(Ub.Where(x => x != "OathOfDemonHunter" && x != "OathOfDemonHunterOld")).Concat(new[] { "OathOfDemonHunter" }))
        {
            Inventory(check,name,Fixture(name));
            var folder = Official.Contains(name) || name == "Paladin" ? "OfficialBlueprints" : "UnfinishedBusinessBlueprints";
            foreach (var auto in Leaves(Fixture(name)).Select(x => Read(x.Name,folder)).Where(x => x.ValueKind != JsonValueKind.Undefined && x.TryGetProperty("autoPreparedSpellsGroups",out _)))
            {
                check(Name(auto.GetProperty("spellcastingClass").GetString())=="Paladin", "Paladin blueprint: oath spell ownership remains Paladin:" + name);
                var groups = auto.GetProperty("autoPreparedSpellsGroups").EnumerateArray().ToArray();
                foreach (var actual in Enumerable.Range(1,12))
                    check(groups.Where(x => FighterProgressionPlan<int>.CompressedLevel(x.GetProperty("classLevel").GetInt32()) <= actual).SelectMany(x => x.GetProperty("spellsList").EnumerateArray().Select(s => s.GetString())).SequenceEqual(
                        groups.Where(x => x.GetProperty("classLevel").GetInt32() <= PaladinProgressionRules.EffectiveLevel(true,actual)).SelectMany(x => x.GetProperty("spellsList").EnumerateArray().Select(s => s.GetString()))), "Paladin planner: exact oath spells, counts and thresholds: " + name + "@" + actual);
            }
        }
        CheckSourceBuilders(check,types);
        CheckCasting(check,types,game);
        CheckVariants(check);
        CheckSafety(check);
        CheckMetadata(check,types,game);
    }
    // Constructor source inventories cover the Remaster absent from cached blueprints too. No Unity builder execution.
    private static void CheckSourceBuilders(Action<bool,string> check, TypeDefinition[] types)
    {
        var builders = types.Where(x => x.BaseType?.Name == "AbstractSubclass" && x.Methods.Any(m => m.Name == "get_Klass" && Calls(m,"get_Paladin"))).ToArray();
        check(builders.Select(x=>x.Name).OrderBy(x=>x).SequenceEqual(Ub.OrderBy(x=>x)), "Paladin metadata: exact seven actual UB builders, including Old and Remaster");
        foreach (var name in Ub)
        {
            var text = File.ReadAllText("SolastaUnfinishedBusiness/Subclasses/" + name + ".cs");
            var block = text.Substring(text.IndexOf("Subclass = CharacterSubclassDefinitionBuilder",StringComparison.Ordinal));
            block = block.Substring(0,block.IndexOf(".AddToDB()",StringComparison.Ordinal));
            var events = Regex.Matches(block,@"\.AddFeaturesAtLevel\((\d+),\s*([^)]*)\)").SelectMany(m =>
                m.Groups[2].Value.Split(',').Select(x => new FeatureUnlockByLevel(new FeatureDefinition { Name = x.Trim() },int.Parse(m.Groups[1].Value)))).ToList();
            check(events.Count>0 && events.Any(x=>x.Level==20), "Paladin source constructor inventory: base-to-capstone events present: " + name);
            Inventory(check,"Source" + name,events);
            check(Calls(builders.Single(x=>x.Name==name).Methods.Single(x=>x.Name=="get_SubclassChoice"),"get_SubclassChoicePaladinSacredOaths"), "Paladin metadata: UB builder registered against actual Paladin choice: " + name);
        }
        check(Read("SubclassChoicePaladinSacredOaths").GetProperty("subclasses").EnumerateArray().Select(x=>x.GetString()).OrderBy(x=>x).SequenceEqual(Official.OrderBy(x=>x)), "Paladin blueprint: all four official oaths discovered, using installed spelling Jugement");
    }
    private static void CheckCasting(Action<bool,string> check, TypeDefinition[] types, AssemblyDefinition game)
    {
        var casting = Read("CastSpellPaladin");
        check(casting.GetProperty("spellKnowledge").GetString()=="WholeList" && casting.GetProperty("spellReadyness").GetString()=="Prepared" && casting.GetProperty("spellPreparationCount").GetString()=="AbilityBonusPlusHalfLevel", "Paladin blueprint: own prepared half-level/CHA caster, not full/known casting");
        var slots = AddedIntegers(types.Single(x=>x.Name=="FeatureDefinitionCastSpellBuilder"),"SlotsByCasterLevel");
        var points = AddedIntegers(types.Single(x=>x.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel");
        var costs = AddedIntegers(types.Single(x=>x.Name=="SpellPointsContext"),"SpellCostByLevel");
        foreach (var roundUp in new[] {false,true})
        {
            var halfRows = Enumerable.Range(1,20).Select(n=>slots.Skip(PaladinProgressionRules.CasterContribution(n,roundUp)*10).Take(10).ToArray()).ToArray();
            var projected = SpellcastingProgressionPlan.ProjectRows(halfRows);
            foreach (var actual in Enumerable.Range(1,12))
            {
                var effective = PaladinProgressionRules.EffectiveLevel(true,actual);
                var contribution = PaladinProgressionRules.CasterContribution(effective,roundUp);
                check(contribution==(effective<=1 && !roundUp ? 0 : (effective+1)/2) && projected[actual-1].SequenceEqual(halfRows[effective-1]), $"Paladin production projection: half contribution/slot row: roundUp={roundUp}@{actual}");
                check(contribution is >=0 and <=10 && projected[actual-1].Skip(5).All(x=>x==0), $"Paladin production projection: no circle6-9/full-caster indices: roundUp={roundUp}@{actual}");
                check(points[contribution]<=64 && (actual!=12 || points[contribution]==64), $"Paladin production projection/compiled table: proper points budget: roundUp={roundUp}@{actual}");
            }
            check(projected[0].Any(x=>x>0)==roundUp && projected[11].Take(5).SequenceEqual(new[] {4,3,3,3,2}), "Paladin production projection: selected spellcasting start1/2 and final4/3/3/3/2 slots, fifth circle: roundUp="+roundUp);
        }
        check(costs.Take(6).SequenceEqual(new[] {0,2,3,5,6,7}), "Paladin compiled table: native slot1..5 Smite/Spell Points costs2/3/5/6/7");
        check(PaladinProgressionRules.CasterContribution(-10,true)==0 && PaladinProgressionRules.CasterContribution(99,true)==10, "Paladin production rules: contribution indices bounded0..10");
        var prep = AddedIntegers(types.Single(x=>x.Name=="ComputeMaxPreparedSpells_Patch"),"PaladinOneDndPreparedSpellsTable");
        check(SpellcastingProgressionPlan.ProjectCumulative(prep).Take(12).SequenceEqual(new[] {2,3,4,5,6,7,9,10,11,12,14,15}), "Paladin compiled2024 prepared table: exact projection,15 at12 without ordinary CHA addition");
        check(PaladinProgressionRules.LegacyPreparedLimit(20,5,0)==15 && PaladinProgressionRules.LegacyPreparedLimit(7,4,2)==9 && PaladinProgressionRules.LegacyPreparedLimit(1,-5,0)==1, "Paladin production preparation: native floor half-class + CHA + original affinities/minimum1");
        var native = game.MainModule.Types.Single(x=>x.Name=="RulesetCharacterHero").Methods.Single(x=>x.Name=="ComputeMaxPreparedSpells");
        check(native.Body.Instructions.Any(x=>x.OpCode.Code==Code.Div && Integer(x.Previous)==2), "Paladin native metadata: integer floor division verified in installed preparation method");

    }
    private static void CheckVariants(Action<bool,string> check)
    {
        // Model the source switches, then execute the real transformer. This does NOT execute Tabletop2024Context.
        for(var flags=0;flags<64;flags++)
        {
            bool On(int bit)=>(flags&(1<<bit))!=0;
            var source=Fixture("Paladin");
            source=source.Where(x=>!On(1) || x.FeatureDefinition.Name!="AdditionalDamagePaladinDivineSmite").ToList();
            source=source.Select(x=>x.FeatureDefinition.Name=="CastSpellPaladin" ? new FeatureUnlockByLevel(x.FeatureDefinition,On(0)?1:2):x).ToList();
            if(On(1))source.Add(new(new FeatureDefinition {Name="FeatureSetDivineSmite2024"},2));
            if(On(2))source.Add(new(new FeatureDefinitionPower {Name="PowerPaladinAbjureFoes"},9));
            if(On(3))source.Add(new(new FeatureDefinition {Name="AttributeModifierPaladinChannelDivinity11"},11));
            source.RemoveAll(x=>x.FeatureDefinition.Name=="PowerPaladinCleansingTouch");
            source.Add(new(new FeatureDefinitionPower {Name=On(4)?"PowerPaladinRestoringTouch":"PowerPaladinCleansingTouch"},14));
            if(On(5))source.Add(new(new FeatureDefinitionPower {Name="PowerRechargeOwnOathCapstone"},20));
            source=source.OrderBy(x=>x.Level).ToList();
            var result=new CompressedProgressionDefinitions("PaladinVariant"+flags).Prepare(source,"Paladin");
            check(Leaves(result).Select(Signature).SequenceEqual(Leaves(source).Select(Signature)), "Paladin production transformer on source variants: complete ordered inventory, flags="+flags);
            check(Leaves(result).Count(x=>x.Name=="AdditionalDamagePaladinDivineSmite")== (On(1)?0:1) && Leaves(result).Count(x=>x.Name=="FeatureSetDivineSmite2024")== (On(1)?1:0), "Paladin modeled variant: exclusive Divine Smite, flags="+flags);
            check(Leaves(result.Where(x=>x.Level<=7)).Any(x=>x.Name=="PowerPaladinAbjureFoes")==On(2) && Leaves(result.Where(x=>x.Level<=8)).Any(x=>x.Name=="AttributeModifierPaladinChannelDivinity11")==On(3), "Paladin modeled variant: Abjure9-to7 and Channel11-to8, flags="+flags);
            check(Leaves(result.Where(x=>x.Level<=10)).Any(x=>x.Name==(On(4)?"PowerPaladinRestoringTouch":"PowerPaladinCleansingTouch")), "Paladin modeled variant: Touch14-to10, flags="+flags);
        }
        var original=Fixture("Paladin"); var output=new CompressedProgressionDefinitions("PaladinAsi").Prepare(original,"Paladin");
        check(original.Count(x=>x.FeatureDefinition.Name=="FeatureSetAbilityScoreChoice")==5 && output.Where(x=>Leaves(new[]{x}).Any(f=>f is FeatureDefinitionPointPool)).Select(x=>x.Level).Contains(12), "Paladin production transformer: five original ASI events incl19-to12");
        var target=Fixture("Paladin");
        foreach(var set in target.Select(x=>x.FeatureDefinition).OfType<FeatureDefinitionFeatureSet>().Where(x=>x.Name=="FeatureSetAbilityScoreChoice")) set.Mode=FeatureDefinitionFeatureSet.FeatureSetMode.Union;
        var bonus=Feature("PointPool1BonusFeats","UnfinishedBusinessBlueprints",new Dictionary<string,FeatureDefinition>());
        target.AddRange(new[]{2,6,10,14,18}.Select(n=>new FeatureUnlockByLevel(bonus,n)));
        target=target.OrderBy(x=>x.Level).ToList();
        var targetPlan=new CompressedProgressionDefinitions("PaladinTargetFeats");var targetOutput=targetPlan.Prepare(target,"Paladin");
        check(Leaves(targetOutput).OfType<FeatureDefinitionPointPool>().Count(x=>x.PoolType=="Feat")==10 && Leaves(targetOutput).OfType<FeatureDefinitionPointPool>().Count(x=>x.PoolType=="AbilityScore")==5, "Paladin production transformer on target source settings: five ASI and ten class feats; racial first feat stays outside class transformation");
        check(Leaves(targetOutput.Where(x=>x.Level==12)).OfType<FeatureDefinitionPointPool>().Count(x=>x.PoolType=="Feat")==2 && targetPlan.PoolOrigins.Values.Distinct().Count()==targetPlan.PoolOrigins.Count,"Paladin production transformer on target settings: extra18 feat and ASI19 feat remain two independent choices at12");
        var pool=new FeatureDefinitionPointPool {Name="RepeatedFeat",PoolType="Feat",PoolAmount=1};
        var repeat=new List<FeatureUnlockByLevel>{new(pool,18),new(pool,19),new(pool,20)};
        var transformer=new CompressedProgressionDefinitions("PaladinRepeat");var rewards=transformer.Prepare(repeat,"Paladin");
        check(Leaves(rewards).Count()==3 && transformer.PoolOrigins.Count==3 && transformer.PoolOrigins.Values.Distinct().Count()==3, "Paladin production transformer: three same-level repeated feat origins stay independent");
    }
    private static void CheckSafety(Action<bool,string> check)
    {
        foreach(var compressed in new[]{false,true})foreach(var paladin in new[]{false,true})foreach(var fallback in new[]{false,true})
            check(PaladinProgressionRules.Use2024ChannelRecovery(compressed,paladin,fallback)==(compressed?paladin:fallback), $"Paladin production rest rule: scoped CD, compressed={compressed}, own2024={paladin}, fallback={fallback}");
        foreach(var actual in new[]{11,12,13})foreach(var mode in new[]{false,true})foreach(var owns in new[]{false,true})foreach(var remaining in new[]{0,1})foreach(var resource in new[]{false,true})
            check(PaladinProgressionRules.CanRecharge(actual,mode,owns,remaining,resource)==(actual==12 && mode && owns && remaining==0 && resource), $"Paladin production recharge guard: level={actual},mode={mode},owns={owns},remaining={remaining},paid={resource}");
        var recharge=new PaladinRechargeLedger();
        check(!recharge.TryFinish(false) && recharge.TryFinish(true) && !recharge.TryFinish(true), "Paladin production transaction: cancelled/invalid callback has no grant; valid callback executes once");
        var next=new PaladinRechargeLedger();check(next.TryFinish(true), "Paladin production transaction: another paid action allowed, no invented long-rest restriction");
        var grant=new PaladinSpellGrantLedger();check(grant.Grant("Paladin12","A") && !grant.Grant("Paladin12","A") && grant.Grant("Paladin12","B") && grant.Grant("PaladinNext","A"), "Paladin production choice ledger: repeated callback once, independent origin retained, next level fresh");
        check(PaladinProgressionRules.IsOriginTag("ClassPaladin1Paladin12A","ClassPaladin1") && !PaladinProgressionRules.IsOriginTag("ClassPaladin12","ClassPaladin1"), "Paladin production origins: compressed child accepted, stale level prefix rejected");
        foreach(var name in new[]{"ConditionOathOfDevotionHolyNimbus","ConditionPaladinAuraOfProtection","ConditionDivineSmiteSpell","ConditionMarkUsedFreeSmite","ConditionHatredArdentHate"})check(PaladinProgressionRules.IsClassCondition(name),"Paladin production respec guard: source class condition:"+name);
        foreach(var active in new[]{false,true})foreach(var level in new[]{0,12})foreach(var own in new[]{false,true})foreach(var klass in new[]{false,true})
            check(PaladinProgressionRules.ShouldTransferCondition(active,level,own,klass)==(!active || level==0 || !own || !klass),$"Paladin production respec transfer: mode={active},level={level},source={own},classCondition={klass}");
        check(PaladinProgressionRules.IsPersistentActivation("Permanent") && PaladinProgressionRules.IsPersistentActivation("PermanentUnlessIncapacitated") && !PaladinProgressionRules.IsPersistentActivation("Action"), "Paladin production respec: both native permanent aura variants avoid an impossible deactivate-aura requirement");
        check(!PaladinProgressionRules.IsClassCondition("ConditionPoisoned"),"Paladin production respec guard: unrelated condition not classified as class condition");
    }

    private static void CheckMetadata(Action<bool,string> check, TypeDefinition[] types, AssemblyDefinition game)
    {
        var context=types.Single(x=>x.Name=="PaladinProgressionContext"); var casting=types.Single(x=>x.Name=="PaladinSpellcastingContext"); var safety=types.Single(x=>x.Name=="PaladinPowerSafetyContext");
        MethodDefinition Method(string owner,string name)=>types.Single(x=>x.Name==owner).Methods.Single(x=>x.Name==name);
        bool Anywhere(TypeDefinition owner,string method,string target=null)=>Types(owner).SelectMany(x=>x.Methods).Any(x=>Calls(x,method,target));
        var startup=types.SelectMany(x=>x.Methods).Single(x=>Calls(x,"LateLoad",context.Name));
        int Position(string owner)=>startup.Body.Instructions.Select((x,i)=>(x,i)).Single(x=>x.x.Operand is MethodReference m && m.Name=="LateLoad" && m.DeclaringType.Name==owner).i;
        check(new[]{"FeatsContext","Tabletop2024Context","SpellPointsContext","FighterProgressionContext","BarbarianProgressionContext","MonkProgressionContext","RogueProgressionContext","WizardProgressionContext","SorcererProgressionContext","WarlockProgressionContext","BardProgressionContext","ClericProgressionContext","DruidProgressionContext"}.All(x=>Position(context.Name)>Position(x)), "Paladin metadata: late initialization after all source rules, points and previous classes");
        var settings=types.Single(x=>x.FullName=="SolastaUnfinishedBusiness.Settings");
        check(settings.Methods.Where(x=>x.IsConstructor && !x.IsStatic).All(x=>!x.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name.Contains("EnablePaladinCompressedProgressionPrototype"))), "Paladin metadata: opt-in is CLR false by default");
        check(Anywhere(types.Single(x=>x.Name=="ClassesDisplay"),"set_EnablePaladinCompressedProgressionPrototype"), "Paladin metadata: setting exposed in existing Classes UI");
        check(Anywhere(context,"get_KlassListSubclassEnabled") && Anywhere(context,"get_Hidden") && Anywhere(context,"get_Subclasses"), "Paladin metadata: actual choice and enabled registration discovery excludes hidden subclasses");
        var rules=types.Single(x=>x.Name=="Tabletop2024Context");
        foreach(var (method,flag) in new[]{("SwitchPaladinRechargeLv20Power","EnablePaladinRechargeLv20Feature"),("SwitchPaladinDivineSmite","EnablePaladinSmite2024"),("SwitchPaladinSpellCastingAtOne","EnablePaladinSpellCastingAtLevel1"),("SwitchPaladinAbjureFoes","EnablePaladinAbjureFoes2024"),("SwitchPaladinChannelDivinity","EnablePaladinChannelDivinity2024"),("SwitchPaladinLayOnHand","EnablePaladinLayOnHands2024"),("SwitchPaladinRestoringTouch","EnablePaladinRestoringTouch2024"),("SwitchPaladinAnyFightingStyle","EnablePaladinAnyFightingStyle2024")})
            check(Calls(Method(rules.Name,method),"get_"+flag) && context.NestedTypes.Any(x=>x.Name==method+"RestartPatch"), "Paladin metadata: authoritative source switch and restart guard:"+flag);
        check(context.NestedTypes.Count(x=>x.Name.EndsWith("RestartPatch"))==10 && Calls(Method(casting.Name,"BeginFinalize"),"get_SessionConsistent"), "Paladin metadata: eight class switches plus Smite and ritual guarded; finalization rejects changed session");
        check(Calls(Method(casting.Name,"Initialize"),"get_SpellKnowledge") && !Calls(Method(casting.Name,"Initialize"),"get_KnownSpells") && !Calls(Method(casting.Name,"Initialize"),"get_ScribedSpells"), "Paladin metadata: no invented learned/scribed full-caster budgets");
        check(Calls(Method(casting.Name,"SingleCasterLevel"),"CasterContribution","PaladinProgressionRules") && Calls(Method(casting.Name,"SingleCasterLevel"),"EffectiveLevel",context.Name) && !Calls(Method(casting.Name,"SingleCasterLevel"),"TryGetAttributeValue"), "Paladin metadata: resource contribution uses own effective class, never character/martial level");
        check(Calls(Method("SharedSpellsContext","GetSharedCasterLevel"),"SingleCasterLevel",casting.Name) && Calls(Method("ModifyPowerPoolAmountPowerSpellPoints","GetCasterLevel"),"SingleCasterLevel",casting.Name), "Paladin metadata: source class contribution routes both single-caster helpers, no mixed-caster integration");
        check(Calls(Method(casting.Name,"IsSupportedHero"),"IsMulticaster") && Anywhere(casting,"get_SpellCastingOrigin"),
            "Paladin metadata: repertoire ownership and native multicaster boundary retained");
        check(Method(casting.Name,"IsCastingSubclass").Body.Instructions.Any(x=>x.Operand is FieldReference f && f.Name=="SubclassCasterType") && Anywhere(casting,"PendingCaster") && Anywhere(casting,"get_LevelupTrainedFeats"), "Paladin metadata: magical martial subclasses, casting feats/styles and pending final choices guarded");
        foreach(var owner in new[]{"MulticlassInOutRulesHelper","AddClassLevel_Patch","AssignClassLevel_Patch"})
        {
            var type=types.Single(x=>x.Name==owner);
            check(Anywhere(type,"CanSelectClass",casting.Name) && Anywhere(type,"IsCapped",context.Name),"Paladin metadata: selection/cap boundary:"+owner);
        }
        check(Calls(Method(context.Name,"CanContinue"),"EnumerateHeroAllowedClassDefinitions"),"Paladin metadata: cap deadlock explicitly checks native eligible alternatives");
        check(Anywhere(types.Single(x=>x.Name=="MulticlassInOutRulesHelper"),"CanSelectClass","ClericSpellcastingContext") && Anywhere(types.Single(x=>x.Name=="MulticlassInOutRulesHelper"),"CanSelectClass","DruidSpellcastingContext"), "Paladin metadata: previous caster guards retained");
        check(Anywhere(casting,"LegacyPreparedLimit","PaladinProgressionRules") && Calls(Method("ComputeMaxPreparedSpells_Patch","Prefix"),"EffectiveLevel",context.Name),"Paladin metadata: legacy and2024 prep both adapt own level with native CHA/affinity handling");
        check(Calls(Method(casting.Name,"AdaptAutoPrepared"),"CopyCustomSubFeatures") && Anywhere(casting,"CompressedLevel") && Calls(Method(casting.Name,"AdaptAutoPrepared"),"All"), "Paladin metadata: oath auto-spells projected; unchanged low-level Smite marker retains identity");
        check(Calls(casting.NestedTypes.Single(x=>x.Name=="AutoPreparedPatch").Methods.Single(x=>x.Name=="Postfix"),"Distinct"), "Paladin metadata: same executable auto-spell deduplicated without losing feature origins");
        check(Calls(Method(rules.Name,"UpdatePaladinSmite"),"SmiteRule",context.Name), "Paladin metadata: native refresh respects frozen selected Smite variant");
        var combat=types.Single(x=>x.Name=="GLBM");
        check(Anywhere(combat,"SmiteRule",context.Name) && Anywhere(combat,"EffectiveSubclassLevel",context.Name), "Paladin metadata: legacy Smite excludes2024 branch and Thunder20 uses effective subclass level");
        check(Anywhere(rules.NestedTypes.Single(x=>x.Name=="PowerOrSpellFinishedByMeRestoringTouch"),"RestoringTouchRule",context.Name), "Paladin metadata: Restoring Touch callback respects frozen rule and native shared healing cost");
        check(Anywhere(types.Single(x=>x.Name=="OathOfThunder"),"EffectiveSubclassLevel",context.Name) && Calls(Method("OathOfDemonHunter","GetSubclassLevel"),"EffectiveLevel","PaladinProgressionRules") && Anywhere(types.Single(x=>x.Name=="OathOfDemonHunterOld"),"EffectiveSubclassLevel",context.Name), "Paladin metadata: direct Thunder/Old/Remaster subclass scaling adapted");
        check(Anywhere(types.Single(x=>x.Name=="RefreshAttributes_Patch"),"EffectiveLevel",context.Name), "Paladin metadata: native HealingPool multiplier uses own effective rank");
        foreach(var (name,operation,value) in new[]{("AttributeModifierPaladinHealingPoolBase","Set",5),("AttributeModifierPaladinHealingPoolMultiplier","MultiplyByClassLevel",1)})
        {
            var json=Read(name);check(json.GetProperty("modifiedAttribute").GetString()=="HealingPool" && json.GetProperty("modifierOperation").GetString()==operation && json.GetProperty("modifierValue").GetInt32()==value,"Paladin blueprint: source healing formula:"+name);
        }
        foreach(var name in new[]{"PowerPaladinNeutralizePoison","PowerPaladinCureDisease"})check(Read(name).GetProperty("costPerUse").GetInt32()==5 && Read(name).GetProperty("rechargeRate").GetString()=="HealingPool","Paladin blueprint: five healing points, shared pool:"+name);
        check(Read("PowerPaladinCleansingTouch").GetProperty("usesDetermination").GetString()=="AbilityBonusPlusFixed","Paladin blueprint: legacy Cleansing Touch remains actual CHA, not virtual level");
        var legacy=Read("AdditionalDamagePaladinDivineSmite"); var improved=Read("AdditionalDamagePaladinImprovedDivineSmite");
        check(legacy.GetProperty("triggerCondition").GetString()=="SpendSpellSlot" && legacy.GetProperty("damageAdvancement").GetString()=="SlotLevel" && !legacy.GetProperty("ignoreCriticalDoubleDice").GetBoolean() && legacy.GetProperty("familiesWithAdditionalDice").GetArrayLength()==2, "Paladin blueprint: legacy Smite paid slot scaling, crit doubling and two target families retained");
        check(improved.GetProperty("triggerCondition").GetString()=="AlwaysActive" && !improved.GetProperty("ignoreCriticalDoubleDice").GetBoolean(), "Paladin blueprint: Improved Divine Smite remains separate passive reward, no second paid legacy trigger");
        var divine=Method("SpellBuilders","BuildDivineSmite");
        check(Calls(divine,"SetCastingTime") && Calls(divine,"SetEffectAdvancement") && Calls(divine,"SetRequiresConcentration") && Calls(divine,"SetAdditionalDamageFamilies"), "Paladin compiled2024 Smite source: hit activation, original slot advance, concentration flag and family modifiers retained");
        check(!Types(safety).SelectMany(x=>x.Methods).Any(x=>Calls(x,"SpendSpellSlot") || Calls(x,"InflictDamage")), "Paladin metadata: safety adapter adds no duplicate Smite charge or damage path");
        check(Types(types.Single(x=>x.Name=="FunctorRespec")).SelectMany(x=>x.Methods).Where(x=>x.Name.Contains("TransferConditionsOfCategory")).Any(x=>Calls(x,"ShouldTransferCondition",safety.Name)), "Paladin metadata: successful respec transfer filters stale own class conditions using tested rule");
        var core=Method("Level20Context","PaladinLoad"); var oaths=Method("Level20SubclassesContext","PaladinLoad");
        check(new[]{"PowerPaladinAuraOfProtection18","PowerPaladinAuraOfCourage18"}.All(n=>core.Body.Instructions.Any(i=>i.Operand as string==n)) && Calls(core,"SetOverriddenPower"), "Paladin metadata: both base18 aura upgrades retain overridden powers");
        check(Official.All(n=>Calls(oaths,"get_"+n)) && Calls(oaths,"SetOverriddenPower"),"Paladin metadata: every official oath receives source18 aura upgrade and20 capstone");
        foreach(var name in new[]{"PowerPaladinAuraOfProtection18","PowerPaladinAuraOfCourage18"})
        {
            var json=Read(name);check(json.GetProperty("effectDescription").GetProperty("targetParameter").GetInt32()==13 && json.GetProperty("overriddenPower").ValueKind==JsonValueKind.String,"Paladin blueprint: upgraded aura source range13 and override:"+name);
            check(PaladinProgressionRules.IsPersistentActivation(json.GetProperty("activationTime").GetString()), "Paladin production respec classification against native aura activation:" + name);
        }
        check(Calls(Method("UpgradeEffectDamageBonusBasedOnClassLevel","GetEffectDescription"),"ScalingLevel","WizardProgressionContext") && Calls(Method("WizardProgressionContext","ScalingLevel"),"EffectiveLevel",context.Name),"Paladin metadata: Dread class damage modifier uses scoped effective rank");
        check(Calls(Method("RulesetEffectPower_Ctor_Patch","Custom"),"HoldsPower",context.Name),"Paladin metadata: class power duration scoped by ownership, no global class-level rewrite");
        var recharge=rules.NestedTypes.Single(x=>x.Name=="CustomBehaviourPaladinRechargeLv20Power");
        check(Anywhere(recharge,"BeginRecharge",safety.Name) && Anywhere(recharge,"SpendSpellSlot") && Anywhere(recharge,"UpdateUsageForPowerPool"),"Paladin metadata: verified once-only guard before original single slot/points charge and one restore");
        check(Calls(Method(safety.Name,"CanRecharge"),"CanRecharge","PaladinProgressionRules") && Calls(Method(safety.Name,"CanRecharge"),"HasAnyFeature") && Calls(Method(safety.Name,"BeginRecharge"),"TryFinish","PaladinRechargeLedger"),"Paladin metadata: production ownership/resource rule and transaction ledger wired");
        check(Calls(Method("ClericProgressionContext","PreserveChannelUsesOnShortRest"),"Use2024ChannelRecovery","PaladinProgressionRules") && Anywhere(types.Single(x=>x.Name=="ApplyRest_Patch"),"ChannelRule",context.Name),"Paladin metadata: own short-rest variant without changing compressed Cleric recovery");
        check(Anywhere(types.Single(x=>x.Name=="FunctorRespec"),"CanRespec",safety.Name) && Calls(Method(safety.Name,"CanRespec"),"GetAllConditionsBySourceGuid"),"Paladin metadata: respec refuses stale oath/aura/Smite conditions before replacement creation");
        var action=safety.NestedTypes.Single(x=>x.Name=="ActionSafetyPatch").Methods.Single(x=>x.Name=="Prefix");
        check(action.CustomAttributes.Any(x=>x.AttributeType.Name=="HarmonyPriority") && Calls(action,"CanUseClassMechanics",context.Name),"Paladin metadata: action validation before UB replacement ExecuteImpl mutates resources");
        foreach(var owner in new[]{context,casting,safety})foreach(var patch in owner.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(x=>x.AttributeType.Name=="HarmonyPatch");if(attr==null)continue;
            var targetType=(TypeReference)attr.ConstructorArguments[0].Value;var name=(string)attr.ConstructorArguments[1].Value;
            var target=game.MainModule.Types.FirstOrDefault(x=>x.FullName==targetType.FullName)??types.Single(x=>x.FullName==targetType.FullName);
            var methods=target.Methods.Where(x=>x.Name==name || x.Name=="get_"+name).ToArray();
            check(methods.Length>0,"Paladin metadata: Harmony target exists:"+patch.Name);
            foreach(var p in patch.Methods.Where(x=>x.Name is "Prefix" or "Postfix").SelectMany(x=>x.Parameters).Where(x=>!x.Name.StartsWith("__")))
                check(methods.Any(x=>x.Parameters.Any(q=>q.Name==p.Name)),"Paladin metadata: Harmony named argument:"+patch.Name+"."+p.Name);
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
