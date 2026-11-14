# Stage13.7C.2 — native finalization: editor candidate only

**Текущий статус:** production QA integration для свежего Sorcerer0→1→2 реализована и прошла executable fixtures. **Wizard1 / Warlock1: BLOCKED. Campaign admission, XP, Flexible Casting и in-game smoke по-прежнему закрыты до C.3 и фактической Unity validation.** `MixedGameplayReady=false` в обеих сборках. Это не migration/import существующего героя.

## Scope и доверенный вход

Starting commit `d71a9b727abce2522ce3c8d2ac2c39179776668a`, feature branch. Baseline повторён: SDK9.0.320, **23 321 checks**, Release Workflow0 errors/0 warnings, tracked tree clean; существующий untracked Mods не включается в Git.

В compile-only QA меню **QA13.7C.2 — EDITOR ONLY** есть deliberate arm следующего нового героя. Arm разрешён только вне Game/GameCampaign, при idle editor, проверенных local building command/services и Unity main thread. Нужны ранее включённые EnableLevel20, EnableMulticlass и latched Sorcerer compression; QA не меняет настройки. В текущем процессе допускается один candidate.

`CreateNewCharacter` Postfix (после обычной регистрации LevelUpHelper) связывает **точный новый объект**, manager и settings fingerprint. Внутри этого доверенного level0 boundary вызывается общий BindNewHeroCore: те же Standard owner, MultipleSpellRepertoires session и Pact ledger. QA=true дополнительно проверяется через RequireZeroAuthority, а ordinary build отвергает QA=true до добавления owner. RuntimeEnabled остаётся const false: binding editor-кандидата не открывает ни один campaign resource hook. Объект, загруженный из pool/save с тем же GUID/name, не получает эту authority.

Поддерживается только installed selection-known Sorcerer casting (native подготовка может использовать captured prepared rules), неизрасходованный editor state: Sorcerer1/2, без conditions, auxiliary race/background/feat casting, invocation grants, дополнительной slot capacity/preparation affinities или текущих extra choice-bearing subclass grants. Некастующие race/background/feats и допустимый Sorcerer origin остаются native choices; неподдержанный источник блокируется. Уровни1/2 имеют effective1/2, вычисленные существующим калькулятором из original captured profile. Mapping выше2 и actual class history не переписываются.

## Installed call order и точка успеха

Аудит выполнен по supplied Assembly-CSharp.dll, Cecil; это не Unity execution.

