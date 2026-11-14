> **Текущее состояние — Stage13.7B.2:** dedicated single-player **campaign** standard-slot→Sorcery native commit реализован за закрытым gate. Production executor проверяется с fake native field ports; настоящий Unity field host скомпилирован и отдельно проаудирован, но **в Unity не исполнялся**. MixedGameplayReady=false, public activation отсутствует; feature не playable. См. последний раздел.

> **Архивная запись — Stage13.7B.1 revised:** реализованы single-direction transaction proposal, source/ticket revalidation и existing-ledger managed arithmetic; **native commit остаётся blocked**. См. последний раздел. MixedGameplayReady=false; Unity validation pending. Предыдущие разделы13.7A/A.2 сохранены как baseline/history.

> **Архивная запись — Stage 13.7A.2:** см. раздел в конце документа. Есть typed authority для registered и проверенного base campaign transient effect, явный локальный identity-only envelope и fail-closed receiver. Native network/ordinary action completion остаются blocked; **Stage 13.7A not ready**. Описание 13.7A ниже сохранено как история baseline. В частности, прежний cached proof после termination заменён строгой проверкой lifetime и актуального registry membership.

# Flexible Casting — source identity, preflight и native commit

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

## Stage 13.7A.2 — source identity и command transport

### Baseline / результат

Работа начата с `0ee69c803b7d4937ddbf42c784087dbf8eb9ec68`, branch `feature/progression-20-to-12`. Tracked tree был чист; существующий untracked `SolastaFiles/Mods/` сохранён. SDK **9.0.320**, baseline build **0 errors / 0 warnings**, **18 196 checks passed**, diff check clean. Зависимости повторно не устанавливались. Ранее сообщённые 17 603 относятся к Stage13.6, не к starting commit13.7A.

**Stage 13.7A not ready:** отсутствует безопасный in-band native network authority transport и атомарный native journal. Новый код — bounded process-local preflight, не разрешение исполнять conversion. MixedGameplayReady=false, public switch отсутствует, CommitNative неизменно PendingAtomicJournal. Save/load, Respec, native commit и mixed gameplay disabled/unverified.

### Установленный native порядок (Cecil / definitions, не Unity)

| Route | Сигнатуры / точный порядок | Registry / lifetime |
|---|---|---|
| Ordinary location UI | `CharacterActionPanel.PowerEngaged(RulesetUsablePower)` → `IRulesetImplementationService.InstantiateEffectPower(RulesetCharacter,RulesetUsablePower,bool)` → `CharacterActionParams.RulesetEffect` → `ExecuteEffectOfAction` / command/action queue | Setter RulesetEffect только сохраняет reference; не регистрирует. Location override вызывает `HandleEffectRegistration(effect,delay)` |
| Location registration | `RulesetImplementationManagerLocation.HandleEffectRegistration(RulesetEffect,bool)` | Записывает DelayEnvironmentRegistration. При false: `effect.Register(true)` → entity service GenerateGuid/RegisterEntity и RegisterPersonalEffect. При true регистрация отложена |
| Action start | `GameLocationActionManager.StartNextActionFromCurrentQueue` → `CharacterActionParams.SignalActionStarting()` → ExecuteActionAsync | SignalActionStarting регистрирует effect с delay=true и guid=0, кроме исключённого native action parameter. Сам факт constructor/UsablePower/ненулевого поля proof не заменяет |
| Ordinary effect application | `CharacterActionMagicEffect.ExecuteImpl` → power effect forms → ApplySpellSlotsForm → UB ShowFlexibleCasting | Capture prefix принимает только actual TryGetEntity GUID→**тот же object**, принадлежащий actor/original hero и actual usable power. Existing13.6 action guard по-прежнему не пропускает native power action к effects. Полный ordinary GUI flow не разблокирован |
| Base campaign | `GameCampaignCharacter.UsePower(RulesetUsablePower)` → base InstantiateEffectPower → FillSource/FillFromActiveEffect → ApplyEffectForms → **RulesetCharacter.UsePower** | Base factory только создаёт RulesetEffectPower, без Register/GenerateGuid. RulesetEntity ctor создаёт attribute dictionary; RegisterAttributes — не registry proof. Campaign method сам не вызывает Terminate. Поэтому нужен отдельный transient authority |
| Constructor resource safety | `RulesetEffectPower(RulesetCharacter,RulesetUsablePower,RulesetItemDevice,RulesetDeviceFunction)` | Сохраняет userId/usablePower/sourceDefinition/remaining duration. ForceSpentPoints имеет отдельные ветви recharge8/11/14; AtWill их обходит (проверено IL). Допускается только exact registered Flexible power с AtWill, Instantaneous, единственной нужной CreateSpellSlot/CreateSorceryPoints form. Source effect construction не означает resource commit |
| Termination | `CharacterActionUsePower.PersistantEffectAction()` | Для instantaneous/nonongoing effect вызывает Terminate(false), а не AddActivePower. Terminate ставит Terminated=true, ClearCasterInfo, TerminateEffect, Unregister. После этого proof отклоняется; PowersUsedByMe не используется как authority |
| Modal callback | ShowFlexibleCasting(originalHero,actualSorcererRepertoire,create) → OnConvertCb | Prefix выполняется до скрытия native buttons, AutoHide, dispatch и spending; читает Shift в этот момент. Старый UB postfix не выдаёт более раннюю Shift identity |
| Native local | `IMagicCommandService.CreateSpellSlot/CreateSorceryPoints(RulesetCharacter,int,RulesetSpellRepertoire)` → LocalCommandManager → RulesetCharacter.Create* | Нет effect/invocation/command ID; остаётся blocked, даже если owner/level совпали с pending modal |
| Native network | NetworkingManager.Create* → Create*RPC(long tick,ulong hero,int circle,string castingFeature) → Create*Async → local Create* | Нет trusted invocation ID/source reference/authority acknowledgement. Tick — MaxTick+send buffer, не уникальный allocation. Effect payload в другом ExecuteActionRPC не связывает поздний Create*RPC с конкретной conversion. Нельзя восстановить proof по timing/hero/circle/feature |

