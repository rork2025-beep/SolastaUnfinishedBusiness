# Stage 4 — Wizard 20→12

**PARTIALLY READY.** Реализован прототип полной таблицы классового колдовства Wizard,
но этап не объявляется завершённым: смешанное колдовство и безопасная миграция книг при respec
остаются незавершёнными. Нового чистого Wizard можно использовать для контролируемых игровых
проверок по плану ниже. Unity/Solasta здесь не запускались. Основные сохранения не использовать.

## Исходное состояние и проверка среды

Работа продолжена в существующем `/workspace/SolastaUnfinishedBusiness`, ветка
`feature/progression-20-to-12`, исходный HEAD `f43a8c409d0abd21f3109364df6104c39356191e`.
Fighter `6c226e662c`, Barbarian `a0dcb5d762`, Monk `bd004f9283`, Rogue `f43a8c409d`
присутствовали до изменений. Существующие исходники не были незакоммичены;
неотслеживаемый `SolastaFiles/Mods/` содержал локальные результаты сборки.
До Wizard прошли 307 проверок и Release Workflow: 0 ошибок, 0 предупреждений.
.NET SDK 9.0.320 уже установлен; переустановка зависимостей не потребовалась.
Новая рабочая копия/worktree не создавалась, release не изменялась.

## Архитектура и включение

Новый `EnableWizardCompressedProgressionPrototype` выключен по умолчанию. В интерфейсе
Classes включить `Wizard 20 to 12 prototype (NEW HEROES; restart; SINGLE CASTER ONLY)`,
затем полностью перезапустить игру и создать отдельного тестового героя.
Нужны уже включённые `EnableLevel20` и `EnableMulticlass`; иначе модуль не активируется.
Другие настройки, включая `MaxAllowedClasses`, требования характеристик,
`UseAlternateSpellPointsSystem` и целевой `EnableRespecAction=true`, модуль не меняет.
Пользовательский Settings.xml не редактировался и не заменялся.

Для выключения убрать только этот переключатель и перезапустить игру. Это восстанавливает
обычные definitions при следующей загрузке мода, но НЕ конвертирует сжатого героя обратно.
Не открывать и не пересохранять одного героя попеременно в двух режимах.
Существующие Wizard выше12 не мигрируются: выводится ошибка, повышение блокируется.
Остальные старые Wizard также не являются поддержанной миграцией — новый тестовый герой обязателен.

Использованы существующие `FighterProgressionPlan`, `CompressedProgressionDefinitions`,
одноразовая session initialization и способы интеграции предыдущих классов. Wizard обрабатывается
после UB, 2024, регистрации подклассов, Spell Points и предыдущих прототипов. Порядок исходных
событий, повторные награды, происхождение пулов, связи overridden powers сохраняются.
Первые пять уровней сохраняют идентичности для штатного multiclass. При выключенном режиме
определения и новые обработчики Wizard не меняют обычную прогрессию.

Фактические уровни, история классов, общий предел20, HP и proficiency bonus не виртуализируются.
Новые вычисления ограничены Wizard. Замены глобального GetClassLevel нет.
Таблицы ячеек копируются; общие строки SharedSpellsContext и другие классы не переписываются.
Переключения правил/режима/Spell Points/подготовки/подклассов требуют перезапуска;
при изменении учитываемых настроек в сессии следующий level-up блокируется с объяснением в логе.

`SpellcastingProgressionPlan` отделяет накопительные значения от отдельных событий обучения,
проецирует строки и считает бюджеты без типов игры. Это основа для будущих классов,
но активирован только Wizard. Самостоятельным UMM-модом модуль пока не является: он зависит от
UB, его поздней инициализации, custom subfeatures, multiclass UI и изменённых методов.
Отдельный UMM-патч потребует проверки версии UB и порядка загрузки, а также выделения этих точек интеграции.

## Прогрессия колдовства

