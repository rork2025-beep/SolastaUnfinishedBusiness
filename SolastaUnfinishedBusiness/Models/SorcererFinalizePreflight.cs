using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

internal static class SorcererFinalizePreflight
{
    internal static bool IsSorcererDraft(string selected, string lastAssigned, bool ownsSorcerer,
        IEnumerable<MultipleSpellRepertoiresPlan.Transaction> transactions)
    {
        // A selected class is the current draft, not a historical/cancelled transaction.
        if (selected != null) return selected == "Sorcerer";
        if (transactions.Any(t => t.ClassId == "Sorcerer" && !t.IsClosed)) return true;
        if (lastAssigned != null) return lastAssigned == "Sorcerer";
        return ownsSorcerer; // missing current-class evidence remains fail-closed
    }
    internal enum Result { PassThrough, IncompleteLearning, PendingAtomicJournal }
    internal static Result Inspect(bool gate,bool bound,bool sorcererDraft,MultipleSpellRepertoiresPlan.Transaction transaction)
    {
        if(!gate||!bound||!sorcererDraft)return Result.PassThrough;
        if(transaction==null||transaction.ClassId!="Sorcerer"||transaction.IsClosed)return Result.IncompleteLearning;
        try { transaction.ValidateComplete(); }
        catch(InvalidOperationException) {return Result.IncompleteLearning;}
        return Result.PendingAtomicJournal; // complete choices still do not authorize native grants
    }
}
