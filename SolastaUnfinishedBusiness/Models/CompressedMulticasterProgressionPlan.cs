using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SolastaUnfinishedBusiness.Models;

// Stage13.2: pure calculation only. No native definitions, Settings, hero objects or resource mutation.
// A valid result describes arithmetic; it does not authorize mixed-caster gameplay.
internal sealed class CompressedMulticasterProgressionPlan
{
    internal const bool MixedGameplayReady = false;
    internal const int MaximumResourceIndex = 20;
    internal enum CasterKind { None, Full, Half, HalfRoundUp, OneThird, Pact }
    internal enum RoundingMode { InstalledSolo, UbGroupedMulticlass }
    internal enum AuxiliaryRole { Unsupported, Race, Feat, Item, AttachedGrant, FightingStyle, Invocation, Background, SubclassGrant }
    internal enum DiagnosticCode
    {
        InvalidCharacterLevel, InvalidClassLevel, TooManyClasses, DuplicateClass, HistoryMismatch,
        UnsupportedClass, UnknownSubclass, MissingSubclass, PrematureSubclass, UnsupportedCompression,
        UnsupportedCompressedLevel, UnsupportedCompressedSubclass, UnsupportedSource, AmbiguousOwner,
        ConflictingSource, ResourceTableSaturated
    }
    internal sealed class Diagnostic(DiagnosticCode code, string owner, bool error = true)
    {
        internal DiagnosticCode Code { get; } = code;
        internal string Owner { get; } = owner;
        internal bool IsError { get; } = error;
    }