1. `CharacterBuildingManager.CreateNewCharacter()` создаёт hero и building data, ставит currentLocalHeroCharacter, инициализирует attributes/pools и active features. Класс ещё не назначен.
2. `CharacterStageClassSelectionPanel.Refresh` вызывает Unassign/Assign. UB может подавлять оба native вызова во время level-up class preview. `CharacterStageLevelGainsPanel.EnterStage/RefreshCharacter` выполняет настоящую AssignClassLevel через IHeroBuildingCommandService после выхода из preview. QA cancel prefix имеет bool return/Priority.Last и не вмешивается в подавленный UB preview. QA class panel пересекает уже вычисленный eligible list с Sorcerer, проверив его присутствие; native selectedClass — индекс0. Prerequisites не обходятся, автоматический preview другого класса не назначается.
3. QA Assign prefix выдаёт advancement ID один раз у легитимного entry с подтверждённой предыдущей history. Native `AssignClassLevel` вызывает AddClassLevel, создаёт class tag, считает HP и уведомляет UI. Это **tentative native draft**, не confirmed QA result. Подавленный assign закрывает неиспользованный ticket; настоящий следующий entry получает новый ID. Finalize retry не генерирует новый ID.
4. Native `LevelUpCharacter(hero,force,canUnlearn)` при force=true может поднять XP. C.2 запрещает force=true и NoExperienceOnLevelUp cheat; QA editor action передаёт force=false, без GrantExperience/изменения XP. Она открывает стандартный CharacterLevelUpScreen на **том же объекте**, не загружая pool copy. Menu/GUI handoff остаётся Unity-unverified.
5. `CharacterEditionScreen.OnFinishCb` валидирует текущую stage, затем через IHeroBuildingCommandService → LocalCommandManager вызывает `FinalizeCharacter(hero,allLevelsAtOnce:false)`. QA prefix Priority.First+100 выполняется **до первых UB/native grants**.
6. QA preflight требует issued ticket, exact hero/manager/data, реальные class/history, unchanged settings, законченные mandatory native pools (optional unlearn отдельно) и complete production MultipleSpellRepertoires.Transaction. Class tag берётся из installed GetClassTag. Acquired/Unlearned/Cantrip выборы принадлежат только этому Sorcerer event. Race/background/feat spell roots и неизвестные extra tags отвергаются до grants. Prerequisites выбранных feats проверяются. Сохраняются selections, subclass, race/background и ресурсы в fingerprint/witness.
7. `SorcererFinalizePreflight.Inspect` для обычного mixed draft **не ослаблен**. Новый RequireQaDraft разрешает только тот же ID, target1/2 и полный Sorcerer transaction в QA preflight. Это дополнительная authority перед native body; обычные guards не выключаются.
8. После разрешения выполняются прежние UB Prefix grants, native FinalizeCharacter: skills/tools/expertise/feats/invocations/metamagic/languages, class/racial repertoire creation or upgrade и GrantCantripsAndSpellsByTag. В конце единственный `UpdateCharacter` вызывает AutoPrepareSpells, RestoreAllSpellSlots, GrantInvocations, GrantPowers, refresh attributes/HP и ComputeCharacterLevel. Native GrantPowers очищает и заново строит usable powers; это native поведение, не дополнительный QA power grant. Поэтому C.2 не пытается сохранить расход ресурсов игрового героя — такой state запрещён заранее.
9. QA transpiler устанавливает `NativeReturned` **перед единственным ret после UpdateCharacter**. Constructor, callback или complete draft не подменяют этот witness. Проверяется native method shape. Затем выполняются обязательные UB postfixes, включая grant helpers, sort repertoires и UnregisterHero.
10. QA Harmony Finalizer при отсутствии исключения/skip и наличии tail witness сверяет result: actual classes/history/CharacterLevel, unchanged XP и Sorcery usage, native class feature presence, exact Sorcerer repertoire/Charisma, selected known/cantrip inventory без duplicates, prepared spell access/limit, сохранение прежних features, и Sorcerer2 native Sorcery maximum2 + обе Flexible Casting powers. Подмена owner/правил/выборов не считается успехом.
11. Только после этих проверок готовится publication packet. Существующий Budget.Refresh применяет allocation, тот же Pact owner остаётся нулевым, MultipleSpellRepertoires сохраняет native selection origins, подтверждённые auto/bonus tags и подготовку через Transaction.Commit, и присваиваются заранее выделенные native slot/SP views. Никаких повторных GrantFeature/GrantSpell/GrantPower и второго resource ledger нет. Native canonical SP power берётся из финального original GrantPowers; второй pool не создаётся адаптером.
12. После coherence check записывается **Committed receipt / ConfirmedActual**, затем единожды публикуются наши RepertoireRefreshed listeners. Sorcery counter не дебитуется/не восстанавливается адаптером; HP/PB/class/history остаются native. После ReleaseCharacter native ClearLists удаляет pending UI data — это cleanup, не дополнительное подтверждение.

Существующие native callbacks внутри Finalize/UpdateCharacter не превращены в общую atomic транзакцию. Практическая гарантия C.2: adapter не синхронизирует/не подтверждает результат до успешного native return + UB обработки; после этого ledger/views согласуются перед **нашими** observers. Нельзя утверждать, что частично упавший native finalizer вообще не изменил hero: tentative/native grants могли уже произойти. Это отражается как Indeterminate, без rollback и без допуска к campaign.

## Retry, cancel и failure

