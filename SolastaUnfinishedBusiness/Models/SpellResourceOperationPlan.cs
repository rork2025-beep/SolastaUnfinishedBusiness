using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using P = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;

namespace SolastaUnfinishedBusiness.Models;

// Typed delta facade over the EXISTING standard/Pact coordinator. Native counters are supplied
// owner accounts, not a second spell-slot/Spell Points engine. No native state/persistence here.
internal sealed class SpellResourceOperationPlan
{
    internal enum Domain { StandardSlots, SpellPoints, PactSlots, Arcanum, SorceryPoints, ClassPower, InvocationUse, FreeSpellUse, ItemCharges, StoredSpellUse, ItemFunctionUses, ItemQuantity, Effect }
    internal enum Kind { SlotToSorcery, SorceryToSlot, SlotEffect, StandardRecovery, PactRecovery, SpendNative, GainNative, SlotToNative, NativeToSlot }
    internal enum Scale { Fixed, Circle, HalfUp, HalfDown, AllMissing, NativeResult }
    internal enum Code { UnknownSource, WrongOwner, WrongDomain, InvalidCircle, InvalidCost, Exhausted, ConflictingOperation, StaleOperation, RuleChanged, InvalidResult, MissingIdentity, PendingAtomicJournal, Ineligible, AmbiguousSource }
    internal sealed class Rejection(Code code, string detail) : InvalidOperationException(code + ": " + detail)
    { internal Code Diagnostic { get; } = code; }
    internal sealed class Source(string id, string definition, string ownerClass, Kind kind, IEnumerable<Domain> inputs, IEnumerable<Domain> outputs,
        string revision, int minimum = 1, int maximum = 9, int requiredLevel = 0, Scale scale = Scale.Fixed, int amount = 1, bool repertoire = false, bool item = false,
        IEnumerable<KeyValuePair<int, int>> dice = null, bool activation = false, int activationCost = 0, bool costFromRule = false, string nativeRecharge = null)
    {
        internal string NativeRecharge { get; } = nativeRecharge; internal bool CostFromRule { get; } = costFromRule; internal bool RequiresActivation { get; } = activation; internal int ActivationCost { get; } = activationCost;
        internal string Id { get; } = id; internal string Definition { get; } = definition; internal string OwnerClass { get; } = ownerClass;
        internal Kind Operation { get; } = kind; internal IReadOnlyList<Domain> Inputs { get; } = Array.AsReadOnly(inputs.ToArray());
        internal IReadOnlyList<Domain> Outputs { get; } = Array.AsReadOnly(outputs.ToArray()); internal string Revision => revision + "/" + string.Join(";", new[] { Id, Definition, OwnerClass, Operation.ToString(), string.Join(",", Inputs), string.Join(",", Outputs), Minimum.ToString(), Maximum.ToString(), RequiredLevel.ToString(), Scaling.ToString(), Amount.ToString(), RequiresRepertoire.ToString(), RequiresItem.ToString(), RequiresActivation.ToString(), ActivationCost.ToString(), CostFromRule.ToString(), NativeRecharge, string.Join(",", Dice.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value)) });
        internal int Minimum { get; } = minimum; internal int Maximum { get; } = maximum; internal int RequiredLevel { get; } = requiredLevel;
        internal Scale Scaling { get; } = scale; internal int Amount { get; } = amount; internal bool RequiresRepertoire { get; } = repertoire;
        internal bool RequiresItem { get; } = item; internal IReadOnlyDictionary<int, int> Dice { get; } = new ReadOnlyDictionary<int, int>((dice ?? Array.Empty<KeyValuePair<int, int>>()).ToDictionary(p => p.Key, p => p.Value));
    }
    // Fresh source-aware proof: native definition, exact repertoire/item, native effective cost,
    // target pool identities and owner progression. A null repertoire never guesses a class.
    internal sealed class NativeDebit(string account, Domain domain, int cost)
    { internal string Account { get; } = account; internal Domain Resource { get; } = domain; internal int Cost { get; } = cost; }
    internal sealed class Proof(string source, string definition, string revision, string repertoire, string item, string inputCounter,
        IEnumerable<string> outputCounters, int effectiveLevel, int nativeCost, bool eligible, bool allowPact = false, bool allowLossyPact = false, bool activationVerified = false, IEnumerable<NativeDebit> activationDebits = null,string nativeEvent=null,int? nativeResult=null,Domain? resolvedInput=null,int? resolvedCircle=null,int? resolvedNativeCircle=null)
    {
        internal Domain? ResolvedInput { get; }=resolvedInput;internal int? ResolvedCircle { get; }=resolvedCircle;internal int? ResolvedNativeCircle { get; }=resolvedNativeCircle;
        internal string NativeEvent { get; }=nativeEvent;internal int? NativeResult { get; }=nativeResult;
        internal bool ActivationVerified { get; } = activationVerified;
        internal IReadOnlyList<NativeDebit> Debits { get; } = Array.AsReadOnly((activationDebits ?? Array.Empty<NativeDebit>()).ToArray());
        internal string Source { get; } = source; internal string Definition { get; } = definition; internal string Revision { get; } = revision;
        internal string Repertoire { get; } = repertoire; internal string Item { get; } = item; internal string InputCounter { get; } = inputCounter;
        internal IReadOnlyList<string> Outputs { get; } = Array.AsReadOnly((outputCounters ?? Array.Empty<string>()).ToArray());
        internal int EffectiveLevel { get; } = effectiveLevel; internal int Cost { get; } = nativeCost; internal bool Eligible { get; } = eligible;
        internal bool AllowPact { get; } = allowPact; internal bool AllowLossyPact { get; } = allowLossyPact;
        internal string Signature => string.Join("/", new[] { Source, Definition, Revision, Repertoire, Item, InputCounter, string.Join(",", Outputs), EffectiveLevel.ToString(), Cost.ToString(), Eligible.ToString(), AllowPact.ToString(), AllowLossyPact.ToString(),NativeEvent,NativeResult?.ToString(),ResolvedInput?.ToString(),ResolvedCircle?.ToString(),ResolvedNativeCircle?.ToString(), ActivationVerified.ToString(), string.Join(",", Debits.Select(d => d.Account + ":" + d.Resource + ":" + d.Cost)) });
    }
    internal sealed class Counter
    {
        internal string Id { get; }
        internal string Owner { get; }
        internal Domain Resource { get; }
        internal int Maximum { get; private set; }
        internal int Remaining { get; private set; }
        internal string Recharge { get; }
        internal bool Overflow { get; }
        internal Counter(string id, string owner, Domain domain, int maximum, int remaining, string recharge, bool overflow = false)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(owner) || maximum < 0 || remaining < 0 || !overflow && remaining > maximum ||
                domain is Domain.StandardSlots or Domain.SpellPoints or Domain.PactSlots or Domain.Arcanum or Domain.Effect ||
                domain == Domain.SorceryPoints && overflow) throw new ArgumentException("Native counter proof required; never clone shared/Pact pools.");
            Id = id; Owner = owner; Resource = domain; Maximum = maximum; Remaining = remaining; Recharge = recharge; Overflow = overflow;
        }
        internal void Expand(int maximum) { Remaining = checked(Remaining + (maximum - Maximum)); Maximum = maximum; }
        internal int Gain(int amount) => Overflow ? amount : Math.Min(amount, Math.Max(0, Maximum - Remaining));
        internal void Debit(int value) => Remaining = checked(Remaining - value);
        internal void Credit(int value) => Remaining = checked(Remaining + value);
        internal void Rest(bool longRest) { if (Resource is not (Domain.ItemCharges or Domain.StoredSpellUse or Domain.ItemQuantity) && (Recharge == "ShortRest" || longRest && Recharge == "LongRest")) Remaining = Maximum; }
    }
    internal sealed class Request(string operation, string hero, string source, string definition, string revision, Kind kind,
        Domain input, Domain output, int circle, int nativeCircle, int quantity, int nativeCost, int expectedOutput, string expectedState,
        IEnumerable<KeyValuePair<int, int>> recovery = null)
    {
        internal string Operation { get; } = operation; internal string Hero { get; } = hero; internal string Source { get; } = source;
        internal string Definition { get; } = definition; internal string Revision { get; } = revision; internal Kind OperationKind { get; } = kind;
        internal Domain Input { get; } = input; internal Domain Output { get; } = output; internal int Circle { get; } = circle;
        internal int NativeCircle { get; } = nativeCircle; internal int Quantity { get; } = quantity; internal int Cost { get; } = nativeCost;
        internal int ExpectedOutput { get; } = expectedOutput; internal string ExpectedState { get; } = expectedState;
        internal IReadOnlyList<KeyValuePair<int, int>> Recovery { get; } = Array.AsReadOnly((recovery ?? Array.Empty<KeyValuePair<int, int>>()).ToArray());
        internal string Signature => string.Join("|", new[] { Hero, Source, Definition, Revision, OperationKind.ToString(), Input.ToString(), Output.ToString(), Circle.ToString(), NativeCircle.ToString(), Quantity.ToString(), Cost.ToString(), ExpectedOutput.ToString(), ExpectedState, string.Join(";", Recovery.Select(p => p.Key + "=" + p.Value)) });
    }
    internal sealed class Receipt(string operation, string before, string after, Domain input, Domain output, int amount, int effectCircle, int dice)
    {
        internal string Operation { get; } = operation; internal string Before { get; } = before; internal string After { get; } = after;
        internal Domain Input { get; } = input; internal Domain Output { get; } = output; internal int OutputAmount { get; } = amount;
        internal int EffectCircle { get; } = effectCircle; internal int BaseDice { get; } = dice;
    }
    private sealed class Prepared(Request request, Source source, Proof proof, int output, IReadOnlyList<(Counter Counter, int Delta)> credits, int dice)
    {
        internal readonly Request Request = request; internal readonly Source Source = source; internal readonly Proof Proof = proof;
        internal readonly int Output = output; internal readonly IReadOnlyList<(Counter Counter, int Delta)> Credits = credits; internal readonly int Dice = dice;
    }
    private static readonly ConditionalWeakTable<P.CastingLedger, SpellResourceOperationPlan> Coordinators = new();
    private readonly string hero;
    private readonly string version;
    internal P.CastingLedger Resources { get; }
    private readonly Dictionary<string, Source> sources;
    private Dictionary<string, Proof> proofs;
    private readonly Dictionary<string, Counter> counters;
    private readonly HashSet<string> classes;
    private readonly Dictionary<string, (Request Request, Receipt Receipt)> receipts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Request> reserved = new(StringComparer.Ordinal);
    private readonly HashSet<string> cancelled = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> rests = new(StringComparer.Ordinal);
    private int revision;
    internal SpellResourceOperationPlan(string hero, string version, P.CastingLedger resources, IEnumerable<Source> sources,
        IEnumerable<Proof> proofs, IEnumerable<Counter> counters, IEnumerable<string> actualClasses, S.Tables tables, IEnumerable<KeyValuePair<int, int>> creationCosts)
    {
        if (string.IsNullOrWhiteSpace(hero) || string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Stable original owner/version required.");
        this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
        creation = new ReadOnlyDictionary<int, int>(creationCosts.ToDictionary(p => p.Key, p => p.Value));
        if (!creation.Keys.OrderBy(c => c).SequenceEqual(Enumerable.Range(1, 5)) || creation.Values.Any(c => c <= 0)) throw new ArgumentException("Installed creation table1..5 required.");
        this.hero = hero; this.version = version; Resources = resources ?? throw new ArgumentNullException(nameof(resources));
        this.sources = sources.ToDictionary(s => s.Id, StringComparer.Ordinal); this.proofs = proofs.ToDictionary(p => p.Source, StringComparer.Ordinal);
        this.counters = counters.ToDictionary(c => c.Id, StringComparer.Ordinal); classes = new HashSet<string>(actualClasses, StringComparer.Ordinal);
        if (classes.Count > 3 || classes.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Validated actual class registry required (maximum three).");
        foreach (var s in this.sources.Values) if (string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Definition) || string.IsNullOrWhiteSpace(s.Revision) ||
            s.ActivationCost < 0 || s.Minimum < 0 || s.Maximum > 9 || s.Minimum > s.Maximum || s.Amount < 0 || s.RequiredLevel < 0 || s.RequiredLevel > 20 ||
            !Enum.IsDefined(typeof(Kind), s.Operation) || !Enum.IsDefined(typeof(Scale), s.Scaling)) throw new ArgumentException("Invalid audited source rule.");
        if (Coordinators.TryGetValue(resources, out _)) throw new Rejection(Code.ConflictingOperation, "One conversion facade per authoritative coordinator; do not reset receipts");
        Coordinators.Add(resources, this);
    }
    internal string State => version + "/" + revision + "/" + Resources.Standard.Current.RegistryVersion + "/" + Resources.Standard.Current.CasterLevel + "/" + Resources.Standard.Current.PointMaximum + "/" + string.Join(",", Resources.Standard.Current.Slots) + "/" + Resources.Standard.Current.Mode + "/" + Resources.Standard.RemainingPoints + "/" +
        string.Join(",", Enumerable.Range(1, 9).Select(c => c + ":" + Resources.Standard.RemainingSlots(c) + ":" + Resources.Standard.TemporarySlots(c) + ":" + Resources.Standard.HighCircleSpent(c))) + "/" +
        Resources.Pact.Current.EffectiveWarlockLevel + ":" + Resources.Pact.Current.Slots + ":" + Resources.Pact.Remaining + ":" + Resources.Pact.Current.Circle + ":" + string.Join(",", Enumerable.Range(6, 4).Select(c => Resources.Pact.ArcanumUsed(c))) + "/" +
        string.Join(",", counters.Values.OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id + ":" + c.Remaining + ":" + c.Maximum)) + "/" +
        string.Join(",", proofs.Values.OrderBy(p => p.Source, StringComparer.Ordinal).Select(p => p.Signature));
    internal void RefreshProofs(string rulesVersion, IEnumerable<Proof> fresh)
    {
        if (rulesVersion != version) throw new Rejection(Code.RuleChanged, hero);
        proofs = fresh.ToDictionary(p => p.Source, StringComparer.Ordinal); revision++;
        foreach (var id in reserved.Keys.Where(id => !receipts.ContainsKey(id))) cancelled.Add(id);
    }
    // Managed snapshot expansion only, not a native grant/respec API. Retain old receipts/debt;
    // reinitializing the facade must never turn a refresh into free native uses or conversion.
    internal void RefreshOwner(string rulesVersion, IEnumerable<string> actualClasses, IEnumerable<Counter> nativeAccounts, IEnumerable<Proof> fresh)
    {
        if (rulesVersion != version) throw new Rejection(Code.RuleChanged, hero);
        var next = new HashSet<string>(actualClasses, StringComparer.Ordinal); var accounts = nativeAccounts.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var nextProofs = fresh.ToDictionary(p => p.Source, StringComparer.Ordinal);
        if (next.Count > 3 || next.Any(string.IsNullOrWhiteSpace) || !classes.IsSubsetOf(next)) throw new Rejection(Code.PendingAtomicJournal, "Class removal/respec is not an expansion");
        foreach (var c in accounts.Values)
        {
            if (counters.TryGetValue(c.Id, out var old) && (c.Owner != old.Owner || c.Resource != old.Resource || c.Recharge != old.Recharge || c.Overflow != old.Overflow ||
                c.Maximum < old.Maximum || c.Remaining != checked(old.Remaining + (c.Maximum - old.Maximum))))
                throw new Rejection(Code.StaleOperation, "Native counter refresh would change confirmed expenditure: " + c.Id);
        }
        foreach (var c in accounts.Values) { if (counters.TryGetValue(c.Id, out var old)) old.Expand(c.Maximum); else counters.Add(c.Id, c); }
        classes.UnionWith(next); proofs = nextProofs; revision++;
        foreach (var id in reserved.Keys.Where(id => !receipts.ContainsKey(id))) cancelled.Add(id);
    }
    private Counter Account(string id, Domain domain, Proof proof)
    {
        if (id == null || !counters.TryGetValue(id, out var c) || c.Resource != domain || c.Owner != (domain is Domain.ItemCharges or Domain.StoredSpellUse or Domain.ItemFunctionUses or Domain.ItemQuantity ? proof.Item : hero))
            throw new Rejection(Code.WrongOwner, id + "/native counter identity/domain");
        return c;
    }
    private static bool Shared(Domain d) => d is Domain.StandardSlots or Domain.SpellPoints or Domain.PactSlots;
    private void SlotAvailable(Request r, Proof p)
    {
        if (!Shared(r.Input)) throw new Rejection(Code.WrongDomain, r.Source);
        if (r.Input == Domain.PactSlots)
        {
            if (r.Circle != Resources.Pact.Current.Circle || !p.AllowPact) throw new Rejection(Code.InvalidCircle, r.Source + "/explicit Pact circle/permission");
            if (Resources.Pact.Remaining < r.Quantity) throw new Rejection(Code.Exhausted, r.Source);
        }
        else
        {
            if ((r.Input == Domain.SpellPoints) != (Resources.Standard.Current.Mode == S.ResourceMode.SpellPoints)) throw new Rejection(Code.WrongDomain, r.Source + "/slot vs SP mode");
            if (!Resources.Standard.CanSpend(r.Circle)) throw new Rejection(Code.Exhausted, r.Source);
        }
    }
    private Prepared Validate(Request r)
    {
        try { return ValidateCore(r); }
        catch(OverflowException) { throw new Rejection(Code.InvalidCost,"Operation arithmetic exceeds installed/native limits"); }
    }
    private Prepared ValidateCore(Request r)
    {
        if (r == null || string.IsNullOrWhiteSpace(r.Operation)) throw new Rejection(Code.MissingIdentity, "operation");
        if(!Resources.CanClaimConsumer(r.Operation,r.Signature))throw new Rejection(Code.ConflictingOperation,"Operation already belongs to another resource source");
        if (r.Hero != hero) throw new Rejection(Code.WrongOwner, r.Hero);
        if (string.IsNullOrWhiteSpace(r.Source) || string.IsNullOrWhiteSpace(r.Definition) || !sources.TryGetValue(r.Source, out var source)) throw new Rejection(Code.UnknownSource, r.Source);
        if (!proofs.TryGetValue(r.Source, out var p) || p.Definition != r.Definition || source.Definition != r.Definition) throw new Rejection(Code.UnknownSource, r.Definition);
        if (p.Revision != r.Revision || source.Revision != r.Revision) throw new Rejection(Code.RuleChanged, r.Source);
        if (!p.Eligible || source.OwnerClass != null && !classes.Contains(source.OwnerClass) || p.EffectiveLevel < source.RequiredLevel) throw new Rejection(Code.Ineligible, r.Source);
        if (source.RequiresRepertoire && string.IsNullOrWhiteSpace(p.Repertoire) || source.RequiresItem && string.IsNullOrWhiteSpace(p.Item)) throw new Rejection(Code.WrongOwner, r.Source + "/unbound repertoire/item");
        if (r.ExpectedState != State) throw new Rejection(Code.StaleOperation, r.Operation);
        if (source.Operation != r.OperationKind || !source.Inputs.Contains(r.Input) || !source.Outputs.Contains(r.Output) || r.Input == Domain.Arcanum || r.Output == Domain.Arcanum)
            throw new Rejection(Code.WrongDomain, r.Source);
        if(source.Operation==Kind.SlotToSorcery)
        {
            if(p.ResolvedInput==null||p.ResolvedCircle==null||p.ResolvedNativeCircle==null)throw new Rejection(Code.Ineligible,"Verified native Flexible Casting source selection required");
            if(p.ResolvedInput!=r.Input)throw new Rejection(Code.WrongDomain,"Request differs from native resource selection");
            if(p.ResolvedCircle!=r.Circle||p.ResolvedNativeCircle!=r.NativeCircle)throw new Rejection(Code.InvalidCircle,"Request differs from native selected circles");
        }
        if (p.EffectiveLevel < 0 || p.EffectiveLevel > 20) throw new Rejection(Code.Ineligible, r.Source + "/effective owner level");
        if (r.Quantity != 1 || r.Circle < source.Minimum || r.Circle > source.Maximum || r.NativeCircle < 0 || r.NativeCircle > 9) throw new Rejection(Code.InvalidCircle, r.Source);
        if (r.Cost < 0 || r.ExpectedOutput < 0 || p.Cost < 0) throw new Rejection(Code.InvalidCost, r.Source);
        if (source.RequiresActivation && !p.ActivationVerified) throw new Rejection(Code.Ineligible, r.Source + "/unverified initiating power cost");
        if (source.ActivationCost > 0 && (p.Debits.Count != 1 || p.Debits[0].Resource != Domain.ClassPower || p.Debits[0].Cost != source.ActivationCost)) throw new Rejection(Code.Ineligible, "Missing exact native initiating-power use");
        if (p.Debits.Select(d => d.Account).Distinct().Count() != p.Debits.Count) throw new Rejection(Code.AmbiguousSource, "duplicate activation debit");
        foreach (var d in p.Debits)
        {
            if (d.Cost < 0 || d.Resource is Domain.StandardSlots or Domain.SpellPoints or Domain.PactSlots or Domain.Arcanum or Domain.Effect) throw new Rejection(Code.WrongDomain, "activation counter");
            if (d.Account == p.InputCounter || p.Outputs.Contains(d.Account)) throw new Rejection(Code.AmbiguousSource, "activation/main account alias");
            if (Account(d.Account, d.Resource, p).Remaining < d.Cost) throw new Rejection(Code.Exhausted, "initiating power use");
        }
        var output = 0; var credit = new List<(Counter, int)>(); var expectedCost = p.Cost;
        if(source.Operation==Kind.GainNative && r.Input==Domain.Effect && p.NativeEvent!=r.Operation)throw new Rejection(Code.MissingIdentity,"Native recovery/refund event must match operation identity");
        if(source.Scaling==Scale.NativeResult && (p.NativeResult==null||p.NativeResult<0||p.NativeResult>source.Amount))throw new Rejection(Code.InvalidResult,"Verified native outcome outside captured roll/recharge bounds");
        var targetAmount = source.Scaling switch { Scale.Circle => checked(r.NativeCircle * source.Amount), Scale.HalfUp => (p.EffectiveLevel + 1) / 2, Scale.HalfDown => p.EffectiveLevel / 2,Scale.NativeResult=>p.NativeResult.Value, _ => source.Amount };
        if (source.CostFromRule)
        {
            expectedCost = targetAmount;
            if(p.Cost!=expectedCost)throw new Rejection(Code.InvalidCost,"Native cost contradicts audited source formula");
        }
        if (Shared(r.Input))
        {
            SlotAvailable(r, p); expectedCost = r.Input == Domain.SpellPoints ? tables.Cost(r.Circle) : 1;
            if (r.Circle != r.NativeCircle && !(source.Operation == Kind.SlotToSorcery && r.Input == Domain.PactSlots && p.AllowLossyPact && r.NativeCircle >= 1 && r.NativeCircle <= r.Circle))
                throw new Rejection(Code.InvalidCircle, r.Source + "/effect/resource circle mismatch");
        }
        else if (r.Input != Domain.Effect)
        {
            var input = Account(p.InputCounter, r.Input, p);
            if (r.Cost > input.Remaining) throw new Rejection(Code.Exhausted, r.Source);
        }
        switch (source.Operation)
        {
            case Kind.SlotToSorcery:
                if (r.Output != Domain.SorceryPoints || p.Outputs.Count != 1) throw new Rejection(Code.WrongDomain, r.Source);
                var sorcery = Account(p.Outputs[0], Domain.SorceryPoints, p); output = sorcery.Gain(r.NativeCircle); credit.Add((sorcery, output)); break;
            case Kind.SorceryToSlot:
                if (r.Input != Domain.SorceryPoints || !creation.TryGetValue(r.Circle, out expectedCost) || r.NativeCircle != r.Circle ||
                    r.Output != (Resources.Standard.Current.Mode == S.ResourceMode.SpellPoints ? Domain.SpellPoints : Domain.StandardSlots)) throw new Rejection(Code.InvalidCircle, r.Source);
                if (p.Cost!=expectedCost)throw new Rejection(Code.InvalidCost,"Native creation cost differs from installed table");
                if (Account(p.InputCounter, Domain.SorceryPoints, p).Remaining < expectedCost) throw new Rejection(Code.Exhausted, r.Source);
                output = r.Output == Domain.SpellPoints ? tables.Cost(r.Circle) : 1; break;
            case Kind.StandardRecovery:
                if (r.Output != (Resources.Standard.Current.Mode == S.ResourceMode.SpellPoints ? Domain.SpellPoints : Domain.StandardSlots) || r.Recovery.Select(s => s.Key).Distinct().Count() != r.Recovery.Count ||
                    r.Recovery.Count == 0 || r.Recovery.Any(s => s.Key < 1 || s.Key > source.Maximum || s.Value <= 0) || r.Recovery.Sum(s => checked(s.Key * s.Value)) > targetAmount) throw new Rejection(Code.InvalidCost, r.Source);
                output = Resources.Standard.PreviewRecovery(r.Recovery); break;
            case Kind.PactRecovery:
                if (r.Output != Domain.PactSlots) throw new Rejection(Code.WrongDomain, r.Source);
                output = Math.Min(Resources.Pact.Used, source.Scaling == Scale.AllMissing || p.EffectiveLevel >= 20 ? Resources.Pact.Used : (Resources.Pact.Current.Slots + 1) / 2); break;
            case Kind.GainNative:
            case Kind.SlotToNative:
                if (p.Outputs.Count == 0) throw new Rejection(Code.WrongOwner, r.Source + "/missing output counter");
                foreach (var key in p.Outputs)
                {
                    var target = Account(key, r.Output, p); if (source.NativeRecharge != null && target.Recharge != source.NativeRecharge) throw new Rejection(Code.Ineligible, "Native recharge event/target policy mismatch"); if (target.Overflow) throw new Rejection(Code.InvalidResult, "Unaudited native overflow policy"); var amount = source.Scaling == Scale.AllMissing ? Math.Max(0, target.Maximum - target.Remaining) : targetAmount;
                    if(source.Id=="transmuted-refund" && amount>Math.Max(0,target.Maximum-target.Remaining))throw new Rejection(Code.Ineligible,"Refund requires verified remaining Sorcery expenditure");
                    if (source.Id == "alchemy-refund" && amount > Math.Max(0, target.Maximum - target.Remaining)) throw new Rejection(Code.Ineligible, "Alchemy native CanUsePower requires enough used capacity");
                    var gain = target.Gain(amount); output = checked(output + gain); credit.Add((target, gain));
                }
                break;
            case Kind.NativeToSlot:
                if (r.Output != (Resources.Standard.Current.Mode == S.ResourceMode.SpellPoints ? Domain.SpellPoints : Domain.StandardSlots) || r.NativeCircle != r.Circle) throw new Rejection(Code.WrongDomain, r.Source);
                output = r.Output == Domain.SpellPoints ? tables.Cost(r.Circle) : 1; break;
            case Kind.SpendNative:
            case Kind.SlotEffect:
                if (r.Output != Domain.Effect) throw new Rejection(Code.WrongDomain, r.Source); break;
            default: throw new Rejection(Code.UnknownSource, r.Source);
        }
        if (r.Input != Domain.Effect && p.Outputs.Contains(p.InputCounter)) throw new Rejection(Code.AmbiguousSource, "Input and output native account alias requires an explicit audited policy");
        if (p.Outputs.Distinct().Count() != p.Outputs.Count) throw new Rejection(Code.WrongOwner, r.Source + "/duplicate output pool");
        if (r.Cost != expectedCost) throw new Rejection(Code.InvalidCost, r.Source);
        foreach (var pair in credit) _ = checked(pair.Item1.Remaining + pair.Item2);
        if (source.Operation is Kind.SorceryToSlot or Kind.NativeToSlot)
        { if(Resources.Standard.Current.Mode==S.ResourceMode.SpellPoints)_ = checked(Resources.Standard.RemainingPoints + tables.Cost(r.Circle)); else _ = checked(Resources.Standard.Current.Slots[r.Circle-1] + Resources.Standard.TemporarySlots(r.Circle) + 1); }
        if (r.ExpectedOutput != output) throw new Rejection(Code.InvalidResult, r.Source + "/expected actual clamped output=" + output);
        var dice = source.Dice.Where(p => p.Key <= r.NativeCircle).OrderBy(p => p.Key).Select(p => p.Value).LastOrDefault();
        return new Prepared(r, source, p, output, credit.AsReadOnly(), dice);
    }
    private readonly S.Tables tables;
    private readonly IReadOnlyDictionary<int, int> creation;
    private static void Identity(Request r)
    { if (r == null || string.IsNullOrWhiteSpace(r.Operation)) throw new Rejection(Code.MissingIdentity, "resource operation"); }
    private Receipt Existing(Request r)
    {
        Identity(r);
        if (receipts.TryGetValue(r.Operation, out var old))
        { if (old.Request.Signature != r.Signature) throw new Rejection(Code.ConflictingOperation, r.Operation); return old.Receipt; }
        if (reserved.TryGetValue(r.Operation, out var pending) && pending.Signature != r.Signature) throw new Rejection(Code.ConflictingOperation, r.Operation);
        if (cancelled.Contains(r.Operation)) throw new Rejection(Code.StaleOperation, r.Operation + "/cancelled");
        return null;
    }
    internal Receipt Prepare(Request r)
    {
        var previous = Existing(r); if (previous != null) return previous;
        var p = Validate(r); Resources.ClaimConsumer(r.Operation,r.Signature); reserved[r.Operation] = r;
        return new Receipt(r.Operation, State, State, r.Input, r.Output, p.Output, r.NativeCircle, p.Dice);
    }
    // Executable managed arithmetic only. Native spend/effect publication is NOT authorized by
    // this method; every native hook below remains closed until Stage13.7 supplies that boundary.
    internal Receipt CommitManaged(Request r)
    {
        var previous = Existing(r); if (previous != null) return previous;
        var p = Validate(r); Resources.ClaimConsumer(r.Operation,r.Signature); var before = State; var key = "m06:" + hero.Length + ":" + hero + ":" + r.Operation;
        if (Shared(r.Input))
        {
            var paid = r.Input == Domain.PactSlots ? Resources.Pact.Spend(key, P.Source.Pact, r.Circle) : Resources.Standard.Spend(key, r.Circle);
            if (!paid) throw new Rejection(Code.Exhausted, r.Source);
        }
        else if (r.Input != Domain.Effect) Account(p.Proof.InputCounter, r.Input, p.Proof).Debit(r.Cost);
        foreach (var d in p.Proof.Debits) Account(d.Account, d.Resource, p.Proof).Debit(d.Cost);
        if (p.Source.Operation is Kind.SorceryToSlot or Kind.NativeToSlot) Resources.Standard.GrantTemporarySlots(key, r.Circle, 1);
        if (p.Source.Operation == Kind.StandardRecovery) Resources.Standard.Recover(key, r.Recovery);
        if (p.Source.Operation == Kind.PactRecovery) Resources.Pact.Recover(key, p.Output);
        foreach (var credit in p.Credits) credit.Counter.Credit(credit.Delta);
        var receipt = new Receipt(r.Operation, before, State, r.Input, r.Output, p.Output, r.NativeCircle, p.Dice);
        receipts.Add(r.Operation, (r, receipt)); reserved[r.Operation] = r; return receipt;
    }
    internal void CommitNative(Request r)
    { Prepare(r); throw new Rejection(Code.PendingAtomicJournal, r.Source + "/native spend, state projection and effect publication are not an atomic verified boundary"); }
    internal void Cancel(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation) || !reserved.ContainsKey(operation)) throw new Rejection(Code.MissingIdentity, operation);
        if (receipts.ContainsKey(operation)) throw new Rejection(Code.PendingAtomicJournal, operation + "/no invented native rollback");
        cancelled.Add(operation);
    }
    internal void Rest(string operation, bool longRest, bool simulate = false)
    {
        if (string.IsNullOrWhiteSpace(operation)) throw new Rejection(Code.MissingIdentity, "rest");
        if (simulate) return;
        if (rests.TryGetValue(operation, out var old)) { if (old != longRest) throw new Rejection(Code.ConflictingOperation, operation); return; }
        Resources.Rest(operation, longRest); foreach (var c in counters.Values) c.Rest(longRest);
        foreach (var id in reserved.Keys.Where(id => !receipts.ContainsKey(id))) cancelled.Add(id);
        rests.Add(operation, longRest); revision++;
    }
}
