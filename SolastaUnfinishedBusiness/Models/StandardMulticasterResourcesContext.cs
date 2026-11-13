using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using static FeatureDefinitionCastSpell;
using static RuleDefinitions;
using ResourcePlan = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;

namespace SolastaUnfinishedBusiness.Models;

// Stage13.3 resource adapter, deliberately unreachable during ordinary play. Admission, validated
// native snapshots and save journals belong to later stages; no loaded hero is auto-imported here.
internal static class StandardMulticasterResourcesContext
{
    internal static bool RuntimeEnabled => CompressedMulticasterProgressionPlan.MixedGameplayReady;
    private static ResourcePlan.Tables tables;
    private static readonly ResourcePlan.OwnerStore<RulesetCharacter> Owners = new(ResolveOwner);
    private static readonly ConditionalWeakTable<RulesetCharacterHero, Binding> Bindings = new();
    private sealed class Binding(CompressedMulticasterProgressionPlan.Result progression, ResourcePlan.Budget budget)
    {
        internal CompressedMulticasterProgressionPlan.Result Progression = progression;
        internal readonly ResourcePlan.Budget Budget = budget;
        internal bool Projecting;
        internal readonly string SettingsSignature = ResourceSettingsSignature();
    }
    [ThreadStatic] private static string spellOperation;
    private sealed class SpellScope(string previous, IDisposable pactScope) : IDisposable
    {
        public void Dispose() { pactScope?.Dispose(); spellOperation = previous; }
    }
    internal static IDisposable EnterSpell(RulesetEffectSpell effect)
    {
        if (!RuntimeEnabled) return null;
        var previous = spellOperation;
        spellOperation = effect.Guid == 0 ? null : "spell:" + effect.Guid;
        return new SpellScope(previous, PactMagicMulticasterContext.EnterSpell(effect));
    }
    // Explicit audit tool only: samples legacy calculations without changing their production path.
    internal static string CompareLegacyResources(RulesetCharacterHero hero, CompressedMulticasterProgressionPlan.Result proposed)
    {
        if (RuntimeEnabled) throw new InvalidOperationException("Shadow comparison requires the legacy path.");
        ValidateHero(hero, proposed);
        var projected = Allocate(proposed);
        var powers = hero.UsablePowers.Where(x => x.PowerDefinition == SpellPointsContext.PowerSpellPoints).ToArray();
        return "legacy-shared=" + SharedSpellsContext.GetSharedCasterLevel(hero) +
            "; legacy-SP-level=" + SpellPointsContext.LegacyCasterLevelForAudit(hero) +
            "; proposed-shared=" + projected.CasterLevel + "; proposed-SP-max=" + projected.PointMaximum +
            "; native-pools=" + powers.Length + "; remaining=" + (powers.Length == 1 ? powers[0].RemainingUses.ToString() : "ambiguous/absent");
    }
    internal static void CaptureInstalledTables()
    {
        if (tables != null) return; // repeated boot never reprojects definitions or refills owners
        tables = new ResourcePlan.Tables(SharedSpellsContext.FullCastingSlots.Select(x => x.Slots.Take(9)),
            SpellPointsContext.CopyInstalledPointTable(), SpellPointsContext.SpellCostByLevel);
    }
    // Future admission/finalization code must bind at creation (level0), then supply validated
    // calculator results on advancement. Deliberately no fallback that refills an existing save.
    internal static void BindNewHero(RulesetCharacterHero hero, CompressedMulticasterProgressionPlan.Result initial)
    {
        if (!RuntimeEnabled || hero == null || hero.ClassesAndLevels.Values.Sum() != 0 ||
            initial == null || initial.Input.ActualCharacterLevel != 0)
            throw new InvalidOperationException("Standard resources: closed gate or non-new hero; automatic conversion is unsupported.");
        MultipleSpellRepertoiresContext.EnsureCanBind(hero);
        PactMagicMulticasterContext.EnsureCanBind(hero);
        var allocation = Allocate(initial);
        var budget = new ResourcePlan.Budget(tables, allocation);
        Owners.Bind(hero, budget); Bindings.Add(hero, new Binding(initial, budget));
        MultipleSpellRepertoiresContext.BindNewHero(hero);
        PactMagicMulticasterContext.BindNewHero(hero, budget, initial);
    }
    internal static void UpdateProgression(RulesetCharacterHero hero, CompressedMulticasterProgressionPlan.Result progression)
    {
        if (!RuntimeEnabled || !Bindings.TryGetValue(hero, out var binding)) throw new InvalidOperationException("Unbound resource owner.");
        ValidateHero(hero, progression);
        var allocation = Allocate(progression);
        if (allocation.Mode != binding.Budget.Current.Mode || allocation.RegistryVersion != binding.Budget.Current.RegistryVersion)
            throw new InvalidOperationException("Resource mode/registry changed during repertoire synchronization.");
        MultipleSpellRepertoiresContext.Synchronize(hero, progression);
        binding.Budget.Refresh(allocation); binding.Progression = progression;
        PactMagicMulticasterContext.Synchronize(hero, progression);
        Refresh(hero);
    }
    private static ResourcePlan.Allocation Allocate(CompressedMulticasterProgressionPlan.Result result,
        IEnumerable<ResourcePlan.Modifier> modifiers = null) => ResourcePlan.Allocation.StandardPortion(tables ?? throw new InvalidOperationException("Missing installed resource tables."),
        result, Main.Settings.UseAlternateSpellPointsSystem ? ResourcePlan.ResourceMode.SpellPoints : ResourcePlan.ResourceMode.Slots, modifiers);
    private static RulesetCharacter ResolveOwner(RulesetCharacter actor) => actor is RulesetCharacterHero
        ? actor : DruidWildshapeContext.StandardResourceOwner(actor);
    private static void ValidateHero(RulesetCharacterHero hero, CompressedMulticasterProgressionPlan.Result result)
    {
        if (!result.IsValid || result.PactProgressions.Count > 1 ||
            hero.ClassesAndLevels.Count != result.Input.Classes.Count ||
            hero.ClassesAndLevels.Any(x => !result.Input.Classes.Any(c => c.ClassId == x.Key.Name && c.ActualLevel == x.Value)) ||
            !hero.ClassesHistory.Select(x => x.Name).SequenceEqual(result.Input.ActualClassHistory) ||
            result.Input.Classes.Any(c => (hero.ClassesAndSubclasses.FirstOrDefault(x => x.Key.Name == c.ClassId).Value?.Name) != c.SubclassId))
            throw new InvalidOperationException("Standard resources: stale/invalid class snapshot or unsupported Pact participant.");
    }
    private static string ResourceSettingsSignature() => string.Join("|", typeof(Settings).GetProperties()
        .Where(p => p.PropertyType == typeof(bool) && (p.Name.Contains("CompressedProgression") || p.Name.EndsWith("2024", StringComparison.Ordinal) ||
            p.Name.Contains("SpellCastingAtLevel1") || p.Name.StartsWith("EnableWarlock", StringComparison.Ordinal) || p.Name == "EnableLevel20" || p.Name == "EnableMulticlass" || p.Name == "UseAlternateSpellPointsSystem" || p.Name is "RemoveSchoolRestrictionsFromSpellBlade" or "RemoveSchoolRestrictionsFromShadowCaster"))
        .OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.Name + "=" + p.GetValue(Main.Settings)));
    private static bool TryBinding(RulesetCharacter actor, out Binding binding)
    {
        binding = null;
        if (!RuntimeEnabled) return false;
        if (ResolveOwner(actor) is not RulesetCharacterHero hero) return false;
        if (!Owners.TryGet(actor, out var budget) || !Bindings.TryGetValue(hero, out binding) || !ReferenceEquals(budget, binding.Budget))
            throw new InvalidOperationException("Standard resources: hero has no validated new-character journal; advancement/save conversion is unavailable.");
        ValidateHero(hero, binding.Progression);
        if (binding.Budget.Current.Mode != (Main.Settings.UseAlternateSpellPointsSystem ? ResourcePlan.ResourceMode.SpellPoints : ResourcePlan.ResourceMode.Slots) ||
            binding.SettingsSignature != ResourceSettingsSignature())
            throw new InvalidOperationException("Standard resource rules changed: restart and a validated new session are required.");
        return true;
    }
    internal static bool TryCasterLevel(RulesetCharacter actor, out int level)
    {
        level = 0;
        if (!TryBinding(actor, out var b)) return false;
        level = b.Budget.Current.CasterLevel; return true;
    }
    internal static bool TryMaximumCircle(RulesetCharacter actor, out int circle)
    {
        circle = 0;
        if (!TryBinding(actor, out var b)) return false;
        circle = b.Budget.Current.MaximumCircle; return true;
    }
    internal static bool TryPoints(RulesetCharacter actor, out int maximum, out int remaining)
    {
        maximum = remaining = 0;
        if (!TryBinding(actor, out var b) || b.Budget.Current.Mode != ResourcePlan.ResourceMode.SpellPoints) return false;
        maximum = b.Budget.Current.PointMaximum; remaining = b.Budget.RemainingPoints; return true;
    }
    internal static bool TryBasePointMaximum(RulesetCharacter actor, out int maximum)
    {
        maximum = 0;
        if (!TryBinding(actor, out var b)) return false;
        maximum = b.Budget.Current.BasePointMaximum; return true;
    }
    private static bool StandardRepertoire(RulesetSpellRepertoire repertoire) =>
        repertoire.SpellCastingFeature.SpellCastingOrigin is CastingOrigin.Class or CastingOrigin.Subclass;
    internal static bool TryCanSpend(RulesetCharacter actor, int circle, out bool available)
    {
        available = false;
        if (!TryBinding(actor, out var b)) return false;
        if (PactMagicMulticasterContext.CanSpend(actor, circle, out available)) return true;
        available = b.Budget.CanSpend(circle); return true;
    }
    internal static bool TrySpend(RulesetSpellRepertoire repertoire, int circle)
    {
        if (!StandardRepertoire(repertoire) || !TryBinding(repertoire.GetCaster(), out var b)) return false;
        if (PactMagicMulticasterContext.Spend(repertoire, circle)) return true;
        if (circle != 0 && (spellOperation == null || !b.Budget.Spend(spellOperation, circle)))
            throw new InvalidOperationException("Standard resources: missing action identity or exhausted resource. Conversion/power callers require later typed transaction adapters.");
        Project(repertoire.GetCaster(), b); return true;
    }
    internal static bool Recover(RulesetCharacter actor, Dictionary<int, int> slots)
    {
        if (!TryBinding(actor, out var b)) return false;
        if (slots.Keys.Any(c => c is < 1 or > 9)) throw new InvalidOperationException("Pact sentinel/unknown resource is not an ordinary recovery index; use its typed owner.");
        if (b.Budget.Current.CasterLevel == 0 && slots.Count > 0) throw new InvalidOperationException("No standard caster budget to recover; Pact/Arcanum require their own recovery path.");
        b.Budget.Recover("recovery:" + Guid.NewGuid(), slots); Project(actor, b); return true;
    }
    internal static bool AddPoints(RulesetCharacter actor, int points)
    {
        if (!TryBinding(actor, out var b) || b.Budget.Current.Mode != ResourcePlan.ResourceMode.SpellPoints) return false;
        b.Budget.RecoverPoints("point-recovery:" + Guid.NewGuid(), points); Project(actor, b); return true;
    }
    internal static bool PrepareRefresh(RulesetCharacter actor)
    {
        if (!TryBinding(actor, out var b)) return false;
        if (b.Projecting || b.Budget.Current.Mode != ResourcePlan.ResourceMode.SpellPoints) return true;
        var hero = (RulesetCharacterHero)ResolveOwner(actor);
        foreach (var condition in hero.AllConditions.ToArray())
        {
            var remove = false;
            foreach (var feature in condition.ConditionDefinition.Features.OfType<FeatureDefinitionMagicAffinity>()
                         .Where(x => x.Name != "MagicAffinityChitinousBoonAdditionalSpellSlot"))
            for (var i = 0; i < feature.AdditionalSlots.Count; i++)
            {
                var slot = feature.AdditionalSlots[i];
                b.Budget.GrantTemporarySlots("condition:" + condition.Guid + ":" + feature.Name + ":" + i, slot.SlotLevel, slot.SlotsNumber);
                remove = true;
            }
            if (remove) hero.RemoveCondition(condition);
        }
        return true;
    }
    internal static bool Refresh(RulesetCharacter actor)
    {
        if (!TryBinding(actor, out var b)) return false;
        if (b.Projecting) return true;
        var hero = (RulesetCharacterHero)ResolveOwner(actor);
        var modifiers = new List<ResourcePlan.Modifier>();
        // In slot mode separate condition instances are distinct capacity sources. They are not
        // duplicate repertoire views of the same permanent feature.
        var conditionFeatures = new HashSet<FeatureDefinitionMagicAffinity>();
        if (b.Budget.Current.Mode == ResourcePlan.ResourceMode.Slots)
        foreach (var condition in hero.AllConditions)
        foreach (var feature in condition.ConditionDefinition.Features.OfType<FeatureDefinitionMagicAffinity>()
                     .Where(x => x.Name != "MagicAffinityChitinousBoonAdditionalSpellSlot"))
        {
            conditionFeatures.Add(feature);
            for (var i = 0; i < feature.AdditionalSlots.Count; i++)
            {
                var slot = feature.AdditionalSlots[i];
                modifiers.Add(new ResourcePlan.Modifier("condition:" + condition.Guid + ":" + feature.Name + ":" + i, slot.SlotLevel, slot.SlotsNumber));
            }
        }
        foreach (var feature in hero.FeaturesToBrowse.OfType<FeatureDefinitionMagicAffinity>()
                     .Where(x => !conditionFeatures.Contains(x))
                     .Where(x => x.Name != "MagicAffinityChitinousBoonAdditionalSpellSlot"))
        for (var i = 0; i < feature.AdditionalSlots.Count; i++)
        {
            var slot = feature.AdditionalSlots[i];
            modifiers.Add(new ResourcePlan.Modifier(feature.Name + ":" + i, slot.SlotLevel, slot.SlotsNumber));
        }
        if (b.Budget.Current.Mode == ResourcePlan.ResourceMode.SpellPoints)
        {
            var power = CanonicalPower(hero, b);
            if (power != null)
            {
                var extra = hero.GetMaxUsesOfPower(power) - b.Budget.Current.BasePointMaximum;
                if (extra < 0) throw new InvalidOperationException("Unsupported negative Spell Points capacity modifier.");
                modifiers.Add(new ResourcePlan.Modifier("native-point-capacity", pointCapacity: extra));
            }
        }
        b.Budget.Refresh(Allocate(b.Progression, modifiers));
        PactMagicMulticasterContext.Synchronize(hero, b.Progression);
        Project(actor, b); return true;
    }
    private static RulesetUsablePower CanonicalPower(RulesetCharacterHero hero, Binding b)
    {
        var powers = hero.UsablePowers.Where(x => x.PowerDefinition == SpellPointsContext.PowerSpellPoints).ToArray();
        if (powers.Length > 1 || powers.Length == 0 && b.Budget.Current.CasterLevel > 0)
            throw new InvalidOperationException("Standard resources: expected exactly one pre-existing PowerSpellPoints object; no automatic replacement/refill.");
        return powers.SingleOrDefault();
    }
    private static void Project(RulesetCharacter actor, Binding b)
    {
        if (b.Projecting) return;
        b.Projecting = true;
        try
        {
            var hero = (RulesetCharacterHero)ResolveOwner(actor);
            if (b.Budget.Current.Mode == ResourcePlan.ResourceMode.SpellPoints)
            {
                var pool = CanonicalPower(hero, b);
                if (pool != null)
                {
                    pool.remainingUses = b.Budget.RemainingPoints;
                    if (!ReferenceEquals(actor, hero))
                    {
                        actor.UsablePowers.RemoveAll(x => x.PowerDefinition == SpellPointsContext.PowerSpellPoints && !ReferenceEquals(x, pool));
                        if (!actor.UsablePowers.Contains(pool)) actor.UsablePowers.Add(pool);
                    }
                }
            }
            var views = hero.SpellRepertoires.Concat(ReferenceEquals(actor, hero) ? Enumerable.Empty<RulesetSpellRepertoire>() : actor.SpellRepertoires).Distinct();
            foreach (var view in views.Where(StandardRepertoire))
            {
                var capacities = new Dictionary<int, int>(); var used = new Dictionary<int, int>();
                for (var circle = 1; circle <= 9; circle++)
                {
                    var capacity = b.Budget.Current.Slots[circle - 1];
                    if (capacity == 0) continue;
                    capacities[circle] = capacity; used[circle] = capacity - b.Budget.RemainingSlots(circle);
                }
                PactMagicMulticasterContext.Project(actor, view, capacities, used);
                view.spellsSlotCapacities = capacities; view.usedSpellsSlots = used;
                view.RepertoireRefreshed?.Invoke(view);
            }
        }
        finally { b.Projecting = false; }
    }
    internal static bool SuppressNativeRestoration(RulesetCharacter actor) => TryBinding(actor, out _);
    internal static void FinishRest(RulesetCharacter actor, RestType restType, bool simulate)
    {
        if (!TryBinding(actor, out var b) || simulate) return;
        var operation = "rest:" + Guid.NewGuid();
        b.Budget.Rest(operation, restType == RestType.LongRest);
        PactMagicMulticasterContext.Rest(actor, operation, restType == RestType.LongRest, false); Project(actor, b);
    }
}
