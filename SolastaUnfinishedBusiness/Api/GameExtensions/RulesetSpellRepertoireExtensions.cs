using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Behaviors;

namespace SolastaUnfinishedBusiness.Api.GameExtensions;

public static class RulesetSpellRepertoireExtensions
{
    public static RulesetCharacter GetCaster(this RulesetSpellRepertoire repertoire)
    {
        var character = EffectHelpers.GetCharacterByGuid(repertoire?.CharacterInventory?.BearerGuid ?? 0) ?? Global.InspectedHero;
        return repertoire?.SpellCastingClass == Api.DatabaseHelper.CharacterClassDefinitions.Druid
            ? Models.DruidWildshapeContext.SpellPointOwner(character) : character;
    }

    [CanBeNull]
    public static CharacterClassDefinition GetCastingClass(this RulesetSpellRepertoire repertoire)
    {
        return repertoire.SpellCastingFeature.GetFirstSubFeatureOfType<ClassHolder>()?.Class
               ?? repertoire.SpellCastingClass;
    }

    public static bool AtLeastOneSpellSlotAvailable(this RulesetSpellRepertoire repertoire)
    {
        for (var spellLevel = 1;
             spellLevel <= repertoire.MaxSpellLevelOfSpellCastingLevel;
             spellLevel++)
        {
            repertoire.GetSlotsNumber(spellLevel, out var remaining, out _);

            if (remaining <= 0)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
