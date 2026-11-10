using System;
using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;

namespace SolastaUnfinishedBusiness.Models;

// Shared definition transformation only; class-specific activation/scaling stays in each context.
internal sealed class CompressedProgressionDefinitions(string prefix)
{
    internal readonly Dictionary<FeatureDefinition, string> PoolOrigins = new();
    internal readonly HashSet<FeatureDefinitionPower> Powers = new();
    internal readonly HashSet<FeatureDefinition> WrappedFeatures = new();
    internal static IEnumerable<FeatureDefinition> Flatten(FeatureDefinition feature)
    {
        yield return feature;
        if (feature is FeatureDefinitionFeatureSet set)
        {
            foreach (var child in set.FeatureSet.SelectMany(Flatten))
            {
                yield return child;
            }
        }
    }

    internal List<FeatureUnlockByLevel> Prepare(List<FeatureUnlockByLevel> target, string owner)
    {
        var plan = new FighterProgressionPlan<FeatureDefinition>(target.Select(x => (x.FeatureDefinition, x.Level)));
        var result = new List<FeatureUnlockByLevel>();
        foreach (var power in target.SelectMany(x => Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPower>())
        {
            Powers.Add(power);
        }

        foreach (var group in plan.Events.GroupBy(x => x.Level))
        {
            // Keep low-level identities intact: UB multiclass proficiency filters compare definitions by identity.
            if (group.Key <= 5)
            {
                result.AddRange(group.Select(x => new FeatureUnlockByLevel(x.Feature, x.Level)));
                continue;
            }

            var wrapper = FeatureDefinitionFeatureSetBuilder.Create($"{prefix}{owner}Level{group.Key}")
                .SetGuiPresentationNoContent(true)
                .SetMode(FeatureDefinitionFeatureSet.FeatureSetMode.Union).AddToDB();
            // Add directly: the builder sorts by title, which would destroy acquisition order.
            foreach (var item in group)
            {
                var origin = $"{owner}Original{item.OriginalLevel}Event{item.OriginalIndex}";
                var cloned = CloneChoices(item.Feature, origin);
                wrapper.FeatureSet.Add(cloned);
                foreach (var feature in Flatten(cloned))
                {
                    WrappedFeatures.Add(feature);
                }
                foreach (var power in Flatten(item.Feature).OfType<FeatureDefinitionPower>())
                {
                    Powers.Add(power);
                }
            }

            result.Add(new FeatureUnlockByLevel(wrapper, group.Key));
        }

        // Validate the actual generated definitions, not only the planning layer.
        string Signature(FeatureDefinition feature) => feature is FeatureDefinitionPointPool pool
            ? $"Pool:{pool.PoolType}:{pool.PoolAmount}"
            : feature.Name;
        var before = plan.Events.SelectMany(x => Flatten(x.Feature))
            .Where(x => x is not FeatureDefinitionFeatureSet).Select(Signature).ToArray();
        var after = result.SelectMany(x => Flatten(x.FeatureDefinition))
            .Where(x => x is not FeatureDefinitionFeatureSet).Select(Signature).ToArray();
        if (!before.SequenceEqual(after))
        {
            throw new InvalidOperationException("Compressed progression changed reward counts or acquisition order: " + owner);
        }

        Main.Info($"Compressed progression verified {owner}: {plan.Events.Count} original acquisition events.");
        return result;
    }

    private FeatureDefinition CloneChoices(FeatureDefinition source, string origin)
    {
        if (source is FeatureDefinitionPointPool pool)
        {
            var clone = FeatureDefinitionPointPoolBuilder.Create(pool, $"{prefix}{origin}{pool.Name}").AddToDB();
            CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(pool, clone);
            PoolOrigins.Add(clone, origin);
            return clone;
        }

        if (source is not FeatureDefinitionFeatureSet set || !Flatten(set).OfType<FeatureDefinitionPointPool>().Any())
        {
            return source;
        }

        var copy = FeatureDefinitionFeatureSetBuilder.Create(set, $"{prefix}{origin}{set.Name}").AddToDB();
        CustomizedSubFeatureDefinitions.CopyCustomSubFeatures(set, copy);
        copy.FeatureSet.Clear();
        for (var i = 0; i < set.FeatureSet.Count; i++)
        {
            copy.FeatureSet.Add(CloneChoices(set.FeatureSet[i], $"{origin}Child{i}"));
        }

        return copy;
    }

}
