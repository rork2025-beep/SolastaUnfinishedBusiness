# Stage13.5D /13.5E-1 — подтверждённые auxiliary spell sources

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

Зарегистрированы как Invocation roots с explicit Warlock casting feature. Это не добавляет Warlock level. RequiredLevel/RequiredPact/RequiredKnownSpell сохраняются из definition, прежние Warlock6/6.1 training validators не меняются. В13.5E-1 шесть `ConsumesSpellSlot=true` получили typed `PaidInvocation` backend; native activation имеет `PendingAtomicJournal` (матрица ниже). `FeatEldritchAdept` имеет отдельный `InvocationSelection` backend с feat-origin claims, native publication также закрыта. Это не разрешение mixed admission.

| Invocation | Spell | Required level / pact / known spell | Domain |
|---|---|---|---|
| `ArmorOfShadows` | `MageArmorInvocationArmorShadows` | 1 / — / — | native invocation / AtWill |
| `AscendantStep` | `LevitateBoots` | 9 / — / — | native invocation / AtWill |
| `BewitchingWhispers` | `DreadfulOmen` | 7 / — / — | PaidInvocation / **BLOCKED_PENDING_NATIVE_JOURNAL** |
| `ChainsCarceri` | `HoldMonsterInvocationChainsCarceri` | 15 / FeatureSetPactChain / — | native invocation / AtWill |
| `DreadfulWord` | `Confusion` | 7 / — / — | PaidInvocation / **BLOCKED_PENDING_NATIVE_JOURNAL** |
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
| `MinionsChaos` | `ConjureElemental` | 9 / — / — | PaidInvocation / **BLOCKED_PENDING_NATIVE_JOURNAL** |
| `MireMind` | `Slow` | 5 / — / — | PaidInvocation / **BLOCKED_PENDING_NATIVE_JOURNAL** |
| `OtherworldlyLeap` | `JumpOtherworldlyLeap` | 9 / — / — | native invocation / AtWill |
| `SignIllOmen` | `BestowCurse` | 5 / — / — | PaidInvocation / **BLOCKED_PENDING_NATIVE_JOURNAL** |
| `ThiefFiveFates` | `Bane` | 1 / — / — | PaidInvocation / **BLOCKED_PENDING_NATIVE_JOURNAL** |

Snapshot inventory: **25** standard spell-grant invocations, из них **6** paid paths закрыты. Этот список — definitions, а не утверждение о selected/enabled invocations текущего героя. Native capture сканирует actual database.

## Остальные прямые источники

| Root / точный feature | Ownership / limits | Status |
|---|---|---|
| Legacy `FeatureSetPactTome` → `PointPoolPactTome` | Warlock / CastSpellWarlock / CHA; original3;3 cantrips из AllSpells, без нового repertoire | Registered attached grant |
|2024 `InvocationPactTome` → `PointPoolPactTome` | Explicit Warlock owner; required1;3 cantrips; не складывается автоматически с legacy | Registered invocation attachment |
| `BookAncientSecrets` → `PointPoolInvocationBookAncientSecrets` | Explicit Warlock owner;2 circle1 rituals, `PactTomeRitual`; required pact как в definition | Registered ritual-only; paid use blocked unless spell independently has ordinary origin |
| `MartialArcaneArcher` → `CastSpellMartialArcaneArcherArcaneMagic` | Actual Fighter3 prerequisite; own Race-origin INT repertoire; inherited High Elf cantrip mechanism | Registered Subclass auxiliary; не OneThird |
| `FeatEldritchAdept` → `PointPoolFeatEldritchAdept` | Native Invocation pool1, native level2 validator; не угадываем repertoire/class owner | InvocationSelection / **BLOCKED_PENDING_NATIVE_JOURNAL**, early rejection |
| Class-origin feat casting с null class | В available snapshots подтверждённых direct definitions нет; generic grant API такой случай допускает | Unknown/unsupported blocks before mutation |
| Background casting | Native DB scan features; snapshot не используется для выдумывания отсутствующего источника | Confirmed supported shapes only; unknown fails closed |
| Conditional/multiple caster / CantripOrSpell auxiliary choice | Требуется отдельный native variant journal | UnsupportedSource |

