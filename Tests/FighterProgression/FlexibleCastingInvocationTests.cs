using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using V = SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using F = SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using Fixture = FlexibleCastingPreflightTests.Fixture;

// Production registry + production preflight/operation plans, using installed resource-table fixtures.
// These tests do not load game types or execute Harmony/Unity.
internal static class FlexibleCastingInvocationTests
{
    private sealed class Use(Fixture fixture, bool create)
    {
        internal readonly Fixture Fixture = fixture;
        internal readonly object Hero = new(), Power = new(), Repertoire = new(), Effect = new(), Modal = new();
        internal bool Alive = true;
        internal V.Evidence Evidence => new("17", Hero, Power, Repertoire,
            SpellResourceConsumerCatalog.FlexibleCasting(create).Definition, create, true);
        internal F.Choice Choice(int circle = 1, bool shift = false) => new(circle, create, shift, false);
        internal V.Authority Capture(V registry, bool registered)
        {
            if (registered) return registry.Register(Evidence, Effect, "effect42", true, () => Alive);
            var source = registry.BeginCampaign(Evidence);
            registry.BindCampaign(source, Evidence, Effect, () => Alive);
            return source;
        }
        internal V.Envelope Prepare(V registry, V.Authority source, F.Choice choice = null)
        {
            registry.AttachModal(source, Evidence, Effect, Modal);
            return registry.Prepare(source, Evidence, Effect, Modal, Fixture.Model, choice ?? Choice(), Fixture.Live());
        }
    }
    private static V.Envelope Copy(V.Envelope e, string field = null) => new(
        field == "nullCommand" ? null : field == "command" ? "unknown" : e.Command, field == "nullInvocation" ? null : field == "invocation" ? "unknown" : e.Invocation,
        field == "hero" ? "wrong" : e.Hero, field == "repertoire" ? "wrong" : e.Repertoire,
        field == "definition" ? "wrong" : e.Definition,
        field == "kind" ? (e.Kind == V.SourceKind.RegisteredEffect ? V.SourceKind.TrustedCampaignInvocation : V.SourceKind.RegisteredEffect) : e.Kind,
        field == "effect" ? "wrong" : e.Effect, field == "direction" ? !e.Create : e.Create,
        field == "circle" ? e.NativeCircle + 1 : e.NativeCircle,
        field == "resourceCircle" ? e.ResourceCircle + 1 : e.ResourceCircle,
        field == "input" ? O.Domain.Arcanum : e.Input, field == "output" ? O.Domain.Arcanum : e.Output,
        field == "cost" ? e.Cost + 1 : e.Cost, field == "proof" ? "wrong" : e.Fingerprint);

