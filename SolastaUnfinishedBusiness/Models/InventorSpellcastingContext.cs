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
using static SolastaUnfinishedBusiness.Classes.InventorClass;
using static HeroDefinitions;

namespace SolastaUnfinishedBusiness.Models;

// WholeList/Prepared casting remains native. Only class progression and transient bonus origins are adapted.
internal static class InventorSpellcastingContext
{
    private const string Origin = "Inventor12";
    private static FeatureDefinitionCastSpell Casting => SpellCasting;
    private static readonly Dictionary<FeatureDefinition, FeatureDefinition> Adapted = new();
    private static readonly ConditionalWeakTable<CharacterHeroBuildingData, InventorSpellGrantLedger> Granted = new();

    private static bool halfRoundUp;
    private static List<FeatureDefinitionCastSpell.SlotsByLevelDuplet> projectedSlots;
    [HarmonyPatch(typeof(SpellPointsContext), nameof(SpellPointsContext.SwitchFeatureDefinitionCastSpellSlots))]
    internal static class SlotsRefreshPatch
    {
        internal static void Postfix()
        {
            if (InventorProgressionContext.Active) Casting.slotsPerLevels = projectedSlots;
        }
    }
    internal static void CaptureRules() => halfRoundUp = true;
    internal static bool IsSupportedHero(RulesetCharacterHero hero) => hero != null &&
        !SharedSpellsContext.IsMulticaster(hero) &&
        !hero.SpellRepertoires.Any(x => x.SpellCastingFeature.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race &&
                                       x.SpellCastingFeature != Casting) &&
        !hero.ClassesAndLevels.Any(x => x.Key != Class && IsCaster(hero, x.Key));