Native acquisition/finalization journal остаётся заблокированным до13.7. Здесь не заявлены game-runtime, save/respec или inventory tests. Полная acceptance matrix и результаты находятся в [MulticasterIntegrationPlan.md](MulticasterIntegrationPlan.md).


## Stage13.5E-1 — paid invocation / Eldritch Adept adapters

Production: `Models/EldritchInvocationSourcesPlan.cs`, `Models/EldritchInvocationSourcesContext.cs`.
Registry читает DB после2024/Spell Points switches, через существующий auxiliary capture. Никакие definitions не меняются. Изменение ожидаемого spell/slot/recharge shape закрывает registry; неизвестная paid invocation остаётся `UNSUPPORTED`. Проверены available JSON и compiled native ABI; игра не запускалась.

| Invocation | Granted spell / минимальный circle | Native RequiredLevel | RequiredPact / RequiredKnownSpell / GrantedFeature | Backend | Native activation |
|---|---|---|---|---|---|
| `BewitchingWhispers` | `DreadfulOmen` /4 |7 |null /null /null |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| `DreadfulWord` | `Confusion` /4 |7 |null /null /null |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| `MinionsChaos` | `ConjureElemental` /5 |9 |null /null /null |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| `MireMind` | `Slow` /3 |5 |null /null /null |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| `SignIllOmen` | `BestowCurse` /3 |5 |null /null /null |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| `ThiefFiveFates` | `Bane` /1 |1 |null /null /null |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |

Все шесть: `ConsumesSpellSlot=true`, `LongRestRecharge=true`. Это invocation grants, не ordinary known Warlock spells. Native invocation-access feature — `CastSpellWarlock`, CHA; backend проверяет feature/ability отдельно от claims. Каст допустим только с подтверждённым invocation origin и native prerequisites. Native `RequiredLevel<=1` не проверяется против Warlock level в существующем GUI; feat имеет отдельный minimum character level2. Более высокие prerequisites используют original/pending Warlock progression, не total character level. RequiredPact/RequiredKnownSpell и успешная проверка дополнительных native validators входят в обязательный owner proof; proof не создаётся из selected class или общего spell list.

`PaidLedger` получает **тот же** `PactMagicResourcePlan.CastingLedger`, а не создаёт pools. `Prepare` не списывает ресурс; `Commit` повторно проверяет доступность и дебетует ровно один выбранный standard/Pact ресурс. Selected circle не меньше granted spell circle, Pact требует своего фактического circle. Standard расходует установленный slot или SP cost (1..5:2/3/5/6/7), без Arcanum и без конвертации Pact→SP. Automatic route использует существующую политику координатора (Warlock/prefer-Pact/Shift); вызывающий journal обязан передать проверенный circle, backend не повторяет небезопасный lowest-slot fallback.

Owner/action/origin/invocation/spell/circle/resource/preferences проверяются до списания. Повтор того же action возвращает receipt; конфликт отклоняется. Один native invocation `used` на definition, независимо от нескольких подтверждённых origins. Short rest восстанавливает Pact, не standard и не invocation use; long rest восстанавливает соответствующие budgets и invocation use; simulation/repeated rest не рефилят повторно. `RefreshOwner` сохраняет расход и receipts, инвалидирует pending actions; удалённый feat не оставляет действующего claim. Этот managed ledger должен принадлежать одному hero journal на всю сессию; новый ledger на refresh недопустим. Import `alreadyUsed` сохраняет имеющийся native use debt; автоматического импорта saves нет.

### Почему native активация закрыта

Compiled spend path:

`InvocationSelectionPanel` → `RulesetImplementationManager[Location].InstantiateEffectInvocation` → `RulesetEffectSpell.RulesetInvocation` → `RulesetCharacter.CastSpell` → `UseInvocation` → `RulesetInvocation.Consume(repertoire)` → `repertoire.SpendSpellSlot(GetLowestAvailableSlotLevel())`.

Шесть definitions не выдают power; native invocation action создаёт spell effect. Location override вызывает base instantiate **до** регистрации effect. UB postfix подставляет `invocationRepertoire`, если repertoire отсутствует. Native `GetSpellRepertoireForInvocations` выбирает реальный feature с `HasAccessToInvocations`, а не текущий класс. Existing `GetLowestAvailableSlotLevel_Patch`/Pact `Lowest` выбирают доступный circle без знания base circle данного invocation spell; при пустом Pact возможен слишком низкий standard circle. Existing SpendSpellSlot patch направляет ресурсы через shared/Pact engine, но обычный authorized-known-spell route не является proof для invocation.

Главное: `Consume` получает только repertoire, сначала ставит `used=true`, затем выбирает/списывает слот; action identity и атомарного возврата native side effects у него нет. `CastSpell` до вызова `UseInvocation` публикует события и выполняет другие действия. Поэтому четыре Priority.First guards (`InstantiateEffectInvocation`, `CastSpell`, `UseInvocation`, `Consume`) отклоняют unsafe paid operation **до соответствующей mutation**, с `PendingAtomicJournal`. Guard на base instantiate покрывает Location override. Это дополнительные guards, не замена Pact engine. Без13.7 нельзя безопасно вызвать backend и объявить native atomic success. Refund после committed spend **запрещён**; разрешена только отмена неизрасходованного prepared action. Проверенной native cancellation boundary не найдено.

### Eldritch Adept: ownership и выбор

Source: `OtherFeats.BuildEldritchAdept` → `FeatEldritchAdept` → `PointPoolFeatEldritchAdept`, Invocation amount1, `ValidatorsFeat.IsLevel2`. Ни casting feature, ни repertoire, ни Pact slots он не создаёт. Native UI `CustomInvocationSubPanel.OnlyStandardInvocationProficiencies` разрешает стандартные invocations по наличию feat, а `CharacterBuildingManager.TrainInvocation` хранит их в native trained inventory. Это не доказательство Warlock-origin.

| Selection case | Backend | Native selection/grant |
|---|---|---|
| Non-Warlock или Warlock: `DevilsSight` → `FeatureSetInvocationDevilsSight` |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| Non-Warlock или Warlock: `BeguilingInfluence` → `ProficiencyBeguilingInfluence` |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| Non-Warlock или Warlock: `EyesRuneKeeper` → `ProficiencyAllLanguages` |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| Warlock owner: одна из шести paid invocations, все prerequisites и реальный invocation-access repertoire |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |
| Non-Warlock: paid spell без invocation-access repertoire, включая `ThiefFiveFates` |UNSUPPORTED (`MissingRepertoire`; при недостающем required level — `MissingPrerequisite`) |UNSUPPORTED |
| Неизвестные / ещё не адаптированные power, spell, pact, custom-selection invocation options |UNSUPPORTED |UNSUPPORTED |
| Уже имеющийся native effective invocation выбирается повторно как новый feat award |UNSUPPORTED (`DuplicateOrigin`) |UNSUPPORTED |
| Два подтверждённых легитимных class/feat claims одной definition |SUPPORTED_BACKEND: оба origin сохраняются, один effective grant/use |BLOCKED_PENDING_NATIVE_JOURNAL; native reconstruction не выполняется |
| Удаление/замена feat без доказательства cleanup features/conditions/resources |UNSUPPORTED (`CleanupRequired`) |BLOCKED_PENDING_NATIVE_JOURNAL |
| Source-local Back/Reset/Cancel, повтор Commit; удаление после cleanup proof |SUPPORTED_BACKEND |BLOCKED_PENDING_NATIVE_JOURNAL |