Base campaign adapter дополнительно требует **exact service implementation type RulesetImplementationManager**, LocalCommandManager и ActivePlayerController.IsCharacterControlled(originalHero). Location override / неизвестный derived service / сетевой campaign отклоняются **до constructor**: их отдельные registration/execution seams не подменяются base proof. Зарегистрированный GUID-путь сохранён отдельно для пригодного actual action source.

### Создание authority и транспорт

1. Prefix `FlexibleCastingCampaignPatches.EntryPatch` стоит на exact `GameCampaignCharacter.UsePower`. Проверяет original hero reference (не подменённый/transformed actor), usable power membership, actual Sorcerer repertoire, native feature authority, definition и безопасную shape.
2. Только здесь `FlexibleCastingInvocationPlan.BeginCampaign` вызывает injected issuer; production issuer — Guid.NewGuid. `campaign:<nonce>` создаётся **один раз на power-use entry**. Zero/reused ID отклоняется. Повтор modal callback / command receive **не создаёт ID**.
3. Синхронный CampaignEntry scope разрешает ровно один constructor для exact actor+usable reference. Factory postfix привязывает exact result object, SourceGuid, actual power definition, no item origin, !Terminated. Missing/ambiguous result отклоняется. Помимо definition проверяется final effect description после native constructor / UB modifiers: только одна соответствующая conversion form, никаких других effects. Scope не является command transport и снимается finally даже при ошибке UI cleanup.
4. Для зарегистрированного effect authority вида `registered:<hero>:<effectGuid>` выдаётся только после actual entity registry lookup с reference equality. Повтор exact proof возвращает authority, чужой reference с тем же GUID отклоняется. Lifetime lambda перепроверяет !Terminated и current TryGetEntity reference при каждом использовании.
5. ApplySpellSlotsForm scope переносит **выданный source authority** в точный ShowFlexibleCasting вызов. Registry привязывает exact modal instance. Другая живая invocation не может перезаписать modal. Смена hero/repertoire/power/direction/source GUID или потеря usable power отклоняется.
6. Source authority различает RegisteredEffect / TrustedCampaignInvocation. Preflight command ID детерминирован из **выданной invocation**, original hero и направления. Prepare использует существующие FlexibleCastingPreflightPlan → SpellResourceOperationPlan.Prepare и тот же Standard/Pact ledger. Sorcery snapshot остаётся отдельным counter; новый resource budget не создаётся.
7. OnConvertCb создаёт immutable typed Envelope: command ID, invocation ID, hero/repertoire ID, source kind, registered native GUID (null для transient), definition, direction, UI circle, actual input circle, input/output domains, effective cost, immutable request fingerprint. FundingIdentity temporary slot остаётся в привязанном ticket того же operation; condition не создаётся.
8. Вызывается явный **`ReceiveLocalCommand(Envelope)`** — новый mod-local identity-only seam. Receiver ищет issued origin по invocation ID, не по hero/level/latest effect. Затем registry проверяет все поля структурно, exact owner/effect/repertoire references, current ticket, live state, rules/cost/resources. Envelope может быть скопирован или доставлен позже внутри того же процесса, но proof заново валидируется.
9. Acknowledgement имеет `PreflightValidatedPendingAtomicJournal`, NativeCompleted=false, ParentCompleted=false. Это **не** acknowledgement native spending или completed parent. В prefix BlockNativeCommit повторно валидирует ticket и вызывает существующий всегда блокирующий native boundary. CommitManaged не вызывается.
10. Narrow campaign transpiler заменяет единственный native parent UsePower call на HoldOrUseCampaignPower: только для проверенной gated Flexible invocation родитель удерживается без Consume/PowerActivated/refund. Для gate OFF/другого power вызов actor.UsePower сохраняется. Если exact modal не был открыт, finalizer закрывает authority и возвращает structured rejection. Другие action parents не разблокированы.

Ни serialized native protocol, ни Photon RPC handler не добавлены. Нового сетевого транспорта **нет**. Вызов ReceiveLocalCommand при NetworkingManager и model Receive(network:true) отклоняются PendingAtomicJournal; неизвестные legacy local Create* также blocked. In-process deferred delivery проверена моделью очереди, не реальным native coroutine/network transport.

### Повторы, отмена, rejection

- Один source разрешает максимум один conversion intent. Identical Prepare возвращает тот же envelope; duplicate receive — тот же acknowledgement **после** live revalidation. Conflicting fields/Shift/circle/cost/direction не создают заменяющий ID.
- Две одновременные invocation одного hero на одном circle получают разные IDs; exact modal overwrite запрещён. Существующий one-pending-operation-per-hero guard сохранён: вторая подготовка blocked до отмены первой, без повреждения первой. Это ограничение preflight, не одновременный native commit.
- Cancel/Rejected/Expired — terminal; поздние delivery/retry отклоняются. Expired exact effect закрывает ticket. Изменение rules/cost/Sorcery/Standard/Pact/conditions/power uses закрывает stale ticket до ack.
- Поддельный/неизвестный packet не отменяет чужой легитимный ticket. Native UI failure закрывает только свой source/modal. Повтор terminal close idempotent.
- Rejection/Cancel скрывает собственный modal через Hide(true), не OnCancelCb. Native OnCancelCb содержит RefundActionUse и **не вызывается**. OnBeginHide/OnEndHide проверены как UI cleanup без прямых resource/refund calls; их реальный Unity lifecycle не выполнялся. Pending success оставляет modal явно незавершённым и пишет PendingAtomicJournal; native Convert callback не выполняется.
- Не выполняются native debit, Sorcery gain, temporary AdditionalSpellSlot publication, healing, recovery, PowerActivated, parent success или refund. Нет global inventory/resource rollback, save migration, нового публичного setting или native durable journal.
- Registry/tombstones существуют только в памяти процесса. Они не сериализуются; состояние после restart/save-load не поддерживается. Конкурентный multi-threaded dispatcher не реализован: native Unity UI/local entry ожидает main thread; сетевой receiver недоступен.

### Narrow finalization regression

