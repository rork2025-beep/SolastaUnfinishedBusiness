# Stage13.5D — подтверждённые auxiliary spell sources

Это **статический registry audit** исходников и available blueprint snapshots. Native `AuxiliarySpellSourcesContext.CaptureInstalledDefinitions` повторно читает реальные DB registrations после switches; его `Coverage`/log — источник итоговой runtime-матрицы. Игра здесь не запускалась. Указанные строки не означают активацию источника у героя. Stable IDs в runtime включают origin, root и точный feature; варианты не объединяются по spell list.

## Собственные native repertoires

Все приведённые ниже casting definitions имеют Race origin, в том числе feats/styles. Native class может быть null; это не разрешение назначить Wizard/Cleric/другой класс. Spell list, raw known/cantrip arrays, slot rows и recharge копируются без переинтерпретации. Источники с conditional/multiple casters или смешанным CantripOrSpell budget закрываются с UnsupportedSource до отдельного adapter.

| Origin / зарегистрированный root | Casting feature | Ability | List | Acquisition / native recharge |
|---|---|---|---|---|
| Race / `ElfHigh` | [`CastSpellElfHigh`](../Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpellElfHigh.json) | Intelligence | `SpellListWizard` | Selection; native cantrips 1; AtWill |
| Race / `GnomeShadow` | [`CastSpellGnomeShadow`](../Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpellGnomeShadow.json) | Intelligence | `SpellListGnomeShadow` | FixedList; native cantrips 1; LongRest |
| Race / `RaceBolgrif` | [`CastSpellBolgrifMagic`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellBolgrifMagic.json) | Wisdom | `SpellListBolgrif` | Selection; native cantrips 1; AtWill |
| Race / `RaceDarkelf` | [`CastSpellDarkelfMagic`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellDarkelfMagic.json) | Charisma | `SpellListDarkelf` | FixedList; native cantrips 1; LongRest |
| Race / `RaceDraconicKobold` | [`CastSpellDraconicKoboldMagic`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellDraconicKoboldMagic.json) | Charisma | `SpellListDraconicKobold` | Selection; native cantrips 1; LongRest |
| Race / `RaceFairy` | [`CastSpellFairy`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFairy.json) | Charisma | `SpellListFairy` | FixedList; native cantrips 1; LongRest |
| Race / `RaceHalfElfDark` | [`CastSpellDarkelfMagic`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellDarkelfMagic.json) | Charisma | `SpellListDarkelf` | FixedList; native cantrips 1; LongRest |
| Race / `RaceHalfElfHigh` | [`CastSpellElfHigh`](../Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpellElfHigh.json) | Intelligence | `SpellListWizard` | Selection; native cantrips 1; AtWill |
| Race / `RaceImpInfernal` | [`CastSpellImpInfernal`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellImpInfernal.json) | Charisma | `SpellListImpInfernal` | FixedList; native cantrips 1; LongRest |
| Race / `RaceMalakh` | [`CastSpellMalakhMagic`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellMalakhMagic.json) | Charisma | `SpellListMalakh` | FixedList; native cantrips 1; LongRest |
| Race / `RaceOni` | [`CastSpellOniOgreMagic`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellOniOgreMagic.json) | Wisdom | `SpellListOni` | FixedList; native cantrips 0; LongRest |
| Race / `RaceTieflingDevilTongue` | [`CastSpellTieflingDevilTongue`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellTieflingDevilTongue.json) | Charisma | `SpellListTieflingDevilTongue` | FixedList; native cantrips 1; LongRest |
| Race / `RaceTieflingFeral` | [`CastSpellTieflingFeral`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellTieflingFeral.json) | Charisma | `SpellListTieflingFeral` | FixedList; native cantrips 1; LongRest |
| Race / `RaceTieflingMephistopheles` | [`CastSpellTieflingMephistopheles`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellTieflingMephistopheles.json) | Charisma | `SpellListTieflingMephistopheles` | FixedList; native cantrips 1; LongRest |
| Race / `RaceTieflingZariel` | [`CastSpellTieflingZariel`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellTieflingZariel.json) | Charisma | `SpellListTieflingZariel` | FixedList; native cantrips 1; LongRest |
| Race / `Tiefling` | [`CastSpellTiefling`](../Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpellTiefling.json) | Charisma | `SpellListTiefling` | FixedList; native cantrips 1; LongRest |
| Feat / `FeatAegisTouchedCha` | [`CastSpellAegisTouchedCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellAegisTouchedCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatAegisTouchedInt` | [`CastSpellAegisTouchedIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellAegisTouchedIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatAegisTouchedWis` | [`CastSpellAegisTouchedWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellAegisTouchedWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatCelestialTouchedCha` | [`CastSpellCelestialTouchedCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellCelestialTouchedCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatCelestialTouchedInt` | [`CastSpellCelestialTouchedIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellCelestialTouchedIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatCelestialTouchedWis` | [`CastSpellCelestialTouchedWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellCelestialTouchedWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatFeyTeleportationCha` | [`CastSpellFeyTeleportCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeyTeleportCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatFeyTeleportationInt` | [`CastSpellFeyTeleportIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeyTeleportIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatFeyTeleportationWis` | [`CastSpellFeyTeleportWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeyTeleportWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatFlameTouchedCha` | [`CastSpellFlameTouchedCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFlameTouchedCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatFlameTouchedInt` | [`CastSpellFlameTouchedIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFlameTouchedIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatFlameTouchedWis` | [`CastSpellFlameTouchedWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFlameTouchedWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatIridescentTouchedCha` | [`CastSpellIridescentTouchedCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellIridescentTouchedCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatIridescentTouchedInt` | [`CastSpellIridescentTouchedIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellIridescentTouchedIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatIridescentTouchedWis` | [`CastSpellIridescentTouchedWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellIridescentTouchedWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatMagicInitiateBard` | [`CastSpellFeatMagicInitiateBard`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatMagicInitiateBard.json) | Charisma | `SpellListBard` | 2 Cantrip (`PointPoolFeatMagicInitiateBardCantrip`), 1 Spell (`PointPoolFeatMagicInitiateBardSpell`); LongRest |
| Feat / `FeatMagicInitiateCleric` | [`CastSpellFeatMagicInitiateCleric`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatMagicInitiateCleric.json) | Wisdom | `SpellListCleric` | 2 Cantrip (`PointPoolFeatMagicInitiateClericCantrip`), 1 Spell (`PointPoolFeatMagicInitiateClericSpell`); LongRest |
| Feat / `FeatMagicInitiateDruid` | [`CastSpellFeatMagicInitiateDruid`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatMagicInitiateDruid.json) | Wisdom | `SpellListDruid` | 2 Cantrip (`PointPoolFeatMagicInitiateDruidCantrip`), 1 Spell (`PointPoolFeatMagicInitiateDruidSpell`); LongRest |
| Feat / `FeatMagicInitiateSorcerer` | [`CastSpellFeatMagicInitiateSorcerer`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatMagicInitiateSorcerer.json) | Charisma | `SpellListSorcerer` | 2 Cantrip (`PointPoolFeatMagicInitiateSorcererCantrip`), 1 Spell (`PointPoolFeatMagicInitiateSorcererSpell`); LongRest |
| Feat / `FeatMagicInitiateWarlock` | [`CastSpellFeatMagicInitiateWarlock`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatMagicInitiateWarlock.json) | Charisma | `SpellListWarlock` | 2 Cantrip (`PointPoolFeatMagicInitiateWarlockCantrip`), 1 Spell (`PointPoolFeatMagicInitiateWarlockSpell`); LongRest |
| Feat / `FeatMagicInitiateWizard` | [`CastSpellFeatMagicInitiateWizard`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatMagicInitiateWizard.json) | Intelligence | `SpellListWizard` | 2 Cantrip (`PointPoolFeatMagicInitiateWizardCantrip`), 1 Spell (`PointPoolFeatMagicInitiateWizardSpell`); LongRest |
| Feat / `FeatPeregrinationTouchedCha` | [`CastSpellPeregrinationTouchedCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellPeregrinationTouchedCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatPeregrinationTouchedInt` | [`CastSpellPeregrinationTouchedIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellPeregrinationTouchedIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatPeregrinationTouchedWis` | [`CastSpellPeregrinationTouchedWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellPeregrinationTouchedWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatRetinueTouchedCha` | [`CastSpellRetinueTouchedCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellRetinueTouchedCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatRetinueTouchedInt` | [`CastSpellRetinueTouchedIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellRetinueTouchedIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatRetinueTouchedWis` | [`CastSpellRetinueTouchedWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellRetinueTouchedWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatShadowTouchedCha` | [`CastSpellShadowTouchedCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellShadowTouchedCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatShadowTouchedInt` | [`CastSpellShadowTouchedIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellShadowTouchedIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatShadowTouchedWis` | [`CastSpellShadowTouchedWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellShadowTouchedWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatSpellSniperDruid` | [`CastSpellFeatSpellSniperDruid`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatSpellSniperDruid.json) | Wisdom | `SpellListFeatSpellSniperDruid` | 1 Cantrip (`PointPoolFeatSpellSniperDruidCantrip`); LongRest |
| Feat / `FeatSpellSniperInventor` | [`CastSpellFeatSpellSniperInventor`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatSpellSniperInventor.json) | Intelligence | `SpellListFeatSpellSniperInventor` | 1 Cantrip (`PointPoolFeatSpellSniperInventorCantrip`); LongRest |
| Feat / `FeatSpellSniperSorcerer` | [`CastSpellFeatSpellSniperSorcerer`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatSpellSniperSorcerer.json) | Charisma | `SpellListFeatSpellSniperSorcerer` | 1 Cantrip (`PointPoolFeatSpellSniperSorcererCantrip`); LongRest |
| Feat / `FeatSpellSniperWarlock` | [`CastSpellFeatSpellSniperWarlock`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatSpellSniperWarlock.json) | Charisma | `SpellListFeatSpellSniperWarlock` | 1 Cantrip (`PointPoolFeatSpellSniperWarlockCantrip`); LongRest |
| Feat / `FeatSpellSniperWizard` | [`CastSpellFeatSpellSniperWizard`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatSpellSniperWizard.json) | Intelligence | `SpellListFeatSpellSniperWizard` | 1 Cantrip (`PointPoolFeatSpellSniperWizardCantrip`); LongRest |
| Feat / `FeatVerdantTouchedCha` | [`CastSpellVerdantTouchedCharisma`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellVerdantTouchedCharisma.json) | Charisma | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatVerdantTouchedInt` | [`CastSpellVerdantTouchedIntelligence`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellVerdantTouchedIntelligence.json) | Intelligence | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatVerdantTouchedWis` | [`CastSpellVerdantTouchedWisdom`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellVerdantTouchedWisdom.json) | Wisdom | `SpellListEmpty` | Spellbook; native cantrips 0; None |
| Feat / `FeatDarkElfMagic` | [`CastSpellFeatDarkElfMagic`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatDarkElfMagic.json) | Charisma | `SpellListFeatDarkElfMagic` | FixedList; native cantrips 1; LongRest |
| Feat / `FeatWoodElfMagic` | [`CastSpellFeatWoodElfMagic`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellFeatWoodElfMagic.json) | Wisdom | `SpellListFeatWoodElfMagic` | 1 Cantrip (`PointPoolFeatWoodElfMagicCantrip`); LongRest |
| Style / `BlessedWarrior` | [`CastSpellBlessedWarrior`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellBlessedWarrior.json) | Charisma | `SpellListCleric` | 2 Cantrip (`PointPoolBlessedWarrior`); LongRest |
| Style / `DruidicWarrior` | [`CastSpellDruidicWarrior`](../Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionCastSpell/CastSpellDruidicWarrior.json) | Wisdom | `SpellListDruid` | 2 Cantrip (`PointPoolDruidicWarrior`); LongRest |

Available snapshot roots: **{'Race': 16, 'Feat': 40, 'Style': 2}**. Их native arrays лежат в linked definitions, не заменяются cumulative class tables. У Magic Initiate `KnownSpells` начинается с0 на1–4 и2 с5 вследствие builder Flat semantics; реальная награда остаётся **2 cantrips +1 spell** из point pools. `KnownSpells=2` не превращается в два дополнительных выбранных spells.

## Feat attachments и invocation free uses

`CasterFeats.MakeSpellFeatureAndInvocations` создаёт отдельный Race-origin carrier, custom invocation для каждого non-reaction spell и auto-prepared feature с явным `AnyClassOrSubclass` matcher. Free use применяет INT/WIS/CHA своего carrier; paid cast из реально существующего class repertoire применяет native ability этого класса и его обычный standard/Pact budget. Эти источники не создают новый class repertoire. Native matcher не сериализован в JSON; его связь проверена исходником и compiled bindings.

| Feat family (каждая имеет Cha / Int / Wis variant) | Carrier casting feature (suffix Charisma / Intelligence / Wisdom) | Native auto-prepared feature |
|---|---|---|
| `FeatAegisTouchedCha/Int/Wis` | `CastSpellAegisTouched{ability}` | `AutoPreparedSpellsFeatAegisTouched` |
| `FeatCelestialTouchedCha/Int/Wis` | `CastSpellCelestialTouched{ability}` | `AutoPreparedSpellsFeatCelestialTouched` |
| `FeatFlameTouchedCha/Int/Wis` | `CastSpellFlameTouched{ability}` | `AutoPreparedSpellsFeatFlameTouched` |
| `FeatIridescentTouchedCha/Int/Wis` | `CastSpellIridescentTouched{ability}` | `AutoPreparedSpellsFeatIridescentTouched` |
| `FeatPeregrinationTouchedCha/Int/Wis` | `CastSpellPeregrinationTouched{ability}` | `AutoPreparedSpellsFeatPeregrinationTouched` |
| `FeatRetinueTouchedCha/Int/Wis` | `CastSpellRetinueTouched{ability}` | `AutoPreparedSpellsFeatRetinueTouched` |
| `FeatShadowTouchedCha/Int/Wis` | `CastSpellShadowTouched{ability}` | `AutoPreparedSpellsFeatShadowTouched` |
| `FeatVerdantTouchedCha/Int/Wis` | `CastSpellVerdantTouched{ability}` | `AutoPreparedSpellsFeatVerdantTouched` |
| `FeatFeyTeleportationCha/Int/Wis` | `CastSpellFeyTeleport{ability}` | `AutoPreparedSpellsFeyTeleportation` |

Точные подтверждённые custom invocations (native ID, granted spell и flags; SHORT REST marker FeyTeleport добавляется factory и не виден в snapshot JSON):

| Custom invocation | Granted spell | Native consumption / recharge |
|---|---|---|
| `CustomInvocationAegisTouchedProtectionFromEvilGoodCharisma` | `ProtectionFromEvilGood` | own invocation; LongRest |
| `CustomInvocationAegisTouchedProtectionFromEvilGoodIntelligence` | `ProtectionFromEvilGood` | own invocation; LongRest |
| `CustomInvocationAegisTouchedProtectionFromEvilGoodWisdom` | `ProtectionFromEvilGood` | own invocation; LongRest |
| `CustomInvocationAegisTouchedProtectionFromPoisonCharisma` | `ProtectionFromPoison` | own invocation; LongRest |
| `CustomInvocationAegisTouchedProtectionFromPoisonIntelligence` | `ProtectionFromPoison` | own invocation; LongRest |
| `CustomInvocationAegisTouchedProtectionFromPoisonWisdom` | `ProtectionFromPoison` | own invocation; LongRest |
| `CustomInvocationAegisTouchedShieldOfFaithCharisma` | `ShieldOfFaith` | own invocation; LongRest |
| `CustomInvocationAegisTouchedShieldOfFaithIntelligence` | `ShieldOfFaith` | own invocation; LongRest |
| `CustomInvocationAegisTouchedShieldOfFaithWisdom` | `ShieldOfFaith` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedCureWoundsCharisma` | `CureWounds` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedCureWoundsIntelligence` | `CureWounds` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedCureWoundsWisdom` | `CureWounds` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedHealingWordCharisma` | `HealingWord` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedHealingWordIntelligence` | `HealingWord` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedHealingWordWisdom` | `HealingWord` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedLesserRestorationCharisma` | `LesserRestoration` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedLesserRestorationIntelligence` | `LesserRestoration` | own invocation; LongRest |
| `CustomInvocationCelestialTouchedLesserRestorationWisdom` | `LesserRestoration` | own invocation; LongRest |
| `CustomInvocationFeyTeleportMistyStepCharisma` | `MistyStep` | own invocation; SHORT REST marker |
| `CustomInvocationFeyTeleportMistyStepIntelligence` | `MistyStep` | own invocation; SHORT REST marker |
| `CustomInvocationFeyTeleportMistyStepWisdom` | `MistyStep` | own invocation; SHORT REST marker |
| `CustomInvocationFlameTouchedBurningHandsCharisma` | `BurningHands` | own invocation; LongRest |
| `CustomInvocationFlameTouchedBurningHandsIntelligence` | `BurningHands` | own invocation; LongRest |
| `CustomInvocationFlameTouchedBurningHandsWisdom` | `BurningHands` | own invocation; LongRest |
| `CustomInvocationFlameTouchedScorchingRayCharisma` | `ScorchingRay` | own invocation; LongRest |
| `CustomInvocationFlameTouchedScorchingRayIntelligence` | `ScorchingRay` | own invocation; LongRest |
| `CustomInvocationFlameTouchedScorchingRayWisdom` | `ScorchingRay` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedColorBurstCharisma` | `ColorBurst` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedColorBurstIntelligence` | `ColorBurst` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedColorBurstWisdom` | `ColorBurst` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedColorSprayCharisma` | `ColorSpray` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedColorSprayIntelligence` | `ColorSpray` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedColorSprayWisdom` | `ColorSpray` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedFaerieFireCharisma` | `FaerieFire` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedFaerieFireIntelligence` | `FaerieFire` | own invocation; LongRest |
| `CustomInvocationIridescentTouchedFaerieFireWisdom` | `FaerieFire` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedExpeditiousRetreatCharisma` | `ExpeditiousRetreat` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedExpeditiousRetreatIntelligence` | `ExpeditiousRetreat` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedExpeditiousRetreatWisdom` | `ExpeditiousRetreat` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedLongstriderCharisma` | `Longstrider` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedLongstriderIntelligence` | `Longstrider` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedLongstriderWisdom` | `Longstrider` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedSpiderClimbCharisma` | `SpiderClimb` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedSpiderClimbIntelligence` | `SpiderClimb` | own invocation; LongRest |
| `CustomInvocationPeregrinationTouchedSpiderClimbWisdom` | `SpiderClimb` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedBlessCharisma` | `Bless` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedBlessIntelligence` | `Bless` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedBlessWisdom` | `Bless` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedEnhanceAbilityCharisma` | `EnhanceAbility` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedEnhanceAbilityIntelligence` | `EnhanceAbility` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedEnhanceAbilityWisdom` | `EnhanceAbility` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedHeroismCharisma` | `Heroism` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedHeroismIntelligence` | `Heroism` | own invocation; LongRest |
| `CustomInvocationRetinueTouchedHeroismWisdom` | `Heroism` | own invocation; LongRest |
| `CustomInvocationShadowTouchedFalseLifeCharisma` | `FalseLife` | own invocation; LongRest |
| `CustomInvocationShadowTouchedFalseLifeIntelligence` | `FalseLife` | own invocation; LongRest |
| `CustomInvocationShadowTouchedFalseLifeWisdom` | `FalseLife` | own invocation; LongRest |
| `CustomInvocationShadowTouchedInflictWoundsCharisma` | `InflictWounds` | own invocation; LongRest |
| `CustomInvocationShadowTouchedInflictWoundsIntelligence` | `InflictWounds` | own invocation; LongRest |
| `CustomInvocationShadowTouchedInflictWoundsWisdom` | `InflictWounds` | own invocation; LongRest |
| `CustomInvocationShadowTouchedInvisibilityCharisma` | `Invisibility` | own invocation; LongRest |
| `CustomInvocationShadowTouchedInvisibilityIntelligence` | `Invisibility` | own invocation; LongRest |
| `CustomInvocationShadowTouchedInvisibilityWisdom` | `Invisibility` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedBarkskinCharisma` | `Barkskin` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedBarkskinIntelligence` | `Barkskin` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedBarkskinWisdom` | `Barkskin` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedEntangleCharisma` | `Entangle` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedEntangleIntelligence` | `Entangle` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedEntangleWisdom` | `Entangle` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedGoodberryCharisma` | `Goodberry` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedGoodberryIntelligence` | `Goodberry` | own invocation; LongRest |
| `CustomInvocationVerdantTouchedGoodberryWisdom` | `Goodberry` | own invocation; LongRest |

Подтверждено **72** custom invocation definitions. Reaction spells factory исключает из free invocation path; auto-prepared доступ остаётся только по установленному matcher. Existing Consume/Recharge patches и native `used` остаются authoritative, adapter проверяет used/at-will, не создаёт второй счётчик.

## Стандартные invocation spell sources

Зарегистрированы как Invocation roots с explicit Warlock casting feature. Это не добавляет Warlock level. RequiredLevel/RequiredPact/RequiredKnownSpell сохраняются из definition, прежние Warlock6/6.1 training validators не меняются. **consumesSpellSlot=true пока UnsupportedSource** для mixed admission: native resource/repertoire selection не подменяется guessed owner. FeatEldritchAdept без подтверждённого отдельного owner adapter также UnsupportedSource.

| Invocation | Spell | Required level / pact / known spell | Domain |
|---|---|---|---|
| `ArmorOfShadows` | `MageArmorInvocationArmorShadows` | 1 / — / — | native invocation / AtWill |
| `AscendantStep` | `LevitateBoots` | 9 / — / — | native invocation / AtWill |
| `BewitchingWhispers` | `DreadfulOmen` | 7 / — / — | **Unsupported paid native invocation** |
| `ChainsCarceri` | `HoldMonsterInvocationChainsCarceri` | 15 / FeatureSetPactChain / — | native invocation / AtWill |
| `DreadfulWord` | `Confusion` | 7 / — / — | **Unsupported paid native invocation** |
| `EldritchSight` | `DetectMagic` | 1 / — / — | native invocation / AtWill |
| `FiendishVigor` | `FalseLife` | 1 / — / — | native invocation / AtWill |
| `InvocationBreakerAndBanisher` | `DispelEvilAndGood` | 9 / — / — | native invocation / LongRest |
| `InvocationBreathOfTheNight` | `FogCloud` | 2 / — / — | native invocation / AtWill |
| `InvocationCallOfTheBeast` | `ConjureAnimals` | 5 / — / — | native invocation / LongRest |
| `InvocationDiscerningGaze` | `DetectEvilAndGood` | 5 / — / — | native invocation / AtWill |
| `InvocationGiftOfTheHunter` | `PassWithoutTrace` | 5 / — / — | native invocation / LongRest |
| `InvocationKinesis` | `Haste` | 5 / — / — | native invocation / LongRest |
| `InvocationShroudOfShadow` | `Invisibility` | 15 / — / — | native invocation / AtWill |
| `InvocationSpectralShield` | `ShieldOfFaith` | 5 / — / — | native invocation / AtWill |
| `InvocationStasis` | `Slow` | 5 / — / — | native invocation / LongRest |
| `InvocationTenaciousPlague` | `InsectPlague` | 9 / FeatureSetPactChain / — | native invocation / LongRest |
| `InvocationTrickstersEscape` | `TrickstersEscape` | 7 / — / — | native invocation / LongRest |
| `InvocationUndyingServitude` | `CreateDeadRisenSkeleton_Enforcer` | 5 / — / — | native invocation / LongRest |
| `InvocationVerdantArmor` | `VerdantArmor` | 5 / — / — | native invocation / AtWill |
| `MinionsChaos` | `ConjureElemental` | 9 / — / — | **Unsupported paid native invocation** |
| `MireMind` | `Slow` | 5 / — / — | **Unsupported paid native invocation** |
| `OtherworldlyLeap` | `JumpOtherworldlyLeap` | 9 / — / — | native invocation / AtWill |
| `SignIllOmen` | `BestowCurse` | 5 / — / — | **Unsupported paid native invocation** |
| `ThiefFiveFates` | `Bane` | 1 / — / — | **Unsupported paid native invocation** |

Snapshot inventory: **25** standard spell-grant invocations, из них **6** paid paths закрыты. Этот список — definitions, а не утверждение о selected/enabled invocations текущего героя. Native capture сканирует actual database.

## Остальные прямые источники

| Root / точный feature | Ownership / limits | Status |
|---|---|---|
| Legacy `FeatureSetPactTome` → `PointPoolPactTome` | Warlock / CastSpellWarlock / CHA; original3;3 cantrips из AllSpells, без нового repertoire | Registered attached grant |
|2024 `InvocationPactTome` → `PointPoolPactTome` | Explicit Warlock owner; required1;3 cantrips; не складывается автоматически с legacy | Registered invocation attachment |
| `BookAncientSecrets` → `PointPoolInvocationBookAncientSecrets` | Explicit Warlock owner;2 circle1 rituals, `PactTomeRitual`; required pact как в definition | Registered ritual-only; paid use blocked unless spell independently has ordinary origin |
| `MartialArcaneArcher` → `CastSpellMartialArcaneArcherArcaneMagic` | Actual Fighter3 prerequisite; own Race-origin INT repertoire; inherited High Elf cantrip mechanism | Registered Subclass auxiliary; не OneThird |
| `FeatEldritchAdept` → `PointPoolFeatEldritchAdept` | Native Invocation pool1, native level2 validator; не угадываем repertoire/class owner | UnsupportedSource, early rejection |
| Class-origin feat casting с null class | В available snapshots подтверждённых direct definitions нет; generic grant API такой случай допускает | Unknown/unsupported blocks before mutation |
| Background casting | Native DB scan features; snapshot не используется для выдумывания отсутствующего источника | Confirmed supported shapes only; unknown fails closed |
| Conditional/multiple caster / CantripOrSpell auxiliary choice | Требуется отдельный native variant journal | UnsupportedSource |

Native acquisition/finalization journal остаётся заблокированным до13.7. Здесь не заявлены game-runtime, save/respec или inventory tests. Полная acceptance matrix и результаты находятся в [MulticasterIntegrationPlan.md](MulticasterIntegrationPlan.md).