- Identical finalize retry в ещё действительном editor binding возвращает recorded result без native body, model commit, refresh или уведомлений. Повторный tail/finalizer callback не создаёт второй grant. Изменённый payload/rules/target отвергается.
- Incomplete preflight не начинает grants; UI может исправить choices с тем же issued ID. Cancel/Unassign до native execution закрывает ticket; native undo остаётся штатным, QA не изобретает financial refund. Новый настоящий Assign entry после undo получает новый ID.
- Reentry отклоняется. Prefix сохраняет Harmony __state **после** успешного Begin, поэтому отклонённый вложенный callback не завершает внешний journal.
- Ошибка native grant, пропущенный body/return marker, изменённые правила после return, ошибка UB postfix либо несовпавший result → **Indeterminate**. Дополнительного adapter grant/refresh/ack нет; повторное выполнение блокируется. Частичные native изменения не объявляются откаченными. Ошибка в самой synchronization тоже блокирует candidate; нельзя импортировать его состояние заново.
- Исключение нашего notification после committed receipt — committed delivery failure: счётчик ошибки/лог, без повторного grant или acknowledgement.
- После release, End QA, смены текущего draft, manager или settings authority может стать stale; replay не используется как способ обойти новый preflight. Candidate/receipts только in-memory, persistent replay safety не заявлена.

## Wizard1 / Warlock1

Оба варианта **явно BLOCKED до assignment**. Не ослаблены CanSelectClass/способности/класс-cap/трёхклассовое ограничение. Для Wizard отсутствует native переход second-class spellbook creation/pages/source-local choices в этом QA coordinator; для Warlock — second-class native repertoire/invocation/pact initialization с подтверждённым source journal. Их backend модели сохранены, но не подменяют native finalization. C.2 не разрешает третий класс.

## Editor QA действия и ограничения

1. Использовать отдельный QA artifact `13.7C.2-EDITOR-ONLY` с full SHA, сохранив обычную mod папку/Settings.xml вне Mods, как в инструкции отката ниже. Source/panel доступны только при compile flag; не public toggle.
2. В main-menu QA panel подготовить следующего нового кандидата. Создать disposable noncasting-race/background Sorcerer обычным creation UI, завершить все native choices. Подтверждение должно показать command ID, Committed и confirmed actual1. Это **ожидание для будущей Unity проверки**, не выполненный тест.
3. При закрытом первом редакторе выбрать этот же candidate в panel и открыть native editor draft Sorcerer2. Source XP не увеличивается; force=false. Заполнить choices и Finish. Ожидается другой issued ID, confirmed actual2, тот же Standard owner, class history Sorcerer/Sorcerer, native Sorcery2 и собственный repertoire. Gameplay XP controls остаются disabled.
4. Wizard/Warlock, imports, campaign, networking, respec, conditions и used resources не проверять как supported. При Indeterminate сохранить diagnostic report и остановить процесс; не сохранять/не использовать candidate как игровой герой.
5. End QA → полностью закрыть игру → восстановить ordinary mod папку с первоначальными settings. QA UI/hooks должны отсутствовать. Никакие пользовательские options/saves не мигрируются.

Отчёт остаётся в `<Solasta>/Mods/SolastaUnfinishedBusiness/QaDiagnostics/FlexibleCasting-QA-latest.txt`. Добавлены C.2 command/state/confirmed actual/failure. Копируйте снимки под разными именами; fresh editor binding не переносится в campaign copy. Для in-game smoke обязательно нужны **C.3 и actual Unity validation**.

## Проверки C.2

Фактические результаты (SDK9.0.320):

```sh
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2 -warnaserror
# exit0; 0 errors / 0 warnings
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
# exit0; 23468 checks passed
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2 -warnaserror \
  -p:SolastaFlexibleCastingQa=true -p:QaSourceCommit=<full-source-SHA> -p:OutputPath=/tmp/c2-qa/
# exit0; 0 errors / 0 warnings
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj -- \
  --qa-audit /tmp/c2-qa/SolastaUnfinishedBusiness.dll <full-source-SHA>
# exit0; 33 QA compiled audits (отдельный запуск)
git diff --check
# exit0; no output
```

Baseline **23 321** → final **23 468** (+147): **141 executed production-adapter/controlled-native-fixture checks**, **5 native/compiled audits**, **1 дополнительная metadata isolation check**. Все старые Fighter/Barbarian/Monk/Rogue/Wizard/Sorcerer/Warlock/Bard/Cleric/Druid/Paladin/Ranger/Inventor, auxiliary/Secrets/third-caster/resource и четыре Flexible Casting/fifth-route rejection suites включены. Старые metadata checks binders обновлены на тот же guarded core call chain; новые checks подтверждают QA=false/throw в обычной сборке и отсутствие native/Harmony зависимостей у pure adapter.

