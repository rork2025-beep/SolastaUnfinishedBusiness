using System;

namespace SolastaUnfinishedBusiness.Models;

internal static class SorcererFinalizePreflight
{
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