В baseline predicate учитывал **любую**, включая cancelled/closed, Sorcerer transaction. Executed regression сначала воспроизвела ошибку: последующий выбранный Wizard считался incomplete Sorcerer draft. Predicate извлечён в production SorcererFinalizePreflight.IsSorcererDraft и исправлен: explicit selected class определяет текущий draft; закрытая историческая Sorcerer claim не перебивает новый Wizard. При отсутствии selected используются active Sorcerer transaction / текущий assigned class; отсутствие достоверного current class остаётся fail-closed.

Incomplete текущий Sorcerer по-прежнему rejected до grants, complete — PendingAtomicJournal; gate-OFF single-caster не меняется. Общий ValidateBeforeFinalize не изменён: stale/cancelled transactions и native atomic finalization остаются blocked. Исправление не разрешает Wizard/другой multiclass draft обходить общий journal.

### Route-by-route readiness

| Route | Реализация / evidence | Статус |
|---|---|---|
| Registered source → exact modal → explicit local envelope | Production registry/preflight executable tests; actual GUID capture/lifetime receiver compiled audits | Implemented/tested model; Unity unverified. Ordinary native action ещё blocked13.6 guard |
| Trusted transient base campaign | Entry ID → one exact factory result → modal; constructor shape / parent hold transpiler and signatures inspected | Implemented/tested model; native bindings inspected; Unity unverified |
| Campaign location override / unknown source | Exact implementation prerequisite rejects before source creation | Blocked with evidence; не guessed base proof |
| Local identity-only receiver | Explicit envelope fields, references, ticket/current-state revalidation; preflight-only ack | Implemented/tested production model, native bridge compiled; Unity unverified |
| Native legacy local command | 3args не содержат invocation identity | Blocked PendingAtomicJournal |
| Delayed in-process envelope | Copy/queue/revalidate/idempotent ack | Executed model only; real native scheduler unverified |
| Delayed/network RPC | Existing RPC no source authority; no authenticated cross-process issued-source mapping | Explicitly blocked; rejection tested |
| Retry / conflict / duplicate / Cancel / late delivery / expired source | Production registry + existing operation plans, resource/counter/condition snapshots on rejected paths | Implemented/tested model; modal/parent lifecycle unverified Unity |
| Sorcerer finalization versus next Wizard level | Production current-draft predicate regression + direct early guard compiled binding | Implemented/tested model; native grants still blocked |
| Native commit / save-load / Respec / mixed gameplay | CommitNative PendingAtomicJournal; no binder/public gate/journal | Disabled/unverified; not ready |

### Проверки и оставшиеся13.7B dependencies

Executed production-model tests используют installed compiled slot/Spell Points/Pact tables и native-state scalar fixtures, не game objects. Снимки включают Standard/Pact/Sorcery, temporary slots/high-circle counts, Arcanum, native Sorcery/power/refund counters и condition identities. Это доказывает отсутствие изменений **в модели** при каждом rejected preflight/transport; не доказывает реальную Unity inventory/conditions/actions/network.

Cecil/definition audits отдельно проверяют native factory/registration/termination order, AtWill constructor branch, patch positional ABI, local receiver signature, отсутствие spend/grant/refund/CommitManaged calls в identity adapter, network payload и gate-OFF branches. Harmony patches, transpiler и Gui.Hide фактически в Unity не исполнялись.

Остаётся: безопасный authoritative authenticated network command envelope и source ownership sync; native action/modal lifecycle и controller checks в Unity; atomic spend+Sorcery callbacks/healing+temporary condition funding journal; родительский action commit/cancellation coordination; durable replay/cancel и save/load/Respec. Нельзя заменять это post-spend hooks, guessed execution tags, Tick или refunds. Никакая часть13.8 не начата.

Изменённые файлы:

- Models/FlexibleCastingInvocationPlan.cs (новый authority/transport registry).
- Models/FlexibleCastingCampaignPatches.cs (новый exact campaign boundary и source binding).
- Models/FlexibleCastingPreflightContext.cs (issued source/modal/local receiver).
- Models/FlexibleCastingPreflightPlan.cs (явный proof kind).
- Models/SpellResourceOperationsContext.cs (единственное узкое разрешение source constructor, без spend).
- Models/SorcererFinalizePreflight.cs и Models/MultipleSpellRepertoiresContext.cs (narrow current-draft fix).
- Tests/FighterProgression/FlexibleCastingInvocationTests.cs (новые executed tests и отдельно binding audits).
- Tests/FighterProgression/FlexibleCastingPreflightTests.cs и FighterProgression.Tests.csproj (fixtures, regression, runner link).
- Documentation/FlexibleCastingPreflight.md.

Финальный прогон (SDK9.0.320, указанные DOTNET_ROOT/PATH/NUGET_PACKAGES/SolastaInstallDir):

```sh
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
# exit0: 0 errors, 0 warnings
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
# exit0: 19373 checks passed
git diff --check
# exit0, no output
```

Baseline **18 196** → final **19 373**, прирост **1 177**: **1 139** новых executed identity/transport production-model checks, **33** новых compiled/definition audits и **5** additional current-draft regressions в прежнем13.7A suite. Старый13.7A suite теперь570 executed +28 compiled checks. Полный runner сохраняет все предыдущие class, spell learning, Pact/Arcanum/invocation, Secrets, auxiliary и third-caster suites. Baseline failures отсутствовали. Отдельная новая regression до исправления воспроизвела stale Sorcerer draft ошибку; после исправления весь runner проходит.

**Оставшиеся Unity сценарии (не выполнялись):** gate-OFF ordinary single-caster Flexible Casting в обоих направлениях; actual base campaign factory/controller authority; registered location action и termination до выбора; точное modal ownership при двух попытках одного героя; parent/action state при PendingAtomicJournal, Hide/Cancel без refund; потеря source/repertoire/settings перед delayed local receiver; network refusal без source mint/spend. Для этих сценариев нужен отдельный контролируемый internal binder/test harness, не public setting. Native conversion commit, actual temporary-slot lifecycle, healing callbacks, durable save/load и Respec в этом slice не разрешены и не проверены.

Итог: **Stage 13.7A not ready**. Native network authority seam отсутствует; ordinary native action остаётся под journal guard, а actual Harmony/GUI/control/lifetime execution требует Unity evidence. Stage13.7B/13.8, native resource commit и persistence не реализованы.

## Stage13.7B.1 revised — guarded standard slot → Sorcery Points