    // Original, unprojected class spell-access rows captured after UB rule switches. The calculator
    // deliberately does not derive class access from the shared resource row or from tabletop rules.
    internal sealed class CastingProfile
    {
        internal string DefinitionId { get; }
        internal CasterKind Kind { get; }
        internal int UnlockOriginalLevel { get; }
        internal bool SupportsCompression { get; }
        internal IReadOnlyList<int> MaximumSpellCircles { get; }
        internal CastingProfile(string definitionId, CasterKind kind, int unlockOriginalLevel,
            bool supportsCompression, IEnumerable<int> maximumSpellCircles)
        {
            RequireId(definitionId, nameof(definitionId));
            if (!Enum.IsDefined(typeof(CasterKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (unlockOriginalLevel is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(unlockOriginalLevel));
            var rows = maximumSpellCircles?.ToArray() ?? throw new ArgumentNullException(nameof(maximumSpellCircles));
            var maximum = kind switch { CasterKind.None => 0, CasterKind.Full => 9, CasterKind.OneThird => 4, _ => 5 };
            if (rows.Length != 20 || rows.Any(n => n < 0 || n > maximum))
                throw new ArgumentException("Expected twenty original class-access rows within the caster's spell-circle limit.", nameof(maximumSpellCircles));
            DefinitionId = definitionId; Kind = kind; UnlockOriginalLevel = unlockOriginalLevel;
            SupportsCompression = supportsCompression; MaximumSpellCircles = Array.AsReadOnly(rows);
        }
    }
    internal sealed class SubclassProfile
    {
        internal string Id { get; }
        internal bool SupportsFeatureCompression { get; }
        internal bool AvailableWithCompressedClass { get; }
        internal CastingProfile Casting { get; }
        internal SubclassProfile(string id, bool supportsFeatureCompression,
            bool availableWithCompressedClass = true, CastingProfile casting = null)
        {
            RequireId(id, nameof(id)); Id = id; SupportsFeatureCompression = supportsFeatureCompression;
            AvailableWithCompressedClass = availableWithCompressedClass; Casting = casting;
        }
    }
    internal sealed class ClassProfile
    {
        internal string Id { get; }
        internal bool SupportsFeatureCompression { get; }
        internal CastingProfile Casting { get; }
        internal int SubclassChoiceOriginalLevel { get; }
        internal IReadOnlyDictionary<string, SubclassProfile> Subclasses { get; }
        internal ClassProfile(string id, bool supportsFeatureCompression, CastingProfile casting = null,
            int subclassChoiceOriginalLevel = 0, IEnumerable<SubclassProfile> subclasses = null)
        {
            RequireId(id, nameof(id));
            var entries = (subclasses ?? Enumerable.Empty<SubclassProfile>()).ToArray();
            if (entries.Any(x => x == null) || entries.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length ||
                subclassChoiceOriginalLevel is < 0 or > 20 || (entries.Length > 0 && subclassChoiceOriginalLevel == 0))
                throw new ArgumentException("Invalid subclass registry or choice level.", nameof(subclasses));
            Id = id; SupportsFeatureCompression = supportsFeatureCompression; Casting = casting;
            SubclassChoiceOriginalLevel = subclassChoiceOriginalLevel;
            Subclasses = new ReadOnlyDictionary<string, SubclassProfile>(entries.ToDictionary(x => x.Id, StringComparer.Ordinal));
        }
    }
    internal sealed class ClassInput(string classId, int actualLevel, bool compressionEnabled = false, string subclassId = null)
    {
        internal string ClassId { get; } = classId;
        internal int ActualLevel { get; } = actualLevel;
        internal bool CompressionEnabled { get; } = compressionEnabled;
        internal string SubclassId { get; } = subclassId;
    }
    // A registered auxiliary source is already classified by its adapter. Its own access ceiling is
    // retained, but neither an extra repertoire view nor a bonus-spell grant adds a casting class.
    internal sealed class AuxiliaryInput(string sourceId, AuxiliaryRole role, int maximumSpellCircle = 0, string ownerClassId = null)
    {
        internal string SourceId { get; } = sourceId;
        internal AuxiliaryRole Role { get; } = role;
        internal int MaximumSpellCircle { get; } = maximumSpellCircle;
        internal string OwnerClassId { get; } = ownerClassId;
    }
    internal sealed class Snapshot
    {
        internal int ActualCharacterLevel { get; }
        internal IReadOnlyList<ClassInput> Classes { get; }
        internal IReadOnlyList<string> ActualClassHistory { get; }
        internal IReadOnlyList<AuxiliaryInput> AuxiliarySources { get; }
        internal Snapshot(int actualCharacterLevel, IEnumerable<ClassInput> classes, IEnumerable<string> actualClassHistory,
            IEnumerable<AuxiliaryInput> auxiliarySources = null)
        {
            ActualCharacterLevel = actualCharacterLevel;
            Classes = Array.AsReadOnly(classes?.ToArray() ?? throw new ArgumentNullException(nameof(classes)));
            ActualClassHistory = Array.AsReadOnly(actualClassHistory?.ToArray() ?? throw new ArgumentNullException(nameof(actualClassHistory)));
            AuxiliarySources = Array.AsReadOnly((auxiliarySources ?? Enumerable.Empty<AuxiliaryInput>()).ToArray());
        }
    }
    internal sealed class ClassResult
    {
        internal string ClassId { get; }
        internal string SubclassId { get; }
        internal int ActualClassLevel { get; }
        // Native subclass progression uses its owning class level, not levels since subclass selection.
        internal int ActualSubclassLevel { get; }
        internal int EffectiveClassLevel { get; }
        internal int EffectiveSubclassLevel { get; }
        internal string CastingDefinitionId { get; }
        internal CasterKind Kind { get; }
        internal bool CastingUnlocked { get; }
        internal int EffectiveCastingLevel { get; }
        internal int SpellAccessLevel { get; }
        internal int MaximumSpellCircle { get; }
        internal int ContributionNumerator { get; }
        internal int ContributionDenominator { get; }
        internal bool IsCastingParticipant => CastingUnlocked && Kind != CasterKind.None;
        internal ClassResult(ClassInput input, int featureLevel, SubclassProfile subclass, CastingProfile casting)
        {
            ClassId = input.ClassId; SubclassId = subclass?.Id; ActualClassLevel = input.ActualLevel;
            ActualSubclassLevel = subclass == null ? 0 : input.ActualLevel; EffectiveClassLevel = featureLevel;
            EffectiveSubclassLevel = subclass == null ? 0 : Effective(input.ActualLevel, input.CompressionEnabled && subclass.SupportsFeatureCompression);
            CastingDefinitionId = casting?.DefinitionId; Kind = casting?.Kind ?? CasterKind.None;
            EffectiveCastingLevel = Kind == CasterKind.None ? 0 : Effective(input.ActualLevel, input.CompressionEnabled && casting.SupportsCompression);
            CastingUnlocked = Kind != CasterKind.None && input.ActualLevel > 0 && EffectiveCastingLevel >= casting.UnlockOriginalLevel;
            SpellAccessLevel = CastingUnlocked ? EffectiveCastingLevel : 0;
            MaximumSpellCircle = CastingUnlocked ? casting.MaximumSpellCircles[SpellAccessLevel - 1] : 0;
            ContributionNumerator = CastingUnlocked && Kind != CasterKind.Pact ? EffectiveCastingLevel : 0;
            ContributionDenominator = Kind switch { CasterKind.Half or CasterKind.HalfRoundUp => 2, CasterKind.OneThird => 3, _ => 1 };
        }
    }
    internal sealed class Bucket(CasterKind kind, int levelSum, int roundedContribution)
    {
        internal CasterKind Kind { get; } = kind;
        internal int LevelSum { get; } = levelSum;
        internal int RoundedContribution { get; } = roundedContribution;
    }
    internal sealed class Result
    {
        internal string RegistryVersion { get; }
        internal Snapshot Input { get; }
        internal bool IsValid => Diagnostics.All(x => !x.IsError);
        internal IReadOnlyList<Diagnostic> Diagnostics { get; }
        internal IReadOnlyList<ClassResult> Classes { get; }
        internal IReadOnlyList<ClassResult> PactProgressions { get; }
        internal IReadOnlyList<AuxiliaryInput> AuxiliarySources { get; }
        internal IReadOnlyList<Bucket> Buckets { get; }
        internal RoundingMode Mode { get; }
        internal int CastingParticipantCount { get; }
        internal int RawSharedCasterLevel { get; }
        internal int ResourceTableIndex { get; }
        // Native slot arrays have rows1..20 at indices0..19. Zero means NO ROW, never index -1.
        internal int? StandardSlotRowIndex => ResourceTableIndex == 0 ? null : ResourceTableIndex - 1;
        internal bool Saturated => RawSharedCasterLevel > MaximumResourceIndex;
        internal Result(string version, Snapshot input, IEnumerable<Diagnostic> diagnostics,
            IEnumerable<ClassResult> classes = null, IEnumerable<Bucket> buckets = null,
            IEnumerable<AuxiliaryInput> auxiliary = null, RoundingMode mode = RoundingMode.InstalledSolo, int raw = 0)
        {
            RegistryVersion = version; Input = input; Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            Classes = Array.AsReadOnly((classes ?? Enumerable.Empty<ClassResult>()).ToArray());
            PactProgressions = Array.AsReadOnly(Classes.Where(x => x.Kind == CasterKind.Pact && x.CastingUnlocked).ToArray());
            AuxiliarySources = Array.AsReadOnly((auxiliary ?? Enumerable.Empty<AuxiliaryInput>()).ToArray());
            Buckets = Array.AsReadOnly((buckets ?? Enumerable.Empty<Bucket>()).ToArray()); Mode = mode;
            CastingParticipantCount = Classes.Count(x => x.IsCastingParticipant); RawSharedCasterLevel = raw;
            ResourceTableIndex = Math.Max(0, Math.Min(MaximumResourceIndex, raw));
        }
    }

    private readonly IReadOnlyDictionary<string, ClassProfile> registry;
    internal string RegistryVersion { get; }
    internal CompressedMulticasterProgressionPlan(string registryVersion, IEnumerable<ClassProfile> profiles)
    {
        RequireId(registryVersion, nameof(registryVersion));
        var entries = profiles?.ToArray() ?? throw new ArgumentNullException(nameof(profiles));
        if (entries.Any(x => x == null) || entries.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new ArgumentException("Duplicate or missing class profile.", nameof(profiles));
        RegistryVersion = registryVersion;
        registry = new ReadOnlyDictionary<string, ClassProfile>(entries.ToDictionary(x => x.Id, StringComparer.Ordinal));
    }
    private static void RequireId(string value, string argument)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A stable source ID is required.", argument);
    }
    private static int Effective(int actual, bool compressed) => compressed ? FighterProgressionPlan<int>.EffectiveLevel(actual) : actual;
    private static int RoundUp(int numerator, int denominator) => (numerator + denominator - 1) / denominator;

    internal Result Calculate(Snapshot input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var errors = new List<Diagnostic>();
        void Error(DiagnosticCode code, string owner) => errors.Add(new Diagnostic(code, owner));
        if (input.ActualCharacterLevel is < 0 or > 20) Error(DiagnosticCode.InvalidCharacterLevel, "Hero");
        if (input.Classes.Any(x => x == null || string.IsNullOrWhiteSpace(x.ClassId) || x.ActualLevel is < 0 or > 20))
            Error(DiagnosticCode.InvalidClassLevel, "Hero");
        if (errors.Count > 0) return new Result(RegistryVersion, input, errors);
        if (input.Classes.Select(x => x.ClassId).Distinct(StringComparer.Ordinal).Count() != input.Classes.Count)
            Error(DiagnosticCode.DuplicateClass, "Hero");
        if (input.Classes.Count(x => x.ActualLevel > 0) > 3) Error(DiagnosticCode.TooManyClasses, "Hero");
        if (input.Classes.Sum(x => (long)x.ActualLevel) != input.ActualCharacterLevel ||
            input.ActualClassHistory.Count != input.ActualCharacterLevel ||
            input.ActualClassHistory.Any(id => !input.Classes.Any(x => x.ClassId == id)) ||
            input.Classes.Any(x => input.ActualClassHistory.Count(id => id == x.ClassId) != x.ActualLevel))
            Error(DiagnosticCode.HistoryMismatch, "Hero");
        var classes = new List<ClassResult>();
        foreach (var item in input.Classes.OrderBy(x => x.ClassId, StringComparer.Ordinal))
        {
            if (!registry.TryGetValue(item.ClassId, out var profile)) { Error(DiagnosticCode.UnsupportedClass, item.ClassId); continue; }
            SubclassProfile subclass = null;
            if (item.SubclassId != null && !profile.Subclasses.TryGetValue(item.SubclassId, out subclass))
            { Error(DiagnosticCode.UnknownSubclass, item.ClassId); continue; }
            if (item.CompressionEnabled && !profile.SupportsFeatureCompression)
            { Error(DiagnosticCode.UnsupportedCompression, item.ClassId); continue; }
            if (item.CompressionEnabled && item.ActualLevel > 12)
            { Error(DiagnosticCode.UnsupportedCompressedLevel, item.ClassId); continue; }
            if (item.CompressionEnabled && subclass is { AvailableWithCompressedClass: false })
            { Error(DiagnosticCode.UnsupportedCompressedSubclass, item.ClassId); continue; }
            var featureLevel = Effective(item.ActualLevel, item.CompressionEnabled && profile.SupportsFeatureCompression);
            if (profile.SubclassChoiceOriginalLevel > 0 && featureLevel >= profile.SubclassChoiceOriginalLevel && subclass == null)
            { Error(DiagnosticCode.MissingSubclass, item.ClassId); continue; }
            if (subclass != null && (item.ActualLevel == 0 || featureLevel < profile.SubclassChoiceOriginalLevel))
            { Error(DiagnosticCode.PrematureSubclass, item.ClassId); continue; }
            // Same precedence as installed GetCasterTypeForClassOrSubclass: class definition first.
            classes.Add(new ClassResult(item, featureLevel, subclass, profile.Casting ?? subclass?.Casting));
        }
        var auxiliary = new List<AuxiliaryInput>();
        foreach (var item in input.AuxiliarySources)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SourceId) || item.Role == AuxiliaryRole.Unsupported ||
                !Enum.IsDefined(typeof(AuxiliaryRole), item.Role) || item.MaximumSpellCircle is < 0 or > 9)
            { Error(DiagnosticCode.UnsupportedSource, item?.SourceId ?? "Unknown"); continue; }
            if ((item.Role == AuxiliaryRole.AttachedGrant && item.OwnerClassId == null) ||
                (item.OwnerClassId != null && !input.Classes.Any(x => x.ClassId == item.OwnerClassId && x.ActualLevel > 0)))
            { Error(DiagnosticCode.AmbiguousOwner, item.SourceId); continue; }
            var previous = auxiliary.FirstOrDefault(x => x.SourceId == item.SourceId);
            if (previous == null) auxiliary.Add(item);
            else if (previous.Role != item.Role || previous.OwnerClassId != item.OwnerClassId || previous.MaximumSpellCircle != item.MaximumSpellCircle)
                Error(DiagnosticCode.ConflictingSource, item.SourceId);
        }
        // No usable arithmetic is returned for incomplete/ambiguous snapshots. Never mask invalid
        // actual levels or an unsupported subclass by clamping them into the resource table.
        if (errors.Count > 0) return new Result(RegistryVersion, input, errors);
        var mode = classes.Count(x => x.IsCastingParticipant) > 1 ? RoundingMode.UbGroupedMulticlass : RoundingMode.InstalledSolo;
        var buckets = new List<Bucket>();
        foreach (var kind in new[] { CasterKind.Full, CasterKind.Half, CasterKind.HalfRoundUp, CasterKind.OneThird })
        {
            var sum = classes.Where(x => x.Kind == kind).Sum(x => x.ContributionNumerator);
            var rounded = kind switch
            {
                CasterKind.Full => sum,
                CasterKind.Half when mode == RoundingMode.UbGroupedMulticlass => sum / 2,
                CasterKind.Half or CasterKind.HalfRoundUp => RoundUp(sum, 2),
                CasterKind.OneThird when mode == RoundingMode.UbGroupedMulticlass => sum / 3,
                _ => RoundUp(sum, 3)
            };
            buckets.Add(new Bucket(kind, sum, rounded));
        }
        var raw = buckets.Sum(x => x.RoundedContribution);
        if (raw > MaximumResourceIndex) errors.Add(new Diagnostic(DiagnosticCode.ResourceTableSaturated, "Standard", false));
        return new Result(RegistryVersion, input, errors, classes, buckets,
            auxiliary.OrderBy(x => x.SourceId, StringComparer.Ordinal), mode, raw);
    }
}
