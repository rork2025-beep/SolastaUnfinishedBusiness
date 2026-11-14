using System;
using System.Linq;
using System.Threading;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Behaviors;
using V = SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using F = SolastaUnfinishedBusiness.Models.FlexibleCastingPreflightPlan;
using T = SolastaUnfinishedBusiness.Models.FlexibleCastingSlotToSorceryPlan;
using N = SolastaUnfinishedBusiness.Models.FlexibleCastingNativeCommitPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class FlexibleCastingPreflightContext
{
    private static void RequireLocalSinglePlayer()
    {
        if(Global.IsSettingUpMultiplayer||ServiceRepository.GetService<INetworkingService>()?.IsMultiplayerGame!=false||
            ServiceRepository.GetService<IMagicCommandService>() is not LocalCommandManager)
            throw new O.Rejection(O.Code.PendingAtomicJournal,"Native Flexible Casting requires verified single-player local command services");
    }
    private static void RequireUnityMainThread()
    {
        var context = SynchronizationContext.Current;
        if (context == null || context.GetType().FullName != "UnityEngine.UnitySynchronizationContext" ||
            context.GetType().Assembly != typeof(UnityEngine.Application).Assembly ||
            AccessTools.Field(context.GetType(), "m_MainThreadID")?.GetValue(context) is not int main || main != Thread.CurrentThread.ManagedThreadId)
            throw new O.Rejection(O.Code.Ineligible, "Flexible Casting native commit requires verified Unity main thread");
    }
    private sealed class NativeHost(Origin source, FlexibleCastingModal modal) : N.IHost
    {
        public object Owner => source.Hero;
        public object Repertoire => source.Repertoire;
        public object Effect => source.Effect;
        public object Modal => modal;
        public V.Evidence Evidence() => FlexibleCastingPreflightContext.Evidence(source);
        public F.Live Read() => FlexibleCastingPreflightContext.Read(source);
        public void Log(Exception error) => Main.Error("Flexible Casting committed; notification/UI failure for " + source.Source.Id + ": " + error);
        public void RequireAccess(bool replay)
        {
            if (!StandardMulticasterResourcesContext.RuntimeEnabled) throw new O.Rejection(O.Code.Ineligible, "Closed mixed prototype gate");
            RequireUnityMainThread();
            RequireLocalSinglePlayer();
            var hero = source.Hero; var power = source.Effect.UsablePower;
            if (hero.Guid.ToString() != source.Source.Entry.Hero || SorcererRepertoire(hero) != source.Repertoire ||
                !hero.UsablePowers.Contains(power) || !hero.HasAnyFeature(power.PowerDefinition) || !ReferenceEquals(source.Source.Entry.Power, power.PowerDefinition))
                throw new O.Rejection(O.Code.WrongOwner, "Original hero/Sorcerer/power ownership changed");
            StandardMulticasterResourcesContext.PreflightResources(hero);
            if (Main.Settings.UseAlternateSpellPointsSystem) throw new O.Rejection(O.Code.WrongDomain, "Native Flexible Casting supports standard slots only");
            if (!replay)
            {
                if (source.Source.Kind != V.SourceKind.TrustedCampaignInvocation ||
                    !CampaignParents.TryGetValue(source.Source.Id, out var parent) || !parent.Ended || !parent.ParentHeld || parent.Finished ||
                    !ReferenceEquals(parent.Authority, source.Source) || !ReferenceEquals(parent.Usable, power) || parent.Hero != hero ||
                    ServiceRepository.GetService<IRulesetImplementationService>()?.GetType() != typeof(RulesetImplementationManager) ||
                    ServiceRepository.GetService<IPlayerControllerService>()?.ActivePlayerController?.IsCharacterControlled(hero) != true ||
                    !Modals.TryGetValue(modal, out var origin) || !ReferenceEquals(origin, source) || !modal.Visible || modal.caster != hero || modal.repertoire != source.Repertoire || modal.createSlotMode ||
                    power.PowerDefinition.RechargeRate != RuleDefinitions.RechargeRate.AtWill || power.RemainingUses != power.MaxUses ||
                    power.PowerDefinition is FeatureDefinitionPowerSharedPool || power.PowerDefinition.HasSubFeatureOfType<CountPowerUseInSpecialFeatures>())
                    throw new O.Rejection(O.Code.PendingAtomicJournal, "Native commit supports only the exact held local campaign parent/modal and unmodified AtWill source");
            }
        }
        public N.Publication Prepare(T.Draft draft)
        {
            if (modal.selectedSlotLevel != draft.Intent.Command.NativeCircle) throw new O.Rejection(O.Code.StaleOperation, "Modal circle changed before commit");
            var hero = source.Hero; var power = source.Effect.UsablePower; var parent = CampaignParents[source.Source.Id];
            var views = StandardMulticasterResourcesContext.PrepareFlexibleSlotViews(hero, draft);
            if (!views.Repertoires.Contains(source.Repertoire)) throw new O.Rejection(O.Code.WrongOwner, "Sorcerer view missing from native publication");
            var beforeSorcery = hero.UsedSorceryPoints; var maximum = power.MaxUses; var remaining = power.RemainingUses;
            var repertoire = views.Repertoires.SelectMany(rep => (rep.RepertoireRefreshed?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Cast<RulesetSpellRepertoire.RepertoireRefreshedHandler>().Select(listener => (Action)(() => listener(rep)))).ToArray();
            var sorceryListeners = hero.SorceryPointsAltered?.GetInvocationList() ?? Array.Empty<Delegate>();
            var sorcery = sorceryListeners.Cast<RulesetCharacter.SorceryPointsAlteredHandler>()
                .Select(listener => (Action)(() => listener(hero, draft.ExpectedSorceryEventValue, null))).ToList();
            // The original Convert callback was suppressed, so its normal one-shot modal listener
            // was never subscribed. Refresh it explicitly, unless it was already present.
            if (!sorceryListeners.Any(d => ReferenceEquals(d.Target, modal) && d.Method.Name == nameof(FlexibleCastingModal.SorceryPointCreated)))
                sorcery.Add(() => modal.SorceryPointCreated(hero, draft.ExpectedSorceryEventValue, null));
            var parentEvents = (hero.PowerActivated?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<RulesetCharacter.PowerActivatedHandler>()
                .Select(listener => (Action)(() => listener(hero, power, 0))).ToArray();
            return new N.Publication(
                () =>
                {
                    views.Validate(); if (modal.selectedSlotLevel != draft.Intent.Command.NativeCircle || hero.UsedSorceryPoints != beforeSorcery || power.RemainingUses != remaining || power.MaxUses != maximum || parent.Finished)
                        throw new O.Rejection(O.Code.StaleOperation, "Native counters/parent changed before publication");
                },
                () => ApplyNativeFields(hero, power, views, draft.NativeUsedSorceryAfter, maximum),
                () =>
                {
                    views.AssertCoherent(); if (hero.UsedSorceryPoints != draft.NativeUsedSorceryAfter || power.RemainingUses != maximum)
                        throw new InvalidOperationException("Native Flexible Casting fields did not agree");
                },
                repertoire, sorcery.ToArray(),
                () => { parent.Finished = true; source.Source.CompleteParent(); }, parentEvents,
                () => { if (Modals.TryGetValue(modal, out var owner) && ReferenceEquals(owner, source)) { Modals.Remove(modal); modal.Hide(true); } },
                () =>
                {
                    if (Modals.TryGetValue(modal, out var owner) && !ReferenceEquals(owner, source)) return;
                    if (modal.caster != hero || modal.repertoire != source.Repertoire) return;
                    if (modal.CanvasGroup != null) { modal.CanvasGroup.interactable = false; modal.CanvasGroup.blocksRaycasts = false; }
                }, views.Release);
        }
    }
    // All references/values/dictionaries have been validated and allocated. No observer or native
    // spend/gain API is allowed in this raw field batch; no managed user callback runs between writes.
    private static void ApplyNativeFields(RulesetCharacterHero hero, RulesetUsablePower power, StandardMulticasterResourcesContext.FlexibleSlotViews views, int sorceryUsed, int parentMaximum)
    {
        hero.usedSorceryPoints = sorceryUsed;
        views.ApplyFields();
        power.remainingUses = parentMaximum; // audited native AtWill Consume();Recharge() final state
    }
}
