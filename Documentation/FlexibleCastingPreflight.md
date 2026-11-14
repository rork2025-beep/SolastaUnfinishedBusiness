# Stage13.7A — native identity / Flexible Casting preflight

## Baseline и область

Branch `feature/progression-20-to-12`, HEAD до работы `d940a33b6599e6262fbc553d3158dd7c4da26d72`. Tracked tree чистый; прежний untracked `SolastaFiles/Mods/` сохранён. Повторно выполнены SDK9.0.320, Release Workflow **0 errors /0 warnings**, runner **17603 checks passed**, `git diff --check` clean. До изменений baseline failures не было. Зависимости не переустанавливались.

Архивный Stage13.7 regression inventory не переимплементирован. Существующие budgets, Pact/Arcanum/invocation, spell-learning/Secrets, third-caster и auxiliary adapters сохранены. Compressed Fighter magical subclasses остаются unsupported; Rogue casting не объявляется original20 при compressed features. Class12/character20/three-class guards, реальные history/HP/PB/ability prerequisites, user settings и saves не меняются.

**Результат: not ready для полного требуемого native source coverage.** Реализованы production preflight/model и guarded bridge для зарегистрированного native effect → UI callback. Campaign/unregistered effect и доставленная local/network-команда без сохранённого source envelope не получают guessed identity. Native Flexible Casting commit, persistence/save-load/Respec и mixed gameplay остаются disabled/unverified. MixedGameplayReady=false; новый public switch/binder не добавлен.

## Проверенный порядок native вызовов

Native/mod assemblies прочитаны через Cecil, definitions/source — без запуска Unity. Raw IL/game DLLs не включаются в коммит.

1. `CharacterActionUsePower` / campaign use создаёт `RulesetEffectPower`. Native `RulesetImplementationManager.InstantiateEffectPower` просто вызывает constructor; сам этот метод не обеспечивает GUID/registration. Constructor записывает user ID и usable power; это ещё не уникальная command identity.
2. `RulesetImplementationManager.ApplySpellSlotsForm(EffectForm, ApplyFormsParams)` принимает **actual activeEffect reference**, source actor, conversion effect type. Existing UB `RulesetImplementationManagerPatcher.ApplySpellSlotsForm_Patch.Prefix` выбирает original hero, его реальный Sorcerer repertoire и вызывает `FlexibleCastingModal.ShowFlexibleCasting(originalHero, repertoire, createSlot)`.
3. Новый action-entry prefix с priority802 запоминает только exact effect reference с проверенной nonzero registration до native effects. Это позволяет не терять уже доказанное происхождение после завершения instantaneous power. Scoped form prefix с priority801 затем видит source **до** existing UB prefix. Принимается только nonzero GUID, который `RulesetEntity.TryGetEntity<RulesetEffectPower>` разрешает в **тот же объект**, либо exact reference с таким proof сохранён action-entry hook до termination, source GUID соответствует actual actor/original hero, usable power принадлежит original hero, definition совпадает с зарегистрированным Flexible Casting power. Repertoire должен быть единственным actual class Sorcerer repertoire с родным CastSpellSorcerer и реальным caster owner.
4. `ShowFlexibleCasting` записывает caster/repertoire/direction в modal, но не сохраняет effect GUID. Новый prefix связывает этот exact modal с captured origin **только внутри** указанного scope; visible modal нельзя перезаписать другим source. Scope снимается finalizer в том числе при исключении. Global last-effect lookup не используется.
5. Native `OnItemSelected(level)` прямо сохраняет level. Это уже circle1–5, не zero-based index. Native `ShowFlexibleCasting` создаёт пять вариантов. Для создания slot стоимость берётся из `RuleDefinitions.SpellSlotCreationCost[level]`.
6. Native `OnConvertCb` сначала скрывает кнопки, затем вызывает `CreateSpellSlot` или `CreateSorceryPoints`. Эти методы меняют interactability, запускают AutoHide, подписываются на SorceryPointsAltered и отправляют команду через **IMagicCommandService**. Новый prefix выполняется **до всего этого**: capture Shift, Prepare, повторное чтение/revalidation и **PendingAtomicJournal**. Original callback не достигает dispatch/resource/effect mutations.
7. Local implementation: `LocalCommandManager.Create*(actor, level, repertoire)` непосредственно вызывает `RulesetCharacter.Create*`. В сигнатуре три аргумента, нет source effect/action/command ID. Новые local prefixes блокируют unmapped entry до native method; не сопоставляют команды по одному owner/level.
8. Network implementation: `NetworkingManager.Create*` отправляет `Create*RPC(commandTick, heroGuid, level, castingFeatureName)`. RPC запускает delayed `Create*Async`, где восстановленные actor/repertoire передаются LocalCommandManager. Tick используется очередью. `NetworkTick.CommandTick` читает MaxTick+currentSendTickBuffer, не выделяет уникальный ID на вызов. В async/local source-effect identity уже потеряна.
9. Последние resource boundaries: `RulesetCharacter.CreateSorceryPoints` → `repertoire.SpendSpellSlot(level)` → usedSorceryPoints clamp/event; `CreateSpellSlot` → `SpendSorceryPoints(native cost)` → `InflictCondition(ConditionAdditionalSpellSlotN, UntilLongRest)`. Native Sorcery spending также имеет healing/AccountUsedMagicAndPower/events. Старые13.6 guards на этих methods **не сняты**. Никакого post-spend preflight/rollback нет.