Fixtures независимо воспроизводят staged selections и native grant outcome, сравнивают их с production MultipleSpellRepertoires/Standard/Pact plans. Покрыты оба resource modes, first grant/advancement, preparation, optional replacement/original origins, incomplete/stale/cancelled/wrong owner/rules/resources, duplicate/tail callbacks, reentry, native failure, skipped/missing return, post-native failure и synchronization/notification exceptions. Fixture grant, Cecil target/argument check и compiled QA hook **не являются запуском Unity**. Native GUI/menu/service registration и actual Harmony execution остаются UNVERIFIED.

Минимальный путь **Sorcerer0→1→2 code-complete, Unity-unverified**. Wizard1/Warlock1 не реализованы и явно blocked. Campaign smoke не готов до **C.3 + actual Unity validation**.

Изменённые production files: QaEditorFinalizationPlan.cs, QaEditorFinalizationContext.cs, QaEditorResourceBindings.cs (новые); StandardMulticasterResourcesContext.cs, MultipleSpellRepertoiresContext.cs, PactMagicMulticasterContext.cs, SorcererFinalizePreflight.cs, FlexibleCastingQaContext.cs, FlexibleCastingQaPlan.cs, FlexibleCastingQaDisplay.cs, основной csproj. Tests: новый QaEditorFinalizationTests.cs, FlexibleCastingPreflightTests.cs, FlexibleCastingQaTests.cs, CompressedMulticasterTests.cs, MultipleSpellRepertoiresTests.cs, PactMagicMulticasterTests.cs, тестовый csproj. Также изменены package-flexible-casting-qa.py, этот документ, checklist и FlexibleCastingPreflight.md. Native DLL/game assemblies, generated binaries, user settings/saves не входят в commit.

Изменённые файлы и полный diff находятся в feature commit. Новые production файлы: QaEditorFinalizationPlan.cs, QaEditorFinalizationContext.cs, QaEditorResourceBindings.cs; QA panel/identity/checklist обновлены, общие owner binders выделены в guarded core. Tests включают production-adapter fixtures и отдельные compiled/native audits; прежние guards и четыре Flexible Casting маршрута/blocked fifth остаются в полном regression run.

---

# Архив Stage13.7C.1 (статус до C.2; инструкции packaging/rollback ниже остаются применимыми)

# Stage13.7C.1 — QA diagnostic build: BLOCKED

**Это не готовый in-game conversion smoke build.** Установочный QA diagnostic package подготовлен; native QA admission, выдача XP и четыре guarded conversions в нём недоступны. Unity не запускалась. `MixedGameplayReady=false` в обычном и QA вариантах. Пятый маршрут Sorcery→temporary standard slot сохраняет PendingAtomicJournal до debit.

## Что установлено аудитом

Baseline `b541b122880fcfa7cf85307c7fb5ad327cac82c0`, branch `feature/progression-20-to-12`, tracked tree clean, прежний untracked `SolastaFiles/Mods/` не включается в Git. SDK9.0.320; повторено **23 251 checks**, Release Workflow0 errors/0 warnings, diff check exit0. Переустановка зависимостей не нужна.

