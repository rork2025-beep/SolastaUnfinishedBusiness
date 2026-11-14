using System;
using System.Collections.Generic;
using F = SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using V = SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using T = SolastaUnfinishedBusiness.Models.FlexibleCastingSlotToSorceryPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;

namespace SolastaUnfinishedBusiness.Models;

// Executable commit adapter, shared by the game-field host and fake-field tests. One in-process
// journal over the EXISTING budget, not a second resource store or a durable save journal.
internal sealed class FlexibleCastingNativeCommitPlan(V registry)
{
    internal interface IHost
    {
        object Owner { get; }
        object Repertoire { get; }
        object Effect { get; }
        object Modal { get; }
        void RequireAccess(bool replay);
        V.Evidence Evidence();
        F.Live Read();
        Publication Prepare(T.Draft draft);
        void Log(Exception error);
    }
    // Every allocation/delegate capture is prepared before writes. ApplyFields must be only
    // assignments to validated references, with no observer, resource API, yield or user hook.
    internal sealed class Publication(Action validate, Action applyFields, Action assertCoherent, Action[] repertoire,
        Action[] sorcery, Action finishParent, Action[] parent, Action finishModal, Action recoverUi, Action release = null)
    {
        internal readonly Action Validate = validate, ApplyFields = applyFields, AssertCoherent = assertCoherent;
        internal readonly Action[] Repertoire = repertoire, Sorcery = sorcery, Parent = parent;
        internal readonly Action FinishParent = finishParent, FinishModal = finishModal, RecoverUi = recoverUi, Release = release;
    }
    internal sealed class Receipt(V.Envelope command, T.Draft draft, IHost host)
    {
        internal readonly V.Envelope Command = command;
        internal readonly T.Draft Draft = draft;
        internal readonly object Owner = host.Owner, Repertoire = host.Repertoire, Effect = host.Effect, Modal = host.Modal;
        internal O.Receipt ResourceReceipt { get; private set; }
        internal bool Committed { get; private set; }
        internal bool Indeterminate { get; private set; }
        internal bool ParentCompleted { get; private set; }
        internal bool ModalCompleted { get; private set; }
        internal int NotificationFailures { get; private set; }
        internal bool ParentAttempted, ModalAttempted;
        internal void Resource(O.Receipt receipt) => ResourceReceipt = receipt;
        internal void Confirm() => Committed = true;
        internal void Uncertain() => Indeterminate = true;
        internal void Failure() => NotificationFailures++;
        internal void ParentDone() => ParentCompleted = true;
        internal void ModalDone() => ModalCompleted = true;
    }
    private readonly Dictionary<string, Receipt> receipts = new(StringComparer.Ordinal);
    private bool busy;
    internal bool HasStarted(string command) => command != null && receipts.ContainsKey(command);
    internal bool TryReceipt(string command, out Receipt receipt) => receipts.TryGetValue(command, out receipt);
    private static void Match(V.Envelope command, IHost host, Receipt receipt)
    {
        if (!command.SameAs(receipt.Command) || !ReferenceEquals(host.Owner, receipt.Owner) || !ReferenceEquals(host.Repertoire, receipt.Repertoire) ||
            !ReferenceEquals(host.Effect, receipt.Effect) || !ReferenceEquals(host.Modal, receipt.Modal))
            throw new O.Rejection(O.Code.ConflictingOperation, "Native conversion identity/binding changed");
    }
    internal Receipt Execute(V.Envelope command, IHost host)
    {
        if (host == null || command == null || string.IsNullOrWhiteSpace(command.Command)) throw new O.Rejection(O.Code.MissingIdentity, "Native host/envelope required");
        if (busy) throw new O.Rejection(O.Code.PendingAtomicJournal, "Reentrant Flexible Casting commit rejected");
        busy = true; Receipt result = null; Publication publication = null; var writing = false;
        try
        {
            host.RequireAccess(true); // gate, Unity main thread and local transport also apply to retries
            if (receipts.TryGetValue(command.Command, out var old))
            {
                Match(command, host, old);
                if (!old.Committed) throw new O.Rejection(O.Code.PendingAtomicJournal, "Rejected/indeterminate native command cannot be replayed");
                if (host.Read().Rules != old.Draft.Before.Rules) throw new O.Rejection(O.Code.RuleChanged, "Conversion rules changed after recorded commit");
                return old; // historical receipt; never republish listeners/parent/UI or reauthorize a source
            }
            host.RequireAccess(false);
            var draft = registry.StandardSlots.Prepare(command, host.Evidence(), host.Effect, host.Read());
            if (draft.Status != T.State.Prepared) throw new O.Rejection(O.Code.PendingAtomicJournal, "Managed arithmetic receipt is not native authorization");
            if (!ReferenceEquals(host.Owner, draft.Intent.Source.Entry.Owner) || !ReferenceEquals(host.Repertoire, draft.Intent.Source.Entry.Repertoire) ||
                !ReferenceEquals(host.Modal, draft.Intent.Source.Modal) || !ReferenceEquals(host.Effect, draft.Intent.Source.Effect))
                throw new O.Rejection(O.Code.WrongOwner, "Native field host does not own issued source");
            result = new Receipt(command, draft, host); receipts.Add(command.Command, result);
            publication = host.Prepare(draft); // allocate dictionaries, notifications, parent/UI captures
            publication.Validate(); host.RequireAccess(false);
            var live = host.Read(); registry.Check(draft.Intent.Source, host.Evidence(), host.Effect);
            draft.Intent.Model.Revalidate(draft.Intent.Ticket, live);
            publication.Validate(); // last raw reference/value check, after all host/proof reads
            var evidence = host.Evidence();
            writing = true;
            result.Resource(registry.StandardSlots.ApplyNativeBudget(draft, evidence, host.Effect, live));
            publication.ApplyFields(); // native Sorcery, all views and AtWill parent counters; no callbacks
            publication.AssertCoherent();
            registry.StandardSlots.ConfirmNative(draft);
            registry.RecordNativeCommit(draft.Intent);
            result.Confirm(); // practical commit point: both resources, every view and SAME identity/receipt
            // All remaining exceptions are notification/UI failures of a COMMITTED operation.
            void Safe(Action action)
            {
                try { action(); } catch (Exception error) { result.Failure(); try { host.Log(error); } catch (Exception) {/* logging cannot undo a commit */} }
            }
            foreach (var notification in publication.Repertoire) Safe(notification);
            foreach (var notification in publication.Sorcery) Safe(notification);
            result.ParentAttempted = true;
            Safe(() => { publication.FinishParent(); result.ParentDone(); });
            if (result.ParentCompleted) foreach (var notification in publication.Parent) Safe(notification);
            result.ModalAttempted = true;
            Safe(() => { publication.FinishModal(); result.ModalDone(); });
            if (!result.ModalCompleted) Safe(publication.RecoverUi);
            return result;
        }
        catch (Exception error)
        {
            if (result?.Committed == true)
            {
                result.Failure(); try { host.Log(error); } catch (Exception) { }
                return result;
            }
            if (result != null)
            {
                if (writing) { result.Uncertain(); registry.StandardSlots.MarkNativeIndeterminate(result.Draft); }
                else { registry.Close(result.Draft.Intent.Source, V.Status.Rejected); result.Draft.Set(T.State.Rejected); }
            }
            throw; // no refund; OOM/process interruption during field writes is outside the practical guarantee
        }
        finally
        {
            try { publication?.Release?.Invoke(); }
            catch (Exception error) { if (result?.Committed == true) result.Failure(); try { host.Log(error); } catch (Exception) { } }
            finally { busy = false; }
        }
    }
}
