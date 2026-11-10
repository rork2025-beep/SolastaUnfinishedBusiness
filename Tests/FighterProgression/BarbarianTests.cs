using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;

internal static class BarbarianTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition type) =>
        new[] { type }.Concat(type.NestedTypes.SelectMany(Types));

    internal static void Run(Action<bool, string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var allTypes = mod.MainModule.Types.SelectMany(Types).ToArray();
        var context = allTypes.Single(x => x.Name == "BarbarianProgressionContext");
        var builders = mod.MainModule.Types.Where(x => x.BaseType?.Name == "AbstractSubclass" &&
            x.Methods.Any(m => m.Name == "get_Klass" && Calls(m, "get_Barbarian"))).ToArray();
        check(builders.Length == 10, "all ten registered UB Barbarian builders discovered from compiled class ownership");
        var official = new[] { "PathBerserker", "PathClaw", "PathMagebane", "PathStone" };
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>();
        sources.Add(("Barbarian", LoadBlueprint("CharacterClassDefinition/Barbarian")));
        sources.AddRange(official.Select(x => (x, LoadBlueprint("CharacterSubclassDefinition/" + x))));
        sources.AddRange(builders.Select(x => (x.Name, LoadBlueprint("CharacterSubclassDefinition/" + x.Name, "UnfinishedBusinessBlueprints"))));
        foreach (var source in sources)
        {
            var original = source.Unlocks.Select(x => (x.FeatureDefinition, x.Level)).ToArray();
            var definitions = new CompressedProgressionDefinitions("Test" + source.Name);
            var transformed = definitions.Prepare(source.Unlocks, source.Name);
            var expected = source.Unlocks.OrderBy(x => x.Level).Select(x => x.FeatureDefinition.Name).ToArray();
            var actual = transformed.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
                .Where(x => x is not FeatureDefinitionFeatureSet).Select(x => x.Name).ToArray();
            check(expected.SequenceEqual(actual), "real transformer preserves complete blueprint order: " + source.Name);
            check(source.Unlocks.Select(x => (x.FeatureDefinition, x.Level)).SequenceEqual(original),
                "real transformer does not modify source lists: " + source.Name);
            check(transformed.All(x => x.Level <= 12), "compressed blueprint class cap: " + source.Name);
        }

        var level20Loader = allTypes.Single(x => x.Name == "Level20Context").Methods.Single(x => x.Name == "BarbarianLoad");
        check(new[]{17,18,19,20}.All(n => level20Loader.Body.Instructions.Any(i => Integer(i) == n)),
            "compiled UB Barbarian loader retains original 17-20 extension events");
        var complete = LoadBlueprint("CharacterClassDefinition/Barbarian");
        complete.Add(new(new FeatureDefinition { Name="IndomitableMight" },18));
        complete.Add(new(new FeatureDefinition { Name="ASI19" },19));
        complete.Add(new(new FeatureDefinition { Name="PrimalChampion20" },20));
        var completeOutput = new CompressedProgressionDefinitions("TestBarbarian20").Prepare(complete,"Barbarian");
        var finalFeatures = completeOutput.Where(x => x.Level == 12)
            .SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Select(x => x.Name).ToArray();
        check(finalFeatures.Contains("IndomitableMight") && finalFeatures.Contains("ASI19") && finalFeatures.Contains("PrimalChampion20"),
            "full Barbarian extension fixture grants every original 18-20 reward by actual 12");

        // Same identity repeated twice at the merged level; ASI contains another feat of the SAME pool type.
        var feat = new FeatureDefinitionPointPool { Name = "BonusFeat", PoolType = "Feat", PoolAmount = 1 };
        var asi = new FeatureDefinitionFeatureSet { Name = "ASIAndFeat", Mode = FeatureDefinitionFeatureSet.FeatureSetMode.Union };
        asi.FeatureSet.Add(new FeatureDefinitionPointPool { Name = "ASI", PoolType = "AbilityScore", PoolAmount = 2 });
        asi.FeatureSet.Add(feat);
        var poolBehavior = new object();
        var setBehavior = new object();
        feat.SubFeatures.Add(poolBehavior);
        asi.SubFeatures.Add(setBehavior);
        var oldPower = new FeatureDefinitionPower { Name = "OldPower" };
        var newPower = new FeatureDefinitionPower { Name = "NewPower", OverriddenPower = oldPower };
        var rewards = new List<FeatureUnlockByLevel>
        {
            new(newPower,20), new(asi,19), new(feat,18), new(feat,18), new(oldPower,15)
        };
        var registry = new CompressedProgressionDefinitions("TestRepeated");
        var output = registry.Prepare(rewards,"Barbarian");
        var pools = output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionPointPool>().ToArray();
        check(pools.Where(x => x.PoolType == "Feat").Sum(x => x.PoolAmount) == 3,
            "real transformer preserves all repeated and ASI feat reward amounts");
        check(pools.Where(x => x.PoolType == "AbilityScore").Sum(x => x.PoolAmount) == 2,
            "real transformer preserves ASI amount");
        check(pools.Select(x => registry.PoolOrigins[x]).Distinct().Count() == pools.Length,
            "every merged choice pool has distinct acquisition provenance");
        check(pools.Select(x => x.Name).Distinct().Count() == pools.Length,
            "merged rewards have distinct definition identities");
        var powers = output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionPower>().ToArray();
        check(powers.SequenceEqual(new[] { oldPower, newPower }) && newPower.OverriddenPower == oldPower,
            "real transformer preserves replacement order and original override references");
        check(pools.All(x => x.PoolType != "Feat" || x.SubFeatures.Contains(poolBehavior)) &&
              output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
                  .OfType<FeatureDefinitionFeatureSet>().Any(x => x.SubFeatures.Contains(setBehavior)),
            "cloned choice pools and sets retain attached runtime behavior");
        check(asi.FeatureSet[1] == feat && feat.Name == "BonusFeat", "nested source sets/pools remain unchanged");
        check(Enumerable.Range(0,21).All(x => BarbarianProgressionRules.EffectiveLevel(false,x) == x),
            "disabled Barbarian scaling is identical at every original level");
        check(BarbarianProgressionRules.EffectiveLevel(true,13) == 13,
            "unsupported existing Barbarian above 12 is not silently remapped");
        check(!BarbarianProgressionRules.IsCapped(true,11) && BarbarianProgressionRules.IsCapped(true,12) &&
              BarbarianProgressionRules.IsCapped(true,13) && !BarbarianProgressionRules.IsCapped(false,12),
            "Barbarian cap only active in compressed mode");
        check(BarbarianProgressionRules.BrutalStrikeChoices(BarbarianProgressionRules.EffectiveLevel(true,10)) == 1 &&
              BarbarianProgressionRules.BrutalStrikeChoices(BarbarianProgressionRules.EffectiveLevel(true,11)) == 2,
            "Brutal Strike second selection at compressed 11 (original 17)");

        // Fixture for the specified ASI+feat + both extra-feat options (not a claim of executing UB startup).
        var barbarianRewards = new List<FeatureUnlockByLevel>();
        foreach (var level in new[] {4,8,12,16,19}) barbarianRewards.Add(new(asi,level));
        foreach (var level in new[] {2,6,10,14,18}) barbarianRewards.Add(new(feat,level));
        var rewardsRegistry = new CompressedProgressionDefinitions("TestBarbarianRewards");
        var rewardOutput = rewardsRegistry.Prepare(barbarianRewards,"Barbarian");
        var rewardPools = rewardOutput.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionPointPool>().ToArray();
        check(rewardPools.Where(x => x.PoolType == "Feat").Sum(x => x.PoolAmount) == 10 &&
              rewardPools.Where(x => x.PoolType == "AbilityScore").Sum(x => x.PoolAmount) == 10,
            "specified Barbarian feat configuration retains five ASIs and ten class feat points");
        var session = new FighterProgressionInitialization();
        var transformations = 0;
        var live = new List<FeatureUnlockByLevel> { new(feat,18), new(asi,19), new(newPower,20) };
        for (var run = 0; run < 3; run++)
        {
            if (!session.ShouldInitialize(true)) continue;
            live = new CompressedProgressionDefinitions("TestSession").Prepare(live,"Barbarian");
            transformations++;
        }
        check(transformations == 1 && live.Count == 1 && live[0].Level == 12,
            "real definition transformation runs once across repeated initialization");
        var offSession = new FighterProgressionInitialization();
        var untouched = new List<FeatureUnlockByLevel> { new(feat,18) };
        if (offSession.ShouldInitialize(false)) untouched = new CompressedProgressionDefinitions("Off").Prepare(untouched,"Barbarian");
        check(untouched.Count == 1 && untouched[0].Level == 18 && untouched[0].FeatureDefinition == feat,
            "disabled definition session preserves actual input references and levels");

        var grant = new PrimalChampionGrantState();
        check(grant.Apply("Barbarian12") == 4 && grant.Apply("Barbarian12") == 0,
            "Primal Champion maximum adjustment applies once");
        check(grant.Remove("Barbarian12") == -4 && grant.Remove("Barbarian12") == 0 && grant.Apply("Barbarian12") == 4,
            "Primal Champion remove/undo/regrant is reversible and idempotent");
        var loaded = new PrimalChampionGrantState();
        check(loaded.Remove("Barbarian12") == -4 && loaded.Remove("Barbarian12") == 0,
            "loaded champion removal subtracts maximum once");
        check(new PrimalChampionGrantState().Apply("Barbarian12") == 4,
            "new respec hero owns an independent grant state");
        var maxMethod = allTypes.Single(x => x.Name == "PrimalChampionMaximum").Methods.Single(x => x.Name == "ChangeMaximum");
        check(Calls(maxMethod,"set_MaxValue") && Calls(maxMethod,"set_MaxEditableValue") &&
              !Calls(maxMethod,"set_BaseValue") && !Calls(maxMethod,"ModifyAttributeAndMax") &&
              !maxMethod.Body.Instructions.Any(x => x.Operand is FieldReference f && f.Name == "baseValue"),
            "Primal Champion callback adjusts maxima without writing BaseValue");
        var buildChampion = context.Methods.Single(x => x.Name == "BuildPrimalChampion");
        check(Calls(buildChampion,"SetModifier") && buildChampion.Body.Instructions.Any(x => x.Operand as string == "Strength") &&
              buildChampion.Body.Instructions.Any(x => x.Operand as string == "Constitution"),
            "compressed champion registers removable Strength/Constitution modifiers");

        CheckRageMetadata(check);
        check(allTypes.SelectMany(x => x.Methods).Any(m => Calls(m,"get_EnableBarbarianRage2024") &&
              Calls(m,"get_UsedRagePoints") && Calls(m,"set_UsedRagePoints")),
            "2024 short-rest Rage recovery path remains present");
        var discovery = context.Methods.Single(x => x.Name == "DiscoverSubclasses");
        check(Calls(discovery,"get_AllSubClasses") && Calls(discovery,"GetDatabase"),
            "runtime discovery uses class registration AND actual game definitions");
        check(context.NestedTypes.SelectMany(x => x.Methods).Any(x => Calls(x,"get_Hidden")) ||
              context.Methods.Any(x => Calls(x,"get_Hidden")),
            "discovery filters hidden definitions rather than enabling subclasses");
        var switch2024 = allTypes.Single(x => x.Name == "Tabletop2024Context").Methods.Single(x => x.Name == "SwitchBarbarianBrutalStrike");
        check(Calls(switch2024,"RemoveAll") && Calls(switch2024,"get_EnableBarbarianBrutalStrike2024") &&
              new[]{9,13,17}.All(n => switch2024.Body.Instructions.Any(i => Integer(i) == n)),
            "2024 replacement retains original 9/13/17 thresholds before compression");
        var startup = allTypes.SelectMany(x => x.Methods).Single(x => Calls(x,"LateLoad","BarbarianProgressionContext"));
        var order = startup.Body.Instructions.Select(x => x.Operand).OfType<MethodReference>().ToArray();
        int Index(string type) => Array.FindIndex(order,x => x.DeclaringType.Name == type && x.Name == "LateLoad");
        check(Index("BarbarianProgressionContext") > Index("Tabletop2024Context") &&
              Index("BarbarianProgressionContext") > Index("SpellPointsContext") &&
              Index("BarbarianProgressionContext") > Index("FighterProgressionContext"),
            "Barbarian transformation runs after final 2024, spell points, and Fighter startup");
        var capFilter = allTypes.Single(x => x.Name == "MulticlassInOutRulesHelper");
        check(capFilter.NestedTypes.Concat(new[]{capFilter}).SelectMany(x => x.Methods)
            .Any(x => Calls(x,"IsCapped","FighterProgressionContext") && Calls(x,"IsCapped","BarbarianProgressionContext")),
            "multiclass filtering composes both independent class caps");
        CheckBindings(check, context, game);
    }

    private static List<FeatureUnlockByLevel> LoadBlueprint(string name, string directory = "OfficialBlueprints")
    {
        using var doc = JsonDocument.Parse(File.ReadAllText("Diagnostics/" + directory + "/" + name + ".json"));
        var identities = new Dictionary<string,FeatureDefinition>();
        return doc.RootElement.GetProperty("featureUnlocks").EnumerateArray().Select(x =>
        {
            var id = x.GetProperty("featureDefinition").GetString().Split(':')[1];
            if (!identities.TryGetValue(id,out var feature)) identities[id] = feature = new FeatureDefinition { Name=id };
            return new FeatureUnlockByLevel(feature,x.GetProperty("level").GetInt32());
        }).ToList();
    }

    private static void CheckRageMetadata(Action<bool,string> check)
    {
        using var source = JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/CharacterClassDefinition/Barbarian.json"));
        int Sum(string attribute, int actual)
        {
            var effective = BarbarianProgressionRules.EffectiveLevel(true,actual);
            return source.RootElement.GetProperty("featureUnlocks").EnumerateArray().Where(x =>
                x.GetProperty("level").GetInt32() <= effective && x.GetProperty("featureDefinition").GetString().Contains(attribute)).Count();
        }
        check(2 + Sum("AttributeModifierBarbarianRagePointsAdd",11) == 6 &&
              2 + Sum("AttributeModifierBarbarianRageDamageAdd",11) == 4,
            "Rage metadata reaches six uses and +4 damage at compressed 11");
        using var power = JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionPower/PowerBarbarianRageStart.json"));
        check(power.RootElement.GetProperty("rechargeRate").GetString() == "RagePoints",
            "Rage action preserves RagePoints resource binding");
        check(FighterProgressionPlan<int>.CompressedLevel(15) == 10 && FighterProgressionPlan<int>.CompressedLevel(11) == 8,
            "Persistent Rage and Relentless Rage compressed thresholds");
    }

    private static bool Calls(MethodDefinition m, string name, string owner = null) => m.HasBody &&
        m.Body.Instructions.Any(x => x.Operand is MethodReference r && r.Name == name && (owner == null || r.DeclaringType.Name == owner));
    private static int? Integer(Mono.Cecil.Cil.Instruction i) => i.OpCode.Code switch
    {
        Mono.Cecil.Cil.Code.Ldc_I4 => (int)i.Operand,
        Mono.Cecil.Cil.Code.Ldc_I4_S => (sbyte)i.Operand,
        _ => null
    };
    private static void CheckBindings(Action<bool,string> check, TypeDefinition context, AssemblyDefinition game)
    {
        foreach (var patch in context.NestedTypes)
        {
            var attr = patch.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "HarmonyPatch");
            if (attr == null) continue;
            var type = (TypeReference)attr.ConstructorArguments[0].Value;
            var name = (string)attr.ConstructorArguments[1].Value;
            var methods = game.MainModule.Types.Single(x => x.FullName == type.FullName).Methods
                .Where(x => x.Name == name || x.Name == "get_" + name).ToArray();
            check(methods.Length > 0, "Barbarian Harmony target exists: " + patch.Name);
            foreach (var parameter in patch.Methods.Where(x => x.Name is "Prefix" or "Postfix").SelectMany(x => x.Parameters)
                         .Where(x => !x.Name.StartsWith("__")))
                check(methods.Any(m => m.Parameters.Any(p => p.Name == parameter.Name)),
                    "Barbarian Harmony argument: " + patch.Name + "." + parameter.Name);
        }
    }
}