| Фактический Wizard | Охваченные исходные уровни | Эффективный | Макс. круг | Spell Points | Базовых новых заклинаний |
|---|---|---|---|---|---|
| 1 | 1 | 1 | 1 | 4 | 6 |
| 2 | 2 | 2 | 1 | 6 | 2 |
| 3 | 3 | 3 | 2 | 14 | 2 |
| 4 | 4 | 4 | 2 | 17 | 2 |
| 5 | 5 | 5 | 3 | 27 | 2 |
| 6 | 6–7 | 7 | 4 | 38 | 4 |
| 7 | 8–9 | 9 | 5 | 57 | 4 |
| 8 | 10–11 | 11 | 6 | 73 | 4 |
| 9 | 12–13 | 13 | 7 | 83 | 4 |
| 10 | 14–15 | 15 | 8 | 94 | 4 |
| 11 | 16–17 | 17 | 9 | 107 | 4 |
| 12 | 18–20 | 20 | 9 | 133 | 6 |

Обычные ячейки на Wizard12: `4/3/3/3/3/2/2/1/1` по кругам1–9.
При Spell Points проецируется существующая альтернативная таблица UB: по одной ограничивающей
ячейке на доступный круг. Расход очков, ограничения6–9, восстановление и запрет бесплатного
повторного использования высоких кругов остаются существующими механизмами UB.
133 — собственный ресурс Wizard12, а Wizard8/Fighter12 должен иметь73, а не133 от общего уровня20.
Новый индекс Spell Points Wizard ограничен диапазоном0–20. Общий shared caster level не изменён.

Подготовка: эффективный уровень Wizard плюс исходный модификатор способности и штатные affinity
бонусы. С `EnablePreparedSpellsTables2024` используется соответствующая строка оригинальной
таблицы Wizard; исходный20 даёт25 до affinity-бонусов. При INT20 обычный вариант также даёт25.
Proficiency-бонусы остаются фактическими, а не вычисляются от эффективного Wizard20.
Cantrips — накопительная таблица: 3,3,3,4,4,4,4,5,5,5,5,5; отдельные подклассовые cantrip-пулы сохраняются.

## Книга и отдельные события обучения

`ScribedSpells` в игре — награда за отдельный уровень, а не накопительный итог. Полная базовая
прогрессия сохраняет `6 + 19×2 = 44` выбора, без копирования свитков и иных дополнительных источников.
Первое исходное событие остаётся в штатном классовом пуле. Следующие события получают отдельные
теги `...Wizard12LearningOriginalN` и собственные ограничения круга. Например, на actual6
первые два выбора принадлежат original6 и допускают до третьего круга, следующие два — original7
и допускают четвёртый. На actual11 события16 и17 отдельно допускают восьмой и девятый круг.
На actual12 сохраняются три события18/19/20, по два выбора каждое.

`AdditionalScribedSpells` magic affinity начисляется на каждое представленное событие.
Spell Master с получением школы на original2 даёт всего63 базовых+бонусных выбора; со школой
на original3 —62. Другие независимые источники не дедуплицируются по равенству definitions.
Дополнительные пулы не сбрасывают RemainingPoints при повторной настройке; отмена/снятие
классовых features очищает и пулы, и их AcquiredSpells. Финализация направляет дополнительные
теги и origin-теги подклассовых cantrip-пулов в штатный GrantCantripsAndSpellsByTag, затем в существующую запись в spellbook.
Пулы учитывают известные/автоподготовленные заклинания, доступный DLC, расширения списка и
резервируют предыдущие выборы: исчерпание допустимого списка не создаёт отрицательный бюджет.
При исчерпанном списке число фактически доступных наград ограничивается доступными заклинаниями.

