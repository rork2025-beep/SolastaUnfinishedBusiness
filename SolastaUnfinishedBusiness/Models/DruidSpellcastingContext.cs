using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Builders.Features;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;
using static HeroDefinitions;

namespace SolastaUnfinishedBusiness.Models;

// WholeList/Prepared casting remains native. Only class progression and transient bonus origins are adapted.
internal static class DruidSpellcastingContext
{
    private const string Origin = "Druid12";
    private static FeatureDefinitionCastSpell Casting => DatabaseHelper.FeatureDefinitionCastSpells.CastSpellDruid;
    private static readonly Dictionary<FeatureDefinition, FeatureDefinition> Adapted = new();
    private static readonly ConditionalWeakTable<CharacterHeroBuildingData, DruidSpellGrantLedger> Granted = new();

    internal static bool IsSupportedHero(RulesetCharacterHero hero) => hero != null &&
        !SharedSpellsContext.IsMulticaster(hero) &&
        !hero.SpellRepertoires.Any(x => x.SpellCastingFeature.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race &&
                                       x.SpellCastingFeature != Casting) &&
        !hero.ClassesAndLevels.Any(x => x.Key != Druid && IsCaster(hero, x.Key));

    internal static int SingleCasterLevel(RulesetCharacterHero hero) =>
        DruidProgressionContext.Active && hero != null && hero.GetClassLevel(Druid) > 0 && IsSupportedHero(hero)
            ? SpellcastingProgressionPlan.CasterLevel(hero.GetClassLevel(Druid)) : 0;

    private static bool IsCaster(RulesetCharacterHero hero, CharacterClassDefinition klass)
    {
        hero.ClassesAndSubclasses.TryGetValue(klass, out var sub);
        return klass.Name == RuleDefinitions.WarlockClass || SharedSpellsContext.GetCasterTypeForClassOrSubclass(
            klass.Name, sub?.Name) != FeatureDefinitionCastSpellBuilder.CasterProgression.None ||
            klass.FeatureUnlocks.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
                .OfType<FeatureDefinitionCastSpell>().Any(x => x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race) ||
            (sub != null && IsCastingSubclass(sub));
    }
    private static bool IsCastingSubclass(CharacterSubclassDefinition sub) => SharedSpellsContext.SubclassCasterType.ContainsKey(sub.Name) ||
        sub.FeatureUnlocks.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionCastSpell>().Any(x => x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race);
    internal static bool CanSelectClass(RulesetCharacterHero hero, CharacterClassDefinition klass)
    {
        if (!DruidProgressionContext.Active) return true;
        if (klass == Druid) return IsSupportedHero(hero);
        if (hero.GetClassLevel(Druid) == 0) return true;
        if (IsCaster(hero, klass)) return false;
        if (hero.ClassesAndSubclasses.ContainsKey(klass)) return true;
        return klass.FeatureUnlocks.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
            .OfType<FeatureDefinitionSubclassChoice>().All(choice => choice.Subclasses.Any(name =>
                DatabaseHelper.TryGetDefinition<CharacterSubclassDefinition>(name, out var sub) && !sub.GuiPresentation.Hidden && !IsCastingSubclass(sub)));
    }

    internal static void Initialize()
    {
        if (Casting.SpellKnowledge != RuleDefinitions.SpellKnowledge.WholeList ||
            Casting.SpellReadyness != RuleDefinitions.SpellReadyness.Prepared)
            throw new InvalidOperationException("Druid prototype requires the installed WholeList/Prepared casting definition.");
        var slots = SpellcastingProgressionPlan.ProjectRows(Casting.SlotsPerLevels).Select((row, index) =>
            new FeatureDefinitionCastSpell.SlotsByLevelDuplet { Level = index + 1, Slots = row.Slots.ToList() }).ToList();
        var cantrips = SpellcastingProgressionPlan.ProjectCumulative(KnownSpellsProgressionPlan.RetainedCantrips(Casting.KnownCantrips));
        Casting.slotsPerLevels = slots;
        Casting.KnownCantrips.Clear(); Casting.KnownCantrips.AddRange(cantrips);
        // No learned/scribed/replacement spell budgets: Druid prepares from its own whole list.
        Main.Info("Druid WholeList/Prepared; EnablePreparedSpellsTables2024=" + Main.Settings.EnablePreparedSpellsTables2024 +
                  "; UseAlternateSpellPointsSystem=" + Main.Settings.UseAlternateSpellPointsSystem);
    }