    internal static int SingleCasterLevel(RulesetCharacterHero hero) =>
        InventorProgressionContext.Active && hero != null && hero.GetClassLevel(Class) > 0 && IsSupportedHero(hero)
            ? InventorProgressionRules.CasterContribution(InventorProgressionContext.EffectiveLevel(hero), halfRoundUp) : 0;

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
        if (!InventorProgressionContext.Active) return true;
        if (klass == Class) return IsSupportedHero(hero);
        if (hero.GetClassLevel(Class) == 0) return true;
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
            throw new InvalidOperationException("Inventor prototype requires the installed WholeList/Prepared casting definition.");
        var slots = SpellcastingProgressionPlan.ProjectRows(Casting.SlotsPerLevels).Select((row, index) =>
            new FeatureDefinitionCastSpell.SlotsByLevelDuplet { Level = index + 1, Slots = row.Slots.ToList() }).ToList();
        var cantrips = SpellcastingProgressionPlan.ProjectCumulative(KnownSpellsProgressionPlan.RetainedCantrips(Casting.KnownCantrips));
        projectedSlots = slots;
        Casting.slotsPerLevels = slots;
        Casting.KnownCantrips.Clear(); Casting.KnownCantrips.AddRange(cantrips);
        // No learned/scribed/replacement spell budgets: Inventor prepares from its own whole list.
        Main.Info("Inventor WholeList/Prepared; EnablePreparedSpellsTables2024=" + Main.Settings.EnablePreparedSpellsTables2024 +
                  "; UseAlternateSpellPointsSystem=" + Main.Settings.UseAlternateSpellPointsSystem);
    }

    internal static FeatureDefinition AdaptAutoPrepared(FeatureDefinition source)
    {
        if (Adapted.TryGetValue(source, out var found)) return found;
        var result = source;
        if (source is FeatureDefinitionAutoPreparedSpells auto && auto.SpellcastingClass == Class)
        {
            // Keep low-level Innovation spell feature identities for late-load consumers.
            if (auto.AutoPreparedSpellsGroups.All(x => x.ClassLevel <= 5)) return Adapted[source] = source;
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

    private static string ClassTag(RulesetCharacterHero hero) => AttributeDefinitions.GetClassTag(Class, hero.GetClassLevel(Class));
    private static bool OwnTag(RulesetCharacterHero hero, string tag)
    {
        if (!InventorProgressionContext.Active || string.IsNullOrEmpty(tag) || hero.GetClassLevel(Class) is < 1 or > 12) return false;
        return InventorProgressionRules.IsOriginTag(tag, ClassTag(hero)) ||
               (hero.ClassesAndSubclasses.TryGetValue(Class, out var sub) &&
                InventorProgressionRules.IsOriginTag(tag, AttributeDefinitions.GetSubclassTag(Class, hero.GetClassLevel(Class), sub)));
    }
    private static bool Allowed(RulesetCharacterHero hero, PointPool pool, PointsPoolType type, SpellDefinition spell)
    {
        var list = pool.SpellListOverride ?? Casting.SpellListDefinition;
        if ((type == PointsPoolType.Cantrip && spell.SpellLevel != 0) || spell.SpellLevel < pool.MinSpellLevel ||
            spell.SpellLevel > pool.MaxSpellLevel || (pool.RitualOnly && !spell.Ritual)) return false;
        if (list.SpellsByLevel.Any(x => x.Spells.Contains(spell))) return true;
        // Preserve explicitly granted innovation/fighting-style spell lists without creating a second repertoire.
        hero.EnumerateFeaturesToBrowse<FeatureDefinitionMagicAffinity>(hero.FeaturesToBrowse, null);
        return hero.FeaturesToBrowse.OfType<FeatureDefinitionMagicAffinity>().Where(x => x.ExtendedSpellList)
            .Any(x => x.ExtendedSpellList.SpellsByLevel.Any(level => level.Spells.Contains(spell)));
    }
    private static IEnumerable<SpellDefinition> ExistingSpells(RulesetCharacterHero hero)
    {
        var rep = hero.GetClassSpellRepertoire(Class);
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
        if (!InventorProgressionContext.Active || hero.GetClassLevel(Class) == 0) return true;
        try
        {
            if (!InventorProgressionContext.CanUseClassMechanics(hero)) throw new InvalidOperationException("Unsupported old hero or rules changed: new test hero/full restart required.");
            if (!IsSupportedHero(hero)) throw new InvalidOperationException("Mixed casting repertoires are unsupported.");
            var data = hero.GetHeroBuildingData();
            if (PendingCaster(data)) throw new InvalidOperationException("Undo the choice granting a second casting repertoire.");
            if (LevelUpHelper.GetSelectedClass(hero) != Class) return true;
            if (Granted.TryGetValue(data, out var ledger) && ledger.HasStarted(ClassTag(hero)))
                throw new InvalidOperationException("Inventor finalization already started: cancel/reload after failure.");
            var known = new HashSet<SpellDefinition>(ExistingSpells(hero));
            foreach (var stack in data.PointPoolStacks.Where(x => IsSpellPool(x.Key)))
            foreach (var pair in stack.Value.ActivePools.Where(x => OwnTag(hero, x.Key)))
            {
                if (pair.Value.RemainingPoints > 0) throw new InvalidOperationException("Complete each independent Inventor/innovation choice.");
                var chosen = (data.AcquiredCantrips.TryGetValue(pair.Key, out var cantrips) ? cantrips : new List<SpellDefinition>())
                    .Concat(data.AcquiredSpells.TryGetValue(pair.Key, out var spells) ? spells : new List<SpellDefinition>()).ToArray();
                if (chosen.Length > pair.Value.MaxPoints || chosen.Any(x => !Allowed(hero, pair.Value, stack.Key, x) || !known.Add(x)))
                    throw new InvalidOperationException("Duplicate or invalid Inventor/innovation spell choice.");
            }
            return true;
        }
        catch (InvalidOperationException ex)
        { Main.Error("Inventor finalization rejected before hero mutation: " + ex.Message); return false; }
    }

    [HarmonyPatch(typeof(RulesetCharacterHero), nameof(RulesetCharacterHero.ComputeMaxPreparedSpells))]
    internal static class PreparedPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static bool Prefix(RulesetCharacterHero __instance, RulesetSpellRepertoire spellRepertoire, ref int __result)
        {
            if (!InventorProgressionContext.Active || spellRepertoire.SpellCastingClass != Class ||
                spellRepertoire.SpellCastingFeature != Casting ||
                Casting.SpellPreparationCount != RuleDefinitions.SpellPreparationCount.AbilityBonusPlusHalfLevel) return true;
            var intelligence = AttributeDefinitions.ComputeAbilityScoreModifier(__instance.GetAttribute(spellRepertoire.SpellCastingAbility).CurrentValue);
            var bonuses = __instance.FeaturesToBrowse.OfType<ISpellCastingAffinityProvider>().Sum(x => x.PreparedSpellModifier switch
            {
                RuleDefinitions.PreparedSpellsModifier.ProficiencyBonus => __instance.TryGetAttributeValue(AttributeDefinitions.ProficiencyBonus),
                RuleDefinitions.PreparedSpellsModifier.SpellcastingAbilityBonus => intelligence,
                _ => 0
            });
            __result = InventorProgressionRules.LegacyPreparedLimit(InventorProgressionContext.EffectiveLevel(__instance), intelligence, bonuses);
            return false;
        }
    }

    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.ComputeAutopreparedSpells))]
    internal static class AutoPreparedPatch
    {
        internal static void Postfix(RulesetCharacter __instance, RulesetSpellRepertoire spellRepertoire)
        {
            if (!InventorProgressionContext.Active || spellRepertoire.SpellCastingClass != Class || __instance.GetClassLevel(Class) == 0) return;
            // Multiple legitimate feature sources may reference the same innovation spell. Keep one executable entry.
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
            Granted.GetValue(heroBuildingData, _ => new InventorSpellGrantLedger()).Grant(ClassTag(heroBuildingData.HeroCharacter), tag);
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
            if (!InventorProgressionContext.Active || string.IsNullOrEmpty(tag) || !hero.TryGetHeroBuildingData(out var data)) return;
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
            if (InventorProgressionContext.Active && __instance.currentHero.GetClassLevel(Class) > 0)
                __instance.compatibleSubclasses.RemoveAll(IsCastingSubclass);
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AssignSubclass))]
    internal static class CasterSubclassBoundaryPatch
    {
        internal static bool Prefix(RulesetCharacterHero hero, CharacterSubclassDefinition subclassDefinition)
        {
            if (!InventorProgressionContext.Active || hero.GetClassLevel(Class) == 0 || !IsCastingSubclass(subclassDefinition)) return true;
            Main.Error("Inventor prototype: a second casting subclass is unsupported. Choose a noncasting subclass."); return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.IsFeatMatchingPrerequisites))]
    internal static class CastingFeatBoundaryPatch
    {
        internal static void Postfix(CharacterHeroBuildingData heroBuildingData, FeatDefinition feat, ref bool __result)
        {
            if (InventorProgressionContext.Active && heroBuildingData.HeroCharacter.GetClassLevel(Class) > 0 &&
                feat.Features.SelectMany(CompressedProgressionDefinitions.Flatten).OfType<FeatureDefinitionCastSpell>()
                    .Any(x => x != Casting && x.SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Race)) __result = false;
        }
    }
}
