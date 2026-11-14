using System;
using System.Collections.Generic;
using System.Linq;
using F = SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using V = SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;

namespace SolastaUnfinishedBusiness.Models;

// Single-direction issued draft/managed journal. The legacy no-host native entry stays blocked.
// Stage13.7B.2 executes through FlexibleCastingNativeCommitPlan with a callback-free field host.
// Staging/managed tests must never be mistaken for Unity execution or native rollback tests.
internal sealed class FlexibleCastingSlotToSorceryPlan(V registry)
{
    internal const bool NativeBoundaryAvailable = false; // legacy no-host API only
    internal enum State { Prepared, Rejected, Cancelled, ManagedCommitted, ManagedIndeterminate, NativeApplying, NativeCommitted, NativeIndeterminate }
    internal enum Checkpoint { BeforeStaging, DebitStaged, CreditStaged, BeforePublish, AfterManagedPublish }
    internal sealed class Draft(V.StandardSlotIntent intent, F.Live before, F.Live after)
    {
        internal readonly V.StandardSlotIntent Intent = intent;
        internal readonly F.Live Before = before, After = after;
        internal State Status { get; private set; } = State.Prepared;
        internal O.Receipt Receipt { get; private set; }
        internal bool DeliveryPending { get; private set; }
        internal bool Busy;
        internal int SlotCircle => Intent.Ticket.Request.Circle;
        internal int SlotDebit => 1;
        internal int SorceryGain => Intent.Ticket.Preview.OutputAmount;
        internal int NativeUsedSorceryAfter => After.SorceryMaximum - After.SorceryRemaining;
        // Native CreateSorceryPoints reports remaining (not gained) points, with feature=null.
        internal int ExpectedSorceryEventValue => After.SorceryRemaining;
        internal bool NativeCompleted => Status == State.NativeCommitted;
        internal bool ParentCompleted => Intent.Source.ParentCompleted;
        internal void Set(State state) => Status = state;
        internal void NativeReceipt(O.Receipt receipt)=>Receipt=receipt;
        internal void Confirm(O.Receipt receipt) { Receipt = receipt; Status = State.ManagedCommitted; }
        internal void PendingDelivery(bool value) => DeliveryPending = value;
    }
    private readonly Dictionary<string, Draft> drafts = new(StringComparer.Ordinal);
    private static O.Rejection Reject(O.Code code, string reason) => new(code, "13.7B.1/" + reason);
    private static F.Live Project(F.Live before, F.Ticket ticket)
    {
        var used = before.Used.ToArray(); used[ticket.Request.Circle - 1] = checked(used[ticket.Request.Circle - 1] + 1);
        var remaining = checked(before.SorceryRemaining + ticket.Preview.OutputAmount);
        if (remaining > before.SorceryMaximum) throw Reject(O.Code.InvalidResult, "Sorcery cap violated");
        return new F.Live(before.Hero, before.Repertoire, before.Casting, before.Actual, before.Effective, before.Compressed, before.Eligible,
            before.Rules, before.SorceryMaximum, remaining, before.Capacities, used, before.PactUsed, before.PointPools, before.PointMaximum,
            before.PointRemaining, before.Costs, before.Conditions, before.PreferPact);
    }
    private void Known(Draft draft)
    {
        if (draft == null || !drafts.TryGetValue(draft.Intent.Command.Command, out var own) || !ReferenceEquals(own, draft))
            throw Reject(O.Code.MissingIdentity, "foreign/missing draft");
    }
    private void Current(Draft draft, V.Evidence evidence, object effect, F.Live live)
    {
        Known(draft); registry.Check(draft.Intent.Source, evidence, effect);
        if (draft.Status == State.ManagedCommitted)
            draft.Intent.Model.RevalidateManagedReceipt(draft.Intent.Ticket, draft.Receipt, live, draft.After.Signature);
        else
        {
            if (draft.Status != State.Prepared) throw Reject(draft.Status == State.ManagedIndeterminate ? O.Code.PendingAtomicJournal : O.Code.StaleOperation, "closed or uncertain draft");
            try { draft.Intent.Model.Revalidate(draft.Intent.Ticket, live); }
            catch (O.Rejection)
            {
                // Match the issued-envelope receiver: once a trusted live proof is stale,
                // restoring old settings/resources must not resurrect the same pending command.
                registry.Close(draft.Intent.Source, V.Status.Rejected);
                draft.Set(State.Rejected);
                throw;
            }
        }
    }
    internal Draft Prepare(V.Envelope command, V.Evidence current, object effect, F.Live live, bool network = false)
    {
        if (network) throw Reject(O.Code.PendingAtomicJournal, "network transport unavailable");
        if (command == null || string.IsNullOrWhiteSpace(command.Command)) throw Reject(O.Code.MissingIdentity, "command envelope");
        if (drafts.TryGetValue(command.Command, out var previous))
        {
            if (!command.SameAs(previous.Intent.Command)) throw Reject(O.Code.ConflictingOperation, "command identity changed");
            Current(previous, current, effect, live); return previous;
        }
        var intent = registry.InspectStandardSlot(command, current, effect, live);
        var draft = new Draft(intent, live, Project(live, intent.Ticket)); drafts.Add(command.Command, draft); return draft;
    }
    // Executable arithmetic only. This NEVER updates native fields/projections, invokes native
    // callbacks or completes a parent. The caller must not use this receipt as native authority.
    // Fault checkpoints precede resource mutation; AfterManagedPublish represents LOST DELIVERY
    // of an already committed model receipt, not a failed conversion to roll back.
    internal Draft CommitManaged(Draft draft, Func<V.Evidence> current, object effect, Func<F.Live> read, Action<Checkpoint> checkpoint = null)
    {
        Known(draft);
        if (draft.Busy) throw Reject(O.Code.PendingAtomicJournal, "reentrant commit");
        Current(draft, current(), effect, read());
        if (draft.Status == State.ManagedCommitted) { draft.PendingDelivery(false); return draft; }
        draft.Busy = true; var publishing = false;
        try
        {
            checkpoint?.Invoke(Checkpoint.BeforeStaging);
            checkpoint?.Invoke(Checkpoint.DebitStaged); // only the immutable After projection, no debit
            checkpoint?.Invoke(Checkpoint.CreditStaged); // only the immutable After projection, no gain
            checkpoint?.Invoke(Checkpoint.BeforePublish);
            var live = read(); Current(draft, current(), effect, live); // fresh source/rules/resources immediately before arithmetic
            publishing = true;
            var receipt = draft.Intent.Model.CommitManagedStandardSlot(draft.Intent.Ticket, live);
            draft.Confirm(receipt); // model commit point: both existing-ledger deltas and its receipt exist
            try { checkpoint?.Invoke(Checkpoint.AfterManagedPublish); }
            catch (Exception) { draft.PendingDelivery(true); } // committed model result, never false "failed/refunded"
            return draft;
        }
        catch (Exception)
        {
            if (publishing)
                draft.Set(State.ManagedIndeterminate); // no invented rollback; never retry an uncertain legacy write
            else if (draft.Status != State.Cancelled)
            {
                registry.Close(draft.Intent.Source, V.Status.Rejected); draft.Set(State.Rejected);
            }
            throw;
        }
        finally { draft.Busy = false; }
    }
    internal void Cancel(Draft draft)
    {
        Known(draft);
        if (draft.Status is State.ManagedCommitted or State.ManagedIndeterminate or State.NativeApplying or State.NativeCommitted or State.NativeIndeterminate)
            throw Reject(O.Code.PendingAtomicJournal, "no native refund/committed rollback");
        if (draft.Status is State.Cancelled or State.Rejected) return;
        registry.Close(draft.Intent.Source, V.Status.Cancelled); draft.Set(State.Cancelled);
    }
    internal O.Receipt ApplyNativeBudget(Draft draft,V.Evidence current,object effect,F.Live live)
    {
        Current(draft,current,effect,live);
        if(draft.Status!=State.Prepared)throw Reject(O.Code.PendingAtomicJournal,"Native commit requires fresh prepared ticket");
        draft.Set(State.NativeApplying);
        var receipt=draft.Intent.Model.CommitManagedStandardSlot(draft.Intent.Ticket,live);
        draft.NativeReceipt(receipt);return receipt;
    }
    internal void ConfirmNative(Draft draft){Known(draft);draft.Intent.Model.CompleteNativeTicket(draft.Intent.Ticket,draft.Receipt);draft.Set(State.NativeCommitted);}
    internal void MarkNativeIndeterminate(Draft draft){Known(draft);draft.Set(State.NativeIndeterminate);}
    // Legacy no-host entry remains blocked. Only the explicit native field-host executor can commit.
    internal void CommitNative(Draft draft, V.Evidence current, object effect, F.Live live, bool network = false)
    {
        if (network) throw Reject(O.Code.PendingAtomicJournal, "network transport unavailable");
        Current(draft, current, effect, live);
        throw Reject(O.Code.PendingAtomicJournal,
            "native slot debit publishes RepertoireRefreshed before Sorcery credit; no verified callback/parent atomic journal. Neither native side was called");
    }
}