    internal static FeatureDefinition AdaptAutoPrepared(FeatureDefinition source)
    {
        if (Adapted.TryGetValue(source, out var found)) return found;
        var result = source;
        if (source is FeatureDefinitionAutoPreparedSpells auto && auto.SpellcastingClass == Druid)
        {
            var groups = auto.AutoPreparedSpellsGroups.Select(x => AutoPreparedSpellsGroupBuilder.BuildSpellGroup(
                FighterProgressionPlan<int>.CompressedLevel(x.ClassLevel), x.SpellsList.ToArray())).ToArray();
            var copy = FeatureDefinitionAutoPreparedSpellsBuilder.Create(auto, Origin + auto.Name).SetPreparedSpellGroups(groups).AddToDB();
            CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(auto, copy); result = copy;
        }
        else if (source is FeatureDefinitionFeatureSet set)
        {
            var children = set.FeatureSet.Select(AdaptAutoPrepared).ToArray();
            if (!children.SequenceEqual(set.FeatureSet))
            {
                var copy = FeatureDefinitionFeatureSetBuilder.Create(set, Origin + "Auto" + set.Name).AddToDB();
                copy.FeatureSet.Clear(); copy.FeatureSet.AddRange(children);
                CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(set, copy); result = copy;
            }
        }
        Adapted[source] = result; return result;
    }

    private static string ClassTag(RulesetCharacterHero hero) => AttributeDefinitions.GetClassTag(Druid, hero.GetClassLevel(Druid));
    private static bool OwnTag(RulesetCharacterHero hero, string tag)
    {
        if (!DruidProgressionContext.Active || string.IsNullOrEmpty(tag) || hero.GetClassLevel(Druid) is < 1 or > 12) return false;
        return DruidProgressionRules.IsOriginTag(tag, ClassTag(hero)) ||
               (hero.ClassesAndSubclasses.TryGetValue(Druid, out var sub) &&
                DruidProgressionRules.IsOriginTag(tag, AttributeDefinitions.GetSubclassTag(Druid, hero.GetClassLevel(Druid), sub)));
    }
    private static bool Allowed(RulesetCharacterHero hero, PointPool pool, PointsPoolType type, SpellDefinition spell)
    {
        var list = pool.SpellListOverride ?? Casting.SpellListDefinition;
        if ((type == PointsPoolType.Cantrip && spell.SpellLevel != 0) || spell.SpellLevel < pool.MinSpellLevel ||
            spell.SpellLevel > pool.MaxSpellLevel || (pool.RitualOnly && !spell.Ritual)) return false;
        if (list.SpellsByLevel.Any(x => x.Spells.Contains(spell))) return true;
        // Preserve only explicitly granted extended lists; a Nature cantrip pool is not Druid casting.
        hero.EnumerateFeaturesToBrowse<FeatureDefinitionMagicAffinity>(hero.FeaturesToBrowse, null);
        return hero.FeaturesToBrowse.OfType<FeatureDefinitionMagicAffinity>().Where(x => x.ExtendedSpellList)
            .Any(x => x.ExtendedSpellList.SpellsByLevel.Any(level => level.Spells.Contains(spell)));
    }
    private static IEnumerable<SpellDefinition> ExistingSpells(RulesetCharacterHero hero)
    {
        var rep = hero.GetClassSpellRepertoire(Druid);
        return rep == null ? Enumerable.Empty<SpellDefinition>() : rep.KnownCantrips.Concat(rep.KnownSpells)
            .Concat(rep.ExtraSpellsByTag.Values.SelectMany(x => x));
    }
    private static bool IsSpellPool(PointsPoolType type) => type is PointsPoolType.Cantrip or PointsPoolType.Spell or PointsPoolType.CantripOrSpell;
    private static bool PendingCaster(CharacterHeroBuildingData data) => data.LevelupTrainedFeats.Values.SelectMany(x => x).SelectMany(x => x.Features)
        .Concat(data.HeroCharacter.TrainedFightingStyles.SelectMany(x => x.Features))
        .SelectMany(CompressedProgressionDefinitions.Flatten).OfType<FeatureDefinitionCastSpell>()
        .Any(x => x != Casting && x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race);

