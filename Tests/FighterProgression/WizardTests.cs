using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;

// Executes the production planner/transformer with managed doubles and reads actual compiled tables.
// Binding checks inspect metadata only: no Harmony invocation, Unity UI, save or spellbook execution.
internal static class WizardTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m, string name, string owner = null) => m.HasBody &&
        m.Body.Instructions.Any(x => x.Operand is MethodReference r && r.Name == name &&
            (owner == null || r.DeclaringType.Name == owner));

    internal static void Run(Action<bool, string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        var context = types.Single(x => x.Name == "WizardProgressionContext");
        var casting = types.Single(x => x.Name == "WizardSpellcastingContext");
        var builders = types.Where(x => x.BaseType?.Name == "AbstractSubclass" &&
            x.Methods.Any(m => m.Name == "get_Klass" && Calls(m, "get_Wizard"))).ToArray();
        var expected = new[] { "WizardAbjuration", "WizardArcaneFighter", "WizardBladeDancer", "WizardDeadMaster",
            "WizardEvocation", "WizardGraviturgist", "WizardSpellMaster", "WizardWarMagic" };
        check(builders.Select(x => x.Name).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)),
            "Wizard: eight UB builders discovered by compiled class ownership");
        var fixtures = new List<(string Name, string Folder)> { ("Wizard", "OfficialBlueprints") };
        fixtures.AddRange(new[] { "TraditionCourtMage", "TraditionGreenmage", "TraditionLoremaster", "TraditionShockArcanist" }
            .Select(x => (x, "OfficialBlueprints")));
        fixtures.AddRange(builders.Select(x => (x.Name, "UnfinishedBusinessBlueprints")));
        foreach (var (name, folder) in fixtures)
        {
            using var j = JsonDocument.Parse(File.ReadAllText("Diagnostics/" + folder + "/" +
                (name == "Wizard" ? "CharacterClassDefinition/" : "CharacterSubclassDefinition/") + name + ".json"));
            var source = j.RootElement.GetProperty("featureUnlocks").EnumerateArray().Select(x => new FeatureUnlockByLevel(
                new FeatureDefinition { Name = x.GetProperty("featureDefinition").GetString() }, x.GetProperty("level").GetInt32())).ToList();
            var before = source.Select(x => (x.FeatureDefinition, x.Level)).ToArray();
            var output = new CompressedProgressionDefinitions("TestWizard" + name).Prepare(source, name);
            check(output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
                    .Where(x => x is not FeatureDefinitionFeatureSet).SequenceEqual(source.OrderBy(x => x.Level).Select(x => x.FeatureDefinition)),
                "Wizard: all fixture events and replacement order preserved: " + name);
            check(source.Select(x => (x.FeatureDefinition, x.Level)).SequenceEqual(before) && output.All(x => x.Level <= 12),
                "Wizard: unchanged fixture source and compressed cap: " + name);
        }
        CheckSpellcasting(check, types);
        CheckRewards(check);
        check(Enumerable.Range(0, 21).All(x => WizardProgressionRules.EffectiveLevel(false, x) == x) &&
            WizardProgressionRules.EffectiveLevel(true, 13) == 13, "Wizard: disabled and unsupported existing levels unchanged");
        check(!WizardProgressionRules.IsCapped(true, 11) && WizardProgressionRules.IsCapped(true, 12) &&
            WizardProgressionRules.IsCapped(true, 13) && !WizardProgressionRules.IsCapped(false, 12), "Wizard: opt-in class cap");
        var startup = types.SelectMany(x => x.Methods).Single(x => Calls(x, "LateLoad", "WizardProgressionContext"));
        int Index(string owner) => startup.Body.Instructions.Select((x, i) => (x, i)).Single(x =>
            x.x.Operand is MethodReference m && m.Name == "LateLoad" && m.DeclaringType.Name == owner).i;
        check(Index("WizardProgressionContext") > Index("Tabletop2024Context") &&
            Index("WizardProgressionContext") > Index("SpellPointsContext") && Index("WizardProgressionContext") > Index("RogueProgressionContext"),
            "Wizard: bootstrap follows final UB/2024/spell-points and prior class transformations (metadata)");
        check(Calls(context.Methods.Single(x => x.Name == "DiscoverSubclasses"), "get_KlassListSubclassEnabled"),
            "Wizard: registered subclass discovery honors enabled choices instead of all registered definitions (metadata)");
        var late = context.Methods.Single(x => x.Name == "LateLoad");
        check(Calls(late, "ShouldInitialize") && Calls(late, "get_EnableWizardCompressedProgressionPrototype"),
            "Wizard: activation uses existing opt-in session latch (metadata)");
        var settingsCtor = types.Single(x => x.FullName == "SolastaUnfinishedBusiness.Settings").Methods.Single(x => x.IsConstructor && !x.IsStatic);
        check(!settingsCtor.Body.Instructions.Any(x => x.Operand is FieldReference f && f.Name.Contains("EnableWizardCompressedProgressionPrototype")),
            "Wizard: setting has CLR false default (metadata)");
        var level20 = types.Single(x => x.Name == "Level20Context");
        check(new[] { 18, 19, 20 }.All(n => level20.Methods.Single(x => x.Name == "WizardLoad").Body.Instructions.Any(i => Integer(i) == n)),
            "Wizard: compiled original 18/19/20 additions retained before compression");
        foreach (var type in new[] { "WizardSpellMastery", "WizardSignatureSpells" })
            check(types.Single(x => x.Name == type).Methods.Any(x => Calls(x, "EffectiveLevel", "WizardProgressionContext")),
                "Wizard: effective rest threshold for " + type + " (metadata)");
        var tabletop = types.Single(x => x.Name == "Tabletop2024Context");
        foreach (var pair in new[] { ("SwitchWizardSchoolOfMagicLearningLevel", "get_EnableWizardToLearnSchoolAtLevel3"),
                     ("SwitchWizardScholar", "get_EnableWizardToLearnScholarAtLevel2") })
            check(Calls(tabletop.Methods.Single(x => x.Name == pair.Item1), pair.Item2), "Wizard: original 2024 option retained: " + pair.Item1);
        check(Calls(tabletop.Methods.Single(x => x.Name == "IsRestActivityMemorizeSpellAvailable"), "EffectiveLevel", "WizardProgressionContext"),
            "Wizard: Memorize uses scoped level; option remains authoritative (metadata)");
        check(casting.Methods.Any(x => Calls(x, "SetPreparedSpellGroups")) &&
            Types(casting).SelectMany(x => x.Methods).Any(x => Calls(x, "CompressedLevel")),
            "Wizard: automatic preparation groups are adapted by cloned definitions (metadata)");
        var recovery = types.Where(x => x.Name == "ApplySpellSlotsForm_Patch").SelectMany(x => x.Methods).ToArray();
        check(recovery.Any(x => Calls(x, "EffectiveLevel", "WizardProgressionContext")), "Wizard: Arcane Recovery effective budget bound in manager patch (metadata)");
        check(types.Single(x => x.Name == "ModifyPowerPoolAmount").Methods.Any(x => Calls(x, "EffectiveLevel", "WizardProgressionContext")),
            "Wizard: ward class-level pool uses scoped level (metadata)");
        foreach (var name in new[] { "WizardAbjuration", "WizardWarMagic" })
            check(Types(types.Single(x => x.Name == name)).SelectMany(x => x.Methods).Any(x =>
                Calls(x, "EffectiveLevel", "WizardProgressionContext") || Calls(x, "EffectiveSubclassLevel", "WizardProgressionContext")),
                "Wizard: direct subclass thresholds adapted: " + name + " (metadata)");
        var eligibility = types.Single(x => x.Name == "MulticlassInOutRulesHelper");
        check(Types(eligibility).SelectMany(x => x.Methods).Any(x => Calls(x, "IsCapped", "WizardProgressionContext") &&
                Calls(x, "CanSelectClass", "WizardSpellcastingContext")) && eligibility.Methods.Any(x => x.Name == "ApproveMultiClassInOut"),
            "Wizard: class cap and casting boundary compose with native attribute eligibility (metadata)");
        check(Calls(casting.Methods.Single(x => x.Name == "IsSupportedHero"), "IsMulticaster", "SharedSpellsContext") &&
            casting.Methods.Single(x => x.Name == "CanSelectClass").HasBody,
            "Wizard: mixed repertoire guard exists; shared caster integration is pending (metadata)");
        check(WizardProgressionRules.EffectiveLevel(true, 12) == 20 && FighterProgressionPlan<int>.EffectiveLevel(8) == 11 &&
            WizardProgressionRules.EffectiveLevel(true, 8) == 11 && 12 + 8 == 20, "Wizard: 12/8 actual/effective level separation");
        foreach (var owner in new[] { context, casting })
        foreach (var patch in owner.NestedTypes)
        {
            var attr = patch.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "HarmonyPatch");
            if (attr == null) continue;
            var targetType = (TypeReference)attr.ConstructorArguments[0].Value;
            var name = (string)attr.ConstructorArguments[1].Value;
            var targets = game.MainModule.Types.Single(x => x.FullName == targetType.FullName).Methods
                .Where(x => x.Name == name || x.Name == "get_" + name).ToArray();
            check(targets.Length > 0, "Wizard: Harmony target exists: " + patch.Name);
            foreach (var param in patch.Methods.Where(x => x.Name is "Prefix" or "Postfix").SelectMany(x => x.Parameters).Where(x => !x.Name.StartsWith("__")))
                check(targets.Any(x => x.Parameters.Any(p => p.Name == param.Name)), "Wizard: Harmony argument binding: " + patch.Name + "." + param.Name);
        }
        check(Calls(casting.NestedTypes.Single(x => x.Name == "GrantLearningPatch").Methods.Single(x => x.Name == "Postfix"), "GrantCantripsAndSpellsByTag"),
            "Wizard: supplementary learning routes through native book grant (metadata, not persistence execution)");
        check(Calls(casting.NestedTypes.Single(x => x.Name == "GrantLearningPatch").Methods.Single(x => x.Name == "Postfix"), "get_AcquiredCantrips"),
            "Wizard: origin-tagged bonus cantrips included in native grant routing (metadata)");
        check(Calls(casting.NestedTypes.Single(x => x.Name == "ClearLearningPatch").Methods.Single(x => x.Name == "Prefix"), "UnacquireSpells"),
            "Wizard: cancellation clears supplementary acquisitions as well as pools (metadata)");
    }

    private static void CheckSpellcasting(Action<bool, string> check, TypeDefinition[] types)
    {
        using var j = JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpellWizard.json"));
        int[] Read(string name) => j.RootElement.GetProperty(name).EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var learn = new SpellcastingProgressionPlan(Read("scribedSpells"));
        check(Enumerable.Range(1, 12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).SequenceEqual(Enumerable.Range(1, 20)),
            "Wizard: twenty independent learning events appear exactly once in original order");
        check(Enumerable.Range(1, 12).Sum(a => SpellcastingProgressionPlan.OriginalLevels(a).Sum(o => learn.LearningAmount(o, 0))) == 44,
            "Wizard: book learning retains six initial plus nineteen pairs = 44 spells");
        check(learn.PrimaryScribedEvents().Take(12).Sum() == 28 && Enumerable.Range(1, 12)
            .SelectMany(a => SpellcastingProgressionPlan.OriginalLevels(a).Skip(1)).Sum(o => learn.LearningAmount(o, 0)) == 16,
            "Wizard: primary and supplemental events total 28+16; no cumulative-learning substitution");
        check(Enumerable.Range(1, 12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).Sum(o => learn.LearningAmount(o, o >= 2 ? 1 : 0)) == 63,
            "Wizard Spell Master: additional scribed spell per original event from original2 gives 63 with school-at2");
        check(Enumerable.Range(1, 12).SelectMany(SpellcastingProgressionPlan.OriginalLevels).Sum(o => learn.LearningAmount(o, o >= 3 ? 1 : 0)) == 62,
            "Wizard Spell Master: school-at3 variant grants62 book selections");
        var expectedCircles = new[] { 1,1,2,2,3,4,5,6,7,8,9,9 };
        check(Enumerable.Range(1, 12).Select(a => SpellcastingProgressionPlan.MaxSpellLevel(SpellcastingProgressionPlan.Effective(a)))
            .SequenceEqual(expectedCircles), "Wizard: maximum circles through actual1-12");
        check(SpellcastingProgressionPlan.OriginalLevels(6).Select(SpellcastingProgressionPlan.MaxSpellLevel).SequenceEqual(new[] { 3, 4 }) &&
            SpellcastingProgressionPlan.OriginalLevels(12).SequenceEqual(new[] { 18,19,20 }), "Wizard: original6/7 circles separated; final level has three events");
        var builder = types.Single(x => x.Name == "FeatureDefinitionCastSpellBuilder");
        var slotsFlat = ReadAddedIntegers(builder, "SlotsByCasterLevel");
        check(slotsFlat.Length == 210, "Wizard: read all 21x10 slot entries from actual compiled UB table");
        var slots = Enumerable.Range(1, 20).Select(n => slotsFlat.Skip(n * 10).Take(10).ToArray()).ToArray();
        var projected = SpellcastingProgressionPlan.ProjectRows(slots);
        for (var a = 1; a <= 12; a++)
        {
            check(projected[a-1].SequenceEqual(slots[SpellcastingProgressionPlan.Effective(a)-1]) &&
                Array.IndexOf(projected[a-1], 0) == expectedCircles[a-1], "Wizard: exact original slot row and circle at actual" + a);
        }
        check(projected[10][8] == 1 && projected[11].Take(9).SequenceEqual(new[] { 4,3,3,3,3,2,2,1,1 }),
            "Wizard: ninth-circle slot at actual11; original20 slot quantities at actual12");
        var pointRows = slots.Select(row => row.Select(x => x > 0 ? 1 : 0).ToArray()).ToArray();
        check(SpellcastingProgressionPlan.ProjectRows(pointRows)[11].SequenceEqual(new[] { 1,1,1,1,1,1,1,1,1,0 }),
            "Wizard: existing spell-points slot restrictions remain one per circle, including sixth-ninth");
        var points = ReadAddedIntegers(types.Single(x => x.Name == "ModifyPowerPoolAmountPowerSpellPoints"), "SpellPointsByLevel");
        check(points.Length == 21 && Enumerable.Range(1, 12).Select(a => points[SpellcastingProgressionPlan.CasterLevel(a)])
            .SequenceEqual(new[] { 4,6,14,17,27,38,57,73,83,94,107,133 }), "Wizard: actual compiled Spell Points table projects to133 at actual12");
        check(Enumerable.Range(-2, 25).All(a => SpellcastingProgressionPlan.CasterLevel(a) >= 0 &&
            SpellcastingProgressionPlan.CasterLevel(a) <= 20), "Wizard: scoped spell-point indices stay inside0-20");
        check(SpellcastingProgressionPlan.ProjectCumulative(Read("knownCantrips")).Take(12)
            .SequenceEqual(new[] { 3,3,3,4,4,4,4,5,5,5,5,5 }), "Wizard: cumulative cantrips retain actual4 and actual8 increases");
        check(SpellcastingProgressionPlan.ProjectCumulative(Read("knownSpells")).Count == 20 &&
            j.RootElement.GetProperty("spellKnowledge").GetString() == "Spellbook" && Read("replacedSpells").All(x => x == 0),
            "Wizard: book knowledge; no native replacement rewards invented");
        var prepared = ReadAddedIntegers(types.Single(x => x.Name == "ComputeMaxPreparedSpells_Patch"), "WizardOneDndPreparedSpellsTable");
        check(prepared.Length == 20 && SpellcastingProgressionPlan.ProjectCumulative(prepared)[11] == 25 &&
            SpellcastingProgressionPlan.Effective(12) + 5 == 25, "Wizard: 2024 table and ordinary INT+level both reach25 before affinity bonuses");
        check(Enumerable.Range(1, 12).Select(a => (SpellcastingProgressionPlan.Effective(a) + 1) / 2)
            .SequenceEqual(new[] { 1,1,2,2,3,4,5,6,7,8,9,10 }), "Wizard: original Arcane Recovery half-level budgets");
        check(SpellcastingProgressionPlan.LearningBudget(2, 1, 0) == 1 &&
            SpellcastingProgressionPlan.LearningBudget(2, 3, 2) == 1 &&
            SpellcastingProgressionPlan.LearningBudget(2, 2, 2) == 0 &&
            SpellcastingProgressionPlan.LearningBudget(2, 1, 3) == 0,
            "Wizard: exhausted spell list reserves earlier choices and never grants negative/unfillable supplementary budget");
        var books = new[] { (Owner: "Hero", Pages: 10), (Owner: "", Pages: 12) };
        check(SpellcastingProgressionPlan.CanFitSpellbooks(books, "Hero", new[] { 9, 9 }) && books[0].Pages == 10,
            "Wizard: book capacity simulation permits two books without mutating inventory");
        check(!SpellcastingProgressionPlan.CanFitSpellbooks(new[] { ("Hero", 1), ("", 1) }, "Hero", new[] { 2 }) &&
            !SpellcastingProgressionPlan.CanFitSpellbooks(books, "Hero", new[] { 9, 9, 9 }),
            "Wizard: fragmented/insufficient pages reject learning instead of native silent GrantSpell failure");
        check(SpellcastingProgressionPlan.CanFitSpellbooks(Array.Empty<(string,int)>(), "Hero", Array.Empty<int>()) &&
            !SpellcastingProgressionPlan.CanFitSpellbooks(Array.Empty<(string,int)>(), "Hero", new[] { 1 }),
            "Wizard: empty-book and empty-selection boundaries");
        var rejected = 0;
        foreach (var bad in new[] { 0,13 }) try { SpellcastingProgressionPlan.OriginalLevels(bad); } catch (ArgumentOutOfRangeException) { rejected++; }
        try { new SpellcastingProgressionPlan(new int[19]); } catch (ArgumentException) { rejected++; }
        try { SpellcastingProgressionPlan.ProjectRows(new int[19]); } catch (ArgumentException) { rejected++; }
        try { SpellcastingProgressionPlan.ProjectCumulative(Array.Empty<int>()); } catch (ArgumentException) { rejected++; }
        check(rejected == 5, "Wizard: invalid class levels and malformed tables rejected");
    }

    private static void CheckRewards(Action<bool, string> check)
    {
        var pool = new FeatureDefinitionPointPool { Name = "WizardASI", PoolType = "AbilityScore", PoolAmount = 2 };
        var source = new[] { 4,8,12,16,19 }.Select(n => new FeatureUnlockByLevel(pool,n)).ToList();
        source.Add(new(new FeatureDefinition { Name = "SpellMastery18" },18));
        source.Add(new(new FeatureDefinition { Name = "SignatureSpells20" },20));
        var latch = new FighterProgressionInitialization(); var writes = 0;
        List<FeatureUnlockByLevel> output = null;
        var transform = new CompressedProgressionDefinitions("WizardTestRewards");
        for (var i = 0; i < 3; i++) if (latch.ShouldInitialize(true)) { output = transform.Prepare(source,"Wizard"); writes++; }
        var pools = output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPointPool>().ToArray();
        check(writes == 1 && pools.Length == 5 && pools.Sum(x => x.PoolAmount) == 10, "Wizard: repeat initialization preserves five ASI events exactly once");
        var final = output.Where(x => x.Level == 12).SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .Where(x => x is not FeatureDefinitionFeatureSet).ToArray();
        check(final.Length == 3 && final[0].Name == "SpellMastery18" && final[1] is FeatureDefinitionPointPool && final[2].Name == "SignatureSpells20",
            "Wizard: final mastery/ASI/signature order retained with independent selection origin");
        var bonusCantrips = new List<FeatureUnlockByLevel> { new(new FeatureDefinitionPointPool {
            Name = "SpellMasterBonusCantrips", PoolType = "Cantrip", PoolAmount = 2 },6) };
        var bonusTransform = new CompressedProgressionDefinitions("WizardCantripReward");
        var bonusOutput = bonusTransform.Prepare(bonusCantrips, "WizardSpellMaster");
        var bonusPool = bonusOutput.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPointPool>().Single();
        check(bonusPool.PoolAmount == 2 && bonusTransform.PoolOrigins.ContainsKey(bonusPool),
            "Wizard Spell Master: separate original6 cantrip reward retains both choices and origin");
        var disabled = new FighterProgressionInitialization();
        check(!disabled.ShouldInitialize(false) && !disabled.ShouldInitialize(true) && source.Count == 7 && source.Last().Level == 20,
            "Wizard: disabled session leaves original definition events untouched");
        foreach (var schoolAt3 in new[] { false, true })
        foreach (var scholar in new[] { false, true })
        {
            var variant = new List<FeatureUnlockByLevel> { new(new FeatureDefinition { Name = "School" },schoolAt3 ? 3 : 2) };
            if (scholar) variant.Add(new(new FeatureDefinitionPointPool { Name = "Scholar", PoolType = "Expertise", PoolAmount = 1 },2));
            var result = new CompressedProgressionDefinitions("Wizard2024" + schoolAt3 + scholar).Prepare(variant,"Wizard");
            check(result.Single(x => x.FeatureDefinition.Name == "School").Level == (schoolAt3 ? 3 : 2) && result.Count == (scholar ? 2 : 1),
                "Wizard: low-level school/Scholar variant fixture " + schoolAt3 + "/" + scholar);
        }
    }

    // Reads the integer Add operands feeding one static field, without initializing the mod or Unity.
    private static int[] ReadAddedIntegers(TypeDefinition type, string field)
    {
        var instructions = type.Methods.Single(x => x.Name == ".cctor").Body.Instructions;
        var end = instructions.Select((x,i) => (x,i)).Single(x => x.x.OpCode.Code == Code.Stsfld && x.x.Operand is FieldReference f && f.Name == field).i;
        var start = instructions.Take(end).Select((x,i) => (x,i)).Where(x => x.x.OpCode.Code == Code.Stsfld).Select(x => x.i).DefaultIfEmpty(-1).Last();
        return instructions.Skip(start+1).Take(end-start-1).Where(x => x.Operand is MethodReference m && m.Name == "Add" &&
            m.DeclaringType is GenericInstanceType g && g.GenericArguments.Single().MetadataType == MetadataType.Int32)
            .Select(x => Integer(x.Previous) ?? throw new Exception("Unexpected nonconstant table entry")).ToArray();
    }
    private static int? Integer(Instruction i) => i.OpCode.Code switch
    {
        Code.Ldc_I4_M1 => -1, Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1, Code.Ldc_I4_2 => 2, Code.Ldc_I4_3 => 3,
        Code.Ldc_I4_4 => 4, Code.Ldc_I4_5 => 5, Code.Ldc_I4_6 => 6, Code.Ldc_I4_7 => 7, Code.Ldc_I4_8 => 8,
        Code.Ldc_I4 or Code.Ldc_I4_S => Convert.ToInt32(i.Operand), _ => null
    };
}
