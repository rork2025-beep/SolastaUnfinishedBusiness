using System;
using System.Collections.Generic;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// No Unity types. This is also the production validator and ordered finalization journal.
// Choices are copied, so later edits to building-data dictionaries cannot alter a prepared transaction.
internal sealed class WarlockInvocationTransaction<T>
{
    internal sealed class Event
    {
        internal readonly string Origin;
        internal readonly int Original;
        internal readonly int Awards;
        internal readonly int Replacements;
        internal readonly IReadOnlyList<T> Removed;
        internal readonly IReadOnlyList<T> Added;

        internal Event(string origin, int original, int awards, int replacements, IEnumerable<T> removed, IEnumerable<T> added)
        {
            Origin = origin; Original = original; Awards = awards; Replacements = replacements;
            Removed = Array.AsReadOnly(removed.ToArray()); Added = Array.AsReadOnly(added.ToArray());
        }
    }

    internal sealed class Frame
    {
        internal readonly Event Event;
        internal readonly IReadOnlyList<T> Before;
        internal readonly IReadOnlyList<T> After;
        internal Frame(Event e, IEnumerable<T> before, IEnumerable<T> after)
        { Event = e; Before = Array.AsReadOnly(before.ToArray()); After = Array.AsReadOnly(after.ToArray()); }
    }

    private readonly T[] initial;
    internal readonly IReadOnlyList<Event> Events;
    private IReadOnlyList<Frame> prepared;
    private bool applied;
    private bool poisoned;

    internal WarlockInvocationTransaction(IEnumerable<T> known, IEnumerable<Event> events)
    {
        initial = known.ToArray();
        Events = Array.AsReadOnly(events.ToArray());
        if (initial.Distinct().Count() != initial.Length || Events.Select(x => x.Origin).Distinct().Count() != Events.Count ||
            !Events.Select(x => x.Original).SequenceEqual(Events.Select(x => x.Original).OrderBy(x => x)) ||
            Events.Any(x => string.IsNullOrEmpty(x.Origin) || x.Original is < 1 or > 20 || x.Awards < 0 || x.Replacements < 0))
            throw new ArgumentException("Invalid invocation inventory or advancement event order.");
    }

    internal IReadOnlyList<Frame> Replay(bool complete, int through = 20,
        Func<T, int, IReadOnlyList<T>, bool> prerequisite = null)
    {
        var known = initial.ToList();
        var frames = new List<Frame>();
        var events = Events.Where(x => x.Original <= through).ToArray();
        for (var index = 0; index < events.Length; index++)
        {
            var e = events[index];
            var before = known.ToArray();
            if (e.Removed.Count > e.Replacements || e.Added.Count > e.Awards + e.Removed.Count ||
                ((complete || index < events.Length - 1) && e.Added.Count != e.Awards + e.Removed.Count))
                throw new InvalidOperationException(e.Origin + ": incomplete or excessive invocation budget.");
            foreach (var removed in e.Removed)
                if (!known.Remove(removed)) throw new InvalidOperationException(e.Origin + ": invocation absent at this event.");
            foreach (var added in e.Added)
            {
                if (known.Contains(added)) throw new InvalidOperationException(e.Origin + ": duplicate invocation acquisition.");
                if (prerequisite != null && !prerequisite(added, e.Original, known.AsReadOnly()))
                    throw new InvalidOperationException(e.Origin + ": invocation prerequisite not satisfied.");
                known.Add(added);
            }
            // A remaining invocation must not lose its Pact/known-spell prerequisite at the end of an event.
            if (complete && prerequisite != null && known.Any(x => !prerequisite(x, e.Original, known.AsReadOnly())))
                throw new InvalidOperationException(e.Origin + ": remaining invocation lost a prerequisite.");
            frames.Add(new Frame(e, before, known));
        }
        return frames.AsReadOnly();
    }

    internal IEnumerable<Event> InvalidatedBy(int original, bool includeCurrent) =>
        Events.Where(x => x.Original > original || (includeCurrent && x.Original == original));

    internal IReadOnlyList<T> Before(int original, Func<T, int, IReadOnlyList<T>, bool> prerequisite = null)
    {
        var frames = Replay(true, original - 1, prerequisite);
        return frames.Count == 0 ? Array.AsReadOnly(initial) : frames[frames.Count - 1].After;
    }

    internal IReadOnlyList<Frame> Prepare(Func<T, int, IReadOnlyList<T>, bool> prerequisite = null)
    {
        if (applied || poisoned) throw new InvalidOperationException("Invocation transaction already closed.");
        prepared = null;
        return prepared = Replay(true, 20, prerequisite);
    }

    // Validation finishes before any application callback. A failed callback rolls back the entire journal.
    // The adapter owns the snapshot of its actual mutation surface (invocations and extra spell dictionaries).
    internal bool ApplyPrepared(Action<Event> apply, Action rollback)
    {
        if (applied) return false;
        if (poisoned || prepared == null) throw new InvalidOperationException("Invocation transaction is not prepared.");
        try
        {
            foreach (var frame in prepared) apply(frame.Event);
            applied = true;
            return true;
        }
        catch
        {
            try { rollback(); }
            catch { poisoned = true; throw; }
            throw;
        }
    }
}
