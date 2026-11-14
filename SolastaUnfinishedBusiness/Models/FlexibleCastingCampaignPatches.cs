using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using V = SolastaUnfinishedBusiness.Models.FlexibleCastingInvocationPlan;
using O = SolastaUnfinishedBusiness.Models.SpellResourceOperationPlan;

namespace SolastaUnfinishedBusiness.Models;

internal static partial class FlexibleCastingPreflightContext
{
    private sealed class CampaignEntry(RulesetCharacterHero hero, RulesetUsablePower usable, V.Authority authority, CampaignEntry previous) : IDisposable
    {
        internal readonly RulesetCharacterHero Hero = hero; internal readonly RulesetUsablePower Usable = usable; internal readonly V.Authority Authority = authority;
        internal bool FactoryStarted; internal bool Bound; internal bool ParentHeld; internal bool Ended; internal bool Finished;
        public void Dispose(){Ended=true;campaign=previous;}
    }
    private static readonly Dictionary<string,CampaignEntry> CampaignParents=new(StringComparer.Ordinal);
    [ThreadStatic] private static CampaignEntry campaign;
    private static bool Flexible(FeatureDefinitionPower p) => new[] { false, true }.Any(slot => p == Api.DatabaseHelper.GetDefinition<FeatureDefinitionPower>(SpellResourceConsumerCatalog.FlexibleCasting(slot).Definition));
    private static bool FlexibleShape(EffectDescription effect, bool create) =>
        effect != null && effect.DurationType == RuleDefinitions.DurationType.Instantaneous &&
        effect.EffectForms.Count == 1 && effect.EffectForms[0].FormType == EffectForm.EffectFormType.SpellSlots &&
        effect.EffectForms[0].SpellSlotsForm?.Type == (create ? SpellSlotsForm.EffectType.CreateSpellSlot : SpellSlotsForm.EffectType.CreateSorceryPoints);
    internal static bool BeginCampaign(GameCampaignCharacter owner, RulesetUsablePower power, out IDisposable state)
    {
        state = null; if (!StandardMulticasterResourcesContext.RuntimeEnabled || !Flexible(power?.PowerDefinition)) return true;
        RequireLocalSinglePlayer(); // reject multiplayer/unknown network state before issuing source authority
        var hero = owner.RulesetCharacter.GetOriginalHero();
        if (hero == null || owner.RulesetCharacter != hero || !hero.UsablePowers.Contains(power) ||
            ServiceRepository.GetService<IMagicCommandService>() is not LocalCommandManager ||
            ServiceRepository.GetService<IRulesetImplementationService>()?.GetType() != typeof(RulesetImplementationManager) ||
            ServiceRepository.GetService<IPlayerControllerService>()?.ActivePlayerController?.IsCharacterControlled(hero) != true)
            throw new O.Rejection(O.Code.Ineligible, "Only a controlled local campaign invocation can issue transient authority; network use remains blocked");
        StandardMulticasterResourcesContext.PreflightResources(hero);
        var definition = power.PowerDefinition;
        var create = definition == Api.DatabaseHelper.GetDefinition<FeatureDefinitionPower>(SpellResourceConsumerCatalog.FlexibleCasting(true).Definition);
        if (definition.RechargeRate != RuleDefinitions.RechargeRate.AtWill || !FlexibleShape(definition.EffectDescription, create))
            throw new O.Rejection(O.Code.Ineligible, "Campaign source constructor/effect shape changed; no resource-capable source allowed");
        var authority = Invocations.BeginCampaign(Evidence(hero, definition, create)); // only ID mint point
        var entry = new CampaignEntry(hero, power, authority, campaign); CampaignParents.Add(authority.Id,entry); campaign = entry; state = entry; return true;
    }
    internal static bool AllowCampaignSourceConstructor(RulesetCharacter actor, RulesetUsablePower usable)
    {
        var entry = campaign;
        if (!StandardMulticasterResourcesContext.RuntimeEnabled || entry == null || entry.FactoryStarted || actor != entry.Hero || !ReferenceEquals(usable, entry.Usable)) return false;
        if (!Flexible(usable.PowerDefinition)) return false;
        entry.FactoryStarted = true; return true; // one audited metadata-only constructor, not native UsePower
    }
    internal static void BindCampaignSource(RulesetCharacter actor, RulesetUsablePower usable, RulesetEffectPower effect)
    {
        var entry = campaign; if (entry == null) return;
        if (!entry.FactoryStarted || entry.Bound || actor != entry.Hero || !ReferenceEquals(usable, entry.Usable) || effect == null || effect.Terminated ||
            effect.SourceGuid != entry.Hero.Guid || !ReferenceEquals(effect.UsablePower, usable) || !ReferenceEquals(effect.PowerDefinition, entry.Authority.Entry.Power) || effect.OriginItem != null ||
            !FlexibleShape(effect.EffectDescription, entry.Authority.Entry.Create))
            throw new O.Rejection(O.Code.AmbiguousSource, "Campaign constructor result does not belong to its trusted invocation");
        Invocations.BindCampaign(entry.Authority, Evidence(entry.Hero, usable.PowerDefinition, entry.Authority.Entry.Create), effect, () => !effect.Terminated);
        entry.Bound = true;
    }
    internal static void HoldOrUseCampaignPower(RulesetCharacter actor, RulesetUsablePower usable)
    {
        if (!StandardMulticasterResourcesContext.RuntimeEnabled || !Flexible(usable.PowerDefinition)) { actor.UsePower(usable); return; }
        var entry = campaign;
        if (entry == null || !entry.Bound || entry.ParentHeld || actor != entry.Hero || !ReferenceEquals(usable, entry.Usable)) throw new O.Rejection(O.Code.MissingIdentity, "Campaign parent does not match its invocation");
        entry.ParentHeld = true; // no native Consume/Recharge/PowerActivated or parent completion/refund
    }
    internal static Exception EndCampaign(Exception error, IDisposable state)
    {
        if (state is CampaignEntry entry)
        {
            try
            {
                if (error != null || !entry.Bound || !entry.ParentHeld || entry.Authority.State != V.Status.ModalOpen)
                {
                    error ??= new O.Rejection(O.Code.MissingIdentity, "Campaign source did not open its exact modal; parent remains unspent");
                    Invocations.Close(entry.Authority, V.Status.Rejected);
                    if (Origins.TryGetValue(entry.Authority.Id, out var origin))
                    {
                        var modal = Gui.GuiService.GetScreen<FlexibleCastingModal>();
                        RejectAndClose(origin, modal, V.Status.Rejected);
                    }
                }
            }
            finally { entry.Dispose(); }
        }
        return error;
    }
}

