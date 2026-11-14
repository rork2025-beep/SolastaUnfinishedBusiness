#if SOLASTA_FLEXIBLE_CASTING_QA
using System;
using System.Collections.Generic;
using System.Linq;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using M = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using Q = SolastaUnfinishedBusiness.Models.QaEditorFinalizationPlan;
using Plan = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;
using ResourcePlan = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class MultipleSpellRepertoiresContext
{
    internal static C QaEditorCalculator()
    {
        var d = definitions?["Sorcerer"] ?? throw new InvalidOperationException("Missing captured original Sorcerer profile");
        var choice = d.Sources.Where(s => s.Feature is FeatureDefinitionSubclassChoice).Select(s => s.Original).DefaultIfEmpty(0).Min();
        return new C("C2-editor-Sorcerer", new[] { new C.ClassProfile("Sorcerer", true,
            new C.CastingProfile(d.Casting.Name, C.CasterKind.Full, 1, true, d.Profile.MaximumCircles), choice,
            d.Subclasses.Keys.Select(id => new C.SubclassProfile(id, true))) });
    }
    internal static M.Transaction QaEditorSpellDraft(RulesetCharacterHero hero, string id, int actual, bool compressed)
    {
        QaEditorFinalizationContext.RequireCandidate(hero);
        var session = Sessions.GetValue(hero, _ => throw new InvalidOperationException("Missing level0 repertoire owner"));
        var sub = hero.ClassesAndSubclasses.FirstOrDefault(c => c.Key.Name == "Sorcerer").Value?.Name;
        var profile = Profile(definitions["Sorcerer"], sub, hero);
        if (profile.Mode != M.Knowledge.Selection) throw new InvalidOperationException("C.2 requires the installed selection-known Sorcerer casting mode");
        if (profile.Grants.Any(g => g.Original <= actual && g.Kind is M.GrantKind.BonusChoice or M.GrantKind.KnownBonus or M.GrantKind.ScribingBonus))
            throw new InvalidOperationException("C.2 does not authorize extra subclass/feat spell choice pools");
        return new M.Transaction(id, session.State, profile, actual, compressed);
    }
    internal static void QaEditorPublish(RulesetCharacterHero hero, M.Transaction transaction, RulesetSpellRepertoire repertoire)
    {
        var session = Sessions.GetValue(hero, _ => throw new InvalidOperationException("Missing repertoire owner"));
        if (session.Published.Contains(transaction.Id)) throw new InvalidOperationException("Duplicate QA repertoire publication");
        var committed = transaction.Commit();
        var view = committed.Repertoires["Sorcerer"];
        session.State = committed.With(new M.Repertoire(view.Definition, view.ActualLevel, view.Compressed, view.Known, view.Cantrips,
            repertoire.PreparedSpells.Select(s => s.Name), view.History,
            repertoire.ExtraSpellsByTag.Select(p => new KeyValuePair<string, IReadOnlyList<string>>(p.Key, p.Value.Select(s => s.Name).Distinct().ToArray()))));
        session.Published.Add(transaction.Id);
    }
}
internal static partial class PactMagicMulticasterContext
{
    internal static Plan.CastingLedger QaEditorLedger(RulesetCharacterHero hero) => Owners.GetValue(hero,
        _ => throw new InvalidOperationException("Missing level0 Pact/Standard owner"));
    internal static void QaEditorSynchronize(RulesetCharacterHero hero, C.Result result)
    {
        var ledger = QaEditorLedger(hero);
        if (result.PactProgressions.Count != 0 || ledger.Pact.Used != 0) throw new InvalidOperationException("C.2 excludes Pact admission");
        ledger.Pact.Refresh(new Plan.Allocation(tables, result));
    }
}
internal static partial class StandardMulticasterResourcesContext
{
    internal static void QaBindEditorZero(RulesetCharacterHero hero, C.Result zero) => BindNewHeroCore(hero, zero, true);
    internal static Q.Publication QaEditorPublication(RulesetCharacterHero hero, M.Transaction spells, C.Result result, RulesetSpellRepertoire repertoire)
    {
        QaEditorFinalizationContext.RequireCandidate(hero);
        var binding = Bindings.GetValue(hero, _ => throw new InvalidOperationException("Missing level0 Standard owner"));
        var ledger = PactMagicMulticasterContext.QaEditorLedger(hero);
        if (!ReferenceEquals(ledger.Standard, binding.Budget)) throw new InvalidOperationException("Duplicate Standard owner");
        ValidateHero(hero, result);
        var allocation = Allocate(result);
        var pool = Main.Settings.UseAlternateSpellPointsSystem ? CanonicalPower(hero, binding) : null;
        if (Main.Settings.UseAlternateSpellPointsSystem && (pool == null || hero.GetMaxUsesOfPower(pool) != allocation.PointMaximum))
            throw new InvalidOperationException("Native Spell Points maximum/pool differs from installed owner");
        var oldCaps = repertoire.spellsSlotCapacities; var oldUsed = repertoire.usedSpellsSlots;
        var caps = new Dictionary<int, int>(); var used = new Dictionary<int, int>();
        for (var circle = 1; circle <= 9; circle++)
            if (allocation.Slots[circle - 1] > 0) { caps.Add(circle, allocation.Slots[circle - 1]); used.Add(circle, 0); }
        var notifications = (repertoire.RepertoireRefreshed?.GetInvocationList() ?? Array.Empty<Delegate>())
            .Cast<RulesetSpellRepertoire.RepertoireRefreshedHandler>().Select(d => (Action)(() => d(repertoire))).ToArray();
        var sorcery = hero.UsedSorceryPoints;
        void Validate()
        {
            QaEditorFinalizationContext.RequireCandidate(hero); ValidateHero(hero, result);
            if (binding.SettingsSignature != ResourceSettingsSignature() || !ReferenceEquals(oldCaps, repertoire.spellsSlotCapacities) ||
                !ReferenceEquals(oldUsed, repertoire.usedSpellsSlots) || hero.UsedSorceryPoints != sorcery || sorcery != 0 ||
                repertoire.usedSpellsSlots.Values.Any(n => n != 0) || hero.AllConditions.Any())
                throw new InvalidOperationException("Changed/non-editor resource state before QA publication");
            if (pool != null && (!ReferenceEquals(pool, CanonicalPower(hero, binding)) || pool.RemainingUses != allocation.PointMaximum))
                throw new InvalidOperationException("Native SP pool is not fresh/unspent");
            if (allocation.Mode == ResourcePlan.ResourceMode.Slots && Enumerable.Range(1, 9).Any(circle =>
                    (oldCaps.TryGetValue(circle, out var count) ? count : 0) != allocation.Slots[circle - 1]))
                throw new InvalidOperationException("Native slot capacity differs from the captured standard resource row");
        }
        return new Q.Publication(Validate, () =>
        {
            // Reuse the three existing owners; no native feature/spell or additional power grant.
            binding.Budget.Refresh(allocation); binding.Progression = result;
            PactMagicMulticasterContext.QaEditorSynchronize(hero, result);
            MultipleSpellRepertoiresContext.QaEditorPublish(hero, spells, repertoire);
            repertoire.spellsSlotCapacities = caps; repertoire.usedSpellsSlots = used;
            if (pool != null) pool.remainingUses = binding.Budget.RemainingPoints;
        }, () =>
        {
            if (hero.UsedSorceryPoints != sorcery || !ReferenceEquals(ledger.Standard, binding.Budget) || ledger.Pact.Remaining != 0 ||
                !Enumerable.Range(1, 9).All(c => binding.Budget.RemainingSlots(c) ==
                    (allocation.Mode == ResourcePlan.ResourceMode.Slots ? allocation.Slots[c - 1] : caps.ContainsKey(c) ? 1 : 0)) ||
                pool != null && pool.RemainingUses != binding.Budget.RemainingPoints)
                throw new InvalidOperationException("Incoherent QA editor resource projection");
        }, notifications);
    }
}
#endif