### Scope и baseline

Starting commit `eba355149e4b4a54f01a8bd6375f2ab75b80256a`, branch `feature/progression-20-to-12`. Tracked tree чист до изменений; прежний untracked SolastaFiles/Mods сохранён. Baseline повторно выполнен: SDK **9.0.320**, Release Workflow **0 errors / 0 warnings**, runner **19 373 checks passed**, diff check clean. Baseline failures отсутствовали, зависимости не переустанавливались.

Coding slice начинается от13.7A.2. Unity smoke test не выдаётся за prerequisite или выполненную проверку: actual Unity execution отсутствует. **Результат разделён:** production model/proposal реализован и проверен вне Unity; безопасный native transaction boundary не подтверждён, поэтому native commit блокируется до первого изменения ресурсов; Unity validation pending. Conversion не playable, Stage13.7B.1 не объявлен завершённым runtime.

Поддерживается в новой модели только один verified **StandardSlots → SorceryPoints**, исходящий из actual Sorcerer repertoire оригинального hero, circle1–5 существующего modal, quantity1/cost1, UI circle=actual consumed circle. Character20/class12/three classes, real history/HP/PB/abilities, предыдущие class modules и guards не меняются. Sorcery→slot, Spell Points, Pact/Arcanum input, network, save/load, Respec и general mixed gameplay не реализованы этим slice. Наличие Pact repertoire у владельца само по себе не запрещает standard input, но Pact budget никогда не списывается новой моделью.

### Точный native call order / границы отказа

Installed Assembly-CSharp и compiled UB исследованы Cecil; это **не Unity execution**.

1. `FlexibleCastingModal.OnConvertCb()` скрывает buttons и вызывает `CreateSorceryPoints()`; тот изменяет interactability, запускает AutoHide, подписывает `SorceryPointCreated` на SorceryPointsAltered и отправляет `IMagicCommandService.CreateSorceryPoints(caster,selectedSlotLevel,repertoire)`. Существующий gated prefix из13.7A.2 останавливает оригинальный callback **до** этих изменений, переносит issued envelope явно. Legacy local/network3arg commands по-прежнему blocked.
2. `LocalCommandManager.CreateSorceryPoints(RulesetCharacter,int,RulesetSpellRepertoire)` вызывает `RulesetCharacter.CreateSorceryPoints(int,RulesetSpellRepertoire)`.
3. `RulesetCharacter.CreateSorceryPoints` сначала вызывает `repertoire.SpendSpellSlot(slotLevel)`; затем `usedSorceryPoints=max(0,usedSorceryPoints-slotLevel)`; затем `SorceryPointsAltered?.Invoke(this,RemainingSorceryPoints,null)`. В методе нет transaction receipt, try/rollback/finally или atomic publication.
4. Native `RulesetSpellRepertoire.SpendSpellSlot(int)` сначала добавляет/increments usedSpellsSlots[circle], затем синхронно вызывает **RepertoireRefreshed**, ещё до возврата к Sorcery write. Callback может бросить исключение или изменить/наблюдать состояние. Это первое установленное окно: slot уже списан, Sorcery ещё не начислены. Исключение SorceryPointsAltered возникает позже, после обоих изменений, но до нормального возврата/ack. Нельзя считать любое исключение признаком, что ничего не произошло.
5. UB `RulesetSpellRepertoirePatcher.SpendSpellSlot_Patch.Prefix` заменяет native body. Сначала auxiliary validation / `StandardMulticasterResourcesContext.TrySpend`; для ordinary legacy режима используются ConsumeSlot и shared-repertoire propagation. ConsumeSlot также меняет usage и вызывает RepertoireRefreshed. Для bound prototype используется существующий authoritative standard ledger, затем projection.
6. `StandardMulticasterResourcesContext.TrySpend` / Pact coordinator используют typed existing budgets; ordinary spell entry требует spellOperation от реального RulesetEffectSpell. Flexible conversion не должна подделывать spell scope. `StandardMulticasterResourcesContext.Project` назначает repertoire dictionaries и вызывает RepertoireRefreshed по каждому view. Исключение может оборвать projection между views. Эта функция не предоставляет callback-free staged publication или conversion receipt boundary.
7. `UsedSorceryPoints` — поле original RulesetCharacter; maximum берётся из actual SorceryPoints attribute, `RemainingSorceryPoints=max(0,maximum-used)`. Operation-plan Counter — managed snapshot, а не второе native хранилище. Для bound prototype slot budget authoritative в existing StandardCasterResourcePlan.Budget; native repertoire dictionaries — projection этого же бюджета. Нельзя отдельно списать ledger и затем вызвать SpendSpellSlot на его view как вторую стоимость.
8. `CreateSorceryPoints` **не вызывает GainSorceryPoints**, SpendSorceryPoints, Heal или AccountUsedMagicAndPower. GainSorceryPoints тоже непосредственно меняет used counter и публикует SorceryPointsAltered; это не atomic conversion API. HealingPerSpentSorceryPoint/Heal/AccountUsedMagicAndPower относятся к **расходованию** Sorcery Points в SpendSorceryPoints. Добавлять healing в рассматриваемое направление нельзя. Произвольные listeners остаются отдельным риском.
9. Native event передаёт **final remaining**, не amount gained, и feature=null. UI OnItemSelected отображает min(usedSorceryPoints,selectedCircle). Модель сохраняет clamping, включая gain0 при полном запасе: native body всё равно расходует slot, если такая команда дошла. Это не новая рекомендация разрешать бесполезный native action; native execution остаётся blocked.
10. Campaign parent из13.7A.2 удерживается до native UsePower; обычный power action остаётся под13.6 guard. Native OnCancelCb включает RefundActionUse и не используется как rollback. Новая модель не завершает parent, не вызывает UsePower/PowerActivated/refund, не пишет success acknowledgement.

### Production proposal / managed journal

Новый `Models/FlexibleCastingSlotToSorceryPlan.cs` — **не новый resource engine** и не native executor. Он хранит issued intent, immutable before/expected-after projections и managed receipt. Нет нового Standard/Pact budget, PowerSpellPoints, независимого счётчика Sorcery или автоматически импортированного hero.

