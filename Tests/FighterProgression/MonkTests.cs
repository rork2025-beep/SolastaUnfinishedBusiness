using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;

// These checks exercise the real managed planning/definition-transform code and inspect compiled
// bindings. Fixtures/doubles do not execute Unity, Harmony, combat, save/load, or the level-up UI.
internal static class MonkTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition type) =>
        new[] { type }.Concat(type.NestedTypes.SelectMany(Types));

    internal static void Run(Action<bool, string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        var context = types.Single(x => x.Name == "MonkProgressionContext");
        var builders = mod.MainModule.Types.Where(x => x.BaseType?.Name == "AbstractSubclass" &&
            x.Methods.Any(m => m.Name == "get_Klass" && Calls(m, "get_Monk"))).ToArray();
        check(builders.Length == 6, "six normal UB Monk registrations discovered from compiled class ownership");
        var legacy = mod.MainModule.Types.Single(x => x.Name == "WayOfTheWealAndWoe");
        check(legacy.BaseType?.Name != "AbstractSubclass", "Weal and Woe is compatibility-only, not a normal registered Monk builder");
        var subclassLoad = types.Single(x => x.Name == "SubclassesContext").Methods.Single(x => x.Name == "Load");
        check(subclassLoad.Body.Instructions.Any(x => x.Operand is MethodReference m && m.Name == ".ctor" &&
                  m.DeclaringType.Name == "WayOfTheWealAndWoe") &&
              subclassLoad.Body.Instructions.Any(x => x.Operand is FieldReference f && f.Name == "hidden"),
            "existing UB bootstrap explicitly constructs and hides compatibility Weal and Woe");

        var official = new[] { "TraditionFreedom", "TraditionLight", "TraditionOpenHand", "TraditionSurvival" };
        // Definition identity differs from builder class name for Shadow; derive the other fixture names
        // from the real compiled const Name rather than assuming filenames identify definitions.
        string DefinitionName(TypeDefinition builder) => builder.Fields
            .Where(x => x.Name == "Name" && x.HasConstant).Select(x => x.Constant as string).Single();
        var normalDefinitions = builders.Select(DefinitionName).ToArray();
        check(!normalDefinitions.Contains("WayOfWealAndWoe") && normalDefinitions.Distinct().Count() == 6,
            "normal Monk subclass inventory excludes hidden legacy definition and duplicate identities");
        var sources = new List<(string Name, List<FeatureUnlockByLevel> Unlocks)>
        {
            ("Monk", LoadBlueprint("CharacterClassDefinition/Monk"))
        };
        sources.AddRange(official.Select(x => (x, LoadBlueprint("CharacterSubclassDefinition/" + x))));
        sources.AddRange(normalDefinitions.Select(x => (x,
            LoadBlueprint("CharacterSubclassDefinition/" + x, "UnfinishedBusinessBlueprints"))));
        foreach (var source in sources)
        {
            var before = source.Unlocks.Select(x => (x.FeatureDefinition, x.Level)).ToArray();
            var transformer = new CompressedProgressionDefinitions("TestMonk" + source.Name);
            var output = transformer.Prepare(source.Unlocks, source.Name);
            var expected = source.Unlocks.OrderBy(x => x.Level).Select(x => x.FeatureDefinition).ToArray();
            var actual = output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
                .Where(x => x is not FeatureDefinitionFeatureSet).ToArray();
            check(expected.SequenceEqual(actual), "all Monk fixture events retain identity and original order: " + source.Name);
            check(source.Unlocks.Select(x => (x.FeatureDefinition, x.Level)).SequenceEqual(before),
                "Monk fixture source remains unchanged: " + source.Name);
            check(output.All(x => x.Level >= 1 && x.Level <= 12), "Monk fixture rewards fit class cap: " + source.Name);
        }

        check(Enumerable.Range(1, 12).Select(x => MonkProgressionRules.EffectiveLevel(true, x))
            .SequenceEqual(new[] { 1,2,3,4,5,7,9,11,13,15,17,20 }), "Monk effective progression mapping");
        check(Enumerable.Range(0, 21).All(x => MonkProgressionRules.EffectiveLevel(false,x) == x),
            "disabled Monk scaling preserves all actual class levels");
        check(MonkProgressionRules.EffectiveLevel(true,13) == 13,
            "unsupported existing Monk above 12 is not silently converted");
        check(!MonkProgressionRules.IsCapped(true,11) && MonkProgressionRules.IsCapped(true,12) &&
              MonkProgressionRules.IsCapped(true,13) && !MonkProgressionRules.IsCapped(false,12),
            "Monk class cap only applies in compressed mode");

        var high = LoadBlueprint("CharacterClassDefinition/Monk");
        high.Add(new(new FeatureDefinition { Name="EmptyBody18" },18));
        high.Add(new(new FeatureDefinition { Name="ASI19" },19));
        high.Add(new(new FeatureDefinition { Name="PerfectSelf20" },20));
        var highOutput = new CompressedProgressionDefinitions("TestMonk20").Prepare(high,"Monk");
        var highest = highOutput.Where(x => x.Level == 12)
            .SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Select(x => x.Name).ToArray();
        check(new[] {"EmptyBody18","ASI19","PerfectSelf20"}.All(highest.Contains),
            "all original 18-20 Monk class extension rewards fit actual 12");
        var level20 = types.Single(x => x.Name == "Level20Context").Methods.Single(x => x.Name == "MonkLoad");
        check(new[] {18,19,20}.All(n => level20.Body.Instructions.Any(i => Integer(i) == n)),
            "compiled UB retains Monk 18/19/20 extension before compression");
        var subclass20 = types.Single(x => x.Name == "Level20SubclassesContext").Methods.Single(x => x.Name == "MonkLoad");
        check(official.All(n => Calls(subclass20,"get_" + n)) &&
              subclass20.Body.Instructions.Any(i => Integer(i) == 17),
            "all four official Monk subclass capstones remain in original level-17 loader");

        CheckRewardSessions(check);
        CheckMartialArtsAndFocus(check, types, game);
        Check2024(check, types, context);
        CheckNativeBindings(check, types, game);
        CheckBodyAndMind(check, types, context);
        CheckLightReplacement(check,types);
        CheckMulticlass(check, types);
        CheckBindings(check, context, game);
    }

    private static void CheckRewardSessions(Action<bool,string> check)
    {
        var feat = new FeatureDefinitionPointPool { Name="MonkFeat", PoolType="Feat", PoolAmount=1 };
        var asi = new FeatureDefinitionFeatureSet { Name="MonkASIAndFeat", Mode=FeatureDefinitionFeatureSet.FeatureSetMode.Union };
        asi.FeatureSet.Add(new FeatureDefinitionPointPool { Name="MonkASI", PoolType="AbilityScore", PoolAmount=2 });
        asi.FeatureSet.Add(feat);
        var source = new List<FeatureUnlockByLevel>();
        foreach (var n in new[]{4,8,12,16,19}) source.Add(new(asi,n));
        foreach (var n in new[]{2,6,10,14,18}) source.Add(new(feat,n));
        var transform = new CompressedProgressionDefinitions("TestMonkRewards");
        var output = transform.Prepare(source,"Monk");
        var pools = output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionPointPool>().ToArray();
        check(pools.Where(x => x.PoolType == "Feat").Sum(x => x.PoolAmount) == 10 &&
              pools.Where(x => x.PoolType == "AbilityScore").Sum(x => x.PoolAmount) == 10,
            "target Monk configuration keeps five ASIs and ten class feat points");
        var merged = output.Where(x => x.Level == 12)
            .SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionPointPool>().ToArray();
        check(merged.Count(x => x.PoolType == "Feat") == 2 &&
              merged.All(transform.PoolOrigins.ContainsKey) &&
              merged.Select(x => transform.PoolOrigins[x]).Distinct().Count() == merged.Length,
            "Monk feat18 and ASI+feat19 remain separate choices with distinct origins at actual 12");
        var state = new FighterProgressionInitialization();
        var passes = 0;
        var list = new List<FeatureUnlockByLevel> { new(feat,18),new(asi,19) };
        for (var i=0; i<3; i++)
        {
            if (!state.ShouldInitialize(true)) continue;
            list = new CompressedProgressionDefinitions("TestMonkOnce").Prepare(list,"Monk");
            passes++;
        }
        check(passes == 1 && list.Count == 1 && list[0].Level == 12,
            "Monk managed transformation remains single-pass under repeated initialization");
        var disabled = new FighterProgressionInitialization();
        var original = new List<FeatureUnlockByLevel> { new(feat,18) };
        if (disabled.ShouldInitialize(false)) original = transform.Prepare(original,"DisabledMonk");
        check(original[0].Level == 18 && original[0].FeatureDefinition == feat,
            "disabled Monk initialization leaves original definitions and levels intact");
    }

    private static void CheckMartialArtsAndFocus(Action<bool,string> check, TypeDefinition[] types, AssemblyDefinition game)
    {
        using var dieJson = JsonDocument.Parse(File.ReadAllText(
            "Diagnostics/OfficialBlueprints/FeatureDefinitionAttackModifier/AttackModifierMonkMartialArtsImprovedDamage.json"));
        var dice = dieJson.RootElement.GetProperty("dieTypeByRankTable").EnumerateArray()
            .ToDictionary(x => x.GetProperty("rank").GetInt32(), x => x.GetProperty("dieType").GetString());
        check(dice[MonkProgressionRules.EffectiveLevel(true,5)] == "D6" &&
              dice[MonkProgressionRules.EffectiveLevel(true,8)] == "D8" &&
              dice[MonkProgressionRules.EffectiveLevel(true,11)] == "D10",
            "2014 Martial Arts rank fixture reaches original 5/11/17 dice at actual 5/8/11");
        var dice2024 = Read2024Dice(types.Single(x => x.Name == "Tabletop2024Context"), game);
        check(dice2024.Count == 20 && dice2024[MonkProgressionRules.EffectiveLevel(true,5)] == "D8" &&
              dice2024[MonkProgressionRules.EffectiveLevel(true,8)] == "D10" &&
              dice2024[MonkProgressionRules.EffectiveLevel(true,11)] == "D12",
            "compiled 2024 Martial Arts table projects original5/11/17 to actual5/8/11");
        using var multiplier = JsonDocument.Parse(File.ReadAllText(
            "Diagnostics/OfficialBlueprints/FeatureDefinitionAttributeModifier/AttributeModifierMonkKiPointsMultiplier.json"));
        check(multiplier.RootElement.GetProperty("modifiedAttribute").GetString() == "KiPoints" &&
              multiplier.RootElement.GetProperty("modifierOperation").GetString() == "MultiplyByClassLevelBeforeAdditions" &&
              MonkProgressionRules.EffectiveLevel(true,12) == 20,
            "Focus class multiplier metadata is preserved and effective Monk12 contributes20");
        var actorTypes = types.Where(x => x.FullName.Contains("RulesetActorPatcher")).ToArray();
        check(actorTypes.SelectMany(x => x.Methods).Any(m => CallsContext(m) &&
              m.HasBody && m.Body.Instructions.Any(i => i.Operand as string == "KiPoints")),
            "compiled KiPoints modifier dispatch reaches Monk-specific effective level");
        var extension = types.Single(x => x.Name == "RulesetCharacterExtensions");
        check(CallsContext(extension.Methods.Single(x => x.Name == "GetMonkDieType")),
            "Monk dice extension uses the scoped effective-level adapter");
        check(types.Single(x => x.Name == "ModifyProviderRankByClassLevel").Methods.Any(CallsContext),
            "provider-rank adapter covers native Monk Martial Arts bonus providers");
        using var movement = JsonDocument.Parse(File.ReadAllText(
            "Diagnostics/OfficialBlueprints/FeatureDefinitionMovementAffinity/MovementAffinityMonkUnarmoredMovement.json"));
        var ranks = movement.RootElement.GetProperty("additiveModifierByLevelTable").EnumerateArray()
            .ToDictionary(x => x.GetProperty("rank").GetInt32(),x => x.GetProperty("modifier").GetInt32());
        check(ranks[MonkProgressionRules.EffectiveLevel(true,6)] == 3 &&
              ranks[MonkProgressionRules.EffectiveLevel(true,8)] == 4 &&
              ranks[MonkProgressionRules.EffectiveLevel(true,10)] == 5 &&
              ranks[MonkProgressionRules.EffectiveLevel(true,12)] == 6,
            "movement rank fixture preserves original 6/10/14/18 advancement through actual12");
        check(types.SelectMany(x => x.Methods).Any(m => CallsContext(m) &&
              (m.Name.Contains("Movement") || m.DeclaringType.Name.Contains("Movement"))),
            "compiled movement integration explicitly dispatches Monk progression");
        check(extension.Methods.Where(x => x.Name == "GetClassLevel").All(x => !CallsContext(x)),
            "global GetClassLevel extensions remain unpatched by Monk compression");
    }

    private static void Check2024(Action<bool,string> check, TypeDefinition[] types, TypeDefinition context)
    {
        var rules = types.Single(x => x.Name == "Tabletop2024Context");
        foreach (var pair in new[]{("SwitchMonkHeightenedFocus",new[]{10,15}),
                     ("SwitchMonkSuperiorDefense",new[]{18}), ("SwitchMonkBodyAndMind",new[]{20})})
        {
            var method = rules.Methods.Single(x => x.Name == pair.Item1);
            check(Calls(method,"RemoveAll") && pair.Item2.All(n => method.Body.Instructions.Any(i => Integer(i) == n)),
                "2024 mutually exclusive replacement removes previous versions: " + pair.Item1);
        }
        var relevant = types.Where(x => x.FullName.Contains("Tabletop2024Context")).SelectMany(x => x.Methods).ToArray();
        var deflect = relevant.Where(m => m.DeclaringType.FullName.Contains("DeflectAttacks")).ToArray();
        check(deflect.Any(CallsContext) && deflect.Any(m => m.HasBody &&
              m.Body.Instructions.Any(i => Integer(i) == 13)),
            "Deflect Attacks class13 improvement uses effective Monk progression");
        check(FighterProgressionPlan<int>.CompressedLevel(13) == 9 &&
              FighterProgressionPlan<int>.CompressedLevel(10) == 8 &&
              FighterProgressionPlan<int>.CompressedLevel(15) == 10,
            "Deflect13, Heightened10 and PerfectFocus15 map to actual9/8/10");
        using var stunning = JsonDocument.Parse(File.ReadAllText(
            "Diagnostics/OfficialBlueprints/FeatureDefinitionPower/PowerMonkStunningStrike.json"));
        check(stunning.RootElement.GetProperty("rechargeRate").GetString() == "KiPoints" &&
              stunning.RootElement.GetProperty("costPerUse").GetInt32() == 1,
            "Stunning Strike retains separate one-point Ki/Focus cost metadata");
        check(types.Any(t => t.Name == "PhysicalAttackFinishedByMeStunningStrike" &&
              Types(t).SelectMany(x => x.Methods).Any(m => Calls(m,"get_EnableMonkStunningStrike2024"))),
            "existing Stunning Strike 2024 once-per-turn gate remains compiled");
        CheckPower(check,"PowerMonkSuperiorDefense","KiPoints",3);
        CheckPower(check,"PowerMonkUncannyMetabolism","LongRest",1);
        CheckPower(check,"PowerMonkPatientDefense2024AtWill","AtWill",1);
        CheckPower(check,"PowerMonkPatientDefense2024Ki","KiPoints",1);
        var startup = types.SelectMany(x => x.Methods).Single(x => Calls(x,"LateLoad","MonkProgressionContext"));
        var calls = startup.Body.Instructions.Select(x => x.Operand).OfType<MethodReference>().ToArray();
        int Index(string name) => Array.FindIndex(calls,x => x.Name == "LateLoad" && x.DeclaringType.Name == name);
        check(Index("MonkProgressionContext") > Index("Tabletop2024Context") &&
              Index("MonkProgressionContext") > Index("SpellPointsContext"),
            "Monk startup transformation runs after final2024 and shared spell-point initialization");
        var discovery = context.Methods.Single(x => x.Name == "DiscoverSubclasses");
        check(Calls(discovery,"get_AllSubClasses") && Calls(discovery,"GetDatabase"),
            "Monk discovery uses runtime registrations and actual definition database");
    }

    private static void CheckNativeBindings(Action<bool,string> check,TypeDefinition[] types,AssemblyDefinition game)
    {
        var native=types.Single(x => x.Name == "MonkNativeProgressionPatches");
        var ready=native.Methods.Single(x => x.Name == "IsReady");
        check(Calls(ready,"HasPatch") && new[]{"slowFallRewritten","deflectMissileRewritten"}.All(n =>
              ready.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == n)),
            "native readiness requires installed patches and both exact-match iterator rewrites");
        var startup=types.Single(x => x.Name == "MonkProgressionContext").Methods.Single(x => x.Name == "LateLoad");
        check(Calls(startup,"IsReady","MonkNativeProgressionPatches"),
            "Monk activation checks native patch readiness before enabling compression");
        foreach(var action in new[]{"CharacterActionSlowFall","CharacterActionDeflectMissile"})
        {
            var type=game.MainModule.Types.Single(x => x.Name == action);
            var candidates=type.NestedTypes.Where(x => x.Name.StartsWith("<ExecuteImpl>") &&
                x.Methods.Any(m => m.Name == "MoveNext")).ToArray();
            check(candidates.Length == 1 && candidates[0].Fields.Count(x => x.FieldType.FullName == type.FullName) == 1,
                "native iterator and captured action identity are unique: " + action);
            var instructions=candidates.Single().Methods.Single(x => x.Name == "MoveNext").Body.Instructions;
            var matches=0;
            for(var i=0;i<instructions.Count;i++)
            {
                if(instructions[i].Operand as string != "CharacterLevel") continue;
                if(action == "CharacterActionSlowFall" && i+3<instructions.Count &&
                   Integer(instructions[i+1]) == 0 &&
                   instructions[i+2].Operand is MethodReference a && a.Name == "GetAttribute" &&
                   instructions[i+3].Operand is MethodReference b && b.Name == "get_CurrentValue") matches++;
                if(action == "CharacterActionDeflectMissile" && i+1<instructions.Count &&
                   instructions[i+1].Operand is MethodReference c && c.Name == "TryGetAttributeValue") matches++;
            }
            check(matches == 1,"native exact CharacterLevel rewrite pattern occurs once: " + action);
        }
        foreach(var name in new[]{"SlowFallLevel","DeflectMissileLevel"})
        {
            var helper=native.Methods.Single(x => x.Name == name);
            check(Calls(helper,"ShouldUse") && CallsContext(helper),
                "native action helper retains scoped activation and Monk rank dispatch: " + name);
        }
        var movement=native.NestedTypes.Single(x => x.Name == "MovementPatch").Methods.Single(x => x.Name == "Prefix");
        var target=game.MainModule.Types.Single(x => x.Name == "RulesetCharacter").Methods
            .Single(x => x.Name == "ComputeSpeedAddition");
        check(movement.Parameters.Where(x => !x.Name.StartsWith("__")).All(p =>
              target.Parameters.Any(x => x.Name == p.Name)),"movement Harmony arguments match actual game method");
        check(Calls(movement,"GetDefinition") &&
              movement.Body.Instructions.Any(i => i.Operand as string == "MovementAffinityMonkUnarmoredMovement") && CallsContext(movement) &&
              movement.Body.Instructions.Any(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Add),
            "movement adapter is scoped to Monk unarmored provider and retains additive accumulation");
        check(MonkProgressionRules.EffectiveLevel(true,12)*5 == 100,
            "native Slow Fall projects original Monk20 reduction100 at actual12");
    }

    private static void CheckBodyAndMind(Action<bool,string> check, TypeDefinition[] types, TypeDefinition context)
    {
        check(MonkProgressionRules.BodyAndMindBonus == 4 && MonkProgressionRules.MaxClassLevel == 12,
            "Body and Mind has the original +4 attribute bonus and actual Monk cap12");
        var grant = new BodyAndMindGrantState();
        check(grant.Apply("Monk12") == 4 && grant.Apply("Monk12") == 0,
            "Body and Mind maximum grant applies once per acquisition");
        check(grant.Remove("Monk12") == -4 && grant.Remove("Monk12") == 0 && grant.Apply("Monk12") == 4,
            "Body and Mind remove/regrant remains reversible within managed lifecycle");
        var loaded = new BodyAndMindGrantState();
        check(loaded.Remove("Monk12") == -4 && loaded.Remove("Monk12") == 0,
            "loaded Body and Mind maximum removal is idempotent in managed state model");
        var max = types.Single(x => x.Name == "BodyAndMindMaximum").Methods.Single(x => x.Name == "ChangeMaximum");
        check(Calls(max,"set_MaxValue") && Calls(max,"set_MaxEditableValue") &&
              !Calls(max,"set_BaseValue") && !Calls(max,"ModifyAttributeAndMax") &&
              !max.Body.Instructions.Any(x => x.Operand is FieldReference f && f.Name == "baseValue"),
            "compressed Body and Mind callback never writes BaseValue");
        var build = context.Methods.Single(x => x.Name == "BuildBodyAndMind");
        check(Calls(build,"SetModifier") && new[]{"Dexterity","Wisdom"}.All(n =>
              build.Body.Instructions.Any(x => x.Operand as string == n)),
            "Body and Mind registers removable Dexterity/Wisdom modifiers");
    }

    private static void CheckLightReplacement(Action<bool,string> check,TypeDefinition[] types)
    {
        var level20=types.Single(x => x.Name == "Level20SubclassesContext");
        var suppress=level20.Methods.Single(x => x.Name == "ShouldSuppressOldRadiantStrikes");
        var getters=suppress.Body.Instructions.Select(x => x.Operand).OfType<MethodReference>()
            .Where(x => x.Name.StartsWith("get_AdditionalDamageTraditionLightRadiantStrikes")).Select(x => x.Name).ToArray();
        check(getters.OrderBy(x => x).SequenceEqual(new[]{
                  "get_AdditionalDamageTraditionLightRadiantStrikesLuminousKi",
                  "get_AdditionalDamageTraditionLightRadiantStrikesShine"}.OrderBy(x => x)) &&
              CallsContext(suppress) && Calls(suppress,"HasAnyFeature") &&
              suppress.Body.Instructions.Any(i => i.Operand as string == "FeatureTraditionLightPurityOfLife"),
            "Light suppression targets exactly two obsolete D4 providers only with compressed capstone ownership");
        var glbm=types.Single(x => x.Name == "GLBM");
        var dispatchers=Types(glbm).SelectMany(x => x.Methods).Where(m => Calls(m,"ShouldSuppressOldRadiantStrikes")).ToArray();
        check(dispatchers.Length == 2 && dispatchers.Any(x => x.Name == "ComputeAndNotifyAdditionalDamage") &&
              dispatchers.Any(x => x.DeclaringType.Name.StartsWith("<HandleAdditionalDamageOnCharacterAttackHitConfirmed>")),
            "Light obsolete damage is suppressed before reaction tagging and in the final damage dispatcher");
        var apply=types.Single(x => x.Name == "CustomBehaviorPurityOfLight").Methods.Single(x => x.Name == "ApplyFeature");
        var instructions=apply.Body.Instructions;
        var active=instructions.Select((x,i) => (Instruction:x,Index:i)).First(x =>
            x.Instruction.Operand is MethodReference m && m.Name == "get_Active" &&
            m.DeclaringType.Name == "MonkProgressionContext").Index;
        check(active+2<instructions.Count && instructions[active+2].OpCode.Code == Mono.Cecil.Cil.Code.Ret &&
              Calls(apply,"RemoveAll"),
            "compressed Light grant returns before destructive removal while disabled original path remains");
    }

    private static void CheckMulticlass(Action<bool,string> check, TypeDefinition[] types)
    {
        var filter = types.Single(x => x.Name == "MulticlassInOutRulesHelper");
        check(Types(filter).SelectMany(x => x.Methods).Any(m =>
              Calls(m,"IsCapped","FighterProgressionContext") &&
              Calls(m,"IsCapped","BarbarianProgressionContext") &&
              Calls(m,"IsCapped","MonkProgressionContext")),
            "multiclass eligibility composes Fighter, Barbarian and Monk caps");
        foreach (var name in new[]{"AssignClassLevel_Patch","AddClassLevel_Patch"})
            check(types.Where(x => x.Name == name).SelectMany(x => x.Methods).Any(m =>
                  Calls(m,"IsCapped","FighterProgressionContext") &&
                  Calls(m,"IsCapped","BarbarianProgressionContext") &&
                  Calls(m,"IsCapped","MonkProgressionContext")),
                "all three class caps remain guarded at assignment boundary: " + name);
        check(MonkProgressionRules.EffectiveLevel(true,12) == 20 &&
              BarbarianProgressionRules.EffectiveLevel(true,8) == 11 && 12+8 == 20,
            "Monk12/Barbarian8 keeps actual20 while independent progressions reach20/11");
        check(MonkProgressionRules.EffectiveLevel(true,12) == 20 &&
              FighterProgressionPlan<int>.EffectiveLevel(8) == 11 && 12+8 == 20,
            "Monk12/Fighter8 keeps actual20 while independent progressions reach20/11");
        var monk=types.Single(x => x.Name == "MonkProgressionContext");
        check(Types(monk).SelectMany(x => x.Methods).All(m =>
              !Calls(m,"ComputeCharacterLevel") && !Calls(m,"ComputeProficiencyBonus") && !Calls(m,"AddClassLevel")),
            "Monk context does not directly recompute or manufacture actual character/class levels");
        var multiclass = types.Single(x => x.Name == "MulticlassContext");
        check(multiclass.Methods.Any(m => m.HasBody && m.Body.Instructions.Any(i =>
                  i.Operand as string == "AttributeModifierExtraAttackForce3")),
            "existing ForceIfBetter extra-attack multiclass repair remains compiled");
        // No in-game claim: equipment checks, Strength/Rage/Reckless, action economy and defense
        // arbitration require the documented manual Monk/Barbarian and Monk/Fighter combat tests.
    }

    private static Dictionary<int,string> Read2024Dice(TypeDefinition rules,AssemblyDefinition game)
    {
        var instructions=rules.Methods.Single(x => x.Name == ".cctor").Body.Instructions;
        var end=instructions.Select((x,i) => (Instruction:x,Index:i)).Single(x =>
            x.Instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stsfld &&
            x.Instruction.Operand is FieldReference f && f.Name == "MonkUnarmedDieTypeByRank2024").Index;
        var start=instructions.Take(end).Select((x,i) => (Instruction:x,Index:i))
            .Where(x => x.Instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stsfld).Last().Index;
        var dieNames=game.MainModule.Types.Single(x => x.Name == "RuleDefinitions")
            .NestedTypes.Single(x => x.Name == "DieType").Fields.Where(x => x.HasConstant)
            .ToDictionary(x => Convert.ToInt32(x.Constant),x => x.Name);
        var result=new Dictionary<int,string>();
        var currentDie=0;
        for(var i=start+1; i<end; i++)
        {
            if(instructions[i].OpCode.Code != Mono.Cecil.Cil.Code.Stfld ||
               instructions[i].Operand is not FieldReference field) continue;
            if(field.DeclaringType.Name != "DieTypeByRank") continue;
            if(field.Name == "dieType") currentDie=Integer(instructions[i-1]).Value;
            if(field.Name == "rank") result.Add(Integer(instructions[i-1]).Value,dieNames[currentDie]);
        }
        return result;
    }

    private static void CheckPower(Action<bool,string> check,string name,string recharge,int cost)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(
            "Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionPower/"+name+".json"));
        check(json.RootElement.GetProperty("rechargeRate").GetString() == recharge &&
              json.RootElement.GetProperty("costPerUse").GetInt32() == cost,
            "Monk power resource/recovery fixture: " + name);
    }

    private static List<FeatureUnlockByLevel> LoadBlueprint(string name,string directory="OfficialBlueprints")
    {
        using var json = JsonDocument.Parse(File.ReadAllText("Diagnostics/"+directory+"/"+name+".json"));
        var identities = new Dictionary<string,FeatureDefinition>();
        return json.RootElement.GetProperty("featureUnlocks").EnumerateArray().Select(x =>
        {
            var name = x.GetProperty("featureDefinition").GetString().Split(':')[1];
            if (!identities.TryGetValue(name,out var feature)) identities[name] = feature = new FeatureDefinition { Name=name };
            return new FeatureUnlockByLevel(feature,x.GetProperty("level").GetInt32());
        }).ToList();
    }
    private static bool CallsContext(MethodDefinition method) => method.HasBody &&
        method.Body.Instructions.Any(i => i.Operand is MethodReference r && r.DeclaringType.Name == "MonkProgressionContext");
    private static bool Calls(MethodDefinition method,string name,string owner=null) => method.HasBody &&
        method.Body.Instructions.Any(i => i.Operand is MethodReference r && r.Name == name &&
            (owner == null || r.DeclaringType.Name == owner));
    private static int? Integer(Mono.Cecil.Cil.Instruction i) => i.OpCode.Code switch
    {
        Mono.Cecil.Cil.Code.Ldc_I4 => (int)i.Operand,
        Mono.Cecil.Cil.Code.Ldc_I4_S => (sbyte)i.Operand,
        Mono.Cecil.Cil.Code.Ldc_I4_0 => 0,
        Mono.Cecil.Cil.Code.Ldc_I4_1 => 1,
        Mono.Cecil.Cil.Code.Ldc_I4_2 => 2,
        Mono.Cecil.Cil.Code.Ldc_I4_3 => 3,
        Mono.Cecil.Cil.Code.Ldc_I4_4 => 4,
        Mono.Cecil.Cil.Code.Ldc_I4_5 => 5,
        Mono.Cecil.Cil.Code.Ldc_I4_6 => 6,
        Mono.Cecil.Cil.Code.Ldc_I4_7 => 7,
        Mono.Cecil.Cil.Code.Ldc_I4_8 => 8,
        _ => null
    };
    private static void CheckBindings(Action<bool,string> check,TypeDefinition context,AssemblyDefinition game)
    {
        var count=0;
        foreach (var patch in context.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "HarmonyPatch");
            if (attr == null) continue;
            var type=(TypeReference)attr.ConstructorArguments[0].Value;
            var name=(string)attr.ConstructorArguments[1].Value;
            var methods=game.MainModule.Types.Single(x => x.FullName == type.FullName).Methods
                .Where(x => x.Name == name || x.Name == "get_"+name).ToArray();
            check(methods.Length>0,"Monk Harmony target exists: "+patch.Name);
            foreach(var parameter in patch.Methods.Where(x => x.Name is "Prefix" or "Postfix")
                        .SelectMany(x => x.Parameters).Where(x => !x.Name.StartsWith("__")))
                check(methods.Any(m => m.Parameters.Any(p => p.Name == parameter.Name)),
                    "Monk Harmony argument binding: "+patch.Name+"."+parameter.Name);
            count++;
        }
        check(count>=7,"all Monk context Harmony patch targets checked against game metadata");
    }
}
