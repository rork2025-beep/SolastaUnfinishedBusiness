using System;
using System.IO;
using System.Text.Json;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Models;

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}
var features = Enumerable.Range(1, 20).Select(i => (Feature: "Feature" + i, Level: i)).ToArray();
var plan = new FighterProgressionPlan<string>(features);
Check(plan.Events.Count == 20 && plan.Events.Select(x => x.Feature).SequenceEqual(features.Select(x => x.Feature)),
    "all intermediate unlocks survive in original order");
Check(Enumerable.Range(1, 12).Select(FighterProgressionPlan<int>.EffectiveLevel)
    .SequenceEqual(new[] { 1,2,3,4,5,7,9,11,13,15,17,20 }), "effective-level mapping");
Check(plan.Events.All(x => x.Level >= 1 && x.Level <= 12) && plan.Events.Last().Level == 12,
    "no unlock beyond class cap");
var rewards = new[] { ("ASI", 19), ("Feat", 18), ("ASI", 16), ("Feat", 14), ("Feat", 18) };
var repeated = new FighterProgressionPlan<string>(rewards);
Check(repeated.Events.Count(x => x.Feature == "Feat") == 3 && repeated.Events.Count(x => x.Feature == "ASI") == 2,
    "repeated reward acquisition counts");
Check(repeated.Events.Select(x => x.OriginalIndex).Distinct().Count() == rewards.Length,
    "each acquisition retains its own origin");
var replacements = new FighterProgressionPlan<string>(new[] { ("Surge2",17), ("Surge1",2), ("Capstone",20), ("BeforeCapstone",18) });
Check(replacements.Events.Select(x => x.Feature).SequenceEqual(new[]{"Surge1","Surge2","BeforeCapstone","Capstone"}),
    "replacement order including merged level 12");
Check(repeated.Events.Where(x => x.OriginalLevel == 18).Select(x => x.OriginalIndex).SequenceEqual(new[]{1,4}),
    "stable source order for equal original levels");
var disabled = new FighterProgressionInitialization();
var writes = 0;
if (disabled.ShouldInitialize(false)) writes++;
if (disabled.ShouldInitialize(true)) writes++;
Check(writes == 0 && features[19].Level == 20, "disabled initialization performs no transformation and remains latched");
var enabled = new FighterProgressionInitialization();
for (var i = 0; i < 3; i++) if (enabled.ShouldInitialize(true)) writes++;
Check(writes == 1, "repeated initialization transforms exactly once");
Check(features.Select(x => x.Level).SequenceEqual(Enumerable.Range(1,20)), "planning does not mutate source definitions");
var rejected = false;
try { _ = new FighterProgressionPlan<int>(new[]{(1,21)}); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "unknown original levels rejected before transformation");
foreach (var fixture in new[] { "CharacterClassDefinition/Fighter", "CharacterSubclassDefinition/MartialChampion" })
{
    using var json = JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/" + fixture + ".json"));
    var source = json.RootElement.GetProperty("featureUnlocks").EnumerateArray()
        .Select(x => (Feature: x.GetProperty("featureDefinition").GetString(), Level: x.GetProperty("level").GetInt32()))
        .ToArray();
    var actualPlan = new FighterProgressionPlan<string>(source);
    Check(actualPlan.Events.Count == source.Length && actualPlan.Events.Select(x => x.OriginalIndex)
        .OrderBy(x => x).SequenceEqual(Enumerable.Range(0, source.Length)),
        "complete official blueprint acquisition inventory: " + fixture);
}

// Check Harmony bindings against the supplied game assembly WITHOUT loading Unity or running patches.
using var game = AssemblyDefinition.ReadAssembly("SolastaFiles/Solasta_Data/Managed/Assembly-CSharp.dll");
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory("SolastaFiles/Solasta_Data/Managed/UnityModManager");
resolver.AddSearchDirectory("SolastaFiles/Solasta_Data/Managed");
resolver.AddSearchDirectory("lib");
using var mod = AssemblyDefinition.ReadAssembly("SolastaFiles/Mods/SolastaUnfinishedBusiness/SolastaUnfinishedBusiness.dll",
    new ReaderParameters { AssemblyResolver = resolver });
var context = mod.MainModule.Types.Single(x => x.Name == "FighterProgressionContext");
var bindings = 0;
foreach (var patch in context.NestedTypes)
{
    var attr = patch.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "HarmonyPatch");
    if (attr == null) continue;
    var targetType = (TypeReference)attr.ConstructorArguments[0].Value;
    var name = (string)attr.ConstructorArguments[1].Value;
    var target = game.MainModule.Types.Single(x => x.FullName == targetType.FullName);
    var methods = target.Methods.Where(x => x.Name == name || x.Name == "get_" + name).ToArray();
    Check(methods.Length > 0, "Harmony target exists: " + patch.Name);
    foreach (var hook in patch.Methods.Where(x => x.Name is "Prefix" or "Postfix"))
    {
        foreach (var parameter in hook.Parameters.Where(x => !x.Name.StartsWith("__")))
        {
            Check(methods.Any(m => m.Parameters.Any(p => p.Name == parameter.Name)),
                "Harmony argument binding: " + patch.Name + "." + parameter.Name);
        }
    }
    bindings++;
}
Check(bindings == 7, "all prototype Harmony patch targets checked against actual game metadata");
BarbarianTests.Run(Check, game, mod);
MonkTests.Run(Check, game, mod);
RogueTests.Run(Check, game, mod);
WizardTests.Run(Check, game, mod);
SorcererTests.Run(Check, game, mod);
WarlockTests.Run(Check, game, mod);
WarlockInvocationTransactionTests.Run(Check, game, mod);
BardTests.Run(Check, game, mod);
ClericTests.Run(Check, game, mod);
DruidTests.Run(Check, game, mod);
PaladinTests.Run(Check, game, mod);
RangerTests.Run(Check, game, mod);
InventorTests.Run(Check, game, mod);
CompressedMulticasterTests.Run(Check, mod);
StandardCasterResourceTests.Run(Check, mod);
MultipleSpellRepertoiresTests.Run(Check, mod);
Console.WriteLine($"{count} checks passed; these are game-independent planner tests, not gameplay tests.");
