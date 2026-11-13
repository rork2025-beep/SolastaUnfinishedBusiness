using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Separate Pact/Arcanum ownership. Standard resources remain the actual13.3 Budget instance.
internal sealed class PactMagicResourcePlan
{
    internal enum Source { Automatic, Standard, Pact, Arcanum }
    internal enum Failure { None, UnownedSpell, InvalidCircle, Exhausted, SelectPactCircle, ArcanumOnly, MissingArcanum, ConflictingAction }
    internal sealed class Arcanum(string origin, int original, int circle, IEnumerable<string> allowedSpells)
    {
        internal string Origin { get; } = origin;
        internal int Original { get; } = original;
        internal int Circle { get; } = circle;
        internal IReadOnlyList<string> Spells { get; } = Array.AsReadOnly(allowedSpells.ToArray());
    }
    internal sealed class Tables
    {
        private readonly (int Circle, int Count)[] rows;
        internal IReadOnlyList<Arcanum> Arcana { get; }
        internal Tables(IEnumerable<IEnumerable<int>> nativePactRows, IEnumerable<Arcanum> arcana)
        {
            var values = nativePactRows.Select(r => r.ToArray()).ToArray();
            if (values.Length != 20 || values.Any(r => r.Length < 5 || r.Any(n => n < 0) || r.Take(5).Count(n => n > 0) != 1 || r.Skip(5).Any(n => n != 0)))
                throw new ArgumentException("Expected twenty native single-circle Pact rows, never shared full-caster rows.");
            rows = values.Select(r => (Array.FindIndex(r, n => n > 0) + 1, r.Single(n => n > 0))).ToArray();
            var grants = arcana.ToArray();
            if (grants.Any(a => a == null || string.IsNullOrWhiteSpace(a.Origin) || a.Original is < 1 or > 20 || a.Circle is < 6 or > 9 ||
                                a.Spells.Any(string.IsNullOrWhiteSpace) || a.Spells.Distinct(StringComparer.Ordinal).Count() != a.Spells.Count) ||
                grants.Select(a => a.Origin).Distinct(StringComparer.Ordinal).Count() != grants.Length ||
                grants.Select(a => a.Circle).Distinct().Count() != grants.Length)
                throw new ArgumentException("Ambiguous native Mystic Arcanum grants.");
            Arcana = Array.AsReadOnly(grants);
        }
        internal (int Circle, int Count) Row(int original) => original == 0 ? (0, 0) : original is >= 1 and <= 20
            ? rows[original - 1] : throw new ArgumentOutOfRangeException(nameof(original));
    }
    internal sealed class Allocation
    {
        internal int ActualWarlockLevel { get; }
        internal int EffectiveWarlockLevel { get; }
        internal int Circle { get; }
        internal int Slots { get; }
        internal IReadOnlyList<Arcanum> Arcana { get; }
        internal Allocation(Tables tables, CompressedMulticasterProgressionPlan.Result progression, int nativeAdditionalPactSlots = 0)
        {
            if (tables == null || progression == null) throw new ArgumentNullException();
            if (!progression.IsValid || progression.PactProgressions.Count > 1 || nativeAdditionalPactSlots < 0)
                throw new ArgumentException("Invalid Pact owner/progression/modifier.");
            var warlock = progression.PactProgressions.SingleOrDefault();
            if (warlock != null && warlock.ClassId != "Warlock") throw new ArgumentException("Unsupported Pact class owner.");
            ActualWarlockLevel = warlock?.ActualClassLevel ?? 0;
            EffectiveWarlockLevel = warlock?.EffectiveCastingLevel ?? 0;
            var row = tables.Row(EffectiveWarlockLevel); Circle = row.Circle;
            Slots = EffectiveWarlockLevel == 0 ? 0 : checked(row.Count + nativeAdditionalPactSlots);
            Arcana = Array.AsReadOnly(tables.Arcana.Where(a => a.Original <= EffectiveWarlockLevel).ToArray());
        }
    }
    internal sealed class Budget
    {
        private readonly Dictionary<int, string> selected = new();
        private readonly HashSet<int> usedArcana = new();
        private readonly Dictionary<string, (Source Source, int Circle, int Epoch, string Spell, bool Refunded)> receipts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> recoveries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> rests = new(StringComparer.Ordinal);
        private int usedPact;
        private int pactEpoch;
        private int longRestEpoch;
        internal Allocation Current { get; private set; }
        internal int Remaining => Math.Max(0, Current.Slots - usedPact);
        internal int Used => usedPact;
        internal IReadOnlyDictionary<int, string> Selected => new ReadOnlyDictionary<int, string>(selected);
        internal Budget(Allocation initial) => Current = initial ?? throw new ArgumentNullException(nameof(initial));
        internal void Refresh(Allocation allocation)
        {
            if (allocation == null || allocation.EffectiveWarlockLevel < Current.EffectiveWarlockLevel ||
                selected.Keys.Any(c => !allocation.Arcana.Any(a => a.Circle == c)))
                throw new InvalidOperationException("Pact conversion/respec requires a validated new journal.");
            Current = allocation; // preserve expenditure and selected/use state, never refill on refresh
        }
        internal void SelectArcanum(int circle, string spell)
        {
            var grant = Current.Arcana.SingleOrDefault(a => a.Circle == circle);
            if (grant == null || !grant.Spells.Contains(spell)) throw new InvalidOperationException("Arcanum selection outside native event/list.");
            if (selected.TryGetValue(circle, out var old) && old != spell) throw new InvalidOperationException("Confirmed Arcanum retraining is unsupported.");
            selected[circle] = spell;
        }
        internal bool CanArcanum(int circle, string spell) => selected.TryGetValue(circle, out var known) && known == spell && !usedArcana.Contains(circle);
        internal bool ArcanumUsed(int circle) => usedArcana.Contains(circle);
        internal bool Spend(string operation, Source source, int circle, string spell = null)
        {
            Id(operation);
            if (receipts.TryGetValue(operation, out var previous))
            {
                if (previous.Source != source || previous.Circle != circle || previous.Spell != spell) throw new InvalidOperationException("Conflicting Pact expenditure identity.");
                return !previous.Refunded;
            }
            if (source == Source.Pact)
            {
                if (circle != Current.Circle || Remaining <= 0) return false;
                usedPact++;
            }
            else if (source == Source.Arcanum)
            {
                if (!CanArcanum(circle, spell)) return false;
                usedArcana.Add(circle);
            }
            else throw new ArgumentException("Pact budget cannot spend standard resources.");
            receipts.Add(operation, (source, circle, source == Source.Pact ? pactEpoch : longRestEpoch, spell, false)); return true;
        }
        internal bool Refund(string operation)
        {
            if (!receipts.TryGetValue(operation, out var r) || r.Refunded || r.Epoch != (r.Source == Source.Pact ? pactEpoch : longRestEpoch)) return false;
            if (r.Source == Source.Pact) usedPact = Math.Max(0, usedPact - 1); else usedArcana.Remove(r.Circle);
            receipts[operation] = (r.Source, r.Circle, r.Epoch, r.Spell, true); return true;
        }
        internal void Recover(string operation, int amount)
        {
            Id(operation);
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (recoveries.TryGetValue(operation, out var old))
            { if (old != amount) throw new InvalidOperationException("Conflicting Pact recovery identity."); return; }
            usedPact = Math.Max(0, usedPact - amount); recoveries.Add(operation, amount);
        }
        internal void RecoverAll(string operation) => Recover(operation, int.MaxValue);
        internal void Rest(string operation, bool longRest, bool simulate = false)
        {
            Id(operation);
            if (simulate) return;
            if (rests.TryGetValue(operation, out var previous))
            { if (previous != longRest) throw new InvalidOperationException("Conflicting rest identity."); return; }
            rests.Add(operation, longRest); usedPact = 0; pactEpoch++;
            if (longRest) { usedArcana.Clear(); longRestEpoch++; }
        }
    }
    internal sealed class Cast(string operation, string owner, string feature, string spell, int baseCircle, int selectedCircle,
        string ability, int saveDc, bool authorized, Source source = Source.Automatic, bool arcanumOnly = false)
    {
        internal string Operation { get; } = operation;
        internal string Owner { get; } = owner;
        internal string Feature { get; } = feature;
        internal string Spell { get; } = spell;
        internal int BaseCircle { get; } = baseCircle;
        internal int Circle { get; } = selectedCircle;
        internal string Ability { get; } = ability;
        internal int SaveDc { get; } = saveDc;
        internal bool Authorized { get; } = authorized;
        internal Source Requested { get; } = source;
        internal bool ArcanumOnly { get; } = arcanumOnly;
        internal bool Same(Cast other) => Owner == other.Owner && Feature == other.Feature && Spell == other.Spell && BaseCircle == other.BaseCircle &&
            Circle == other.Circle && Ability == other.Ability && SaveDc == other.SaveDc && Requested == other.Requested && ArcanumOnly == other.ArcanumOnly && Authorized == other.Authorized;
    }
    internal sealed class Decision(Source source, int circle, Failure failure = Failure.None)
    {
        internal Source Resource { get; } = source;
        internal int Circle { get; } = circle;
        internal Failure Error { get; } = failure;
        internal bool Allowed => Error == Failure.None;
    }
    // This coordinator keeps only dispatch receipts. It does not copy either underlying budget.
    internal sealed class CastingLedger(StandardCasterResourcePlan.Budget standard, Budget pact)
    {
        private readonly Dictionary<string, (Cast Cast, Decision Decision, bool Refunded)> receipts = new(StringComparer.Ordinal);
        internal StandardCasterResourcePlan.Budget Standard { get; } = standard ?? throw new ArgumentNullException(nameof(standard));
        internal Budget Pact { get; } = pact ?? throw new ArgumentNullException(nameof(pact));
        internal Decision Resolve(Cast cast, bool preferPact, bool shift)
        {
            Id(cast.Operation);
            if (receipts.TryGetValue(cast.Operation, out var receipt)) return cast.Same(receipt.Cast) && !receipt.Refunded
                ? receipt.Decision : new Decision(cast.Requested, cast.Circle, Failure.ConflictingAction);
            if (!cast.Authorized || string.IsNullOrWhiteSpace(cast.Owner) || string.IsNullOrWhiteSpace(cast.Feature) || string.IsNullOrWhiteSpace(cast.Spell) || string.IsNullOrWhiteSpace(cast.Ability))
                return new Decision(cast.Requested, cast.Circle, Failure.UnownedSpell);
            if (cast.BaseCircle is < 1 or > 9 || cast.Circle < cast.BaseCircle || cast.Circle > 9)
                return new Decision(cast.Requested, cast.Circle, Failure.InvalidCircle);
            if (cast.Requested == Source.Arcanum)
                return new Decision(Source.Arcanum, cast.Circle, cast.Owner == "Warlock" && cast.Circle == cast.BaseCircle && Pact.CanArcanum(cast.Circle, cast.Spell) ? Failure.None : Failure.MissingArcanum);
            if (cast.ArcanumOnly) return new Decision(cast.Requested, cast.Circle, Failure.ArcanumOnly);
            var canPact = Pact.Remaining > 0 && cast.Circle == Pact.Current.Circle;
            var canStandard = Standard.CanSpend(cast.Circle);
            if (cast.Requested == Source.Pact) return new Decision(Source.Pact, cast.Circle, canPact ? Failure.None :
                Pact.Remaining > 0 && cast.BaseCircle <= Pact.Current.Circle ? Failure.SelectPactCircle : Failure.Exhausted);
            if (cast.Requested == Source.Standard) return new Decision(Source.Standard, cast.Circle, canStandard ? Failure.None : Failure.Exhausted);
            if (cast.Requested != Source.Automatic) return new Decision(cast.Requested, cast.Circle, Failure.InvalidCircle);
            var pactFirst = preferPact || cast.Owner == "Warlock" && !shift || cast.Owner != "Warlock" && shift;
            if (canPact && (pactFirst || !canStandard)) return new Decision(Source.Pact, cast.Circle);
            if (canStandard) return new Decision(Source.Standard, cast.Circle);
            return new Decision(Source.Pact, cast.Circle, Pact.Remaining > 0 && cast.BaseCircle <= Pact.Current.Circle ? Failure.SelectPactCircle : Failure.Exhausted);
        }
        internal Decision Spend(Cast cast, bool preferPact = false, bool shift = false)
        {
            var decision = Resolve(cast, preferPact, shift);
            if (!decision.Allowed || receipts.ContainsKey(cast.Operation)) return decision;
            var paid = decision.Resource == Source.Standard ? Standard.Spend(cast.Operation, decision.Circle)
                : Pact.Spend(cast.Operation, decision.Resource, decision.Circle, cast.Spell);
            if (!paid) return new Decision(decision.Resource, decision.Circle, Failure.Exhausted);
            receipts.Add(cast.Operation, (cast, decision, false)); return decision;
        }
        internal bool Refund(string operation)
        {
            if (!receipts.TryGetValue(operation, out var r) || r.Refunded) return false;
            var refunded = r.Decision.Resource == Source.Standard ? Standard.Refund(operation) : Pact.Refund(operation);
            if (refunded) receipts[operation] = (r.Cast, r.Decision, true); return refunded;
        }
        internal void Rest(string operation, bool longRest, bool simulate = false)
        { Standard.Rest(operation, longRest, simulate); Pact.Rest(operation, longRest, simulate); }
    }
    private static void Id(string id) { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Stable action identity required."); }
}
