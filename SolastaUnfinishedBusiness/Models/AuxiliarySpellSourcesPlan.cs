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
    internal enum Domain { NativeSlots, NativeInvocation, NativeRitual, AttachedClass, PaidInvocation, InvocationSelection, Unsupported }
    internal enum Code { UnknownSource, UnsupportedSource, WrongOwner, DuplicateSource, InvalidChoice, PrematureSource, PendingAtomicJournal, AmbiguousTag, InactiveVariant, UnverifiedCondition, StaleSelection }
    internal sealed class Rejection(Code code, string source) : InvalidOperationException(code + ": " + source)
    { internal Code Diagnostic { get; } = code; internal string SourceId { get; } = source; }
    internal enum ChoiceKind { Cantrip, Spell, CantripOrSpell }
    internal sealed class Pool
    {
        internal string Id { get; }
        internal IReadOnlyList<R.Spell> Allowed { get; }
        internal int Amount { get; }
        internal ChoiceKind Kind { get; }
        internal bool Cantrip => Kind==ChoiceKind.Cantrip;
        internal bool Shared => Kind==ChoiceKind.CantripOrSpell;
        internal int Minimum { get; }
        internal int NativeMaximum { get; }
        internal int Maximum { get; }
        internal string ExtraSpellsTag { get; }
        internal string ListOverride { get; }
        internal string AuthorizedList { get; }
        internal bool RitualOnly { get; }
        internal bool AllowDuplicateRewards { get; }
        internal bool AllowConfirmedReselection { get; }
        internal Variant Variant { get; }
        internal Pool(string id,IEnumerable<R.Spell> allowed,int amount,bool cantrip)
            :this(id,allowed,amount,cantrip?ChoiceKind.Cantrip:ChoiceKind.Spell) { }
        internal Pool(string id,IEnumerable<R.Spell> allowed,int amount,ChoiceKind kind,int minimum=0,int maximum=9,
            string tag="",string listOverride=null,bool ritualOnly=false,IEnumerable<string> rituals=null,bool allowDuplicateRewards=false,Variant variant=null,string authorizedList=null,bool allowConfirmedReselection=false)
        {
            var all=allowed.ToArray();var ritualSet=new HashSet<string>(rituals??Array.Empty<string>(),StringComparer.Ordinal);
            if(string.IsNullOrWhiteSpace(id) || amount<0 || minimum<0 || maximum<0 || maximum>9 ||
                !Enum.IsDefined(typeof(ChoiceKind),kind) || all.Any(a=>a==null || string.IsNullOrWhiteSpace(a.Id) || a.Circle is < 0 or > 9) ||
                all.Select(a=>a.Id).Distinct().Count()!=all.Length)throw new ArgumentException("Invalid native choice pool.");
            Variant=variant;Id=id;Amount=amount;Kind=kind;Minimum=minimum;NativeMaximum=maximum;
            Maximum=kind==ChoiceKind.Cantrip?0:R.ChoiceMaximum(maximum,false);
            if(Minimum>Maximum)throw new ArgumentException("Invalid native circle range.");
            ExtraSpellsTag=tag;ListOverride=listOverride;AuthorizedList=authorizedList??listOverride;RitualOnly=ritualOnly;AllowDuplicateRewards=allowDuplicateRewards;AllowConfirmedReselection=allowConfirmedReselection;
            Allowed=Array.AsReadOnly(all.Where(a=>a.Circle>=Minimum && a.Circle<=Maximum &&
                (kind==ChoiceKind.CantripOrSpell || (a.Circle==0)==Cantrip) && (!ritualOnly || ritualSet.Contains(a.Id))).ToArray());
        }
    }
    internal sealed class Variant
    {
        internal string Root { get; }
        internal IReadOnlyList<string> Path { get; }
        internal string Id => PathId(Root,Path);
        internal IReadOnlyDictionary<string,string> Branches { get; }
        internal IReadOnlyList<string> Conditions { get; }
        internal bool Unsupported { get; }
        internal bool Constrained => Branches.Count>0 || Conditions.Count>0 || Unsupported;
        internal static string PathId(string root,IEnumerable<string> path)=>string.Concat(new[]{root}.Concat(path).Select(p=>p.Length+":"+p));
        internal Variant(string root,IEnumerable<string> path,IEnumerable<KeyValuePair<string,string>> branches=null,IEnumerable<string> conditions=null,bool unsupported=false)
        {
            var parts=path.ToArray();var choices=(branches??Array.Empty<KeyValuePair<string,string>>()).ToArray();
            var tests=(conditions??Array.Empty<string>()).ToArray();
            if(string.IsNullOrWhiteSpace(root) || parts.Length==0 || parts.Any(string.IsNullOrWhiteSpace) ||
                choices.Any(c=>string.IsNullOrWhiteSpace(c.Key) || string.IsNullOrWhiteSpace(c.Value)) || choices.Select(c=>c.Key).Distinct().Count()!=choices.Length ||
                tests.Any(string.IsNullOrWhiteSpace) || tests.Distinct().Count()!=tests.Length)throw new ArgumentException("Invalid source variant path.");
            Root=root;Path=Array.AsReadOnly(parts);Branches=new ReadOnlyDictionary<string,string>(choices.ToDictionary(c=>c.Key,c=>c.Value,StringComparer.Ordinal));
            Conditions=Array.AsReadOnly(tests);Unsupported=unsupported;
        }
        internal bool Active(VariantState state)
        {
            if(Unsupported)throw new Rejection(Code.UnsupportedSource,Id+"/unknown graph mode");
            if(state==null)
            {if(Constrained)throw new Rejection(Code.PendingAtomicJournal,Id+"/missing selected-branch/condition proof");return true;}
            if(!state.Roots.Contains(Root))return false;
            foreach(var b in Branches)
            {
                if(!state.Branches.TryGetValue(b.Key,out var selected))throw new Rejection(Code.PendingAtomicJournal,b.Key+"/unbound native choice");
                if(selected!=b.Value)return false;
            }
            foreach(var c in Conditions)
            {
                if(!state.Conditions.TryGetValue(c,out var valid))throw new Rejection(Code.UnverifiedCondition,c);
                if(!valid)return false;
            }
            return true;
        }
    }
    internal sealed class VariantState
    {
        internal IReadOnlyList<string> Roots { get; }
        internal IReadOnlyDictionary<string,string> Branches { get; }
        internal IReadOnlyDictionary<string,bool> Conditions { get; }
        internal VariantState(IEnumerable<string> roots,IEnumerable<KeyValuePair<string,string>> branches=null,IEnumerable<KeyValuePair<string,bool>> conditions=null)
        {
            var r=roots.ToArray();var b=(branches??Array.Empty<KeyValuePair<string,string>>()).ToArray();var c=(conditions??Array.Empty<KeyValuePair<string,bool>>()).ToArray();
            if(r.Any(string.IsNullOrWhiteSpace) || r.Distinct().Count()!=r.Length || b.Any(x=>string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)) || c.Any(x=>string.IsNullOrWhiteSpace(x.Key)) || b.Select(x=>x.Key).Distinct().Count()!=b.Length || c.Select(x=>x.Key).Distinct().Count()!=c.Length)
                throw new Rejection(Code.InvalidChoice,"duplicate root or double exclusion branch proof");
            Roots=Array.AsReadOnly(r);Branches=new ReadOnlyDictionary<string,string>(b.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal));
            Conditions=new ReadOnlyDictionary<string,bool>(c.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal));
        }
    }
    internal sealed class Source
    {
        internal Variant Variant { get; }
        internal string ChoiceOrigin => Id;
        internal string SpellListId { get; }
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
            IEnumerable<int> known = null, IEnumerable<int> cantrips = null, bool atWillInvocation = false, IEnumerable<string> prerequisites = null, string castingFeature = "", Variant variant = null, string spellListId = null)
        {
            var all = spells.ToArray(); var selections = (pools ?? Enumerable.Empty<Pool>()).ToArray();
            var rows = (slots ?? Enumerable.Range(0,20).Select(_ => new int[9])).Select(r => (IReadOnlyList<int>)Array.AsReadOnly(r.ToArray())).ToArray();
            if (new[]{id,root,feature,nativeOrigin,ability,recharge,knowledge}.Any(string.IsNullOrWhiteSpace) ||
                !Enum.IsDefined(typeof(Origin),kind) || !Enum.IsDefined(typeof(Domain),domain) || requiredLevel is < 0 or > 20 ||
                domain == Domain.AttachedClass && ownerClass == null && kind != Origin.Feat ||
                rows.Length != 20 || rows.Any(r => r.Count != 9 || r.Any(n => n < 0)) ||
                all.Any(s => s == null || string.IsNullOrEmpty(s.Id) || s.Circle is < 0 or > 9) || all.Select(s => s.Id).Distinct().Count() != all.Length ||
                selections.Any(p => string.IsNullOrEmpty(p.Id) || p.Amount < 0) || selections.Select(p => p.Id).Distinct().Count() != selections.Length)
                throw new ArgumentException("Invalid explicit auxiliary source.");
            SpellListId=spellListId;Variant=variant??new Variant(root,new[]{feature});
            if(Variant.Root!=root || selections.Any(p=>p.Variant!=null && p.Variant.Root!=root))throw new Rejection(Code.WrongOwner,id);
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
        internal void ValidateOwner(string root,string feature,string ability,string nativeClass,int level,VariantState variants=null)
        {
            if(!Variant.Active(variants))throw new Rejection(Code.InactiveVariant,Id);
            if(Resource is Domain.PaidInvocation or Domain.InvocationSelection)throw new Rejection(Code.PendingAtomicJournal,Id);
            if(Resource==Domain.Unsupported)throw new Rejection(Code.UnsupportedSource,Id);
            if(root!=Root || feature!=Feature || ability!=Ability || nativeClass!=null && nativeClass!=OwnerClass)
                throw new Rejection(Code.WrongOwner,Id);
            if(level<RequiredLevel)throw new Rejection(Code.PrematureSource,Id);
        }
        // Native free-use casting never falls through to a guessed class resource. An attached
        // spell may use a real class repertoire only when the native attachment explicitly matches.
        internal Domain Route(string spell,bool paid,bool nativeAttachmentMatches=false,VariantState variants=null)
        {
            if(!Variant.Active(variants))throw new Rejection(Code.InactiveVariant,Id);
            if(Resource is Domain.PaidInvocation or Domain.InvocationSelection)throw new Rejection(Code.PendingAtomicJournal,Id);
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
    internal static void ValidateNativeTags(IEnumerable<KeyValuePair<string,Pool>> rewards)
    {
        var all=rewards.ToArray();
        foreach(var reward in all)
            if(string.IsNullOrWhiteSpace(reward.Value.ExtraSpellsTag))throw new Rejection(Code.PendingAtomicJournal,reward.Key+"/native execution tag needs explicit journal binding");
        for(var i=0;i<all.Length;i++)for(var j=0;j<i;j++)
        {
            var a=all[i].Value;var b=all[j].Value;
            if(a.ExtraSpellsTag==b.ExtraSpellsTag && (a.Shared || b.Shared || a.Cantrip==b.Cantrip))
                throw new Rejection(Code.AmbiguousTag,all[i].Key+"/merged original rewards share native spell tag");
        }
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
        private VariantState variants;
        private readonly VariantState initialVariants;
        private HashSet<string> active;
        internal int Revision { get; private set; }
        internal Selection(AuxiliarySpellSourcesPlan registry,IEnumerable<string> requiredSources,
            IEnumerable<KeyValuePair<string,IReadOnlyList<string>>> confirmed=null,VariantState variants=null)
        {
            this.registry=registry; required=requiredSources.ToArray();this.variants=variants;initialVariants=variants;
            if(required.Distinct().Count()!=required.Length)throw new Rejection(Code.DuplicateSource,"pending");
            foreach(var id in required)
            {
                if(registry.Require(id).Resource is Domain.PaidInvocation or Domain.InvocationSelection)throw new Rejection(Code.PendingAtomicJournal,id);
                if(registry.Require(id).Resource==Domain.Unsupported)throw new Rejection(Code.UnsupportedSource,id);
            }
            initial=(confirmed??Enumerable.Empty<KeyValuePair<string,IReadOnlyList<string>>>()).ToDictionary(p=>p.Key,p=>(IReadOnlyList<string>)Array.AsReadOnly(p.Value.ToArray()),StringComparer.Ordinal);
            chosen=new Dictionary<string,IReadOnlyList<string>>(initial,StringComparer.Ordinal);
            active=Active(variants);
            ActiveKeys(variants,active);
        }
        private HashSet<string> Active(VariantState state)
        {
            if(state!=null)
            {
                foreach(var branch in state.Branches)
                    if(!required.Select(registry.Require).SelectMany(s=>new[]{s.Variant}.Concat(s.Pools.Where(p=>p.Variant!=null).Select(p=>p.Variant))).Any(v=>v.Branches.TryGetValue(branch.Key,out var option) && option==branch.Value))
                        throw new Rejection(Code.InvalidChoice,branch.Key+"/unknown variant branch");
            }
            return new HashSet<string>(required.Where(id=>registry.Require(id).Variant.Active(state)),StringComparer.Ordinal);
        }
        private HashSet<string> ActiveKeys(VariantState state,HashSet<string> sources) => new(sources.SelectMany(id=>registry.Require(id).Pools
            .Where(p=>p.Variant==null || p.Variant.Active(state)).Select(p=>Key(id,p.Id))),StringComparer.Ordinal);
        internal void SetVariants(VariantState state)
        {
            Editable();var next=Active(state);var oldKeys=ActiveKeys(variants,active);var nextKeys=ActiveKeys(state,next);
            var invalidated=oldKeys.Except(nextKeys).Concat(nextKeys.Except(oldKeys)).ToArray();
            if(invalidated.Any(initial.ContainsKey))throw new Rejection(Code.PendingAtomicJournal,"confirmed variant change requires native rollback");
            foreach(var key in invalidated)chosen.Remove(key);
            variants=state;active=next;Revision++;
        }
        private static string Key(string source,string pool)=>source.Length+":"+source+":"+pool;
        private void Editable(){if(cancelled || committed!=null)throw new InvalidOperationException("Closed auxiliary journal.");}
        internal void Select(string sourceId,string poolId,IEnumerable<string> spells,int? revision=null)
        {
            Editable();var source=registry.Require(sourceId);
            if(revision.HasValue && revision!=Revision || (source.Variant.Constrained || source.Pools.Any(p=>p.Variant?.Constrained==true) || Revision>0 && variants!=null) && !revision.HasValue)throw new Rejection(Code.StaleSelection,sourceId);
            if(!required.Contains(sourceId))throw new Rejection(Code.WrongOwner,sourceId);
            if(!active.Contains(sourceId))throw new Rejection(Code.InactiveVariant,sourceId);
            var pool=source.Pools.SingleOrDefault(p=>p.Id==poolId)??throw new Rejection(Code.InvalidChoice,sourceId+"/"+poolId);
            if(pool.Variant!=null && !pool.Variant.Active(variants))throw new Rejection(Code.InactiveVariant,sourceId+"/"+poolId);
            var values=spells.ToArray();var key=Key(sourceId,poolId);
            if(values.Length!=pool.Amount || values.Distinct().Count()!=values.Length || values.Any(s=>!pool.Allowed.Any(a=>a.Id==s)) ||
                source.Pools.Where(p=>p.Id!=poolId && (!p.AllowDuplicateRewards || !pool.AllowDuplicateRewards)).Any(p=>chosen.TryGetValue(Key(sourceId,p.Id),out var prior) && prior.Intersect(values).Any()))
                throw new Rejection(Code.InvalidChoice,sourceId);
            if(initial.TryGetValue(key,out var confirmed) && !confirmed.SequenceEqual(values) && !pool.AllowConfirmedReselection)
                throw new Rejection(Code.PendingAtomicJournal,sourceId+"/confirmed replacement requires native policy and journal");
            chosen[key]=Array.AsReadOnly(values);
        }
        internal void Back(string sourceId)
        { Editable();if(!required.Contains(sourceId))throw new Rejection(Code.WrongOwner,sourceId);registry.Require(sourceId);foreach(var pool in registry.Require(sourceId).Pools){var key=Key(sourceId,pool.Id);if(initial.TryGetValue(key,out var old))chosen[key]=old;else chosen.Remove(key);} }
        internal void Reset(){Editable();chosen.Clear();foreach(var p in initial)chosen.Add(p.Key,p.Value);variants=initialVariants;active=Active(variants);Revision++;}
        internal IReadOnlyDictionary<string,IReadOnlyList<string>> Preview()=>new ReadOnlyDictionary<string,IReadOnlyList<string>>(new Dictionary<string,IReadOnlyList<string>>(cancelled?initial:chosen,StringComparer.Ordinal));
        internal IReadOnlyDictionary<string,IReadOnlyList<string>> Cancel(){Editable();cancelled=true;return Preview();}
        internal IReadOnlyDictionary<string,IReadOnlyList<string>> Commit()
        {
            if(cancelled)throw new InvalidOperationException("Cancelled auxiliary journal.");
            foreach(var source in active.Select(registry.Require))foreach(var pool in source.Pools.Where(p=>p.Variant==null || p.Variant.Active(variants)))
            {
                if(!chosen.TryGetValue(Key(source.Id,pool.Id),out var values) || values.Count!=pool.Amount || values.Distinct().Count()!=values.Count ||
                    values.Any(v=>!pool.Allowed.Any(a=>a.Id==v)))throw new Rejection(Code.InvalidChoice,source.Id+"/incomplete or invalid confirmed choices");
                if(source.Pools.Where(p=>p.Id!=pool.Id && (!p.AllowDuplicateRewards || !pool.AllowDuplicateRewards)).Any(p=>
                    chosen.TryGetValue(Key(source.Id,p.Id),out var other) && other.Intersect(values).Any()))throw new Rejection(Code.InvalidChoice,source.Id+"/duplicate rewards");
            }
            return committed??=Preview();
        }
    }
}
