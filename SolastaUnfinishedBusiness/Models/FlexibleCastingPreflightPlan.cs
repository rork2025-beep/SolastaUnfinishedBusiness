using System;
using System.Collections.Generic;
using System.Linq;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;

namespace SolastaUnfinishedBusiness.Models;

// 13.7A: ephemeral pre-dispatch identity and proof, not a native spend/persistence journal.
internal sealed class FlexibleCastingPreflightPlan
{
    internal enum SourceProofKind { RegisteredEffect, TrustedCampaignInvocation }
    internal sealed class Authority(string hero, string effect, string repertoire, bool createSlot, bool ownedEffect,SourceProofKind sourceKind=SourceProofKind.RegisteredEffect)
    {
        internal SourceProofKind SourceKind { get; }=sourceKind;
        internal string Hero { get; } = hero;
        internal string Effect { get; } = effect;
        internal string Repertoire { get; } = repertoire;
        internal bool CreateSlot { get; } = createSlot;
        internal bool OwnedEffect { get; } = ownedEffect;
        // One conversion command per verified source-power invocation; retries never mint a GUID.
        internal string Command => "flex:"+Hero+":"+Effect+":"+(CreateSlot?"slot":"sorcery");
    }
    internal sealed class Live(string hero,string repertoire,string casting,int actual,int effective,bool compressed,
        bool eligible,string rules,int sorceryMaximum,int sorceryRemaining,IEnumerable<int> capacities,
        IEnumerable<int> used,int pactUsed,int pointPools,int pointMaximum,int pointRemaining,
        IEnumerable<int> creationCosts,IEnumerable<string> conditions,bool preferPact)
    {
        internal string Hero { get; }=hero;internal string Repertoire { get; }=repertoire;internal string Casting { get; }=casting;
        internal int Actual { get; }=actual;internal int Effective { get; }=effective;internal bool Compressed { get; }=compressed;
        internal bool Eligible { get; }=eligible;internal string Rules { get; }=rules;
        internal int SorceryMaximum { get; }=sorceryMaximum;internal int SorceryRemaining { get; }=sorceryRemaining;
        internal IReadOnlyList<int> Capacities { get; }=Array.AsReadOnly(capacities.ToArray());internal IReadOnlyList<int> Used { get; }=Array.AsReadOnly(used.ToArray());
        internal int PactUsed { get; }=pactUsed;internal int PointPools { get; }=pointPools;internal int PointMaximum { get; }=pointMaximum;internal int PointRemaining { get; }=pointRemaining;
        internal IReadOnlyList<int> Costs { get; }=Array.AsReadOnly(creationCosts.ToArray());
        internal IReadOnlyList<string> Conditions { get; }=Array.AsReadOnly(conditions.OrderBy(x=>x,StringComparer.Ordinal).ToArray());
        internal bool PreferPact { get; }=preferPact;
        internal string Signature=>string.Join("|",new[]{Hero,Repertoire,Casting,Actual.ToString(),Effective.ToString(),Compressed.ToString(),Eligible.ToString(),Rules,SorceryMaximum.ToString(),SorceryRemaining.ToString(),
            string.Join(",",Capacities),string.Join(",",Used),PactUsed.ToString(),PointPools.ToString(),PointMaximum.ToString(),PointRemaining.ToString(),string.Join(",",Costs),string.Join(",",Conditions),PreferPact.ToString()});
    }
    internal sealed class Choice(int nativeCircle,bool createSlot,bool shift,bool preferPact,O.Domain? requestedInput=null,int? requestedCircle=null,int? requestedCost=null)
    {
        internal int NativeCircle { get; }=nativeCircle;internal bool CreateSlot { get; }=createSlot;
        internal bool Shift { get; }=shift;internal bool PreferPact { get; }=preferPact;
        internal O.Domain? RequestedInput { get; }=requestedInput;internal int? RequestedCircle { get; }=requestedCircle;internal int? RequestedCost { get; }=requestedCost;
        internal string Signature=>string.Join("/",new[]{NativeCircle.ToString(),CreateSlot.ToString(),Shift.ToString(),PreferPact.ToString(),RequestedInput?.ToString(),RequestedCircle?.ToString(),RequestedCost?.ToString()});
    }
    internal sealed class Ticket(Authority authority,Choice choice,O.Request request,O.Receipt preview,string nativeSignature)
    {
        internal Authority Authority { get; }=authority;internal Choice Choice { get; }=choice;internal O.Request Request { get; }=request;
        internal O.Receipt Preview { get; }=preview;internal string NativeSignature { get; }=nativeSignature;
        internal void RequireStandardSlotToSorcery()
        {
            if(Authority.CreateSlot||Request.OperationKind!=O.Kind.SlotToSorcery||Request.Input!=O.Domain.StandardSlots||Request.Output!=O.Domain.SorceryPoints)
                throw new O.Rejection(O.Code.WrongDomain,"13.7B.1 accepts only ordinary standard slot -> Sorcery Points");
            if(Request.Circle is <1 or >5||Request.Circle!=Request.NativeCircle||Request.Quantity!=1||Request.Cost!=1)
                throw new O.Rejection(O.Code.InvalidCost,"One verified actual circle1..5 standard slot required");
        }
        internal string FundingIdentity=>Authority.CreateSlot?Request.Operation+"/AdditionalSpellSlot"+Request.NativeCircle:null;
    }
    private readonly string hero,rules;
    private readonly O operations;
    private readonly O.Counter sorcery;
    private readonly S.Tables tables;
    private readonly Dictionary<string,Ticket> tickets=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> attempts=new(StringComparer.Ordinal);
    private readonly HashSet<string> cancelled=new(StringComparer.Ordinal);
    internal FlexibleCastingPreflightPlan(string hero,string rules,O operations,O.Counter sorcery,S.Tables tables)
    {this.hero=hero;this.rules=rules;this.operations=operations;this.sorcery=sorcery;this.tables=tables;}
    private void Validate(Authority authority,Live live)
    {
        if(authority==null||!authority.OwnedEffect||string.IsNullOrWhiteSpace(authority.Effect)||authority.Effect=="0")throw new O.Rejection(O.Code.MissingIdentity,"Verified native source effect required");
        if(authority.Hero!=hero||live.Hero!=hero||authority.Repertoire!=live.Repertoire||live.Casting!="CastSpellSorcerer")throw new O.Rejection(O.Code.WrongOwner,"Actual original-hero Sorcerer repertoire required");
        if(!live.Eligible||live.Actual<2||live.Actual>(live.Compressed?12:20)||live.Effective!=(live.Compressed?FighterProgressionPlan<int>.EffectiveLevel(live.Actual):live.Actual))throw new O.Rejection(O.Code.Ineligible,"Flexible Casting native authority/class snapshot");
        if(live.Rules!=rules)throw new O.Rejection(O.Code.RuleChanged,"Native Flexible Casting rules/cost/modifier snapshot changed");
        if(live.Costs.Count!=5||live.Costs.Any(x=>x<=0)||live.Capacities.Count!=9||live.Used.Count!=9)throw new O.Rejection(O.Code.InvalidCost,"Malformed native resource/cost view");
        if(live.SorceryMaximum!=sorcery.Maximum||live.SorceryRemaining!=sorcery.Remaining)throw new O.Rejection(O.Code.StaleOperation,"Native Sorcery balance differs from the authoritative counter");
        var resources=operations.Resources;
        if(resources.Standard.Current.Mode==S.ResourceMode.SpellPoints&&(live.PointPools!=1||live.PointMaximum!=resources.Standard.Current.PointMaximum||live.PointRemaining!=resources.Standard.RemainingPoints))
            throw new O.Rejection(O.Code.StaleOperation,"Native standard Spell Points view/pool identity changed");
        for(var c=1;c<=9;c++)
        {
            var capacity=resources.Standard.Current.Slots[c-1]+resources.Standard.TemporarySlots(c);
            var used=capacity-resources.Standard.RemainingSlots(c);
            if(c==resources.Pact.Current.Circle){capacity+=resources.Pact.Current.Slots;used+=resources.Pact.Used;}
            if(live.Capacities[c-1]!=capacity||live.Used[c-1]!=used)throw new O.Rejection(O.Code.StaleOperation,"Native repertoire projection differs at circle"+c);
        }
        if(live.PactUsed!=resources.Pact.Used)throw new O.Rejection(O.Code.StaleOperation,"Native Pact view differs");
    }
    internal Ticket Prepare(Authority authority,Choice choice,Live live)
    {
        Validate(authority,live);
        if(cancelled.Contains(authority.Command))throw new O.Rejection(O.Code.StaleOperation,"Cancelled conversion command");
        if(attempts.TryGetValue(authority.Command,out var intent)&&intent!=choice.Signature)throw new O.Rejection(O.Code.ConflictingOperation,"Command retry changed its original selection");
        attempts[authority.Command]=choice.Signature;
        if(tickets.TryGetValue(authority.Command,out var old))
        {
            if(old.Choice.Signature!=choice.Signature||old.Authority.Repertoire!=authority.Repertoire||old.Authority.Effect!=authority.Effect)throw new O.Rejection(O.Code.ConflictingOperation,"Conversion identity reused with different contents");
            Revalidate(old,live);return old;
        }
        if(tickets.Any(t=>!cancelled.Contains(t.Key)))throw new O.Rejection(O.Code.PendingAtomicJournal,"Cancel the pending conversion before opening another command");
        if(choice.CreateSlot!=authority.CreateSlot)throw new O.Rejection(O.Code.ConflictingOperation,"Wrong conversion direction");
        if(choice.NativeCircle is <1 or >5)throw new O.Rejection(O.Code.InvalidCircle,"Native Flexible Casting modal exposes circles1..5");
        if(choice.PreferPact!=live.PreferPact)throw new O.Rejection(O.Code.RuleChanged,"Source selection differs from current native priority");
        var source=SpellResourceConsumerCatalog.FlexibleCasting(authority.CreateSlot);
        var standard=operations.Resources.Standard.Current.Mode==S.ResourceMode.SpellPoints?O.Domain.SpellPoints:O.Domain.StandardSlots;
        var selected=authority.CreateSlot?null:SpellResourceConsumerCatalog.ResolveFlexibleInput(operations.Resources,choice.NativeCircle,choice.PreferPact,choice.Shift,false);
        var input=authority.CreateSlot?O.Domain.SorceryPoints:selected.Domain;var output=authority.CreateSlot?standard:O.Domain.SorceryPoints;
        var circle=authority.CreateSlot?choice.NativeCircle:selected.Circle;
        var cost=authority.CreateSlot?live.Costs[circle-1]:input==O.Domain.SpellPoints?tables.Cost(circle):1;
        if(choice.RequestedInput!=null&&choice.RequestedInput!=input)throw new O.Rejection(O.Code.WrongDomain,"Unverified requested input (including Arcanum)");
        if(choice.RequestedCircle!=null&&choice.RequestedCircle!=circle)throw new O.Rejection(O.Code.InvalidCircle,"Native consumed circle differs");
        if(choice.RequestedCost!=null&&choice.RequestedCost!=cost)throw new O.Rejection(O.Code.InvalidCost,"Native effective cost differs");
        var amount=authority.CreateSlot?(output==O.Domain.SpellPoints?tables.Cost(circle):1):Math.Min(choice.NativeCircle,sorcery.Maximum-sorcery.Remaining);
        var proof=new O.Proof(source.Id,source.Definition,source.Revision,live.Repertoire,null,authority.CreateSlot?sorcery.Id:null,
            authority.CreateSlot?Array.Empty<string>():new[]{sorcery.Id},live.Effective,authority.CreateSlot?cost:0,true,
            input==O.Domain.PactSlots,selected?.Lossy??false,resolvedInput:input,resolvedCircle:circle,resolvedNativeCircle:choice.NativeCircle);
        operations.RefreshProofs(SpellResourceConsumerCatalog.Revision,new[]{proof});
        var request=new O.Request(authority.Command,hero,source.Id,source.Definition,source.Revision,source.Operation,input,output,circle,choice.NativeCircle,1,cost,amount,operations.State);
        var preview=operations.Prepare(request);
        var ticket=new Ticket(authority,choice,request,preview,live.Signature);tickets.Add(authority.Command,ticket);return ticket;
    }
    internal void Revalidate(Ticket ticket,Live live)
    {
        Validate(ticket.Authority,live);
        if(!tickets.TryGetValue(ticket.Request.Operation,out var own)||!ReferenceEquals(ticket,own)||cancelled.Contains(ticket.Request.Operation))throw new O.Rejection(O.Code.StaleOperation,"Missing/cancelled preflight ticket");
        if(live.Signature!=ticket.NativeSignature||operations.State!=ticket.Request.ExpectedState)throw new O.Rejection(O.Code.StaleOperation,"Preflight proof/resources/conditions changed before dispatch");
        operations.Prepare(ticket.Request); // same immutable request, never CommitManaged
    }
    internal void Cancel(Authority authority)
    {
        if(authority.Hero!=hero)throw new O.Rejection(O.Code.WrongOwner,"Cancel owner");
        if(tickets.ContainsKey(authority.Command))operations.Cancel(authority.Command);
        cancelled.Add(authority.Command);
    }
    // Model arithmetic only; this does not publish a native projection, event, parent action or acknowledgement.
    internal O.Receipt CommitManagedStandardSlot(Ticket ticket,Live live)
    {ticket.RequireStandardSlotToSorcery();Revalidate(ticket,live);return operations.CommitManaged(ticket.Request);}
    internal void RevalidateManagedReceipt(Ticket ticket,O.Receipt receipt,Live live,string expectedProjection)
    {
        Validate(ticket.Authority,live);
        if(!tickets.TryGetValue(ticket.Request.Operation,out var own)||!ReferenceEquals(own,ticket)||receipt.Operation!=ticket.Request.Operation||
            operations.State!=receipt.After||live.Signature!=expectedProjection)
            throw new O.Rejection(O.Code.StaleOperation,"Managed receipt/projection changed; never perform another debit");
    }
    internal void BlockNativeCommit(Ticket ticket,Live live)
    {Revalidate(ticket,live);operations.CommitNative(ticket.Request);}
}