### Shift

Старый `FlexibleCastingModalPatcher.OnConvertCb_Patch.Postfix` вызывает RegisterShiftState **после** command dispatch. Получать из него якобы ранее выбранный Shift нельзя; synchronous local call уже мог завершить spend. Новая pre-dispatch choice читает Global.IsShiftPressed один раз на callback entry. Повтор с другим Shift — conflicting request. Revalidate использует сохранённую choice, не более поздний Global input. Старый Postfix при mixed gate ON возвращается до изменения native Shift counters; при gate OFF его обычный путь сохранён.

## Что связано в proof и ticket

- Original hero GUID и actual Sorcerer repertoire **reference**; логический ID `heroGuid/CastSpellSorcerer` не заменяет native reference check.
- Native registered source-effect GUID и exact power definition; direction сверяется с form/modal.
- Derived immutable command ID `flex:{heroGuid}:{effectGuid}:{slot|sorcery}`. Один conversion command на source invocation. Retry не создаёт GUID и не меняет ID. Unregistered/zero GUID не превращается в session counter или случайный GUID.
- Actual/effective Sorcerer levels из существующего validated standard owner, compression flag, actual class IDs. Новому native context не открыт прямой calculator API; используется узкий scalar accessor. Full regression isolation check калькулятора сохранён.
- Explicit input/output domains, native UI circle и реально consumed circle отдельно. `ResolveFlexibleInput` из13.6 использует те же Standard/Pact budgets и native Shift/AlwaysPactFirst rules. Возможен authorized Pact5→Sorcery1. Arcanum запрещён. Наличие высокого shared budget не расширяет Sorcerer learning.
- Native cost dictionary1–5, native Sorcery attribute maximum/used, actual PowerSpellPoints count/max/remaining, underlying native repertoire capacity/used arrays и Pact display sentinel. Сверяются **не** getters, которые просто возвращают тот же ledger, а actual stored native views. Несоответствие — stale rejection.
- Read-only rule/settings/modifier fingerprint: bool/int UB settings, обе power definitions/cost/recharge, installed creation/SP costs, actual magic affinities/healing-per-spent metadata. Native power max/remaining uses и condition GUIDs включены в live-state signature. Настройки не записываются.
- Для slot creation ticket.FundingIdentity = command ID + `/AdditionalSpellSlotN`. Это только provenance для будущего журнала; native condition не создаётся и не считается оплаченной по одному имени/GUID.

`FlexibleCastingPreflightPlan` вызывает **существующий** SpellResourceOperationPlan.Prepare и его typed sources. Native context не вызывает CommitManaged. Managed Counter — view родного Sorcery attribute, не новый native pool. Existing Standard/Pact budget instances берутся у одного bound original-hero coordinator, включая Sorcerer+Wizard без Pact. Не создаются дополнительные Standard/Pact budgets или PowerSpellPoints.

