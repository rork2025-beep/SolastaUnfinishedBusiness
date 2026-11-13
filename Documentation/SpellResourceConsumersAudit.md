# Stage13.6 — native conversions и resource consumers

## Evidence matrix до реализации delta

Baseline `fa9fd13d55d5be318065d0f4e9b18c1b7ca4e53d`: tracked clean, SDK9.0.320,16496 passed, Release Workflow0/0. Это повторный Cloud build/model run; Unity не запускалась.

| Criterion | Existing implementation / native hooks | Executable evidence | Remaining delta /13.7 |
|---|---|---|---|
|M06-01 |CompressedMulticasterProgressionPlan, Standard Allocation.StandardPortion, Pact Allocation/Budget; same owner bind |Calculator/Standard/Pact suites, class half/round-up tables |Добавить exact Wizard/Inventor examples; никаких новых standard/Pact pools |
|M06-02 |CastingLedger.Resolve/Spend; CastSpell scope, SpendSpellSlot, upcast/slots hooks |Pact cross-cast/Shift/preference/exhaustion/receipt tests |Reaction source UI/effect lifetime/native publication —13.7/13.8 |
|M06-03 |Standard Budget.Rest/Recover; Pact Budget.Rest/Recover; RecoverPower actual effect GUID |Standard/Pact rests, Cunning/Master tests |Recover/AddPoints currently generate Guid.NewGuid; typed operation identity required before native recovery |
|M06-04 |Pact Tables.Arcanum, SelectArcanum, native SpendMysticArcanum and projection |Exact6–9, independent grant/use/recovery suites |Durable selections/used state/rollback —13.7 |
|M06-05 |Stage6/6.1 transactions,13.5F Adept/catalog, auxiliary Tome/Book, isolated Inventor infusion planner |Original18/19/20, custom Surge/Boons, all68 Adept and source-choice regressions |Native atomic grants/cleanup —13.7; не переписывать transactions |

Матрица создана до изменения production code. Финальные результаты и source-specific acceptance ниже/в integration plan.

## Source facts, не tabletop rules

Native `RulesetCharacter.CreateSorceryPoints(level,repertoire)` сначала вызывает SpendSpellSlot(level), затем `usedSorceryPoints=max(0,used-level)`, затем event. `CreateSpellSlot` сначала SpendSorceryPoints(SpellSlotCreationCost[level]), затем InflictCondition(ConditionAdditionalSpellSlotN,UntilLongRest). Native creation dictionary:1→2,2→3,3→5,4→6,5→7. Это Sorcery Points, не Spell Points. GainSorceryPoints ограничивает возврат текущим used debt; native SpendSorceryPoints также вызывает magic-affinity notifications/модификаторы, поэтому нельзя считать всю native операцию rollback-safe.

UB alternate SP converts additional-slot conditions into **bonus Spell Points без capacity clamp**, затем удаляет condition. Standard Budget уже имеет bonus credit; slots mode должен иметь временную capacity до LongRest, без изменения class spell access.

UB SpendMulticasterWarlockSlots допускает Pact input для Flexible Casting через Shift/preference/exhaustion policy. При AlwaysSpendPactSlotsFirst=false legacy ветка может потратить Pact P за UI circle<=P; native Sorcery gain равен UI circle, не автоматически P. Typed request обязан явно указывать оба circle и разрешённую source policy. Это не разрешение оплатить preconstructed spell effect1 ресурсом Pact5: обычные spell/smite requests требуют согласованного selected effect circle.

`Behaviors/Specific/_GLBM.cs` отдельно проверяет EldritchSmite availability по Pact, FindClassHoldingFeature через ClassHolder.Warlock, затем reaction SpendSpellSlot. Divine Smite2014 зависит от оружия/toggle/target и таблицы dice;2024 идёт через spell/power path и не должен дополнительно вызывать legacy trigger. Нет generic combat rollback.

Native/mod call-site inventory получен Cecil по обеим assemblies, включая iterator MoveNext, а не только поиском исходников. Game IL не коммитится.

## Восстановление среды и область доказательств

