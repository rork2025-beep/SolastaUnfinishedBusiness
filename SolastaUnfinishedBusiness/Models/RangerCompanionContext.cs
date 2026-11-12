using System.Linq;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static class RangerCompanionContext
{
    private const string BeastPrefix = "PowerRangerWildMasterSummonBeastCompanion";
    private static bool IsBeastPower(FeatureDefinitionPower power) => power != null && power.Name.StartsWith(BeastPrefix, System.StringComparison.Ordinal);
    internal static bool HasActiveCompanion(RulesetCharacterHero hero) => hero.PowersUsedByMe.Any(x => IsBeastPower(x.PowerDefinition));
    internal static bool CanAdvance(RulesetCharacterHero hero)
    {
        if (!HasActiveCompanion(hero)) return true;
        Main.Error("Ranger prototype: dismiss the active Wild Master companion before level-up. Re-summon after advancement for source HP, PB/WIS and original11 creature upgrade; no hero/companion was changed.");
        return false;
    }
    internal static bool CanSummon(CharacterActionMagicEffect action, FeatureDefinitionPower power)
    {
        if (!IsBeastPower(power)) return true;
        var hero = action.ActingCharacter.RulesetCharacter;
        var current = action.ActionParams.RulesetEffect;
        if (!hero.PowersUsedByMe.Any(x => !ReferenceEquals(x, current) && IsBeastPower(x.PowerDefinition))) return true;
        Main.Error("Ranger prototype: dismiss the existing Wild Master companion before resummoning; duplicate summon rejected before resource use.");
        return false;
    }
    internal static bool CanRespec(RulesetCharacterHero hero)
    {
        if (!RangerProgressionContext.Active || hero == null || hero.GetClassLevel(Ranger) == 0) return true;
        if (!RangerProgressionContext.CanUseClassMechanics(hero)) return false;
        var summons = hero.PowersUsedByMe.Any(x => x.PowerDefinition.EffectDescription.EffectForms.Any(f => f.FormType == EffectForm.EffectFormType.Summon)) ||
            hero.SpellsCastByMe.Any(x => x.SpellDefinition.EffectDescription.EffectForms.Any(f => f.FormType == EffectForm.EffectFormType.Summon));
        var marks = hero.ConcentratedSpell?.SpellDefinition == SolastaUnfinishedBusiness.Api.DatabaseHelper.SpellDefinitions.HuntersMark ||
            EffectHelpers.GetAllConditionsBySourceGuid(hero.Guid).Any(c => c.ConditionDefinition.Name == "ConditionMarkedByHunter");
        if (!summons && !marks) return true;
        Main.Error("Ranger prototype: dismiss owned summons and end Hunter's Mark before respec. No hero state changed.");
        return false;
    }
    internal static bool ShouldTransferCondition(RulesetActor oldActor, RulesetActor newActor, RulesetCondition condition)
    {
        if (oldActor is not RulesetCharacterHero hero) return true;
        return RangerProgressionRules.ShouldTransferCondition(RangerProgressionContext.Active, hero.GetClassLevel(Ranger),
            condition.SourceGuid == oldActor.Guid || condition.SourceGuid == newActor.Guid,
            RangerProgressionRules.IsClassCondition(condition.ConditionDefinition.Name));
    }
}
