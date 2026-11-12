using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.CustomUI;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static class RangerInvocationContext
{
    internal static bool KeepBaseEvent(FeatureUnlockByLevel unlock, IEnumerable<FeatureUnlockByLevel> source)
    {
        var name = unlock.FeatureDefinition.Name;
        var pool = name == "AdditionalDamageRangerFavoredEnemyChoice" ? RulesContext.InvocationPoolRangerPreferredEnemy :
            name == "TerrainTypeAffinityRangerNaturalExplorerChoice" ? RulesContext.InvocationPoolRangerTerrainType : null;
        return RangerProgressionRules.KeepLegacySelection(Main.Settings.EnableRangerFavoredEnemy2024, pool != null,
            pool != null && source.Any(x => x.Level == unlock.Level && x.FeatureDefinition == pool));
    }
    private const string Origin = "Ranger12Invocation";
    private static readonly Dictionary<FeatureDefinitionCustomInvocationPool, string> Origins = new();
    internal static FeatureDefinition Adapt(FeatureDefinition source, string owner, int original, int index) =>
        Adapt(source, RangerInvocationPlan.Origin(owner, original, index));
    private static FeatureDefinition Adapt(FeatureDefinition source, string origin)
    {
        if (source is FeatureDefinitionCustomInvocationPool pool)
        {
            var clone = CustomInvocationPoolDefinitionBuilder.Create(pool, Origin + origin + pool.Name)
                .Setup(pool.PoolType, pool.Points, pool.IsUnlearn).AddToDB();
            CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(pool, clone); Origins.Add(clone, origin); return clone;
        }
        if (source is not FeatureDefinitionFeatureSet set || !CompressedProgressionDefinitions.Flatten(set).OfType<FeatureDefinitionCustomInvocationPool>().Any()) return source;
        var copy = FeatureDefinitionFeatureSetBuilder.Create(set, Origin + origin + set.Name).AddToDB();
        copy.FeatureSet.Clear(); copy.FeatureSet.AddRange(set.FeatureSet.Select((x, i) => Adapt(x, origin + "Child" + i)));
        CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(set, copy); return copy;
    }
    [HarmonyPatch(typeof(CustomInvocationSelectionPanel), "UpdateGrantedFeatures")]
    internal static class OriginPatch
    {
        internal static void Postfix(List<(string, FeatureDefinitionCustomInvocationPool)> ____gainedCustomFeatures)
        {
            if (!RangerProgressionContext.Active) return;
            for (var i = 0; i < ____gainedCustomFeatures.Count; i++)
            {
                var (tag, pool) = ____gainedCustomFeatures[i];
                if (Origins.TryGetValue(pool, out var origin)) ____gainedCustomFeatures[i] = (tag + Origin + origin, pool);
            }
        }
    }
    private static Dictionary<string, FeatureDefinitionCustomInvocationPool> Current(RulesetCharacterHero hero)
    {
        var result = new Dictionary<string, FeatureDefinitionCustomInvocationPool>();
        if (!RangerProgressionContext.Active || hero.GetClassLevel(Ranger) is < 1 or > 12) return result;
        Add(Ranger.FeatureUnlocks, AttributeDefinitions.GetClassTag(Ranger, hero.GetClassLevel(Ranger)));
        if (hero.ClassesAndSubclasses.TryGetValue(Ranger, out var sub)) Add(sub.FeatureUnlocks, AttributeDefinitions.GetSubclassTag(Ranger, hero.GetClassLevel(Ranger), sub));
        return result;
        void Add(IEnumerable<FeatureUnlockByLevel> unlocks, string parent)
        {
            foreach (var pool in unlocks.Where(x => x.Level == hero.GetClassLevel(Ranger)).SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionCustomInvocationPool>())
                if (Origins.TryGetValue(pool, out var origin)) result.Add(parent + Origin + origin, pool);
        }
    }
    internal static void Validate(CharacterHeroBuildingData data)
    {
        var current = Current(data.HeroCharacter);
        var events = current.Select(pair =>
            (Origin: pair.Key, Type: pair.Value.PoolType.Name, Awards: pair.Value.Points,
                Selected: data.LevelupTrainedInvocations.TryGetValue(pair.Key, out var choices)
                    ? (IEnumerable<InvocationDefinition>)choices : Enumerable.Empty<InvocationDefinition>()));
        var otherChoices = data.LevelupTrainedInvocations.Where(x => !current.ContainsKey(x.Key)).SelectMany(x => x.Value);
        RangerInvocationPlan.Validate(data.HeroCharacter.TrainedInvocations.Concat(otherChoices), events,
            item => item is InvocationDefinitionCustom custom ? custom.PoolType.Name : string.Empty);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.TrainInvocation))]
    internal static class TrainBoundaryPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, InvocationDefinition invocation, string tag)
        {
            if (!Current(heroBuildingData.HeroCharacter).TryGetValue(tag, out var pool)) return true;
            var chosen = heroBuildingData.LevelupTrainedInvocations.TryGetValue(tag, out var list) ? list : new List<InvocationDefinition>();
            return invocation is InvocationDefinitionCustom custom && pool.PoolType == custom.PoolType && chosen.Count < pool.Points &&
                !heroBuildingData.HeroCharacter.TrainedInvocations.Contains(invocation) &&
                !heroBuildingData.LevelupTrainedInvocations.Values.SelectMany(x => x).Contains(invocation);
        }
    }
    internal static void Clear(CharacterHeroBuildingData data, string tag)
    {
        foreach (var key in data.LevelupTrainedInvocations.Keys.Concat(data.UnlearnedInvocations.Keys)
                     .Where(x => x.StartsWith(tag + Origin, StringComparison.Ordinal)).Distinct().ToArray())
        { data.LevelupTrainedInvocations.Remove(key); data.UnlearnedInvocations.Remove(key); }
    }
}