1. `CharacterBuildingManager.CreateNewCharacter()` создаёт новый `RulesetCharacterHero`, устанавливает `currentLocalHeroCharacter`, регистрирует attributes, инициализирует pools/lists и обновляет active features; класс ещё не назначен. После этого можно **наблюдать** точный level-0 объект. Это не доказывает binding ресурсов или готовность finalization.
2. `LevelUpCharacter(hero, force, forceClass)` уже работает с существующим героем и draft/pools. UB RegisterHero выполняется в Prefix. Нельзя просто пересчитать бюджет по изменённому draft и считать уровень подтверждённым.
3. Prefix `CharacterBuildingManagerPatcher.FinalizeCharacter_Patch` до race/feat/native grants вызывает `ValidateSorcererFinalizeEntry`, auxiliary validation и `ValidateBeforeFinalize`. `SorcererFinalizePreflight.Inspect` отклоняет incomplete draft и возвращает **PendingAtomicJournal даже для полного draft**. `MultipleSpellRepertoiresContext.ValidateBeforeFinalize` допускает только отсутствие pending transactions. Нет native commit coordinator и подтверждённого переноса spellbook/origin/resource state. QA не обходит эти guards.
4. `GameCampaign.CreateParty` создаёт контейнер и начальную позицию, но не загружает героев. Реальная загрузка выполняется в `GameManager.<CreateGameAsync>.MoveNext`: `ICharacterPoolService.LoadCharacter` → новый `GameCampaignCharacter` → `GameCampaignParty.AddCharacter`. `CharacterPoolManager.LoadCharacter` создаёт **другой** `RulesetCharacterHero`, десериализует его и вызывает PostLoad. CWT binding к object reference из редактора не переносится. GUID/name совпадение не является доказательством; автоматический import/save migration запрещён.
5. В current code Standard BindNewHero требует actual level0, captured sources и общий gate. Последующий UpdateProgression требует согласованного native class/repertoire state. Простое открытие global gate оставило бы других членов партии unbound. Такой путь не реализован.
6. Native `RulesetCharacterHero.GrantExperience(int)` меняет Experience.BaseValue, делает Refresh, AccountEarnedXP, вызывает ExperienceGained; не меняет class level/history. UB prefix умножает входной XP согласно MultiplyTheExperienceGainedBy. Native UI использует `ExperienceThresholds[actualCharacterLevel]` как следующий порог. Preview считает только недостающую разницу и отклоняет нестандартный положительный multiplier. Однако **native XP action закрыт**, поскольку нет admitted original owner и рабочего native advancement.

Это blocker в коде, а не требование сначала запустить Unity. Чтобы выполнить исходный smoke objective, нужны согласованный native finalization нового героя/advancement и trusted editor→fresh-party transfer для **всей** партии. В этом slice они не подменены частичным импортом или отключением защиты. Все requested conversion smoke scenarios остаются NOT AVAILABLE.

## Реализованная изоляция и панель

- `SolastaFlexibleCastingQa=true` добавляет compile symbol `SOLASTA_FLEXIBLE_CASTING_QA`. Без него отсутствуют QA menu/context/Harmony hooks и QA assembly metadata. Runtime gate остаётся const false.
- Build identity: `13.7C.1-DIAGNOSTIC-BLOCKED`; full source SHA находится в assembly metadata, панели и QA-MANIFEST.json. Кандидатные dirty builds не являются финальными артефактами. Packaging разрешён только из clean committed feature branch; SHA финального пакета совпадает с исходниками.
- Новый пункт существующего UMM/mod menu: **QA 13.7C.1 — BLOCKED**. Панель показывает admission code/reason, selected original native hero GUID, observed-at-zero status, actual class/character levels/history, XP/next threshold, native Sorcery/SP balances, slot dictionaries/Pact sentinel, repertoires/abilities/known spells и последнюю QA rejection/source status. Native/legacy views явно не выдаются за bound multicaster budget.
- Любой loaded save, character-pool copy, Respec или неизвестная party membership не получает QA resource owner. Даже синтетический полный level0 proof возвращает NativeFinalizationUnavailable. Вся партия отклоняется при хотя бы одном unbound участнике; нет частично открытого global path.
- QA-only CreateNewCharacter Postfix сохраняет только weak observation текущего level0 объекта. Не вызывает BindNewHero, UpdateProgression, native grants, XP или finalization.
- QA campaign UsePower отклоняет две exact registered Flexible Casting powers до native power body; modal OnConvertCb отклоняет direct conversion до debit. Identity-less LocalCommandManager conversion calls также отклоняются. QA Respec functor возвращает пустой IEnumerator, не заменяя героя. Эти compiled guards **не проверены в Unity**; unsupported location parents не объявлены рабочими.
- `Grant XP to next level` видна, disabled с причиной. Прямой вызов action тоже отклоняется. XP preview и arithmetic tests не являются реализацией native выдачи XP.
- `End QA session` закрывает diagnostic run до перезапуска процесса; не сохраняет QA controls в Settings.xml. Никакие текущие game/user settings не меняются программно ради QA admission.
- QA updater не запрашивает ordinary release и не заменяет себя; ручной update action блокируется. Обычный updater не изменён.

