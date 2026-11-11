using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;

// Pure production rules/transformer run against managed doubles. Cecil checks inspect metadata only.
// No Harmony execution, prepared-spell GUI, rest, combat, respec or save/load is claimed.
internal static class ClericTests
{
    private static readonly string[] Official = { "DomainBattle", "DomainElementalCold", "DomainElementalFire", "DomainElementalLighting",
        "DomainInsight", "DomainLaw", "DomainLife", "DomainMischief", "DomainOblivion", "DomainSun" };
    private static readonly string[] Ub = { "DomainDefiler", "DomainNature", "DomainOrder", "DomainSmith", "DomainTempest" };
    private static readonly string[] Capstones = { "FeatureSetDomainBattleParagonOfBattle", "PowerDomainColdSummonBlizzard", "PowerDomainFireSummonInferno",
        "FeatureSetDomainLightningLivingTempest", "FeatureSetDomainInsightAvatarOfKnowledge", "FeatureDomainLawFinalWord", "DomainLifeSupremeHealing",
        "PowerDomainMischiefStrikeOfChaos17", "FeatureDomainOblivionKeeperOfOblivion", "FeatureSetDomainSunRisingDawn" };
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
        var source = Read(name, folder).GetProperty("featureUnlocks").EnumerateArray()
            .Select(x => new FeatureUnlockByLevel(Feature(Name(x.GetProperty("featureDefinition").GetString()), folder, cache), x.GetProperty("level").GetInt32())).ToList();
        if (name == "Cleric")
        {
            source.Add(new(Feature("PowerClericTurnUndead17", folder, cache), 17));
            source.Add(new(Feature("AttributeModifierClericChannelDivinityAdd", folder, cache), 18));
            source.Add(new(Feature("FeatureSetAbilityScoreChoice", folder, cache), 19));
        }
        var index = Array.IndexOf(Official, name);
        if (index >= 0)
        {
            source.Add(new(Feature(Capstones[index], folder, cache), 17));
            var kind = name is "DomainLife" or "DomainInsight" or "DomainOblivion" ? "Cleric" : name is "DomainBattle" or "DomainLaw" ? "Paladin" : "Wizard";
            source.Add(new(Feature("PowerClericDivineInterventionImprovement" + kind, folder, cache), 20));
            if (name == "DomainMischief")
            {
                source.Add(new(Feature("PowerDomainMischiefFortuneFavorTheBold", folder, cache), 17));
                source.Add(new(Feature("PowerDomainMischiefStrikeOfChaos20", folder, cache), 20));
            }
        }
        return source.OrderBy(x => x.Level).ToList();
    }
    private static IEnumerable<FeatureDefinition> Leaves(IEnumerable<FeatureUnlockByLevel> source) =>
        source.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x => x is not FeatureDefinitionFeatureSet);
    private static string Signature(FeatureDefinition f) => f is FeatureDefinitionPointPool p ? $"{p.PoolType}:{p.PoolAmount}" : f.Name;

    internal static void Run(Action<bool, string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        check(Enumerable.Range(1, 12).Select(n => ClericProgressionRules.EffectiveLevel(true, n)).SequenceEqual(new[] { 1,2,3,4,5,7,9,11,13,15,17,20 }), "Cleric: exact20-to12 mapping");
        check(Enumerable.Range(1, 12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).SequenceEqual(Enumerable.Range(1, 20)), "Cleric: all20 original advancement events without omissions");
        check(ClericProgressionRules.IsCapped(true, 12) && ClericProgressionRules.IsCapped(true, 13) && !ClericProgressionRules.IsCapped(true, 11) && !ClericProgressionRules.IsCapped(false, 12), "Cleric: opt-in class12 boundary");
        check(Enumerable.Range(0, 21).All(n => ClericProgressionRules.EffectiveLevel(false, n) == n) && ClericProgressionRules.EffectiveLevel(true, 13) == 13, "Cleric: disabled and unsupported old levels retain actual rank");
        var active = new FighterProgressionInitialization(); var off = new FighterProgressionInitialization();
        check(active.ShouldInitialize(true) && !active.ShouldInitialize(true) && !off.ShouldInitialize(false) && !off.ShouldInitialize(true), "Cleric: one-time initialization, including disabled latch");
        foreach (var name in new[] { "Cleric" }.Concat(Official).Concat(Ub))
        {
            var folder = Ub.Contains(name) ? "UnfinishedBusinessBlueprints" : "OfficialBlueprints";
            var source = Fixture(name, folder); var before = source.Select(x => (x.FeatureDefinition, x.Level)).ToArray();
            var previousPowers = Leaves(source).OfType<FeatureDefinitionPower>().Select(x => (Power: x, Previous: x.OverriddenPower)).ToArray();
            var transformer = new CompressedProgressionDefinitions("ClericFixture" + name); var result = transformer.Prepare(source, name);
            check(Leaves(result).Select(Signature).SequenceEqual(Leaves(source).Select(Signature)), "Cleric: complete ordered base/domain feature inventory: " + name);
            check(source.Select(x => (x.FeatureDefinition, x.Level)).SequenceEqual(before) && result.All(x => x.Level <= 12), "Cleric: source unchanged and all output levels capped: " + name);
            check(transformer.PoolOrigins.Values.Distinct().Count() == transformer.PoolOrigins.Count, "Cleric: independent repeated choice origins: " + name);
            check(previousPowers.All(x => Leaves(result).Contains(x.Power) && ReferenceEquals(x.Power.OverriddenPower, x.Previous)), "Cleric: power identities and original override references retained: " + name);
            if (name == "Cleric") continue;
            var auto = Leaves(source).Select(x => Read(x.Name, folder)).Where(x => x.ValueKind != JsonValueKind.Undefined && x.TryGetProperty("autoPreparedSpellsGroups", out _)).ToArray();
            check(auto.Length > 0 && auto.All(x => Name(x.GetProperty("spellcastingClass").GetString()) == "Cleric"), "Cleric: domain spells belong to Cleric, not a second casting class: " + name);
            foreach (var definition in auto)
            {
                var groups = definition.GetProperty("autoPreparedSpellsGroups").EnumerateArray().ToArray();
                var projected = groups.Select(x => (Level: FighterProgressionPlan<int>.CompressedLevel(x.GetProperty("classLevel").GetInt32()), Spells: x.GetProperty("spellsList").EnumerateArray().Select(s => s.GetString()).ToArray())).ToArray();
                check(projected.SelectMany(x => x.Spells).SequenceEqual(groups.SelectMany(x => x.GetProperty("spellsList").EnumerateArray().Select(s => s.GetString()))) &&
                    Enumerable.Range(1, 12).All(actual => projected.Where(x => x.Level <= actual).SelectMany(x => x.Spells).SequenceEqual(groups.Where(x => x.GetProperty("classLevel").GetInt32() <= ClericProgressionRules.EffectiveLevel(true, actual)).SelectMany(x => x.GetProperty("spellsList").EnumerateArray().Select(s => s.GetString())))),
                    "Cleric: unchanged domain spells with exact compressed threshold availability: " + definition.GetProperty("name").GetString());
            }
        }
        CheckTables(check, types); CheckVariants(check); CheckMetadata(check, types, game);
    }

    private static void CheckTables(Action<bool, string> check, TypeDefinition[] types)
    {
        var casting = Read("CastSpellCleric");
        check(casting.GetProperty("spellKnowledge").GetString() == "WholeList" && casting.GetProperty("spellReadyness").GetString() == "Prepared" &&
            new[] { "knownSpells", "scribedSpells", "replacedSpells" }.All(n => casting.GetProperty(n).EnumerateArray().All(x => x.GetInt32() == 0)), "Cleric: actual WholeList/Prepared definition has no Sorcerer known events or Wizard scribing");
        var cantrips = casting.GetProperty("knownCantrips").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        check(SpellcastingProgressionPlan.ProjectCumulative(cantrips).Take(12).SequenceEqual(new[] { 3,3,3,4,4,4,4,5,5,5,5,5 }), "Cleric: ordinary cantrip progression3/4/5 at actual1/4/8");
        var slots = AddedIntegers(types.Single(x => x.Name == "FeatureDefinitionCastSpellBuilder"), "SlotsByCasterLevel");
        var rows = Enumerable.Range(1, 20).Select(n => slots.Skip(n * 10).Take(10).ToArray()).ToArray();
        var output = SpellcastingProgressionPlan.ProjectRows(rows);
        for (var actual = 1; actual <= 12; actual++)
            check(output[actual - 1].SequenceEqual(rows[ClericProgressionRules.EffectiveLevel(true, actual) - 1]), "Cleric: exact full-caster slots: actual" + actual);
        check(output[10][8] == 1 && output[11].SequenceEqual(new[] { 4,3,3,3,3,2,2,1,1,0 }), "Cleric: ninth circle at11 and original20 slots at12");
        var points = AddedIntegers(types.Single(x => x.Name == "ModifyPowerPoolAmountPowerSpellPoints"), "SpellPointsByLevel");
        check(Enumerable.Range(1, 12).Select(n => points[SpellcastingProgressionPlan.CasterLevel(n)]).SequenceEqual(new[] { 4,6,14,17,27,38,57,73,83,94,107,133 }), "Cleric: real UB Spell Points table reaches133 without a new hardcoded budget");
        check(Enumerable.Range(-3, 30).All(n => SpellcastingProgressionPlan.CasterLevel(n) is >= 0 and <= 20), "Cleric: casting table indices bounded0-20");
        var prepared = AddedIntegers(types.Single(x => x.Name == "ComputeMaxPreparedSpells_Patch"), "ClericOrDruidOneDndPreparedSpellsTable");
        check(SpellcastingProgressionPlan.ProjectCumulative(prepared).Take(12).SequenceEqual(new[] { 4,5,6,7,9,11,14,16,17,18,19,22 }), "Cleric: real compiled2024 prepared table projected without adding ordinary WIS twice");
        check(ClericProgressionRules.LegacyPreparedLimit(20, 5, 0) == 25 && ClericProgressionRules.LegacyPreparedLimit(17, 4, 3) == 24 &&
            ClericProgressionRules.LegacyPreparedLimit(1, -5, 0) == 1, "Cleric: production legacy preparation uses effective class + WIS + original affinities and final minimum1");
        check(new[] { 1,5,6,9,12 }.Select(n => ClericProgressionRules.DivineSparkDice(ClericProgressionRules.EffectiveLevel(true, n))).SequenceEqual(new[] { 1,1,2,3,4 }), "Cleric: production Spark healing dice1/2/3/4 map at original1/7/13/18");
        check(ClericProgressionRules.IsOriginTag("03ClassCleric1", "03ClassCleric1") && ClericProgressionRules.IsOriginTag("03ClassCleric1Thaumaturge", "03ClassCleric1") &&
            !ClericProgressionRules.IsOriginTag("03ClassCleric12", "03ClassCleric1") && !ClericProgressionRules.IsOriginTag("06SubclassOther", "03ClassCleric1"), "Cleric: exact level origins accept extra tags but reject stale level12 at1 and foreign owners");
        foreach (var (compressed, cleric, paladin, expected) in new[] { (false,false,false,false), (false,false,true,true), (false,true,false,true), (false,true,true,true),
                     (true,false,false,false), (true,false,true,false), (true,true,false,true), (true,true,true,true) })
            check(ClericProgressionRules.Use2024ChannelRecovery(compressed, cleric, paladin) == expected,
                $"Cleric: short-rest recovery scoped to own rule; compressed={compressed}, Cleric2024={cleric}, Paladin2024={paladin}");
        var ledger = new ClericSpellGrantLedger();
        check(ledger.Grant("Cleric6", "A") && !ledger.Grant("Cleric6", "A") && ledger.Grant("Cleric6", "B") && ledger.HasStarted("Cleric6"), "Cleric: native repeated grant deduplicated while independent pool remains grantable");
        check(!ledger.HasStarted("Cleric7") && ledger.Grant("Cleric7", "A"), "Cleric: reused building data can advance again without stale grant rejection");
    }

    private static void CheckVariants(Action<bool, string> check)
    {
        for (var flags = 0; flags < 32; flags++)
        {
            bool On(int bit) => (flags & (1 << bit)) != 0;
            // Source switch effects, exercised by the production transformer. Not native switch execution.
            var source = Fixture("Cleric", "OfficialBlueprints");
            source = source.Select(x => x.FeatureDefinition.Name == "SubclassChoiceClericDivineDomains" ? new FeatureUnlockByLevel(x.FeatureDefinition, On(3) ? 3 : 1) : x).ToList();
            if (On(0)) source.Add(new(new FeatureDefinitionPower { Name = "PowerClericDivineSpark" }, 2));
            if (On(1)) source.Add(new(Feature("FeatureSetClericDivineOrder", "UnfinishedBusinessBlueprints", new()), 1));
            if (On(2))
            {
                source.RemoveAll(x => x.FeatureDefinition.Name.StartsWith("PowerClericTurnUndead") && x.FeatureDefinition.Name != "PowerClericTurnUndead");
                source.Add(new(new FeatureDefinition { Name = "FeatureClericSearUndead" }, 5));
            }
            foreach (var asi in source.Select(x => x.FeatureDefinition).OfType<FeatureDefinitionFeatureSet>().Where(x => x.Name == "FeatureSetAbilityScoreChoice")) asi.Mode = FeatureDefinitionFeatureSet.FeatureSetMode.Union;
            var bonusFeat = new FeatureDefinitionPointPool { Name = "PointPool1BonusFeats", PoolType = "Feat", PoolAmount = 1 };
            source.AddRange(new[] { 2,6,10,14,18 }.Select(n => new FeatureUnlockByLevel(bonusFeat, n)));
            var transformer = new CompressedProgressionDefinitions("ClericVariant" + flags); var output = transformer.Prepare(source, "Cleric");
            var leaves = Leaves(output).ToArray(); var pools = leaves.OfType<FeatureDefinitionPointPool>().ToArray();
            check(pools.Where(x => x.PoolType == "AbilityScore").Sum(x => x.PoolAmount) == 10 && pools.Where(x => x.PoolType == "Feat").Sum(x => x.PoolAmount) == 10,
                "Cleric: variant" + flags + " preserves5 class ASI+feats and5 extra feat events; first-level racial reward independent");
            check(output.Single(x => x.FeatureDefinition.Name == "SubclassChoiceClericDivineDomains").Level == (On(3) ? 3 : 1) &&
                leaves.Any(x => x.Name == "PowerClericDivineSpark") == On(0), "Cleric: variant" + flags + " final domain selection and Spark presence");
            var channel = output.SelectMany(u => Leaves(new[] { u }).Select(f => (u.Level, f.Name))).Where(x => x.Name.StartsWith("AttributeModifierClericChannelDivinity")).ToArray();
            int Uses(int actual) => channel.Where(x => x.Level <= actual).Sum(x => x.Name.EndsWith("Add") ? 1 : On(0) ? 2 : 1);
            check(Uses(6) == (On(0) ? 3 : 2) && Uses(11) == Uses(6) && Uses(12) == (On(0) ? 4 : 3) && channel.Length == 3,
                "Cleric: variant" + flags + " one shared Channel attribute and original6/18 upgrades, no duplicate pools");
            check(On(2) ? leaves.Count(x => x.Name.StartsWith("PowerClericTurnUndead")) == 1 && leaves.Any(x => x.Name == "FeatureClericSearUndead") :
                output.Where(x => x.Level == 11).SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Any(x => x.Name == "PowerClericTurnUndead17"),
                "Cleric: variant" + flags + " Sear replaces every legacy Destroy tier, otherwise original17 appears at11");
            var order = output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionFeatureSet>().FirstOrDefault(x => x.Name == "FeatureSetClericDivineOrder");
            check(On(1) ? order?.Mode == FeatureDefinitionFeatureSet.FeatureSetMode.Exclusion && order.FeatureSet.Count == 2 : order == null,
                "Cleric: variant" + flags + " Divine Order remains one exclusive Protector/Thaumaturge choice");
            var domain = Fixture("DomainLife", "OfficialBlueprints");
            if (On(4))
            {
                domain.RemoveAll(x => x.FeatureDefinition.Name == "AdditionalDamageDomainLifeDivineStrike");
                domain.Add(new(Feature("FeatureSetClericBlessedStrikes", "UnfinishedBusinessBlueprints", new()), 7));
                domain.Add(new(Feature("FeatureClericImprovedBlessedStrikes", "UnfinishedBusinessBlueprints", new()), 14));
            }
            var result = new CompressedProgressionDefinitions("ClericVariantDomain" + flags).Prepare(domain, "DomainLife");
            check(On(4) ? !Leaves(result).Any(x => x.Name == "AdditionalDamageDomainLifeDivineStrike") && result.Where(x => x.Level == 10).SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Any(x => x.Name == "FeatureClericImprovedBlessedStrikes") :
                Leaves(result).Any(x => x.Name == "AdditionalDamageDomainLifeDivineStrike"), "Cleric: variant" + flags + " Blessed Strikes replaces legacy damage; original14 improvement at10");
        }
    }

    private static void CheckMetadata(Action<bool, string> check, TypeDefinition[] types, AssemblyDefinition game)
    {
        var context = types.Single(x => x.Name == "ClericProgressionContext"); var casting = types.Single(x => x.Name == "ClericSpellcastingContext");
        var startup = types.SelectMany(x => x.Methods).Single(x => Calls(x, "LateLoad", "ClericProgressionContext"));
        int Position(string owner) => startup.Body.Instructions.Select((x, i) => (x, i)).Single(x => x.x.Operand is MethodReference m && m.Name == "LateLoad" && m.DeclaringType.Name == owner).i;
        check(new[] { "BardProgressionContext", "WarlockProgressionContext", "Tabletop2024Context", "SpellPointsContext" }.All(x => Position("ClericProgressionContext") > Position(x)), "Cleric metadata: final transformation follows all prior modules and rules");
        var settings = types.Single(x => x.FullName == "SolastaUnfinishedBusiness.Settings");
        check(settings.Methods.Where(x => x.IsConstructor && !x.IsStatic).All(x => !x.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name.Contains("EnableClericCompressedProgressionPrototype"))), "Cleric metadata: CLR false opt-in default");
        var discover = context.Methods.Single(x => x.Name == "DiscoverSubclasses");
        check(Types(context).SelectMany(x => x.Methods).Any(x => Calls(x, "get_SelectableByPlayer", "DeityDefinition")) && Calls(discover, "get_KlassListSubclassEnabled"), "Cleric metadata: discover selectable deity registrations and enabled UB domains, never infer every Domain name");
        var deities = Directory.GetFiles("Diagnostics/OfficialBlueprints/DeityDefinition", "*.json").Select(path => { using var d = JsonDocument.Parse(File.ReadAllText(path)); return d.RootElement.Clone(); }).Where(x => x.GetProperty("selectableByPlayer").GetBoolean());
        check(deities.SelectMany(x => x.GetProperty("subclasses").EnumerateArray().Select(s => s.GetString())).Distinct().OrderBy(x => x).SequenceEqual(Official.OrderBy(x => x)), "Cleric: all10 official domains are actual deity choices");
        var builders = types.Where(x => x.BaseType?.Name == "AbstractSubclass" && x.Methods.Any(m => m.Name == "get_Klass" && Calls(m, "get_Cleric"))).ToArray();
        check(builders.Select(x => x.Name).OrderBy(x => x).SequenceEqual(Ub.OrderBy(x => x)), "Cleric metadata: all5 actual UB domain builders discovered");
        var level20 = types.Single(x => x.Name == "Level20Context").Methods.Single(x => x.Name == "ClericLoad");
        check(Calls(level20, "get_AttributeModifierClericChannelDivinityAdd") && new[] { 17,18,19 }.All(n => level20.Body.Instructions.Any(x => Integer(x) == n)), "Cleric metadata: source17 Turn/18 Channel/19 ASI and original20 slots retained");
        var domains = types.Single(x => x.Name == "Level20SubclassesContext"); var load = domains.Methods.Single(x => x.Name == "ClericLoad");
        check(Official.All(x => Calls(load, "get_" + x)) && Capstones.All(n => load.Body.Instructions.Any(i => i.Operand as string == n)), "Cleric metadata: all10 official17 domain features verified against compiled loader");
        check(new[] { "Cleric", "Paladin", "Wizard" }.All(kind => domains.Methods.Single(x => x.Name == ".cctor").Body.Instructions.Any(i => i.Operand as string == "PowerClericDivineInterventionImprovement" + kind)), "Cleric metadata: all3 installed original20 Intervention upgrades exist");
        var rules = types.Single(x => x.Name == "Tabletop2024Context");
        foreach (var (method, flag) in new[] { ("BlessedStrikes", "EnableClericBlessedStrikes2024"), ("ChannelDivinity", "EnableClericChannelDivinity2024"), ("DivineOrder", "EnableClericDivineOrder2024"), ("DomainLearningLevel", "EnableClericToLearnDomainAtLevel3"), ("SearUndead", "EnableClericSearUndead2024") })
            check(Calls(rules.Methods.Single(x => x.Name == "SwitchCleric" + method), "get_" + flag), "Cleric metadata: authoritative enabled/disabled source switch: " + flag);
        check(!settings.Properties.Any(x => x.Name == "EnableClericDivineIntervention2024") && !rules.Methods.Any(x => x.Name == "SwitchClericDivineIntervention"), "Cleric metadata:2024 Divine Intervention setting and implementation are absent, not falsely claimed supported");
        check(context.NestedTypes.Count(x => x.Name.EndsWith("RestartPatch")) == 5 && Calls(casting.Methods.Single(x => x.Name == "BeginFinalize"), "get_SessionConsistent"), "Cleric metadata: every compiled Cleric switch deferred and inconsistent session rejected");
        check(Calls(casting.Methods.Single(x => x.Name == "Initialize"), "get_SpellKnowledge") && !Calls(casting.Methods.Single(x => x.Name == "Initialize"), "get_ScribedSpells") && !Calls(casting.Methods.Single(x => x.Name == "Initialize"), "get_KnownSpells"), "Cleric metadata: native WholeList preparation retained, no borrowed known/scribed budgets");
        check(Calls(casting.Methods.Single(x => x.Name == "AdaptAutoPrepared"), "CopyCustomSubFeatures") && Types(casting).SelectMany(x => x.Methods).Any(x => Calls(x, "CompressedLevel")), "Cleric metadata: auto-prepared groups cloned with custom behaviors and compressed thresholds");
        check(Calls(casting.NestedTypes.Single(x => x.Name == "AutoPreparedPatch").Methods.Single(x => x.Name == "Postfix"), "Distinct"), "Cleric metadata: duplicate executable auto-prepared entries removed only for Cleric");
        check(Calls(casting.NestedTypes.Single(x => x.Name == "PreparedPatch").Methods.Single(x => x.Name == "Prefix"), "LegacyPreparedLimit") &&
            Calls(types.Single(x => x.Name == "ComputeMaxPreparedSpells_Patch").Methods.Single(x => x.Name == "Prefix"), "EffectiveLevel", "ClericProgressionContext"), "Cleric metadata: ordinary WIS preparation and existing2024 table both use effective class level");
        check(Types(rules).SelectMany(x => x.Methods).Any(x => Calls(x, "DivineSparkDice", "ClericProgressionRules")) &&
            rules.NestedTypes.Single(x => x.Name == "ModifyEffectDescriptionPowerTurnUndead").Methods.Any(x => Calls(x, "EffectiveLevel", "ClericProgressionContext")), "Cleric metadata: direct Spark heal and pre5 Sear thresholds adapted");
        var searHandler = rules.NestedTypes.Single(x => x.Name == "ModifyEffectDescriptionPowerTurnUndead").Methods.Single(x => x.Name == "GetEffectDescription");
        check(Calls(searHandler, "FirstOrDefault") && Calls(searHandler, "get_DamageType") &&
            Calls(game.MainModule.Types.Single(x => x.Name == "EffectDescription").Methods.Single(x => x.Name == "Copy"), "GetCopy", "EffectForm"),
            "Cleric metadata: inactive/pre5 Sear removes the cloned damage form, not only the unmatched static blueprint instance");
        check(Types(types.Single(x => x.Name == "ApplyRest_Patch")).SelectMany(x => x.Methods).Count(x => Calls(x, "PreserveChannelUsesOnShortRest")) == 2,
            "Cleric metadata: short-rest prefix/postfix share the scoped recovery gate without Paladin2024 leaking into legacy Cleric");
        check(Types(domains).SelectMany(x => x.Methods).Count(x => Calls(x, "EffectiveLevel", "ClericProgressionContext")) == 4, "Cleric metadata: Law callbacks, Mischief temp HP and Oblivion healing use effective rank");
        var native = types.Single(x => x.Name == "ClericNativeProgressionPatches");
        check(Calls(context.Methods.Single(x => x.Name == "LateLoad"), "IsReady", native.Name) && Calls(native.Methods.Single(x => x.Name == "IsReady"), "GetPatchInfo"), "Cleric metadata: native intervention binding/IL readiness required before activation");
        var chance = game.MainModule.Types.Single(x => x.Name == "CharacterActionUsePower").NestedTypes.Single(x => x.Name.StartsWith("<CheckExecutionFailure>"));
        var iterator = chance.Methods.Single(x => x.Name == "MoveNext");
        check(chance.Fields.Count(x => x.FieldType.Name == "CharacterActionUsePower") == 1 && iterator.Body.Instructions.Count(i => i.Operand as string == "CharacterLevel" && i.Next.Operand is MethodReference m && m.Name == "TryGetAttributeValue") == 1,
            "Cleric metadata: actual native iterator has exactly one captured action and level read targeted by transpiler");
        check(new[] { "Cleric", "Paladin", "Wizard" }.All(kind => Calls(native.Methods.Single(x => x.Name == "InterventionLevel"), "get_PowerClericDivineIntervention" + kind)), "Cleric metadata: chance correction scoped to3 exact installed intervention definitions");
        foreach (var kind in new[] { "Cleric", "Paladin", "Wizard" })
        {
            var j = Read("PowerClericDivineIntervention" + kind);
            check(j.GetProperty("hasCastingFailure").GetBoolean() && j.GetProperty("rechargeRate").GetString() == "LongRest" && j.GetProperty("fixedUsesPerRecharge").GetInt32() == 1,
                "Cleric: native Intervention" + kind + " retains failure, one long-rest use and summon effects");
        }
        var order = builders.Single(x => x.Name == "DomainOrder").Methods.Single(x => x.IsConstructor && !x.IsStatic);
        check(!Calls(order, "get_PowerClericDivineInterventionPaladin") && order.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "PowerClericDivineInterventionImprovementPaladin"), "Cleric metadata: known upstream Order omission of legacy10 Intervention remains explicit");
        var helper = types.Single(x => x.Name == "MulticlassInOutRulesHelper");
        check(Types(helper).SelectMany(x => x.Methods).Any(x => new[] { "Wizard", "Sorcerer", "Warlock", "Bard", "Cleric" }.All(c => Calls(x, "CanSelectClass", c + "SpellcastingContext"))) && Calls(context.Methods.Single(x => x.Name == "CanContinue"), "EnumerateHeroAllowedClassDefinitions"), "Cleric metadata: all prior caster guards retained with explicit cap deadlock and unchanged ability rules");
        check(Calls(types.Single(x => x.Name == "SharedSpellsContext").Methods.Single(x => x.Name == "GetSharedCasterLevel"), "SingleCasterLevel", casting.Name) &&
            Calls(types.Single(x => x.Name == "ModifyPowerPoolAmountPowerSpellPoints").Methods.Single(x => x.Name == "GetCasterLevel"), "IsSupportedHero", casting.Name), "Cleric metadata: own Spell Points/reaction caster level, no mixed-caster double count");
        check(Calls(types.Single(x => x.Name == "FinalizeCharacter_Patch").Methods.Single(x => x.Name == "Prefix"), "BeginFinalize", casting.Name) &&
            Calls(casting.NestedTypes.Single(x => x.Name == "GrantChoicesPatch").Methods.Single(x => x.Name == "Prefix"), "Grant"), "Cleric metadata: validate before hero mutation and once-only tagged bonus spell grants");
        foreach (var owner in new[] { context, casting })
        foreach (var patch in owner.NestedTypes)
        {
            var attribute = patch.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "HarmonyPatch"); if (attribute == null) continue;
            var targetType = (TypeReference)attribute.ConstructorArguments[0].Value; var methodName = (string)attribute.ConstructorArguments[1].Value;
            var target = game.MainModule.Types.FirstOrDefault(x => x.FullName == targetType.FullName) ?? types.Single(x => x.FullName == targetType.FullName);
            var methods = target.Methods.Where(x => x.Name == methodName || x.Name == "get_" + methodName).ToArray();
            check(methods.Length > 0, "Cleric metadata: Harmony target: " + patch.Name);
            foreach (var parameter in patch.Methods.Where(x => x.Name is "Prefix" or "Postfix").SelectMany(x => x.Parameters).Where(x => !x.Name.StartsWith("__")))
                check(methods.Any(x => x.Parameters.Any(p => p.Name == parameter.Name)), "Cleric metadata: Harmony argument: " + patch.Name + "." + parameter.Name);
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