    internal static bool BeginFinalize(RulesetCharacterHero hero)
    {
        if (!DruidProgressionContext.Active || hero.GetClassLevel(Druid) == 0) return true;
        try
        {
            if (!DruidProgressionContext.SessionConsistent) throw new InvalidOperationException("Rules changed: full restart required.");
            if (!IsSupportedHero(hero)) throw new InvalidOperationException("Mixed casting repertoires are unsupported.");
            var data = hero.GetHeroBuildingData();
            if (PendingCaster(data)) throw new InvalidOperationException("Undo the choice granting a second casting repertoire.");
            if (LevelUpHelper.GetSelectedClass(hero) != Druid) return true;
            if (Granted.TryGetValue(data, out var ledger) && ledger.HasStarted(ClassTag(hero)))
                throw new InvalidOperationException("Druid finalization already started: cancel/reload after failure.");
            var known = new HashSet<SpellDefinition>(ExistingSpells(hero));
            foreach (var stack in data.PointPoolStacks.Where(x => IsSpellPool(x.Key)))
            foreach (var pair in stack.Value.ActivePools.Where(x => OwnTag(hero, x.Key)))
            {
                if (pair.Value.RemainingPoints > 0) throw new InvalidOperationException("Complete each independent Druid/domain choice.");
                var chosen = (data.AcquiredCantrips.TryGetValue(pair.Key, out var cantrips) ? cantrips : new List<SpellDefinition>())
                    .Concat(data.AcquiredSpells.TryGetValue(pair.Key, out var spells) ? spells : new List<SpellDefinition>()).ToArray();
                if (chosen.Length > pair.Value.MaxPoints || chosen.Any(x => !Allowed(hero, pair.Value, stack.Key, x) || !known.Add(x)))
                    throw new InvalidOperationException("Duplicate or invalid Druid/domain spell choice.");
            }
            return true;
        }
        catch (InvalidOperationException ex)
        { Main.Error("Druid finalization rejected before hero mutation: " + ex.Message); return false; }
    }

