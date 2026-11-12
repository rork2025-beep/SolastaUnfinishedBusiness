using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using static HeroDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class RangerSpellcastingContext
{
    private static readonly ConditionalWeakTable<CharacterHeroBuildingData, RangerSpellGrantLedger> Granted = new();
    private static bool OwnTag(RulesetCharacterHero hero, string tag)
    {
        if (!RangerProgressionContext.Active || hero.GetClassLevel(Ranger) is < 1 or > 12) return false;
        var parent = AttributeDefinitions.GetClassTag(Ranger, hero.GetClassLevel(Ranger));
        return RangerProgressionRules.IsOriginTag(tag, parent) || hero.ClassesAndSubclasses.TryGetValue(Ranger, out var sub) &&
            RangerProgressionRules.IsOriginTag(tag, AttributeDefinitions.GetSubclassTag(Ranger, hero.GetClassLevel(Ranger), sub));
    }
    private static int AdditionalKnown(RulesetCharacterHero hero)
    {
        hero.EnumerateFeaturesToBrowse<ISpellCastingAffinityProvider>(hero.FeaturesToBrowse, null);
        return hero.FeaturesToBrowse.OfType<ISpellCastingAffinityProvider>().Sum(x => x.AdditionalKnownSpellsCount);
    }
    private static bool Allowed(RulesetCharacterHero hero, SpellDefinition spell, int original)
    {
        if (spell.SpellLevel < 1 || spell.SpellLevel > MaximumSpellLevel(original)) return false;
        bool InList(SpellListDefinition list) => list != null && list.SpellsByLevel.Any(x => x.Spells.Contains(spell));
        if (InList(Casting.SpellListDefinition)) return true;
        hero.EnumerateFeaturesToBrowse<FeatureDefinitionMagicAffinity>(hero.FeaturesToBrowse, null);
        return hero.FeaturesToBrowse.OfType<FeatureDefinitionMagicAffinity>()
            .Where(x => RangerProgressionContext.OriginalUnlockLevel(x) <= original).Any(x => InList(x.ExtendedSpellList));
    }
    private static RangerSpellLearningState<SpellDefinition> Initial(CharacterHeroBuildingData data) =>
        new(data.HeroCharacter.GetClassSpellRepertoire(Ranger)?.KnownSpells ?? new List<SpellDefinition>());
    private static RangerSpellLearningState<SpellDefinition> Apply(CharacterHeroBuildingData data,
        (string Tag, int Original, int Learned, int Replaced) e, RangerSpellLearningState<SpellDefinition> state, bool complete)
    {
        var first = Events(data.HeroCharacter)[0];
        var current = data.HeroCharacter.GetClassSpellRepertoire(Ranger)?.KnownSpells.Count ?? 0;
        var awards = e.Tag == first.Tag ? Math.Max(0, plan.Known[e.Original - 1] + AdditionalKnown(data.HeroCharacter) - current) : e.Learned;
        return state.Apply(Chosen(data.UnlearnedSpells, e.Tag), Chosen(data.AcquiredSpells, e.Tag),
            awards, data.CanUnlearn ? e.Replaced : 0, complete, spell => Allowed(data.HeroCharacter, spell, e.Original));
    }
    private static IReadOnlyList<SpellDefinition> Before(CharacterHeroBuildingData data, int original)
    {
        var state = Initial(data);
        foreach (var e in Events(data.HeroCharacter).Where(x => x.Original < original)) state = Apply(data, e, state, true);
        return state.Known;
    }
    private static void ValidateAll(CharacterHeroBuildingData data)
    {
        var state = Initial(data);
        foreach (var e in Events(data.HeroCharacter)) state = Apply(data, e, state, true);
        var expected = Math.Max(data.HeroCharacter.GetClassSpellRepertoire(Ranger)?.KnownSpells.Count ?? 0,
            Casting.KnownSpells[data.HeroCharacter.GetClassLevel(Ranger) - 1] + AdditionalKnown(data.HeroCharacter));
        if (state.Known.Count != expected) throw new InvalidOperationException("Known-spell count does not match the final Ranger source table.");
        foreach (var stack in data.PointPoolStacks.Where(x => x.Key is PointsPoolType.Cantrip or PointsPoolType.CantripOrSpell))
        foreach (var pair in stack.Value.ActivePools.Where(x => OwnTag(data.HeroCharacter, x.Key)))
            if (pair.Value.RemainingPoints > 0) throw new InvalidOperationException("Complete each independent Ranger/subclass cantrip or bonus-spell pool.");
    }
    internal static void FilterAvailableSpells(RulesetCharacterHero hero, string tag, List<SpellDefinition> spells)
    {
        if (!TryEvent(hero, tag, out var original)) return;
        try
        {
            var known = KnownBefore(hero.GetHeroBuildingData(), original);
            spells.RemoveAll(spell => spell.SpellLevel > 0 && !known.Contains(spell) && !Allowed(hero, spell, original));
        }
        catch (InvalidOperationException) { spells.Clear(); }
    }
    internal static bool BeginFinalize(RulesetCharacterHero hero)
    {
        if (!RangerProgressionContext.Active || hero.GetClassLevel(Ranger) == 0) return true;
        try
        {
            if (!RangerProgressionContext.SessionConsistent) throw new InvalidOperationException("Rules changed; full restart required.");
            if (!IsSupportedHero(hero)) throw new InvalidOperationException("Mixed casting is not supported.");
            var data = hero.GetHeroBuildingData();
            if (data.LevelupTrainedFeats.Values.SelectMany(x => x).SelectMany(x => x.Features)
                .Concat(hero.TrainedFightingStyles.SelectMany(x => x.Features)).SelectMany(CompressedProgressionDefinitions.Flatten)
                .OfType<FeatureDefinitionCastSpell>().Any(x => x != Casting && x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race))
                throw new InvalidOperationException("Undo the choice granting a second casting repertoire.");
            if (LevelUpHelper.GetSelectedClass(hero) != Ranger) return true;
            var parent = AttributeDefinitions.GetClassTag(Ranger, hero.GetClassLevel(Ranger));
            if (Granted.TryGetValue(data, out var ledger) && ledger.HasStarted(parent))
                throw new InvalidOperationException("Finalization already started; cancel/reload after failure.");
            RangerInvocationContext.Validate(data);
            ValidateAll(data); return true;
        }
        catch (InvalidOperationException ex)
        { Main.Error("Ranger finalization rejected before hero mutation: " + ex.Message); return false; }
    }
}
