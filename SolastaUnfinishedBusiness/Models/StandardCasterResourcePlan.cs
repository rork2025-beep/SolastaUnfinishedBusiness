using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SolastaUnfinishedBusiness.Models;

// One ledger per original hero. Repertoires and substitute forms are views, never resource owners.
// No native objects, Settings, class admission, Pact Magic or spell-learning decisions here.
internal sealed class StandardCasterResourcePlan
{
    internal enum ResourceMode { Slots, SpellPoints }
    internal sealed class Tables
    {
        private readonly int[][] slots;
        private readonly int[] points;
        private readonly int[] costs;
        internal Tables(IEnumerable<IEnumerable<int>> ordinarySlots, IEnumerable<int> spellPoints, IEnumerable<int> spellCosts)
        {
            slots = ordinarySlots?.Select(x => x.ToArray()).ToArray() ?? throw new ArgumentNullException(nameof(ordinarySlots));
            points = spellPoints?.ToArray() ?? throw new ArgumentNullException(nameof(spellPoints));
            costs = spellCosts?.ToArray() ?? throw new ArgumentNullException(nameof(spellCosts));
            if (slots.Length != 20 || slots.Any(x => x.Length != 9 || x.Any(n => n < 0)) ||
                points.Length != 21 || points[0] != 0 || points.Any(n => n < 0) ||
                costs.Length != 10 || costs[0] != 0 || costs.Skip(1).Any(n => n <= 0))
                throw new ArgumentException("Expected installed ordinary rows1..20, points0..20 and costs0..9.");
        }
        internal int Cost(int circle) { Circle(circle); return costs[circle]; }
        internal int Points(int index) => points[index];
        internal int[] SlotRow(int index) => index == 0 ? new int[9] : (int[])slots[index - 1].Clone();
    }
    // A stable identity prevents a feature viewed in two repertoires from granting twice. Temporary
    // SP slot credits use GrantTemporarySlots instead; they are consumed once, not capacity modifiers.
    internal sealed class Modifier(string sourceId, int slotCircle = 0, int slotCount = 0, int pointCapacity = 0)
    {
        internal string SourceId { get; } = sourceId;
        internal int SlotCircle { get; } = slotCircle;
        internal int SlotCount { get; } = slotCount;
        internal int PointCapacity { get; } = pointCapacity;
    }
    internal sealed class Allocation
    {
        internal string RegistryVersion { get; }
        internal ResourceMode Mode { get; }
        internal int CasterLevel { get; }
        internal int BasePointMaximum { get; }
        internal int PointMaximum { get; }
        internal IReadOnlyList<int> Slots { get; }
        internal int MaximumCircle { get; }
        internal Allocation(Tables tables, CompressedMulticasterProgressionPlan.Result progression,
            ResourceMode mode, IEnumerable<Modifier> modifiers = null) : this(tables, progression, mode, modifiers, false) { }
        // Explicit coordinator boundary: Pact remains outside this allocation. Do not re-calculate
        // after removing Pact: its presence affects installed UB grouped rounding for half casters.
        internal static Allocation StandardPortion(Tables tables, CompressedMulticasterProgressionPlan.Result progression,
            ResourceMode mode, IEnumerable<Modifier> modifiers = null) => new(tables, progression, mode, modifiers, true);
        private Allocation(Tables tables, CompressedMulticasterProgressionPlan.Result progression,
            ResourceMode mode, IEnumerable<Modifier> modifiers, bool separatePact)
        {
            if (tables == null || progression == null) throw new ArgumentNullException();
            if (!progression.IsValid || progression.PactProgressions.Count > 1 || !separatePact && progression.PactProgressions.Count != 0)
                throw new ArgumentException("Invalid progression or Pact participant: Stage13.3 supports standard resources only.");
            if (!Enum.IsDefined(typeof(ResourceMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            RegistryVersion = progression.RegistryVersion; Mode = mode; CasterLevel = progression.ResourceTableIndex;
            var row = tables.SlotRow(CasterLevel);
            BasePointMaximum = tables.Points(CasterLevel);
            var pointMaximum = BasePointMaximum;
            var seen = new Dictionary<string, Modifier>(StringComparer.Ordinal);
            foreach (var m in modifiers ?? Enumerable.Empty<Modifier>())
            {
                if (m == null || string.IsNullOrWhiteSpace(m.SourceId) || m.SlotCircle is < 0 or > 9 || m.SlotCount < 0 || m.PointCapacity < 0 ||
                    (m.SlotCount > 0 && m.SlotCircle == 0)) throw new ArgumentException("Invalid resource modifier.");
                if (seen.TryGetValue(m.SourceId, out var old))
                {
                    if (old.SlotCircle != m.SlotCircle || old.SlotCount != m.SlotCount || old.PointCapacity != m.PointCapacity)
                        throw new ArgumentException("Conflicting modifier identity: " + m.SourceId);
                    continue;
                }
                seen.Add(m.SourceId, m);
                // Native permanent AdditionalSlots remain additional slot uses even in SP mode;
                // they do not silently mint permanent SP. Temporary condition slots are credits.
                if (m.SlotCircle > 0) row[m.SlotCircle - 1] = checked(row[m.SlotCircle - 1] + m.SlotCount);
                pointMaximum = checked(pointMaximum + m.PointCapacity);
            }
            MaximumCircle = Array.FindLastIndex(row, x => x > 0) + 1;
            if (mode == ResourceMode.SpellPoints)
            {
                // Installed SP high-circle policy is one use per circle6..9 per long rest.
                // AdditionalSlots do not bypass this restriction.
                for (var c = 1; c <= 9; c++) row[c - 1] = row[c - 1] > 0 ? 1 : 0;
            }
            Slots = Array.AsReadOnly(row); PointMaximum = pointMaximum;
        }
    }
    internal sealed class Budget
    {
        private readonly Tables tables;
        private readonly int[] usedSlots = new int[9];
        private readonly bool[] highCircleUsed = new bool[9];
        private sealed class Receipt(int circle, int pointCost, int bonusCost, int epoch)
        {
            internal readonly int Circle = circle;
            internal readonly int PointCost = pointCost;
            internal readonly int BonusCost = bonusCost;
            internal readonly int Epoch = epoch;
            internal bool Refunded;
        }
        private int epoch;
        private readonly Dictionary<string, Receipt> spent = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> recoveryIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> temporaryIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> restIds = new(StringComparer.Ordinal);
        private int usedPoints;
        private int bonusPoints;
        internal Allocation Current { get; private set; }
        internal int RemainingPoints => checked(Math.Max(0, Current.PointMaximum - usedPoints) + bonusPoints);
        internal Budget(Tables tables, Allocation initial)
        {
            this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
            Current = initial ?? throw new ArgumentNullException(nameof(initial));
        }
        internal void Refresh(Allocation allocation)
        {
            if (allocation == null || allocation.RegistryVersion != Current.RegistryVersion || allocation.Mode != Current.Mode)
                throw new InvalidOperationException("Resource rules/mode changed: a new validated session is required.");
            // Recalculate capacities without refilling resources or deleting unavailable-circle debt.
            Current = allocation;
        }
        internal bool CanSpend(int circle)
        {
            Circle(circle);
            if (circle == 0) return true;
            var i = circle - 1;
            return Current.Slots[i] > 0 && (Current.Mode == ResourceMode.Slots
                ? usedSlots[i] < Current.Slots[i]
                : RemainingPoints >= tables.Cost(circle) && (circle < 6 || !highCircleUsed[i]));
        }
        internal bool Spend(string operationId, int circle)
        {
            Id(operationId); Circle(circle);
            if (spent.TryGetValue(operationId, out var previous))
            {
                if (previous.Circle != circle) throw new InvalidOperationException("Conflicting expenditure identity.");
                return !previous.Refunded; // same action observed in a second repertoire, not another debit
            }
            if (!CanSpend(circle)) return false;
            var pointCost = 0; var bonusCost = 0;
            if (circle > 0)
            {
                if (Current.Mode == ResourceMode.Slots) usedSlots[circle - 1]++;
                else
                {
                    var cost = tables.Cost(circle);
                    var bonusSpent = Math.Min(bonusPoints, cost);
                    bonusPoints -= bonusSpent; pointCost = cost - bonusSpent; bonusCost = bonusSpent;
                    usedPoints = checked(usedPoints + pointCost);
                    if (circle >= 6) highCircleUsed[circle - 1] = true;
                }
            }
            spent.Add(operationId, new Receipt(circle, pointCost, bonusCost, epoch)); return true;
        }
        // Only an explicitly identified expenditure in the current rest epoch may be undone.
        // Back/Cancel before resource commitment calls neither Spend nor Refund.
        internal bool Refund(string operationId)
        {
            Id(operationId);
            if (!spent.TryGetValue(operationId, out var receipt) || receipt.Refunded || receipt.Epoch != epoch) return false;
            if (receipt.Circle > 0)
            {
                if (Current.Mode == ResourceMode.Slots) usedSlots[receipt.Circle - 1] = Math.Max(0, usedSlots[receipt.Circle - 1] - 1);
                else
                {
                    usedPoints = Math.Max(0, usedPoints - receipt.PointCost);
                    bonusPoints = checked(bonusPoints + receipt.BonusCost);
                    if (receipt.Circle >= 6) highCircleUsed[receipt.Circle - 1] = false;
                }
            }
            receipt.Refunded = true; return true;
        }
        internal int RemainingSlots(int circle)
        {
            Circle(circle);
            if (circle == 0) return 0;
            return Current.Mode == ResourceMode.SpellPoints ? (CanSpend(circle) ? 1 : 0)
                : Math.Max(0, Current.Slots[circle - 1] - usedSlots[circle - 1]);
        }
        internal bool HighCircleSpent(int circle) { Circle(circle); return circle >= 6 && highCircleUsed[circle - 1]; }
        internal bool GrantTemporarySlots(string sourceId, int circle, int count)
        {
            Id(sourceId); Circle(circle);
            if (circle == 0 || count < 0 || Current.Mode != ResourceMode.SpellPoints) throw new ArgumentException("Expected temporary SP slot credit.");
            var value = checked(tables.Cost(circle) * count);
            if (temporaryIds.TryGetValue(sourceId, out var previous))
            {
                if (previous != value) throw new InvalidOperationException("Conflicting temporary grant identity.");
                return false;
            }
            bonusPoints = checked(bonusPoints + value); temporaryIds.Add(sourceId, value); return true;
        }
        internal void Recover(string operationId, IEnumerable<KeyValuePair<int, int>> slots)
        {
            Id(operationId);
            var values = slots?.ToArray() ?? throw new ArgumentNullException(nameof(slots));
            foreach (var value in values)
            {
                Circle(value.Key);
                if (value.Key == 0 || value.Value < 0) throw new ArgumentException("Invalid recovery.");
            }
            var signature = "slots:" + string.Join(";", values.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value));
            if (RecoverySeen(operationId, signature)) return;
            var recovered = values.Aggregate(0, (sum, x) => checked(sum + checked(tables.Cost(x.Key) * x.Value)));
            if (Current.Mode == ResourceMode.SpellPoints) usedPoints = Math.Max(0, usedPoints - recovered);
            else foreach (var value in values) usedSlots[value.Key - 1] = Math.Max(0, usedSlots[value.Key - 1] - value.Value);
            // Recovery restores resource, not the high-circle once-per-long-rest permission.
            recoveryIds.Add(operationId, signature);
        }
        internal void RecoverPoints(string operationId, int amount)
        {
            Id(operationId);
            if (amount < 0 || Current.Mode != ResourceMode.SpellPoints) throw new ArgumentException("Invalid point recovery.");
            var signature = "points:" + amount;
            if (RecoverySeen(operationId, signature)) return;
            usedPoints = Math.Max(0, usedPoints - amount);
            recoveryIds.Add(operationId, signature);
        }
        private bool RecoverySeen(string operationId, string signature)
        {
            if (!recoveryIds.TryGetValue(operationId, out var previous)) return false;
            if (previous != signature) throw new InvalidOperationException("Conflicting recovery identity.");
            return true;
        }
        internal void Rest(string operationId, bool longRest, bool simulate = false)
        {
            Id(operationId);
            if (simulate || !restIds.Add(operationId) || !longRest) return;
            Array.Clear(usedSlots, 0, usedSlots.Length); Array.Clear(highCircleUsed, 0, highCircleUsed.Length);
            usedPoints = 0; bonusPoints = 0; epoch++;
            // Keep operation/grant receipts: callbacks from an earlier action/removed condition may
            // arrive after rest. They must not debit/refill the new budget again.
        }
    }
    // Used by the native adapter as well as executable fake-actor tests. The resolver explicitly
    // recognizes an original hero; it must not map every summon to its summoner.
    internal sealed class OwnerStore<TActor>(Func<TActor, TActor> resolveOwner) where TActor : class
    {
        private readonly ConditionalWeakTable<TActor, Budget> budgets = new();
        internal void Bind(TActor actor, Budget budget)
        {
            var owner = resolveOwner(actor) ?? throw new InvalidOperationException("Unknown standard resource owner.");
            if (!ReferenceEquals(owner, actor)) throw new InvalidOperationException("Bind the original hero, not a substitute form.");
            budgets.Add(owner, budget ?? throw new ArgumentNullException(nameof(budget)));
        }
        internal bool TryGet(TActor actor, out Budget budget)
        {
            var owner = actor == null ? null : resolveOwner(actor);
            budget = null;
            return owner != null && budgets.TryGetValue(owner, out budget);
        }
    }
    private static void Id(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Stable operation/source ID required.");
    }
    private static void Circle(int circle)
    {
        if (circle is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(circle));
    }
}