internal static class FlexibleCastingCampaignPatches
{
    [HarmonyPatch(typeof(GameCampaignCharacter), nameof(GameCampaignCharacter.UsePower))]
    internal static class EntryPatch
    {
        [HarmonyPriority(Priority.First + 2)]
        internal static bool Prefix(GameCampaignCharacter __instance, RulesetUsablePower __0, out IDisposable __state) => FlexibleCastingPreflightContext.BeginCampaign(__instance, __0, out __state);
        internal static Exception Finalizer(Exception __exception, IDisposable __state) => FlexibleCastingPreflightContext.EndCampaign(__exception, __state);
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList(); var native = AccessTools.Method(typeof(RulesetCharacter), nameof(RulesetCharacter.UsePower));
            var replacement = AccessTools.Method(typeof(FlexibleCastingPreflightContext), nameof(FlexibleCastingPreflightContext.HoldOrUseCampaignPower));
            if (code.Count(i => i.Calls(native)) != 1) throw new InvalidOperationException("Campaign UsePower shape changed: parent cannot be safely held");
            foreach (var i in code) { if (i.Calls(native)) { i.opcode = System.Reflection.Emit.OpCodes.Call; i.operand = replacement; } yield return i; }
        }
    }
    [HarmonyPatch(typeof(RulesetImplementationManager), nameof(RulesetImplementationManager.InstantiateEffectPower))]
    internal static class SourceCreatedPatch
    {
        internal static void Postfix(RulesetCharacter __0, RulesetUsablePower __1, RulesetEffectPower __result)
        { if (StandardMulticasterResourcesContext.RuntimeEnabled) FlexibleCastingPreflightContext.BindCampaignSource(__0, __1, __result); }
    }
}
