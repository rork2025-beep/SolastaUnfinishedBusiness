using System;
using System.Linq;
using M = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;

namespace SolastaUnfinishedBusiness.Models;

// Same lifecycle is executed by the QA Harmony bridge and controlled-native fixtures.
// This is an in-process editor journal, never authority for a loaded/campaign hero.
internal sealed class QaEditorFinalizationPlan(object owner, object manager, string rules, Func<string> issue)
{
    internal enum Phase { Issued, Executing, NativeReturned, Synchronizing, Committed, Cancelled, Indeterminate }
    internal sealed class Ticket(string id, int target)
    {
        internal readonly string Id = id;
        internal readonly int Target = target;
        internal Phase State = Phase.Issued;
        internal Prepared Draft;
        internal string Failure;
        internal int NotificationFailures;
    }
    internal sealed class Prepared(string fingerprint, M.Transaction spells, C.Result progression, Action validateBeforeNative, Action verifyNative)
    {
        internal readonly string Fingerprint = fingerprint;
        internal readonly M.Transaction Spells = spells;
        internal readonly C.Result Progression = progression;
        internal readonly Action ValidateBeforeNative = validateBeforeNative, VerifyNative = verifyNative;
    }
    internal sealed class Publication(Action validate, Action apply, Action verify, Action[] notifications)
    {
        internal readonly Action Validate = validate, Apply = apply, Verify = verify;
        internal readonly Action[] Notifications = notifications;
    }
    internal interface IHost
    {
        object Owner { get; }
        object Manager { get; }
        string Rules { get; }
        string IntentFingerprint { get; }
        int Actual { get; }
        void RequireEditor();
        Prepared Prepare(Ticket ticket);
        Publication PreparePublication(Ticket ticket);
        void Log(Exception error);
    }
    internal int ConfirmedActual { get; private set; }
    internal Ticket Current { get; private set; }
    private bool busy;
    private readonly System.Collections.Generic.HashSet<string> issued = new(StringComparer.Ordinal);
    private void Check(IHost host)
    {
        if (!ReferenceEquals(owner, host.Owner) || !ReferenceEquals(manager, host.Manager)) throw new InvalidOperationException("Wrong exact QA editor hero/manager");
        host.RequireEditor();
        if (host.Rules != rules) throw new InvalidOperationException("QA editor settings changed; restart required");
    }
    internal Ticket Issue(IHost host, string selectedClass)
    {
        Check(host);
        if (selectedClass != "Sorcerer" || ConfirmedActual >= 2) throw new InvalidOperationException("C.2 supports Sorcerer0->1->2 only; second caster requires native admission/learning transition");
        if (host.Actual != ConfirmedActual) throw new InvalidOperationException("Native class assignment did not start at the confirmed actual history");
        if (Current?.State == Phase.Indeterminate) throw new InvalidOperationException("Indeterminate native finalization cannot be retried");
        if (Current?.State == Phase.Issued) return Current; // same held native assignment, no replacement nonce
        if (Current != null && Current.State is not (Phase.Cancelled or Phase.Committed)) throw new InvalidOperationException("Previous QA advancement is still executing");
        var id = issue();
        if (string.IsNullOrWhiteSpace(id) || !issued.Add(id)) throw new InvalidOperationException("Invalid/reused QA advancement identity");
        return Current = new Ticket(id, ConfirmedActual + 1);
    }
    // True authorizes the ONE original native finalizer. False is an identical committed retry.
    internal bool Begin(IHost host, Ticket ticket)
    {
        Check(host); Known(ticket);
        if (busy) throw new InvalidOperationException("Reentrant QA finalization");
        if (host.Actual != ticket.Target) throw new InvalidOperationException("Changed native class/history target");
        if (ticket.State == Phase.Committed)
        {
            if (host.IntentFingerprint != ticket.Draft.Fingerprint) throw new InvalidOperationException("Conflicting retry changed the confirmed draft");
            return false;
        }
        if (ticket.State != Phase.Issued) throw new InvalidOperationException("Cancelled/indeterminate or repeated in-flight QA finalization");
        var prepared = host.Prepare(ticket); // no native/resource grants, may reject incomplete UI choices
        if (prepared.Fingerprint != host.IntentFingerprint) throw new InvalidOperationException("Draft changed while preparing native authorization");
        prepared.Spells.ValidateComplete();
        if (!prepared.Progression.IsValid || prepared.Progression.Input.ActualCharacterLevel != ticket.Target ||
            prepared.Progression.Classes.Count != 1 || prepared.Progression.Classes.Single().ClassId != "Sorcerer" ||
            prepared.Spells.ActualLevel != ticket.Target || prepared.Spells.ClassId != "Sorcerer")
            throw new InvalidOperationException("Foreign class progression/learning plan");
        prepared.ValidateBeforeNative(); Check(host);
        ticket.Draft = prepared;
        busy = true; ticket.State = Phase.Executing; // BEFORE the first UB prefix/native grant
        return true;
    }
    internal void NativeReturned(object actualOwner, Ticket ticket)
    {
        Known(ticket);
        if (!ReferenceEquals(owner, actualOwner)) throw new InvalidOperationException("Wrong native return owner");
        if (ticket.State == Phase.Committed || busy && ticket.State == Phase.NativeReturned) return;
        if (!busy || ticket.State != Phase.Executing)
            throw new InvalidOperationException("Unproven native finalizer return");
        ticket.State = Phase.NativeReturned;
    }
    internal void Finish(IHost host, Ticket ticket, Exception nativeError, bool originalRan)
    {
        Known(ticket);
        if (ticket.State == Phase.Committed) return; // duplicate callback cannot publish twice
        if (!busy) return; // incomplete preflight never started native work
        try
        {
            if (nativeError != null || !originalRan || ticket.State != Phase.NativeReturned)
                throw new InvalidOperationException("Native/UB finalization did not return successfully", nativeError);
            Check(host);
            if (host.Actual != ticket.Target) throw new InvalidOperationException("Native result changed actual class/history");
            ticket.Draft.VerifyNative();
            var packet = host.PreparePublication(ticket); // preallocate all native views before synchronization
            packet.Validate();
            ticket.State = Phase.Synchronizing;
            packet.Apply(); packet.Verify();
            ConfirmedActual = ticket.Target; ticket.State = Phase.Committed; // receipt BEFORE our observers
            foreach (var notification in packet.Notifications)
                try { notification(); } catch (Exception e) { ticket.NotificationFailures++; try { host.Log(e); } catch (Exception) { } }
        }
        catch (Exception e)
        {
            ticket.State = Phase.Indeterminate; ticket.Failure = e.ToString();
            try { host.Log(e); } catch (Exception) { }
            throw; // no post-grant refund or automatic native re-execution
        }
        finally { busy = false; }
    }
    internal void Cancel(Ticket ticket)
    {
        Known(ticket);
        if (ticket.State == Phase.Committed) return;
        if (busy || ticket.State == Phase.Indeterminate) throw new InvalidOperationException("Cannot cancel/rollback partially executed native grants");
        ticket.State = Phase.Cancelled;
    }
    private void Known(Ticket ticket)
    {
        if (ticket == null || !ReferenceEquals(Current, ticket)) throw new InvalidOperationException("Unissued/stale QA advancement");
    }
}
