using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using F = SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using N = SolastaUnfinishedBusiness.Models.FlexibleCastingNativeCommitPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using T = SolastaUnfinishedBusiness.Models.FlexibleCastingSlotToSorceryPlan;
using V = SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using Fixture = FlexibleCastingPreflightTests.Fixture;

// B.4 fail-closed evidence. The fifth native route is NOT implemented. These execute the real
// preflight/executor rejection and existing budget arithmetic, not a pretend condition executor.
internal static class FlexibleCastingSlotConditionTests
{
    private sealed class ConditionPort
    {
        internal readonly object Existing = new();
        internal readonly Dictionary<ulong, object> Registry = new();
        internal readonly List<object> Graph = new();
        internal int AttachAttempts, RegisterAttempts = 0, RemoveAttempts = 0, Notifications = 0, Healing = 0, Parents = 0, ModalCompletions = 0;
        internal ConditionPort() { Graph.Add(Existing); Registry.Add(101, Existing); }
        internal string Snapshot => string.Join("/", new[] { Registry.Count, Graph.Count, AttachAttempts,
            RegisterAttempts, RemoveAttempts, Notifications, Healing, Parents, ModalCompletions });
        internal void ForbiddenPrepare()
        {
            AttachAttempts++;
            throw new InvalidOperationException("Blocked slot route reached a native condition port");
        }
    }
    private sealed class Host : N.IHost
    {
        internal readonly Fixture F;
        internal readonly V Registry = new(() => Guid.NewGuid().ToString("N"));
        internal readonly ConditionPort Conditions = new();
        internal readonly V.Authority Source;
        internal readonly V.Envelope Command;
        public object Owner { get; } = new();
        public object Repertoire { get; } = new();
        public object Effect { get; } = new();
        public object Modal { get; } = new();
        internal bool Gate = true, Local = true, Alive = true, Held = true;
        internal Host(string other, bool three, int circle)
        {
            F = new Fixture(S.ResourceMode.Slots, other, create: true, threeClasses: three);
            Source = Registry.BeginCampaign(Evidence());
            Registry.BindCampaign(Source, Evidence(), Effect, () => Alive);
            Registry.AttachModal(Source, Evidence(), Effect, Modal);
            Command = Registry.Prepare(Source, Evidence(), Effect, Modal, F.Model,
                new F.Choice(circle, true, false, false), Read());
        }
        public V.Evidence Evidence() => new("17", Owner, this, Repertoire,
            SpellResourceConsumerCatalog.FlexibleCasting(true).Definition, true, true);
        public F.Live Read() => F.Live();
        public void RequireAccess(bool replay)
        {
            if (!Gate) throw new O.Rejection(O.Code.Ineligible, "gate OFF");
            if (!Local) throw new O.Rejection(O.Code.PendingAtomicJournal, "network blocked");
            if (!replay && !Held) throw new O.Rejection(O.Code.PendingAtomicJournal, "stale modal/parent");
        }
        public N.Publication Prepare(T.Draft draft) { Conditions.ForbiddenPrepare(); return null; }
        public void Log(Exception error) { }
        internal string Snapshot => F.Balances() + "/" + F.Live().Signature + "/" + Conditions.Snapshot;
        internal N.Receipt Execute(V.Envelope command = null) => Registry.NativeCommits.Execute(command ?? Command, this);
    }
    internal static void Run(Action<bool, string> check, AssemblyDefinition game, TypeDefinition[] types)
    {
        var behavior = 0; var metadata = 0;
        void B(bool ok, string name) { behavior++; check(ok, "13.7B.4 executed guard/ledger: " + name); }
        void A(bool ok, string name) { metadata++; check(ok, "13.7B.4 native/compiled audit: " + name); }
        void Reject(Host host, Action execute, O.Code code, string name)
        {
            var before = host.Snapshot; var rejected = false;
            try { execute(); } catch (O.Rejection e) { rejected = e.Diagnostic == code; }
            B(rejected, name + " / " + code);
            B(host.Snapshot == before && ReferenceEquals(host.Conditions.Registry[101], host.Conditions.Existing) &&
                ReferenceEquals(host.Conditions.Graph.Single(), host.Conditions.Existing),
                name + " / all balances, graph/registry identities, views, healing, notifications and parent unchanged");
            B(host.Conditions.AttachAttempts == 0 && !host.Registry.NativeCommits.HasStarted(host.Command.Command) &&
                !host.Source.ParentCompleted, name + " / rejected before publication port, resource receipt or success");
        }
        foreach (var other in new[] { "Wizard", "Warlock" })
        foreach (var three in new[] { false, true })
        for (var circle = 1; circle <= 5; circle++)
        {
            var host = new Host(other, three, circle);
            B(host.Command.Input == O.Domain.SorceryPoints && host.Command.Output == O.Domain.StandardSlots &&
                host.Command.NativeCircle == circle && host.Command.ResourceCircle == circle && host.Command.Cost == host.F.NativeCosts[circle - 1],
                "issued slot intent binds actual Sorcery cost and exact UI/resource circle before any mutation");
            Reject(host, () => host.Execute(), O.Code.PendingAtomicJournal,
                other + (three ? " + martial third class" : " two classes") + " slot" + circle);
            B(host.Source.State == V.Status.Rejected,
                "unsupported condition route closes authority instead of leaving acknowledged preflight");
            Reject(host, () => host.Execute(), O.Code.StaleOperation, "same unsupported command is terminal, not a new grant");
            host.Registry.Close(host.Source, V.Status.Cancelled);
            Reject(host, () => host.Execute(), O.Code.StaleOperation, "Cancel after rejection never refunds or revives identity");
        }
        foreach (var invalid in new[] { "cancel", "gate", "network", "modal", "owner", "repertoire", "rules", "resources", "source", "condition", "payload" })
        {
            var h = new Host("Warlock", false, 2); var code = O.Code.StaleOperation;
            switch (invalid)
            {
                case "cancel": h.Registry.Close(h.Source, V.Status.Cancelled); break;
                case "gate": h.Gate = false; code = O.Code.Ineligible; break;
                case "network": h.Local = false; code = O.Code.PendingAtomicJournal; break;
                case "modal": h.Held = false; code = O.Code.PendingAtomicJournal; break;
                case "owner": h.F.Owner = "foreign-hero"; code = O.Code.WrongOwner; break;
                case "repertoire": h.F.Repertoire = "17/CastSpellWizard"; code = O.Code.WrongOwner; break;
                case "rules": h.F.Rules = "different-rules"; code = O.Code.RuleChanged; break;
                case "resources": h.F.NativeSorcery--; break;
                case "source": h.Alive = false; break;
                case "condition": h.F.Conditions = new[] { "changed-instance" }; break;
                case "payload": code = O.Code.ConflictingOperation; break;
            }
            var c = h.Command;
            var command = invalid != "payload" ? c : new V.Envelope(c.Command, c.Invocation, c.Hero, c.Repertoire, c.Definition,
                c.Kind, c.Effect, c.Create, c.NativeCircle, c.ResourceCircle, c.Input, c.Output, c.Cost + 1, c.Fingerprint);
            Reject(h, () => h.Execute(command), code, invalid + " cannot reach condition attachment");
        }
        BudgetAnalysis(B, types);
        Audit(A, game, types);
        Console.WriteLine($"Stage13.7B.4: {behavior} executed production rejection/ledger checks; {metadata} native/compiled/definition audits. " +
            "NO funded native condition adapter; attach/registration failure injection, successful lifecycle and Unity remain untested. Fifth route BLOCKED.");
    }