- `FlexibleCastingInvocationPlan.InspectStandardSlot` сначала проходит существующие Receive/source authority/current ticket проверки, затем `Ticket.RequireStandardSlotToSorcery`: только правильные definition/operation/domain/circle/quantity/cost. Возвращает references на **тот же** issued source, envelope, preflight plan и ticket. Один lazy transaction facade на registry.
- `Prepare` возвращает тот же Draft для одинакового envelope; conflict same ID/different fields отклоняется. Immutable After содержит usage[circle]+1, RemainingSorcery+clampedGain, остальные capacities/Pact/SP/costs/conditions unchanged. Это **ожидаемая проекция**, не запись в game state.
- `CommitManaged` существует только для проверяемой арифметики backend. Source evidence и Live читаются повторно через переданные readers непосредственно перед существующим `CommitManagedStandardSlot`; там ещё раз проверяются typed direction и полный ticket. Применяется существующий SpellResourceOperationPlan.CommitManaged к тому же Standard budget и Sorcery snapshot. Ни один native runtime caller в compiled mod не вызывает этот managed method.
- Managed commit point — возврат существующего coordinator с **обоими** изменениями и receipt, записанным в Draft. До этого checkpoint'ы BeforeStaging/DebitStaged/CreditStaged/BeforePublish только готовят immutable delta proposal. Fault в них отбрасывает предложение, закрывает source, ничего не refund'ит.
- AfterManagedPublish failure означает **потерю доставки уже committed model receipt**, а не failed conversion: Status=ManagedCommitted, DeliveryPending=true. Повтор с тем же issued intent и current expected projection возвращает тот же receipt без повторного debit/gain. Rules/owner/source/resource changes отклоняются даже на повторе. Expiry после managed commit может вернуть PendingAtomicJournal из прежнего registry cancellation guard: подтверждённый расход не отменяется, новый debit/gain не выполняется. Это отказ, не renewed authority или refund.
- Managed receipt не проецирует native поля: тесты явно делают отдельную test-side scalar projection перед моделированием post-commit retry. Попытка выдать arithmetic за native publication без этой projection отвергается stale proof. NativeCompleted=false и ParentCompleted=false неизменно.
- Исключение **внутри** старого mutable CommitManaged, если оно возникнет после входа в publish, помечает ManagedIndeterminate; повтор/rollback запрещены. Отсутствие таких исключений при произвольных concurrent writes, OOM или изменениях coordinator не доказано. Это ещё одна причина не считать arithmetic native-atomic guarantee. Fault checkpoints проверяют staging, а не невозможную инъекцию внутрь native slot write.
- Cancel до publish terminal/idempotent, в том числе через реальный путь registry.Close от modal. Reentrant managed commit отклоняется. Cancel committed/indeterminate draft — PendingAtomicJournal; никаких refund/resets confirmed debt. Обнаруженный stale pending proof закрывается terminal, как в исходном envelope receiver: возврат старых settings не оживляет команду. State и receipts process-local, без persistent replay safety. Managed receipt retry реализован только у нового model facade; исходный native receiver остаётся preflight-only, native receipt publication/replay integration отсутствует.

### Native integration: преднамеренный block

Gated `FlexibleCastingPreflightContext.BlockNativeCommit` направляет только StandardSlots→SorceryPoints envelope в новый proposal и `FlexibleCastingSlotToSorceryPlan.CommitNative`. Он снова проверяет issued source/current ticket/owner/circle/rules/resources и **безусловно выбрасывает PendingAtomicJournal до любого native или managed debit**. NativeBoundaryAvailable=false. Все остальные направления сохраняют прежний block. `MixedGameplayReady=false`, нового public setting нет; ordinary gate-OFF code path остаётся прежним.

**Почему native commit не реализован:** installed public API публикует slot callback между debit и credit, а общий projection helper публикует callbacks между views. Нет verified seam, который одновременно фиксирует existing ledger, raw original-hero Sorcery counter, все необходимые projections и command receipt **до** первого observer/reentrant callback и согласует parent action. Нет native outbox/ack для частично выполненного multicast listener. Catch + refund не восстанавливает effects внешних callbacks и потому запрещён. Простое подавление exceptions или замена failed на success также не применяется.

Требуется будущий narrowly scoped native journal: валидированный callback-free preparation/state publication для этого owner; coherent slot+Sorcery+projection+receipt commit point; точные semantics публикации RepertoireRefreshed/SorceryPointsAltered и parent lifecycle; отказ/потеря доставки после commit без повторных callbacks/ресурсов; фактическая Unity проверка. Это описание оставшейся работы, не её реализация или разрешение native commit. Native budget/user settings/saves не менялись.

### Failure / evidence matrix

| Сценарий | Проверенный результат | Evidence |
|---|---|---|
| Valid standard slot1–5, registered/transient source, Sorcerer+Wizard/Warlock | Ровно один existing-model slot, min(circle,missing Sorcery) gain; без Pact debit | Executed production model, не Unity |
| Cap/partial cap | Native clamped amount, включая0; никакого overflow/healing | Executed model + audited native formula |
| Duplicate/conflicting command | Same receipt / reject; ни второго debit, ни false native ack | Executed model |
| Insufficient slot / stale resource/rules/source/owner / cancellation | Отказ до новой mutation, parent held | Executed model |
| Fault до staging / debit staged / credit staged / before publish | Proposal rejected, exact model/native-fixture balances unchanged | Executed **staging** fault tests, не native write fault tests |
| Cancel в staging / native-style registry Cancel | Повторная проверка запрещает publish | Executed model |
| После managed publish потеря receipt delivery | Committed model receipt + DeliveryPending; retry без новых ресурсов | Executed model, не native callbacks |
| Внутренний publish failure / uncertain mutation | ManagedIndeterminate block, нет выдуманного rollback | Conservative code guard; не доказанный atomic recovery |
| Native RepertoireRefreshed failure после debit | Возможен spent slot без Sorcery | Cecil call-order evidence + отдельно labelled counterexample fixture, не production execution |
| Native SorceryPointsAltered failure после credit | Оба native ресурса могли измениться без normal return | Cecil evidence + отдельно labelled counterexample fixture; recovery не реализован |
| Native CommitNative / network / excluded domains | PendingAtomicJournal либо typed domain rejection, без spending | Executed model guard + compiled bridge audit |
| Native UI/action/real resource conversion | Не запускались | Unity validation pending |

