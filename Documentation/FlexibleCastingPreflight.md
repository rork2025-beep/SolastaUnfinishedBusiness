> **Текущее состояние — Stage 13.7A.2:** см. раздел в конце документа. Есть typed authority для registered и проверенного base campaign transient effect, явный локальный identity-only envelope и fail-closed receiver. Native network/ordinary action completion остаются blocked; **Stage 13.7A not ready**. Описание 13.7A ниже сохранено как история baseline. В частности, прежний cached proof после termination заменён строгой проверкой lifetime и актуального registry membership.

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