После `No usable execution environment is available` shell действительно повторно проверен (`pwd`, Git status/branch). Ветка `feature/progression-20-to-12`, HEAD `fa9fd13d55d5be318065d0f4e9b18c1b7ca4e53d`. Найдены и сохранены четыре draft-файла; наличие и правильность не предполагались. Для повторного baseline tracked drafts временно сохранены вне checkout, компилировался HEAD без нового `.cs`, затем drafts восстановлены в `finally`. Повторный SDK9.0.320/build0/0/**16496 checks** подтверждён. Worktree/release не создавались/не менялись. Переустановка зависимостей не потребовалась.

Аудит использует final UB source, serialized installed blueprints и Cecil native/mod call sites, включая iterator `MoveNext` и generated closures. Blueprint inventory не равен запуску Unity DatabaseRepository: unknown/changed definitions или новые варианты другого мода требуют fresh descriptor и fail-closed diagnostic. Raw IL, game DLL и временные logs не включаются.

## Production contract

`SpellResourceOperationPlan` — одна facade на существующий `PactMagicResourcePlan.CastingLedger`: те же **Standard Budget и Pact Budget**, не второй spell engine. Её `Counter` — проверяемый managed снимок native Sorcery/power/item счётчика, не новый `RulesetUsablePower`, Pact pool или PowerSpellPoints. Native binder здесь отсутствует. `Source`, `Proof`, `Request`, `Receipt` неизменяемы; source revision включает ID, owner, domains, policy/limits/dice/cost/recharge. Proof отдельно содержит настоящий repertoire/item, counter identities, effective source level, eligibility, verified Pact policy и дополнительные native activation debits.

Фазы: `Prepare` проверяет свежий owner/source/rules/resource fingerprint и прогнозирует без resource mutation; `CommitManaged` исполняет resource arithmetic и создаёт один receipt; source claim в existing CastingLedger запрещает повторно истолковать тот же logical operation как ordinary cast; повтор возвращает тот же receipt, конфликт/устаревшее действие отклоняется. Cancel до commit не списывает ничего, после commit не изобретает rollback. Native `CommitNative` всегда `PendingAtomicJournal`. Составная стоимость (slot + ограниченный power, либо Sorcery + power) проверяется **вся до debit**. Ни в одном request нельзя одновременно оплатить стандартный и Pact ресурс. Alias/duplicate counters без отдельной доказанной политики запрещены.

Rest имеет устойчивую identity; simulation не меняет state. Старый callback после rest не создаёт ресурс повторно. `RefreshOwner` допускает только проверенное расширение class/native-capacity snapshot с сохранением spent debt и receipts, максимум3 classes; class removal, decreased counter capacity, refill под видом refresh — rejection. Это не native level-up/respec/save API. `RefreshProofs` инвалидирует pending requests. One-facade latch предотвращает потерю receipts при повторной инициализации.

Domain inventory: StandardSlots, SpellPoints, PactSlots, Arcanum (**не конвертируется**), SorceryPoints, ClassPower, InvocationUse, FreeSpellUse, ItemCharges, StoredSpellUse, ItemFunctionUses, ItemQuantity, Effect. Последние два native домена подтверждены `UseDeviceFunction`/`AccountFunctionUse`: consumable stack и отдельные iterations/cooldown не являются общими item charges. Hero resources принадлежат original hero; item counters — конкретному item GUID/function, а не классу создателя или текущего держателя. Actual holder/attunement/function eligibility проверяет fresh native proof, не общее наличие caster class.

## Подтверждённые native conversion/consumer families

В таблице backend **SUPPORTED_BACKEND** означает выполненную production-model проверку resource route. Для **каждой** строки native publication **BLOCKED_PENDING_NATIVE_JOURNAL**. Существующие корректные обычные cast/Pact/Arcanum/paid-invocation ledgers повторно не реализуются.

| Initiator / точный source path | Owner / input → output, cost | Native mutation / recovery / identity | Typed backend |
|---|---|---|---|
|`FlexibleCastingModal.OnConvertCb` → IMagicCommandService → LocalCommandManager / NetworkingManager → `RulesetCharacter.CreateSorceryPoints` |Sorcerer repertoire; explicitly selected ordinary slot/SP or authorized Pact → Sorcery; gain=UI circle, capped by own Sorcery capacity |Сначала slot spend, затем usedSorceryPoints clamp/event; command не передаёт action GUID. No rollback; Shift callback timing тоже требует13.7 |`FlexibleCasting(false)`; actual resource circle отдельно от UI circle; no Arcanum/fallback |
|Та же chain → `RulesetCharacter.CreateSpellSlot` |Sorcery → temporary slot1–5; costs2/3/5/6/7 из native cctor |Сначала SpendSorceryPoints, затем AdditionalSpellSlot condition UntilLongRest; stable op нужен **до** spend |`FlexibleCasting(true)`; slot capacity или bonus SP credit, one grant/receipt |
|`SpellPointsContext.ConvertAdditionalSlotsIntoSpellPointsBeforeRefreshSpellRepertoire` |Temporary slot → bonus SP по installed SpellCostByLevel, без native maximum clamp |Condition удаляется после credit; condition GUID сам по себе ещё не доказывает оплаченное создание |Existing standard Budget extended; `PrepareRefresh` blocked before credit/removal без funding journal |
|`GLBM.HandleAdditionalDamageOnCharacterAttackHitConfirmed` SpendSpellSlot branch; native `CharacterActionSpendSpellSlot.ExecuteImpl` |Divine Smite2014: actual Paladin/native-feature owner; explicit ordinary/SP/Pact выбранного circle → damage intent |Weapon/target/ranged subclass/critical/once rules native; legacy skipped при активной2024 spell variant. Reaction verdict/effect identity должны быть одни |`Smite(false,nativeDice)`; 2/3/4/5 base dice по original table, cap5;2024 ordinary spell route остаётся существующим |
|Та же branch, `AdditionalDamageInvocationEldritchSmite`; `FindClassHoldingFeature`, `ClassHolder.Warlock` |Warlock/native invocation; **Pact only**, selected Pact circle → native damage intent |Не ordinary known spell и не free invocation; no unrelated standard/SP fallback |`Smite(true,nativeDice)`; Pact5 →6 base dice; no fake circle1 effect |
|`GameLocationBattleManagerPatcher` ReduceDamage SpendSpellSlot case |Actual `feature.SpellCastingClass` repertoire; chosen circle → native reduction |Reaction first, reduction after validated choice; no generic damage rollback |`SlotEffect("blade-dance-reduction", nativeFeature, actualOwner)` |
|`WizardAbjuration.CustomBehaviorRechargeArcaneWard.OnPowerOrSpellFinishedByMe` |Wizard slot → native Ward counter, gain=2×selected circle |Reaction/visual effect, then UpdateUsageForPower; preflight before effect required |`WardRefill`; real Ward capacity, no full refill shortcut |
|`ClassFeats.ActionFinishedByMeFeatSlayTheEnemies` |Ranger repertoire; exact PowerFeatSlayTheEnemies1/2/3 circle **plus** real PB-based native power-pool use → condition effect |Power finishes before slot debit; actual PB не масштабируется effective class level |`SlotEffect("slay-enemies",...)`; fixed source circle and separate activation debit |
|`2024DruidContext.CustomBehaviorPowerDruidWildResurgenceShape` |Druid; native TurnStart power use + lowest explicitly selected legal slot → one Wild Shape use |Spend slot then UpdateUsageForPowerPool(-1); source flag/HasPower and effective milestone5; original hero owner |`SlotTrade("wild-shape-refund",...)` + explicit initiating-use counter |
|`PowerDruidWildResurgenceSlot` / CustomBehaviorSpendWildShape |Wild Shape use + own LongRest activation use → additional circle1 slot |Condition and Wild Shape counters different native effects; cannot treat as single free slot |`WildShapeToSlot` + activation debit, one temporary credit |
|`2024PaladinContext.CustomBehaviourPaladinRechargeLv20Power` |Actual Paladin original20; circle5 → one use of exact installed Oath capstone |Native dynamically registered `PowerRecharge{power.Name}`; Spend5 then UpdateUsage(-1) |`SlotTrade("paladin-capstone-refund",...)`, target proof must be exact native power |
|`InnovationArtillerist.CustomBehaviorRefundCannon` |Inventor; chosen lowest legal slot → **+1 per actual matching ...Activate power** |Native RepayPowerUse → RepayUse caps **one**, not full. Refund loop runs **before** slot spend; action preflight must precede loop |`SlotTrade("cannon-refund",...)`, independent target IDs; full refill rejected by expected result |
|`InnovationAlchemy.CustomBehaviorRefundAlchemyPool` |Inventor fixed native PowerInnovationAlchemyRefundFromSlot1…5; slotN →N Alchemy uses |Callback adds without clamp, но native CanUsePower требует N≤used; legitimate activation cannot overflow |`SlotTrade("alchemy-refund",...)`; fixed N, enough native debt required |
|`InnovationVitriolist.CustomBehaviorRefundMixture` |Inventor repertoire; reaction slotN →N Mixture pool uses |PrepareAndReactWithSpellUsingSpellSlot spends through native action, update after verdict |`SlotTrade("mixture-refund",...)`; independent native pool/circle |
|`RangerMarkContext`, `PowerFavoredEnemyHuntersMark` |Actual free power counter → Hunter's Mark effect; zero standard/SP/Pact |Parent power/action only; CastNoCost child не второй power use. Existing RangerMark ledger preserved |`FreeMark`; no paid fallback unless separate authorized ordinary spell request |
|`InventorClass.LateLoadSpellStoringItem`, `BuildCreateSpellStoringItemPower` |Inventor original11/actual8; exact shared native storing-power pool → item creation intent |After Innovation spell lists constructed; unique/owned permanent item effect, later destruction/cancel not atomic |`StoredItemCreation`; `DescribeStoredCreation` verifies actual shared pool and spell1/2; existing Inventor safety untouched |
|`RulesetCharacter.UseDeviceSpell/UseDevicePower` → virtual `UseDeviceFunction`; Hero override |Specific item/function; native quantity, charges, function uses or at-will → item effect |Base dispatches ItemUsed event **before** hero override spends/destroys/AccountFunctionUse. Extra charge helper is another callback, not another free activation |`DeviceFunction`; device Usage is authority, not Function.ParentUsage. Exact item GUID/holder proof; no creator/user class pool |
|`RulesetItemDevice.Recharge` / function expiration/recharge |Authorized native item roll/elapsed-time result → capped native item/function resource |Random recharge, Dawn and cooldown expiry cannot be guessed from long rest; stable event/roll identity required |`DeviceRecovery`; bounded native result/event proof consumed once, None/SSI refill denied |
|`RulesetCharacter.UsePower`; GameCampaignCharacter/functors, CharacterActionSpendPower, UsePower, CounterAttackWithPower, ReturnMissile, PowerBundle rest/callbacks |Actual usable power and native pool ID, cost modifiers/recharge → native power effect |API itself may use Sorcery or slots. Choice not inferred from generic power; activation may be post-effect |`Power` for native counter domains, `SlotPower` for proven slot cost; exact proof required; no pool fabrication |
|`RulesetCharacter.CastSpell`, RulesetCharacterPatcher.SpendSpellSlot, ordinary reaction spells |Actual owned repertoire/effect GUID; ordinary or Pact resolved by existing coordinator |Keep source ability/DC/upcast, Shift/preference and exhausted fallback; no new resource engine |Existing `CastingLedger`, Standard/Pact scopes; all previous tests retained |
|`RulesetInvocation.Consume`, invocation power/action paths |Exact invocation owner and existing paid/free/native domain |used flag is separate from budget; native may set it before slot spend |Existing13.5E-1/F plans, six paid + full Adept catalog; no new interpretation |
|`LocalCommandManager.RecoverSpellSlots` → `RecoverMissingSlots`; native/mod ApplySpellSlotsForm |Exact class power and chosen recovery dictionary → ordinary resources only, or explicit Pact recovery |Modal callback has dictionary/repertoire but loses power/action GUID. Old random-GUID retry receipts removed for bound prototype |`Recovery`, `PactRecovery`; missing native origin now PendingAtomicJournal |

### Direct Sorcery call-site inventory

- `RulesetCharacter.ActivateMetamagic` и SorceryPoints-recharge `UsePower`: native effective cost, actual option/power ownership. `Sorcery("metamagic")` / `Power` никогда не расходуют Spell Points.
- `SorcerousPsion.CustomBehaviorMindSculpt`: one Sorcery Point, tag/effect eligibility; damage modifications precede finished callback. `Sorcery("mind-sculpt")` enforces1.
- `CustomBehaviorSupremeWill`: native ShortRest power **and**2×effect circle Sorcery Points; `Sorcery("supreme-will")` has formula plus separate power debit. Concentration choice/tag must be preflighted before spell effect.
- `ClassesContext.TryAlterOutcomeAttributeCheckSorcererMagicalGuidance`:1; `MetamagicBuilders.TryAlterOutcomeAttackMetamagicSeeking`:2, native reaction verdict. Fixed costs enforced.
- `MetamagicBuilders.MagicEffectInitiatedByMeTransmuted`: SpendSorceryPoints(-1), i.e. authorized refund1, not negative shared cost. `Sorcery("transmuted-refund")` gains once with own cap.
- `SorcerousWildMagic.ApplyWildSurge`: Spend(-UsedSorceryPoints) resets own Sorcery debt; `Sorcery("wild-magic-reset",AllMissing)`.
- `Level20SubclassesContext.RollSavingThrowFinishedManaOverflow`: successful native save gives1; `Sorcery("mana-overflow")`, source/save receipt required.
- `RulesetImplementationManager(Patcher).ApplySpellSlotsForm`: fixed gain, rounded Sorcerer effective half gain, native target power recharge; inventory below. UB overrides native CharacterLevel half calculation with **SorcererProgressionContext.EffectiveLevel**, retained.
- `RulesetCharacterPatcher.CreateSorceryPoints/GainSorceryPoints` OriginalForm forwarding: same original hero, not a second resource ledger. Existing Druid owner resolution retained.
- Native `SpendSorceryPoints` also calls AccountUsedMagicAndPower, HealingPerSpentSorceryPoint Heal, and SorceryPointsAltered handlers. Resource receipts **do not** execute/roll back healing/events. These are mandatory13.7 effect-publication boundaries, not unmodelled extra spell pools.

Generic native power callback family observed in Cecil: CircleOfTheCosmos ConstellationForm, CircleOfTheNight WildShape, DomainDefiler DyingLight, DomainTempest DestructiveWrath, MartialWarlord CoordinatedAssault, RoguishSwashbuckler MasterDuelist, WayOfZenArchery ZenShot, WizardWarMagic PowerSurge, SpellBuilders RingOfBladesFree, RaceImp ImpishWrath, RaceWyrmkin ReactiveRetribution, PowerBundle.SpendBundledPowerIfNeeded/FunctorUseCustomRestPower и CharacterActionSpendPower patch. They retain **native** pool identity/recharge/conditions through the same typed `Power` contract; no automatic conversion or new class contribution. Ki/Rage or unrelated class definitions are not rewritten. An unknown native pool/activation sequence must fail closed rather than infer ownership.


### Все 15 установленных SpellSlots-form powers

| Native definition | Form / result | Native owner / typed source |
|---|---|---|
|`PowerCircleLandNaturalRecovery`|RecoverHalfLevelUp; max circle 5|Druid / Recovery|
|`PowerDivineHeartDivineFount`|GainSorceryPoints 1|Sorcerer / SorceryRecovery|
|`PowerSorcererChildRiftOffering`|GainSorceryPoints 1|Sorcerer / SorceryRecovery|
|`PowerSorcererCreateSorceryPoints`|CreateSorceryPoints|Sorcerer / FlexibleCasting|
|`PowerSorcererCreateSpellSlot`|CreateSpellSlot|Sorcerer / FlexibleCasting|
|`PowerSorcererHauntedSoulRechargeVengefulSpirits`|RechargePower → PowerSorcererHauntedSoulVengefulSpirits|Sorcerer / HauntedRecharge; native input4 Sorcery Points|
|`PowerSorcererHauntedSoulSoulDrain`|GainSorceryPoints 1|Sorcerer / SorceryRecovery|
|`PowerSorcererManaPainterDrain`|GainSorceryPoints 1|Sorcerer / SorceryRecovery|
|`PowerSorcererManaPainterManaOverflow`|GainSorceryPoints 2|Sorcerer / SorceryRecovery|
|`PowerSorcererManaPainterTap`|RecovererSorceryHalfLevelUp|Sorcerer / SorceryRecovery|
|`PowerSorcererSorcerousRestoration`|GainSorceryPoints 4|Sorcerer / SorceryRecovery|
|`PowerSorcerousRestoration`|9000|Sorcerer / SorceryRecovery|
|`PowerSpellMasterBonusRecovery`|RecoverHalfLevelUp; max circle 9|Wizard / Recovery|
|`PowerWarlockEldritchMaster`|RecoverHalfLevelUp; max circle 5|Warlock / existing PactRecovery(master)|
|`PowerWizardArcaneRecovery`|RecoverHalfLevelUp; max circle 5|Wizard / Recovery|

`9000` — фактический ExtraEffectType.RecoverSorceryHalfLevelDown. Fixed gains берутся из final effect form, rounding — из UB patch, а не native CharacterLevel. У activation дополнительно остаются target/hit/visibility/action и real power-use prerequisites. Их `eligible` proof нельзя заменять одним наличием класса.

### Все 11 powers с native RechargeRate.SorceryPoints

| Definition | Native CostPerUse |
|---|---|
|`PowerSorcererChildRiftDeflection`|1|
|`PowerSorcererDraconicElementalResistance`|1|
|`PowerSorcererHauntedSoulRechargeVengefulSpirits`|4|
|`PowerDivineHeartEmpoweredHealing`|1|
|`PowerSorcererDraconicBloodlineAweOrFearPresence`|5|
|`PowerSorcererSorceryIncarnate`|2|
|`PowerSorcerousFieldManipulatorForcefulStepPoints`|4|
|`PowerSorcerousPsionPsychokinesisPoints`|1|
|`PowerSorcerousSorrAkkathTouchOfDarknessPoints`|4|
|`PowerSorcerousSpellBladeManaShieldPoints`|2|
|`PowerSorcerousWildMagicBendLuck`|2|

Все11 проходят production `Power(...,SorceryPoints)` с реальной ценой. Haunted Recharge отдельно проходит composite restore route, который объединяет эту цену и recharge target в одном operation ID; нельзя выполнить обе routes как два независимых платежа за одну action. Нулевых serialized `RechargeRate.SpellSlots` powers найдено; native UsePower поддерживает эту ветку, поэтому существует typed `SlotPower` adapter, а неизвестный owned descriptor по умолчанию не допускается.

### Полная матрица наблюдаемых item shapes

Проверены все serialized usable-device definitions обоих blueprint roots: **шесть** effective `(device.Usage, function.UseAffinity)` shapes. `function.ParentUsage` использовать как эффективный режим нельзя.

| Device Usage / affinity | Typed domain / cost | Recharge / lifetime |
|---|---|---|
|Single / AtWill |ItemQuantity, one stack item |Native SpendStack или DestroyItem; не создание/удаление managed inventory |
|Charges / ChargeCost |ItemCharges либо verified StoredSpellUse; native UseAmount + verified overcharge |Device-level shared charges, no duplicated per-function pool |
|Charges / AtWill |Same native counter; actual native UseAmount, including zero |Не назначать стоимость заклинания по circle |
|ByFunction / IterationPerRecharge |ItemFunctionUses, one native iteration |Native function-specific limit и recharge |
|ByFunction / RechargeDuration |ItemFunctionUses, explicit available token; one action |Native cooldown expiry proof + stable event ID; long rest не заменяет время |
|ByFunction / AtWill |Effect→Effect, zero resource debit |Native holder/attunement/action restrictions остаются; no standard/Pact cost |

SSI generated spell wands (включая доступные Innovation spell definitions) отдельно прогоняются с **6 charges**, стоимостью1, device recharge **None**, Destroy on last charge и native spell source. Their ability/DC/attack **BasedOnItemSummoner**, не caster contribution держателя. Exhaustion не переключает на creator/user slots. Заряды обычного предмета с LongRest recharge восстанавливаются по native Recharge roll/bonus/cap, **не автоматически до maximum**. `Counter.Rest` не refills ItemCharges/StoredSpellUse/ItemQuantity; `DeviceRecovery` требует заранее доказанного результата native roll/time event, exact operation ID, captured maximum result и точного recharge policy и item identity. Повтор receipt не reroll; источник с None не получает recharge adapter.

## Ограничения и native guard matrix

| Boundary | Native status / why |
|---|---|
|CreateSpellSlot / CreateSorceryPoints |BLOCKED_PENDING_NATIVE_JOURNAL before native debit; no action GUID in method signature |
|Power creation / action execution / UsePower fallback |BLOCKED_PENDING_NATIVE_JOURNAL; pre-effect proof must cover own use plus secondary slot/Sorcery input and callback side effects |
|Item action / UseDeviceFunction |BLOCKED_PENDING_NATIVE_JOURNAL; OriginItem checked explicitly; dispatch/event, inventory quantity, charge roll, destruction must commit once |
|SpendSpellSlot reaction action |BLOCKED_PENDING_NATIVE_JOURNAL; same prompt/trigger/effect source and selected circle required |
|Sorcery Spend/Gain APIs |BLOCKED_PENDING_NATIVE_JOURNAL; includes native notifications/healing and original-form forwarding. A late spend guard is **not** proof that earlier damage/effects were rolled back |
|RecoverMissingSlots / AddPoints |BLOCKED_PENDING_NATIVE_JOURNAL; no random new IDs; original power identity/recharge use must survive GUI selection |
|Additional-slot condition conversion |BLOCKED_PENDING_NATIVE_JOURNAL before credit/removal; condition GUID requires funding linkage, not automatic free import |
|Existing ordinary cast/Pact/Arcanum/invocation plans |Existing backend retained; overall mixed runtime still disabled. This stage does not enable native atomic finalization |

Gate OFF exits every new native guard before source/resource lookup; ordinary single-caster UB execution and all existing class adapters retain their path. Gate ON is **not available**: constant `MixedGameplayReady=false`, no public setting/binder/admission change. Protection hooks are conservative handoff boundaries, не finished UI rejection flow. Descriptor readers не grant powers/repertoires, не создают pools и не вызываются автоматически для saves.

Native counters remain snapshots. `RefreshOwner` intentionally rejects native resource **capacity decreases/class removal**, stale debt and unknown states until13.7 can validate/rebind them atomically. Это документированный fail-closed запрет, а не обещанная respec support. Actual native cost modifiers, prerequisites, long-rest/dawn/turn clocks, item lifetime/ownership and typed source provenance must be supplied as verified fresh proofs by the future binder; a managed `eligible=true` alone is not a native verification result.

## Acceptance и Stage13.7 handoff

| Criterion | Backend acceptance | Evidence / remaining native work |
|---|---|---|
|M06-01 |PASS_BACKEND |W12/Wizard8 index11/73, W12/Inventor8 index6/32; Pact4x5/four Arcana; solo/martial/three-class/slots/SP suites; same coordinator objects |
|M06-02 |PASS_BACKEND |Existing normal/reaction/upcast/Shift/priority/exhaustion/DC/ability/receipt suites plus nonordinary exact circle checks. Native UI/effect publication BLOCKED_PENDING_NATIVE_JOURNAL |
|M06-03 |PASS_BACKEND |Standard/Pact rest separation, Cunning/Master original effective levels, native-use recovery limits, authoritative recovery preview with bonus/debt, stable callbacks. Native modal identity projection BLOCKED_PENDING_NATIVE_JOURNAL |
|M06-04 |PASS_BACKEND |Existing exact6/7/8/9 selected/used/long-rest state; no generic slots or conversion. Durable state BLOCKED_PENDING_NATIVE_JOURNAL |
|M06-05 |PASS_BACKEND |All original Stage6/6.1, Tome/Book/Boons, custom Surge, prerequisites, Adept claims, Infusion isolation and13.5F suites unchanged. Native grants/debt persistence BLOCKED_PENDING_NATIVE_JOURNAL |
|M06-06 |PASS_BACKEND |Both conversions, smites, source-specific trades,15 form definitions,11 native Sorcery power definitions, every observed direct Sorcery call-site family, six device shapes and generated SSI charges covered by tested typed adapters. Native sources/events/effects remain BLOCKED_PENDING_NATIVE_JOURNAL |

PASS means backend **resource-domain planning/arithmetic** for identified installed families; it does not claim combat, native activation, GUI, inventory persistence, rest, Harmony execution, save/load or respec success. Unknown/changed profiles, ambiguous counters/tags, missing class/repertoire/item/activation proofs, illegal resource domains/circles and stale operations stay rejected, not relabelled out-of-scope. No identified installed family remains silently routed through an ordinary-spell fallback.

Stage13.7 must:

1. Maintain exactly one original-hero binding and conversion facade, including Druid substitute/form/reversion; never bind a summon as its owner or import old saves automatically.
2. Assign action/effect/trigger GUID **before** costs/conditions/damage, preserve source class, actual feature/power/item/function identity, acquisition origin, and verified native predicates. Current descriptor readers are not a complete live binder.
3. Capture complete native effective costs/remaining uses/shared pool and every additional activation debit; bind source-rule fingerprints and settings revision. Fixed cost mismatch must fail before mutation.
4. Atomically debit and publish **all** native side effects: Sorcery healing/events; additional-slot condition create/credit/remove/expiry; smite damage/reaction verdict; summons/power targets; item charges/stack decrement/random final-charge destruction. No imaginary generic rollback.
5. Project temporary slot capacity to native repertoire/UI without double-counting condition modifiers. Pure Budget can spend new temporary slots; native projection/funding lifecycle remains intentionally blocked. SP credits do not unlock new class spell circles.
6. Retain GUI recovery source-power identity, chosen dictionary and already-paid native-use limits through command/network callbacks. Shift state is currently registered after Flexible Casting command; do not guess its earlier intent.
7. Treat native recharge roll/time/turn/long-rest events as stable receipts, not arbitrary `Guid.NewGuid` retries or free reset. Refund/recovery cannot delete spent debt or refill unrelated pools.
8. Serialize and rebind source-scoped claims/choices/resources, native invocation `used`, Pact/Arcanum/Sorcery/standard/item counters, and effects. Existing confirmed-choice removal, capacity decreases, feat/respec cleanup and rollback require explicit validated journals, not this expansion API.
9. Validate all of the above in Unity before Stage13.8 admission. This stage does not provide native durable journal, migration, release switch, respec or inventory rollback.

### Ручные проверки после готовности native journal/admission

Только новые test heroes, отдельные saves, SP ON/OFF и актуальные2014/2024 configurations:

1. W12/Wizard8, W12/Inventor8, W12/Sorcerer8, W12/Paladin8, W/Fighter, three classes: one standard pool, exact Pact/Arcanum capacities; class spell access/CHA/INT/DC remain independent.
2. Normal/reaction casts and upcast: Warlock/non-Warlock repertoire, Shift and AlwaysPactFirst, both/one/neither budget available; duplicate callback and cancellation. Pact5 cannot pay prebuilt effect1.
3. Both Flexible Casting directions across1–5; higher ordinary input where installed; full Sorcery capacity, insufficient funds, explicit Pact UI/native circle policy; created slot/bonus SP, short/long rest and expiry. No Arcanum conversion or level6–9 creation.
4. Legacy/2024 Smite exclusion, Eldritch Smite actual Warlock ownership/Pact-only, critical/target/weapon/once effects. Cancel/failed hit must not publish free damage or double debit.
5. Arcane/Natural/SpellMaster recovery, native once-use limit, Back/Cancel/duplicate command; Cunning and Master, short/long/simulated rest. No cross-refill of standard/Pact/Arcanum.
6. Free Mark parent and cloned CastNoCost child; zero standard/SP/Pact cost, exact native-use count, exhausted free power has no guessed fallback; concentration/target effects unchanged.
7. Wild Resurgence both directions and own power-use limits; Paladin5-slot recharge, all cannon Activate targets (+1 each, including Fortified Position), Alchemy fixedN/native debt guard, Vitriolist reactions, Ward2×circle, Slay the Enemies PB pool and fixed1/2/3 circles.
8. Sorcery power costs, Psion source tags/Supreme Will two costs, Seeking/Guidance, Transmuted refund, Wild Surge reset, Mana Overflow and native spend-triggered healing. Repeat, interrupted and failed activation must preserve actual debt/effects.
9. SSI at Inventor8 and each Innovation bonus option: create/cancel/remove/charge6→0, item held by another hero, source-owner GUID, summoner DC/ability, last-charge destruction, shared power pool uniqueness. Check infusions, cannons/constructs and respec early guards unchanged.
10. Device Single stack vs charge pool vs function iterations/cooldown vs at-will; overcharge and native recharge rolls, Dawn vs LongRest, attunement, item transfer and failed actions. No borrowed class resources or duplicate inventory.
11. Save/load, location change, repeated rests, Druid transformation/reversion, interrupted callbacks, changed rules, rejected old heroes, level-up Back/Cancel/Reset and respec: exact stable ownership/debt/cleanup; no automated migration. None of these native tests ran in Codex.

### Не-Sorcerer владельцы native Sorcery attribute / Metamagic

Дополнительно проверены реальные источники, которые не должны отбрасываться по отсутствию Sorcerer class:

- `OtherFeats.BuildMetamagicAdept`: FeatMetamagicAdept, PointPoolFeatMetamagicAdept2 choices, native Metamagic toggle, AttributeModifierSorcererSorceryPointsBonus2. Несмотря на имя Bonus2, actual operation — **AddHalfProficiencyBonus**, не фиксированные2 и не effective class level. MustCastSpells/actual level2 validator остаются native prerequisites. Backend использует **существующий native Sorcery attribute** original hero с его фактической capacity; не добавляет Sorcerer, repertoire, Pact или caster contribution.
- Generic native metamagic, Seeking и Transmuted refund допускают verified trained-option ownership через feat **или** class. Поэтому эти три adapters не требуют вымышленного Sorcerer class owner. Actual native option/cost/trigger proof обязателен. Class-specific Psion/Guidance/Surge/ManaPainter routes сохраняют своё class/source ownership.
- `DomainOrder.EmbodimentOfLaw`: MetamagicEmbodimentOfTheLaw имеет cost0 и native validator; отдельный PowerDomainOrderEmbodimentOfLaw расходуется при initiation. Реальный builder задаёт **WIS-based LongRest uses**, несмотря на комментарий PB/day. AttributeModifierDomainOrderSorceryPointsMinimum ForceIfBetter1 нужен native UI, не создаёт Sorcerer class. `EmbodimentOfLaw` adapter расходует только родной power counter и сохраняет фактический ресурс/ability. Trained option/grants и native counter cleanup остаются13.7.

У Item recharge immutable source хранит native outcome **bound**, а fresh proof — один verified result и **совпадающий operation/event ID**. Разные Dawn rolls допускаются через RefreshProofs без переинициализации facade/receipt loss. Попытка использовать прошлый outcome с новым retry ID или результат вне native bound отклоняется до mutation. Это managed contract, не реализованный native clock/roll journal.


`ResolveFlexibleInput` воспроизводит actual SpendMulticasterWarlockSlots policy для conversion по availability существующего coordinator, Shift, AlwaysPactFirst и родному repertoire. Fresh proof фиксирует выбранные domain, resource circle и native UI circle; запрос с другими значениями отклоняется. Проверены все восемь сочетаний source/Shift/preference в обоих режимах, отдельно разрешённый lossy case. Conversion не маскируется под известное заклинание.


## Финальная валидация и изменённые файлы

- Baseline HEAD13.5F: `fa9fd13d55d5be318065d0f4e9b18c1b7ca4e53d`, **16496** passed, SDK9.0.320, Release Workflow0 errors/0 warnings.
- Финальный `dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2`: **0 errors /0 warnings**.
- `dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj`: **17603 checks passed**, прирост **1107**. Новый Stage13.6 блок: **895 executed production-model checks**, отдельно **212 compiled/definition audits**. Все прежние16496 проходят. Native fixtures читаются через Cecil/JSON без Unity execution.
- `git diff --check`: чисто. Generated assemblies/Mods, logs, game DLLs, settings и saves не включаются. SDK/dependencies не переустанавливались.

11 файлов:

1. `SolastaUnfinishedBusiness/Models/SpellResourceOperationPlan.cs` — новый typed facade/requests/proofs/receipts, composite costs, native event outcomes, owner expansion.
2. `SolastaUnfinishedBusiness/Models/SpellResourceConsumerCatalog.cs` — source-specific profiles, exact native Flexible Casting source policy, non-Sorcerer metamagic и device shapes.
3. `SolastaUnfinishedBusiness/Models/SpellResourceOperationsContext.cs` — actual native descriptors и nine OFF-gated journal boundaries.
4. `SolastaUnfinishedBusiness/Models/StandardCasterResourcePlan.cs` — temporary slot capacity/expiry, exact grants и recovery preview.
5. `SolastaUnfinishedBusiness/Models/StandardMulticasterResourcesContext.cs` — reject unjournaled recovery/condition import; убраны retry-generated GUID.
6. `SolastaUnfinishedBusiness/Models/PactMagicResourcePlan.cs` — только additive logical-operation claim в existing CastingLedger; allocation/casting/Pact/Arcanum formulas сохранены.
7. `Tests/FighterProgression/SpellResourceOperationTests.cs` — production behavior и отдельно named native blueprint/ABI audits.
8. `Tests/FighterProgression/FighterProgression.Tests.csproj` — production model links.
9. `Tests/FighterProgression/Program.cs` — full-suite runner.
10. `Documentation/SpellResourceConsumersAudit.md` — source/consumer inventory, acceptance, risks/handoff/manual tests.
11. `Documentation/MulticasterIntegrationPlan.md` — Stage13.6 architecture/acceptance/validation.

**Stage13.6 backend готов к принятию** в указанной source-proof/planning области. Все M06-01…06 — PASS_BACKEND, native readiness — BLOCKED_PENDING_NATIVE_JOURNAL. Mixed gameplay не включён, native journal/persistence13.7 и activation13.8 не начаты. Commit SHA/push result указываются в итоговом отчёте; Git changes только на feature branch.