    private static void BudgetAnalysis(Action<bool, string> b, TypeDefinition[] types)
    {
        int[] Values(string type, string field) => CompressedMulticasterTests.AddedIntegers(types.Single(t => t.Name == type), field).Where(n => n >= 0).ToArray();
        var slots = Values("FeatureDefinitionCastSpellBuilder", "SlotsByCasterLevel");
        var tables = new S.Tables(Enumerable.Range(1, 20).Select(n => slots.Skip(n * 10).Take(9)),
            Values("ModifyPowerPoolAmountPowerSpellPoints", "SpellPointsByLevel"), Values("SpellPointsContext", "SpellCostByLevel"));
        var profiles = new[] { "Sorcerer", "Wizard" }.Select(name => new C.ClassProfile(name, true,
            new C.CastingProfile("CastSpell" + name, C.CasterKind.Full, 1, true, Enumerable.Range(1, 20).Select(n => Math.Min(9, (n + 1) / 2)))));
        var calculator = new C("b4-condition-budget", profiles);
        var result = calculator.Calculate(new C.Snapshot(20, new[] { new C.ClassInput("Sorcerer", 8, true), new C.ClassInput("Wizard", 12, true) },
            Enumerable.Repeat("Sorcerer", 8).Concat(Enumerable.Repeat("Wizard", 12))));
        S.Allocation Allocate(params S.Modifier[] modifiers) => new(tables, result, S.ResourceMode.Slots, modifiers);
        var ordinary = Allocate();
        for (var circle = 1; circle <= 5; circle++)
        {
            var first = new S.Modifier("native-condition:101", circle, 1);
            var second = new S.Modifier("native-condition:102", circle, 1);
            var budget = new S.Budget(tables, Allocate(first, first, second));
            var baseCount = ordinary.Slots[circle - 1];
            b(budget.RemainingSlots(circle) == baseCount + 2,
                "existing ordinary condition modifiers: distinct identities preserved, duplicate view counted once / circle" + circle);
            while (budget.CanSpend(circle)) budget.Spend("expenditure:" + circle + ":" + budget.RemainingSlots(circle), circle);
            for (var n = 0; n < 3; n++) budget.Refresh(Allocate(first, second));
            b(budget.RemainingSlots(circle) == 0, "repeated refresh cannot erase spent condition-slot debt / circle" + circle);
            budget.Refresh(Allocate(first)); budget.Refresh(Allocate(first));
            b(budget.RemainingSlots(circle) == 0, "existing modifier removal/repeated removal cannot refill another slot / circle" + circle);
            budget.Rest("short", false);
            b(budget.RemainingSlots(circle) == 0, "ordinary short rest retains slot debt / circle" + circle);
            budget.Refresh(Allocate(first, second));
            b(budget.RemainingSlots(circle) == 0, "readding capacity preserves debt until long rest / circle" + circle);
            budget.Refresh(ordinary); budget.Rest("expiry-long", true); budget.Rest("expiry-long", true);
            b(budget.RemainingSlots(circle) == baseCount, "model expiry after spent temporary capacity clears debt at long rest once / circle" + circle);

            var hazard = new S.Budget(tables, ordinary);
            hazard.GrantTemporarySlots("funding:issued-command", circle, 1);
            hazard.Refresh(Allocate(first));
            b(hazard.RemainingSlots(circle) == baseCount + 2,
                "HAZARD reproduced: existing GrantTemporarySlots plus same condition modifier gives TWO slots; not a supported adapter / circle" + circle);
            hazard.Refresh(ordinary);
            b(hazard.TemporarySlots(circle) == 1,
                "HAZARD reproduced: removing condition modifier alone does NOT revoke operation temporary grant / circle" + circle);
            hazard.Rest("expire", true);
            b(!hazard.GrantTemporarySlots("funding:issued-command", circle, 1) && hazard.RemainingSlots(circle) == baseCount,
                "existing expired grant receipt cannot mint replacement capacity on replay / circle" + circle);
        }
    }