## Retry, Cancel и revalidation

Первый verified command фиксирует intent. Native callback retry с теми же contents возвращает **тот же Ticket**. Иной direction/level/input/cost/Shift под той же identity отклоняется, в том числе после неудачной попытки. Missing authority, wrong owner/repertoire, illegal level/domain/Arcanum, insufficient resources, wrong native cost, changed settings/eligibility, stale projected native balances/conditions/power uses отклоняются до mutation.

`Revalidate` повторно читает actual native state и проверяет ticket ownership, closed/cancelled state, native fingerprint и current authoritative operation snapshot непосредственно перед будущим commit. Возвращает read-only Prepare; `BlockNativeCommit` затем вызывает старый CommitNative, который всегда PendingAtomicJournal. Не вызываются native spend, condition publication, healing, events или refunds.

Cancel терминален для command; старый ticket не возобновляется новым nonce. Native Cancel callback для bound prototype не выполняет `RefundActionUse` или GUI lifecycle: безопасная отмена parent action ещё требует13.7B. Это не законченный игровой UI; данный gate недоступен пользователю.

Session не импортирует changed native Sorcery balances и не refills counter после rest/level-up. На расхождение — отказ. Native counter update/rebinding/serialization остаются следующим этапом. One-facade-per-ledger constraint13.6 сохраняется; coexistence с будущими consumer bindings требует общего owner registry, не второго facade.

## Узкий FinalizeCharacter safeguard

В existing CharacterBuildingManagerPatcher.FinalizeCharacter_Patch.Prefix первым вызывается `ValidateSorcererFinalizeEntry`, priority801. Он выполняется до Auxiliary/other BeginFinalize, GrantRaceFeatures, GrantSpellRepertoire, GrantCustomFeaturesFromFeats и до native TrainSkills/TrainSpells/TrainInvocations.

Только для bound repertoire session при ON gate: определяется Sorcerer draft по selected class, assigned class history, pending/cancelled Sorcerer transaction; missing selected-class metadata у Sorcerer owner не даёт обход. Требуется ровно один active typed Sorcerer transaction. Missing/cancelled/incomplete choices → IncompleteLearning. Новый `Transaction.ValidateComplete` выполняет read-only Replay(true), не Commit. Даже complete choices → PendingAtomicJournal, native finalization не разрешается.

Gate OFF и unbound/non-Sorcerer path сохраняют existing behavior. Это не новый general finalization coordinator, не spell/feat grants, не cleanup/rollback. Другие классы и their restrictions не переписаны.

## Supported / blocked matrix

| Path | 13.7A result |
|---|---|
|Verified registered source effect → exact modal → conversion choice |Implemented identity + fresh typed preflight, read-only; native commit BLOCKED_PENDING_NATIVE_JOURNAL |
|Standard slot → Sorcery |Production preflight tested |
|Standard Spell Points → Sorcery |Production preflight tested; Sorcery и Spell Points разные counters |
|Authorized Pact including actual5/UI1 |Production preflight tested; correct native policy, no Arcanum |
|Sorcery → temporary1–5 slot / SP credit intent |Production preflight + stable funding identity tested; no condition publication |
|Wrong owner/repertoire/authority/rules/cost/level/stale balances |Structured rejection, unchanged resources/native counters/temporary-condition state |
|Direct native LocalCommandManager entry without envelope |BLOCKED_PENDING_NATIVE_JOURNAL |
|Native networking delayed command |BLOCKED_PENDING_NATIVE_JOURNAL: no source-effect mapping in payload |
|Campaign/unregistered transient source effect |MissingIdentity **before** modal/Prepare; no fabricated GUID |
|Bound incomplete Sorcerer FinalizeCharacter |Rejected before grants; even complete choices still blocked for atomic finalization |
|Ordinary single-caster with MixedGameplayReady=false |Existing behavior retained; new native prefixes return before resource/source lookup |

## Why overall not ready / 13.7B handoff