**Точный путь отчёта:** `<Solasta>/Mods/SolastaUnfinishedBusiness/QaDiagnostics/FlexibleCasting-QA-latest.txt`. Кнопка «Сохранить диагностический снимок» пишет только туда; предыдущий снимок с тем же именем заменяется. Скопируйте его после каждого шага в отдельную папку с указанным в checklist именем. Save files/settings не редактируются. TextArea даёт также копируемый текст. При ошибке записи причина видна в панели. UMM log содержит QA build identity и rejection.

## Сборка и упаковка (для оператора окружения)

Из корня checkout, с существующими Solasta reference assemblies и восстановленными NuGet dependencies:

```bash
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.setup-solasta/dotnet-home
export NUGET_PACKAGES=/workspace/.setup-solasta/nuget DOTNET_CLI_TELEMETRY_OPTOUT=1
export SolastaInstallDir=/workspace/SolastaUnfinishedBusiness/SolastaFiles

dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
git diff --check
# После commit/review, из чистой feature ветки:
python3 Scripts/package-flexible-casting-qa.py --out /workspace/artifacts/flexible-casting-qa
```

Packager запускает отдельную Release Workflow QA сборку с `-warnaserror`, `-p:SolastaFlexibleCastingQa=true`, full `QaSourceCommit` и временным изолированным OutputPath; исходный mod output не используется для упаковки. QA metadata audit выполняется отдельной командой runner `--qa-audit <QA.dll> <fullSHA>`. В ZIP включаются mod DLL, явно разрешённые private dependencies из lib, mod assets/translations, отдельный Info.json и эти инструкции. **Нет Settings, game DLL, saves или proprietary assemblies.** Manifest содержит SHA256 каждого файла, sidecar — SHA256 ZIP. Packaging не публикует release и не делает push сам.

Необходимо предоставить ZIP пользователю отдельным каналом: доступных инструментов загрузки workspace-файла в этот чат нет. Наличие локального ZIP не означает, что пользователь уже получил его или может скачать по локальному пути. Оператор может воспроизвести packaging из указанного commit и передать полученный архив. Пакет diagnostic-only; не называть его conversion-ready.

## Установка и откат без программирования

Установка имеет смысл только для проверки diagnostic panel, не для conversion smoke.

1. Полностью закройте Solasta. Скопируйте существующую папку `Mods/SolastaUnfinishedBusiness` **за пределы Mods**, например в `UB-ordinary-backup`. В ней остаются исходные Settings.xml и остальные пользовательские файлы. Исходную папку также перенесите из Mods; не объединяйте файлы двух сборок.
2. Распакуйте ZIP так, чтобы был один путь `Mods/SolastaUnfinishedBusiness/Info.json` рядом с QA DLL. В UMM должен быть один UB с именем **UB QA 13.7C.1-DIAGNOSTIC-BLOCKED [SHA]**. Никогда не запускайте обе версии одновременно.
3. Не переносите старые настройки автоматически. Не открывайте настоящие saves ради теста. Запустите игру, откройте UMM (обычно Ctrl+F10) → UB → **QA 13.7C.1 — BLOCKED**, сравните full SHA с manifest. Если отличается, остановитесь. Следуйте одностраничному checklist.
4. Для отката нажмите **End QA session**, полностью закройте игру, переместите QA папку вне Mods (сохранив отчёты), восстановите исходную папку из backup. Запустите обычную сборку: QA пункта быть не должно. Ничего не удаляйте из saves и не открывайте одноразовый тестовый save как обычный совместимый save.

## Smoke matrix и checkpoint план

| Сценарий | Ожидание после будущего validated admission | В этом QA artifact |
|---|---|---|
| Fresh Sorcerer2→Sorcerer2/Wizard1, обычный long-rest UI | Native choice/finalization + независимые repertoires | BLOCKED; нет native QA admission/advancement |
| Sorcerer+Warlock, затем опционально третий разрешённый класс | Native prerequisites; максимум3 | BLOCKED |
| Standard slot→Sorcery | Один debit/clamped gain | Backend B.2 сохранён, QA execution BLOCKED |
| SP→Sorcery; Sorcery→temporary SP | Один existing pool; native costs/temporary credit | Backend B.3 сохранён, QA execution BLOCKED |
| Pact→Sorcery, Shift/priority/lossy circle | Реальный Pact debit; Arcanum unchanged | Backend B.3 сохранён, QA execution BLOCKED |
| Sorcery→temporary standard slot | PendingAtomicJournal до debit/condition/parent completion | Fifth backend guard сохранён; QA entry отклоняется ещё раньше |
| Short/long rest после conversion | Coherent before/after resources, no duplicate grant | NOT RUN; нет successful QA conversion |