Копирование/переписывание, ритуалы, стоимость, время и правила владения книгами не переписаны:
они используют тот же Wizard repertoire с проекцией таблицы доступных кругов.
Вместимость не бесконечна и автоматически не увеличивается. С включённым ограничением страниц
новый выбор на Wizard2+ проверяет размещение всех текущих классовых событий по реальным книгам
в порядке, соответствующем native grant. Если страниц не хватает, выводится сообщение:
нужно отменить level-up и получить дополнительные книги. Никакие страницы или предметы
скрыто не создаются. Проверка создания Wizard1 пропускается, поскольку стартовое снаряжение
ещё не выдано. Переписывание вне level-up остаётся native и требует отдельного игрового теста.
У Wizard в исходной Spellbook-прогрессии нет обычных replacement-наград: новые не придуманы.

## Классовые способности и подклассы

Arcane Recovery использует эффективный Wizard в собственном бюджете восстановления; половина
уровня с округлением вверх даёт на actual12 бюджет10. Существующие частота, ограничения кругов,
условия отдыха и преобразование восстановления в Spell Points сохранены.
Spell Mastery18→actual12, ASI19→actual12, Signature Spells20→actual12 сохраняют исходный порядок.
Проверки доступности rest activities учитывают эффективный Wizard; выбор кругов/стоимость/
relearn-опция/восстановление signature uses остаются исходными.

Динамически выбираются зарегистрированные subclass choices и определения, отфильтрованные по
Hidden; список отражается в UB log. Проверены снимки четырёх официальных и восьми UB-подклассов.
Это проверка инвентаря/преобразования и метаданных, а не запуск их боевых эффектов.

| Подкласс | Что адаптировано или сохранено | Что проверять в игре |
|---|---|---|
| TraditionCourtMage | все unlocks, ownership powers, class-rank effects | Spell Shield, улучшения и временные HP |
| TraditionGreenmage | unlocks, атаки/поглощение, расширения магии | Entangling Shot, Magical Arrow и защиты |
| TraditionLoremaster | unlocks и magic affinities | дополнительные знания, подготовка и переписывание |
| TraditionShockArcanist | unlocks, Wizard rank в additional damage и powers | Arcane Fury, Shock/Greater Shock, ограничения реакций |
| WizardAbjuration | ward pool от эффективного Wizard; порог original10→actual8 | projected ward, counterspell, refresh ward |
| WizardArcaneFighter | исходные unlocks и power ownership | Extra Attack, защитные эффекты и стоимость действий |
| WizardBladeDancer | исходные unlocks, affinity/power связи | Bladesong, Extra Attack, концентрация и реакция |
| WizardDeadMaster | клонированные auto-prepared группы; SourceClassLevel призывов | списки нежити, HP/атаки слуг, концентрация |
| WizardEvocation | исходные unlocks, кастомные callbacks, rank dispatch | Sculpt/Potent/Empowered/Overchannel и стоимость |
| WizardGraviturgist | unlocks и class-bound powers | gravity effects, DC, длительность, перемещение и ресурсы |
| WizardSpellMaster | каждый бонусный scribing event, отдельные cantrips, affinities | 62/63 выбора, подготовка, скидка scribing и Bonus Recovery |
| WizardWarMagic | прямые class checks и numeric damage от effective Wizard | Arcane Deflection, Power Surge, Deflecting Shroud и отдых |

Auto-prepared группы Wizard копируются с преобразованными ClassLevel, оригинальные группы
других классов не изменяются. Не зависящие от класса DC от INT и фактический PB сохранены.
Пять базовых ASI исходных4/8/12/16/19 переходят на actual4/7/9/11/12. Общий механизм
повторных feat/ASI пулов и выборов остаётся прежним, включая пользовательские дополнительные награды.

## D&D 2024 и текущие настройки

`EnableWizardToLearnSchoolAtLevel3` сохраняет школу на2 или3; `EnableWizardToLearnScholarAtLevel2`
сохраняет отдельный Scholar-пул; `EnableWizardMemorizeSpell2024` по-прежнему управляет Memorize
и его условиями. Компрессия выполняется после этих изменений, не выдаёт обе версии школы/Scholar.
Перечисленные ранее пользовательские параметры Fighter не определяют значений этих трёх Wizard
флагов. В checkout и доступных attachments Settings.xml не найден: фактические значения Wizard не предполагаются,
а при запуске явно записываются в лог. Для проверки требуется прогнать обе ветви настроек.

