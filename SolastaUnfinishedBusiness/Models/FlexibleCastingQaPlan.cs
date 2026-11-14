using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Readiness diagnostics only. Observing CreateNewCharacter is NOT binding a resource owner.
// Neither the QA variant nor this pure policy authorizes native finalization or conversions.
internal static class FlexibleCastingQaPlan
{
    internal const string BuildId = "13.7C.1-DIAGNOSTIC-BLOCKED";
    internal const bool NativeAdmissionReady = false;
    internal enum Code { OrdinaryBuild, Ended, NotLocal, NoParty, UnprovenOwner, InvalidHistory, NativeFinalizationUnavailable }
    internal sealed class Hero(object identity, bool observedAtZero, bool boundAtZero, int level,
        IEnumerable<KeyValuePair<string, int>> classes, IEnumerable<string> history)
    {
        internal readonly object Identity = identity;
        internal readonly bool ObservedAtZero = observedAtZero, BoundAtZero = boundAtZero;
        internal readonly int Level = level;
        internal readonly IReadOnlyDictionary<string, int> Classes = classes.ToDictionary(x => x.Key, x => x.Value);
        internal readonly IReadOnlyList<string> History = history.ToArray();
    }
    internal static Code Check(bool qaBuild, bool ended, bool local, IEnumerable<Hero> party)
    {
        if (!qaBuild) return Code.OrdinaryBuild;
        if (ended) return Code.Ended;
        if (!local) return Code.NotLocal;
        var heroes = party?.ToArray();
        if (heroes == null || heroes.Length == 0) return Code.NoParty;
        if (heroes.Any(h => h == null || h.Identity == null || !h.ObservedAtZero || !h.BoundAtZero) ||
            heroes.Select(h => h.Identity).Distinct(ReferenceEquality.Instance).Count() != heroes.Length) return Code.UnprovenOwner;
        if (heroes.Any(h => h.Level is < 0 or > 20 || h.Classes.Count > 3 || h.Classes.Values.Any(n => n is < 1 or > 12) ||
            h.Classes.Values.Sum() != h.Level || h.History.Count != h.Level ||
            h.History.Any(id => !h.Classes.ContainsKey(id)) || h.Classes.Any(c => h.History.Count(id => id == c.Key) != c.Value)))
            return Code.InvalidHistory;
        // A complete staged draft still returns PendingAtomicJournal in the existing finalizer.
        // The pool-to-party transfer also has no validated resource-owner/transaction journal.
        return Code.NativeFinalizationUnavailable;
    }
    internal static string Reason(Code code) => code switch
    {
        Code.OrdinaryBuild => "Обычная сборка: QA entry отсутствует.",
        Code.Ended => "QA session завершена. Для нового запуска полностью перезапустите игру.",
        Code.NotLocal => "Требуется подтверждённый local single-player; multiplayer/неизвестный transport запрещён.",
        Code.NoParty => "Нет активной партии. Admission закрыт; level-0 observation не является binding.",
        Code.UnprovenOwner => "У партии нет доказанного level-0 binding каждого original hero. Loaded save/character-pool import не допускается.",
        Code.InvalidHistory => "Несогласованная history/level, class cap12, total cap20 или более трёх классов.",
        _ => "PendingAtomicJournal: native finalization и передача level-0 ownership всей новой партии не реализованы. Admission/XP/conversions закрыты."
    };
    // Installed ExperienceThresholds[level] is the next threshold for actual level1..19.
    // Read-only preview, NEVER native XP authority; an admitted original hero is still required.
    internal static int NextXpDelta(int actualLevel, int currentXp, IReadOnlyList<int> thresholds, int multiplier)
    {
        if (actualLevel is < 1 or >= 20 || currentXp < 0 || thresholds == null || thresholds.Count <= actualLevel ||
            multiplier is not (100 or <= 0)) throw new InvalidOperationException("XP preview requires actual level1..19, installed table, unchanged100% XP rule.");
        if (thresholds[actualLevel] < thresholds[actualLevel - 1]) throw new InvalidOperationException("Invalid XP table.");
        return Math.Max(0, thresholds[actualLevel] - currentXp);
    }
    private sealed class ReferenceEquality : IEqualityComparer<object>
    {
        internal static readonly ReferenceEquality Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
