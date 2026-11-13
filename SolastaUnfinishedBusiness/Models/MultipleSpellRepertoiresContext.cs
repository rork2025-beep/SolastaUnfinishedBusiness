using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Classes;
using SolastaUnfinishedBusiness.Patches;
using static FeatureDefinitionCastSpell;
using static RuleDefinitions;
using Plan = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;

namespace SolastaUnfinishedBusiness.Models;

// Stage13.4: native repertoire ownership/access adapter. No class admission, Pact or second budget.
internal static class MultipleSpellRepertoiresContext
{
    internal static bool RuntimeEnabled => StandardMulticasterResourcesContext.RuntimeEnabled;
    private sealed class Source(FeatureDefinition feature, int original, string id, FeatureDefinition[] requiredOptions)
    {
        internal readonly FeatureDefinition Feature = feature;
        internal readonly int Original = original;
        internal readonly string Id = id;
        internal Plan.Grant[] Grants = Array.Empty<Plan.Grant>();
        internal readonly FeatureDefinition[] RequiredOptions = requiredOptions;
    }
    private sealed class Definition(CharacterClassDefinition owner, FeatureDefinitionCastSpell casting, Plan.Profile profile,
        Source[] sources, Dictionary<string, Source[]> subclasses)
    {
        internal readonly CharacterClassDefinition Owner = owner;
        internal readonly FeatureDefinitionCastSpell Casting = casting;
        internal readonly Plan.Profile Profile = profile;
        internal readonly Source[] Sources = sources;
        internal readonly Dictionary<string, Source[]> Subclasses = subclasses;
    }
    private static Dictionary<string, Definition> definitions;
    private static readonly ConditionalWeakTable<RulesetCharacterHero, Session> Sessions = new();
    private sealed class Session
    {
        internal Plan.HeroState State = new(Array.Empty<Plan.Repertoire>());
        internal readonly Dictionary<string, Plan.Transaction> Transactions = new(StringComparer.Ordinal);
        internal readonly HashSet<string> Published = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Tag> Tags = new(StringComparer.Ordinal);
    }
    private sealed class Tag(string transaction, string owner, string feature, int original, string grant = null)
    {
        internal readonly string Transaction = transaction;
        internal readonly string Class = owner;
        internal readonly string Feature = feature;
        internal readonly int Original = original;
        internal readonly string Grant = grant;
    }
    private static Source[] Sources(IEnumerable<FeatureUnlockByLevel> unlocks, string owner)
    {
        var result = new List<Source>();
        void Walk(FeatureDefinition feature, int original, string path, FeatureDefinition[] selected, HashSet<FeatureDefinition> ancestors)
        {
            if (!ancestors.Add(feature)) throw new InvalidOperationException("Cyclic feature graph: " + owner);
            result.Add(new Source(feature, original, path, selected));
            if (feature is FeatureDefinitionFeatureSet set)
                for (var i = 0; i < set.FeatureSet.Count; i++)
                    Walk(set.FeatureSet[i], original, path + ":" + i,
                        set.Mode == FeatureDefinitionFeatureSet.FeatureSetMode.Exclusion ? selected.Append(set.FeatureSet[i]).ToArray() : selected,
                        new HashSet<FeatureDefinition>(ancestors));
        }
        foreach (var entry in unlocks.Select((u, index) => (Unlock: u, Index: index)))
            Walk(entry.Unlock.FeatureDefinition, entry.Unlock.Level, owner + ":" + entry.Index, Array.Empty<FeatureDefinition>(), new HashSet<FeatureDefinition>());
        return result.ToArray();
    }
    private static int[] Cumulative(IReadOnlyList<int> values) => Enumerable.Range(0, 20).Select(i => values.Count == 0 ? 0 : values[Math.Min(i, values.Count - 1)]).ToArray();
    private static int[] Events(IReadOnlyList<int> values) => Enumerable.Range(0, 20).Select(i => i < values.Count ? values[i] : 0).ToArray();
    private static Plan.Spell[] Spells(SpellListDefinition list) => list == null ? Array.Empty<Plan.Spell>() : list.SpellsByLevel
        .SelectMany(s => s.Spells).Distinct().Select(s => new Plan.Spell(s.Name, s.SpellLevel)).ToArray();
    internal static void CaptureInstalledDefinitions()
    {
        if (definitions != null) return;
        try { CaptureCore(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // An unsupported installed source must not break ordinary gameplay with this gate off.
            Main.Error("Multicaster repertoire capture unavailable; admission stays closed: " + ex.Message);
        }
    }
    private static void CaptureCore()
    {
        var captured = new Dictionary<string, Definition>(StringComparer.Ordinal);
        foreach (var name in new[] { "Bard", "Cleric", "Druid", "Paladin", "Ranger", "Sorcerer", "Wizard", "Warlock", InventorClass.ClassName })
        {
            var owner = DatabaseHelper.GetDefinition<CharacterClassDefinition>(name);
            var sources = Sources(owner.FeatureUnlocks, owner.Name);
            var casting = sources.Select(x => x.Feature).OfType<FeatureDefinitionCastSpell>().Distinct()
                .Single(x => x.SpellCastingOrigin == CastingOrigin.Class);
            var mode = casting.SpellKnowledge switch
            {
                SpellKnowledge.Selection => Plan.Knowledge.Selection, SpellKnowledge.Spellbook => Plan.Knowledge.Spellbook,
                SpellKnowledge.WholeList => Plan.Knowledge.WholeList,
                _ => throw new InvalidOperationException("Unsupported installed class spell knowledge: " + name)
            };
            var table = Main.Settings.EnablePreparedSpellsTables2024
                ? RulesetCharacterHeroPatcher.ComputeMaxPreparedSpells_Patch.CopyInstalledPreparedTable(owner) : null;
            var prepared = casting.SpellReadyness != SpellReadyness.Prepared ? Plan.Preparation.None : table != null
                ? Plan.Preparation.InstalledTable : casting.SpellPreparationCount switch
                {
                    SpellPreparationCount.AbilityBonusPlusLevel => Plan.Preparation.AbilityPlusLevel,
                    SpellPreparationCount.AbilityBonusPlusHalfLevel => Plan.Preparation.AbilityPlusHalfLevel,
                    _ => Plan.Preparation.AbilityOnly
                };
            var profile = new Plan.Profile(name, casting.Name, casting.spellcastingAbility, casting.SpellListDefinition.Name,
                mode, prepared, Spells(casting.SpellListDefinition),
                casting.SlotsPerLevels.Take(20).Select(r => r.Slots.FindLastIndex(n => n > 0) + 1),
                Cumulative(casting.KnownCantrips), Cumulative(casting.KnownSpells), Events(casting.ReplacedSpells), Events(casting.ScribedSpells), table);
            var subclasses = sources.Select(x => x.Feature).OfType<FeatureDefinitionSubclassChoice>().SelectMany(c => c.Subclasses).Distinct()
                .ToDictionary(id => id, id => Sources(DatabaseHelper.GetDefinition<CharacterSubclassDefinition>(id).FeatureUnlocks, id), StringComparer.Ordinal);
            var definition = new Definition(owner, casting, profile, sources, subclasses);
            foreach (var source in sources.Concat(subclasses.Values.SelectMany(x => x))) source.Grants = CaptureGrants(definition, source);
            captured.Add(name, definition);
        }
        definitions = captured; // atomic immutable source publication, before any class projection
    }
    private static Source[] OwnSources(Definition definition, string subclass, RulesetCharacterHero hero) => definition.Sources.Concat(subclass == null
        ? Array.Empty<Source>() : definition.Subclasses.TryGetValue(subclass, out var sources) ? sources
        : throw new InvalidOperationException("Unregistered casting subclass: " + subclass)).Where(s => s.RequiredOptions.All(f => hero.HasAnyFeature(f))).ToArray();
    private static Plan.Profile Profile(Definition definition, string subclass, RulesetCharacterHero hero)
    {
        var p = definition.Profile;
        return new Plan.Profile(p.ClassId, p.FeatureId, p.Ability, p.ListId, p.Mode, p.PreparedRule, p.List.Values,
            p.MaximumCircles, p.Cantrips, p.Known, p.Replacements, p.Scribed, p.PreparedTable, OwnSources(definition, subclass, hero).SelectMany(s => s.Grants));
    }
    private static Plan.Grant[] CaptureGrants(Definition definition, Source source)
    {
        var grants = new List<Plan.Grant>();
        {
            if (source.Feature is FeatureDefinitionAutoPreparedSpells auto && auto.SpellcastingClass == definition.Owner)
            {
                for (var index = 0; index < auto.AutoPreparedSpellsGroups.Count; index++)
                {
                    var group = auto.AutoPreparedSpellsGroups[index];
                    grants.Add(new Plan.Grant(source.Id + ":" + auto.Name + ":" + index, Math.Max(source.Original, group.ClassLevel), Plan.GrantKind.AutoPrepared,
                        group.SpellsList.Select(s => new Plan.Spell(s.Name, s.SpellLevel))));
                }
            }
            if (source.Feature is FeatureDefinitionMagicAffinity affinity && affinity.ExtendedSpellList)
                grants.Add(new Plan.Grant(source.Id + ":" + affinity.Name, source.Original, Plan.GrantKind.ExpandedList, Spells(affinity.ExtendedSpellList)));
            if (source.Feature is FeatureDefinitionMagicAffinity learning)
            {
                if (learning.AdditionalScribedSpells > 0) grants.Add(new Plan.Grant(source.Id + ":scribing", source.Original,
                    Plan.GrantKind.ScribingBonus, Array.Empty<Plan.Spell>(), learning.AdditionalScribedSpells));
                if (learning.AdditionalKnownSpellsCount > 0) grants.Add(new Plan.Grant(source.Id + ":known", source.Original,
                    Plan.GrantKind.KnownBonus, Array.Empty<Plan.Spell>(), learning.AdditionalKnownSpellsCount));
            }
            if (source.Feature is FeatureDefinitionPointPool pool && pool.PoolType is HeroDefinitions.PointsPoolType.Cantrip or HeroDefinitions.PointsPoolType.Spell or HeroDefinitions.PointsPoolType.CantripOrSpell)
                grants.Add(new Plan.Grant(source.Id + ":" + pool.Name, source.Original, pool.ExtraSpellsTag == "MysticArcanum" ? Plan.GrantKind.MysticArcanum : Plan.GrantKind.BonusChoice,
                    Spells(pool.SpellListOverride ?? definition.Casting.SpellListDefinition)
                        .Where(s => s.Circle >= (pool.PoolType == HeroDefinitions.PointsPoolType.Spell ? Math.Max(1, pool.MinSpellLevel) : pool.MinSpellLevel) &&
                            s.Circle <= Plan.ChoiceMaximum(pool.MaxSpellLevel, pool.PoolType == HeroDefinitions.PointsPoolType.Cantrip) &&
                            (!pool.RitualOnly || DatabaseHelper.GetDefinition<SpellDefinition>(s.Id).Ritual)), pool.PoolAmount, pool.ExtraSpellsTag));
        }
        return grants.ToArray();
    }
    internal static void EnsureCanBind(RulesetCharacterHero hero)
    {
        if (!RuntimeEnabled || definitions == null || hero == null || hero.ClassesAndLevels.Values.Sum() != 0 || Sessions.TryGetValue(hero, out _))
            throw new InvalidOperationException("Repertoire binding requires a new hero, captured definitions and an open gate.");
    }
    internal static void BindNewHero(RulesetCharacterHero hero)
    {
        EnsureCanBind(hero);
        Sessions.Add(hero, new Session());
    }
    internal static void Synchronize(RulesetCharacterHero hero, CompressedMulticasterProgressionPlan.Result progression)
    {
        if (!RuntimeEnabled || !Sessions.TryGetValue(hero, out var session) || !progression.IsValid || progression.PactProgressions.Count > 1)
            throw new InvalidOperationException("Unbound/invalid standard repertoire session.");
        var views = new List<Plan.Repertoire>();
        foreach (var c in progression.Classes.Where(c => c.IsCastingParticipant))
        {
            if (!definitions.TryGetValue(c.ClassId, out var definition) || definition.Casting.Name != c.CastingDefinitionId)
                throw new InvalidOperationException("Unsupported repertoire owner/feature.");
            var matches = hero.SpellRepertoires.Where(r => r.SpellCastingClass == definition.Owner && r.SpellCastingFeature == definition.Casting).ToArray();
            if (matches.Length != 1 || matches[0].SpellCastingAbility != definition.Profile.Ability) throw new InvalidOperationException("Missing/duplicate repertoire or mismatched ability.");
            var rep = matches[0]; var p = Profile(definition, c.SubclassId, hero);
            var known = rep.KnownSpells.AsEnumerable();
            if (p.Mode == Plan.Knowledge.Spellbook) known = known.Concat(rep.EnumerateAvailableScribedSpells());
            var old = session.State.Repertoires.TryGetValue(c.ClassId, out var before) ? before : null;
            views.Add(new Plan.Repertoire(p, c.ActualClassLevel, c.EffectiveCastingLevel != c.ActualClassLevel ||
                progression.Input.Classes.Single(i => i.ClassId == c.ClassId).CompressionEnabled,
                known.Select(s => s.Name).Distinct(), rep.KnownCantrips.Select(s => s.Name).Distinct(), rep.PreparedSpells.Select(s => s.Name).Distinct(),
                old?.History, rep.ExtraSpellsByTag.Select(x => new KeyValuePair<string, IReadOnlyList<string>>(x.Key,
                    Array.AsReadOnly(x.Value.Select(s => s.Name).Distinct().ToArray())))));
        }
        session.State = new Plan.HeroState(views); // racial/feat/Secrets list owners never create a class
    }
    private static Plan.Repertoire CurrentView(Session session, string owner)
    {
        var pending = session.Transactions.Values.Where(t => !t.IsClosed && t.ClassId == owner).ToArray();
        if (pending.Length > 1) throw new InvalidOperationException("Multiple active advancements for the same repertoire.");
        if (pending.Length == 1) return pending[0].Preview().Repertoires[owner];
        return session.State.Repertoires.TryGetValue(owner, out var view) ? view : null;
    }
    private static bool TryView(RulesetCharacter character, RulesetSpellRepertoire repertoire, out Plan.Repertoire view)
    {
        view = null;
        if (!RuntimeEnabled || repertoire.SpellCastingFeature.SpellCastingOrigin is not (CastingOrigin.Class or CastingOrigin.Subclass)) return false;
        var hero = character as RulesetCharacterHero ?? DruidWildshapeContext.StandardResourceOwner(character);
        if (hero == null || !Sessions.TryGetValue(hero, out var session) || repertoire.SpellCastingClass == null ||
            (view = CurrentView(session, repertoire.SpellCastingClass.Name)) == null || view.Definition.FeatureId != repertoire.SpellCastingFeature.Name)
            throw new InvalidOperationException("Unclassified/unbound class repertoire; no shared-circle learning fallback.");
        return true;
    }
    internal static bool TryAccess(RulesetSpellRepertoire repertoire, out int maximum)
    {
        maximum = 0;
        if (!RuntimeEnabled || !TryView(repertoire.GetCaster(), repertoire, out var view)) return false;
        maximum = view.MaximumCircle; return true;
    }
    private static int ScopedPreparedBonus(RulesetCharacterHero hero, Plan.Repertoire view, int ability)
    {
        var definition = definitions[view.Definition.ClassId];
        hero.ClassesAndSubclasses.TryGetValue(definition.Owner, out var subclass);
        var own = new HashSet<FeatureDefinition>(OwnSources(definition, subclass?.Name, hero).Where(s => s.Original <= view.EffectiveLevel).Select(s => s.Feature));
        // Class/subclass affinity ownership is explicit. Generic item/feat affinity classification
        // is intentionally not guessed from another repertoire's list (Stage13.5 adapters).
        var all = new HashSet<FeatureDefinition>(definitions.Values.SelectMany(d => d.Sources.Concat(d.Subclasses.Values.SelectMany(s => s))).Select(s => s.Feature));
        var features = hero.FeaturesByType<FeatureDefinitionMagicAffinity>().ToArray();
        if (features.Any(f => f.PreparedSpellModifier != PreparedSpellsModifier.None && !all.Contains(f)))
            throw new InvalidOperationException("Prepared affinity has no registered class owner; explicit auxiliary adapter required.");
        return features.Where(f => own.Contains(f)).Sum(f => f.PreparedSpellModifier switch
        {
            PreparedSpellsModifier.ProficiencyBonus => hero.TryGetAttributeValue(AttributeDefinitions.ProficiencyBonus),
            PreparedSpellsModifier.SpellcastingAbilityBonus => ability, _ => 0
        });
    }
    internal static bool TryPrepared(RulesetCharacterHero hero, RulesetSpellRepertoire repertoire, out int maximum)
    {
        maximum = 0;
        if (!TryView(hero, repertoire, out var view)) return false;
        if (view.Definition.PreparedRule == Plan.Preparation.None) return true;
        var ability = AttributeDefinitions.ComputeAbilityScoreModifier(hero.GetAttribute(view.Definition.Ability).CurrentValue);
        maximum = view.Definition.PreparedLimit(view.EffectiveLevel, ability, ScopedPreparedBonus(hero, view, ability)); return true;
    }
    internal static bool AutoPrepared(RulesetCharacter actor, RulesetSpellRepertoire repertoire)
    {
        if (!TryView(actor, repertoire, out var view)) return false;
        repertoire.AutoPreparedSpells.Clear();
        repertoire.AutoPreparedSpells.AddRange(view.AutoPrepared.Select(DatabaseHelper.GetDefinition<SpellDefinition>));
        return true;
    }
    internal static bool EnumerateKnown(CharacterHeroBuildingData data, List<SpellDefinition> result)
    {
        if (!RuntimeEnabled || !Sessions.TryGetValue(data.HeroCharacter, out var session)) return false;
        var owner = LevelUpHelper.GetSelectedClass(data.HeroCharacter);
        var view = owner == null ? null : CurrentView(session, owner.Name);
        if (view == null) throw new InvalidOperationException("Missing selected repertoire.");
        // Relearn/display-all are UI policies; neither changes class ownership nor removes spells
        // from another repertoire. Legal cross-class overlap must remain selectable.
        var spells = view.Known.Concat(view.Cantrips).Concat(view.AutoPrepared).Concat(view.Bonus.Values.SelectMany(s => s));
        var tags = new HashSet<string>(session.Tags.Where(x => x.Value.Class == owner.Name).Select(x => x.Key), StringComparer.Ordinal);
        spells = spells.Concat(data.AcquiredSpells.Where(x => tags.Contains(x.Key)).SelectMany(x => x.Value).Select(s => s.Name))
            .Concat(data.AcquiredCantrips.Where(x => tags.Contains(x.Key)).SelectMany(x => x.Value).Select(s => s.Name));
        result.Clear(); result.AddRange(spells.Distinct().Select(DatabaseHelper.GetDefinition<SpellDefinition>)); return true;
    }
    // Typed transaction API for finalization13.7. No implicit parsing of a different class's tags.
    internal static Plan.Transaction BeginAdvancement(RulesetCharacterHero hero, string id, string classId, string subclass, int actual, bool compressed)
    {
        if (!RuntimeEnabled || !Sessions.TryGetValue(hero, out var session)) throw new InvalidOperationException("Repertoire gate closed/unbound.");
        if (session.Published.Contains(id)) throw new InvalidOperationException("Advancement identity already published.");
        if (session.Transactions.TryGetValue(id, out var existing))
        {
            if (existing.ClassId != classId || existing.ActualLevel != actual || existing.Compressed != compressed)
                throw new InvalidOperationException("Conflicting advancement identity.");
            return existing;
        }
        if (session.Transactions.Values.Any(t => !t.IsClosed)) throw new InvalidOperationException("Finish or cancel the previous repertoire advancement first.");
        var transaction = new Plan.Transaction(id, session.State, Profile(definitions[classId], subclass, hero), actual, compressed);
        session.Transactions.Add(id, transaction);
        foreach (var e in transaction.Events)
        {
            var tag = "MultiRepertoire:" + id.Length + ":" + id + ":" + e.Original;
            session.Tags.Add(tag, new Tag(id, classId, definitions[classId].Casting.Name, e.Original));
            foreach (var grant in transaction.Definition.Grants.Where(g => (g.Kind is Plan.GrantKind.BonusChoice or Plan.GrantKind.MysticArcanum) && g.Original == e.Original))
                session.Tags.Add(tag + ":" + grant.Id.Length + ":" + grant.Id, new Tag(id, classId, definitions[classId].Casting.Name, e.Original, grant.Id));
        }
        return transaction;
    }
    private static bool TryTag(CharacterHeroBuildingData data, string tag, out Session session, out Tag origin)
    {
        session = null; origin = null;
        if (!RuntimeEnabled || tag == null || !Sessions.TryGetValue(data.HeroCharacter, out session)) return false;
        if (session.Tags.TryGetValue(tag, out origin)) return true;
        if (tag.StartsWith("MultiRepertoire:", StringComparison.Ordinal)) throw new InvalidOperationException("Stale/foreign repertoire origin.");
        return false;
    }
    internal static bool TryFeature(CharacterHeroBuildingData data, string tag, out FeatureDefinitionCastSpell casting)
    {
        casting = null;
        if (!TryTag(data, tag, out _, out var origin)) return false;
        casting = definitions[origin.Class].Casting; return true;
    }
    internal static bool CanAcquire(CharacterHeroBuildingData data, SpellDefinition spell, string tag, HeroDefinitions.PointsPoolType type)
    {
        if (!TryTag(data, tag, out var session, out var origin)) return true;
        var profile = session.Transactions[origin.Transaction].Definition;
        var allowed = origin.Grant == null ? profile.Allows(spell.Name, origin.Original, type == HeroDefinitions.PointsPoolType.Cantrip)
            : profile.Grants.Single(g => g.Id == origin.Grant).Spells.Any(s => s.Id == spell.Name && (profile.Grants.Single(g => g.Id == origin.Grant).Kind == Plan.GrantKind.MysticArcanum || s.Circle <= profile.MaximumCircles[origin.Original - 1]));
        if (!allowed) return false;
        var own = session.Transactions[origin.Transaction].Preview().Repertoires[origin.Class];
        return !own.Known.Concat(own.Cantrips).Concat(own.AutoPrepared).Concat(own.Bonus.Values.SelectMany(s => s)).Contains(spell.Name);
    }
    internal static void Submit(CharacterHeroBuildingData data, string tag)
    {
        if (!TryTag(data, tag, out var session, out var origin)) throw new InvalidOperationException("Unknown repertoire event.");
        IReadOnlyList<string> Selected(Dictionary<string, List<SpellDefinition>> values, string key) =>
            values.TryGetValue(key, out var spells) ? Array.AsReadOnly(spells.Select(s => s.Name).ToArray()) : Array.Empty<string>();
        var ordinary = session.Tags.Single(t => t.Value.Transaction == origin.Transaction && t.Value.Original == origin.Original && t.Value.Grant == null).Key;
        var bonuses = session.Tags.Where(t => t.Value.Transaction == origin.Transaction && t.Value.Original == origin.Original && t.Value.Grant != null)
            .Select(t => new KeyValuePair<string, IReadOnlyList<string>>(t.Value.Grant, Selected(data.AcquiredSpells, t.Key).Concat(Selected(data.AcquiredCantrips, t.Key)).ToArray()));
        session.Transactions[origin.Transaction].Select(new Plan.Choice(origin.Original, Selected(data.AcquiredSpells, ordinary),
            Selected(data.UnlearnedSpells, ordinary), Selected(data.AcquiredCantrips, ordinary), bonuses));
    }
    internal static bool ResetChoices(RulesetCharacterHero hero, string tag)
    {
        if (!RuntimeEnabled || !hero.TryGetHeroBuildingData(out var data) || !TryTag(data, tag, out var session, out var origin)) return false;
        session.Transactions[origin.Transaction].Reset();
        foreach (var own in session.Tags.Where(t => t.Value.Transaction == origin.Transaction).Select(t => t.Key))
        {
            data.AcquiredSpells.Remove(own); data.AcquiredCantrips.Remove(own); data.UnlearnedSpells.Remove(own);
            foreach (var stack in data.PointPoolStacks.Values) stack.ActivePools.Remove(own);
        }
        return true;
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.GetSpellFeature))]
    internal static class FeaturePatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, string tag, ref FeatureDefinitionCastSpell __result)
        { if (!TryFeature(heroBuildingData, tag, out var casting)) return true; __result = casting; return false; }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AcquireSpell))]
    internal static class AcquirePatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, SpellDefinition spell, string tag, HeroDefinitions.PointsPoolType poolType) =>
            CanAcquire(heroBuildingData, spell, tag, poolType);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.GrantCantripsAndSpellsByTag))]
    internal static class GrantBoundaryPatch
    {
        internal static bool Prefix(CharacterHeroBuildingData heroBuildingData, string tag, FeatureDefinitionCastSpell spellcastingFeature)
        {
            if (!TryFeature(heroBuildingData, tag, out var owner)) return true;
            Main.Error(owner != spellcastingFeature ? "Wrong repertoire feature for spell grant." : "Pending repertoire grant requires Stage13.7 atomic finalization.");
            return false;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.ClearPrevious))]
    internal static class ResetPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(RulesetCharacterHero hero, string tag) => !ResetChoices(hero, tag);
    }
    // Called only AFTER a future atomic native journal has granted the staged choices. This API
    // publishes managed origins; it never creates books, grants spells or refills shared resources.
    internal static void PublishValidatedCommit(RulesetCharacterHero hero, string transactionId)
    {
        if (!RuntimeEnabled || !Sessions.TryGetValue(hero, out var session)) throw new InvalidOperationException("Unbound repertoire commit.");
        if (session.Published.Contains(transactionId)) return;
        if (!session.Transactions.TryGetValue(transactionId, out var transaction)) throw new InvalidOperationException("Unknown repertoire commit.");
        var target = transaction.Preview().Repertoires[transaction.ClassId];
        var native = hero.SpellRepertoires.Single(r => r.SpellCastingClass?.Name == transaction.ClassId && r.SpellCastingFeature.Name == target.Definition.FeatureId);
        var known = native.KnownSpells.AsEnumerable();
        if (target.Definition.Mode == Plan.Knowledge.Spellbook) known = known.Concat(native.EnumerateAvailableScribedSpells());
        if (!new HashSet<string>(known.Select(s => s.Name)).SetEquals(target.Known) ||
            !new HashSet<string>(native.KnownCantrips.Select(s => s.Name)).SetEquals(target.Cantrips) ||
            target.Bonus.Values.SelectMany(s => s).Any(s => !native.ExtraSpellsByTag.Values.SelectMany(v => v).Any(v => v.Name == s)))
            throw new InvalidOperationException("Native spell inventory does not match the staged owning repertoire.");
        session.State = transaction.Commit();
        session.Published.Add(transactionId); session.Transactions.Remove(transactionId);
        foreach (var tag in session.Tags.Where(t => t.Value.Transaction == transactionId).Select(t => t.Key).ToArray()) session.Tags.Remove(tag);
    }
    internal static bool ValidateBeforeFinalize(RulesetCharacterHero hero)
    {
        if (!RuntimeEnabled) return true;
        if (!Sessions.TryGetValue(hero, out var session)) return false;
        try
        {
            foreach (var pair in session.Transactions)
            {
                if (pair.Value.IsCancelled) return false; // native pending-data cleanup belongs to the coordinator
                var target = pair.Value.Preview().Repertoires[pair.Value.ClassId];
                if (target.Definition.Mode != Plan.Knowledge.Spellbook) continue;
                var rep = hero.SpellRepertoires.Single(r => r.SpellCastingFeature.Name == target.Definition.FeatureId && r.SpellCastingClass.Name == target.Definition.ClassId);
                rep.GatherAndSortSpellbooks();
                if (!pair.Value.FitsSpellbooks(rep.foundSpellbooks.Select(b => (b.OwnerName, b.RemainingPages)), hero.Name,
                        ServiceRepository.GetService<IGameSettingsService>().SpellbookPagesConstraint,
                        target.ActualLevel == 1 && hero.ClassesAndLevels.Values.Sum() == 1 && !LevelUpHelper.IsLevelingUp(hero))) return false;
            }
            // Mutation/commit is deliberately not performed here. Native finalizer needs the13.7
            // resource+spellbook atomic journal; an unfinished transaction cannot fall through.
            return session.Transactions.Count == 0;
        }
        catch (InvalidOperationException ex) { Main.Error("Multicaster repertoire finalization rejected: " + ex.Message); return false; }
    }
    [HarmonyPatch(typeof(RulesetCharacterHero), nameof(RulesetCharacterHero.ComputeMaxPreparedSpells))]
    internal static class PreparedPatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(RulesetCharacterHero __instance, RulesetSpellRepertoire spellRepertoire, ref int __result)
        { if (TryPrepared(__instance, spellRepertoire, out var maximum)) __result = maximum; }
    }
}