## Границы мультикласса и незавершённые механики

Wizard13 запрещён. Wizard12/Fighter8 и Wizard12/Rogue8 допускаются с немагическим подклассом
и исходными требованиями входа/выхода. Ограничения числа классов и общего уровня не менялись.
При INT<13 выход из Wizard может быть запрещён штатным требованием, даже если другие
характеристики подходят. При отсутствии допустимого класса повышение блокируется с сообщением;
модуль НЕ снимает требования и НЕ поднимает Wizard выше12.

Wizard+Cleric/Sorcerer/Warlock/Inventor/другие casters пока не поддержаны. Второй кастующий
класс отфильтрован/заблокирован и при входе в Wizard из уже кастующего класса. Кастующие
подклассы других классов исключаются из subclass UI и защищены guard при назначении.
Общая интеграция Spell Points нескольких классов — **PENDING**. Примеры Wizard12/Cleric8,
Wizard12/Sorcerer8, Inventor12/Wizard8 пока недоступны в этом режиме.

Дополнительные НЕрасовые repertoires от feats/fighting styles также не поддерживаются:
существующий UB определяет их как multicaster. Полный фильтр таких feat-выборов ещё не реализован.
Не выбирать их для тестового героя: после получения другого repertoire следующий level-up
будет остановлен, но это не делает смешанную магию пригодной для игры. Расовая магия исключена
из счётчика multicaster и требует проверки отдельно. Изменённые другими модами providers,
нестандартные кастующие подклассы вне регистрации SharedSpellsContext не сертифицированы.

Интерфейс нескольких последовательных spell pools, Back/Cancel/повторное назначение, сочетание
с feats и исчерпанными списками нуждаются в настоящем игровом прогоне. Компиляция и подсчёт
событий не подтверждают прохождение всех UI-стадий. Сторонние плагины и переписывание definitions
после Wizard LateLoad не входят в проверенный контракт.

## Respec и сохранения — конкретные риски

Целевой EnableRespecAction=true не менялся. Аудит `ToolsContext.FunctorRespec` показал, что
существующий UB создаёт нового героя, переносит часть условий и заменяет старого.
`DropItem` специально обрабатывает spellbook: создаёт свитки только для заклинаний, которым
нашлось соответствующее ItemDefinition, а не переносит книгу целиком. Для новых high-level
заклинаний без свитка возможна потеря; копии приготовленных заклинаний, mastery/signature tags
и старые условия также нельзя считать проверенно совместимыми.

На этом этапе НЕ реализована отдельная без потерь миграция старых книг/их страниц и
Known/Prepared/ExtraSpells при respec. Условия старого героя могут пережить смену подкласса;
полное удаление устаревших Wizard-specific состояний не подтверждено. Автоматическое
восстановление уже повреждённого сохранения и переход между режимами не реализованы.
Именно поэтому итог этапа PARTIALLY READY, а не завершённая полная поддержка Wizard.
Проверять respec только на отдельной копии тестового сохранения с перечнем книг и заклинаний.

## Проверки и сборка

После реализации: 441 проверка, включая предыдущие307; Release Workflow — 0 ошибок,
0 предупреждений. `git diff --check` проходит. Выходная DLL существует и не пуста,
локальные binaries не добавляются в Git.

Проверки выполняют production planner/общий transformer с минимальными managed doubles;
читают фактические compiled slot/Spell Points/prepared таблицы через Mono.Cecil;
проверяют ссылки Harmony и имена аргументов по предоставленной Assembly-CSharp.
Покрыты13 inventory fixtures (Wizard+4 official+8 UB), все события обучения,44 и63 выбора,
кантрипы, таблицы/границы/круги, mastery/signature порядок, ASI, варианты школы/Scholar,
одноразовый latch/disabled mode, размещение по книгам и регрессии четырёх предыдущих классов.
Проверки Harmony/порядка bootstrap/ресурсов/subclass callbacks — метаданные, а НЕ выполнение patches.
Число проверок не равно числу игровых сценариев или полностью проверенных механик.