    private static void Audit(Action<bool, string> a, AssemblyDefinition game, TypeDefinition[] types)
    {
        MethodDefinition Method(string type, string name) => game.MainModule.Types.Single(t => t.Name == type).Methods.Single(m => m.Name == name);
        Instruction[] Calls(MethodDefinition m, string name) => m.Body.Instructions.Where(i => i.Operand is MethodReference r && r.Name == name).ToArray();
        int Offset(MethodDefinition m, string name) => Calls(m, name).First().Offset;
        var create = Method("RulesetCharacter", "CreateSpellSlot");
        a(Offset(create, "SpendSorceryPoints") < Offset(create, "InflictCondition") &&
            create.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "SpellSlotCreationCost"),
            "native creation pays installed Sorcery cost before rejectable condition attachment");
        var inflict = Method("RulesetActor", "InflictCondition");
        a(Offset(inflict, "IsImmuneToCondition") < Offset(inflict, "CreateActiveCondition") &&
            Offset(inflict, "get_CanReceiveCondition") < Offset(inflict, "CreateActiveCondition") && inflict.Body.Instructions.Any(i => i.OpCode.Code == Code.Ldnull),
            "InflictCondition can refuse before creation; no success receipt for funding");
        var allocate = Method("RulesetCondition", "CreateActiveCondition");
        a(Calls(allocate, "Get").Length == 1 && Calls(allocate, "Register").Length == 0 &&
            allocate.Body.Instructions.Any(i => i.OpCode.Code == Code.Stfld && i.Operand is FieldReference f && f.Name == "targetGuid") &&
            allocate.Body.Instructions.Any(i => i.OpCode.Code == Code.Stfld && i.Operand is FieldReference f && f.Name == "sourceGuid"),
            "CreateActiveCondition supplies source/target fields, NOT registry proof");
        var register = Method("RulesetEntity", "Register");
        a(Offset(register, "GenerateGuid") < Offset(register, "RegisterEntity"), "native entity GUID generation precedes registry insertion");
        var insert = Method("RulesetEntityManager", "RegisterEntity");
        a(insert.ReturnType.FullName == "System.Void" && Calls(insert, "TryAdd").Single().Next.OpCode.Code == Code.Pop &&
            insert.Body.Instructions.All(i => i.Operand is not MethodReference r || r.Name != "Invoke"),
            "actual manager is callback-free but ignores TryAdd result: exact registry reference must still be verified");
        var add = Method("RulesetActor", "AddConditionOfCategory");
        a(Offset(add, "Add") < Offset(add, "Register") && Offset(add, "Register") < Offset(add, "OnConditionAdded") &&
            Offset(add, "OnConditionAdded") < Offset(add, "RefreshAll"), "native attach inserts/registers, publishes, then refreshes");
        var refreshBranch = add.Body.Instructions.Last(i => i.OpCode.Code == Code.Ldarg_3 || i.Operand is ParameterDefinition p && p.Index == 2);
        a(add.Parameters[2].Name == "refresh" && refreshBranch.Offset > Offset(add, "OnConditionAdded"),
            "refresh=false does not defer native attach callbacks");
        var remove = Method("RulesetActor", "RemoveConditionOfCategory");
        a(Calls(remove, "Remove").Single().Next.OpCode.Code == Code.Pop && Offset(remove, "Remove") < Offset(remove, "OnConditionRemoved") &&
            Offset(remove, "OnConditionRemoved") < Offset(remove, "Unregister") && Offset(remove, "Unregister") < Offset(remove, "RefreshAll"),
            "single removal ignores membership result; callbacks before unregister/refresh; duplicate removal is not a funded receipt");
        foreach (var name in new[] { "RemoveAllConditionsOfCategory", "RemoveAllConditionsOfCategoryExcludingSources" })
        {
            var bulk = Method("RulesetActor", name);
            var listRemoval = name == "RemoveAllConditionsOfCategory" ? "Remove" : "RemoveAt";
            a(Calls(bulk, "RemoveConditionOfCategory").Length == 0 && Offset(bulk, "Unregister") < Offset(bulk, listRemoval) &&
                Offset(bulk, listRemoval) < Offset(bulk, "OnConditionRemoved"), name + " bypasses single-removal seam; unregisters first");
        }
        var byType = Method("RulesetActor", "RemoveAllConditionsOfCategoryAndType");
        a(Calls(byType, "RemoveConditionOfCategory").Length == 0 && Offset(byType, "OnConditionRemoved") < Offset(byType, "Unregister") &&
            Offset(byType, "Unregister") < Offset(byType, "Remove"),
            "type-filtered bulk path publishes OnConditionRemoved while still attached, then unregisters/removes; distinct order");
        var category = Method("RulesetActor", "RemoveConditionCategoryAsNeeded");
        a(Calls(category, "RemoveConditionOfCategory").Length == 0 && Calls(category, "OnConditionRemoved").Length == 0 &&
            Offset(category, "Unregister") < Offset(category, "Remove"), "category removal bypasses both per-condition callback and single-removal hook");
        var unregister = Method("RulesetCondition", "Unregister");
        a(Offset(unregister, "Return") < Offset(unregister, "Unregister"), "condition instance returns to object pool before entity unregister");
        var rest = Method("RulesetActor", "ApplyRestForConditions");
        a(Calls(rest, "get_DurationType").Length > 0 && Calls(rest, "RemoveCondition").Length == 1,
            "rest duration cleanup delegates to individual removal; resource rest postfix alone cannot fix earlier observers");
        a(Calls(Method("GameCampaignCharacter", "EngageRest"), "ApplyRestForConditions").Length == 1 &&
            Offset(Method("RulesetCharacter", "RefreshEffectsForRest"), "ApplyRestForConditions") <
            Offset(Method("RulesetCharacter", "RefreshEffectsForRest"), "TerminateSpell"),
            "campaign rest engagement and refresh-effects cleanup enter condition removal separately from ApplyRest resource postfix");
        var refresh = Method("RulesetCharacterHero", "RefreshAll");
        a(Offset(refresh, "RefreshAttributeModifiersFromConditions") < Offset(refresh, "RefreshSpellRepertoires") &&
            Calls(refresh, "get_CharacterRefreshed").Length > 0, "RefreshAll is a broad mutating/publishing operation, not a callback-free condition packet");
        for (var circle = 1; circle <= 5; circle++)
        {
            using var json = JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/ConditionDefinition/ConditionAdditionalSpellSlot" + circle + ".json"));
            var root = json.RootElement;
            a(root.GetProperty("allowMultipleInstances").GetBoolean() && root.GetProperty("durationType").GetString() == "UntilLongRest" &&
                root.GetProperty("features").GetArrayLength() == 1 && !root.GetProperty("terminateWhenRemoved").GetBoolean(),
                "installed slot condition" + circle + " independently expires at long rest; not a replay-safe funding record");
        }
        var budget = types.Single(t => t.Name == "StandardCasterResourcePlan").NestedTypes.Single(t => t.Name == "Budget");
        a(budget.Methods.Single(m => m.Name == "GrantTemporarySlots").Parameters.All(p => p.ParameterType.Name != "RulesetCondition") &&
            budget.Methods.All(m => !m.Name.Contains("RevokeTemporary", StringComparison.Ordinal)),
            "existing temporary-grant budget has no exact native-condition removal/revoke API");
        a(!CompressedMulticasterProgressionPlan.MixedGameplayReady, "mixed gameplay gate stays OFF");
    }
}