План поздних checkpoints (пример Sorcerer12/Wizard8, **все NOT RUN**; не является обходом multiclass prerequisites):

| Character level | Actual classes | Projected class features/access |
|---|---|---|
| 2 | Sorcerer2 | Original Sorcerer1–2, Flexible Casting source availability |
| 3 | Sorcerer2/Wizard1 | Sorcerer original2 и Wizard original1; обычный long-rest class choice |
| 12 | Sorcerer11/Wizard1 | Sorcerer original1–17; Wizard только original1 |
| 13 | Sorcerer12/Wizard1 | Sorcerer original1–20; **Sorcerer13 запрещён** |
| 20 | Sorcerer12/Wizard8 | Sorcerer original20, Wizard original11; total cap20 |

В первом будущем conversion smoke достаточно уровней2/3. Higher checkpoints, full class unlocks, shared slots/spell access, subclass behavior и runtime cap enforcement нельзя отмечать verified до фактического прохождения. Даже при высоких shared slots доступ Wizard не становится автоматически original20. Смена Slots/SP требует отдельного чистого запуска/новых test heroes; QA не переключает пользовательские flags.

## Валидация и ограничения

Фактические результаты в подготовленном SDK9.0.320 окружении:

| Команда | Exit | Результат |
|---|---:|---|
| `dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2 -warnaserror` | 0 | 0 errors / 0 warnings |
| `dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj` | 0 | **23 321 checks** против baseline23 251 |
| QA build с отдельным OutputPath, define и full source identity, `-warnaserror` | 0 | 0 errors / 0 warnings |
| Runner `--qa-audit <QA.dll> <fullSHA>` | 0 | **20 compiled audits**, отдельно от полного regression count |
| `git diff --check` | 0 | no output |
| `python3 -m py_compile Scripts/package-flexible-casting-qa.py` | 0 | script syntax valid |
| QA validation target с invalid SHA / установленным обычным OutputPath | 1 / 1 | Два ожидаемых отказа; не build regressions |

Прирост полного runner **+70**: **61 executed production readiness/XP-preview check**, **9 ordinary/native metadata audits**. Проверены отсутствие QA entry в ordinary build, запрет даже синтетического fully-bound admission, duplicate/unbound/loaded identity, actual history/caps, terminal End, XP thresholds1–19 из installed native table, cap20 и multiplier rejection. Это **не тесты успешного native admission, XP grant или конверсий**. 20 QA audits проверяют metadata/SHA, menu interface, actual Harmony target existence, closed native/XP/Respec entry и отсутствие открытия global gate. Early candidate warning об unset updater flag исправлен; финальные обе сборки без предупреждений.

Изменённые файлы: `Models/FlexibleCastingQaPlan.cs`, `Models/FlexibleCastingQaContext.cs`, `Displays/FlexibleCastingQaDisplay.cs`, `Models/UpdateContext.cs`, основной `.csproj`; `Tests/FighterProgression/FlexibleCastingQaTests.cs`, тестовый `.csproj`, `Program.cs`; `Scripts/package-flexible-casting-qa.py`; `Documentation/FlexibleCastingQaBuild.md`, `FlexibleCastingQaSmokeChecklist.ru.md`, дополнение `FlexibleCastingPreflight.md`. Финальный SHA находится в Git, assembly metadata и manifest; ZIP собирается после commit из чистого feature HEAD и не включается в Git.

Code/model checks, Cecil audits и packaging integrity проверяются отдельно от Unity. В настоящем Solasta не проверены menu discovery/Harmony, resource/UI/parent callbacks, character-pool ownership, long-rest advancement, XP events, save/load или Respec. Stage13.8, fifth lifecycle route и native finalization coordinator не реализованы. **Stage13.7C.1 smoke objective NOT READY; diagnostic artifact only.**