```bash
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.setup-solasta/dotnet-home
export NUGET_PACKAGES=/workspace/.setup-solasta/nuget DOTNET_CLI_TELEMETRY_OPTOUT=1
export SolastaInstallDir=/workspace/SolastaUnfinishedBusiness/SolastaFiles
cd /workspace/SolastaUnfinishedBusiness
dotnet --version
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
git diff --check
```

## Ручной план для нового героя

1. Сделать отдельный игровой профиль/копию тестового сохранения. Зафиксировать Settings и
   включить только нужные прототипы; основной Wizard-тест начать без других классов.
   Выбрать INT≥13 и STR либо DEX≥13 для дальнейшего Fighter/Rogue, иметь свободные книги.
   Не брать feats/fighting styles, создающие второй НЕрасовый repertoire.
2. Включить переключатель Wizard, перезапустить игру. Проверить лог активации, список
   зарегистрированных видимых подклассов и значения трёх Wizard2024 флагов.
3. Создать Wizard1: actual1, обычные HP/PB, три cantrips, шесть книжных выборов,4 Spell Points.
   Проверить отсутствие дублирования стартового feat, привычные навыки/снаряжение.
4. Повышать по одному уровню. На каждом сверять строку таблицы выше, книгу/новые выборы,
   cantrips, prepared limit и самостоятельные ASI/feat-этапы. На actual6 выполнить по два выбора
   original6≤круг3 и original7≤круг4; нельзя выбирать четыре заклинания круга4.
5. На actual6/8/11/12: выбрать часть заклинаний, Back, вернуться; затем полностью Cancel и
   повторить. Счётчики не пополняются, выбранные в отменённой попытке заклинания не остаются
   в книге; разные события/ASI/feats не исчезают. На actual12 должны быть original18/19/20.
6. Wizard11: девятый круг открыт только во втором событии (original17),107 points.
   Wizard12:133 points; итог44 базовых новых spellbook spells без копирования/других наград.
   При включённом Spell Master и школе на2 —63, на3 —62, плюс отдельные cantrips.
7. Проверить preparation до/после long rest и save/load, INT/affinity бонусы, ритуал из книги
   без подготовки, переписывание свитков вплоть до девятого круга. При ограничении страниц
   попытаться заполнить почти полные/раздельные книги: недостающий выбор должен быть запрещён
   с сообщением; отменить, приобрести пустую книгу, повторить без потери прошлых записей.
8. На Wizard12 проверить Spell Mastery: допустимые круги1/2 и исходные rest-условия.
   Проверить Signature Spells: разрешённые заклинания круга3, расход/восстановление собственных
   uses, опцию повторного обучения. Повторить long rest дважды, убедиться в отсутствии дублей.
9. Потратить Spell Points на разные круги. При достаточных оставшихся points повторное
   заклинание использованного круга6–9 должно оставаться ограничено исходными правилами UB.
   Arcane Recovery: правильный бюджет, исходные ограничения и восстановление после rest;
   отдельный прогон с Spell Points OFF после полного перезапуска и на другом новом герое.
10. Для каждой строки подклассовой таблицы создать отдельного героя и проверить перечисленные
    силы на фактических уровнях6/8/10/12, замену старших powers, отсутствие лишнего Extra Attack,
    Ward/Power Surge/rest reset, DC/урон, реакции, концентрацию и расход действий. Скрытые/выключенные
    подклассы не должны становиться доступными после включения прототипа.
11. Для Wizard2024 прогнать школу2/3, Scholar OFF/ON, Memorize OFF/ON, prepared tables OFF/ON
    на отдельных новых героях после перезапуска. Сохраняются ровно разрешённые варианты/выборы.