`AdeptSelection` использует production `WarlockInvocationTransaction<string>` для feat-local one-award replay. Origin — отдельный acquisition ID, не текущий class tag; existing class claims не редактируются. Skip не создаёт незаполненный feat. Подтверждённое удаление снимает только feat claim, сохраняя class claim того же invocation. Повторное получение feat не очищает invocation use/resource debt. Реальные native feature removal, inventory/condition cleanup и respec rollback в этом этапе не реализованы.

Native `TrainFeat` складывает pool в текущий class tag + empty ExtraSpellsTag; при одновременном class reward источники могут слиться. Поэтому early root preflight закрывает Adept до pool/native feat grants, а `BeforeTraining` проверяет pending Adept до invocation pool helpers. Без source-aware journal нельзя угадывать происхождение одного выбора. Existing class Stage6/6.1 transactions не переписаны.

### Проверки и ограничения

Результаты финального прогона приведены в Stage13.5E-1 разделе `MulticasterIntegrationPlan.md`. Production backend тестируется с реальными compiled slot/SP tables и JSON prerequisites всех шести invocations, в обоих resource modes, Warlock-only /Warlock+Sorcerer /Warlock+Paladin и обоих acquisition origins. Отдельные compiled audits проверяют ABI и actual spend path; они **не выполняют** Harmony/native GUI/бой/отдых/save-load.

`MixedGameplayReady=false`; новых settings нет. При gate OFF ранний выход сохраняет обычные native invocation/feat paths, включая free/at-will invocations. Settings.xml, saves и game DLLs не меняются. M05-05 остаётся **PARTIAL**: backend gaps для данных paths сокращены, native journal и остальные auxiliary variants требуют отдельных этапов. Stage13.5E-2 не начат.

## Stage13.5E-2 — variants, conditional graphs и общий выбор

Этот раздел уточняет прежние blanket rejections multiple-caster/Exclusion/CantripOrSpell; исторические результаты13.5D/13.5E-1 выше сохранены. Native gameplay по-прежнему выключен. Источник инвентаря — все available JSON roots race/feat/style/background/subclass/invocation плюс registration/builders исходников, а не tabletop предположения. Unity DB здесь не выполнялась; runtime capture повторяет обход реального DB и закрывается при неподдержанном/неоднозначном graph.

### Точный инвентарь затронутых definitions

| Registered root / definition | Установленная структура и владение | Backend / native boundary |
|---|---|---|
| `MartialArcaneArcher` | Original3: независимый `CastSpellMartialArcaneArcherArcaneMagic` (Race origin, INT, `SpellListMartialArcaneArcherArcaneMagic`) и `FeatureSetMartialArcaneArcherArcaneLore` (Exclusion: `ProficiencyMartialArcaneArcherArcana` / `ProficiencyMartialArcaneArcherNature`) |SUPPORTED_BACKEND: skill exclusion не находится на пути cast; прежний blanket отказ устранён. Acquisition — PendingAtomicJournal |
| `ElfHigh`, `RaceHalfElfHigh` | Оба используют `CastSpellElfHigh`, INT / `SpellListWizard`; различные root identities |SUPPORTED_BACKEND, независимые origin IDs. Совпадение actual repertoire с несколькими подтверждёнными roots — AmbiguousTag до13.7 |
| `RaceDarkelf`, `RaceHalfElfDark` | Оба используют `CastSpellDarkelfMagic`, CHA / `SpellListDarkelf`; различные roots |SUPPORTED_BACKEND, тот же guard при ambiguous native identity |
| `BackgroundDevoted` | `BonusCantripsBackgroundDevoted`, Race origin, WIS, `SpellListBackgroundDevoted`, FixedList, known cantrips1, LongRest, нулевые slot rows |Подтверждённый background source, SUPPORTED_BACKEND; не дополнительный casting class |
| `PointPoolBardMagicalSecrets10` | Amount2, CantripOrSpell, min0/max9, `SpellListAllSpells`, tag `MagicalSecrets`, non-ritual; Bard original10 |Существующий Bard Secrets adapter сохранён |
| `PointPoolBardMagicalSecrets14` | Те же pool rules, Bard original14 |Существующий Bard adapter |
| `PointPoolBardMagicalSecrets18` | Те же pool rules, Bard original18 |Существующий Bard adapter |
| `PointPoolCollegeLoreAdditionalMagicalSecrets` | Те же pool rules; `CollegeOfLore`, original6 |Существующий Bard adapter |
| `FeatureSetPactTome`, `InvocationPactTome`, `BookAncientSecrets` | Native attached Warlock cantrip/ritual pools; class/invocation roots различаются |Пути сохраняются новым graph walker; counts3/3/2, native lists/tags/ritual-only policy. Prerequisites и Warlock ownership прежние |