Cecil audits проверяют конкретные native signatures/field-write/callback ordering, clamp, отсутствие healing в этом направлении, existing guards, отсутствие native debit/parent/refund calls у CommitNative, отсутствие managed commit callers в runtime и gate-OFF branch. Они не считаются Unity tests.

Изменённые файлы:

- Models/FlexibleCastingSlotToSorceryPlan.cs (новый single-direction proposal/managed journal/native block).
- Models/FlexibleCastingInvocationPlan.cs (verified typed intent и singleton facade).
- Models/FlexibleCastingPreflightPlan.cs (typed ticket constraint / existing-ledger arithmetic / receipt validation).
- Models/FlexibleCastingPreflightContext.cs (narrow gated native block route).
- Tests/FighterProgression/FlexibleCastingSlotToSorceryTests.cs (новые production-model tests, audits и отдельные counterexample fixtures).
- Tests/FighterProgression/FlexibleCastingPreflightTests.cs и FighterProgression.Tests.csproj (runner / production source link).
- Documentation/FlexibleCastingPreflight.md.

Финальные validation commands/results (SDK9.0.320, существующие PATH/DOTNET_ROOT/DOTNET_CLI_HOME/NUGET_PACKAGES/SolastaInstallDir):

```sh
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
# exit0, 0 errors / 0 warnings
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
# exit0, 20785 checks passed
git diff --check
# exit0, no output
```

Baseline **19 373** → final **20 785** (+**1 412**): **1 394 executed production-model checks**, отдельно **16 Cecil/definition audits** и **2 executable native-order counterexample fixtures**. Последние два только иллюстрируют недостаточность native call order; они не production execution и не Unity. Все прежние class, repertoire, invocation, Secrets, auxiliary, shared-budget и resource suites сохранены. Отдельно проверены PendingAtomicJournal на post-managed expiry (guard confirmed debt) и terminal stale-proof regression. Финальный полный прогон проходит.

**Итог по трём категориям:** code/model implemented and tested outside Unity; native transaction still blocked; Unity validation pending. Conversion не объявляется playable или runtime-complete. В disposable Solasta session не проверены actual debit/gain, callbacks/healing interactions, parent/UI, rest, persistent replay/save-load и Respec. Для native реализации нужен описанный callback/parent atomic boundary; текущий slice не включает её, reverse/SP/Pact/network или расширение mixed gameplay.

## Stage13.7B.2 — single-player native standard-slot commit

### Baseline / ограничения scope

Starting HEAD `889d71634634208b8a6862df1de5380a9401829e`, branch `feature/progression-20-to-12`. До изменений tracked tree чистый; прежний untracked SolastaFiles/Mods сохранён. SDK9.0.320; baseline Release Workflow **0 errors / 0 warnings**, **20 785 checks passed**, diff check clean. Baseline failures отсутствовали; зависимости повторно не устанавливались.

Теперь имеется **исполнитель**, не ещё один proposal: actual local receiver вызывает production FlexibleCastingNativeCommitPlan с NativeHost, который напрямую записывает original hero Sorcery counter и repertoire dictionaries. Этот путь не вызывает native CreateSorceryPoints, SpendSpellSlot, GainSorceryPoints, UsePower, Heal или RefundActionUse. Существующие raw/legacy/local/network guards не сняты.

Границы: только StandardSlots→SorceryPoints, circle1–5, одна native Sorcerer repertoire original hero, quantity1/cost1, verified local transient **base campaign** source, held campaign parent. Pact может присутствовать у владельца, но не является input и его counters/sentinel/Arcanum не меняются. Spell Points, Arcanum input, reverse, network, обычный location CharacterAction parent, unknown/modified shared-pool source, save migration/Respec остаются blocked. Существующий no-host `FlexibleCastingSlotToSorceryPlan.CommitNative` и его historical NativeBoundaryAvailable=false сохраняются как **legacy blocked API**; новый dedicated field-host executor реализован отдельно и подключён к receiver.

MixedGameplayReady=false; никакого public switch, native save import, общего multicaster admission или изменения классов/levels/history/HP/PB/prerequisites не добавлено. Ordinary gate-OFF callback возвращается до новой ветви. Actual Unity session не запускалась. Код не объявляется playable.

### Native evidence и authority

Native signatures/IL читаются отдельно от executed tests:

- RulesetCharacter.usedSorceryPoints — raw Int32 field; UsedSorceryPoints setter только stfld, без callbacks. Remaining вычисляется из actual SorceryPoints attribute и used counter. Новый host берёт native maximum/remaining из прежнего Live proof.
- RulesetSpellRepertoire.spellsSlotCapacities и usedSpellsSlots — dictionaries, присваивание ссылок не публикует observers. Ledger остаётся **тем же** bound StandardCasterResourcePlan.Budget; repertoire — view, не второе списание.
- RepertoireRefreshedHandler.Invoke(repertoire); SorceryPointsAlteredHandler.Invoke(hero,remaining,null); PowerActivatedHandler.Invoke(hero,usablePower,0) — audited native ABIs.
- AtWill branch RulesetCharacter.UsePower делает Consume(), Recharge(), PowerActivated(...,slotLevel0). Consume только remainingUses--; Recharge только remainingUses=MaxUses. Итоговое remainingUses можно заранее вычислить и записать без промежуточного observer. spentPoints не меняется. AccountUsedMagicAndPower/Heal в этой branch отсутствуют.
- UB UsePower postfix UpdateUsageForPower — no-op для non-shared source; CountPowerUseInSpecialFeatures — no-op без marker. NativeHost **rejects** shared-pool source и этот marker, не имитирует неизвестные pool/counter side effects. Для original hero отсутствует substitute-form synchronization.
- Verify main thread использует actual SynchronizationContext.Current type/assembly UnityEngine.UnitySynchronizationContext и installed private m_MainThreadID, сравнивая с Thread.CurrentThread.ManagedThreadId. Не запоминает первый попавшийся thread. Если context/field unavailable — отказ до mutation. Поле подтверждено в UnityEngine.CoreModule; фактическое reflection/read в Unity пока не выполнялось.