1. **Registered GUID is not universal.** GameCampaignCharacter.UsePower creates effect and directly fills ApplyFormsParams/applies forms without an explicit entity registration/GUID-assignment step. InstantiateEffectPower/RulesetEffect constructors themselves do not allocate a command GUID. `PowersUsedByMe` is also not authority for these instantaneous powers: AddActivePower occurs only for ongoing effects; instantaneous powers terminate. That invalid assumption was removed during review. Nonzero identity must be proved from the entity registry at capture, not inferred from a power template or persistent-effects list. Campaign/transient source needs an explicitly authorized command-creation envelope before it can join this protocol.
2. **Native transport does not preserve the mapping.** Tick/hero/level/feature are insufficient to recover the captured effect unambiguously. Thread-local UI scopes do not survive RPC/delayed execution. Need an authoritative in-band command ID + original source/funding mapping, duplicate/ack/cancel protocol, and server-side proof revalidation. Do not silently select a pending ticket by hero+level.
3. Parent power-effect creation and mixed owner admission are still protected by13.6 gates. Source scope requires an already legitimate registered invocation; this slice does not relax those guards to make the UI reachable. Native binding/activation is not provided.
4. Atomic native resource debit + additional-slot condition creation/funding/credit/removal, native Sorcery healing/events, parent action refund and UI failure flow remain absent. No imaginary rollback.
5. Shared owner registry for all consumer facades, rest/counter snapshot updates, persistent state/save-load/respec, interruption and changed-rule cleanup remain open. No existing-save migration.
6. Unity tests have not run. Required future manual checks: both directions and all1–5 levels; slots/SP; Sorcerer/Wizard and Sorcerer/Warlock; Shift/priority/exhaustion/lossy Pact; direct/network/delayed/retried/cancelled commands; missing/zero/terminated/reused effect identity; no duplicate funding/conditions; exact original owner; incomplete direct finalization before every grant; normal single-caster OFF regression. Save/load/Respec are separate tests after implementation.

These are actual blocking links, not assertions that model tests prove native execution. **Do not mark the full Stage13.7A native slice ready until campaign/source identity and the required native command mapping are substantiated.**

## Validation / files

Финальный полный прогон на .NET SDK9.0.320: **18196 checks passed** = **17603 baseline** + **593 новых**: **565 executed production preflight/model checks**, отдельно **28 compiled/definition audits**. Release Workflow **0 errors /0 warnings**, `git diff --check` clean. Native handlers/Harmony/GUI/combat/rest/save-load/Respec в Unity **не запускались**. Проверка target signature/IL не выдаётся за native execution.

Exact commands:

```bash
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.setup-solasta/dotnet-home
export NUGET_PACKAGES=/workspace/.setup-solasta/nuget DOTNET_CLI_TELEMETRY_OPTOUT=1
export SolastaInstallDir=/workspace/SolastaUnfinishedBusiness/SolastaFiles
cd /workspace/SolastaUnfinishedBusiness
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
git diff --check
```

Regression failure during development was the calculator isolation assertion: first adapter directly consumed a progression Result. Fixed by the existing standard owner's validated scalar accessor, **not** by weakening the whitelist. Initial primary-constructor warnings were removed; final build has no warnings.

14 changed files (relative to checkout):

- `SolastaUnfinishedBusiness/Models/FlexibleCastingPreflightPlan.cs`
- `SolastaUnfinishedBusiness/Models/FlexibleCastingPreflightContext.cs`
- `SolastaUnfinishedBusiness/Models/SorcererFinalizePreflight.cs`
- `SolastaUnfinishedBusiness/Models/MultipleSpellRepertoiresPlan.cs`
- `SolastaUnfinishedBusiness/Models/MultipleSpellRepertoiresContext.cs`
- `SolastaUnfinishedBusiness/Models/StandardMulticasterResourcesContext.cs`
- `SolastaUnfinishedBusiness/Models/PactMagicMulticasterContext.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/FlexibleCastingModalPatcher.cs`
- `Tests/FighterProgression/FlexibleCastingPreflightTests.cs`
- `Tests/FighterProgression/FighterProgression.Tests.csproj`
- `Tests/FighterProgression/Program.cs`
- `Documentation/FlexibleCastingPreflight.md`
- `Documentation/MulticasterIntegrationPlan.md`

No generated/proprietary binaries, settings, saves, worktree or release changes. Commit/push to feature branch only, SHA/result in final report.