Доступные snapshots содержат **60 Race-origin casting occurrences**: race16, feat40, style2, background1, subclass1. Их старый основной inventory выше дополнен здесь background и subclass. Среди этих roots: **0** multiple-caster cast graphs; **0** условных/exclusive ветвей, содержащих сам CastSpell; **0** multiple distinct native spell-pool tags под одним casting root. Единственный реально затронутый blanket Exclusion rejection — независимый Arcane Lore у ArcaneArcher. Совпадающих casting feature IDs между roots — ровно **две пары**, перечисленные выше. Это не утверждение, что один герой законно имеет обе race roots одновременно.

Всего registered snapshot `CantripOrSpell` definitions — ровно **четыре** Bard pools выше. Standalone auxiliary mixed pool в этих данных не обнаружен; production adapter проверен на тех же native pool descriptors и явно synthetic auxiliary fixtures. Source searches не нашли других constructors с CantripOrSpell. Динамически создаваемые UI pools известных классов принадлежат их уже существующим class-local transactions, не регистрируются повторно как auxiliary.

### Typed variants и один shared budget

`AuxiliarySpellSourceGraphPlan.Walk` сохраняет каждый child occurrence, Union и Exclusion вместо безусловного flatten. Variant хранит root, полный feature path, stable ID, branch prerequisites и predicate IDs. Path и branch IDs используют definition names + occurrence ordinal одинаковых siblings, **не переводимый title и не позицию в UI**; перестановка различных siblings не меняет IDs. Повторные одинаковые children сохраняют отдельные occurrences. Неизвестный mode, null/cycle/неподдержанная структура дают UnsupportedSource. Native `DeterminedByAncestry` не выдаётся за проверенный spell predicate: для cast descendants такой mode закрыт, реально наблюдённых casting descendants этого типа нет.

Source отдельно хранит native origin/feature, root owner, ability, authorized spell IDs/list ID, resource domain, все20 slot/known/cantrip rows, recharge и acquisition origin. `VariantState` — source-scoped proof owned roots, единственного selected child каждой relevant exclusion и результатов зарегистрированных conditions. Отсутствующий proof не означает выбор обеих ветвей. Неизвестный/double branch отвергается; false condition не выдаёт source; неизвестный predicate — UnverifiedCondition. Native получение такого proof/durable journal остаётся13.7. Никакие существующие save choices не импортируются автоматически.

Pools связываются с casting feature по ближайшей структурной области или точному установленному `FeatHelpers.SpellTag`/ExtraSpellsTag. Общие ability/list/selected class не являются evidence. Совпадающие tags при нескольких возможных owners дают AmbiguousTag. Root mismatch не допускается. Два подтверждённых независимых native источника одной casting definition сохраняют отдельные model origins; adapter не создаёт два native repertoires без binding proof.

