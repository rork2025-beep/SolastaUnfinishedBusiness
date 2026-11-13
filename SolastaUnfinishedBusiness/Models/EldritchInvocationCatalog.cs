using System;
using System.Collections.Generic;
using System.Linq;
using E = SolastaUnfinishedBusiness.Models.EldritchInvocationSourcesPlan;

namespace SolastaUnfinishedBusiness.Models;

// Audited registered identities and graph shapes, not name-based gameplay heuristics.
// Levels/pacts/spells/resources are read from the final installed definitions after rule switches.
internal static class EldritchInvocationCatalog
{
    private const string Rows = @"AgonizingBlast||AdditionalDamageInvocationAgonizingBlast|0|0|None|AdditionalDamageInvocationAgonizingBlast@FeatureDefinitionAdditionalDamage
ArmorOfShadows|MageArmorInvocationArmorShadows||0|0|None|
AscendantStep|LevitateBoots||0|0|None|
BeguilingInfluence||ProficiencyBeguilingInfluence|0|0|None|ProficiencyBeguilingInfluence@FeatureDefinitionProficiency
BewitchingWhispers|DreadfulOmen||1|1|None|
BookAncientSecrets||FeatureSetInvocationBookAncientSecrets|0|0|None|ActionAffinityInvocationBookAncientSecrets@FeatureDefinitionActionAffinity;FeatureSetInvocationBookAncientSecrets@FeatureDefinitionFeatureSet;MagicAffinityInvocationBookAncientSecrets@FeatureDefinitionMagicAffinity;PointPoolInvocationBookAncientSecrets@FeatureDefinitionPointPool
ChainsCarceri|HoldMonsterInvocationChainsCarceri||0|0|None|
DevilsSight||FeatureSetInvocationDevilsSight|0|0|None|ConditionAffinityInvocationDevilsSight@FeatureDefinitionConditionAffinity;FeatureSetInvocationDevilsSight@FeatureDefinitionFeatureSet;SenseBlindSight16@FeatureDefinitionSense;SenseSeeInvisible16@FeatureDefinitionSense
DreadfulWord|Confusion||1|1|None|
EldritchSight|DetectMagic||0|0|None|
EldritchSpear||CombatAffinityEldritchSpear|0|0|None|CombatAffinityEldritchSpear@FeatureDefinitionCombatAffinity
EyesRuneKeeper||ProficiencyAllLanguages|0|0|None|ProficiencyAllLanguages@FeatureDefinitionProficiency
FiendishVigor|FalseLife||0|0|None|
InvocationAbilitiesOfTheChainMaster||FeatureInvocationAbilitiesOfTheChainMaster|0|0|None|FeatureInvocationAbilitiesOfTheChainMaster@FeatureDefinition
InvocationAspectOfTheMoon||FeatureSetInvocationAspectOfTheMoon|0|0|None|CampAffinityInvocationAspectOfTheMoonRest@FeatureDefinitionCampAffinity;CampAffinityInvocationAspectOfTheMoonTrance@FeatureDefinitionCampAffinity;FeatureSetInvocationAspectOfTheMoon@FeatureDefinitionFeatureSet
InvocationBondOfTheTalisman||PowerInvocationBondOfTheTalisman|0|0|None|PowerInvocationBondOfTheTalisman@FeatureDefinitionPower
InvocationBreakerAndBanisher|DispelEvilAndGood||0|1|None|
InvocationBreathOfTheNight|FogCloud||0|0|None|
InvocationBurningHex||PowerInvocationBurningHex|0|0|Hex|PowerInvocationBurningHex@FeatureDefinitionPower
InvocationCallOfTheBeast|ConjureAnimals||0|1|None|
InvocationChillingBlast||FeatureInvocationChillingBlast|0|0|None|FeatureInvocationChillingBlast@FeatureDefinition
InvocationChillingHex||PowerInvocationChillingHex|0|0|Hex|PowerInvocationChillingHex@FeatureDefinitionPower
InvocationCorrosiveBlast||FeatureInvocationCorrosiveBlast|0|0|None|FeatureInvocationCorrosiveBlast@FeatureDefinition
InvocationDevouringBlade||AttributeModifierDevouringBladeExtraAttack|0|0|ThirstingBlade|AttributeModifierDevouringBladeExtraAttack@FeatureDefinitionAttributeModifier
InvocationDiscerningGaze|DetectEvilAndGood||0|0|None|
InvocationEldritchMind||MagicAffinityInvocationEldritchMind|0|0|None|MagicAffinityInvocationEldritchMind@FeatureDefinitionMagicAffinity
InvocationEldritchSmite||AdditionalDamageInvocationEldritchSmite|0|0|None|AdditionalDamageInvocationEldritchSmite@FeatureDefinitionAdditionalDamage
InvocationFieryBlast||FeatureInvocationFieryBlast|0|0|None|FeatureInvocationFieryBlast@FeatureDefinition
InvocationFulminateBlast||FeatureInvocationFulminateBlast|0|0|None|FeatureInvocationFulminateBlast@FeatureDefinition
InvocationGiftOfTheEverLivingOnes||HealingModifierBeaconOfHope|0|0|None|HealingModifierBeaconOfHope@FeatureDefinitionHealingModifier
InvocationGiftOfTheHunter|PassWithoutTrace||0|1|None|
InvocationGiftOfTheProtectors||DamageAffinityInvocationGiftOfTheProtectorsRelentlessEndurance|0|0|None|DamageAffinityInvocationGiftOfTheProtectorsRelentlessEndurance@FeatureDefinitionDamageAffinity
InvocationGraspingBlast||PowerInvocationGraspingBlast|0|0|None|PowerInvocationGraspingBlast@FeatureDefinitionPower
InvocationHinderingBlast||AdditionalDamageInvocationHinderingBlast|0|0|None|AdditionalDamageInvocationHinderingBlast@FeatureDefinitionAdditionalDamage
InvocationImprovedPactWeapon||AttackModifierInvocationImprovedPactWeapon|0|0|None|AttackModifierInvocationImprovedPactWeapon@FeatureDefinitionAttackModifier
InvocationInexorableHex||PowerInvocationInexorableHex|0|0|Hex|PowerInvocationInexorableHex@FeatureDefinitionPower
InvocationKinesis|Haste||0|1|None|
InvocationNecroticBlast||FeatureInvocationNecroticBlast|0|0|None|FeatureInvocationNecroticBlast@FeatureDefinition
InvocationPactBlade||FeatureSetPactBlade|0|0|None|AttackModifierPactBladeMagicalWeapon@FeatureDefinitionAttackModifier;FeatureSetPactBlade@FeatureDefinitionFeatureSet;ProficiencyPactBladeMartialWeapon@FeatureDefinitionProficiency
InvocationPactChain||FeatureSetPactChain|0|0|None|AttributeModifierWarlockPactChainInitial@FeatureDefinitionAttributeModifier;FeatureSetPactChain@FeatureDefinitionFeatureSet;PowerPactChainImp@FeatureDefinitionPower;PowerPactChainPseudodragon@FeatureDefinitionPower;PowerPactChainQuasit@FeatureDefinitionPower;PowerPactChainSprite@FeatureDefinitionPower
InvocationPactTome||PointPoolPactTome|0|0|None|PointPoolPactTome@FeatureDefinitionPointPool
InvocationPerniciousCloak||PowerInvocationPerniciousCloak|0|0|None|PowerInvocationPerniciousCloak@FeatureDefinitionPower
InvocationPoisonousBlast||FeatureInvocationPoisonousBlast|0|0|None|FeatureInvocationPoisonousBlast@FeatureDefinition
InvocationPsychicBlast||FeatureInvocationPsychicBlast|0|0|None|FeatureInvocationPsychicBlast@FeatureDefinition
InvocationRadiantBlast||FeatureInvocationRadiantBlast|0|0|None|FeatureInvocationRadiantBlast@FeatureDefinition
InvocationShroudOfShadow|Invisibility||0|0|None|
InvocationSpectralShield|ShieldOfFaith||0|0|None|
InvocationStasis|Slow||0|1|None|
InvocationSuperiorPactWeapon||AttackModifierInvocationSuperiorPactWeapon|0|0|None|AttackModifierInvocationSuperiorPactWeapon@FeatureDefinitionAttackModifier
InvocationTenaciousPlague|InsectPlague||0|1|None|
InvocationThunderBlast||FeatureInvocationThunderBlast|0|0|None|FeatureInvocationThunderBlast@FeatureDefinition
InvocationTombOfFrost||PowerInvocationTombOfFrost|0|0|None|PowerInvocationTombOfFrost@FeatureDefinitionPower
InvocationTrickstersEscape|TrickstersEscape||0|1|None|
InvocationUltimatePactWeapon||AttackModifierInvocationUltimatePactWeapon|0|0|None|AttackModifierInvocationUltimatePactWeapon@FeatureDefinitionAttackModifier
InvocationUndyingServitude|CreateDeadRisenSkeleton_Enforcer||0|1|None|
InvocationVerdantArmor|VerdantArmor||0|0|None|
InvocationVexingHex||PowerInvocationVexingHex|0|0|Hex|PowerInvocationVexingHex@FeatureDefinitionPower
Lifedrinker||AdditionalDamageLifedrinker|0|0|None|AdditionalDamageLifedrinker@FeatureDefinitionAdditionalDamage
MinionsChaos|ConjureElemental||1|1|None|
MireMind|Slow||1|1|None|
OneWithShadows||LightAffinityInvocationOneWithShadows|0|0|None|LightAffinityInvocationOneWithShadows@FeatureDefinitionLightAffinity|PowerInvocationOneWithShadows@FeatureDefinitionPower
OtherworldlyLeap|JumpOtherworldlyLeap||0|0|None|
RepellingBlast||PowerInvocationRepellingBlast|0|0|None|PowerInvocationRepellingBlast@FeatureDefinitionPower
SignIllOmen|BestowCurse||1|1|None|
ThiefFiveFates|Bane||1|1|None|
ThirstingBlade||AttributeModifierThirstingBladeExtraAttack|0|0|None|AttributeModifierThirstingBladeExtraAttack@FeatureDefinitionAttributeModifier
VoiceChainMaster||AttributeModifierInvocationVoiceChainMaster|0|0|None|AttributeModifierInvocationVoiceChainMaster@FeatureDefinitionAttributeModifier
WitchSight||FeatureSetInvocationWitchSight|0|0|None|FeatureSetInvocationWitchSight@FeatureDefinitionFeatureSet;MagicAffinityInvocationWitchSight@FeatureDefinitionMagicAffinity;SenseSeeInvisible16@FeatureDefinitionSense";
    internal static IReadOnlyList<string> Ids => Array.AsReadOnly(Rows.Split('\n').Select(r=>r.Split('|')[0].Trim()).ToArray());
    internal static E.Definition Classify(string id,string spell,int circle,string feature,int level,string pact,string known,
        bool consumes,bool longRest,IEnumerable<E.FeatureGrant> features,E.ValidatorRule validator,
        AuxiliarySpellSourcesPlan.Source choices=null,bool selectable=true,bool custom=false)
    {
        var nodes=features.ToArray();var row=Rows.Split('\n').Select(r=>r.Trim().Split('|')).SingleOrDefault(r=>r[0]==id);
        var signature=string.Join(";",nodes.Select(n=>n.Id+"@"+n.Type).OrderBy(x=>x,StringComparer.Ordinal));
        var expectedValidator=row==null?E.ValidatorRule.Unverified:(E.ValidatorRule)Enum.Parse(typeof(E.ValidatorRule),row[5]);
        var alternate=row!=null && row.Length>7 && row[7]==signature && feature=="PowerInvocationOneWithShadows";
        var verified=!(id=="InvocationEldritchSmite" && !nodes.Any(n=>n.OwnerClass=="Warlock" && n.Trigger=="SpendSpellSlot")) && !custom && !nodes.Any(n=>n.Type=="FeatureDefinitionFeatureSet" && n.Mode!="Union") && row!=null && row[1]==(spell??"") && (row[2]==(feature??"") && row[6]==signature || alternate) &&
            (row[3]=="1")==consumes && (row[4]=="1")==longRest && validator==expectedValidator;
        var kind=!verified?E.Kind.Unsupported:spell!=null?consumes?E.Kind.PaidSpell:longRest?E.Kind.LimitedSpell:E.Kind.AtWillSpell:
            nodes.Any(n=>n.Type=="FeatureDefinitionPointPool")?E.Kind.Choice:
            nodes.Any(n=>n.Type=="FeatureDefinitionPower")?E.Kind.Power:
            id is "DevilsSight" or "BeguilingInfluence" or "EyesRuneKeeper"?E.Kind.Passive:E.Kind.Feature;
        if(kind==E.Kind.Choice && choices==null)kind=E.Kind.Unsupported;
        return new E.Definition(id,kind,spell,circle,feature,level,pact,known,consumes,longRest,nodes,choices,validator,
            selectable&&!custom,custom?"custom invocation pool excluded by native Adept UI":row==null?"unregistered invocation identity":!verified?"changed native grant shape or unverified validator":
            kind==E.Kind.Unsupported?"missing native choice profile":null,nodes.Any(n=>n.OwnerClass=="Warlock" && n.Trigger=="SpendSpellSlot"));
    }
}
