using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using static FeatureDefinitionCastSpell;
using Plan = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;

namespace SolastaUnfinishedBusiness.Models;

// Gated Pact adapter. Same hero and standard Budget as13.3; no new native power/repertoire.
internal static class PactMagicMulticasterContext
{
    internal static bool RuntimeEnabled => StandardMulticasterResourcesContext.RuntimeEnabled;
    private static Plan.Tables tables;
    private static readonly ConditionalWeakTable<RulesetCharacterHero, Plan.CastingLedger> Owners = new();
    [ThreadStatic] private static RulesetEffectSpell activeSpell;
    private sealed class SpellScope(RulesetEffectSpell previous) : IDisposable
    { public void Dispose() => activeSpell = previous; }
    internal static IDisposable EnterSpell(RulesetEffectSpell effect)
    { if (!RuntimeEnabled) return null; var before = activeSpell; activeSpell = effect; return new SpellScope(before); }
    internal static void CaptureInstalledTables()
    {
        if (tables != null) return;
        try { CaptureCore(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        { Main.Error("Pact snapshot unavailable; mixed admission remains closed: " + ex.Message); }
    }
    private static void CaptureCore()
    {
        var casting = DatabaseHelper.FeatureDefinitionCastSpells.CastSpellWarlock;
        if (casting.SlotsRecharge != RuleDefinitions.RechargeRate.ShortRest) throw new InvalidOperationException("Pact slots must recharge on short rest.");
        var grants = DatabaseHelper.CharacterClassDefinitions.Warlock.FeatureUnlocks.SelectMany(u =>
            CompressedProgressionDefinitions.Flatten(u.FeatureDefinition).OfType<FeatureDefinitionPointPool>()
                .Where(p => p.ExtraSpellsTag == "MysticArcanum").Select(p =>
                {
                    if (p.PoolAmount != 1 || p.MinSpellLevel != p.MaxSpellLevel) throw new InvalidOperationException("Unsupported native Arcanum pool.");
                    var list = p.SpellListOverride ?? casting.SpellListDefinition;
                    return new Plan.Arcanum(p.Name + ":" + u.Level, u.Level, p.MinSpellLevel,
                        list.SpellsByLevel.SelectMany(r => r.Spells).Where(s => s.SpellLevel == p.MinSpellLevel).Select(s => s.Name).Distinct());
                })).ToArray();
        tables = new Plan.Tables(casting.SlotsPerLevels.Select(r => r.Slots), grants);
    }
    internal static void EnsureCanBind(RulesetCharacterHero hero)
    {
        if (!RuntimeEnabled || tables == null || hero == null || hero.ClassesAndLevels.Values.Sum() != 0 || Owners.TryGetValue(hero, out _))
            throw new InvalidOperationException("Pact binding requires captured tables and an unbound new hero.");
    }
    internal static void BindNewHero(RulesetCharacterHero hero, StandardCasterResourcePlan.Budget standard,
        CompressedMulticasterProgressionPlan.Result progression)
    {
        EnsureCanBind(hero);
        Owners.Add(hero, new Plan.CastingLedger(standard, new Plan.Budget(new Plan.Allocation(tables, progression))));
    }
    private static RulesetCharacterHero Owner(RulesetCharacter actor) => actor as RulesetCharacterHero ?? DruidWildshapeContext.StandardResourceOwner(actor);
    private static bool TryLedger(RulesetCharacter actor, out Plan.CastingLedger ledger)
    {
        ledger = null;
        if (!RuntimeEnabled) return false;
        var hero = Owner(actor);
        if (hero == null || !Owners.TryGetValue(hero, out ledger)) return false;
        // Reuse actual class/history/settings validation of the authoritative standard owner.
        StandardMulticasterResourcesContext.TryCasterLevel(hero, out _);
        return ledger.Pact.Current.EffectiveWarlockLevel > 0;
    }
    internal static void Synchronize(RulesetCharacterHero hero, CompressedMulticasterProgressionPlan.Result progression)
    {
        if (!RuntimeEnabled || !Owners.TryGetValue(hero, out var ledger)) throw new InvalidOperationException("Unbound Pact owner.");
        var extra = hero.FeaturesByType<FeatureDefinitionMagicAffinity>()
            .Where(f => f == DatabaseHelper.FeatureDefinitionMagicAffinitys.MagicAffinityChitinousBoonAdditionalSpellSlot)
            .SelectMany(f => f.AdditionalSlots).Sum(s => s.SlotsNumber);
        var allocation = new Plan.Allocation(tables, progression, extra);
        ledger.Pact.Refresh(allocation);
        if (allocation.EffectiveWarlockLevel == 0) return;
        var repertoire = SharedSpellsContext.GetWarlockSpellRepertoire(hero) ?? throw new InvalidOperationException("Missing owning Warlock repertoire.");
        if (repertoire.SpellCastingFeature != DatabaseHelper.FeatureDefinitionCastSpells.CastSpellWarlock) throw new InvalidOperationException("Unknown Pact casting feature.");
        if (repertoire.ExtraSpellsByTag.TryGetValue("MysticArcanum", out var arcana))
            foreach (var group in arcana.Distinct().GroupBy(s => s.SpellLevel))
            {
                if (group.Count() != 1) throw new InvalidOperationException("Ambiguous Arcanum selection; automatic conversion is unsupported.");
                ledger.Pact.SelectArcanum(group.Key, group.Single().Name);
            }
    }
    internal static bool TryNumbers(RulesetCharacter actor, out int circle, out int maximum, out int used)
    {
        circle = maximum = used = 0;
        if (!TryLedger(actor, out var ledger)) return false;
        circle = ledger.Pact.Current.Circle; maximum = ledger.Pact.Current.Slots; used = maximum - ledger.Pact.Remaining; return true;
    }
    internal static bool CanSpend(RulesetCharacter actor, int circle, out bool available)
    {
        available = false;
        if (!TryLedger(actor, out var ledger)) return false;
        available = ledger.Standard.CanSpend(circle) || ledger.Pact.Current.Circle == circle && ledger.Pact.Remaining > 0; return true;
    }
    private static bool ClassRepertoire(RulesetSpellRepertoire repertoire) => repertoire.SpellCastingFeature.SpellCastingOrigin is CastingOrigin.Class or CastingOrigin.Subclass;
    private static bool ArcanumOnly(RulesetSpellRepertoire rep, SpellDefinition spell) => rep.SpellCastingClass == DatabaseHelper.CharacterClassDefinitions.Warlock &&
        rep.ExtraSpellsByTag.TryGetValue("MysticArcanum", out var spells) && spells.Contains(spell) && !rep.KnownSpells.Contains(spell) && !rep.AutoPreparedSpells.Contains(spell) &&
        !rep.ExtraSpellsByTag.Where(p => p.Key != "MysticArcanum").Any(p => p.Value.Contains(spell));
    internal static bool Spend(RulesetSpellRepertoire repertoire, int circle, bool arcanum = false)
    {
        if (!RuntimeEnabled || !ClassRepertoire(repertoire) || !TryLedger(repertoire.GetCaster(), out var ledger)) return false;
        if (circle == 0) return true;
        var effect = activeSpell;
        if (effect == null || effect.Guid == 0 || effect.SpellRepertoire != repertoire)
            throw new InvalidOperationException("Pact prototype: resource use requires a scoped spell effect. Unclassified conversion/action is blocked.");
        var spell = effect.SpellDefinition;
        var onlyArcanum = ArcanumOnly(repertoire, spell);
        var hero = Owner(repertoire.GetCaster());
        var classId = repertoire.SpellCastingClass?.Name ?? throw new InvalidOperationException("Unknown spell class owner.");
        var arcanumCast = arcanum || onlyArcanum;
        // Arcanum access is not the ordinary class-circle5 boundary. Require BOTH the actual
        // native grant and the frozen selection; used charges are checked by the resource ledger.
        var authorized = arcanumCast
            ? repertoire.ExtraSpellsByTag.TryGetValue("MysticArcanum", out var granted) && granted.Contains(spell) &&
              ledger.Pact.Selected.TryGetValue(spell.SpellLevel, out var selected) && selected == spell.Name
            : repertoire.HasKnowledgeOfSpell(spell);
        var request = new Plan.Cast("spell:" + effect.Guid, classId, repertoire.SpellCastingFeature.Name, spell.Name, spell.SpellLevel, circle,
            repertoire.SpellCastingAbility, repertoire.SaveDC, authorized,
            arcanumCast ? Plan.Source.Arcanum : Plan.Source.Automatic, onlyArcanum);
        var location = GameLocationCharacter.GetFromActor(hero);
        var decision = ledger.Spend(request, Main.Settings.AlwaysSpendPactSlotsFirst, location != null && location.GetAndClearShiftState());
        if (!decision.Allowed) throw new InvalidOperationException("Pact prototype: " + decision.Error + "; requested circle=" + circle + "; Pact circle=" + ledger.Pact.Current.Circle);
        StandardMulticasterResourcesContext.Refresh(hero); return true;
    }
    internal static void Project(RulesetCharacter actor, RulesetSpellRepertoire repertoire, Dictionary<int, int> capacities, Dictionary<int, int> used)
    {
        if (!TryLedger(actor, out var ledger) || !ClassRepertoire(repertoire)) return;
        var circle = ledger.Pact.Current.Circle;
        capacities.TryGetValue(circle, out var standardMaximum); used.TryGetValue(circle, out var standardUsed);
        capacities[circle] = standardMaximum + ledger.Pact.Current.Slots;
        used[circle] = standardUsed + ledger.Pact.Current.Slots - ledger.Pact.Remaining;
        used[SharedSpellsContext.PactMagicSlotsTab] = ledger.Pact.Current.Slots - ledger.Pact.Remaining; // display sentinel only, never a resource/table index
        if (repertoire.SpellCastingClass != DatabaseHelper.CharacterClassDefinitions.Warlock) return;
        repertoire.usedMysticArcanum.Clear();
        foreach (var arcanum in ledger.Pact.Current.Arcana.Where(a => ledger.Pact.ArcanumUsed(a.Circle))) repertoire.usedMysticArcanum[arcanum.Circle] = 1;
    }
    internal static bool DisplaySlots(RulesetCharacter actor, RulesetSpellRepertoire repertoire, SpellDefinition spell, int circle, out int remaining, out int maximum)
    {
        remaining = maximum = 0;
        if (!TryLedger(actor, out var ledger) || !ClassRepertoire(repertoire)) return false;
        if (spell != null && ArcanumOnly(repertoire, spell))
        { maximum = circle == spell.SpellLevel ? 1 : 0; remaining = maximum == 1 && ledger.Pact.CanArcanum(circle, spell.Name) ? 1 : 0; return true; }
        maximum = circle is >= 1 and <= 9 ? ledger.Standard.Current.Slots[circle - 1] : 0;
        remaining = circle is >= 1 and <= 9 ? ledger.Standard.RemainingSlots(circle) : 0;
        if (circle == ledger.Pact.Current.Circle) { maximum += ledger.Pact.Current.Slots; remaining += ledger.Pact.Remaining; }
        return true;
    }
    internal static bool UpcastLevels(RulesetSpellRepertoire repertoire, SpellDefinition spell, List<int> levels, out bool available)
    {
        available = false;
        if (!RuntimeEnabled) return false;
        if (!TryLedger(repertoire.GetCaster(), out var ledger) || !ClassRepertoire(repertoire)) return false;
        levels?.Clear();
        if (ArcanumOnly(repertoire, spell)) return true; // selected Arcanum is fixed at its own circle
        for (var circle = Math.Max(1, spell.SpellLevel + 1); circle <= 9; circle++)
            if (ledger.Standard.CanSpend(circle) || ledger.Pact.Current.Circle == circle && ledger.Pact.Remaining > 0)
            { available = true; levels?.Add(circle); }
        return true;
    }
    internal static bool Lowest(RulesetSpellRepertoire repertoire, out int circle)
    {
        circle = 0;
        if (!RuntimeEnabled) return false;
        if (!TryLedger(repertoire.GetCaster(), out var ledger) || !ClassRepertoire(repertoire)) return false;
        if (ledger.Pact.Remaining > 0 && (Main.Settings.AlwaysSpendPactSlotsFirst || repertoire.SpellCastingClass == DatabaseHelper.CharacterClassDefinitions.Warlock))
            circle = ledger.Pact.Current.Circle;
        else circle = Enumerable.Range(1, 9).FirstOrDefault(c => ledger.Standard.CanSpend(c));
        if (circle == 0 && ledger.Pact.Remaining > 0) circle = ledger.Pact.Current.Circle;
        return true;
    }
    internal static void Rest(RulesetCharacter actor, string operation, bool longRest, bool simulate)
    { if (TryLedger(actor, out var ledger)) ledger.Pact.Rest(operation, longRest, simulate); }
    internal static bool RecoverPower(RulesetCharacterHero hero, BaseDefinition source, ulong effectGuid)
    {
        if (!TryLedger(hero, out var ledger)) return false;
        var cunning = source?.Name == "PowerWarlockMagicalCunning";
        var master = source == Level20Context.PowerWarlockEldritchMaster;
        if (!cunning && !master) return false;
        if (effectGuid == 0 || source is not FeatureDefinitionPower power || !hero.HasAnyFeature(power) ||
            cunning && (!Main.Settings.EnableWarlockMagicalCunningAndImprovedEldritchMaster2024 || ledger.Pact.Current.EffectiveWarlockLevel < 2) ||
            master && ledger.Pact.Current.EffectiveWarlockLevel < 20)
            throw new InvalidOperationException("Pact recovery requires its actual owned power, original milestone and action identity.");
        if (master || ledger.Pact.Current.EffectiveWarlockLevel == 20) ledger.Pact.RecoverAll("power:" + effectGuid);
        else ledger.Pact.Recover("power:" + effectGuid, (ledger.Pact.Current.Slots + 1) / 2);
        StandardMulticasterResourcesContext.Refresh(hero); return true;
    }
    internal static bool BlockConversion(RulesetCharacter actor)
    {
        if (!TryLedger(actor, out _)) return false;
        Main.Error("Pact prototype: slot-to-Sorcery-Points conversion is blocked until a typed native conversion journal is available; neither budget was changed."); return true;
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.CastSpell))]
    internal static class CastScopePatch
    {
        internal static void Prefix(RulesetEffectSpell __0, out IDisposable __state) => __state = EnterSpell(__0);
        internal static Exception Finalizer(Exception __exception, IDisposable __state) { __state?.Dispose(); return __exception; }
    }
    [HarmonyPatch(typeof(RulesetSpellRepertoire), nameof(RulesetSpellRepertoire.SpendMysticArcanum))]
    internal static class ArcanumPatch
    {
        internal static bool Prefix(RulesetSpellRepertoire __instance, int __0) => !Spend(__instance, __0, true);
    }
    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.CreateSorceryPoints))]
    internal static class ConversionPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(RulesetCharacter __instance) => !BlockConversion(__instance);
    }
}
