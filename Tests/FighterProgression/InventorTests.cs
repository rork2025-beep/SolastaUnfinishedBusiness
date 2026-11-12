using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using Tx = SolastaUnfinishedBusiness.Models.WarlockInvocationTransaction<string>;

// Executes production planners/transactions against managed fixture doubles.
// Blueprint, source and Cecil assertions below inspect metadata; they do not execute Unity/Harmony.
internal static class InventorTests
{
    private static readonly string[] Innovations = { "InnovationAlchemy", "InnovationArmor", "InnovationArtillerist", "InnovationVitriolist", "InnovationVivisectionist", "InnovationWeapon" };
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
        if (json.TryGetProperty("poolType", out var poolType) && json.TryGetProperty("poolAmount", out _))
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
        var cache = new Dictionary<string, FeatureDefinition>();
        return Read(name, "UnfinishedBusinessBlueprints").GetProperty("featureUnlocks").EnumerateArray()
            .Select(x => new FeatureUnlockByLevel(Feature(Name(x.GetProperty("featureDefinition").GetString()), "UnfinishedBusinessBlueprints", cache), x.GetProperty("level").GetInt32()))
            .OrderBy(x => x.Level).ToList();
    }
    private static IEnumerable<FeatureDefinition> Leaves(IEnumerable<FeatureUnlockByLevel> source) => source.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x => x is not FeatureDefinitionFeatureSet);
    private static string Signature(FeatureDefinition f) => f is FeatureDefinitionPointPool p ? $"{p.PoolType}:{p.PoolAmount}" : f.Name;
    private static bool Reject(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
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
        var start = end - 1;
        while (start >= 0 && body[start].OpCode.Code != Code.Stsfld) start--;
        return body.Skip(start + 1).Take(end - start - 1).Where(i => i.Operand is MethodReference m && m.Name == "Add" && m.DeclaringType.Name == "List`1").Select(i => Integer(i.Previous)).ToArray();
    }
    private static (int Original, int Awards, int Replacements)[] Schedule() => Fixture("Inventor")
        .SelectMany(u => CompressedProgressionDefinitions.Flatten(u.FeatureDefinition).Select(f => (u.Level, f.Name)))
        .Where(e => e.Name.StartsWith("InvocationPoolInfusionLearn") || e.Name == "InvocationPoolInventorUnlearnInfusion")
        .GroupBy(e => e.Level).OrderBy(g => g.Key).Select(g => (g.Key,
            g.Where(e => e.Name.StartsWith("InvocationPoolInfusionLearn")).Sum(e => int.Parse(e.Name.Substring("InvocationPoolInfusionLearn".Length))),
            g.Count(e => e.Name == "InvocationPoolInventorUnlearnInfusion"))).ToArray();
    internal static void Run(Action<bool, string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        var expected = new[] { 1, 2, 3, 4, 5, 7, 9, 11, 13, 15, 17, 20 };
        check(Enumerable.Range(1, 12).Select(n => InventorProgressionRules.EffectiveLevel(true, n)).SequenceEqual(expected), "Inventor production mapping: complete 20 to12");
        foreach (var actual in Enumerable.Range(0, 21))
            check(InventorProgressionRules.EffectiveLevel(false, actual) == actual && !InventorProgressionRules.IsCapped(false, actual) && InventorProgressionRules.IsCapped(true, actual) == (actual >= 12), "Inventor production rules: disabled identity and active cap:" + actual);
        var on = new FighterProgressionInitialization(); var off = new FighterProgressionInitialization();
        check(on.ShouldInitialize(true) && !on.ShouldInitialize(true) && !off.ShouldInitialize(false) && !off.ShouldInitialize(true), "Inventor production latch: enabled/disabled initialization exactly once");
        foreach (var name in new[] { "Inventor" }.Concat(Innovations))
        {
            var source = Fixture(name); var initial = source.Select(x => (x.FeatureDefinition, x.Level)).ToArray();
            var transformer = new CompressedProgressionDefinitions("InventorFixture" + name); var output = transformer.Prepare(source, name);
            check(Leaves(output).Select(Signature).SequenceEqual(Leaves(source).Select(Signature)), "Inventor production transformer: every original feature/reward in order:" + name);
            check(initial.SequenceEqual(source.Select(x => (x.FeatureDefinition, x.Level))) && output.All(x => x.Level <= 12), "Inventor production transformer: source untouched/output capped:" + name);
            check(transformer.PoolOrigins.Values.Distinct().Count() == transformer.PoolOrigins.Count, "Inventor production transformer: ASI/feat/expertise independent origins:" + name);
            foreach (var actual in Enumerable.Range(1, 12))
                check(Leaves(output.Where(x => x.Level <= actual)).Select(Signature).SequenceEqual(Leaves(source.Where(x => x.Level <= expected[actual - 1])).Select(Signature)), "Inventor production transformer: complete feature inventory:" + name + "@" + actual);
            foreach (var power in Leaves(source).OfType<FeatureDefinitionPower>())
                check(Leaves(output).Contains(power), "Inventor production transformer: power/upgrade identity retained:" + power.Name);
            if (name == "Inventor") continue;
            check(source.Select(x => x.Level).Distinct().OrderBy(x => x).SequenceEqual(new[] { 3, 5, 9, 11, 15 }), "Inventor blueprint metadata: Innovation milestones incl late SSI:" + name);
            foreach (var auto in Leaves(source).Select(x => Read(x.Name, "UnfinishedBusinessBlueprints")).Where(x => x.ValueKind != JsonValueKind.Undefined && x.TryGetProperty("autoPreparedSpellsGroups", out _)))
            {
                check(Name(auto.GetProperty("spellcastingClass").GetString()) == "Inventor", "Inventor blueprint metadata: bonus spell class:" + name);
                var groups = auto.GetProperty("autoPreparedSpellsGroups").EnumerateArray().ToArray();
                foreach (var actual in Enumerable.Range(1, 12))
                    check(groups.Where(g => FighterProgressionPlan<int>.CompressedLevel(g.GetProperty("classLevel").GetInt32()) <= actual).SelectMany(g => g.GetProperty("spellsList").EnumerateArray().Select(s => s.GetString())).SequenceEqual(groups.Where(g => g.GetProperty("classLevel").GetInt32() <= expected[actual - 1]).SelectMany(g => g.GetProperty("spellsList").EnumerateArray().Select(s => s.GetString()))), "Inventor blueprint projection: every Innovation bonus spell:" + name + "@" + actual);
            }
        }
        var baseSource = Fixture("Inventor");
        check(baseSource.Where(x => x.FeatureDefinition.Name == "FeatureSetAbilityScoreChoice").Select(x => FighterProgressionPlan<int>.CompressedLevel(x.Level)).SequenceEqual(new[] { 4, 7, 9, 11, 12 }), "Inventor blueprint mapping: five ASI4/8/12/16/19 rewards");
        check(Leaves(baseSource.Where(x => x.Level == 11)).Count(f => f.Name is "PowerInventorSpellStoringItem" or "PowerInventorSpellStoringItem2") == 2 && FighterProgressionPlan<int>.CompressedLevel(11) == 8, "Inventor blueprint mapping: both SSI components at actual8");
        check(baseSource.Any(x => x.Level == 6 && x.FeatureDefinition.Name == "ProficiencyInventorToolExpertise") && baseSource.Any(x => x.Level == 7 && x.FeatureDefinition.Name == "FeatureSetInventorFlashOfGenius"), "Inventor blueprint metadata: Tool Expertise and Genius source milestones");
        foreach (var set in baseSource.Select(x => x.FeatureDefinition).OfType<FeatureDefinitionFeatureSet>().Where(x => x.Name == "FeatureSetAbilityScoreChoice")) set.Mode = FeatureDefinitionFeatureSet.FeatureSetMode.Union;
        var targetOutput = new CompressedProgressionDefinitions("InventorTargetAsi").Prepare(baseSource, "Inventor");
        check(Leaves(targetOutput).OfType<FeatureDefinitionPointPool>().Count(x => x.PoolType == "Feat") == 10 && Leaves(targetOutput).OfType<FeatureDefinitionPointPool>().Count(x => x.PoolType == "AbilityScore") == 5, "Inventor production transformer on target ASI+feat fixture: five ASI and ten class feats, racial feat independent");
        check(Leaves(targetOutput.Where(x => x.Level == 12)).OfType<FeatureDefinitionPointPool>().Count(x => x.PoolType == "Feat") == 2, "Inventor production transformer: original18 extra feat and19 ASI feat both available at12");
        Casting(check, types); Infusions(check); Items(check, types); Metadata(check, types, game);
    }
    private static void Casting(Action<bool, string> check, TypeDefinition[] types)
    {
        var caster = Read("CastSpellsInventor", "UnfinishedBusinessBlueprints");
        check(caster.GetProperty("spellKnowledge").GetString() == "WholeList" && caster.GetProperty("spellReadyness").GetString() == "Prepared" && caster.GetProperty("spellPreparationCount").GetString() == "AbilityBonusPlusHalfLevel" && caster.GetProperty("spellcastingAbility").GetString() == "Intelligence", "Inventor blueprint metadata: INT WholeList/Prepared half preparation");
        var slots = AddedIntegers(types.Single(x => x.Name == "FeatureDefinitionCastSpellBuilder"), "SlotsByCasterLevel");
        var points = AddedIntegers(types.Single(x => x.Name == "ModifyPowerPoolAmountPowerSpellPoints"), "SpellPointsByLevel");
        var nativeRows = Enumerable.Range(1, 20).Select(n => slots.Skip(10 * InventorProgressionRules.CasterContribution(n)).Take(10).ToArray()).ToArray();
        var projected = SpellcastingProgressionPlan.ProjectRows(nativeRows);
        var cantrips = SpellcastingProgressionPlan.ProjectCumulative(caster.GetProperty("knownCantrips").EnumerateArray().Select(x => x.GetInt32()).ToArray());
        foreach (var actual in Enumerable.Range(1, 12))
        {
            var effective = InventorProgressionRules.EffectiveLevel(true, actual); var contribution = InventorProgressionRules.CasterContribution(effective);
            check(projected[actual - 1].SequenceEqual(nativeRows[effective - 1]) && projected[actual - 1].Skip(5).All(x => x == 0), "Inventor production slot projection on compiled UB table: half-round-up/no circle6+:" + actual);
            check(points[contribution] == new[] { 4, 4, 6, 6, 14, 17, 27, 32, 38, 44, 57, 64 }[actual - 1], "Inventor compiled table: half-caster Spell Points:" + actual);
            foreach (var intelligence in new[] { -2, 0, 3, 5 })
                check(InventorProgressionRules.LegacyPreparedLimit(effective, intelligence, 2) == Math.Max(1, effective / 2 + intelligence + 2), "Inventor production preparation: real INT and affinity bonus:" + actual + "/" + intelligence);
            check(cantrips[actual - 1] == (effective < 10 ? 2 : effective < 14 ? 3 : 4), "Inventor production cantrip projection: independent cumulative source:" + actual);
        }
        check(projected[11].Take(5).SequenceEqual(new[] { 4, 3, 3, 3, 2 }) && points[10] == 64 && points[20] == 133, "Inventor compiled tables: original20 half-caster slots4/3/3/3/2 and64 points, never133");
        var list = Read("SpellListInventor", "UnfinishedBusinessBlueprints");
        check(list.GetProperty("maxSpellLevel").GetInt32() == 5 && list.GetProperty("hasCantrips").GetBoolean(), "Inventor blueprint metadata: dedicated list ceiling5/cantrips");
        var ritual = Read("MagicAffinityInventorRituals", "UnfinishedBusinessBlueprints");
        check(ritual.GetProperty("ritualCasting").GetString() == "Prepared", "Inventor blueprint metadata: prepared ritual casting retained");
        var ledger = new InventorSpellGrantLedger();
        check(ledger.Grant("12", "cantrip") && !ledger.Grant("12", "cantrip") && ledger.HasStarted("12") && ledger.Grant("next", "cantrip") && !ledger.HasStarted("12"), "Inventor production grant ledger: once per actual level/origin, reused building data safe");
    }
    private static void Infusions(Action<bool, string> check)
    {
        var schedule = Schedule(); var plan = new InventorInfusionPlan<string>(schedule);
        check(schedule.Sum(e => e.Awards) == 12 && schedule.Sum(e => e.Replacements) == 18, "Inventor source schedule: twelve known and eighteen independent optional retrain events");
        check(schedule.Where(e => e.Awards > 0).Select(e => (e.Original, e.Awards)).SequenceEqual(new[] { (2, 4), (6, 2), (10, 2), (14, 2), (18, 2) }), "Inventor source schedule: exact original learn events");
        check(Enumerable.Range(1, 12).SelectMany(plan.Events).SequenceEqual(schedule), "Inventor production infusion planner: all registered events exactly once");
        var known = new List<string>();
        foreach (var actual in Enumerable.Range(1, 12))
        {
            var selected = plan.Events(actual).ToDictionary(e => e.Original.ToString(), e => Enumerable.Range(0, e.Awards).Select(n => "Infusion" + e.Original + ":" + n).ToArray());
            var tx = plan.Transaction(actual, known, n => n.ToString(), tag => selected[tag], _ => Array.Empty<string>());
            var frames = tx.Prepare();
            if (frames.Count > 0) known = frames.Last().After.ToList();
            check(known.Count == schedule.Where(e => e.Original <= InventorProgressionRules.EffectiveLevel(true, actual)).Sum(e => e.Awards), "Inventor production transaction: known count after each compressed level:" + actual);
            check(tx.Events.Sum(e => e.Replacements) == plan.Events(actual).Sum(e => e.Replacements), "Inventor production transaction: independent replacement budgets:" + actual);
        }
        check(known.Count == 12 && plan.Events(12).Select(e => e.Original).SequenceEqual(new[] { 18, 19, 20 }), "Inventor production planner: all three final advancement events retained");
        var added = new Dictionary<string, string[]> { ["18"] = new[] { "new18a", "new18b", "replace18" }, ["19"] = new[] { "replace19" }, ["20"] = new[] { "replace20" } };
        var removed = new Dictionary<string, string[]> { ["18"] = new[] { "old" }, ["19"] = new[] { "new18a" }, ["20"] = new[] { "replace19" } };
        Tx Chain() => plan.Transaction(12, new[] { "old" }, n => n.ToString(), tag => added[tag], tag => removed[tag]);
        var chain = Chain(); var frames12 = chain.Prepare();
        check(frames12.Last().After.SequenceEqual(new[] { "new18b", "replace18", "replace20" }), "Inventor production transaction: learn18/retrain18/retrain19/retrain20; earlier-event new infusion replaceable");
        check(chain.Before(19).Contains("new18a") && !chain.Before(18).Contains("new18a"), "Inventor production transaction: chronological virtual known inventory");
        var applied = new List<string>(); var rollbacks = 0;
        check(chain.ApplyPrepared(e => applied.Add(e.Origin), () => rollbacks++) && !chain.ApplyPrepared(e => applied.Add(e.Origin), () => rollbacks++) && applied.SequenceEqual(new[] { "18", "19", "20" }) && rollbacks == 0, "Inventor production finalization journal: native event order and apply once");
        var bad = Chain();
        check(Reject(() => bad.Prepare((_, original, _) => original != 18)), "Inventor production transaction: original-event prerequisite rejects premature infusion");
        var prerequisite = new Tx(Array.Empty<string>(), new[] { new Tx.Event("orig13", 13, 1, 0, Array.Empty<string>(), new[] { "requires14" }) });
        check(Reject(() => prerequisite.Prepare((_, original, _) => original >= 14)), "Inventor production transaction: original13 cannot learn original14 infusion");
        var free = new Tx(new[] { "old" }, new[] { new Tx.Event("orig19", 19, 0, 1, Array.Empty<string>(), new[] { "free" }) });
        check(Reject(() => free.Prepare()), "Inventor production transaction: optional budget is not a free acquisition");
        removed["19"] = new[] { "future20" }; check(Reject(() => Chain().Prepare()), "Inventor production transaction: future or unknown infusion cannot be unlearned"); removed["19"] = new[] { "new18a" };
        added["19"] = new[] { "new18b" }; check(Reject(() => Chain().Prepare()), "Inventor production transaction: duplicate known infusion rejected"); added["19"] = new[] { "replace19" };
        added["18"] = new[] { "new18b", "replace18" }; check(Reject(() => Chain().Prepare()), "Inventor production transaction: reset18 invalidates stale later replacement/missing learn budget"); added["18"] = new[] { "new18a", "new18b", "replace18" };
        var failure = Chain(); failure.Prepare(); var snapshot = new List<string> { "old" };
        check(Reject(() => failure.ApplyPrepared(e => { snapshot.Clear(); snapshot.AddRange(e.Added); if (e.Original == 19) throw new InvalidOperationException(); }, () => { snapshot.Clear(); snapshot.Add("old"); })) && snapshot.SequenceEqual(new[] { "old" }), "Inventor production journal: injected failure restores infusion snapshot");
        foreach (var actual in Enumerable.Range(1, 12))
        {
            var effective = InventorProgressionRules.EffectiveLevel(true, actual);
            check(InventorProgressionRules.ActiveCapacity(effective) == new[] { 0, 2, 2, 2, 2, 3, 3, 4, 4, 5, 5, 6 }[actual - 1], "Inventor capacity reference rule: active budget distinct from known/replacements:" + actual);
            check(InventorProgressionRules.CannonLimit(effective) == (actual >= 10 ? 2 : 1), "Inventor production cannon limiter: original15 at actual10:" + actual);
        }
    }
    private static void Items(Action<bool, string> check, TypeDefinition[] types)
    {
        // These fixture assertions describe installed definitions, not native item lifecycle execution.
        var infusionPowers = Directory.GetFiles("Diagnostics/UnfinishedBusinessBlueprints", "CustomInvocation*.json", SearchOption.AllDirectories)
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone())
            .Where(j => j.TryGetProperty("grantedFeature", out var grant) && grant.ValueKind == JsonValueKind.String)
            .Select(j => (Invocation: j, PowerName: Name(j.GetProperty("grantedFeature").GetString())))
            .Where(e => e.PowerName.StartsWith("PowerInfusion") || e.PowerName.StartsWith("PowerCreateInfusedReplica")).ToArray();
        check(infusionPowers.Length == 35, "Inventor blueprint metadata: thirty-five registered infusion choices audited");
        foreach (var e in infusionPowers)
        {
            var level = e.Invocation.GetProperty("requiredLevel").GetInt32();
            check(level is 1 or 6 or 10 or 14, "Inventor blueprint metadata: original infusion prerequisite:" + e.PowerName);
            var power = Read(e.PowerName, "UnfinishedBusinessBlueprints");
            check(power.ValueKind != JsonValueKind.Undefined && power.GetProperty("$type").GetString().StartsWith("FeatureDefinitionPowerSharedPool"), "Inventor blueprint metadata: infusion retains shared-pool power type:" + e.PowerName);
            foreach (var actual in new[] { 6, 8, 10 })
            {
                var schedule = new InventorInfusionPlan<string>(Schedule());
                var earlier = schedule.Events(actual).First().Original;
                var tx = new Tx(Array.Empty<string>(), new[] { new Tx.Event("nativePool", earlier, 1, 0, Array.Empty<string>(), new[] { e.PowerName }) });
                check(Reject(() => tx.Prepare((_, original, _) => original >= level)) == (earlier < level), "Inventor production transaction on infusion blueprint: original-event prerequisite:" + e.PowerName + "@" + earlier);
            }
        }
        var tool = Read("ProficiencyInventorToolExpertise", "UnfinishedBusinessBlueprints");
        check(tool.GetProperty("proficiencyType").GetString() == "ToolOrExpertise" && tool.GetProperty("proficiencies").GetArrayLength() == 7, "Inventor blueprint metadata: all seven native Tool Expertise entries preserved");
        var genius = Read("PowerInventorFlashOfGeniusBonus", "UnfinishedBusinessBlueprints");
        check(genius.GetProperty("usesAbilityScoreName").GetString() == "Intelligence" && genius.GetProperty("usesDetermination").GetString() == "AbilityBonusPlusFixed" && genius.GetProperty("rechargeRate").GetString() == "LongRest", "Inventor blueprint metadata: Flash uses real INT and long rest");
        var soul = Read("PowerInventorSoulOfArtifice", "UnfinishedBusinessBlueprints");
        check(soul.GetProperty("fixedUsesPerRecharge").GetInt32() == 1 && soul.GetProperty("rechargeRate").GetString() == "LongRest", "Inventor blueprint metadata: Soul keeps native one/LR resource");
        var source = File.ReadAllText("SolastaUnfinishedBusiness/Classes/InventorClass.cs");
        check(source.Contains("SetChargesCapitalNumber(6)") && source.Contains("BasedOnItemSummoner") && source.Contains("SetOutOfChargesConsequence(EquipmentDefinitions.ItemOutOfCharges.Destroy)"), "Inventor source metadata: SSI fixed six charges, summoner DC, exhausted-item removal");
        check(source.Split("new ModifyAttunementLimit(1)").Length == 3, "Inventor source metadata: Adept/Savant each adds one attunement slot; Soul adds none");
        var armor = File.ReadAllText("SolastaUnfinishedBusiness/Subclasses/InnovationArmor.cs");
        check(armor.Contains("SetFixedValue(InventorClass.InfusionPool, 2)") && armor.Contains("ArmorerInfusions.Marker"), "Inventor source metadata: Armor original9 adds two active capacity, not two learned choices");
        foreach (var name in Innovations)
        {
            var feature = Fixture(name).Single(f => f.FeatureDefinition.Name == "FeatureSetSpellStoringItem" + name);
            var powers = Leaves(new[] { feature }).OfType<FeatureDefinitionPower>().ToArray();
            check(feature.Level == 11 && powers.Length > 0 && powers.All(p => p.Name.StartsWith("PowerCreateSpellStoringWandOf")), "Inventor blueprint metadata: dynamic Innovation-specific SSI choices at original11:" + name);
        }
        var ledger = new InventorActionLedger();
        check(ledger.TryBegin() && !ledger.TryBegin() && new InventorActionLedger().TryBegin(), "Inventor production item-action journal: repeated parent callback cannot create a second item; new action distinct");
        foreach (var active in new[] { 0, 5, 6, 8 }) foreach (var maximum in new[] { 6, 8 }) foreach (var duplicate in new[] { false, true })
            check(InventorProgressionRules.CanInfuse(active, maximum, duplicate) == (active < maximum && !duplicate), "Inventor production infusion safety: known versus active capacity and unique source:" + active + "/" + maximum + "/" + duplicate);
    }
    private static void Metadata(Action<bool, string> check, TypeDefinition[] types, AssemblyDefinition game)
    {
        MethodDefinition Method(string type, string method) => types.Single(t => t.Name == type).Methods.First(m => m.Name == method);
        bool Anywhere(string type, string call, string owner = null) => Types(types.Single(t => t.Name == type)).SelectMany(t => t.Methods).Any(m => Calls(m, call, owner));
        MethodDefinition boot;
        // Locate compiler-generated boot callback by its calls, not by a compiler-dependent lambda number.
        boot = types.SelectMany(t => t.Methods).Single(m => Calls(m, "LateLoad", "InventorProgressionContext"));
        var calls = boot.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => (MethodReference)i.Operand).ToArray();
        int Position(string type, string method = "LateLoad") => Array.FindIndex(calls, m => m.DeclaringType.Name == type && m.Name == method);
        check(Position("InventorProgressionContext") > Position("InventorClass", "LateLoadSpellStoringItem") && Position("InventorProgressionContext") > Position("RangerProgressionContext"), "Inventor Cecil metadata: after dynamic SSI and all previous progression modules");
        check(Anywhere("InventorProgressionContext", "ShouldInitialize") && Anywhere("InventorProgressionContext", "get_EnableLevel20") && Anywhere("InventorProgressionContext", "get_EnableMulticlass"), "Inventor Cecil metadata: default-off latch and prerequisites");
        check(Calls(Method("InventorProgressionContext", "LateLoad"), "Capture", "InventorInfusionContext") && Calls(Method("InventorProgressionContext", "LateLoad"), "RegisterPowers", "InventorItemSafetyContext"), "Inventor Cecil metadata: capture final native infusion schedule and dynamic item powers");
        check(Anywhere("InventorInfusionContext", "Transaction", "InventorInfusionPlan`1") && Anywhere("InventorInfusionContext", "Prepare", "WarlockInvocationTransaction`1") && Anywhere("InventorInfusionContext", "ApplyPrepared", "WarlockInvocationTransaction`1"), "Inventor Cecil metadata: actual adapter uses executed production planner and transaction");
        check(Anywhere("InventorInfusionContext", "Setup", "CustomInvocationPoolDefinitionBuilder") && Anywhere("InventorInfusionContext", "CopyCustomSubFeatures"), "Inventor Cecil metadata: custom pool clones preserve type/learn/unlearn budgets/subfeatures");
        foreach (var boundary in new[] { ("MulticlassInOutRulesHelper", "EnumerateHeroAllowedClassDefinitions"), ("AddClassLevel_Patch", "Prefix"), ("AssignClassLevel_Patch", "Prefix") })
            check(Anywhere(boundary.Item1, "IsCapped", "InventorProgressionContext") && Anywhere(boundary.Item1, "CanSelectClass", "InventorSpellcastingContext"), "Inventor Cecil metadata: class12/martial eligibility boundary:" + boundary.Item1);
        check(Calls(Method("FinalizeCharacter_Patch", "Prefix"), "BeginFinalize", "InventorInfusionContext") && Calls(Method("FinalizeCharacter_Patch", "Prefix"), "BeginFinalize", "InventorSpellcastingContext"), "Inventor Cecil metadata: validate both infusion and spell drafts before native mutations");
        check(Calls(Method("WarlockInvocationContext", "ApplyOrPass"), "TryApply", "InventorInfusionContext") && Anywhere("InventorInfusionContext", "get_FinalizationAdapterAvailable"), "Inventor Cecil metadata: ordered custom finalization routed through verified native binding");
        foreach (var method in new[] { "Refresh", "ResetLearnings", "OnFeatureSelected" })
            check(Calls(Method("CustomInvocationSelectionPanel", method), method == "Refresh" ? "EnterSelection" : method == "ResetLearnings" ? "ResetFrom" : "CanSelect", "InventorInfusionContext"), "Inventor Cecil metadata: custom UI original-event lens/Back/reset/selection boundary:" + method);
        check(Anywhere("SpellsByLevelGroupExtensions", "Known", "InventorInfusionContext") && Calls(Method("InvocationPoolTypeCustom", "GetClassOrSubclassLevel"), "SelectionLevel", "InventorInfusionContext"), "Inventor Cecil metadata: custom infusion virtual known inventory and original prerequisite level");
        check(Anywhere("InventorItemSafetyContext", "GetAllEffectsBySourceGuid") && Anywhere("InventorItemSafetyContext", "CanAdvance") && Anywhere("ToolsContext", "CanRespec", "InventorItemSafetyContext"), "Inventor Cecil metadata: active owner-scoped items/construct guard before level-up and respec");
        check(Calls(Method("SharedSpellsContext", "GetSharedCasterLevel"), "SingleCasterLevel", "InventorSpellcastingContext") && Calls(Method("ModifyPowerPoolAmountPowerSpellPoints", "GetCasterLevel"), "SingleCasterLevel", "InventorSpellcastingContext"), "Inventor Cecil metadata: class-specific half-caster level feeds SP and spell circles");
        check(Anywhere("InventorSpellcastingContext", "LegacyPreparedLimit", "InventorProgressionRules") && Anywhere("InventorSpellcastingContext", "IsMulticaster"), "Inventor Cecil metadata: INT preparation and mixed-caster restrictions");
        var soul = types.Single(t => t.Name == "CustomBehaviorInitiatedSoulOfArtifice");
        check(Types(soul).SelectMany(t => t.Methods).Any(m => Calls(m, "EffectiveLevel", "InventorProgressionContext")), "Inventor Cecil metadata: Soul healing uses effective source class level20");
        check(Calls(soul.Methods.Single(m => m.Name == "OnSavingThrowInitiated"), "get_items") || soul.Methods.Single(m => m.Name == "OnSavingThrowInitiated").Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "items"), "Inventor Cecil metadata: Soul saving throw still counts actual attuned inventory");
        check(Anywhere("InnovationArtillerist", "CannonLimit", "InventorProgressionRules") && Anywhere("InnovationVitriolist", "EffectiveLevel", "InventorProgressionContext"), "Inventor Cecil metadata: direct Innovation thresholds adapted");
        check(Calls(Method("ModifyEffectDescriptionOnLevels", "GetLevel"), "EffectiveLevel", "InventorProgressionContext") && Calls(Method("ModifyPowerPoolAmount", "PoolChangeAmount"), "EffectiveLevel", "InventorProgressionContext"), "Inventor Cecil metadata: infusion enhancements/Innovation resource class scaling");
        check(Anywhere("InventorItemSafetyContext", "CanInfuse", "InventorProgressionRules") && Anywhere("InventorItemSafetyContext", "TryBegin", "InventorActionLedger"), "Inventor Cecil metadata: item guard uses executed production capacity/action rules");
        check(Calls(Method("InventorProgressionContext", "CanContinue"), "IsNewTestHero") && Calls(Method("InventorProgressionContext", "CanUseClassMechanics"), "IsNewTestHero"), "Inventor Cecil metadata: unmarked old Inventors rejected without migration");
        check(Calls(Method("InventorProgressionContext", "EffectiveLevel"), "SourceOwner") && Anywhere("InventorProgressionContext", "get_CharacterFamily"), "Inventor Cecil metadata: construct scaling resolves owner only for Inventor construct family");
        check(Calls(Method("ApplyDamageForm_Patch", "GetDiceOfRank"), "EffectiveLevel", "InventorProgressionContext"), "Inventor Cecil metadata: Arcane Jolt and construct dice use Inventor class progression");
        check(!types.Single(t => t.FullName == "SolastaUnfinishedBusiness.Settings").Methods.Where(m => m.IsConstructor).Any(m => Calls(m, "set_EnableInventorCompressedProgressionPrototype")), "Inventor Cecil metadata: new boolean option has no constructor opt-in and defaults off");
        check(Anywhere("InventorItemSafetyContext", "InfusionOrigin") && Anywhere("InventorItemSafetyContext", "get_TrainedInvocations") && Anywhere("InventorItemSafetyContext", "GetRemainingPowerUses"), "Inventor Cecil metadata: stale/duplicate master infusion and exhausted native resources rejected");
        check(Anywhere("InventorItemSafetyContext", "get_SourceDefinition") && Anywhere("InventorItemSafetyContext", "get_SpellListDefinition") && Anywhere("InventorItemSafetyContext", "HasAnyFeature"), "Inventor Cecil metadata: SSI class8/list/Innovation ownership boundary");
        var original = game.MainModule.Types.Single(t => t.Name == "CharacterBuildingManager").Methods.Single(m => m.Name == "FinalizeCharacter");
        check(original.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "TrainInvocations" && m.DeclaringType.Name == "RulesetCharacterHero") == 1 && original.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "UnlearnInvocations" && m.DeclaringType.Name == "RulesetCharacterHero") == 1, "Inventor native Cecil metadata: finalization transpiler's expected single train/unlearn call sites");
        var registered = Directory.GetFiles("SolastaUnfinishedBusiness/Subclasses", "Innovation*.cs").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x);
        check(registered.SequenceEqual(Innovations.OrderBy(x => x)), "Inventor source inventory: all six actual Innovation implementations covered; no official Inventor class");
        foreach (var file in new[] { "Classes/InventorClass.cs", "Subclasses/InnovationArtillerist.cs", "Subclasses/InnovationVitriolist.cs" })
            check(!File.ReadAllText("SolastaUnfinishedBusiness/" + file).Contains("GetClassLevel(InventorClass.Class)"), "Inventor source audit: direct class-level reads replaced only in source class callbacks:" + file);
    }
}