Trusted campaign authority остаётся прежней13.7A.2: nonce один раз при exact GameCampaignCharacter.UsePower entry; exact constructor result/effect ref/original hero/usable power/definition/lifetime; exact modal; явный immutable local envelope. NativeHost дополнительно требует ended scope + ParentHeld, actual local controller ownership, exact base implementation, verified non-multiplayer state, actual original-hero Sorcerer repertoire и unmodified AtWill remaining==MaxUses. Registered action sources сохраняют preflight support, но не получают guessed parent completion: location actions по-прежнему blocked13.6 guard.

### Исполнение и commit point

1. `FlexibleCastingPreflightContext.ReceiveLocalCommand(Envelope)` при closed gate не выполняет новую ветвь. Для разрешённого gated Standard envelope выбирается **сохранённый exact Origin/NativeHost**, затем `Invocations.NativeCommits.Execute`. Другие inputs остаются на прежнем blocked маршруте. Нет hero+circle/latest-effect/tick correlation.
2. Executor ставит in-process reentrancy guard **до** host/proof reads. Проверяются gate/main thread/local service и actual binding. Отдельно проверяются INetworkingService.IsMultiplayerGame=false и отсутствие multiplayer setup; unknown network state тоже rejected, даже при LocalCommandManager. Duplicate payload проверяется структурно и по owner/repertoire/effect/modal references. Committed exact duplicate возвращает historical receipt; **не повторяет spending, publication, parent, hide**. Изменение rules запрещает replay. Source termination после commit не разрешает новое действие, но не уничтожает historical receipt.
3. Для новой команды прежний registry/ticket заново проверяет direction, definition, owner, circles/cost, available resource, rules/conditions/power state. ManagedCommitted receipt не является native permission. Только Prepared draft допускается к native execution.
4. `PrepareFlexibleSlotViews` до mutation выбирает все original hero repertoires по тому же StandardRepertoire classifier, что existing projection; исключает independent auxiliary views. Проверяются actual owner, неизменный repertoire list, dictionary references и значения всех1–9 circles + Pact sentinel. Все views должны совпасть с validated Before. Заранее выделяются новые capacities/usage dictionaries; меняется только usage выбранного Standard circle на+1. Существующие остальные keys/Arcanum/bonus sources сохраняются.
5. Заранее снимаются snapshots invocation lists native delegates в исходном порядке; создаются raw-field write packet, coherence checks, parent/modal callbacks. Перед writes повторно проверяются main thread/local ownership, source/ticket/rules/live balance, выбранный UI circle, **все** prepared field references/values. Никаких observers/yields между этим и field batch.
6. `ApplyNativeBudget` вызывает проверенный existing operation facade ровно один раз: тот же authoritative Standard budget -1, тот же managed Sorcery snapshot +clampedGain, same operation receipt. Затем raw field batch присваивает original hero.usedSorceryPoints=max(0,beforeUsed-circle), все заранее подготовленные repertoire dictionaries и native AtWill remainingUses=prepared MaxUses. Второй SpendSpellSlot по view **никогда не вызывается**.
7. AssertCoherent подтверждает expected single budget debit, native Sorcery и every view. Retire active ticket (с сохранением receipt), mark issued source NativeCommitted, record executor receipt.Committed=true. **Это practical commit point, до первого observer.** Receipt содержит тот же Envelope.Command/operation, owner/repertoire/effect/modal, resource receipt, parent/modal status и notification-failure count.
8. Публикация после commit: repertoire listeners по каждому view; Sorcery listeners с final remaining и feature=null; explicit owned modal refresh, если его one-shot handler не был подписан; mark campaign parent finished; native PowerActivated listeners с usable power и0; owned modal Hide(true). У ordinary Convert callback подписка/AutoHide не выполнялась, поэтому NativeHost готовит нужный modal refresh явно. Parent AtWill resource final state уже staged; нет второго consume/spend.
9. Каждый captured listener вызывается **один раз**; thrown listener логируется, остальные listeners/parent/modal продолжаются. Это преднамеренная exception policy: native multicast остановился бы на первом exception, здесь receipt фиксирует committed-with-notification-failures. Acknowledgement сообщает NativeCommitted / NativeCommittedWithNotificationFailures и отдельные ParentCompleted/ModalCompleted, не claimed rollback.
10. `Binding.Projecting` устанавливается raw projection batch и удерживается на время всех notifications. Existing Refresh/PrepareRefresh guard не допускает recursive projection. В finally снимается только собственный guard; reentrant conversion от любого listener rejected. Отдельная последующая invocation может снова использовать modal и актуальный budget: old ticket перестаёт быть active, receipt/identity остаются.
11. При Hide failure не повторяются debit/grant/parent/hide. Best-effort UI recovery отключает input/raycast **только всё ещё принадлежащего этому source modal**. Чужая перепривязка не затрагивается. Partial GUI destruction/navigation recovery не обещается без Unity.

### Failure / replay policy

| Момент | Результат |
|---|---|
| Gate/thread/network/owner/domain/source mismatch, stale ticket/rules, insufficient resource | Отказ до ledger/native mutation; нет success acknowledgement |
| Preparation/reference validation/cancellation до writes | Данные ресурсов/views/parent не меняются; owned invocation закрывается, никаких refunds |
| Reentrant call во время подготовки или любого notification | PendingAtomicJournal до новых mutation; outer conversion не повторяется |
| Полный field batch + receipt coherent | Committed независимо от последующих notification exceptions |
| Repertoire/Sorcery/modal-refresh/PowerActivated listener throws | Receipt и оба resources committed; логирование, оставшиеся listeners и UI completion once; no replay |
| Hide/UI recovery failure | Committed-with-notification-failures; ParentCompleted/ModalCompleted отражают результат, никаких выдуманных success/refund |
| Exact duplicate после commit / после effect termination | Возвращается тот же historical receipt, без повторных events или parent/UI действий |
| Conflict same command ID / changed bindings / changed rules | Отказ без нового расхода и без замены receipt |
| Cancel committed command | PendingAtomicJournal; no rollback/debt reset |
| Unexpected interruption внутри raw write/receipt batch | Indeterminate, no refund/replay; require inspection/restart. Никакой гарантии для process termination, OOM, ThreadAbort или внешнего потока, меняющего game state |