    [HarmonyPatch(typeof(RulesetCharacterHero), nameof(RulesetCharacterHero.ComputeMaxPreparedSpells))]
    internal static class PreparedPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static bool Prefix(RulesetCharacterHero __instance, RulesetSpellRepertoire spellRepertoire, ref int __result)
        {
            if (!DruidProgressionContext.Active || spellRepertoire.SpellCastingClass != Druid ||
                Main.Settings.EnablePreparedSpellsTables2024 || spellRepertoire.SpellCastingFeature != Casting ||
                Casting.SpellPreparationCount != RuleDefinitions.SpellPreparationCount.AbilityBonusPlusLevel) return true;
            var wisdom = AttributeDefinitions.ComputeAbilityScoreModifier(__instance.GetAttribute(spellRepertoire.SpellCastingAbility).CurrentValue);
            var bonuses = __instance.FeaturesToBrowse.OfType<ISpellCastingAffinityProvider>().Sum(x => x.PreparedSpellModifier switch
            {
                RuleDefinitions.PreparedSpellsModifier.ProficiencyBonus => __instance.TryGetAttributeValue(AttributeDefinitions.ProficiencyBonus),
                RuleDefinitions.PreparedSpellsModifier.SpellcastingAbilityBonus => wisdom,
                _ => 0
            });
            __result = DruidProgressionRules.LegacyPreparedLimit(DruidProgressionContext.EffectiveLevel(__instance), wisdom, bonuses);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.ComputeAutopreparedSpells))]
    internal static class AutoPreparedPatch
    {
        internal static void Postfix(RulesetCharacter __instance, RulesetSpellRepertoire spellRepertoire)
        {
            if (!DruidProgressionContext.Active || spellRepertoire.SpellCastingClass != Druid || __instance.GetClassLevel(Druid) == 0) return;
            // Multiple legitimate feature sources may reference the same domain spell. Keep one executable entry.
            var distinct = spellRepertoire.AutoPreparedSpells.Distinct().ToArray();
            spellRepertoire.AutoPreparedSpells.Clear();
            spellRepertoire.AutoPreparedSpells.AddRange(distinct);
        }
    }

    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AcquireSpell))]
    internal static class ChoiceBoundaryPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, SpellDefinition spell, string tag, PointsPoolType poolType)
        {
            var data = heroBuildingData;
            if (!OwnTag(data.HeroCharacter, tag) || !IsSpellPool(poolType)) return true;
            if (!data.PointPoolStacks[poolType].ActivePools.TryGetValue(tag, out var pool) || pool.RemainingPoints <= 0 || !Allowed(data.HeroCharacter, pool, poolType, spell)) return false;
            return !ExistingSpells(data.HeroCharacter).Concat(data.AcquiredCantrips.Values.SelectMany(x => x))
                .Concat(data.AcquiredSpells.Values.SelectMany(x => x)).Concat(data.BonusCantrips.Values.SelectMany(x => x)).Contains(spell);
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.GetSpellFeature))]
    internal static class ChoiceFeaturePatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, string tag, ref FeatureDefinitionCastSpell __result)
        {
            if (!OwnTag(heroBuildingData.HeroCharacter, tag) || !tag.Contains(Origin)) return true;
            __result = Casting; return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.GrantCantripsAndSpellsByTag))]
    internal static class GrantChoicesPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, string tag, FeatureDefinitionCastSpell spellcastingFeature) =>
            spellcastingFeature != Casting || !OwnTag(heroBuildingData.HeroCharacter, tag) ||
            Granted.GetValue(heroBuildingData, _ => new DruidSpellGrantLedger()).Grant(ClassTag(heroBuildingData.HeroCharacter), tag);
        internal static void Postfix(CharacterBuildingManager __instance, CharacterHeroBuildingData heroBuildingData,
            string tag, FeatureDefinitionCastSpell spellcastingFeature, bool __runOriginal)
        {
            if (!__runOriginal || spellcastingFeature != Casting || !OwnTag(heroBuildingData.HeroCharacter, tag) || tag.Contains(Origin)) return;
            foreach (var key in heroBuildingData.AcquiredCantrips.Keys.Concat(heroBuildingData.AcquiredSpells.Keys).Concat(heroBuildingData.BonusCantrips.Keys)
                         .Where(x => x.StartsWith(tag + Origin, StringComparison.Ordinal)).Distinct().OrderBy(x => x).ToArray())
                __instance.GrantCantripsAndSpellsByTag(heroBuildingData, key, Casting, false);
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.ClearPrevious))]
    internal static class ClearChoicesPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacterHero hero, string tag)
        {
            if (!DruidProgressionContext.Active || string.IsNullOrEmpty(tag) || !hero.TryGetHeroBuildingData(out var data)) return;
            Granted.Remove(data);
            foreach (var key in data.AcquiredCantrips.Keys.Concat(data.AcquiredSpells.Keys).Concat(data.BonusCantrips.Keys)
                         .Concat(data.PointPoolStacks.Values.SelectMany(x => x.ActivePools.Keys))
                         .Where(x => x.StartsWith(tag + Origin, StringComparison.Ordinal)).Distinct().ToArray())
            {
                data.AcquiredCantrips.Remove(key); data.AcquiredSpells.Remove(key); data.BonusCantrips.Remove(key); data.UnlearnedSpells.Remove(key);
                foreach (var stack in data.PointPoolStacks.Values) stack.ActivePools.Remove(key);
            }
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.RemoveActiveFeaturesFromHeroByTag))]
    internal static class RemoveChoicesPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(RulesetCharacterHero hero, string tag) => ClearChoicesPatch.Prefix(hero, tag);
    }
    [HarmonyPatch(typeof(CharacterStageSubclassSelectionPanel), nameof(CharacterStageSubclassSelectionPanel.OnBeginShow))]
    internal static class CasterSubclassSelectionPatch
    {
        internal static void Prefix(CharacterStageSubclassSelectionPanel __instance)
        {
            if (DruidProgressionContext.Active && __instance.currentHero.GetClassLevel(Druid) > 0)
                __instance.compatibleSubclasses.RemoveAll(IsCastingSubclass);
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AssignSubclass))]
    internal static class CasterSubclassBoundaryPatch
    {
        internal static bool Prefix(RulesetCharacterHero hero, CharacterSubclassDefinition subclassDefinition)
        {
            if (!DruidProgressionContext.Active || hero.GetClassLevel(Druid) == 0 || !IsCastingSubclass(subclassDefinition)) return true;
            Main.Error("Druid prototype: a second casting subclass is unsupported. Choose a noncasting subclass."); return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.IsFeatMatchingPrerequisites))]
    internal static class CastingFeatBoundaryPatch
    {
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData, FeatDefinition feat, ref bool __result)
        {
            if (DruidProgressionContext.Active && heroBuildingData.HeroCharacter.GetClassLevel(Druid) > 0 &&
                feat.Features.SelectMany(CompressedProgressionDefinitions.Flatten).OfType<FeatureDefinitionCastSpell>()
                    .Any(x => x != Casting && x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race)) __result = false;
        }
    }
}
