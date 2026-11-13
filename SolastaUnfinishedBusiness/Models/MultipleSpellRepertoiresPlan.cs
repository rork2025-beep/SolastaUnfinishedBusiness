using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Class access/choices are separate from StandardCasterResourcePlan. No resource mutation here.
internal sealed class MultipleSpellRepertoiresPlan
{
    internal enum Knowledge { Selection, Spellbook, WholeList }
    internal enum Preparation { None, AbilityPlusLevel, AbilityPlusHalfLevel, AbilityOnly, InstalledTable }
    internal enum GrantKind { AutoPrepared, ExpandedList, BonusChoice, ScribingBonus, KnownBonus, MysticArcanum }
    internal sealed class Spell(string id, int circle)
    {
        internal string Id { get; } = id;
        internal int Circle { get; } = circle;
    }
    internal sealed class Grant(string id, int original, GrantKind kind, IEnumerable<Spell> spells, int choices = 0, string extraSpellsTag = "Extra")
    {
        internal string Id { get; } = id;
        internal int Original { get; } = original;
        internal GrantKind Kind { get; } = kind;
        internal IReadOnlyList<Spell> Spells { get; } = Array.AsReadOnly(spells.ToArray());
        internal int Choices { get; } = choices;
        internal string ExtraSpellsTag { get; } = extraSpellsTag;
        internal bool StoreInExtra => !string.IsNullOrEmpty(ExtraSpellsTag);
    }
    internal sealed class Profile
    {
        internal string ClassId { get; }
        internal string FeatureId { get; }
        internal string Ability { get; }
        internal string ListId { get; }
        internal Knowledge Mode { get; }
        internal Preparation PreparedRule { get; }
        internal IReadOnlyList<int> MaximumCircles { get; }
        internal IReadOnlyList<int> Cantrips { get; }
        internal IReadOnlyList<int> Known { get; }
        internal IReadOnlyList<int> Replacements { get; }
        internal IReadOnlyList<int> Scribed { get; }
        internal IReadOnlyList<int> PreparedTable { get; }
        internal IReadOnlyDictionary<string, Spell> List { get; }
        internal IReadOnlyList<Grant> Grants { get; }
        internal Profile(string classId, string featureId, string ability, string listId, Knowledge mode, Preparation preparation,
            IEnumerable<Spell> list, IEnumerable<int> maximumCircles, IEnumerable<int> cantrips,
            IEnumerable<int> known, IEnumerable<int> replacements, IEnumerable<int> scribed,
            IEnumerable<int> preparedTable = null, IEnumerable<Grant> grants = null)
        {
            Id(classId); Id(featureId); Id(ability); Id(listId);
            if (!Enum.IsDefined(typeof(Knowledge), mode) || !Enum.IsDefined(typeof(Preparation), preparation)) throw new ArgumentException("Unknown casting rule.");
            ClassId = classId; FeatureId = featureId; Ability = ability; ListId = listId; Mode = mode; PreparedRule = preparation;
            MaximumCircles = Rows(maximumCircles); Cantrips = Rows(KnownSpellsProgressionPlan.RetainedCantrips(cantrips));
            Known = Rows(known); Replacements = Rows(replacements); Scribed = Rows(scribed);
            if (MaximumCircles.Any(n => n > 9) || Known.Skip(1).Where((n, i) => n < Known[i]).Any()) throw new ArgumentException("Invalid access/known table.");
            PreparedTable = preparedTable == null ? null : Rows(preparedTable);
            if (preparation == Preparation.InstalledTable && PreparedTable == null) throw new ArgumentException("Missing prepared table.");
            var spells = list.ToArray(); ValidateSpells(spells);
            List = new ReadOnlyDictionary<string, Spell>(spells.GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal));
            var extra = (grants ?? Enumerable.Empty<Grant>()).ToArray();
            if (extra.Any(g => g == null || string.IsNullOrWhiteSpace(g.Id) || g.Original is < 1 or > 20 || g.Choices < 0 ||
                               !Enum.IsDefined(typeof(GrantKind), g.Kind) || g.Kind is not (GrantKind.BonusChoice or GrantKind.ScribingBonus or GrantKind.KnownBonus or GrantKind.MysticArcanum) && g.Choices != 0) ||
                extra.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != extra.Length) throw new ArgumentException("Ambiguous grant origin.");
            ValidateSpells(spells.Concat(extra.SelectMany(g => g.Spells)));
            if (extra.Any(g => g.Kind == GrantKind.MysticArcanum && (classId != "Warlock" || g.Choices != 1 ||
                g.ExtraSpellsTag != "MysticArcanum" || g.Spells.Any(s => s.Circle is < 6 or > 9) || g.Spells.Select(s => s.Circle).Distinct().Count() != 1)))
                throw new ArgumentException("Mystic Arcanum requires its own Warlock grant, exact high circle and native extra tag.");
            Grants = Array.AsReadOnly(extra);
        }
        internal int PreparedLimit(int original, int abilityModifier, int scopedBonus)
        {
            if (original is < 1 or > 20) return 0;
            var count = PreparedRule switch
            {
                Preparation.None => 0,
                Preparation.InstalledTable => PreparedTable[original - 1] + scopedBonus,
                Preparation.AbilityPlusLevel => abilityModifier + original + scopedBonus,
                Preparation.AbilityPlusHalfLevel => abilityModifier + original / 2 + scopedBonus,
                _ => abilityModifier + scopedBonus
            };
            return PreparedRule == Preparation.None ? 0 : Math.Max(1, count);
        }
        internal bool Allows(string spell, int original, bool cantrip = false)
        {
            if (original is < 1 or > 20) return false;
            var found = List.TryGetValue(spell, out var entry) ? entry : Grants.Where(g => g.Kind == GrantKind.ExpandedList && g.Original <= original)
                .SelectMany(g => g.Spells).FirstOrDefault(x => x.Id == spell);
            return found != null && (cantrip ? found.Circle == 0 : found.Circle > 0 && found.Circle <= MaximumCircles[original - 1]);
        }
        internal Spell Find(string id) => List.TryGetValue(id, out var spell) ? spell : Grants.SelectMany(g => g.Spells).FirstOrDefault(s => s.Id == id)
            ?? throw new InvalidOperationException("Unknown spell in owning repertoire.");
    }
    internal sealed class Origin(string classId, string featureId, int original, string source, string spellId, bool removed = false)
    {
        internal string ClassId { get; } = classId;
        internal string FeatureId { get; } = featureId;
        internal int Original { get; } = original;
        internal string Source { get; } = source;
        internal string SpellId { get; } = spellId;
        internal bool Removed { get; } = removed;
    }
    internal sealed class Repertoire
    {
        internal Profile Definition { get; }
        internal int ActualLevel { get; }
        internal int EffectiveLevel { get; }
        internal bool Compressed { get; }
        internal IReadOnlyList<string> Known { get; }
        internal IReadOnlyList<string> Cantrips { get; }
        internal IReadOnlyList<string> Prepared { get; }
        internal IReadOnlyList<Origin> History { get; }
        internal IReadOnlyDictionary<string, IReadOnlyList<string>> Bonus { get; }
        internal IEnumerable<string> AutoPrepared => Definition.Grants.Where(g => g.Kind == GrantKind.AutoPrepared && g.Original <= EffectiveLevel)
            .SelectMany(g => g.Spells).Select(s => s.Id).Distinct(StringComparer.Ordinal);
        internal int MaximumCircle => EffectiveLevel == 0 ? 0 : Definition.MaximumCircles[EffectiveLevel - 1];
        internal Repertoire(Profile definition, int actual, bool compressed, IEnumerable<string> known = null,
            IEnumerable<string> cantrips = null, IEnumerable<string> prepared = null, IEnumerable<Origin> history = null,
            IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> bonus = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (actual < 0 || actual > (compressed ? 12 : 20)) throw new ArgumentOutOfRangeException(nameof(actual));
            ActualLevel = actual; Compressed = compressed; EffectiveLevel = compressed ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
            Known = Unique(known); Cantrips = Unique(cantrips); Prepared = Unique(prepared);
            if (Known.Intersect(Cantrips).Any()) throw new ArgumentException("Spell/cantrip collision.");
            History = Array.AsReadOnly((history ?? Enumerable.Empty<Origin>()).ToArray());
            if (History.Any(o => o.ClassId != definition.ClassId || o.FeatureId != definition.FeatureId)) throw new ArgumentException("Foreign spell history.");
            var bonuses = (bonus ?? Enumerable.Empty<KeyValuePair<string, IReadOnlyList<string>>>()).ToArray();
            Bonus = new ReadOnlyDictionary<string, IReadOnlyList<string>>(bonuses.ToDictionary(x => x.Key, x => Unique(x.Value), StringComparer.Ordinal));
        }
        internal Repertoire Prepare(IEnumerable<string> spells, int ability, int scopedBonus)
        {
            var chosen = Unique(spells);
            if (chosen.Count > Definition.PreparedLimit(EffectiveLevel, ability, scopedBonus) || chosen.Any(s =>
                    !Definition.Allows(s, EffectiveLevel) || (Definition.Mode != Knowledge.WholeList && !Known.Contains(s))))
                throw new InvalidOperationException("Prepared spells violate their owning class rules.");
            return new(Definition, ActualLevel, Compressed, Known, Cantrips, chosen, History, Bonus);
        }
    }
    internal sealed class HeroState
    {
        internal IReadOnlyDictionary<string, Repertoire> Repertoires { get; }
        internal HeroState(IEnumerable<Repertoire> repertoires)
        {
            var views = repertoires.ToArray();
            if (views.Select(x => x.Definition.ClassId).Distinct().Count() != views.Length) throw new ArgumentException("Duplicate casting class owner.");
            Repertoires = new ReadOnlyDictionary<string, Repertoire>(views.ToDictionary(x => x.Definition.ClassId, StringComparer.Ordinal));
        }
        internal HeroState With(Repertoire view) => new(Repertoires.Values.Where(r => r.Definition.ClassId != view.Definition.ClassId).Append(view));
    }
    internal sealed class Choice(int original, IEnumerable<string> added = null, IEnumerable<string> removed = null,
        IEnumerable<string> cantrips = null, IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> bonus = null)
    {
        internal int Original { get; } = original;
        internal IReadOnlyList<string> Added { get; } = Unique(added);
        internal IReadOnlyList<string> Removed { get; } = Unique(removed);
        internal IReadOnlyList<string> Cantrips { get; } = Unique(cantrips);
        internal IReadOnlyDictionary<string, IReadOnlyList<string>> Bonus { get; } = new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            (bonus ?? Enumerable.Empty<KeyValuePair<string, IReadOnlyList<string>>>()).ToDictionary(x => x.Key, x => Unique(x.Value), StringComparer.Ordinal));
    }
    internal sealed class Event(int original, int learned, int replacements, int cantrips)
    {
        internal int Original { get; } = original;
        internal int Learned { get; } = learned;
        internal int Replacements { get; } = replacements;
        internal int Cantrips { get; } = cantrips;
    }
    internal sealed class Transaction
    {
        private readonly HeroState initial;
        private readonly Repertoire previous;
        private readonly Repertoire target;
        private readonly SortedDictionary<int, Choice> choices = new();
        private readonly Dictionary<string, Choice> bardChoices = new(StringComparer.Ordinal);
        private readonly HashSet<int> bardComplete = new();
        private bool bardStepMode;
        internal int BardRevision { get; private set; }
        internal IReadOnlyList<BardMagicalSecretsPlan.Step> BardSteps => BardMagicalSecretsPlan.Steps(Id, BardRevision, Definition, Events);
        internal IReadOnlyDictionary<string, Choice> BardSelections => new ReadOnlyDictionary<string, Choice>(bardChoices);
        private HeroState committed;
        private bool cancelled;
        internal string Id { get; }
        internal string ClassId => target.Definition.ClassId;
        internal int ActualLevel => target.ActualLevel;
        internal bool Compressed => target.Compressed;
        internal Profile Definition => target.Definition;
        internal bool IsClosed => cancelled || committed != null;
        internal bool IsCancelled => cancelled;
        internal IReadOnlyList<Event> Events { get; }
        internal Transaction(string id, HeroState state, Profile profile, int actual, bool compressed)
        {
            MultipleSpellRepertoiresPlan.Id(id); Id = id; initial = state;
            previous = state.Repertoires.TryGetValue(profile.ClassId, out var old) ? old : new Repertoire(profile, 0, compressed);
            if (previous.Definition.FeatureId != profile.FeatureId || previous.Compressed != compressed || actual != previous.ActualLevel + 1)
                throw new InvalidOperationException("Class mode/ownership changed or nonsequential advancement.");
            target = new Repertoire(profile, actual, compressed);
            var knownPlan = new KnownSpellsProgressionPlan(profile.Known, profile.Replacements);
            Events = Array.AsReadOnly(Enumerable.Range(previous.EffectiveLevel + 1, target.EffectiveLevel - previous.EffectiveLevel).Select(n => new Event(n,
                profile.Mode == Knowledge.Selection ? knownPlan.Learned(n) + profile.Grants.Where(g => g.Kind == GrantKind.KnownBonus && g.Original == n).Sum(g => g.Choices)
                    : profile.Mode == Knowledge.Spellbook ? profile.Scribed[n - 1] + profile.Grants.Where(g => g.Kind == GrantKind.ScribingBonus && g.Original <= n).Sum(g => g.Choices) : 0,
                profile.Mode == Knowledge.Selection ? profile.Replacements[n - 1] : 0,
                profile.Cantrips[n - 1] - (n == 1 ? 0 : profile.Cantrips[n - 2]))).ToArray());
        }
        internal HeroState Preview() => Replay(false);
        internal void Select(Choice choice)
        {
            Editable();
            if (bardStepMode) throw new InvalidOperationException("Do not mix original-event and Bard step journals.");
            if (!Events.Any(e => e.Original == choice.Original)) throw new InvalidOperationException("Foreign original event.");
            if (Events.Where(e => e.Original < choice.Original).Any(e => !choices.ContainsKey(e.Original))) throw new InvalidOperationException("Earlier event incomplete.");
            var saved = choices.ToArray();
            foreach (var key in choices.Keys.Where(k => k >= choice.Original).ToArray()) choices.Remove(key);
            choices.Add(choice.Original, choice);
            try { Replay(false); }
            catch { choices.Clear(); foreach (var item in saved) choices.Add(item.Key, item.Value); throw; }
        }
        internal void SelectBardStep(BardMagicalSecretsPlan.Step token, IEnumerable<string> added = null,
            IEnumerable<string> removed = null, IEnumerable<string> cantrips = null)
        {
            Editable();
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (!bardStepMode && choices.Count > 0) throw new InvalidOperationException("Do not mix Bard and event journals.");
            var steps = BardSteps.ToArray();
            var index = Array.FindIndex(steps, s => s.Key == token.Key);
            if (index < 0 || token.Transaction != Id || token.Revision != BardRevision ||
                token.Awards != steps[index].Awards || token.Replacements != steps[index].Replacements || token.Cantrips != steps[index].Cantrips)
                throw new InvalidOperationException("Stale or foreign Bard selection step.");
            if (steps.Take(index).Any(s => !bardChoices.ContainsKey(s.Key))) throw new InvalidOperationException("Earlier Bard step incomplete.");
            var fragment = new Choice(token.Original, added, removed, cantrips);
            if (fragment.Removed.Count > token.Replacements || fragment.Added.Count != token.Awards + fragment.Removed.Count || fragment.Cantrips.Count != token.Cantrips)
                throw new InvalidOperationException("Incomplete Bard ordinary/Secrets budget.");
            var before = bardChoices.ToArray(); var oldMode = bardStepMode; var revision = BardRevision;
            var oldEvents = choices.ToArray();
            try
            {
                if (bardChoices.ContainsKey(token.Key)) BardRevision++;
                foreach (var later in steps.Skip(index)) bardChoices.Remove(later.Key);
                bardChoices[token.Key] = fragment; bardStepMode = true;
                RebuildBardEvents(); Replay(false);
            }
            catch
            {
                bardChoices.Clear(); foreach (var pair in before) bardChoices.Add(pair.Key, pair.Value);
                bardStepMode = oldMode; BardRevision = revision;
                choices.Clear(); foreach (var pair in oldEvents) choices.Add(pair.Key, pair.Value);
                RecomputeBardComplete(); throw;
            }
        }
        internal void BackBardStep(BardMagicalSecretsPlan.Step token)
        {
            Editable();
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (!bardStepMode && choices.Count > 0) throw new InvalidOperationException("Do not mix Bard and event journals.");
            var steps = BardSteps.ToArray(); var index = Array.FindIndex(steps, s => s.Key == token.Key);
            if (index < 0 || token.Transaction != Id || token.Revision != BardRevision) throw new InvalidOperationException("Stale/foreign Bard Back step.");
            foreach (var later in steps.Skip(index)) bardChoices.Remove(later.Key);
            bardStepMode = true; BardRevision++; RebuildBardEvents();
        }
        private void RecomputeBardComplete()
        {
            bardComplete.Clear();
            if (!bardStepMode) return;
            foreach (var group in BardSteps.GroupBy(s => s.Original))
                if (group.All(s => bardChoices.ContainsKey(s.Key))) bardComplete.Add(group.Key);
        }
        private void RebuildBardEvents()
        {
            choices.Clear(); RecomputeBardComplete();
            foreach (var group in BardSteps.GroupBy(s => s.Original))
            {
                var ordinary = group.Single(s => s.GrantId == null);
                if (!bardChoices.TryGetValue(ordinary.Key, out var selected)) continue;
                var extra = group.Where(s => s.GrantId != null && bardChoices.ContainsKey(s.Key)).Select(s =>
                    new KeyValuePair<string, IReadOnlyList<string>>(s.GrantId, bardChoices[s.Key].Added));
                choices.Add(group.Key, new Choice(group.Key, selected.Added, selected.Removed, selected.Cantrips, extra));
            }
        }
        internal void Skip(int original) => Select(new Choice(original));
        internal void Back(int original)
        {
            Editable();
            if (bardStepMode) { BackBardStep(BardSteps.Single(s => s.Original == original && s.GrantId == null)); return; }
            if (!Events.Any(e => e.Original == original)) throw new InvalidOperationException("Foreign Back event.");
            foreach (var key in choices.Keys.Where(k => k >= original).ToArray()) choices.Remove(key);
        }
        internal void Reset() { Editable(); choices.Clear(); bardChoices.Clear(); bardComplete.Clear(); BardRevision++; }
        internal HeroState Cancel() { if (committed != null) throw new InvalidOperationException("Already committed."); cancelled = true; return initial; }
        internal HeroState Commit()
        {
            if (cancelled) throw new InvalidOperationException("Cancelled transaction.");
            return committed ??= Replay(true);
        }
        private void Editable() { if (cancelled || committed != null) throw new InvalidOperationException("Closed transaction."); }
        private HeroState Replay(bool complete)
        {
            if (cancelled) return initial;
            var known = previous.Known.ToList(); var cantrips = previous.Cantrips.ToList(); var history = previous.History.ToList();
            var bonus = previous.Bonus.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            foreach (var e in Events)
            {
                if (!choices.TryGetValue(e.Original, out var c))
                {
                    if (complete) throw new InvalidOperationException("Incomplete original spell event.");
                    break;
                }
                var profile = target.Definition;
                var eventComplete = !bardStepMode || bardComplete.Contains(e.Original);
                if (complete && !eventComplete) throw new InvalidOperationException("Incomplete Bard Secrets step.");
                if (c.Removed.Count > e.Replacements || c.Added.Count != e.Learned + c.Removed.Count || c.Cantrips.Count != e.Cantrips ||
                    c.Added.Any(s => !profile.Allows(s, e.Original) || c.Removed.Contains(s) || cantrips.Contains(s) || bonus.Values.Any(b => b.Contains(s))) ||
                    c.Cantrips.Any(s => !profile.Allows(s, e.Original, true) || cantrips.Contains(s) || bonus.Values.Any(b => b.Contains(s)))) throw new InvalidOperationException("Invalid original spell budget/access.");
                // Production known-event planner, scoped to THIS class; later original events may
                // replace an earlier same-advancement selection, never a spell in another class.
                BardSpellLearningState<string> bard = null;
                if (profile.ClassId == "Bard")
                {
                    bard = new BardSpellLearningState<string>(known, cantrips, bonus.Values.SelectMany(v => v).Distinct(), true);
                    bard = bard.LearnOrdinary(c.Removed, c.Added).LearnCantrips(c.Cantrips);
                    known = bard.Ordinary.ToList(); cantrips = bard.Cantrips.ToList();
                }
                else
                {
                    known = KnownSpellsProgressionPlan.ApplyEvent(known, c.Removed, c.Added);
                    cantrips.AddRange(c.Cantrips);
                }
                var ordinaryHistory = c.Removed.Select(s => OriginFor(s, e.Original, "replacement", true))
                    .Concat(c.Added.Select(s => OriginFor(s, e.Original, profile.Mode == Knowledge.Spellbook ? "spellbook" : "ordinary")))
                    .Concat(c.Cantrips.Select(s => OriginFor(s, e.Original, "cantrip"))).ToArray();
                if (bard != null) history.AddRange(ordinaryHistory);
                var bonusGrants = profile.Grants.Where(g => (g.Kind is GrantKind.BonusChoice or GrantKind.MysticArcanum) && g.Original == e.Original);
                if (bard != null) bonusGrants = bonusGrants.OrderBy(g => g.Id, StringComparer.Ordinal);
                foreach (var g in bonusGrants)
                {
                    if (!c.Bonus.TryGetValue(g.Id, out var selected))
                    {
                        if (!eventComplete) continue;
                        throw new InvalidOperationException("Missing owner-scoped bonus step.");
                    }
                    if (selected.Count != g.Choices || selected.Any(s =>
                        !g.Spells.Any(sp => sp.Id == s && (g.Kind == GrantKind.MysticArcanum || sp.Circle <= profile.MaximumCircles[e.Original - 1])) || known.Contains(s) || cantrips.Contains(s) || bonus.Values.Any(b => b.Contains(s))))
                        throw new InvalidOperationException("Invalid owner-scoped bonus spell choice.");
                    if (bard != null) bard = bard.LearnSecrets(selected);
                    if (g.StoreInExtra) bonus.Add(g.Id, selected);
                    else
                    {
                        foreach (var spell in selected)
                            if (profile.Find(spell).Circle == 0) cantrips.Add(spell); else known.Add(spell);
                    }
                    history.AddRange(selected.Select(s => OriginFor(s, e.Original, g.Id)));
                }
                if (c.Bonus.Keys.Any(k => !profile.Grants.Any(g => g.Id == k && (g.Kind is GrantKind.BonusChoice or GrantKind.MysticArcanum) && g.Original == e.Original)))
                    throw new InvalidOperationException("Foreign bonus choice origin.");
                if (bard == null) history.AddRange(ordinaryHistory);
            }
            return initial.With(new Repertoire(target.Definition, target.ActualLevel, target.Compressed, known, cantrips, previous.Prepared, history, bonus));
        }
        private Origin OriginFor(string spell, int original, string source, bool removed = false) =>
            new(target.Definition.ClassId, target.Definition.FeatureId, original, Id + ":" + source, spell, removed);
        internal bool FitsSpellbooks(IEnumerable<(string Owner, int Pages)> books, string hero, bool pagesRequired, bool initialEquipmentPending)
        {
            if (target.Definition.Mode != Knowledge.Spellbook || !pagesRequired || initialEquipmentPending && target.ActualLevel == 1) return true;
            Replay(true);
            var costs = Events.SelectMany(e => choices[e.Original].Added.Concat(choices[e.Original].Bonus
                .Where(pair => !target.Definition.Grants.Single(g => g.Id == pair.Key).StoreInExtra).SelectMany(pair => pair.Value)))
                .Select(s => target.Definition.Find(s).Circle).Where(circle => circle > 0);
            return SpellcastingProgressionPlan.CanFitSpellbooks(books, hero, costs);
        }
    }
    // Native point-pool0 is a dynamic class ceiling for Spell pools, but circle0 for Cantrip pools.
    internal static int ChoiceMaximum(int configured, bool cantripOnly) => cantripOnly ? 0 : configured == 0 ? 9 : configured;
    private static IReadOnlyList<int> Rows(IEnumerable<int> source)
    {
        var rows = source.ToArray();
        if (rows.Length != 20 || rows.Any(n => n < 0)) throw new ArgumentException("Expected twenty original rows.");
        return Array.AsReadOnly(rows);
    }
    private static IReadOnlyList<string> Unique(IEnumerable<string> values)
    {
        var items = (values ?? Enumerable.Empty<string>()).ToArray();
        if (items.Any(string.IsNullOrWhiteSpace) || items.Distinct(StringComparer.Ordinal).Count() != items.Length) throw new ArgumentException("Invalid/duplicate spell identity.");
        return Array.AsReadOnly(items);
    }
    private static void ValidateSpells(IEnumerable<Spell> values)
    {
        var spells = values.ToArray();
        if (spells.Any(s => s == null || string.IsNullOrWhiteSpace(s.Id) || s.Circle is < 0 or > 9) ||
            spells.GroupBy(s => s.Id, StringComparer.Ordinal).Any(g => g.Select(s => s.Circle).Distinct().Count() != 1)) throw new ArgumentException("Conflicting spell identity.");
    }
    private static void Id(string id) { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Stable owner/origin identity required."); }
}
