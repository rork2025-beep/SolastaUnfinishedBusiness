using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;

namespace SolastaUnfinishedBusiness.Models;

// Typed registration/selection plan. Native repertoires, slot debt and invocation charges remain
// native objects; this model does not create pools, infer class ownership or implement save import.
internal sealed class AuxiliarySpellSourcesPlan
{
    internal enum Origin { Race, Feat, Style, Invocation, Subclass, Background }
    internal enum Domain { NativeSlots, NativeInvocation, NativeRitual, AttachedClass, Unsupported }
    internal enum Code { UnknownSource, UnsupportedSource, WrongOwner, DuplicateSource, InvalidChoice, PrematureSource, PendingAtomicJournal, AmbiguousTag }
    internal sealed class Rejection(Code code, string source) : InvalidOperationException(code + ": " + source)
    { internal Code Diagnostic { get; } = code; internal string SourceId { get; } = source; }
    internal sealed class Pool(string id, IEnumerable<R.Spell> allowed, int amount, bool cantrip)
    {
        internal string Id { get; } = id;
        internal IReadOnlyList<R.Spell> Allowed { get; } = Array.AsReadOnly(allowed.ToArray());
        internal int Amount { get; } = amount;
        internal bool Cantrip { get; } = cantrip;
    }
    internal sealed class Source
    {
        internal string Id { get; }
        internal Origin Kind { get; }
        internal string Root { get; }
        internal string Feature { get; }
        internal string NativeOrigin { get; }
        internal string CastingFeature { get; }
        internal string Ability { get; }
        internal string OwnerClass { get; }
        internal Domain Resource { get; }
        internal int RequiredLevel { get; }
        internal string Recharge { get; }
        internal string Knowledge { get; }
        internal IReadOnlyList<R.Spell> Spells { get; }
        internal IReadOnlyList<Pool> Pools { get; }
        internal IReadOnlyList<IReadOnlyList<int>> NativeSlotRows { get; }
        internal IReadOnlyList<int> NativeKnown { get; }
        internal IReadOnlyList<int> NativeCantrips { get; }
        internal bool AtWillInvocation { get; }
        internal IReadOnlyList<string> Prerequisites { get; }
        internal Source(string id, Origin kind, string root, string feature, string nativeOrigin, string ability,
            string ownerClass, Domain domain, int requiredLevel, string recharge, string knowledge,
            IEnumerable<R.Spell> spells, IEnumerable<Pool> pools = null, IEnumerable<IEnumerable<int>> slots = null,
            IEnumerable<int> known = null, IEnumerable<int> cantrips = null, bool atWillInvocation = false, IEnumerable<string> prerequisites = null, string castingFeature = "")
        {
            var all = spells.ToArray(); var selections = (pools ?? Enumerable.Empty<Pool>()).ToArray();
            var rows = (slots ?? Enumerable.Range(0,20).Select(_ => new int[9])).Select(r => (IReadOnlyList<int>)Array.AsReadOnly(r.ToArray())).ToArray();
            if (new[]{id,root,feature,nativeOrigin,ability,recharge,knowledge}.Any(string.IsNullOrWhiteSpace) ||
                !Enum.IsDefined(typeof(Origin),kind) || !Enum.IsDefined(typeof(Domain),domain) || requiredLevel is < 0 or > 20 ||
                domain == Domain.AttachedClass && ownerClass == null && kind != Origin.Feat ||
                rows.Length != 20 || rows.Any(r => r.Count != 9 || r.Any(n => n < 0)) ||
                all.Any(s => s == null || string.IsNullOrEmpty(s.Id) || s.Circle is < 0 or > 9) || all.Select(s => s.Id).Distinct().Count() != all.Length ||
                selections.Any(p => string.IsNullOrEmpty(p.Id) || p.Amount < 0 || p.Allowed.Any(s => (s.Circle == 0) != p.Cantrip)) || selections.Select(p => p.Id).Distinct().Count() != selections.Length)
                throw new ArgumentException("Invalid explicit auxiliary source.");
            Id=id; Kind=kind; Root=root; Feature=feature; NativeOrigin=nativeOrigin; CastingFeature=castingFeature==""?feature:castingFeature; Ability=ability; OwnerClass=ownerClass;
            Resource=domain; RequiredLevel=requiredLevel; Recharge=recharge; Knowledge=knowledge; AtWillInvocation=atWillInvocation;
            Prerequisites=Array.AsReadOnly((prerequisites??Enumerable.Empty<string>()).ToArray());
            Spells=Array.AsReadOnly(all); Pools=Array.AsReadOnly(selections); NativeSlotRows=Array.AsReadOnly(rows);
            NativeKnown=Rows(known); NativeCantrips=Rows(cantrips);
        }
        private static IReadOnlyList<int> Rows(IEnumerable<int> input)
        { var values=(input??new int[20]).ToArray();if(values.Length!=20 || values.Any(n=>n<0))throw new ArgumentException("Twenty native rows required.");return Array.AsReadOnly(values); }
        internal bool CanUseNativeInvocation(bool used) => Resource==Domain.NativeInvocation && (AtWillInvocation || !used);
        internal bool RestoresOn(string rest, bool simulate) => !simulate && Resource==Domain.NativeSlots &&
            (Recharge=="LongRest" && rest=="LongRest" || Recharge=="ShortRest" && rest is "ShortRest" or "LongRest");
        internal C.AuxiliaryInput CalculatorInput(int level = 20) => new(Id, Kind switch
        {
            Origin.Race=>C.AuxiliaryRole.Race, Origin.Feat=>C.AuxiliaryRole.Feat,
            Origin.Style=>C.AuxiliaryRole.FightingStyle, Origin.Background=>C.AuxiliaryRole.Background,
            Origin.Subclass=>C.AuxiliaryRole.SubclassGrant, _=>C.AuxiliaryRole.Invocation
        }, level<RequiredLevel?0:Resource==Domain.NativeSlots
            ? Spells.Where(s=>s.Circle==0 || NativeSlotRows[Math.Max(0,Math.Min(19,level-1))][s.Circle-1]>0).Select(s=>s.Circle).DefaultIfEmpty(0).Max()
            : Spells.Select(s=>s.Circle).DefaultIfEmpty(0).Max(), OwnerClass);
        internal void ValidateOwner(string root,string feature,string ability,string nativeClass,int level)
        {
            if(Resource==Domain.Unsupported)throw new Rejection(Code.UnsupportedSource,Id);
            if(root!=Root || feature!=Feature || ability!=Ability || nativeClass!=null && nativeClass!=OwnerClass)
                throw new Rejection(Code.WrongOwner,Id);
            if(level<RequiredLevel)throw new Rejection(Code.PrematureSource,Id);
        }
        // Native free-use casting never falls through to a guessed class resource. An attached
        // spell may use a real class repertoire only when the native attachment explicitly matches.
        internal Domain Route(string spell,bool paid,bool nativeAttachmentMatches=false)
        {
            if(Resource==Domain.Unsupported)throw new Rejection(Code.UnsupportedSource,Id);
            if(!Spells.Any(s=>s.Id==spell) && !Pools.Any(p=>p.Allowed.Any(s=>s.Id==spell)))throw new Rejection(Code.InvalidChoice,Id);
            if(paid && (Resource!=Domain.AttachedClass || !nativeAttachmentMatches))throw new Rejection(Code.WrongOwner,Id);
            return Resource;
        }
    }
    internal IReadOnlyDictionary<string,Source> Sources { get; }
    internal AuxiliarySpellSourcesPlan(IEnumerable<Source> sources)
    {
        var all=sources.ToArray();
        if(all.Any(s=>s==null) || all.Select(s=>s.Id).Distinct(StringComparer.Ordinal).Count()!=all.Length)
            throw new Rejection(Code.DuplicateSource,"registry");
        Sources=new ReadOnlyDictionary<string,Source>(all.ToDictionary(s=>s.Id,StringComparer.Ordinal));
    }
    internal Source Require(string id) => Sources.TryGetValue(id,out var source)?source:throw new Rejection(Code.UnknownSource,id);
    internal static void ValidateInputs(IEnumerable<C.AuxiliaryInput> expected,IEnumerable<C.AuxiliaryInput> supplied)
    {
        var actual=expected.ToArray();var inputs=supplied.ToArray();
        if(inputs.Any(i=>i==null) || inputs.Select(i=>i.SourceId).Distinct().Count()!=inputs.Length)throw new Rejection(Code.DuplicateSource,"snapshot");
        foreach(var row in inputs)
        {
            var source=actual.SingleOrDefault(a=>a.SourceId==row.SourceId)??throw new Rejection(Code.UnknownSource,row.SourceId);
            if(source.Role!=row.Role || source.OwnerClassId!=row.OwnerClassId || source.MaximumSpellCircle!=row.MaximumSpellCircle)throw new Rejection(Code.WrongOwner,row.SourceId);
        }
        foreach(var row in actual)if(!inputs.Any(i=>i.SourceId==row.SourceId))throw new Rejection(Code.UnknownSource,row.SourceId+"/missing");
    }
    // This journal is deliberately source-local, separate from class spells and native finalization.
    internal sealed class Selection
    {
        private readonly AuxiliarySpellSourcesPlan registry;
        private readonly string[] required;
        private readonly Dictionary<string,IReadOnlyList<string>> initial;
        private readonly Dictionary<string,IReadOnlyList<string>> chosen;
        private IReadOnlyDictionary<string,IReadOnlyList<string>> committed;
        private bool cancelled;
        internal Selection(AuxiliarySpellSourcesPlan registry,IEnumerable<string> requiredSources,
            IEnumerable<KeyValuePair<string,IReadOnlyList<string>>> confirmed=null)
        {
            this.registry=registry; required=requiredSources.ToArray();
            if(required.Distinct().Count()!=required.Length)throw new Rejection(Code.DuplicateSource,"pending");
            foreach(var id in required)if(registry.Require(id).Resource==Domain.Unsupported)throw new Rejection(Code.UnsupportedSource,id);
            initial=(confirmed??Enumerable.Empty<KeyValuePair<string,IReadOnlyList<string>>>()).ToDictionary(p=>p.Key,p=>(IReadOnlyList<string>)Array.AsReadOnly(p.Value.ToArray()),StringComparer.Ordinal);
            chosen=new Dictionary<string,IReadOnlyList<string>>(initial,StringComparer.Ordinal);
        }
        private static string Key(string source,string pool)=>source.Length+":"+source+":"+pool;
        private void Editable(){if(cancelled || committed!=null)throw new InvalidOperationException("Closed auxiliary journal.");}
        internal void Select(string sourceId,string poolId,IEnumerable<string> spells)
        {
            Editable();var source=registry.Require(sourceId);
            if(!required.Contains(sourceId))throw new Rejection(Code.WrongOwner,sourceId);
            var pool=source.Pools.SingleOrDefault(p=>p.Id==poolId)??throw new Rejection(Code.InvalidChoice,sourceId+"/"+poolId);
            var values=spells.ToArray();var key=Key(sourceId,poolId);
            if(values.Length!=pool.Amount || values.Distinct().Count()!=values.Length || values.Any(s=>!pool.Allowed.Any(a=>a.Id==s)) ||
                source.Pools.Where(p=>p.Id!=poolId).Any(p=>chosen.TryGetValue(Key(sourceId,p.Id),out var prior) && prior.Intersect(values).Any()))
                throw new Rejection(Code.InvalidChoice,sourceId);
            chosen[key]=Array.AsReadOnly(values);
        }
        internal void Back(string sourceId)
        { Editable();if(!required.Contains(sourceId))throw new Rejection(Code.WrongOwner,sourceId);registry.Require(sourceId);foreach(var pool in registry.Require(sourceId).Pools){var key=Key(sourceId,pool.Id);if(initial.TryGetValue(key,out var old))chosen[key]=old;else chosen.Remove(key);} }
        internal void Reset(){Editable();chosen.Clear();foreach(var p in initial)chosen.Add(p.Key,p.Value);}
        internal IReadOnlyDictionary<string,IReadOnlyList<string>> Preview()=>new ReadOnlyDictionary<string,IReadOnlyList<string>>(new Dictionary<string,IReadOnlyList<string>>(cancelled?initial:chosen,StringComparer.Ordinal));
        internal IReadOnlyDictionary<string,IReadOnlyList<string>> Cancel(){Editable();cancelled=true;return Preview();}
        internal IReadOnlyDictionary<string,IReadOnlyList<string>> Commit()
        {
            if(cancelled)throw new InvalidOperationException("Cancelled auxiliary journal.");
            foreach(var source in required.Select(registry.Require))foreach(var pool in source.Pools)
                if(!chosen.ContainsKey(Key(source.Id,pool.Id)))throw new Rejection(Code.InvalidChoice,source.Id+"/incomplete");
            return committed??=Preview();
        }
    }
}
