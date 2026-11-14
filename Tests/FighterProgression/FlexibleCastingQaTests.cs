using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using Q = SolastaUnfinishedBusiness.Models.FlexibleCastingQaPlan;

internal static class FlexibleCastingQaTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition type) => new[] { type }.Concat(type.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m, string name) => m.HasBody && m.Body.Instructions.Any(i => i.Operand is MethodReference r && r.Name == name);
    internal static void Run(Action<bool, string> check, AssemblyDefinition game, AssemblyDefinition ordinary)
    {
        var executed = 0; var audits = 0;
        void B(bool ok, string text) { executed++; check(ok, "C.1 executed QA readiness/XP preview: " + text); }
        void A(bool ok, string text) { audits++; check(ok, "C.1 ordinary/native audit: " + text); }
        Q.Hero Hero(bool observed = true, bool bound = true, object identity = null, int level = 3,
            KeyValuePair<string, int>[] classes = null, string[] history = null) => new(identity ?? new object(), observed, bound, level,
            classes ?? new[] { new KeyValuePair<string, int>("Sorcerer", 2), new KeyValuePair<string, int>("Wizard", 1) },
            history ?? new[] { "Sorcerer", "Sorcerer", "Wizard" });
        var valid = new[] { Hero(), Hero() };
        B(Q.Check(false, false, true, valid) == Q.Code.OrdinaryBuild, "ordinary build cannot enter QA");
        B(Q.Check(true, true, true, valid) == Q.Code.Ended, "End is terminal for this run");
        B(Q.Check(true, false, false, valid) == Q.Code.NotLocal, "network/unverified local transport rejected");
        B(Q.Check(true, false, true, null) == Q.Code.NoParty && Q.Check(true, false, true, Array.Empty<Q.Hero>()) == Q.Code.NoParty, "null/empty party is not admission");
        B(Q.Check(true, false, true, new[] { Hero(bound: false) }) == Q.Code.UnprovenOwner, "observed zero-level creation is NOT bound ownership");
        B(Q.Check(true, false, true, new[] { Hero(observed: false) }) == Q.Code.UnprovenOwner, "loaded/pool hero without exact creation evidence rejected");
        B(Q.Check(true, false, true, new[] { valid[0], valid[0] }) == Q.Code.UnprovenOwner, "duplicate original identity rejected");
        B(Q.Check(true, false, true, new[] { Hero(), Hero(bound: false) }) == Q.Code.UnprovenOwner, "one unbound party member rejects whole party");
        foreach (var invalid in new[]
                 {
                     Hero(level: 21), Hero(level: 4), Hero(history: new[] { "Sorcerer", "Wizard", "Wizard" }),
                     Hero(level: 13, classes: new[] { new KeyValuePair<string,int>("Sorcerer",13) }, history: Enumerable.Repeat("Sorcerer",13).ToArray()),
                     Hero(level: 4, classes: new[] { "Sorcerer", "Wizard", "Warlock", "Fighter" }.Select(id => new KeyValuePair<string,int>(id,1)).ToArray(), history: new[] { "Sorcerer", "Wizard", "Warlock", "Fighter" })
                 })
            B(Q.Check(true, false, true, new[] { invalid }) == Q.Code.InvalidHistory, "invalid real level/history/class cap/count rejected");
        B(Q.Check(true, false, true, valid) == Q.Code.NativeFinalizationUnavailable, "even synthetic fully-bound proof cannot bypass pending native finalization");
        var zero = Hero(level: 0, classes: Array.Empty<KeyValuePair<string,int>>(), history: Array.Empty<string>());
        B(Q.Check(true, false, true, new[] { zero }) == Q.Code.NativeFinalizationUnavailable, "new level0 boundary alone does not authorize first grant");
        var third = Hero(level: 20, classes: new[] { new KeyValuePair<string,int>("Sorcerer",8), new KeyValuePair<string,int>("Warlock",6), new KeyValuePair<string,int>("Fighter",6) },
            history: Enumerable.Repeat("Sorcerer",8).Concat(Enumerable.Repeat("Warlock",6)).Concat(Enumerable.Repeat("Fighter",6)).ToArray());
        B(Q.Check(true, false, true, new[] { third }) == Q.Code.NativeFinalizationUnavailable, "valid three-class shape does not relax runtime boundary");
        var refs = valid.Select(h => h.Identity).ToArray();
        for (var i = 0; i < 3; i++) Q.Check(true, false, true, valid);
        B(valid.Select(h => h.Identity).SequenceEqual(refs) && valid.All(h => h.Level == 3 && h.History.Count == 3), "readiness retries do not mutate character state or bind owners");
        var thresholds = InstalledThresholds(game);
        for (var level = 1; level < 20; level++)
        {
            var xp = thresholds[level - 1];
            B(Q.NextXpDelta(level, xp, thresholds, 100) == thresholds[level] - xp, "installed next-threshold delta at actual level" + level);
            B(Q.NextXpDelta(level, thresholds[level], thresholds, 100) == 0, "already at next threshold never grants extra XP / level" + level);
        }
        foreach (var multiplier in new[] { 50, 150, 200 })
            B(Reject(() => Q.NextXpDelta(2, 300, thresholds, multiplier)), "native UB multiplier must not turn exact delta into extra XP");
        B(Reject(() => Q.NextXpDelta(0, 0, thresholds, 100)) && Reject(() => Q.NextXpDelta(20, 355000, thresholds, 100)), "level0/cap20 XP mutation forbidden");
        B(Reject(() => Q.NextXpDelta(2, -1, thresholds, 100)) && Reject(() => Q.NextXpDelta(2, 0, new[] { 0 }, 100)), "negative XP/short table rejected");
        B(!Q.NativeAdmissionReady && !CompressedMulticasterProgressionPlan.MixedGameplayReady, "both readiness gates remain closed; XP preview has no native writer");

        var types = ordinary.MainModule.Types.SelectMany(Types).ToArray();
        A(!types.Any(t => t.Name is "FlexibleCastingQaContext" or "FlexibleCastingQaDisplay" or "ObserveCreationPatch" or "RejectCampaignConversionPatch"), "ordinary assembly contains no QA UI/native hooks");
        A(!ordinary.CustomAttributes.Any(a => a.AttributeType.Name == "AssemblyMetadataAttribute" && ((string)a.ConstructorArguments[0].Value).StartsWith("FlexibleCastingQa", StringComparison.Ordinal)), "ordinary assembly has no QA identity");
        var gate = types.Single(t => t.Name == "StandardMulticasterResourcesContext").Methods.Single(m => m.Name == "get_RuntimeEnabled");
        A(gate.Body.Instructions.Any(i => i.OpCode.Code == Code.Ldc_I4_0) && !gate.Body.Instructions.Any(i => i.Operand is MethodReference), "ordinary global runtime remains constant false");
        A(Calls(types.Single(t => t.Name == "UpdateContext").Methods.Single(m => m.Name == "Load"), "GetLatestVersion"), "ordinary updater behavior preserved");
        var create = game.MainModule.Types.Single(t => t.Name == "CharacterBuildingManager").Methods.Single(m => m.Name == "CreateNewCharacter");
        A(create.Body.Instructions.Any(i => i.OpCode.Code == Code.Newobj && i.Operand is MethodReference r && r.DeclaringType.Name == "RulesetCharacterHero") &&
            !Calls(create, "AssignClassLevel"), "native creation allocates hero before class selection; observation point is not finalization");
        var load = game.MainModule.Types.Single(t => t.Name == "CharacterPoolManager").Methods.Single(m => m.Name == "LoadCharacter");
        A(load.Body.Instructions.Any(i => i.OpCode.Code == Code.Newobj && i.Operand is MethodReference r && r.DeclaringType.Name == "RulesetCharacterHero"), "pool loading creates another native hero object");
        var start = game.MainModule.Types.Single(t => t.Name == "GameManager").NestedTypes.Single(t => t.Name.StartsWith("<CreateGameAsync>", StringComparison.Ordinal)).Methods.Single(m => m.Name == "MoveNext");
        A(Calls(start, "LoadCharacter") && start.Body.Instructions.Any(i => i.OpCode.Code == Code.Newobj && i.Operand is MethodReference r && r.DeclaringType.Name == "GameCampaignCharacter"), "campaign startup loads pool character and wraps it; no proven level0 journal transfer");
        var grant = game.MainModule.Types.Single(t => t.Name == "RulesetCharacterHero").Methods.Single(m => m.Name == "GrantExperience");
        A(Calls(grant, "set_BaseValue") && Calls(grant, "Refresh") && Calls(grant, "AccountEarnedXP") && !Calls(grant, "AssignClassLevel"), "native XP path updates experience/events without assigning class levels");
        A(types.Single(t => t.Name == "SorcererFinalizePreflight").Methods.Single(m => m.Name == "Inspect").Body.Instructions.Any(i => i.OpCode.Code == Code.Ldc_I4_2), "completed Sorcerer draft retains PendingAtomicJournal");
        Console.WriteLine($"Stage13.7C.1: {executed} executed readiness/XP-preview checks; {audits} ordinary/native audits. Admission and XP native action NOT IMPLEMENTED; diagnostic QA only, no Unity execution.");
    }
    private static int[] InstalledThresholds(AssemblyDefinition game)
    {
        var il = game.MainModule.Types.Single(t => t.Name == "RuleDefinitions").Methods.Single(m => m.Name == ".cctor").Body.Instructions.ToArray();
        var end = Array.FindIndex(il, i => i.OpCode.Code == Code.Stsfld && i.Operand is FieldReference f && f.Name == "ExperienceThresholds");
        var blob = ((FieldReference)il.Take(end).Last(i => i.OpCode.Code == Code.Ldtoken).Operand).Resolve().InitialValue;
        return Enumerable.Range(0, blob.Length / 4).Select(n => BitConverter.ToInt32(blob, n * 4)).ToArray();
    }
    private static bool Reject(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    internal static void AuditQa(Action<bool, string> check, AssemblyDefinition qa, string expectedSha)
    {
        using var game = AssemblyDefinition.ReadAssembly(Path.Combine("SolastaFiles", "Solasta_Data", "Managed", "Assembly-CSharp.dll"));
        var types = qa.MainModule.Types.SelectMany(Types).ToArray();
        var metadata = qa.CustomAttributes.Where(a => a.AttributeType.Name == "AssemblyMetadataAttribute").ToDictionary(a => (string)a.ConstructorArguments[0].Value, a => (string)a.ConstructorArguments[1].Value);
        check(metadata["FlexibleCastingQaBuild"] == Q.BuildId && metadata["FlexibleCastingQaSourceCommit"] == expectedSha, "QA compiled identity matches full source SHA");
        var context = types.Single(t => t.Name == "FlexibleCastingQaContext");
        foreach (var patch in context.NestedTypes.Where(t => t.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPatch")))
        {
            var attribute = patch.CustomAttributes.Single(a => a.AttributeType.Name == "HarmonyPatch");
            var targetType = (TypeReference)attribute.ConstructorArguments[0].Value;
            var methodName = (string)attribute.ConstructorArguments[1].Value;
            var target = types.Concat(game.MainModule.Types.SelectMany(Types)).Single(t => t.FullName == targetType.FullName);
            check(target.Methods.Any(m => m.Name == methodName), "QA Harmony target exists in compiled/native definition: " + patch.Name);
        }
        check(types.Single(t => t.Name == "FlexibleCastingQaDisplay").Interfaces.Any(i => i.InterfaceType.Name == "IMenuSelectablePage"), "QA panel is discoverable through existing mod menu");
        check(!context.Methods.Concat(context.NestedTypes.SelectMany(t => t.Methods)).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Any(i => i.Operand is MethodReference r && r.Name is "BindNewHero" or "UpdateProgression" or "GrantExperience" or "CommitManaged" or "Execute" or "CreateSorceryPoints" or "CreateSpellSlot"), "QA diagnostic entry cannot bind/spend/grant/finalize/award XP");
        check(!Calls(types.Single(t => t.Name == "UpdateContext").Methods.Single(m => m.Name == "Load"), "GetLatestVersion"), "QA does not offer ordinary updater on load");
        check(context.NestedTypes.Single(t => t.Name == "ObserveCreationPatch").CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPatch"), "QA-only level0 observation hook compiled");
        foreach (var name in new[] { "RejectCampaignConversionPatch", "RejectModalConversionPatch" })
            check(context.NestedTypes.Single(t => t.Name == name).Methods.Single(m => m.Name == "Prefix").ReturnType.FullName == "System.Boolean", "QA rejection prefix compiled: " + name);
        foreach (var name in new[] { "RejectIdentitylessSlotPatch", "RejectIdentitylessSorceryPatch" })
            check(context.NestedTypes.Single(t => t.Name == name).Methods.Single(m => m.Name == "Prefix").Body.Instructions.Any(i => i.OpCode.Code == Code.Throw), "QA rejects local/network identity-less boundary: " + name);
        check(context.NestedTypes.Single(t => t.Name == "RejectRespecPatch").Methods.Single(m => m.Name == "Prefix").ReturnType.FullName == "System.Boolean", "QA Respec entry is suppressed without replacing heroes");
        check(context.Methods.Single(m => m.Name == "Admission").Body.Instructions.Any(i => i.Operand is MethodReference r && r.Name == "Check"), "QA UI uses production readiness rejection");
        check(context.Methods.Single(m => m.Name == "CheckXp").Body.Instructions.Any(i => i.OpCode.Code == Code.Throw), "direct QA XP call rejects even if disabled button is bypassed");
        check(types.Single(t => t.Name == "StandardMulticasterResourcesContext").Methods.Single(m => m.Name == "get_RuntimeEnabled").Body.Instructions.Any(i => i.OpCode.Code == Code.Ldc_I4_0), "QA does not open global mixed runtime gate");
        check(!CompressedMulticasterProgressionPlan.MixedGameplayReady, "model mixed gameplay gate remains false");
    }
}
