#if SOLASTA_FLEXIBLE_CASTING_QA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api.GameExtensions;

namespace SolastaUnfinishedBusiness.Models;

// Compiled only in the labelled diagnostic QA artifact. No admission, resource binding, XP
// mutation or legacy native conversion dispatch is reachable from this diagnostic entry.
internal static class FlexibleCastingQaContext
{
    private sealed class FreshObservation;
    private static readonly ConditionalWeakTable<RulesetCharacterHero, FreshObservation> Fresh = new();
    private static WeakReference<RulesetCharacterHero> lastCreated;
    internal static string LastRejection { get; private set; } = "Last conversion: NONE; source identity: NONE; code=QA_ADMISSION_BLOCKED; no command issued.";
    internal static bool Ended { get; private set; }
    internal static string SourceCommit => typeof(FlexibleCastingQaContext).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "FlexibleCastingQaSourceCommit").Value;
    internal static string ReportPath => Path.Combine(Main.ModFolder, "QaDiagnostics", "FlexibleCasting-QA-latest.txt");
    internal static RulesetCharacterHero[] Heroes()
    {
        var heroes = new List<RulesetCharacterHero>();
        if (Gui.GameCampaign && Gui.GameCampaign.Party != null)
            heroes.AddRange(Gui.GameCampaign.Party.CharactersList.Select(c => c.RulesetCharacter).OfType<RulesetCharacterHero>());
        if (lastCreated != null && lastCreated.TryGetTarget(out var fresh) && !heroes.Contains(fresh)) heroes.Add(fresh);
        return heroes.ToArray();
    }
    private static bool Local => !Global.IsSettingUpMultiplayer && ServiceRepository.GetService<INetworkingService>()?.IsMultiplayerGame == false &&
        ServiceRepository.GetService<IMagicCommandService>() is LocalCommandManager;
    internal static FlexibleCastingQaPlan.Code Admission()
    {
        var party = Gui.GameCampaign ? Gui.GameCampaign.Party?.CharactersList : null;
        return FlexibleCastingQaPlan.Check(true, Ended, Local, party?.Select(c => c.RulesetCharacter).OfType<RulesetCharacterHero>()
            .Select(h => new FlexibleCastingQaPlan.Hero(h, Fresh.TryGetValue(h, out _), false,
                h.ClassesAndLevels.Values.Sum(), h.ClassesAndLevels.Select(c => new KeyValuePair<string, int>(c.Key.Name, c.Value)),
                h.ClassesHistory.Select(c => c.Name))));
    }
    internal static void End() { Ended = true; lastCreated = null; }
    private static void RejectConversion(string entry, RulesetCharacter actor)
    {
        LastRejection = "Last conversion: REJECTED before QA debit; entry=" + entry + "; hero=" + actor?.Guid +
            "; source identity=NONE (not issued); code=QA_ADMISSION_BLOCKED; no QA parent completion/refund.";
        Main.Error(LastRejection);
    }
    internal static void CheckXp(RulesetCharacterHero hero)
    {
        // No API caller can mistake a preview or a new-character observation for admission.
        // Do not call GrantExperience until ALL members are bound and native advancement works.
        throw new InvalidOperationException(FlexibleCastingQaPlan.Reason(Admission()));
    }
    internal static string Snapshot(RulesetCharacterHero hero)
    {
        var b = new StringBuilder();
        b.AppendLine("UB QA " + FlexibleCastingQaPlan.BuildId).AppendLine("Source commit: " + SourceCommit)
            .AppendLine("UTC: " + DateTime.UtcNow.ToString("O")).AppendLine("MixedGameplayReady=false; ADMITTED=false; NO NATIVE QA CONVERSION ENABLED")
            .AppendLine("Admission code: " + Admission()).AppendLine(FlexibleCastingQaPlan.Reason(Admission()))
            .AppendLine("Observed level0 heroes are NOT bound; the global resource path remains disabled.");
        if (hero == null) return b.AppendLine("Selected original hero: none").ToString();
        b.AppendLine("Selected original hero: " + hero.Name + " " + hero.SurName + "; GUID=" + hero.Guid)
            .AppendLine("Exact current object observed at level0: " + Fresh.TryGetValue(hero, out _))
            .AppendLine("Character level attribute: " + hero.TryGetAttributeValue(AttributeDefinitions.CharacterLevel))
            .AppendLine("Actual classes: " + string.Join(", ", hero.ClassesAndLevels.Select(c => c.Key.Name + "=" + c.Value)))
            .AppendLine("Class history: " + string.Join(" -> ", hero.ClassesHistory.Select(c => c.Name)));
        var level = hero.ClassesAndLevels.Values.Sum();
        var xp = hero.TryGetAttributeValue(AttributeDefinitions.Experience);
        b.AppendLine("XP: " + xp + "; actual total level=" + level);
        if (level is >= 1 and < 20 && RuleDefinitions.ExperienceThresholds.Length > level)
            b.AppendLine("Next XP threshold (installed): " + RuleDefinitions.ExperienceThresholds[level]);
        else b.AppendLine("Next XP threshold: unavailable / cap20");
        b.AppendLine("XP multiplier setting: " + Main.Settings.MultiplyTheExperienceGainedBy)
            .AppendLine("UseAlternateSpellPointsSystem=" + Main.Settings.UseAlternateSpellPointsSystem)
            .AppendLine("AlwaysSpendPactSlotsFirst=" + Main.Settings.AlwaysSpendPactSlotsFirst)
            .AppendLine("Native Sorcery maximum=" + hero.TryGetAttributeValue(AttributeDefinitions.SorceryPoints) + "; used=" + hero.UsedSorceryPoints +
                "; remaining=" + (hero.TryGetAttributeValue(AttributeDefinitions.SorceryPoints) - hero.UsedSorceryPoints));
        if (hero.ClassesAndLevels.Any(c => c.Key.Name == "Warlock" && c.Value is >= 1 and <= 12))
            b.AppendLine("Legacy computed Pact circle=" + SharedSpellsContext.GetWarlockSpellLevel(hero) + "; maximum=" + SharedSpellsContext.GetWarlockMaxSlots(hero) +
                "; used=" + SharedSpellsContext.GetWarlockUsedSlots(hero) + " (not an admitted multicaster budget)");
        var points = hero.UsablePowers.Where(p => p.PowerDefinition == SpellPointsContext.PowerSpellPoints).ToArray();
        b.AppendLine("Native PowerSpellPoints count=" + points.Length);
        foreach (var p in points) b.AppendLine("SP pool remaining=" + p.RemainingUses + "; maximum=" + hero.GetMaxUsesOfPower(p));
        b.AppendLine("Native repertoire views below are diagnostics, NOT a newly imported shared owner:");
        foreach (var rep in hero.SpellRepertoires)
        {
            b.AppendLine("Repertoire: " + rep.SpellCastingFeature.Name + "; ability=" + rep.SpellCastingAbility +
                "; class=" + rep.SpellCastingClass?.Name + "; subclass=" + rep.SpellCastingSubclass?.Name)
                .AppendLine("  slots=" + string.Join(",", rep.spellsSlotCapacities.OrderBy(c => c.Key).Select(c => c.Key + ":" + c.Value)))
                .AppendLine("  used (Pact sentinel=-1)=" + string.Join(",", rep.usedSpellsSlots.OrderBy(c => c.Key).Select(c => c.Key + ":" + c.Value)))
                .AppendLine("  known=" + string.Join(",", rep.KnownSpells.Select(s => s.Name)))
                .AppendLine("  cantrips=" + string.Join(",", rep.KnownCantrips.Select(s => s.Name)));
        }
        b.AppendLine(FlexibleCastingPreflightContext.QaCommandSnapshot(hero));
        return b.ToString();
    }
    internal static void SaveReport(RulesetCharacterHero hero)
    {
        var report = Snapshot(hero);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.CreateNewCharacter))]
    private static class ObserveCreationPatch
    {
        private static void Postfix(CharacterBuildingManager __instance)
        {
            if (Ended) return;
            var hero = __instance.CurrentLocalHeroCharacter;
            if (hero == null || hero.ClassesAndLevels.Count != 0 || hero.ClassesHistory.Count != 0 ||
                hero.TryGetAttributeValue(AttributeDefinitions.CharacterLevel) != 0) return;
            if (!Fresh.TryGetValue(hero, out _)) Fresh.Add(hero, new FreshObservation());
            lastCreated = new WeakReference<RulesetCharacterHero>(hero);
        }
    }
    // Closed diagnostic build must not silently fall through to the identity-less vanilla path.
    // Only these two exact installed powers and this specific modal callback are intercepted.
    [HarmonyPatch(typeof(GameCampaignCharacter), nameof(GameCampaignCharacter.UsePower))]
    private static class RejectCampaignConversionPatch
    {
        [HarmonyPriority(Priority.First + 100)]
        private static bool Prefix(GameCampaignCharacter __instance, RulesetUsablePower __0)
        {
            if (!new[] { false, true }.Any(create => __0?.PowerDefinition ==
                    Api.DatabaseHelper.GetDefinition<FeatureDefinitionPower>(SpellResourceConsumerCatalog.FlexibleCasting(create).Definition))) return true;
            RejectConversion("campaign-power", __instance.RulesetCharacter);
            return false;
        }
    }
    [HarmonyPatch(typeof(FlexibleCastingModal), nameof(FlexibleCastingModal.OnConvertCb))]
    private static class RejectModalConversionPatch
    {
        [HarmonyPriority(Priority.First + 100)]
        private static bool Prefix(FlexibleCastingModal __instance)
        {
            RejectConversion("modal-convert", __instance.caster);
            return false; // no guessed source, native debit, condition, refund or parent acknowledgement
        }
    }
    [HarmonyPatch(typeof(LocalCommandManager), nameof(LocalCommandManager.CreateSpellSlot))]
    private static class RejectIdentitylessSlotPatch
    {
        private static void Prefix() => throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.PendingAtomicJournal,
            "QA_ADMISSION_BLOCKED: identity-less local/network slot creation unavailable");
    }
    [HarmonyPatch(typeof(LocalCommandManager), nameof(LocalCommandManager.CreateSorceryPoints))]
    private static class RejectIdentitylessSorceryPatch
    {
        private static void Prefix() => throw new SpellResourceOperationPlan.Rejection(SpellResourceOperationPlan.Code.PendingAtomicJournal,
            "QA_ADMISSION_BLOCKED: identity-less local/network Sorcery conversion unavailable");
    }
    [HarmonyPatch(typeof(ToolsContext.FunctorRespec), nameof(ToolsContext.FunctorRespec.Execute))]
    private static class RejectRespecPatch
    {
        private static bool Prefix(ref System.Collections.IEnumerator __result)
        {
            LastRejection = "QA_ENTRY_BLOCKED: Respec is unsupported; no hero replacement or migration performed.";
            Main.Error(LastRejection);
            __result = Array.Empty<object>().GetEnumerator();
            return false;
        }
    }
}
internal static partial class FlexibleCastingPreflightContext
{
    internal static string QaCommandSnapshot(RulesetCharacterHero hero)
    {
        var rows = Origins.Values.Where(o => ReferenceEquals(o.Hero, hero)).ToArray();
        if (rows.Length == 0) return FlexibleCastingQaContext.LastRejection;
        return string.Join(Environment.NewLine, rows.Select(o => "Source=" + o.Source.Id + "; command=" + o.Authority.Command + "; state=" + o.Source.State));
    }
}
#endif