`Pool.ChoiceKind.CantripOrSpell` содержит **один Amount=N**. Allowed list фильтруется по raw/native min/max, kind, override и ritual-only whitelist. Maximum0 сохраняет установленное UB значение «без явной верхней границы» для Spell/CantripOrSpell; Cantrip остаётся circle0. Raw maximum, ExtraSpellsTag и override сохраняются отдельно. N cantrips, N spells или смешанные N selections расходуют один бюджет; N+N запрещено. Один spell не удовлетворяет два independent pools одного source без явной duplicate policy; ни один установленный adapter такую дополнительную policy не включает. Одинаковый spell в двух легитимных разных source origins сохраняется отдельно.

Расширен **существующий** `AuxiliarySpellSourcesPlan.Selection`: Select/Preview/Back/Reset/Cancel/Commit/repeated Commit, revision checks, active source/pool variants. `SetVariants` инвалидирует только зависимые pending keys, сохраняет unrelated choices. Reset возвращает initial variant proof и confirmed choices. Подтверждённое изменение branch либо replacement существующего award требует PendingAtomicJournal; free retraining/respec не добавлены. Менять pending preview внутри исходного N можно без дополнительного award. Commit заново проверяет исходные/выбранные amounts, list/circle и duplicates.

### Итоговая supported / blocked матрица

| Shape | Backend | Native состояние |
|---|---|---|
| Наблюдённые60 native independent casting occurrences |SUPPORTED_BACKEND |Новая acquisition/finalization BLOCKED_PENDING_NATIVE_JOURNAL; прежние подтверждённые native budgets не превращаются в shared class pool |
| Независимый non-spell Exclusion рядом с cast |SUPPORTED_BACKEND на реальном ArcaneArcher graph |Skill choice не активирует/отключает cast |
| Union нескольких casting features |SUPPORTED_BACKEND, synthetic graph tests; фактических roots не обнаружено |Binding до выдачи каждого feature обязателен; незавершённый native commit blocked |
| Exclusive/conditional cast или pool |SUPPORTED_BACKEND при точном branch/predicate proof; synthetic tests |Без native proof — PendingAtomicJournal / UnverifiedCondition, не double grant |
| Два native casting features с различными явными spell tags |SUPPORTED_BACKEND, synthetic tests |При совпадающем/неразличимом execution tag — AmbiguousTag |
| CantripOrSpell |SUPPORTED_BACKEND: единый N; реальные4 Bard descriptors + synthetic auxiliary cases |Bard прежний путь; новые auxiliary acquisitions требуют13.7 |
| Shared casting ID у разных roots |SUPPORTED_BACKEND, реальные две пары; origins независимы |Неоднозначный native repertoire ownership — AmbiguousTag |
| Неизвестный ancestry/conditional graph mode |UNSUPPORTED, fail-closed |Нет native grants |
| Paid invocation / Adept поддержанные варианты13.5E-1 |SUPPORTED_BACKEND, прежние suites сохранены |BLOCKED_PENDING_NATIVE_JOURNAL |
| Остальные допустимые Adept options, ещё не адаптированные13.5E-1 |Остаются unresolved/UNSUPPORTED в текущем backend |Не переведены в «вне scope» ради PASS; M05-05 остаётся PARTIAL |

Native preflight объединяет acquired cantrips и spells только при одном подтверждённом shared-pool tag, проверяет сумму N и резервирует обе стороны tag. Неразличимый/empty/unbound native tag не угадывается. Ранний feat/style/root preflight требует branch proof до pool/feature helpers; pending invocation attachments и прямые grant helpers проходят существующие preflight guards. Native atomic commit/rollback, durable origins, save-load, respec и resource projection после этих операций **не реализуются** здесь.

Финальные counts/build и M05 acceptance — в §18 `MulticasterIntegrationPlan.md`. MixedGameplayReady=false; class/total/shared caps, Pact separation, Bard/third-caster и обычные gate-OFF paths сохранены. Никакая native игровая проверка не заявляется. Остановка после13.5E-2.
