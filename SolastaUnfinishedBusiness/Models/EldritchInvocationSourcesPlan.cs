using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using P = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;

namespace SolastaUnfinishedBusiness.Models;

// Stage13.5E-1 backend only. Proofs/claims must come from an atomic owner journal;
// native inventory reconstruction and native publication are deliberately not implemented here.
internal sealed class EldritchInvocationSourcesPlan
{
    internal enum Origin { Warlock, EldritchAdept }
    internal enum Kind { PaidSpell, Passive, Unsupported }
    internal enum Code
    {
        UnknownInvocation, UnsupportedOption, WrongOwner, MissingPrerequisite, MissingRepertoire,
        InvalidSpell, InvalidCircle, Exhausted, AlreadyUsed, MissingActionIdentity, ConflictingAction,
        PendingAtomicJournal, CleanupRequired, DuplicateOrigin, ClosedSelection
    }
    internal sealed class Rejection(Code code, string detail) : InvalidOperationException(code + ": " + detail)
    { internal Code Diagnostic { get; } = code; }
    internal sealed class Definition(string id, Kind kind, string spell, int circle, string feature,
        int requiredLevel, string requiredPact, string requiredKnownSpell, bool consumesSlot, bool longRestRecharge)
    {
        internal string Id { get; } = id;
        internal Kind Type { get; } = kind;
        internal string Spell { get; } = spell;
        internal int Circle { get; } = circle;
        internal string Feature { get; } = feature;
        internal int RequiredLevel { get; } = requiredLevel;
        internal string RequiredPact { get; } = requiredPact;
        internal string RequiredKnownSpell { get; } = requiredKnownSpell;
        internal bool ConsumesSlot { get; } = consumesSlot;
        internal bool LongRestRecharge { get; } = longRestRecharge;
    }
    internal sealed class Claim(string id, Origin origin, string invocation)
    {
        internal string Id { get; } = id; // original class event or feat acquisition, never selected class tag
        internal Origin Source { get; } = origin;
        internal string Invocation { get; } = invocation;
    }
    internal sealed class Owner
    {
        internal string Hero { get; }
        internal int CharacterLevel { get; }
        internal int WarlockPrerequisiteLevel { get; }
        internal string CastingFeature { get; }
        internal string Ability { get; }
        internal int SaveDc { get; }
        internal bool NativeValidatorsVerified { get; }
        internal IReadOnlyList<string> Pacts { get; }
        internal IReadOnlyList<string> KnownSpells { get; }
        internal IReadOnlyList<Claim> Claims { get; }
        internal Owner(string hero, int characterLevel, int warlockPrerequisiteLevel,
            string castingFeature, string ability, int saveDc, bool nativeValidatorsVerified,
            IEnumerable<Claim> claims, IEnumerable<string> pacts = null, IEnumerable<string> knownSpells = null)
        {
            if (string.IsNullOrWhiteSpace(hero) || characterLevel is < 0 or > 20 ||
                warlockPrerequisiteLevel is < 0 or > 20 || saveDc < 0) throw new ArgumentException("Invalid owner proof.");
            Hero=hero; CharacterLevel=characterLevel; WarlockPrerequisiteLevel=warlockPrerequisiteLevel;
            CastingFeature=castingFeature; Ability=ability; SaveDc=saveDc; NativeValidatorsVerified=nativeValidatorsVerified;
            Claims=Array.AsReadOnly(claims.ToArray());
            if (Claims.Any(c=>c==null || string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Invocation) || !Enum.IsDefined(typeof(Origin),c.Source)) ||
                Claims.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=Claims.Count)
                throw new Rejection(Code.DuplicateOrigin,hero);
            Pacts=Array.AsReadOnly((pacts??Array.Empty<string>()).ToArray());
            KnownSpells=Array.AsReadOnly((knownSpells??Array.Empty<string>()).ToArray());
        }
    }
    private readonly string invocationCastingFeature;
    private readonly string invocationAbility;
    internal IReadOnlyDictionary<string,Definition> Definitions { get; }
    internal EldritchInvocationSourcesPlan(IEnumerable<Definition> definitions,string castingFeature,string ability)
    {
        if(string.IsNullOrWhiteSpace(castingFeature) || string.IsNullOrWhiteSpace(ability))throw new ArgumentException("Native invocation-access feature/ability required.");
        invocationCastingFeature=castingFeature;invocationAbility=ability;
        var all=definitions.ToArray();
        if(all.Any(d=>d==null || string.IsNullOrWhiteSpace(d.Id) || d.RequiredLevel is < 0 or > 20 ||
                      !Enum.IsDefined(typeof(Kind),d.Type) || d.Type==Kind.PaidSpell &&
                      (!d.ConsumesSlot || !d.LongRestRecharge || string.IsNullOrWhiteSpace(d.Spell) || d.Circle is < 1 or > 5 || d.Feature!=null) ||
                      d.Type==Kind.Passive && (d.Spell!=null || string.IsNullOrWhiteSpace(d.Feature) || d.ConsumesSlot || d.LongRestRecharge)) ||
            all.Select(d=>d.Id).Distinct(StringComparer.Ordinal).Count()!=all.Length)
            throw new ArgumentException("Invalid registered invocation definitions.");
        Definitions=new ReadOnlyDictionary<string,Definition>(all.ToDictionary(d=>d.Id,StringComparer.Ordinal));
    }
    internal Definition Require(string id)=>id!=null && Definitions.TryGetValue(id,out var d)?d:throw new Rejection(Code.UnknownInvocation,id);
    internal void Prerequisites(Owner owner, Definition d, Origin origin)
    {
        if(!owner.NativeValidatorsVerified)throw new Rejection(Code.MissingPrerequisite,d.Id+"/unverified native validators");
        if(d.Type==Kind.Unsupported)throw new Rejection(Code.UnsupportedOption,d.Id);
        // Installed GUI skips the Warlock-level test for RequiredLevel<=1. Adept itself requires character level2.
        if(origin==Origin.EldritchAdept && owner.CharacterLevel<2 || origin==Origin.Warlock && owner.WarlockPrerequisiteLevel<1 ||
            d.RequiredLevel>1 && owner.WarlockPrerequisiteLevel<d.RequiredLevel ||
            d.RequiredPact!=null && !owner.Pacts.Contains(d.RequiredPact) ||
            d.RequiredKnownSpell!=null && !owner.KnownSpells.Contains(d.RequiredKnownSpell))
            throw new Rejection(Code.MissingPrerequisite,d.Id);
        if(d.Type==Kind.PaidSpell && (string.IsNullOrWhiteSpace(owner.CastingFeature) || string.IsNullOrWhiteSpace(owner.Ability)))
            throw new Rejection(Code.MissingRepertoire,d.Id+"/native HasAccessToInvocations repertoire required");
        if(d.Type==Kind.PaidSpell && (owner.CastingFeature!=invocationCastingFeature || owner.Ability!=invocationAbility))
            throw new Rejection(Code.WrongOwner,d.Id+"/not the registered invocation-access feature/ability");
    }
    internal Definition Owned(Owner owner,string origin,string invocation)
    {
        var claim=owner.Claims.SingleOrDefault(c=>c.Id==origin && c.Invocation==invocation)
            ??throw new Rejection(Code.WrongOwner,origin+"/"+invocation);
        var d=Require(invocation);Prerequisites(owner,d,claim.Source);return d;
    }
    // A feat-local journal reuses the Stage6.1 replay planner. No native grants or other
    // classes are mutated. A confirmed origin can be removed only after cleanup proof.
    internal sealed class AdeptSelection
    {
        private readonly EldritchInvocationSourcesPlan registry;
        private readonly Owner owner;
        private readonly string origin;
        private readonly Claim[] initial;
        private string selected;
        private bool cancelled;
        private IReadOnlyList<Claim> committed;
        internal AdeptSelection(EldritchInvocationSourcesPlan registry,Owner owner,string origin)
        {
            if(string.IsNullOrWhiteSpace(origin) || owner.Claims.Any(c=>c.Id==origin))throw new Rejection(Code.DuplicateOrigin,origin);
            this.registry=registry;this.owner=owner;this.origin=origin;initial=owner.Claims.ToArray();
        }
        private void Editable(){if(cancelled || committed!=null)throw new Rejection(Code.ClosedSelection,origin);}
        internal void Select(string invocation)
        {
            Editable();var d=registry.Require(invocation);registry.Prerequisites(owner,d,Origin.EldritchAdept);
            // Native UI owns one effective grant. Importing ambiguous duplicate native grants is not safe.
            if(initial.Any(c=>c.Invocation==invocation))throw new Rejection(Code.DuplicateOrigin,invocation+"/already granted");
            var tx=new WarlockInvocationTransaction<string>(Array.Empty<string>(),new[]{
                new WarlockInvocationTransaction<string>.Event(origin,Math.Max(1,owner.CharacterLevel),1,0,Array.Empty<string>(),new[]{invocation})});
            tx.Prepare((id,_,_)=>{registry.Prerequisites(owner,registry.Require(id),Origin.EldritchAdept);return true;});
            selected=invocation;
        }
        internal void Reset(){Editable();selected=null;}
        internal void Back()=>Reset();
        internal IReadOnlyList<Claim> Cancel(){Editable();cancelled=true;return Array.AsReadOnly(initial);}
        internal IReadOnlyList<Claim> Commit()
        {
            if(cancelled)throw new Rejection(Code.ClosedSelection,origin);
            if(committed!=null)return committed;
            if(selected==null)throw new Rejection(Code.MissingPrerequisite,origin+"/one selection required; Skip cannot grant the feat");
            registry.Prerequisites(owner,registry.Require(selected),Origin.EldritchAdept);
            return committed=Array.AsReadOnly(initial.Concat(new[]{new Claim(origin,Origin.EldritchAdept,selected)}).ToArray());
        }
    }
    internal IReadOnlyList<Claim> RemoveAdept(Owner owner,string origin,bool cleanupVerified)
    {
        var claim=owner.Claims.SingleOrDefault(c=>c.Id==origin && c.Source==Origin.EldritchAdept)
            ??throw new Rejection(Code.WrongOwner,origin);
        if(!cleanupVerified)throw new Rejection(Code.CleanupRequired,origin+"/native features, conditions and resources");
        // Other claims survive, including a legitimate confirmed class claim for the same definition.
        return Array.AsReadOnly(owner.Claims.Where(c=>c!=claim).ToArray());
    }
    internal sealed class Activation(string operation,string hero,string origin,string invocation,string spell,
        int circle,P.Source resource,bool preferPact=false,bool shift=false)
    {
        internal string Operation { get; } = operation;
        internal string Hero { get; } = hero;
        internal string Origin { get; } = origin;
        internal string Invocation { get; } = invocation;
        internal string Spell { get; } = spell;
        internal int Circle { get; } = circle;
        internal P.Source Resource { get; } = resource;
        internal bool PreferPact { get; } = preferPact;
        internal bool Shift { get; } = shift;
        internal bool Same(Activation a)=>Hero==a.Hero && Origin==a.Origin && Invocation==a.Invocation && Spell==a.Spell &&
            Circle==a.Circle && Resource==a.Resource && PreferPact==a.PreferPact && Shift==a.Shift;
    }
    internal sealed class PaidLedger
    {
        private readonly EldritchInvocationSourcesPlan registry;
        private Owner owner;
        internal P.CastingLedger Resources { get; }
        private readonly Dictionary<string,(Activation Action,P.Cast Cast,P.Decision Decision,bool Committed,bool Cancelled)> actions=new(StringComparer.Ordinal);
        private readonly HashSet<string> used=new(StringComparer.Ordinal); // native invocation definition, not acquisition origin
        private readonly Dictionary<string,bool> rests=new(StringComparer.Ordinal);
        internal PaidLedger(EldritchInvocationSourcesPlan registry,Owner owner,P.CastingLedger resources,IEnumerable<string> alreadyUsed=null)
        {
            this.registry=registry;this.owner=owner;Resources=resources??throw new ArgumentNullException(nameof(resources));
            foreach(var id in alreadyUsed??Array.Empty<string>())
            {if(registry.Require(id).Type!=Kind.PaidSpell)throw new Rejection(Code.UnsupportedOption,id);used.Add(id);}
        }
        internal bool Used(string invocation)=>used.Contains(invocation);
        internal void RefreshOwner(Owner updated)
        {
            if(updated==null || updated.Hero!=owner.Hero)throw new Rejection(Code.WrongOwner,"owner refresh");
            owner=updated;
            foreach(var key in actions.Where(p=>!p.Value.Committed).Select(p=>p.Key).ToArray())
            {var r=actions[key];actions[key]=(r.Action,r.Cast,r.Decision,false,true);}
            // Preserve definition-use debt and receipts across feat removal/retraining/refresh.
        }

        private (P.Cast Cast,P.Decision Decision) Validate(Activation a)
        {
            if(a==null || string.IsNullOrWhiteSpace(a.Operation))throw new Rejection(Code.MissingActionIdentity,"native Consume has no action identity");
            if(a.Hero!=owner.Hero)throw new Rejection(Code.WrongOwner,a.Hero);
            var d=registry.Owned(owner,a.Origin,a.Invocation);
            if(d.Type!=Kind.PaidSpell)throw new Rejection(Code.UnsupportedOption,d.Id);
            if(a.Spell!=d.Spell)throw new Rejection(Code.InvalidSpell,d.Id);
            if(a.Circle<d.Circle || a.Circle>9 || a.Resource is not (P.Source.Automatic or P.Source.Standard or P.Source.Pact))
                throw new Rejection(Code.InvalidCircle,d.Id);
            if(used.Contains(d.Id))throw new Rejection(Code.AlreadyUsed,d.Id);
            // Authorization is the invocation claim, NOT HasKnowledgeOfSpell. No borrowed repertoire.
            var cast=new P.Cast("invocation:"+owner.Hero.Length+":"+owner.Hero+":"+a.Operation,"Warlock",owner.CastingFeature,
                d.Spell,d.Circle,a.Circle,owner.Ability,owner.SaveDc,true,a.Resource);
            var decision=Resources.Resolve(cast,a.PreferPact,a.Shift);
            if(!decision.Allowed)throw new Rejection(decision.Error==P.Failure.Exhausted?Code.Exhausted:Code.InvalidCircle,d.Id+"/"+decision.Error);
            return (cast,decision);
        }
        internal P.Decision Prepare(Activation a)
        {
            if(a==null || string.IsNullOrWhiteSpace(a.Operation))throw new Rejection(Code.MissingActionIdentity,"paid invocation");
            if(actions.TryGetValue(a.Operation,out var old))
            {
                if(!a.Same(old.Action))throw new Rejection(Code.ConflictingAction,a.Operation);
                if(old.Cancelled)throw new Rejection(Code.ClosedSelection,a.Operation+"/cancelled");
                return old.Decision;
            }
            var v=Validate(a);actions.Add(a.Operation,(a,v.Cast,v.Decision,false,false));return v.Decision;
        }
        internal P.Decision Commit(Activation a)
        {
            Prepare(a);var r=actions[a.Operation];if(r.Committed)return r.Decision;
            var v=Validate(a); // other actions may have spent resources since Prepare
            var paid=Resources.Spend(v.Cast,a.PreferPact,a.Shift);
            if(!paid.Allowed)throw new Rejection(Code.Exhausted,a.Invocation);
            used.Add(a.Invocation);actions[a.Operation]=(a,v.Cast,paid,true,false);return paid;
        }
        internal void CancelPrepared(string operation)
        {
            if(!actions.TryGetValue(operation,out var r))throw new Rejection(Code.MissingActionIdentity,operation);
            if(r.Committed)throw new Rejection(Code.PendingAtomicJournal,"no verified native post-spend cancellation/refund boundary");
            actions[operation]=(r.Action,r.Cast,r.Decision,false,true); // nothing was debited
        }
        internal void Rest(string operation,bool longRest,bool simulate=false)
        {
            if(string.IsNullOrWhiteSpace(operation))throw new Rejection(Code.MissingActionIdentity,"rest");
            if(simulate)return;
            if(rests.TryGetValue(operation,out var prior))
            {if(prior!=longRest)throw new Rejection(Code.ConflictingAction,operation);return;}
            // Invalidated pending actions cannot be committed after resources change epoch.
            Resources.Rest(operation,longRest);
            foreach(var key in actions.Where(p=>!p.Value.Committed).Select(p=>p.Key).ToArray())
            {var r=actions[key];actions[key]=(r.Action,r.Cast,r.Decision,false,true);}
            if(longRest)used.Clear();rests.Add(operation,longRest);
        }
    }
}
