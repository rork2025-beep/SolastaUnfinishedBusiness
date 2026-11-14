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