    internal static void Run(Action<bool, string> check, AssemblyDefinition game, TypeDefinition[] types)
    {
        var behavior = 0; var metadata = 0;
        void B(bool ok, string name) { behavior++; check(ok, "13.7A.2 executed model: " + name); }
        void A(bool ok, string name) { metadata++; check(ok, "13.7A.2 compiled/definition audit: " + name); }
        void Reject(Use u, Action action, O.Code code, string name)
        {
            var before = u.Fixture.Balances(); var rejected = false;
            try { action(); } catch (O.Rejection ex) { rejected = ex.Diagnostic == code; }
            B(rejected, name + " structured rejection: " + code);
            B(before == u.Fixture.Balances(), name + " zero Standard/Pact/Sorcery/native-counter/condition/refund changes");
        }
        foreach (var mode in new[] { S.ResourceMode.Slots, S.ResourceMode.SpellPoints })
        foreach (var other in new[] { "Wizard", "Warlock" })
        foreach (var create in new[] { false, true })
        foreach (var registered in new[] { false, true })
        {
            var u = new Use(new Fixture(mode, other, create), create); var minted = 0;
            var registry = new V(() => (++minted).ToString());
            var before = u.Fixture.Balances(); var source = u.Capture(registry, registered);
            B(source.Kind == (registered ? V.SourceKind.RegisteredEffect : V.SourceKind.TrustedCampaignInvocation), "explicit source authority kind");
            B(minted == (registered ? 0 : 1), "transient ID minted once at entry; registered uses registry GUID");
            B(ReferenceEquals(source, registry.Find(u.Effect)), "exact effect maps to its issued authority");
            var command = u.Prepare(registry, source);
            B(command.Invocation == source.Id && command.Hero == "17" && command.Repertoire == u.Fixture.Repertoire,
                "source -> exact modal -> explicit local command/owner/repertoire mapping");
            B(command.Create == create && command.NativeCircle == 1 && command.ResourceCircle == 1, "direction and both circles bound");
            var retry = registry.Prepare(source, u.Evidence, u.Effect, u.Modal, u.Fixture.Model, u.Choice(), u.Fixture.Live());
            B(ReferenceEquals(command, retry) && minted == (registered ? 0 : 1), "identical UI retry reuses envelope and invocation");
            var queue = new Queue<V.Envelope>(); queue.Enqueue(Copy(command));
            var ack = registry.Receive(queue.Dequeue(), u.Evidence, u.Effect, u.Fixture.Live());
            B(ack.Invocation == source.Id && ack.Command == command.Command && !ack.NativeCompleted && !source.ParentCompleted,
                "delayed in-process delivery only acknowledges preflight, never native or parent completion");
            B(ReferenceEquals(ack, registry.Receive(Copy(command), u.Evidence, u.Effect, u.Fixture.Live())), "duplicate delivery is idempotent");
            B(before == u.Fixture.Balances(), "capture/Prepare/receive/ack/retry perform no spending, grants, healing, conditions or refund");
            foreach (var field in new[] { "command", "invocation", "hero", "repertoire", "definition", "kind", "effect", "direction", "circle", "resourceCircle", "input", "output", "cost", "proof" })
                Reject(u, () => registry.Receive(Copy(command, field), u.Evidence, u.Effect, u.Fixture.Live()), O.Code.ConflictingOperation, "conflicting " + field);
            Reject(u, () => registry.Receive(null, u.Evidence, u.Effect, u.Fixture.Live()), O.Code.MissingIdentity, "null envelope");
            Reject(u, () => registry.Receive(Copy(command, "nullCommand"), u.Evidence, u.Effect, u.Fixture.Live()), O.Code.MissingIdentity, "null command identity");
            Reject(u, () => registry.Receive(Copy(command, "nullInvocation"), u.Evidence, u.Effect, u.Fixture.Live()), O.Code.MissingIdentity, "null invocation identity");
            B(ReferenceEquals(ack, registry.Receive(command, u.Evidence, u.Effect, u.Fixture.Live())), "forged delivery does not invalidate legitimate acknowledged intent");
            Reject(u, () => registry.Prepare(source, u.Evidence, u.Effect, u.Modal, u.Fixture.Model, u.Choice(2), u.Fixture.Live()), O.Code.ConflictingOperation, "second conversion intent");
            Reject(u, () => registry.Receive(command, u.Evidence, u.Effect, u.Fixture.Live(), true), O.Code.PendingAtomicJournal, "network delivery explicitly blocked");
            Reject(u, () => registry.BlockCommit(command, u.Evidence, u.Effect, u.Fixture.Live()), O.Code.PendingAtomicJournal, "CommitNative remains blocked");
            registry.Close(source); registry.Close(source);
            Reject(u, () => registry.Receive(command, u.Evidence, u.Effect, u.Fixture.Live()), O.Code.StaleOperation, "cancel -> late delivery");
            Reject(u, () => registry.Prepare(source, u.Evidence, u.Effect, u.Modal, u.Fixture.Model, u.Choice(), u.Fixture.Live()), O.Code.StaleOperation, "cancel -> retry");
            B(before == u.Fixture.Balances() && !source.ParentCompleted, "Cancel has no refund/completion or resource mutation");
        }
        foreach (var mode in new[] { S.ResourceMode.Slots, S.ResourceMode.SpellPoints })
        foreach (var registered in new[] { false, true })
        {
            foreach (var wrong in new[] { "hero", "owner", "repertoire", "power", "definition", "direction", "eligible" })
            {
                var u = new Use(new Fixture(mode, "Warlock"), false); var v = new V(() => Guid.NewGuid().ToString("N")); var source = u.Capture(v, registered); var e = u.Evidence;
                var bad = new V.Evidence(wrong == "hero" ? "other" : e.Hero, wrong == "owner" ? new object() : e.Owner,
                    wrong == "power" ? new object() : e.Power, wrong == "repertoire" ? new object() : e.Repertoire,
                    wrong == "definition" ? "OtherPower" : wrong == "direction" ? SpellResourceConsumerCatalog.FlexibleCasting(true).Definition : e.Definition,
                    wrong == "direction", wrong != "eligible");
                Reject(u, () => v.AttachModal(source, bad, u.Effect, u.Modal), wrong is "hero" or "owner" or "repertoire" ? O.Code.WrongOwner : wrong is "power" or "direction" ? O.Code.ConflictingOperation : O.Code.Ineligible, "wrong source evidence " + wrong);
            }
            {
                var u = new Use(new Fixture(mode, "Warlock"), false); var n = 0; var v = new V(() => (++n).ToString()); var a = u.Capture(v, registered);
                Reject(u, () => v.AttachModal(a, u.Evidence, new object(), u.Modal), O.Code.WrongOwner, "wrong effect reference");
                v.AttachModal(a, u.Evidence, u.Effect, u.Modal);
                Reject(u, () => v.Prepare(a, u.Evidence, u.Effect, new object(), u.Fixture.Model, u.Choice(), u.Fixture.Live()), O.Code.WrongOwner, "wrong modal reference");
                var secondEffect = new object(); var b = v.BeginCampaign(u.Evidence); v.BindCampaign(b, u.Evidence, secondEffect, () => true);
                B(a.Id != b.Id, "simultaneous same-hero/same-circle sources have distinct IDs");
                Reject(u, () => v.AttachModal(b, u.Evidence, secondEffect, u.Modal), O.Code.AmbiguousSource, "second source cannot overwrite modal");
                var secondModal = new object(); v.AttachModal(b, u.Evidence, secondEffect, secondModal);
                var command = v.Prepare(a, u.Evidence, u.Effect, u.Modal, u.Fixture.Model, u.Choice(), u.Fixture.Live());
                Reject(u, () => v.Prepare(b, u.Evidence, secondEffect, secondModal, u.Fixture.Model, u.Choice(), u.Fixture.Live()), O.Code.PendingAtomicJournal, "existing one-pending-operation guard preserved");
                B(v.Receive(command, u.Evidence, u.Effect, u.Fixture.Live()).Invocation == a.Id, "simultaneous source rejection cannot redirect first intent");
                u.Alive = false;
                Reject(u, () => v.Receive(command, u.Evidence, u.Effect, u.Fixture.Live()), O.Code.StaleOperation, "terminated source before delivery");
                B(a.State == V.Status.Expired, "terminated source closes ticket before ack");
            }
            foreach (var stale in new[] { "rules", "sorcery", "conditions", "cost", "power", "standard", "pact" })
            {
                var u = new Use(new Fixture(mode, "Warlock"), false); var v = new V(() => "stale"); var a = u.Capture(v, registered); var c = u.Prepare(v, a);
                switch (stale)
                {
                    case "rules": u.Fixture.Rules = "changed"; break;
                    case "sorcery": u.Fixture.NativeSorcery--; break;
                    case "conditions": u.Fixture.Conditions = new[] { "changed" }; break;
                    case "cost": u.Fixture.NativeCosts[0]++; break;
                    case "power": u.Fixture.NativePowerUses--; break;
                    case "standard": u.Fixture.Resources.Standard.Spend("other", 1); break;
                    case "pact": u.Fixture.Resources.Pact.Spend("other", PactMagicResourcePlan.Source.Pact, 5); break;
                }
                Reject(u, () => v.Receive(c, u.Evidence, u.Effect, u.Fixture.Live()), stale == "rules" ? O.Code.RuleChanged : O.Code.StaleOperation, "stale " + stale);
                B(a.State == V.Status.Rejected, "stale ticket terminal before ack");
            }
        }
        // No identity means no entry, including a plausible but unregistered native GUID.
        {
            var u = new Use(new Fixture(S.ResourceMode.Slots, "Wizard"), false); var v = new V(() => "same");
            Reject(u, () => v.Register(u.Evidence, u.Effect, "42", false, () => true), O.Code.MissingIdentity, "no registry proof");
            Reject(u, () => v.Register(u.Evidence, u.Effect, "0", true, () => true), O.Code.MissingIdentity, "zero GUID");
            Reject(u, () => v.Find(u.Effect), O.Code.MissingIdentity, "unissued transient reference");
            Reject(u, () => v.BeginCampaign(new V.Evidence("17", u.Hero, u.Power, u.Repertoire, u.Evidence.Definition, false, false)), O.Code.Ineligible, "unproven campaign entry");
            Reject(u, () => new V(() => Guid.Empty.ToString("N")).BeginCampaign(u.Evidence), O.Code.MissingIdentity, "empty GUID invocation ID");
            var a = v.BeginCampaign(u.Evidence);
            Reject(u, () => v.BeginCampaign(u.Evidence), O.Code.ConflictingOperation, "minted ID reuse");
            Reject(u, () => new V(() => "0").BeginCampaign(u.Evidence), O.Code.MissingIdentity, "zero invocation ID");
            Reject(u, () => v.BindCampaign(a, u.Evidence, u.Effect, () => false), O.Code.AmbiguousSource, "dead transient effect");
            v.BindCampaign(a, u.Evidence, u.Effect, () => true);
            Reject(u, () => v.BindCampaign(a, u.Evidence, u.Effect, () => true), O.Code.AmbiguousSource, "effect bound twice");
            Reject(u, () => new V(() => "foreign").AttachModal(a, u.Evidence, u.Effect, u.Modal), O.Code.MissingIdentity, "foreign issuer authority");
            var registered = new V(() => "unused"); var r = registered.Register(u.Evidence, u.Effect, "42", true, () => true);
            B(ReferenceEquals(r, registered.Register(u.Evidence, u.Effect, "42", true, () => true)), "same verified registry entry retry");
            Reject(u, () => registered.Register(u.Evidence, new object(), "42", true, () => true), O.Code.WrongOwner, "GUID reused for different native reference");
        }
        // Pact actual input circle and native UI conversion circle remain distinct in transported commands.
        foreach (var mode in new[] { S.ResourceMode.Slots, S.ResourceMode.SpellPoints })
        {
            var u = new Use(new Fixture(mode, "Warlock"), false); var v = new V(() => "pact"); var a = u.Capture(v, false); var c = u.Prepare(v, a, u.Choice(1, true));
            B(c.Input == O.Domain.PactSlots && c.ResourceCircle == 5 && c.NativeCircle == 1 && c.Cost == 1, "Pact5 -> Sorcery1 native policy survives explicit transport");
            B(v.Receive(Copy(c), u.Evidence, u.Effect, u.Fixture.Live()).Result == "PreflightValidatedPendingAtomicJournal", "authorized Pact identity-only ack");
        }
        foreach (var mode in new[] { S.ResourceMode.Slots, S.ResourceMode.SpellPoints })
        foreach (var create in new[] { false, true })
        for (var circle = 1; circle <= 5; circle++)
        {
            var u = new Use(new Fixture(mode, "Wizard", create), create); var v = new V(() => "circle" + circle); var source = u.Capture(v, false);
            var before = u.Fixture.Balances(); var c = u.Prepare(v, source, u.Choice(circle));
            B(c.NativeCircle == circle && c.ResourceCircle == circle && !v.Receive(c, u.Evidence, u.Effect, u.Fixture.Live()).NativeCompleted,
                "all local conversion circles/directions retain preflight-only acknowledgement");
            B(before == u.Fixture.Balances(), "all circles leave budgets/counters/conditions/refunds unchanged");
        }
        Audit(A, game, types);
        Console.WriteLine($"Stage13.7A.2: {behavior} executed identity/transport model checks; {metadata} compiled/definition audits; no Unity execution.");
    }
    private static void Audit(Action<bool, string> a, AssemblyDefinition game, TypeDefinition[] types)
    {
        MethodReference[] Calls(MethodDefinition m) => m.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => (MethodReference)i.Operand).ToArray();
        foreach (var create in new[] { false, true })
        {
            using var json = JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionPower/" + SpellResourceConsumerCatalog.FlexibleCasting(create).Definition + ".json"));
            var root = json.RootElement; var effect = root.GetProperty("effectDescription");
            a(root.GetProperty("rechargeRate").GetString() == "AtWill" && effect.GetProperty("durationType").GetString() == "Instantaneous", "installed blueprint Flexible power is instantaneous/AtWill");
            var forms = effect.GetProperty("effectForms").EnumerateArray().ToArray();
            a(forms.Length == 1 && forms[0].GetProperty("formType").GetString() == "SpellSlots", "installed source has one SpellSlots form, not an unrelated power");
        }
        var campaign = game.MainModule.Types.Single(t => t.Name == "GameCampaignCharacter").Methods.Single(m => m.Name == "UsePower");
        var calls = Calls(campaign); var names = calls.Select(m => m.Name).ToArray();
        a(Array.IndexOf(names, "InstantiateEffectPower") < Array.IndexOf(names, "ApplyEffectForms") && Array.IndexOf(names, "ApplyEffectForms") < Array.IndexOf(names, "UsePower"), "campaign source factory -> forms/modal -> native parent UsePower");
        a(calls.Count(m => m.DeclaringType.Name == "RulesetCharacter" && m.Name == "UsePower") == 1, "campaign transpiler has exactly one verified parent call seam");
        a(!calls.Any(m => m.Name.Contains("Register") || m.Name.Contains("Terminate")), "campaign method itself does not prove effect registration or termination");
        var ctor = game.MainModule.Types.Single(t => t.Name == "RulesetImplementationManager").Methods.Single(m => m.Name == "InstantiateEffectPower");
        a(Calls(ctor).Length == 1 && Calls(ctor)[0].Name == ".ctor", "factory only constructs effect; registration not inferred");
        var location = game.MainModule.Types.Single(t => t.Name == "RulesetImplementationManagerLocation").Methods.Single(m => m.Name == "InstantiateEffectPower");
        a(Calls(location).Any(m => m.Name == "HandleEffectRegistration"), "location override is distinct from unregistered campaign base factory");
        var signal = game.MainModule.Types.Single(t => t.Name == "CharacterActionParams").Methods.Single(m => m.Name == "SignalActionStarting");
        a(Calls(signal).Any(m => m.Name == "Register") && Calls(signal).Any(m => m.Name == "RegisterPersonalEffect"), "ordinary action-start has conditional native registry registration");
        var nativeCtor = game.MainModule.Types.Single(t => t.Name == "RulesetEffectPower").Methods.Single(m => m.IsConstructor && m.Parameters.Count == 4);
        var force = nativeCtor.Body.Instructions.Where(i => i.Operand is MethodReference m && m.Name == "ForceSpentPoints").ToArray();
        var guards = nativeCtor.Body.Instructions.Where(i => i.Operand is MethodReference m && m.Name == "get_RechargeRate").ToArray();
        var recharge = game.MainModule.Types.Single(t => t.Name == "RuleDefinitions").NestedTypes.Single(t => t.Name == "RechargeRate");
        var atWill = Convert.ToInt32(recharge.Fields.Single(f => f.Name == "AtWill").Constant);
        a(force.Length == 3 && guards.Length == 3 && guards.All(g =>
        {
            var branch = g.Next.Next;
            var threshold = g.Next.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_S ? Convert.ToInt32(g.Next.Operand) : -1;
            if (g.Next.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_8) threshold = 8;
            var target = branch.Operand as Mono.Cecil.Cil.Instruction;
            return threshold != atWill && branch.OpCode.Code == Mono.Cecil.Cil.Code.Bne_Un_S && target != null &&
                force.Any(f => f.Offset > branch.Offset && f.Offset < target.Offset);
        }), "native ctor ForceSpentPoints occurs only behind non-AtWill recharge branches");
        a(Calls(nativeCtor).All(m => m.Name is not ("RegisterEntity" or "RegisterEffect")), "RegisterAttributes is not registry membership proof");
        var terminated = game.MainModule.Types.Single(t => t.Name == "RulesetEffect");
        a(terminated.Properties.Any(p => p.Name == "Terminated" && p.GetMethod != null) && terminated.Methods.Single(m => m.Name == "Terminate").Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "terminated"), "exact source lifetime exposes native Terminated state");
        var context = types.Single(t => t.Name == "FlexibleCastingPreflightContext");
        var begin = context.Methods.Single(m => m.Name == "BeginCampaign");
        a(Calls(begin).Any(m => m.Name == "BeginCampaign") && Calls(begin).Any(m => m.Name == "IsCharacterControlled"), "trusted campaign entry issues ID only with native controlled-owner check");
        a(Calls(context.Methods.Single(m => m.Name == "AllowCampaignSourceConstructor")).All(m => m.Name is not ("UsePower" or "SpendSorceryPoints")), "one-time source constructor permission never spends native resource");
        a(Calls(begin).Any(m => m.Name == "FlexibleShape") && Calls(context.Methods.Single(m => m.Name == "BindCampaignSource")).Any(m => m.Name == "FlexibleShape"), "definition and final effect shape both checked before campaign forms");
        a(Calls(begin).First().Name == "get_RuntimeEnabled", "gate OFF exits before campaign evidence/ID issuance");
        var receive = context.Methods.Single(m => m.Name == "ReceiveLocalCommand");
        a(receive.Parameters.Single().ParameterType.Name == "Envelope" && Calls(receive).Any(m => m.Name == "Receive"), "local receiver takes explicit envelope into production validation");
        var convert = context.NestedTypes.Single(t => t.Name == "ConvertPatch").Methods.Single(m => m.Name == "Prefix");
        a(Calls(convert).First().Name == "get_RuntimeEnabled", "ordinary gate-OFF callback retains native behavior");
        a(Calls(convert).Any(m => m.Name == "ReceiveLocalCommand") && !Calls(convert).Any(m => m.DeclaringType.Name == "IMagicCommandService"), "UI prefix uses identity-only receiver, never identity-less native dispatch");
        var patches = types.Single(t => t.Name == "FlexibleCastingCampaignPatches");
        foreach (var patch in patches.NestedTypes)
        {
            var attr = patch.CustomAttributes.Single(c => c.AttributeType.Name == "HarmonyPatch");
            var target = game.MainModule.Types.Single(t => t.FullName == ((TypeReference)attr.ConstructorArguments[0].Value).FullName).Methods.Single(m => m.Name == (string)attr.ConstructorArguments[1].Value);
            a(true, "native target exists: " + target.FullName);
            foreach (var hook in patch.Methods.Where(m => m.Name is "Prefix" or "Postfix"))
            foreach (var p in hook.Parameters.Where(p => p.Name.Length == 3 && p.Name.StartsWith("__") && char.IsDigit(p.Name[2])))
                a(target.Parameters[p.Name[2] - '0'].ParameterType.FullName == p.ParameterType.FullName, "campaign positional ABI " + p.Name);
        }
        var sensitive = new HashSet<string> { "CommitManaged", "SpendSorceryPoints", "CreateSpellSlot", "CreateSorceryPoints", "InflictCondition", "RefundActionUse", "Heal" };
        a(!context.Methods.Concat(context.NestedTypes.SelectMany(t => t.Methods)).Where(m => m.HasBody).SelectMany(Calls).Any(m => sensitive.Contains(m.Name)), "identity adapter has no resource/grant/refund calls");
        var hide = context.Methods.Single(m => m.Name == "RejectAndClose");
        a(Calls(hide).Any(m => m.Name == "Hide") && !Calls(hide).Any(m => m.Name is "OnCancelCb" or "RefundActionUse"), "rejection hides only owned modal without native cancel/refund");
        foreach (var name in new[] { "OnBeginHide", "OnEndHide" })
        {
            var method = game.MainModule.Types.Single(t => t.Name == "FlexibleCastingModal").Methods.Single(m => m.Name == name);
            a(!Calls(method).Any(m => sensitive.Contains(m.Name)), "native " + name + " is UI cleanup, no direct resource/refund calls");
        }
        foreach (var name in new[] { "CreateSpellSlotRPC", "CreateSorceryPointsRPC" })
        {
            var rpc = game.MainModule.Types.Single(t => t.Name == "NetworkingManager").Methods.Single(m => m.Name == name);
            a(rpc.Parameters.Count == 4 && rpc.Parameters.Last().ParameterType.FullName == "System.String", name + " lacks an authority envelope; deliberately blocked");
        }
        var finalization = types.Single(t => t.Name == "MultipleSpellRepertoiresContext").Methods.Single(m => m.Name == "ValidateSorcererFinalizeEntry");
        a(Calls(finalization).Any(m => m.Name == "IsSorcererDraft"), "native narrow guard uses tested current-draft classifier");
        a(!CompressedMulticasterProgressionPlan.MixedGameplayReady, "mixed gate stays false");
    }
}
