using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;

namespace SolastaUnfinishedBusiness.Models;

// Registry adapter, not another rounding calculator. One parent class supplies one contribution.
// Feature compression is independent: these installed casting tables always use actual levels.
internal sealed class ThirdCasterProgressionPlan
{
    internal sealed class Source
    {
        internal string ClassId { get; }
        internal string SubclassId { get; }
        internal int ChoiceOriginalLevel { get; }
        internal bool AvailableWithCompressedClass { get; }
        internal R.Profile Repertoire { get; }
        internal Source(string classId, string subclassId, int choiceOriginalLevel,
            bool availableWithCompressedClass, R.Profile repertoire)
        {
            if (string.IsNullOrWhiteSpace(classId) || string.IsNullOrWhiteSpace(subclassId) || repertoire == null ||
                repertoire.ClassId != classId || repertoire.SupportsCastingCompression || repertoire.Mode != R.Knowledge.Selection ||
                repertoire.PreparedRule != R.Preparation.None || choiceOriginalLevel is < 1 or > 20 ||
                repertoire.UnlockOriginalLevel < choiceOriginalLevel || repertoire.MaximumCircles.Any(n => n > 4) ||
                Enumerable.Range(0, repertoire.UnlockOriginalLevel - 1).Any(i => repertoire.MaximumCircles[i] != 0 ||
                    repertoire.Known[i] != 0 || repertoire.Cantrips[i] != 0 || repertoire.Replacements[i] != 0))
                throw new ArgumentException("Invalid registered third-caster source or pre-unlock reward.");
            ClassId = classId; SubclassId = subclassId; ChoiceOriginalLevel = choiceOriginalLevel;
            AvailableWithCompressedClass = availableWithCompressedClass; Repertoire = repertoire;
        }
        internal C.SubclassProfile CalculatorProfile(bool featureCompression) => new(SubclassId, featureCompression,
            AvailableWithCompressedClass, new C.CastingProfile(Repertoire.FeatureId, C.CasterKind.OneThird,
                Repertoire.UnlockOriginalLevel, false, Repertoire.MaximumCircles));
        internal R.Profile ForAdvancement(int actual, bool compressed)
        {
            if (actual < Repertoire.UnlockOriginalLevel || actual > (compressed ? 12 : 20) || compressed && !AvailableWithCompressedClass)
                throw new InvalidOperationException("Third-caster advancement is premature, capped or unsupported with parent compression.");
            return Repertoire;
        }
        // Native subclass repertoires may have NO SpellCastingClass. Never infer ownership from
        // the spell list, casting ability, hero name or another casting repertoire.
        internal bool Matches(string nativeClass, string nativeSubclass, string feature) =>
            (nativeClass == null || nativeClass == ClassId) && nativeSubclass == SubclassId && feature == Repertoire.FeatureId;
    }
    internal IReadOnlyDictionary<string, Source> Sources { get; }
    internal ThirdCasterProgressionPlan(IEnumerable<Source> sources)
    {
        var rows = sources?.ToArray() ?? throw new ArgumentNullException(nameof(sources));
        if (rows.Any(s => s == null) || rows.Select(s => s.SubclassId).Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Select(s => s.Repertoire.FeatureId).Distinct(StringComparer.Ordinal).Count() != rows.Length)
            throw new ArgumentException("Ambiguous registered third-caster ownership.");
        Sources = new ReadOnlyDictionary<string, Source>(rows.ToDictionary(s => s.SubclassId, StringComparer.Ordinal));
    }
    internal C.ClassProfile AdaptParent(C.ClassProfile parent)
    {
        var owned = Sources.Values.Where(s => s.ClassId == parent.Id).ToArray();
        if (parent.Casting != null || parent.Subclasses.Keys.Any(id => Sources.TryGetValue(id, out var s) && s.ClassId != parent.Id) || owned.Any(s => s.ChoiceOriginalLevel != parent.SubclassChoiceOriginalLevel || !parent.Subclasses.ContainsKey(s.SubclassId)))
            throw new InvalidOperationException("Third casting requires its registered noncasting parent/subclass choice.");
        return new C.ClassProfile(parent.Id, parent.SupportsFeatureCompression, null, parent.SubclassChoiceOriginalLevel,
            parent.Subclasses.Values.Select(s => Sources.TryGetValue(s.Id, out var source)
                ? source.CalculatorProfile(s.SupportsFeatureCompression) : s));
    }
}
