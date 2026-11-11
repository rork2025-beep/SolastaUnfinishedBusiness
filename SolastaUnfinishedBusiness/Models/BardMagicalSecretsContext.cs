using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using static HeroDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class BardSpellcastingContext
{
    private sealed class Secret
    {
        internal int Original;
        internal FeatureDefinitionPointPool Pool;
    }
    private sealed class Choice
    {
        internal string Tag;
        internal int Original;
        internal int Awards;
        internal int Replaced;
        internal Secret Secret;
        internal int Phase => Secret == null ? 1 : 2;
    }
    private static readonly Dictionary<FeatureDefinition, Secret> SecretPools = new();
    private static readonly Dictionary<string, Secret> SecretTags = new();
    private static readonly ConditionalWeakTable<CharacterHeroBuildingData, BardSpellGrantLedger> Granted = new();

    internal static void RegisterSecrets(Dictionary<FeatureDefinition, string> origins)
    {
        foreach (var pair in origins)
        {
            if (pair.Key is not FeatureDefinitionPointPool pool || pool.ExtraSpellsTag != "MagicalSecrets") continue;
            var start = pair.Value.IndexOf("Original", StringComparison.Ordinal) + "Original".Length;
            var end = pair.Value.IndexOf("Event", start, StringComparison.Ordinal);
            var original = int.Parse(pair.Value.Substring(start, end - start));
            pool.maxSpellLevel = Math.Min(pool.MaxSpellLevel, SpellcastingProgressionPlan.MaxSpellLevel(original));
            SecretPools.Add(pool, new Secret { Original = original, Pool = pool });
        }
    }
    internal static void RegisterSecretTag(FeatureDefinition feature, string tag)
    {
        if (SecretPools.TryGetValue(feature, out var secret)) SecretTags[tag + secret.Pool.ExtraSpellsTag] = secret;
    }
    private static bool TrySecret(RulesetCharacterHero hero, string tag, out Secret secret)
    {
        secret = null;
        if (!BardProgressionContext.Active || hero.GetClassLevel(Bard) is < 1 or > 12 || !SecretTags.TryGetValue(tag, out secret)) return false;
        var actual = hero.GetClassLevel(Bard);
        var owner = tag.StartsWith(AttributeDefinitions.GetClassTag(Bard, actual) + Origin, StringComparison.Ordinal) ||
                    (hero.ClassesAndSubclasses.TryGetValue(Bard, out var subclass) &&
                     tag.StartsWith(AttributeDefinitions.GetSubclassTag(Bard, actual, subclass) + Origin, StringComparison.Ordinal));
        return owner && SpellcastingProgressionPlan.OriginalLevels(actual).Contains(secret.Original);
    }
    private static int AdditionalKnown(RulesetCharacterHero hero)
    {
        hero.EnumerateFeaturesToBrowse<ISpellCastingAffinityProvider>(hero.FeaturesToBrowse, null);
        return hero.FeaturesToBrowse.OfType<ISpellCastingAffinityProvider>().Sum(x => x.AdditionalKnownSpellsCount);
    }
    private static List<Choice> Choices(CharacterHeroBuildingData data)
    {
        var hero = data.HeroCharacter;
        var events = Events(hero);
        if (events.Count == 0) return new();
        var known = hero.GetClassSpellRepertoire(Bard)?.KnownSpells.Count ?? 0;
        var first = Math.Max(0, plan.Known[events[0].Original - 1] + AdditionalKnown(hero) - known);
        var result = events.Select((e, i) => new Choice
        {
            Tag = e.Tag,
            Original = e.Original,
            Awards = i == 0 ? first : e.Learned,
            Replaced = e.Replaced
        }).ToList();
        foreach (var tag in data.PointPoolStacks[PointsPoolType.CantripOrSpell].ActivePools.Keys)
            if (TrySecret(hero, tag, out var secret)) result.Add(new Choice
            {
                Tag = tag,
                Original = secret.Original,
                Awards = secret.Pool.PoolAmount,
                Secret = secret
            });
        return BardSpellLearningState<SpellDefinition>.Ordered(result.Select(x => (x, x.Original, x.Phase, x.Tag)));
    }
    private static BardSpellLearningState<SpellDefinition> Initial(CharacterHeroBuildingData data, bool pendingCantrips = true)
    {
        var repertoire = data.HeroCharacter.GetClassSpellRepertoire(Bard);
        var secrets = repertoire?.ExtraSpellsByTag.Values.SelectMany(x => x).Distinct() ?? Enumerable.Empty<SpellDefinition>();
        var state = new BardSpellLearningState<SpellDefinition>(repertoire?.KnownSpells ?? new(), repertoire?.KnownCantrips ?? new(), secrets, true);
        return pendingCantrips ? state.LearnCantrips(Chosen(data.AcquiredCantrips, Events(data.HeroCharacter)[0].Tag)) : state;
    }
    private static BardSpellLearningState<SpellDefinition> Apply(CharacterHeroBuildingData data, Choice e,
        BardSpellLearningState<SpellDefinition> state, bool complete)
    {
        var removed = Chosen(data.UnlearnedSpells, e.Tag);
        var added = Chosen(data.AcquiredSpells, e.Tag).Concat(e.Secret == null ? Enumerable.Empty<SpellDefinition>() : Chosen(data.AcquiredCantrips, e.Tag)).ToArray();
        if (removed.Count > e.Replaced || added.Length > e.Awards + removed.Count ||
            (complete && added.Length != e.Awards + removed.Count)) throw new InvalidOperationException("Incomplete Bard spell event: " + e.Tag);
        if (added.Any(spell => !Allowed(data.HeroCharacter, spell, e.Original, e.Secret)))
            throw new InvalidOperationException("Bard spell outside original event circle/list: " + e.Tag);
        return e.Secret == null ? state.LearnOrdinary(removed, added) : state.LearnSecrets(added);
    }
    private static BardSpellLearningState<SpellDefinition> Before(CharacterHeroBuildingData data, string tag)
    {
        var state = Initial(data);
        foreach (var e in Choices(data))
        {
            if (e.Tag == tag) return state;
            state = Apply(data, e, state, true);
        }
        throw new InvalidOperationException("Unknown Bard spell origin.");
    }
    private static bool InList(SpellListDefinition list, SpellDefinition spell) => list != null &&
        list.SpellsByLevel.Any(x => x.Spells.Contains(spell));
    private static bool Allowed(RulesetCharacterHero hero, SpellDefinition spell, int original, Secret secret)
    {
        if (secret != null)
            return spell.SpellLevel >= secret.Pool.MinSpellLevel && spell.SpellLevel <= secret.Pool.MaxSpellLevel &&
                   (!secret.Pool.RitualOnly || spell.Ritual) && InList(secret.Pool.SpellListOverride ?? Casting.SpellListDefinition, spell);
        if (spell.SpellLevel < 1 || spell.SpellLevel > SpellcastingProgressionPlan.MaxSpellLevel(original)) return false;
        return InClassLists(hero, spell, original);
    }
    private static bool InClassLists(RulesetCharacterHero hero, SpellDefinition spell, int original)
    {
        if (InList(Casting.SpellListDefinition, spell)) return true;
        // Extended access is not a second spellcasting repertoire. The 2024 switch unlocks this at original10.
        hero.EnumerateFeaturesToBrowse<FeatureDefinitionMagicAffinity>(hero.FeaturesToBrowse, null);
        return hero.FeaturesToBrowse.OfType<FeatureDefinitionMagicAffinity>()
            .Where(x => !x.Name.StartsWith("MagicAffinityBardMagicalSecrets", StringComparison.Ordinal) || original >= 10)
            .Any(x => InList(x.ExtendedSpellList, spell));
    }
    internal static void FilterAvailableSpells(RulesetCharacterHero hero, string tag, List<SpellDefinition> spells)
    {
        var original = StepOriginal(hero, tag);
        if (original == 0 || !hero.TryGetHeroBuildingData(out var data)) return;
        var secret = TrySecret(hero, tag, out var found) ? found : null;
        try
        {
            var known = Before(data, tag).All.ToArray();
            spells.RemoveAll(spell => !known.Contains(spell) &&
                !(secret == null && spell.SpellLevel == 0 ? InClassLists(hero, spell, original) : Allowed(hero, spell, original, secret)));
        }
        catch (InvalidOperationException) { spells.Clear(); }
    }

    private static void ValidateAll(CharacterHeroBuildingData data)
    {
        var state = Initial(data);
        foreach (var e in Choices(data)) state = Apply(data, e, state, true);
        var expected = Math.Max(data.HeroCharacter.GetClassSpellRepertoire(Bard)?.KnownSpells.Count ?? 0,
            Casting.KnownSpells[data.HeroCharacter.GetClassLevel(Bard) - 1] + AdditionalKnown(data.HeroCharacter));
        if (state.Ordinary.Count != expected) throw new InvalidOperationException("Bard ordinary known-spell count does not match final table.");
    }
    internal static bool BeginFinalize(RulesetCharacterHero hero)
    {
        if (!BardProgressionContext.Active || hero.GetClassLevel(Bard) == 0 || LevelUpHelper.GetSelectedClass(hero) != Bard) return true;
        try
        {
            if (!BardProgressionContext.SessionConsistent) throw new InvalidOperationException("Progression settings changed; full restart required.");
            if (!IsSupportedHero(hero)) throw new InvalidOperationException("Mixed casting is not supported.");
            var data = hero.GetHeroBuildingData();
            if (Granted.TryGetValue(data, out var ledger) && ledger.HasStarted(Events(hero)[0].Tag)) throw new InvalidOperationException("Bard spell finalization already started; cancel/reload after failure.");
            if (data.LevelupTrainedFeats.Values.SelectMany(x => x).SelectMany(x => x.Features)
                .SelectMany(CompressedProgressionDefinitions.Flatten).OfType<FeatureDefinitionCastSpell>()
                .Any(x => x != Casting && x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race))
                throw new InvalidOperationException("A feat grants a second casting repertoire.");
            ValidateAll(data); return true;
        }
        catch (InvalidOperationException ex)
        { Main.Error("Bard spell finalization rejected before hero mutation: " + ex.Message); return false; }
    }

    private static int StepOriginal(RulesetCharacterHero hero, string tag) =>
        TryEvent(hero, tag, out var original) ? original : TrySecret(hero, tag, out var secret) ? secret.Original : 0;
    private static int StepPhase(RulesetCharacterHero hero, string tag, PointsPoolType type) =>
        type == PointsPoolType.SpellUnlearn ? 0 : TrySecret(hero, tag, out _) ? 2 : 1;
}
