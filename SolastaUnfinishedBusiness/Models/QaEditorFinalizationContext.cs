#if SOLASTA_FLEXIBLE_CASTING_QA
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using M = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using Q = SolastaUnfinishedBusiness.Models.QaEditorFinalizationPlan;

namespace SolastaUnfinishedBusiness.Models;

internal static class QaEditorFinalizationContext
{
    private sealed class Candidate(RulesetCharacterHero hero, CharacterBuildingManager manager, string rules)
    {
        internal readonly RulesetCharacterHero Hero = hero;
        internal readonly CharacterBuildingManager Manager = manager;
        internal readonly Q Journal = new(hero, manager, rules, () => "qa-editor:" + Guid.NewGuid().ToString("N"));
        internal readonly string Rules = rules;
        internal readonly Dictionary<string, CharacterHeroBuildingData> DataWitness = new(StringComparer.Ordinal);
        internal bool Binding;
        internal string Failure;
    }
    private static readonly ConditionalWeakTable<RulesetCharacterHero, Candidate> Candidates = new();
    private static Candidate held; // exactly one disposable editor candidate; no GUID/name lookup
    private static bool armed;
    [ThreadStatic] private static Candidate executing;
    private static string Rules() => string.Join("|", typeof(Settings).GetProperties()
        .Where(p => p.PropertyType == typeof(bool) || p.Name is "MaxAllowedClasses" or "TotalFeatsGrantedFirstLevel" or "MultiplyTheExperienceGainedBy")
        .OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.Name + "=" + p.GetValue(Main.Settings)));
    private static void RequireEditor(CharacterBuildingManager manager, RulesetCharacterHero hero, bool current = true)
    {
        FlexibleCastingPreflightContext.QaRequireMainThread();
        if (FlexibleCastingQaContext.Ended || Gui.Game || Gui.GameCampaign || Global.IsSettingUpMultiplayer ||
            ServiceRepository.GetService<INetworkingService>()?.IsMultiplayerGame != false ||
            ServiceRepository.GetService<IHeroBuildingCommandService>() is not LocalCommandManager ||
            !ReferenceEquals(ServiceRepository.GetService<ICharacterBuildingService>(), manager) || current && !ReferenceEquals(manager.CurrentLocalHeroCharacter, hero))
            throw new InvalidOperationException("C.2 requires the exact local editor object, no active game/campaign/network");
        if (!current && hero != null && manager.CurrentLocalHeroCharacter != null && !ReferenceEquals(manager.CurrentLocalHeroCharacter, hero))
            throw new InvalidOperationException("Another native editor draft is active");
        if (!Main.Settings.EnableLevel20 || !Main.Settings.EnableMulticlass || !Main.Settings.EnableSorcererCompressedProgressionPrototype || !SorcererProgressionContext.Active)
            throw new InvalidOperationException("EnableLevel20, EnableMulticlass and restarted Sorcerer compression are required; no setting changed automatically");
    }
    internal static void Arm()
    {
        var manager = ServiceRepository.GetService<ICharacterBuildingService>() as CharacterBuildingManager;
        RequireEditor(manager, null, false);
        if (manager == null || held != null || manager.CurrentLocalHeroCharacter != null) throw new InvalidOperationException("Arm from an idle editor; restart before another candidate");
        armed = true;
    }
    internal static void RequireCandidate(RulesetCharacterHero hero)
    {
        if (hero == null || !Candidates.TryGetValue(hero, out var c) || !ReferenceEquals(held, c) || c.Failure != null)
            throw new InvalidOperationException("Unknown/failed exact level0 QA candidate");
        RequireEditor(c.Manager, hero);
        if (Rules() != c.Rules) throw new InvalidOperationException("QA settings changed; new process required");
    }
    internal static void RequireZeroAuthority(RulesetCharacterHero hero)
    {
        RequireCandidate(hero);
        if (!held.Binding || hero.ClassesAndLevels.Count != 0 || hero.ClassesHistory.Count != 0 || hero.SpellRepertoires.Count != 0)
            throw new InvalidOperationException("Level0 binding is allowed only inside the trusted CreateNewCharacter postfix");
    }
    private static C.Result Calculate(Candidate c, int actual)
    {
        var classes = actual == 0 ? Array.Empty<C.ClassInput>() : new[] { new C.ClassInput("Sorcerer", actual, true,
            c.Hero.ClassesAndSubclasses.FirstOrDefault(p => p.Key.Name == "Sorcerer").Value?.Name) };
        var result = MultipleSpellRepertoiresContext.QaEditorCalculator().Calculate(new C.Snapshot(actual, classes, Enumerable.Repeat("Sorcerer", actual)));
        if (!result.IsValid) throw new InvalidOperationException("Installed Sorcerer progression/subclass snapshot rejected");
        return result;
    }
    private sealed class Host(Candidate candidate) : Q.IHost
    {
        private RulesetCharacterHero Hero => candidate.Hero;
        public object Owner => Hero;
        public object Manager => candidate.Manager;
        public string Rules => QaEditorFinalizationContext.Rules();
        public int Actual
        {
            get
            {
                var actual = Hero.ClassesHistory.Count;
                if (Hero.ClassesAndLevels.Values.Sum() != actual || Hero.ClassesHistory.Any(c => c.Name != "Sorcerer") ||
                    Hero.ClassesAndLevels.Any(c => c.Key.Name != "Sorcerer" || c.Value != actual))
                    throw new InvalidOperationException("Foreign/changed actual class history");
                return actual;
            }
        }
        private CharacterHeroBuildingData Data => Hero.GetHeroBuildingData() ?? throw new InvalidOperationException("Native editor draft missing");
        private static string SpellTags(Dictionary<string, List<SpellDefinition>> tags) => string.Join(";", tags.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => p.Key + "=" + string.Join(",", p.Value.Select(s => s.Name))));
        private static string Definitions<T>(Dictionary<string, List<T>> tags) where T : BaseDefinition => string.Join(";",
            tags.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + string.Join(",", p.Value.Select(d => d.Name))));
        public string IntentFingerprint => Actual + "/" + RuntimeHelpers.GetHashCode(Data) + "/" + Data.LevelingUp + "/" +
            string.Join(",", Hero.ClassesAndSubclasses.Select(p => p.Key.Name + ":" + p.Value.Name)) + "/" +
            SpellTags(Data.AcquiredSpells) + "/" + SpellTags(Data.AcquiredCantrips) + "/" + SpellTags(Data.UnlearnedSpells) + "/" +
            Definitions(Data.LevelupTrainedFeats) + "/" + Definitions(Data.LevelupTrainedSkills) + "/" + Definitions(Data.LevelupTrainedToolTypes) + "/" +
            Definitions(Data.LevelupTrainedLanguages) + "/" + Definitions(Data.LevelupTrainedMetamagicOptions) + "/" +
            string.Join(";", Data.LevelupTrainedExpertises.OrderBy(p => p.Key).Select(p => p.Key + "=" + string.Join(",", p.Value))) + "/" +
            Hero.RaceDefinition?.Name + "/" + Hero.BackgroundDefinition?.Name + "/" +
            string.Join(";", Data.PointPoolStacks.OrderBy(p => p.Key).SelectMany(s => s.Value.ActivePools.OrderBy(p => p.Key)
                .Select(p => s.Key + ":" + p.Key + ":" + p.Value.RemainingPoints + ":" + p.Value.MaxPoints)));
        public void RequireEditor() => RequireCandidate(Hero);
        public void Log(Exception error) => Main.Error("QA editor finalization " + candidate.Journal.Current?.Id + ": " + error);
        public Q.Prepared Prepare(Q.Ticket ticket)
        {
            var data = Data;
            if (!candidate.DataWitness.TryGetValue(ticket.Id, out var issuedData) || !ReferenceEquals(data, issuedData))
                throw new InvalidOperationException("Native building data differs from the issued assignment entry");
            var ledger = PactMagicMulticasterContext.QaEditorLedger(Hero);
            if (ledger.Standard.Current.CasterLevel != candidate.Journal.ConfirmedActual || ledger.Pact.Used != 0 ||
                ledger.Standard.RemainingPoints != ledger.Standard.Current.PointMaximum ||
                Enumerable.Range(1, 9).Any(circle => ledger.Standard.RemainingSlots(circle) != ledger.Standard.Current.Slots[circle - 1]))
                throw new InvalidOperationException("Level0 owner has stale/spent resources before native grants");
            if (Main.Settings.UseAlternateSpellPointsSystem)
            {
                var pools = Hero.UsablePowers.Where(p => p.PowerDefinition == SpellPointsContext.PowerSpellPoints).ToArray();
                if (candidate.Journal.ConfirmedActual == 0 ? pools.Length != 0 : pools.Length != 1 || pools[0].RemainingUses != ledger.Standard.RemainingPoints)
                    throw new InvalidOperationException("Native SP owner/pool is stale or spent before native grants");
            }
            if (!Hero.RaceDefinition || !Hero.BackgroundDefinition) throw new InvalidOperationException("Incomplete native race/background draft");
            var roots = Hero.RaceDefinition.FeatureUnlocks.Select(u => u.FeatureDefinition)
                .Concat(Hero.SubRaceDefinition ? Hero.SubRaceDefinition.FeatureUnlocks.Select(u => u.FeatureDefinition) : Array.Empty<FeatureDefinition>())
                .Concat(Hero.BackgroundDefinition.Features)
                .Concat(Data.LevelupTrainedFeats.Values.SelectMany(v => v).Concat(Hero.TrainedFeats).SelectMany(f => f.Features));
            if (roots.SelectMany(CompressedProgressionDefinitions.Flatten).Any(f => f is FeatureDefinitionCastSpell or FeatureDefinitionAutoPreparedSpells or FeatureDefinitionGrantInvocations ||
                    f is FeatureDefinitionPointPool p && (p.PoolType.ToString().Contains("Spell") || p.PoolType.ToString().Contains("Cantrip") || p.PoolType.ToString().Contains("Invocation")) ||
                    f is FeatureDefinitionMagicAffinity affinity && (affinity.AdditionalSlots.Count != 0 || affinity.PreparedSpellModifier != RuleDefinitions.PreparedSpellsModifier.None)))
                throw new InvalidOperationException("C.2 does not finalize race/background/feat spell sources; select a noncasting root");
            foreach (var feat in Data.LevelupTrainedFeats.Values.SelectMany(v => v).OfType<FeatDefinitionWithPrerequisites>())
                if (!feat.Validate(feat, Hero).result) throw new InvalidOperationException("Native pending feat prerequisite failed");
            if (Data.HeroCharacter != Hero || LevelUpHelper.GetSelectedClass(Hero) != DatabaseHelper.CharacterClassDefinitions.Sorcerer ||
                Data.LevelingUp != (ticket.Target == 2) || Hero.UsedSorceryPoints != 0 || Hero.AllConditions.Any() ||
                Hero.SpellRepertoires.Any(r => r.SpellCastingFeature != DatabaseHelper.FeatureDefinitionCastSpells.CastSpellSorcerer ||
                    r.SpellCastingClass != DatabaseHelper.CharacterClassDefinitions.Sorcerer || r.usedSpellsSlots.Values.Any(n => n != 0)) ||
                AuxiliarySpellSourcesContext.SnapshotInputs(Hero).Count != 0 || Data.LevelupTrainedInvocations.Values.Any(v => v.Count != 0))
                throw new InvalidOperationException("Unsupported nonfresh/auxiliary casting/invocation/resource draft");
            if (Hero.FeaturesByType<FeatureDefinitionMagicAffinity>().Any(f => f.AdditionalSlots.Count != 0 || f.PreparedSpellModifier != RuleDefinitions.PreparedSpellsModifier.None))
                throw new InvalidOperationException("C.2 excludes additional-slot/preparation modifiers; no native capacity may be dropped");
            if (Data.PointPoolStacks.Any(s => !s.Key.ToString().Contains("Unlearn") && s.Value.ActivePools.Values.Any(p => p.RemainingPoints != 0)))
                throw new InvalidOperationException("Incomplete native mandatory choice pool");
            var tag = AttributeDefinitions.GetClassTag(DatabaseHelper.CharacterClassDefinitions.Sorcerer, ticket.Target);
            foreach (var tags in new[] { Data.AcquiredSpells, Data.AcquiredCantrips, Data.UnlearnedSpells })
                if (tags.Any(p => p.Key != tag && p.Value.Count != 0)) throw new InvalidOperationException("Extra/native spell tag has no C.2 owner adapter");
            string[] Selected(Dictionary<string, List<SpellDefinition>> tags) => tags.TryGetValue(tag, out var spells) ? spells.Select(s => s.Name).ToArray() : Array.Empty<string>();
            var tx = MultipleSpellRepertoiresContext.QaEditorSpellDraft(Hero, ticket.Id, ticket.Target, true);
            tx.Select(new M.Choice(ticket.Target, Selected(Data.AcquiredSpells), Selected(Data.UnlearnedSpells), Selected(Data.AcquiredCantrips)));
            tx.ValidateComplete();
            var expected = tx.Preview().Repertoires["Sorcerer"];
            if (DatabaseHelper.FeatureDefinitionCastSpells.CastSpellSorcerer.spellcastingAbility != expected.Definition.Ability ||
                Hero.SpellRepertoires.Any(r => r.SpellCastingAbility != expected.Definition.Ability))
                throw new InvalidOperationException("Native repertoire ability changed from its captured class owner");
            var fingerprint = IntentFingerprint;
            var before = Hero.ActiveFeatures.Values.SelectMany(v => v).ToArray();
            var priorReps = Hero.SpellRepertoires.ToArray();
            string ExistingNative() => string.Join(";", Hero.SpellRepertoires.Select(r => RuntimeHelpers.GetHashCode(r) + ":" +
                string.Join(",", r.KnownSpells.Select(s => s.Name)) + ":" + string.Join(",", r.KnownCantrips.Select(s => s.Name)) + ":" +
                string.Join(",", r.usedSpellsSlots.Select(p => p.Key + "=" + p.Value)))) + "/" + string.Join(",", Hero.ActiveFeatures.Values.SelectMany(v => v).Select(f => f.Name)) + "/" +
                string.Join(";", Hero.UsablePowers.Where(p => p.PowerDefinition == SpellPointsContext.PowerSpellPoints)
                    .Select(p => RuntimeHelpers.GetHashCode(p) + ":" + p.RemainingUses + ":" + Hero.GetMaxUsesOfPower(p)));
            var nativeBefore = ExistingNative();
            var sorcery = Hero.UsedSorceryPoints;
            var oldXp = Hero.TryGetAttributeValue(AttributeDefinitions.Experience);
            void VerifyNative()
            {
                RequireEditor();
                if (Hero.FeaturesByType<FeatureDefinitionMagicAffinity>().Any(f => f.AdditionalSlots.Count != 0 || f.PreparedSpellModifier != RuleDefinitions.PreparedSpellsModifier.None))
                    throw new InvalidOperationException("Unsupported resource modifier appeared during native finalization");
                if (!ReferenceEquals(Data, data) || Actual != ticket.Target || Hero.TryGetAttributeValue(AttributeDefinitions.CharacterLevel) != ticket.Target ||
                    IntentFingerprint != fingerprint ||
                    Hero.TryGetAttributeValue(AttributeDefinitions.Experience) != oldXp || Hero.UsedSorceryPoints != sorcery ||
                    before.Any(f => !Hero.ActiveFeatures.Values.Any(v => v.Contains(f))) || Hero.SpellRepertoires.Count != 1)
                    throw new InvalidOperationException("Native actual/features/XP/resources differ from expected editor result");
                var rep = Hero.SpellRepertoires.Single();
                if (rep.SpellCastingFeature != DatabaseHelper.FeatureDefinitionCastSpells.CastSpellSorcerer || rep.SpellCastingClass != DatabaseHelper.CharacterClassDefinitions.Sorcerer ||
                    rep.SpellCastingAbility != expected.Definition.Ability || ticket.Target == 2 && !priorReps.Contains(rep) ||
                    !new HashSet<string>(rep.KnownSpells.Select(s => s.Name)).SetEquals(expected.Known) ||
                    !new HashSet<string>(rep.KnownCantrips.Select(s => s.Name)).SetEquals(expected.Cantrips) ||
                    rep.KnownSpells.Count != expected.Known.Count || rep.KnownCantrips.Count != expected.Cantrips.Count ||
                    !new HashSet<string>(rep.AutoPreparedSpells.Select(s => s.Name)).SetEquals(expected.AutoPrepared) ||
                    rep.ExtraSpellsByTag.Values.SelectMany(v => v).Any(s => !expected.AutoPrepared.Contains(s.Name) && !expected.Known.Contains(s.Name)) ||
                    rep.PreparedSpells.Any(s => !expected.Known.Contains(s.Name) && !expected.AutoPrepared.Contains(s.Name)) ||
                    !Hero.HasAnyFeature(DatabaseHelper.FeatureDefinitionCastSpells.CastSpellSorcerer))
                    throw new InvalidOperationException("Native spell/repertoire ownership or selected inventory mismatch");
                if (expected.Definition.PreparedRule != M.Preparation.None &&
                    rep.PreparedSpells.Count(s => !expected.AutoPrepared.Contains(s.Name)) > expected.Definition.PreparedLimit(expected.EffectiveLevel,
                        AttributeDefinitions.ComputeAbilityScoreModifier(Hero.GetAttribute(expected.Definition.Ability).CurrentValue), 0))
                    throw new InvalidOperationException("Native prepared-spell limit does not match class-local rules");
                if (ticket.Target == 2 && (Hero.TryGetAttributeValue(AttributeDefinitions.SorceryPoints) != 2 ||
                    !new[] { false, true }.All(create => Hero.UsablePowers.Any(p => p.PowerDefinition == DatabaseHelper.GetDefinition<FeatureDefinitionPower>(SpellResourceConsumerCatalog.FlexibleCasting(create).Definition)))))
                    throw new InvalidOperationException("Native Sorcerer2 Sorcery/Flexible Casting feature grant missing");
            }
            return new Q.Prepared(fingerprint, tx, Calculate(candidate, ticket.Target), () =>
            {
                if (!ReferenceEquals(Data, data) || IntentFingerprint != fingerprint || Hero.UsedSorceryPoints != sorcery || Hero.TryGetAttributeValue(AttributeDefinitions.Experience) != oldXp ||
                    !Hero.SpellRepertoires.SequenceEqual(priorReps) || ExistingNative() != nativeBefore)
                    throw new InvalidOperationException("Draft/owner/resources changed immediately before native grants");
                SorcererFinalizePreflight.RequireQaDraft(ticket, tx);
            }, VerifyNative);
        }
        public Q.Publication PreparePublication(Q.Ticket ticket) => StandardMulticasterResourcesContext.QaEditorPublication(Hero,
            ticket.Draft.Spells, ticket.Draft.Progression, Hero.SpellRepertoires.Single());
    }
    internal static string Report(RulesetCharacterHero hero)
    {
        if (hero == null || !Candidates.TryGetValue(hero, out var c)) return "C.2 editor candidate: none. Arm next creation explicitly.";
        return "C.2 editor confirmed actual=" + c.Journal.ConfirmedActual + "; command=" + c.Journal.Current?.Id + "; state=" + c.Journal.Current?.State +
            "; failure=" + (c.Failure ?? c.Journal.Current?.Failure ?? "none") + "; PARTY ADMITTED=false; XP/conversions blocked";
    }
    internal static RulesetCharacterHero EditorHero => held?.Hero;
    internal static void OpenLevelTwo(RulesetCharacterHero hero)
    {
        if (hero == null || !Candidates.TryGetValue(hero, out var c)) throw new InvalidOperationException("Exact fresh editor candidate required");
        RequireEditor(c.Manager, hero, false);
        if (c.Journal.ConfirmedActual != 1 || c.Journal.Current?.State != Q.Phase.Committed || Main.Settings.NoExperienceOnLevelUp)
            throw new InvalidOperationException("Only confirmed Sorcerer1 with native force=false is allowed");
        // Mirrors the native CharactersPanel UI handoff, WITHOUT pool LoadCharacter or force XP.
        c.Manager.LevelUpCharacter(hero, false, true);
        var screen = Gui.GuiService.GetScreen<CharacterLevelUpScreen>();
        screen.CurrentHero = hero;
        screen.Show(false);
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.CreateNewCharacter))]
    private static class CreatePatch
    {
        [HarmonyPriority(Priority.Last - 100)]
        private static void Postfix(CharacterBuildingManager __instance)
        {
            if (!armed) return; armed = false;
            var hero = __instance.CurrentLocalHeroCharacter;
            RequireEditor(__instance, hero);
            if (held != null || hero.ClassesHistory.Count != 0) throw new InvalidOperationException("Only exact fresh level0 candidate allowed");
            held = new Candidate(hero, __instance, Rules()); Candidates.Add(hero, held); held.Binding = true;
            try { StandardMulticasterResourcesContext.QaBindEditorZero(hero, Calculate(held, 0)); }
            catch (Exception e) { held.Failure = e.ToString(); throw; }
            finally { held.Binding = false; }
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.AssignClassLevel))]
    private static class AssignPatch
    {
        [HarmonyPriority(Priority.First + 100)]
        private static void Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero, CharacterClassDefinition classDefinition, out Q.Ticket __state)
        {
            __state = null;
            if (Candidates.TryGetValue(hero, out var c))
            {
                if (!ReferenceEquals(__instance, c.Manager)) throw new InvalidOperationException("Wrong native assignment manager");
                var ticket = c.Journal.Issue(new Host(c), classDefinition.Name);
                var data = hero.GetHeroBuildingData();
                if (data == null || c.DataWitness.TryGetValue(ticket.Id, out var old) && !ReferenceEquals(old, data))
                    throw new InvalidOperationException("Assignment retry changed native draft identity");
                c.DataWitness[ticket.Id] = data;
                __state = ticket;
            }
        }
        private static Exception Finalizer(RulesetCharacterHero hero, Q.Ticket __state, bool __runOriginal, Exception __exception)
        {
            if (__state != null && Candidates.TryGetValue(hero, out var c) && (!__runOriginal || __exception != null))
            {
                if (__exception != null) c.Failure = "Native assignment failed: " + __exception;
                c.Journal.Cancel(__state);
            }
            return __exception;
        }
    }
    [HarmonyPatch(typeof(CharacterStageClassSelectionPanel), nameof(CharacterStageClassSelectionPanel.OnBeginShow))]
    private static class ClassPanelPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(CharacterStageClassSelectionPanel __instance)
        {
            if (__instance.currentHero == null || !Candidates.TryGetValue(__instance.currentHero, out _)) return;
            RequireCandidate(__instance.currentHero);
            var sorcerer = DatabaseHelper.CharacterClassDefinitions.Sorcerer;
            if (!__instance.compatibleClasses.Contains(sorcerer)) throw new InvalidOperationException("Native Sorcerer eligibility rejected; QA never overrides prerequisites");
            // Intersect the existing UB/native eligible list; avoid default preview assigning
            // an unrelated class before the user can select the deliberately armed Sorcerer.
            __instance.compatibleClasses.Clear(); __instance.compatibleClasses.Add(sorcerer);
            __instance.selectedClass = 0; // native field is the eligible-list index
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.LevelUpCharacter))]
    private static class LevelUpPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero, bool force)
        {
            if (!Candidates.TryGetValue(hero, out var c)) return;
            if (!ReferenceEquals(__instance, c.Manager)) throw new InvalidOperationException("Wrong native level-up manager");
            RequireEditor(c.Manager, hero, false);
            if (force || c.Journal.ConfirmedActual != 1 || c.Journal.Current?.State != Q.Phase.Committed || c.Failure != null)
                throw new InvalidOperationException("C.2 cannot force XP, import a hero, repeat indeterminate work, or advance beyond Sorcerer2");
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.FinalizeCharacter))]
    private static class FinalizePatch
    {
        [HarmonyPriority(Priority.First + 100)]
        private static bool Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero, bool __1, out Q.Ticket __state)
        {
            __state = null;
            if (!Candidates.TryGetValue(hero, out var c)) return true;
            if (!ReferenceEquals(__instance, c.Manager)) throw new InvalidOperationException("Wrong native finalization manager");
            if (__1) throw new InvalidOperationException("C.2 forbids template/all-level finalization");
            var ticket = c.Journal.Current;
            var run = c.Journal.Begin(new Host(c), ticket);
            __state = ticket; // only after Begin succeeds; a reentrant rejection must not finish the outer operation
            if (run) executing = c;
            return run;
        }
        private static void Tail(RulesetCharacterHero hero)
        {
            if (executing != null && ReferenceEquals(executing.Hero, hero)) executing.Journal.NativeReturned(hero, executing.Journal.Current);
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToArray();
            if (code.Count(i => i.opcode == OpCodes.Ret) != 1 || !code.Any(i => i.Calls(AccessTools.Method(typeof(CharacterBuildingManager), "UpdateCharacter"))))
                throw new InvalidOperationException("C.2 native finalizer return shape changed");
            foreach (var instruction in code)
            {
                if (instruction.opcode == OpCodes.Ret)
                {
                    var load = new CodeInstruction(OpCodes.Ldarg_1); load.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    yield return load; yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FinalizePatch), nameof(Tail)));
                }
                yield return instruction;
            }
        }
        [HarmonyPriority(Priority.Last - 100)]
        private static Exception Finalizer(RulesetCharacterHero hero, Q.Ticket __state, bool __runOriginal, Exception __exception)
        {
            if (__state == null || !Candidates.TryGetValue(hero, out var c)) return __exception;
            try { c.Journal.Finish(new Host(c), __state, __exception, __runOriginal); }
            catch (Exception error) { __exception ??= error; }
            finally { if (ReferenceEquals(executing, c)) executing = null; }
            return __exception;
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.UnassignLastClassLevel))]
    private static class CancelClassPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero)
        {
            if (Candidates.TryGetValue(hero, out var c) && c.Journal.Current != null)
            {
                if (!ReferenceEquals(__instance, c.Manager)) throw new InvalidOperationException("Wrong native cancellation manager");
                if (c.Journal.Current.State == Q.Phase.Committed) throw new InvalidOperationException("Cannot undo a confirmed editor result as a draft");
                c.Journal.Cancel(c.Journal.Current);
            }
            return true; // do not run this side-effecting prefix when UB suppresses preview-only unassignment
        }
    }
    [HarmonyPatch(typeof(CharacterBuildingManager), nameof(CharacterBuildingManager.ReleaseCharacter))]
    private static class ReleasePatch
    {
        private static void Prefix(CharacterBuildingManager __instance, RulesetCharacterHero hero)
        {
            if (hero != null && Candidates.TryGetValue(hero, out var c))
            {
                if (!ReferenceEquals(__instance, c.Manager)) throw new InvalidOperationException("Wrong native release manager");
                if (c.Journal.Current?.State == Q.Phase.Issued) c.Journal.Cancel(c.Journal.Current);
            }
        }
    }
}
internal static partial class FlexibleCastingPreflightContext
{
    internal static void QaRequireMainThread() => RequireUnityMainThread();
}
#endif
