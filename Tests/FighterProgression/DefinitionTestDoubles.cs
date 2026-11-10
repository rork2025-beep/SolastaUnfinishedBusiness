// Minimal managed model for testing the REAL shared transformer without invoking Unity.
// These types model list/copy behavior only; they do not pretend to implement the game engine.
using System.Collections.Generic;

internal class FeatureDefinition
{
    internal string Name { get; set; }
    internal List<object> SubFeatures { get; } = new();
}
internal sealed class FeatureDefinitionPointPool : FeatureDefinition
{
    internal string PoolType { get; set; }
    internal int PoolAmount { get; set; }
}
internal sealed class FeatureDefinitionPower : FeatureDefinition
{
    internal FeatureDefinitionPower OverriddenPower { get; set; }
}
internal sealed class FeatureDefinitionFeatureSet : FeatureDefinition
{
    internal enum FeatureSetMode { Union, Exclusion }
    internal FeatureSetMode Mode { get; set; }
    internal List<FeatureDefinition> FeatureSet { get; } = new();
}
internal sealed class FeatureUnlockByLevel(FeatureDefinition feature, int level)
{
    internal FeatureDefinition FeatureDefinition { get; } = feature;
    internal int Level { get; } = level;
}
namespace SolastaUnfinishedBusiness
{
    internal static class Main
    {
        internal static void Info(string message) { }
    }
}
namespace SolastaUnfinishedBusiness.Builders
{
    // Keeps the same extension-method namespace available to the linked production source.
    internal static class TestBuilderMarker { }
}
namespace SolastaUnfinishedBusiness.Builders.Features
{
    internal sealed class FeatureDefinitionPointPoolBuilder
    {
        private FeatureDefinitionPointPool definition;
        internal static FeatureDefinitionPointPoolBuilder Create(FeatureDefinitionPointPool source, string name) =>
            new() { definition = new FeatureDefinitionPointPool { Name = name, PoolType = source.PoolType, PoolAmount = source.PoolAmount } };
        internal FeatureDefinitionPointPool AddToDB() => definition;
    }
    internal sealed class FeatureDefinitionFeatureSetBuilder
    {
        private FeatureDefinitionFeatureSet definition;
        internal static FeatureDefinitionFeatureSetBuilder Create(string name) =>
            new() { definition = new FeatureDefinitionFeatureSet { Name = name } };
        internal static FeatureDefinitionFeatureSetBuilder Create(FeatureDefinitionFeatureSet source, string name)
        {
            var builder = Create(name);
            builder.definition.Mode = source.Mode;
            builder.definition.FeatureSet.AddRange(source.FeatureSet);
            return builder;
        }
        internal FeatureDefinitionFeatureSetBuilder SetGuiPresentationNoContent(bool hidden) => this;
        internal FeatureDefinitionFeatureSetBuilder SetMode(FeatureDefinitionFeatureSet.FeatureSetMode mode)
        {
            definition.Mode = mode;
            return this;
        }
        internal FeatureDefinitionFeatureSet AddToDB() => definition;
    }
}

namespace SolastaUnfinishedBusiness.Api.GameExtensions
{
    internal static class CustomizedSubFeatureDefinitions
    {
        internal static void CopyCustomSubFeatures(FeatureDefinition source, FeatureDefinition target) =>
            target.SubFeatures.AddRange(source.SubFeatures);
    }
}