12. Wizard12→Fighter1→Fighter8 и Wizard12→Rogue1→Rogue8: Wizard остаётся12, общий уровень20,
    PB/HP/история фактические, Spell Points133. В обратном порядке Wizard8/Fighter12: points73.
    Wizard13 недоступен; Fighter/Rogue выбрать немагический подкласс. Проверить лимит3 классов.
13. Попробовать Wizard+Cleric/Sorcerer/Inventor и кастующий Fighter/Rogue-подкласс:
    недопустимый выбор должен быть недоступен/отклонён. При INT<13 или трёх capped-классах без
    допустимого выхода получить понятный отказ, а не пустой level-up; требования не обходятся.
14. Сохранить/загрузить чистого Wizard12 и Wizard12/Fighter8 в неизменном режиме. Сверить списки
    книг, страницы, learned/prepared/auto-prepared, mastery/signature selections и ресурсы.
15. На копии теста записать все книги и их spell IDs, переписать отдельные заклинания со свитков,
    пройти respec Wizard→Wizard, сменить подкласс и Wizard→немагический класс→Wizard.
    Проверить свитки на земле, невосстановленные spells без scroll definitions, отсутствие старых
    условий/сил/бесплатных mastery эффектов, повторные награды, ресурсы и save/load после respec.
    Любая потеря/дубль — дефект; respec не разрешается считать проверенным по успешной пересборке.
16. Выключить Wizard-переключатель, перезапустить и создать НОВОГО обычного Wizard:
    стандартная прогрессия до20, оригинальные круги/ресурсы/награды. Сжатый save не загружать.
    Отдельно повторить ключевые регрессии Fighter/Barbarian/Monk/Rogue.

## Изменённые файлы

Новые production-модули:

- `SolastaUnfinishedBusiness/Models/WizardProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/WizardProgressionRules.cs`
- `SolastaUnfinishedBusiness/Models/WizardSpellcastingContext.cs`
- `SolastaUnfinishedBusiness/Models/SpellcastingProgressionPlan.cs`

Точки интеграции:

- `SolastaUnfinishedBusiness/Settings.cs`
- `SolastaUnfinishedBusiness/Displays/ClassesDisplay.cs`
- `SolastaUnfinishedBusiness/Models/BootContext.cs`
- `SolastaUnfinishedBusiness/Models/2024WizardContext.cs`
- `SolastaUnfinishedBusiness/Models/Level20Context.cs`
- `SolastaUnfinishedBusiness/Models/SpellPointsContext.cs`
- `SolastaUnfinishedBusiness/Api/Helpers/MulticlassInOutRulesHelper.cs`
- `SolastaUnfinishedBusiness/Behaviors/ModifyPowerPoolAmount.cs`
- `SolastaUnfinishedBusiness/Behaviors/Specific/_GLBM.cs`
- `SolastaUnfinishedBusiness/Behaviors/UpgradeEffectDamageBonusBasedOnClassLevel.cs`
- `SolastaUnfinishedBusiness/Definitions/InvocationPoolTypeCustom.cs`
- `SolastaUnfinishedBusiness/CustomUI/MulticlassGameUi.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetActorPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterHeroPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetImplementationManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Subclasses/WizardAbjuration.cs`
- `SolastaUnfinishedBusiness/Subclasses/WizardWarMagic.cs`

Проверки/отчёт:

- `Tests/FighterProgression/WizardTests.cs` (новый)
- `Tests/FighterProgression/FighterProgression.Tests.csproj`
- `Tests/FighterProgression/Program.cs`
- `Tests/FighterProgression/RogueTests.cs` (метаданные новой цепочки Wizard→Rogue scaling)
- `Documentation/WizardProgressionPrototype.md` (этот файл)

Новых классов сверх Wizard нет. Коммит включает только исходники, тесты и этот отчёт;
DLL, game assemblies, Settings.xml и build artifacts исключены. Публикации пакета/release нет.
