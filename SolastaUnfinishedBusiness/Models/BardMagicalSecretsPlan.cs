using System;
using System.Collections.Generic;
using System.Linq;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;

namespace SolastaUnfinishedBusiness.Models;

// Installed sources, not a tabletop grant schedule. Reused by native capture and executable tests.
internal static class BardMagicalSecretsPlan
{
    internal enum SourceKind { Selection, ExpandedList }
    internal sealed class Source(string id, string definition, string owner, int original, SourceKind kind,
        IEnumerable<R.Spell> spells, int amount = 0, int minimum = 0, int maximum = 9)
    {
        internal string Id { get; } = id;
        internal string Definition { get; } = definition;
        internal string Owner { get; } = owner;
        internal int Original { get; } = original;
        internal SourceKind Kind { get; } = kind;
        internal IReadOnlyList<R.Spell> Spells { get; } = Array.AsReadOnly(spells.ToArray());
        internal int Amount { get; } = amount;
        internal int Minimum { get; } = minimum;
        internal int Maximum { get; } = maximum;
    }
    internal static R.Grant Grant(Source source, IReadOnlyList<int> ownMaximum)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.Id) || string.IsNullOrWhiteSpace(source.Definition) ||
            string.IsNullOrWhiteSpace(source.Owner) || source.Original is < 1 or > 20 || ownMaximum == null || ownMaximum.Count != 20 ||
            ownMaximum.Any(n => n is < 0 or > 9) ||
            !Enum.IsDefined(typeof(SourceKind), source.Kind) || source.Amount < 0 || source.Minimum < 0 || source.Maximum < source.Minimum || source.Maximum > 9 ||
            source.Kind == SourceKind.ExpandedList && source.Amount != 0)
            throw new ArgumentException("Invalid installed Bard Secrets source.");
        return source.Kind == SourceKind.ExpandedList
            ? new R.Grant(source.Id, source.Original, R.GrantKind.ExpandedList, source.Spells)
            : new R.Grant(source.Id, source.Original, R.GrantKind.BonusChoice,
                source.Spells.Where(s => s.Circle >= source.Minimum && s.Circle <= Math.Min(source.Maximum, ownMaximum[source.Original - 1])), source.Amount, "MagicalSecrets");
    }
    internal static bool IsSecret(R.Grant grant) => grant.Kind == R.GrantKind.BonusChoice && grant.ExtraSpellsTag == "MagicalSecrets";
    internal sealed class Step(string transaction, int revision, int original, string grantId, int awards, int replacements, int cantrips)
    {
        internal string Transaction { get; } = transaction;
        internal int Revision { get; } = revision;
        internal int Original { get; } = original;
        // null = ordinary class event. Every bonus source retains its own independent key.
        internal string GrantId { get; } = grantId;
        internal string Key => Original + ":" + (GrantId == null ? "ordinary" : "bonus:" + GrantId);
        internal int Awards { get; } = awards;
        internal int Replacements { get; } = replacements;
        internal int Cantrips { get; } = cantrips;
    }
    internal static IReadOnlyList<Step> Steps(string transaction, int revision, R.Profile profile, IEnumerable<R.Event> events)
    {
        if (profile.ClassId != "Bard") throw new InvalidOperationException("Bard steps require their own repertoire.");
        var steps = events.SelectMany(e => new[] { new Step(transaction, revision, e.Original, null, e.Learned, e.Replacements, e.Cantrips) }
            .Concat(profile.Grants.Where(g => g.Original == e.Original && g.Kind == R.GrantKind.BonusChoice)
                .Select(g => new Step(transaction, revision, e.Original, g.Id, g.Choices, 0, 0))));
        return BardSpellLearningState<string>.Ordered(steps.Select(s => (s, s.Original, s.GrantId == null ? 0 : 1, s.GrantId ?? string.Empty))).AsReadOnly();
    }
    // Native uses one MagicalSecrets execution tag; managed acquisition history keeps each source.
    // An unmatched native addition/removal is rejected, never assigned a guessed origin.
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> PreserveOrigins(R.Profile profile, R.Repertoire previous,
        IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> nativeExtra)
    {
        var result = nativeExtra.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (profile.ClassId != "Bard" || previous != null && (previous.Definition.ClassId != profile.ClassId || previous.Definition.FeatureId != profile.FeatureId))
            throw new InvalidOperationException("Foreign Secrets owner.");
        var origins = previous?.Bonus.Where(p => profile.Grants.Any(g => IsSecret(g) && g.Id == p.Key)).ToArray()
                      ?? Array.Empty<KeyValuePair<string, IReadOnlyList<string>>>();
        var native = result.TryGetValue("MagicalSecrets", out var spells) ? spells : Array.Empty<string>();
        if (!new HashSet<string>(origins.SelectMany(p => p.Value), StringComparer.Ordinal).SetEquals(native))
            throw new InvalidOperationException("Native Bard Secrets do not match confirmed source origins; migration or partial grant is unsupported.");
        result.Remove("MagicalSecrets");
        foreach (var origin in origins) result.Add(origin.Key, origin.Value);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(result);
    }
}