Practical guarantee основана на single Unity thread, заранее проверенных references/values и observer-free raw assignments. Прямые writes/методы промежуточного batch не вызывают callbacks. Существующий coordinator может выделять внутренние receipts/hash state; OOM между этими writes **не** считается recoverable failed transaction. Catch не используется для маскировки partial state как rolled back. При indeterminate active reservation остаётся блокирующей; persistence/crash recovery не реализована.

### Coverage / evidence matrix

| Route / механизм | Реализация | Evidence |
|---|---|---|
| Verified base campaign source → exact modal → standard native commit | Connected actual receiver + NativeHost + existing owner/ledger | Production executor executable tests с fake native fields; game host compiled/audited, Unity-unverified |
| Raw original hero Sorcery + all relevant native views | Dedicated callback-free field batch, no second budget/view debit | Fake-port coherence observer tests; actual stfld/ABI audits |
| Registered location source | Preflight retained; native parent unsupported | Explicit native reject + existing action guard; not enabled |
| Native events / campaign parent / modal | Ordered once-only publication, committed notification-failure policy | Executed fake native port; actual delegate ABI/native AtWill/GUI IL inspected |
| Second legitimate invocation / duplicate / cancellation | Active reservation retired, receipts retained, exact identity match | Executed production adapter tests |
| Spell Points/Pact/Arcanum/reverse/network | No native execution | Executed rejection tests + unchanged legacy guards |
| Ordinary gate OFF | Existing path unchanged, new receiver exits | Complete old regressions + compiled branch inspection |
| Save/load, Respec, durable replay / real Harmony/UI path | Not implemented/validated | Unity pending; no save migration |

Executable tests invoke the **same FlexibleCastingNativeCommitPlan.Execute** used by the real local receiver, with fake field/notification hosts. Они проверяют actual existing budget arithmetic и native-counter/view fixture writes, а не отдельный parallel transaction algorithm. Native Unity host, reflection access, Harmony patch ordering и actual GUI/parent callbacks **не исполняются** этими tests. Cecil audits не считаются Unity execution.

### Проверки в disposable Solasta session (ещё не выполнены)

Нужен будущий controlled internal new-hero binder/test harness; public gameplay gate не включается этим коммитом. Не менять existing user options автоматически, не использовать старые saves. Для standard-slot теста требуется отдельный test profile без Spell Points; unsupported profile должен дать отказ без расходов.

- Original Sorcerer repertoire: записать capacities/used/remaining standard slots всех participating views, actual used/remaining Sorcery и maximum. Например circle3 +3, либо +1 при missing1; ровно один slot расходуется. Full cap — native clamp0, без overflow/heal.
- Base campaign power use: exact source effect/nonce/modal; MainThreadID, held parent и local controller. Все repertoire/Sorcery listeners должны видеть оба ресурса и committed receipt согласованными.
- Native PowerActivated(...,0), AtWill remaining final value, one modal refresh/hide и parent completion по одному разу. Нет UsePower/Consume extra usage, false refund или second view debit.
- Повторить exact envelope, conflict envelope, late delivery, Cancel до commit, source termination, новый power-use после завершения. Проверить source/receipt IDs и отсутствие повторных effects/events.
- Controlled throwing listener на каждом notification stage: ресурсы остаются committed, остальные notifications/parent/UI обрабатываются, duplicate не повторяет failed/successful listeners. UI hide failure — committed status и safe owned-input recovery, без вмешательства в другой modal.
- Reject Spell Points, Pact, Arcanum, reverse, network, registered/location parent и wrong owner/repertoire/level/cost/rules; ресурсы неизменны. Gate OFF single-caster behavior сравнить с обычным UB.
- Save/load, Respec, crash recovery и normal gameplay readiness не считать доказанными этим smoke test; это отдельные этапы.

Изменённые файлы:

- Models/FlexibleCastingNativeCommitPlan.cs — новый production executor/native journal/notification policy.
- Models/FlexibleCastingNativeCommitContext.cs — новый actual Unity field/notification/main-thread host.
- Models/StandardMulticasterResourcesContext.cs — callback-free prepared projection и scoped refresh guard.
- Models/FlexibleCastingCampaignPatches.cs — exact held parent сохраняется до commit.
- Models/FlexibleCastingInvocationPlan.cs — native source/receipt/ack lifecycle.
- Models/FlexibleCastingPreflightContext.cs — actual local dispatch и success/error handling.
- Models/FlexibleCastingPreflightPlan.cs — retire committed reservation без удаления debt/receipt.
- Models/FlexibleCastingSlotToSorceryPlan.cs — native application/confirmation states; legacy unbound entry blocked.
- Tests/FighterProgression/FlexibleCastingNativeCommitTests.cs — новые executor fake-port tests и отдельные native audits.
- Tests/FighterProgression/FlexibleCastingPreflightTests.cs и FighterProgression.Tests.csproj — runner/source links.
- Documentation/FlexibleCastingPreflight.md.

Финальный полный прогон (SDK9.0.320):

```sh
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
# exit0: 0 errors / 0 warnings
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
# exit0: 21919 checks passed
git diff --check
# exit0: no output
```

Baseline **20 785** → final **21 919** (+**1 134**): **1 120 executed production commit-adapter checks with fake native field/notification ports**, отдельно **14 compiled/native signature audits**. Все прежние class, resource, spell-learning, Bard Secrets, Warlock invocation/Pact/Arcanum, auxiliary/third-caster и transaction regressions сохранены. Ни одна проверка не названа Unity execution.

**Итог:** single-player campaign native path implemented and code-tested at the production executor boundary; actual Unity field host compiled/audited, Unity-unverified. Обязательна disposable in-game проверка real Harmony/source/controller/main-thread/GUI/resource пути до normal gameplay. MixedGameplayReady=false; public activation, location-action completion, reverse/SP/Pact/network, save/load/persistent replay/Respec не добавлены. Release/пакет мода не публиковались; proprietary assemblies, outputs, settings и saves не включаются в коммит.
