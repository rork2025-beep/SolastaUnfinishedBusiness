using System;
using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Builders.Features;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using Third = SolastaUnfinishedBusiness.Models.ThirdCasterProgressionPlan;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class MultipleSpellRepertoiresContext
{
    private static Dictionary<string, Definition> ThirdDefinitions = new(StringComparer.Ordinal);
    private static Third ThirdCasters;
    private static IReadOnlyDictionary<string, C.ClassProfile> thirdCasterParents;
    // For the future admission/finalization coordinator. These profiles do not open the UI gate.
    internal static IReadOnlyDictionary<string, C.ClassProfile> ThirdCasterClassProfiles => thirdCasterParents;
    private static void CaptureThirdCasterDefinitions()
    {
        var captured = new Dictionary<string, Definition>(StringComparer.Ordinal);
        var sources = new List<Third.Source>();
        var parents = new List<C.ClassProfile>();
        var registered = SharedSpellsContext.SubclassCasterType.Where(p => p.Value == FeatureDefinitionCastSpellBuilder.CasterProgression.OneThird)
            .Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var owner in DatabaseRepository.GetDatabase<CharacterClassDefinition>())
        {
            var classSources = Sources(owner.FeatureUnlocks, owner.Name);
            var choices = classSources.Where(s => s.Feature is FeatureDefinitionSubclassChoice).ToArray();
            var subclasses = choices.SelectMany(s => ((FeatureDefinitionSubclassChoice)s.Feature).Subclasses).ToHashSet(StringComparer.Ordinal);
            if (SubclassesContext.KlassListContextTab.TryGetValue(owner, out var context)) subclasses.UnionWith(context.AllSubClasses.Select(s => s.Name));
            var selected = subclasses.Where(registered.Contains).ToArray();
            if (selected.Length == 0) continue;
            // Policies mirror the existing progression modules. Unknown parents are diagnosed,
            // not silently assigned a compressed casting or feature progression.
            if (owner.Name is not ("Fighter" or "Rogue") || choices.Length != 1 || classSources.Any(s => s.Feature is FeatureDefinitionCastSpell))
                throw new InvalidOperationException("Third-caster parent needs an explicit registered adapter: " + owner.Name);
            bool FeatureCompression(string id) => owner.Name == "Rogue" || id is "MartialChampion" or "MartialWeaponMaster";
            var choiceLevel = choices[0].Original;
            parents.Add(new C.ClassProfile(owner.Name, true, null, choiceLevel,
                subclasses.Select(id => new C.SubclassProfile(id, FeatureCompression(id), FeatureCompression(id)))));
            foreach (var id in selected)
            {
                var subclass = DatabaseHelper.GetDefinition<CharacterSubclassDefinition>(id);
                var subSources = Sources(subclass.FeatureUnlocks, id);
                var casts = subSources.Where(s => s.Feature is FeatureDefinitionCastSpell).ToArray();
                if (casts.Length != 1 || casts[0].RequiredOptions.Length != 0 ||
                    ((FeatureDefinitionCastSpell)casts[0].Feature).SpellCastingOrigin != FeatureDefinitionCastSpell.CastingOrigin.Subclass)
                    throw new InvalidOperationException("Ambiguous or conditional third-caster feature: " + id);
                var cast = (FeatureDefinitionCastSpell)casts[0].Feature;
                var profile = CaptureProfile(owner, cast, false, casts[0].Original);
                var ownClassSources = Sources(owner.FeatureUnlocks, owner.Name);
                var definition = new Definition(owner, cast, profile, ownClassSources,
                    new Dictionary<string, Source[]>(StringComparer.Ordinal) { [id] = subSources });
                foreach (var feature in ownClassSources.Concat(subSources)) feature.Grants = CaptureGrants(definition, feature);
                captured.Add(id, definition);
                sources.Add(new Third.Source(owner.Name, id, choiceLevel, owner.Name == "Rogue", profile));
            }
        }
        if (!registered.SetEquals(captured.Keys))
            throw new InvalidOperationException("Unowned registered third-caster subclass: " + string.Join(",", registered.Except(captured.Keys)));
        var registry = new Third(sources);
        var adapted = parents.ToDictionary(p => p.Id, registry.AdaptParent, StringComparer.Ordinal);
        ThirdDefinitions = captured; ThirdCasters = registry;
        thirdCasterParents = new System.Collections.ObjectModel.ReadOnlyDictionary<string, C.ClassProfile>(adapted);
        foreach (var source in sources) Main.Info("Third-caster source: " + source.ClassId + "/" + source.SubclassId + "/" + source.Repertoire.FeatureId +
            " choice=" + source.ChoiceOriginalLevel + " casting=" + source.Repertoire.UnlockOriginalLevel + " compressed-parent=" + source.AvailableWithCompressedClass + " casting-compression=false");
    }
    private static Definition DefinitionFor(string classId, string subclass)
    {
        if (definitions != null && definitions.TryGetValue(classId, out var ordinary)) return ordinary;
        if (subclass != null && ThirdDefinitions.TryGetValue(subclass, out var third) && third.Owner.Name == classId) return third;
        throw new InvalidOperationException("No registered casting source for parent/subclass: " + classId + "/" + subclass);
    }
    private static bool MatchesDefinition(RulesetSpellRepertoire repertoire, Definition definition, string subclass)
    {
        if (repertoire.SpellCastingFeature != definition.Casting) return false;
        if (definition.Casting.SpellCastingOrigin == FeatureDefinitionCastSpell.CastingOrigin.Class)
            return repertoire.SpellCastingClass == definition.Owner;
        return subclass != null && ThirdCasters.Sources.TryGetValue(subclass, out var source) &&
            source.Matches(repertoire.SpellCastingClass?.Name, repertoire.SpellCastingSubclass?.Name, repertoire.SpellCastingFeature.Name);
    }
    internal static string RepertoireClassId(RulesetSpellRepertoire repertoire)
    {
        if (!RuntimeEnabled || definitions == null) throw new InvalidOperationException("Repertoire source registry/gate unavailable.");
        var subclass = repertoire.SpellCastingSubclass?.Name;
        var owner = repertoire.SpellCastingClass?.Name;
        if (repertoire.SpellCastingFeature.SpellCastingOrigin == FeatureDefinitionCastSpell.CastingOrigin.Subclass)
        {
            if (subclass != null && ThirdCasters.Sources.TryGetValue(subclass, out var source) && source.Matches(owner, subclass, repertoire.SpellCastingFeature.Name)) return source.ClassId;
        }
        else if (owner != null && definitions.TryGetValue(owner, out var ordinary) && MatchesDefinition(repertoire, ordinary, null)) return owner;
        throw new InvalidOperationException("Unclassified or mismatched casting feature/subclass owner.");
    }
}
