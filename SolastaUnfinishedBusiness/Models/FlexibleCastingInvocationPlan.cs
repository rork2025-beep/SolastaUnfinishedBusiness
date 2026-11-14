using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using F = SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;

namespace SolastaUnfinishedBusiness.Models;

// Process-local source authority and explicit command transport. Never a resource/native journal.
internal sealed class FlexibleCastingInvocationPlan
{
    internal enum SourceKind { RegisteredEffect, TrustedCampaignInvocation }
    internal enum Status { Entry, Captured, ModalOpen, Prepared, AcknowledgedPreflight, Cancelled, Rejected, Expired }
    internal sealed class Evidence(string hero, object owner, object power, object repertoire, string definition, bool create, bool eligible)
    {
        internal string Hero { get; } = hero; internal object Owner { get; } = owner; internal object Power { get; } = power; internal object Repertoire { get; } = repertoire;
        internal string Definition { get; } = definition; internal bool Create { get; } = create; internal bool Eligible { get; } = eligible;
    }
    internal sealed class Authority
    {
        internal readonly string Id; internal readonly SourceKind Kind; internal readonly Evidence Entry;
        internal object Effect { get; private set; }
        internal string NativeEffectId { get; private set; }
        internal Status State { get; private set; } = Status.Entry;
        internal object Modal { get; private set; }
        private Func<bool> alive;
        internal Authority(string id, SourceKind kind, Evidence entry) { Id = id; Kind = kind; Entry = entry; }
        internal void Bind(object effect, string nativeId, Func<bool> isAlive) { Effect = effect; NativeEffectId = nativeId; alive = isAlive; State = Status.Captured; }
        internal bool Alive => alive != null && alive();
        internal void Open(object modal) { Modal = modal; State = Status.ModalOpen; }
        internal void Set(Status state) => State = state;
        internal F.Authority Preflight(string repertoire) => new(Entry.Hero, Id, repertoire, Entry.Create, true,
            Kind == SourceKind.RegisteredEffect ? F.SourceProofKind.RegisteredEffect : F.SourceProofKind.TrustedCampaignInvocation);
        // Parent action is deliberately held. No acknowledgement claims that native work completed.
        internal bool ParentCompleted => false;
    }
    internal sealed class Envelope(string command, string invocation, string hero, string repertoire, string definition, SourceKind kind, string effect,
        bool create, int nativeCircle, int resourceCircle, O.Domain input, O.Domain output, int cost, string requestFingerprint)
    {
        internal string Command { get; } = command; internal string Invocation { get; } = invocation; internal string Hero { get; } = hero; internal string Repertoire { get; } = repertoire;
        internal string Definition { get; } = definition; internal SourceKind Kind { get; } = kind; internal string Effect { get; } = effect;
        internal bool Create { get; } = create; internal int NativeCircle { get; } = nativeCircle; internal int ResourceCircle { get; } = resourceCircle;
        internal O.Domain Input { get; } = input; internal O.Domain Output { get; } = output; internal int Cost { get; } = cost; internal string Fingerprint { get; } = requestFingerprint;
        internal bool SameAs(Envelope other) => other != null && Command == other.Command && Invocation == other.Invocation &&
            Hero == other.Hero && Repertoire == other.Repertoire && Definition == other.Definition && Kind == other.Kind && Effect == other.Effect &&
            Create == other.Create && NativeCircle == other.NativeCircle && ResourceCircle == other.ResourceCircle && Input == other.Input &&
            Output == other.Output && Cost == other.Cost && Fingerprint == other.Fingerprint;
    }
    internal sealed class Acknowledgement(string command, string invocation)
    {
        internal string Command { get; } = command; internal string Invocation { get; } = invocation;
        internal string Result => "PreflightValidatedPendingAtomicJournal";
        internal bool NativeCompleted => false;
    }
    private sealed class Pending(Authority source, F model, F.Authority preflight, F.Choice choice, F.Ticket ticket, Envelope envelope)
    {
        internal readonly Authority Source = source; internal readonly F Model = model; internal readonly F.Authority Preflight = preflight;
        internal readonly F.Choice Choice = choice; internal readonly F.Ticket Ticket = ticket; internal readonly Envelope Envelope = envelope;
        internal Acknowledgement Ack;
    }
    private readonly Func<string> mint;
    private readonly Dictionary<string, Authority> sources = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<object, Authority> effects = new();
    private readonly ConditionalWeakTable<object, Authority> modalOwners = new();
    private readonly Dictionary<string, Pending> pending = new(StringComparer.Ordinal);
    internal FlexibleCastingInvocationPlan(Func<string> mintInvocation) => mint = mintInvocation;
    private static bool MissingId(string id) => string.IsNullOrWhiteSpace(id) || id == "0" || Guid.TryParse(id, out var guid) && guid == Guid.Empty;
    private static void Valid(Evidence e)
    {
        if (e == null || !e.Eligible || MissingId(e.Hero) || e.Owner == null || e.Power == null || e.Repertoire == null ||
            e.Definition != SpellResourceConsumerCatalog.FlexibleCasting(e.Create).Definition) throw new O.Rejection(O.Code.Ineligible, "Trusted Flexible Casting entry evidence required");
    }
    private static void Same(Evidence expected, Evidence current)
    {
        Valid(current);
        if (current.Hero != expected.Hero || !ReferenceEquals(current.Owner, expected.Owner) || !ReferenceEquals(current.Repertoire, expected.Repertoire)) throw new O.Rejection(O.Code.WrongOwner, "Source owner/repertoire reference mismatch");
        if (!ReferenceEquals(current.Power, expected.Power) || current.Definition != expected.Definition || current.Create != expected.Create) throw new O.Rejection(O.Code.ConflictingOperation, "Native source definition/direction mismatch");
    }
    internal Authority BeginCampaign(Evidence entry)
    {
        Valid(entry); var nonce = mint();
        if (MissingId(nonce)) throw new O.Rejection(O.Code.MissingIdentity, "Trusted entry ID issuer failed");
        var id = "campaign:" + nonce;
        if (sources.ContainsKey(id)) throw new O.Rejection(O.Code.ConflictingOperation, "Reused invocation ID");
        var source = new Authority(id, SourceKind.TrustedCampaignInvocation, entry); sources.Add(id, source); return source;
    }
    internal void BindCampaign(Authority source, Evidence actual, object effect, Func<bool> alive)
    {
        Known(source); Same(source.Entry, actual);
        if (source.Kind != SourceKind.TrustedCampaignInvocation || source.State != Status.Entry || effect == null || alive == null || !alive() || effects.TryGetValue(effect, out _))
            throw new O.Rejection(O.Code.AmbiguousSource, "Transient effect must be bound once inside its trusted invocation");
        source.Bind(effect, null, alive); effects.Add(effect, source);
    }
    internal Authority Register(Evidence entry, object effect, string nativeGuid, bool registryProved, Func<bool> alive)
    {
        Valid(entry);
        if (effect == null || MissingId(nativeGuid) || !registryProved || alive == null || !alive()) throw new O.Rejection(O.Code.MissingIdentity, "Actual live registry proof required");
        var id = "registered:" + entry.Hero + ":" + nativeGuid;
        if (sources.TryGetValue(id, out var previous))
        { Same(previous.Entry, entry); Check(previous, entry, effect); return previous; }
        if (effects.TryGetValue(effect, out _)) throw new O.Rejection(O.Code.ConflictingOperation, "Effect reference already belongs to another invocation");
        var source = new Authority(id, SourceKind.RegisteredEffect, entry); source.Bind(effect, nativeGuid, alive); sources.Add(id, source); effects.Add(effect, source); return source;
    }
    private void Known(Authority source)
    { if (source == null || !sources.TryGetValue(source.Id, out var actual) || !ReferenceEquals(actual, source)) throw new O.Rejection(O.Code.MissingIdentity, "Unknown source authority"); }
    internal Authority Find(object effect)
    { if (effect == null || !effects.TryGetValue(effect, out var source)) throw new O.Rejection(O.Code.MissingIdentity, "Unmapped exact effect reference"); return source; }
    internal void Check(Authority source, Evidence current, object effect)
    {
        Known(source); Same(source.Entry, current);
        if (!ReferenceEquals(source.Effect, effect)) throw new O.Rejection(O.Code.WrongOwner, "Unrelated native effect reference");
        if (source.State is Status.Cancelled or Status.Rejected or Status.Expired) throw new O.Rejection(O.Code.StaleOperation, "Closed invocation");
        if (!source.Alive) { Close(source, Status.Expired); throw new O.Rejection(O.Code.StaleOperation, "Terminated/expired source"); }
    }
    internal void AttachModal(Authority source, Evidence current, object effect, object modal)
    {
        Check(source, current, effect);
        if (modal == null || source.Modal != null && !ReferenceEquals(source.Modal, modal)) throw new O.Rejection(O.Code.AmbiguousSource, "Invocation already owns an exact modal");
        if (source.State != Status.Captured && source.State != Status.ModalOpen) throw new O.Rejection(O.Code.StaleOperation, "Modal source already has an intent");
        if (modalOwners.TryGetValue(modal, out var occupant) && !ReferenceEquals(occupant, source))
        {
            if (occupant.State is not (Status.Cancelled or Status.Rejected or Status.Expired)) throw new O.Rejection(O.Code.AmbiguousSource, "Modal belongs to another live invocation");
            modalOwners.Remove(modal);
        }
        if (!modalOwners.TryGetValue(modal, out _)) modalOwners.Add(modal, source);
        source.Open(modal);
    }
    internal Envelope Prepare(Authority source, Evidence current, object effect, object modal, F model, F.Choice choice, F.Live live)
    {
        Check(source, current, effect);
        if (!ReferenceEquals(source.Modal, modal) || modal == null) throw new O.Rejection(O.Code.WrongOwner, "Wrong modal reference");
        var key = source.Preflight(live.Repertoire);
        if (pending.TryGetValue(key.Command, out var old))
        {
            if (!ReferenceEquals(old.Source, source) || !ReferenceEquals(old.Model, model) || old.Choice.Signature != choice.Signature) throw new O.Rejection(O.Code.ConflictingOperation, "Invocation already sealed another conversion intent");
            model.Revalidate(old.Ticket, live); return old.Envelope;
        }
        var ticket = model.Prepare(key, choice, live); var r = ticket.Request;
        var envelope = new Envelope(r.Operation, source.Id, live.Hero, live.Repertoire, current.Definition, source.Kind, source.NativeEffectId,
            current.Create, r.NativeCircle, r.Circle, r.Input, r.Output, r.Cost, r.Signature);
        pending.Add(r.Operation, new Pending(source, model, key, choice, ticket, envelope)); source.Set(Status.Prepared); return envelope;
    }
    internal Acknowledgement Receive(Envelope command, Evidence current, object effect, F.Live live, bool network = false)
    {
        if (network) throw new O.Rejection(O.Code.PendingAtomicJournal, "Native network has no authenticated authority-envelope seam");
        if (command == null || MissingId(command.Command) || MissingId(command.Invocation) || !pending.TryGetValue(command.Command, out var p) || !command.SameAs(p.Envelope))
            throw new O.Rejection(command == null || MissingId(command.Command) || MissingId(command.Invocation) ? O.Code.MissingIdentity : O.Code.ConflictingOperation, "Unknown/conflicting in-band conversion envelope");
        Check(p.Source, current, effect);
        if (command.Hero != live.Hero || command.Repertoire != live.Repertoire) throw new O.Rejection(O.Code.WrongOwner, "Receiving owner/repertoire mismatch");
        try { p.Model.Revalidate(p.Ticket, live); }
        catch (O.Rejection) { Close(p.Source, Status.Rejected); throw; }
        p.Ack ??= new Acknowledgement(command.Command, command.Invocation); p.Source.Set(Status.AcknowledgedPreflight); return p.Ack;
    }
    internal void BlockCommit(Envelope command, Evidence current, object effect, F.Live live)
    { Receive(command, current, effect, live); var p = pending[command.Command]; p.Model.BlockNativeCommit(p.Ticket, live); }
    internal void Close(Authority source, Status reason = Status.Cancelled)
    {
        Known(source);
        if (reason is not (Status.Cancelled or Status.Rejected or Status.Expired)) throw new ArgumentException("Terminal rejection/cancel only");
        if (source.State is Status.Cancelled or Status.Rejected or Status.Expired) return;
        foreach (var p in pending.Values) if (ReferenceEquals(p.Source, source)) p.Model.Cancel(p.Preflight);
        source.Set(reason); // no native refund, completion, cleanup or resource mutation
    }
}
