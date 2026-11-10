# Stage 5 — Sorcerer 20→12

**PARTIALLY READY.** Реализован прототип прогрессии Sorcerer и его колдовства. Сборка и
автоматические проверки проходят; Unity/Solasta здесь не запускались. Смешанное колдовство
остаётся отдельной задачей, respec/save compatibility не подтверждены. Начинать только с
нового тестового персонажа; основные сохранения не использовать.

## До изменений

Использован существующий checkout `/workspace/SolastaUnfinishedBusiness`, ветка
`feature/progression-20-to-12`, исходный HEAD `604a2197691afb3176327aa3a336f8f89f6222bc`.
Fighter, Barbarian, Monk, Rogue и Wizard присутствовали в пяти предыдущих коммитах.
Отслеживаемые файлы были чистыми; `SolastaFiles/Mods/` содержал неотслеживаемые результаты сборки.
До редактирования прошли441 проверка и Release Workflow:0 ошибок/0 предупреждений.
.NET SDK9.0.320 уже подготовлен, переустановка зависимостей не потребовалась.
Worktree не создавалась; release не изменялась.

## Включение и архитектура

Новый `EnableSorcererCompressedProgressionPrototype` имеет значение false по умолчанию.
В Classes включить `Sorcerer 20 to 12 prototype (NEW HEROES; restart; SINGLE CASTER ONLY)`,
полностью перезапустить игру и создать нового героя. Нужны уже включённые EnableLevel20 и
EnableMulticlass; автоматически эти параметры не включаются. Остальные настройки, включая
MaxAllowedClasses=3, требования характеристик, Spell Points, дополнительные feats и целевой
EnableRespecAction=true, модуль не меняет. Settings.xml не создавался/не перезаписывался.

Отключение: убрать переключатель и перезапустить игру. Это восстанавливает обычные definitions
при новой инициализации, но не конвертирует уже созданного сжатого Sorcerer обратно.
Не загружать/не пересохранять одного героя попеременно в разных режимах. Старые Sorcerer выше12
не мигрируются: выводится ошибка и запрещается дальнейшее повышение. Старые герои до12 тоже
не являются поддержанной миграцией; автоматической конверсии сохранений нет.

Использованы прежние CompressedProgressionDefinitions, FighterProgressionPlan, session latch
и SpellcastingProgressionPlan. Новый KnownSpellsProgressionPlan отделяет накопительное число
известных заклинаний от отдельных необязательных замен и порядка событий. Книжные механики
Wizard не копируются: Sorcerer не записывает заклинания в spellbook и не получает scribing.
Новый модуль запускается после окончательных UB/2024 definitions, Spell Points и Wizard.
Повторный запуск инициализации не преобразует уже сжатые определения второй раз.

Фактические class/character levels, история, базовые HP, proficiency bonus и урон cantrips от
общего уровня не меняются. Числовые эффекты собственных классовых способностей используют
эффективный Sorcerer, а фиксированные/PB/ability-based ресурсы сохраняют свои исходные правила.
Глобальной замены GetClassLevel нет. Sorcerer13 запрещён; предел общего уровня20 и существующее
ограничение числа классов сохраняются. Другие классы автоматически не включаются в свои прототипы.

При выключенном Sorcerer-модуле новые обработчики пропускают обычную логику. Wizard сохранён;
в его rank dispatcher добавлена только отдельная ветка Sorcerer, а его эффективный уровень,
учебные пулы, spellbooks и ограничения не переписаны. Все прежние441 проверки проходят.

Изменения режима/правил Sorcerer/Spell Points/набора подклассов требуют перезапуска.
Учитываемые настройки фиксируются при запуске; изменение в сессии блокирует следующий level-up.
Hot-switch используемых таблиц во время игры не является поддержанным сценарием.

## Таблица прогрессии

Числа SP ниже — базовые Sorcery Points без дополнительных feat/item/subclass модификаторов.
Spell Points — другая, отдельная шкала ресурсов.

| Actual Sorcerer | Исходные события | Effective | Макс. круг | Известных базовых spells | Новых базовых choices | Возможностей замены | Sorcery Points | Spell Points |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 1 | 2 | 2 | 0 | 0 | 4 |
| 2 | 2 | 2 | 1 | 3 | 1 | 1 | 2 | 6 |
| 3 | 3 | 3 | 2 | 4 | 1 | 1 | 3 | 14 |
| 4 | 4 | 4 | 2 | 5 | 1 | 1 | 4 | 17 |
| 5 | 5 | 5 | 3 | 6 | 1 | 1 | 5 | 27 |
| 6 | 6–7 | 7 | 4 | 8 | 2 | 2 | 7 | 38 |
| 7 | 8–9 | 9 | 5 | 10 | 2 | 2 | 9 | 57 |
| 8 | 10–11 | 11 | 6 | 12 | 2 | 2 | 11 | 73 |
| 9 | 12–13 | 13 | 7 | 13 | 1 | 2 | 13 | 83 |
| 10 | 14–15 | 15 | 8 | 14 | 1 | 2 | 15 | 94 |
| 11 | 16–17 | 17 | 9 | 15 | 1 | 2 | 17 | 107 |
| 12 | 18–20 | 20 | 9 | 15 | 0 | 3 | 20 | 133 |

Обычные ячейки Sorcerer12: `4/3/3/3/3/2/2/1/1` по кругам1–9. При включённых Spell Points
берётся существующая альтернативная таблица UB с лимитирующей ячейкой каждого доступного
круга. Расход очков и ограничения шестого–девятого кругов остаются механизмами UB.
Исходные таблицы копируются: строки общих таблиц и других классов не изменяются.

Число base cantrips на actual1–12: `4,4,4,5,5,5,5,6,6,6,6,6`.
Найден исходный дефект: legacy KnownCantrips содержит6 на original16 и5 на original17–20.
Только в сжатой проекции сохраняется достигнутый максимум6. Исходный normal Sorcerer не
редактируется. Повторная настройка учебных пулов пересчитывает остаток с учётом выбранных
cantrips, а не накапливает штатное повторное добавление MaxPoints.

## Изучение, замены и интерфейс

Оригинальная UB-таблица KnownSpells через20 даёт15 базовых известных заклинаний. Это не44
книжных выбора Wizard. Сохраняются20 событий повышения,15 базовых приобретений и19 отдельных
возможностей замены (original2–20). Если использовать все замены, получится до34 операций
изучения; итоговая базовая известная коллекция всё равно15. Бонусные/автоподготовленные spells
подклассов сохраняются отдельно и не удаляются ради достижения числа15.

Первое событие остаётся в native class tag. Следующие получают независимые теги вида
`...Sorcerer12Original07`, собственный SpellUnlearn-пул и Spell-пул. Пустой Spell-пул существует
как placeholder: выбор замены добавляет в него соответствующую возможность изучения.
Неиспользованная необязательная замена не выдаёт бесплатного нового заклинания.

Пары идут в порядке `unlearn6 → learn6 → unlearn7 → learn7`, а не все unlearn перед всеми learn.
Меняются только роли/порядок шагов Sorcerer; cantrips и независимые источники сохраняются.
Для каждого шага считается виртуальный репертуар после предыдущих событий: original7 может
заменить заклинание, выбранное на original6; позднее можно снова изучить ранее забытое
заклинание. Нельзя забывать уже забытое или неизвестное на данном шаге заклинание либо дважды
изучать уже известное. Автоподготовленные бонусы не превращаются в заменяемые базовые spells.

Ограничения круга относятся к исходному событию: на actual6 original6 допускает круг3,
original7 — круг4; на actual11 original16 допускает8, original17 —9. На actual12 original18,
19 и20 сохраняют по одной необязательной замене, даже когда число известных spells не растёт.

При изменении раннего выбора зависимые поздние выборы очищаются и их бюджеты восстанавливаются
по данным текущего level-up. Повторная настройка не пополняет уже потраченные выборы. Clear/
удаление features очищает AcquiredSpells, UnlearnedSpells и дополнительные cantrip choices.
Перед выходом из панели проверяются завершённость обязательных выборов, согласованность
последовательности и отсутствие второго кастующего repertoire. Финализация использует
native lose-then-grant для каждого события в исходном порядке, включая оба дополнительных
события на actual12. Название обычного class tag на уровне12 не ошибочно принимается за
уже обработанный прототипный тег.

Это реализация интерфейсных адаптеров, а не подтверждение работы UI в Unity. Back/Reset/Auto/
Skip/Cancel, события с нулём новых spells, несколько наград и редкие исчерпанные списки
обязательно проверить в игре. Для нестандартных providers и сторонних модов гарантии нет.

## Раздельные ресурсы и конверсия

Sorcery Points остаются native атрибутом SorceryPoints и счётчиком usedSorceryPoints.
Их исходный множитель class level теперь получает effective Sorcerer; добавления от других
разрешённых источников остаются исходными. На actual12 базовый максимум20, не133.

Spell Points остаются PowerSpellPoints/remainingUses. Собственный caster level Sorcerer
используется вместо total character level; индекс ограничен0–20. Sorcerer12/Fighter8 имеет
базовые133 Spell Points и20 Sorcery Points; Sorcerer8/Fighter12 —73 и11 соответственно.
В SharedSpellsContext добавлен узкий override только для совместимого единственного Sorcerer:
он нужен меню реакций/уровней заклинаний, которые используют этот helper даже без multicaster.
Второй кастер отключает этот override; смешанные effective levels не суммируются.

Flexible Casting использует native SpellSlotCreationCost и SpellCostByLevel. Создаваемые
ячейки ограничены исходным словарём1–5, проверяется наличие Sorcery Points. Для обратной
конверсии проверяются собственный Sorcerer repertoire, доступная ячейка и достаточные Spell
Points при альтернативном режиме. Исходное превращение временных дополнительных ячеек в
Spell Points не переписано и не выдаётся второй обычный набор ячеек поверх альтернативного.
Стоимость цикла по фактическим таблицам не даёт прибыли Sorcery Points; guards не разрешают
программное превышение диапазона/остатка. Временные прибавки сверх базового ресурса остаются
семантикой UB; модуль не объединяет две шкалы и не начисляет обе по одному событию.

Restoration с половиной класса получает effective Sorcerer (ceil/floor сохранены).
Legacy capstone original20→actual12 восстанавливает исходные4 Sorcery Points при соответствующей
rest activity. 2024 Restoration на original5 остаётся5, а half-down бюджет на actual12 равен10;
исходная доступность/LongRest-лимит клонированного power не заменяется бесконечным восстановлением.
Long rest, расход/восстановление и возможные временные условия должны быть проверены в игре.

## Metamagic, 2024, feats

Сохраняются2 начальных Metamagic choices на original3 (или2 с опцией2024), затем по1 на
original10→actual8 и original17→actual11: всего4 базовых варианта. Оба повторных Additional
Metamagic definition получают самостоятельные origins. Стоимости и runtime ReplaceMetamagicOption
(например, у Psion), bound features и ограничения исходных опций не переписаны. В native
MetamagicOptionDefinition нет отдельного required class level; доступ определяется выдачей
пулов/опций и существующими validators. Новое общее retraining-правило метамагии не придумано.
Проверять замену опций в рамках существующих UB возможностей/respec.

Поддержаны исходные переключения шести флагов:

- EnableSorcererArcaneApotheosis2024: выбранный capstone original20→actual12, старый исключается.
- EnableSorcererMetamagic2024: начальные выборы на2 вместо3.
- EnableSorcererOrigin2024: origin на3 вместо1; соответствующие grants на2 тоже сдвигаются штатно.
- EnableSorcererInnateSorcery2024: Innate Sorcery на1, Sorcery Incarnate original7→actual6.
- EnableSorcererSorcerousRestoration2024: исходный power на5 и его effective half-level recovery.
- EnableSorcererDraconicBloodlineAC2024: исходный вариант AC, без дополнительного нового бонуса.

Прямые проверки7/20 адаптированы; условия Apotheosis (Innate Sorcery и turn tracking) и
внутренняя схема возврата стоимости сохранены. Если Apotheosis включена без Innate Sorcery,
модуль не включает последнюю автоматически: исходное условие Apotheosis по-прежнему обязательно.

В доступном checkout/attachments Settings.xml не найден, а значения этих шести флагов в
переданной конфигурации не заданы. Их фактическое включение не предполагается; при запуске
модуль пишет все Sorcerer2024 значения в лог. Проверки обеих ветвей приведены ниже.

Пять ASI original4/8/12/16/19 переходят на actual4/7/9/11/12. При целевых ASI+feat и обеих
extra-feat опциях сохраняются10 классовых feat points и10 ability points, отдельно от первого
персонажного feat и расовых/других источников. Повторные definitions не дедуплицируются по имени.

## Подклассы

Извлекаются фактические subclass choices и включённые регистрации UB; Hidden отфильтрованы.
Отключённые подклассы не включаются модулем. Для теста конкретного UB-подкласса включить его
обычным способом ДО перезапуска, проверить список обнаруженных подклассов в UB log.
Проверены11 blueprint inventories: класс+4 official+6 UB, а также compiled high-level loaders.

| Подкласс | Сохранённая/адаптированная область | Обязательная игровая проверка |
|---|---|---|
| Child Rift | bonus spells, Deflection, Offering6→6, Riftwalk14→10, Magic Mastery18→12 | ресурсные расходы; Riftwalk landing damage, владение и ранг вспомогательного power, preserve-slot rolls |
| Draconic Bloodline | ancestry, resilience/2024 AC, Elemental Affinity6→6, Wings14→10, Presence18→12 | damage, resistance, полёт и общий пул Awe/Fear |
| Haunted Soul | bonus spells, Spirit Visage, Vengeful Spirits6→6, Soul Drain14→10, Possession18→12 | реактивация, стоимость, концентрация/контроль и окончание эффектов |
| Mana Painter | bonus spells, Drain2/3, Tap6→6, Mana Balance14→10, Mana Overflow18→12 | замена Drain старшим power, Sorcery recovery и saving throws |
| Divine Heart | deity choice, Cleric extended list, Fount6→6, Portal14→10, Recovery18→12 | доступность Cleric spells, отдельные auto spells, исцеление и recovery costs |
| Field Manipulator | Displacement, Heightened affinity6→6, mental proficiency14→10, Forceful Step18→12 | движение/позиции, DC, duration, отдельные fixed/Sorcery uses и скидки |
| Psion | auto spells, PB-based fixed Psychokinesis и отдельный Sorcery-funded pool, Mind Sculpt6→6, Mind over Matter14→10, Supreme Will18→12 | сначала fixed/PB uses, затем1 SP; temporary HP от effective класса; метамагия/концентрация и прочие costs |
| Sorr-Akkath | auto spells, spell sneak damage по effective rank, Blood6→6, Darkness14→10, Touch18→12 | условия sneak damage, action economy, стоимость Touch4 Sorcery Points |
| SpellBlade | enchantment scaling, Mana Shield2/3, Extra Attack/cantrip replacement6→6, War Sorcerer14→10, Reflexes18→12 | PB uses и отдельные Sorcery costs, отсутствие лишнего Extra Attack и действий |
| Wild Magic | Surge/Tides, Bend Luck6→6, Controlled Chaos14→10, Bombardment18→12 | частота triggers, возвращение ресурсов, выбор двух surge результатов, slot surge до5 и однократный bombardment |

Группы AutoPreparedSpells Sorcerer клонируются и их ClassLevel сжимаются; original definitions
других классов не меняются. Shared power links, overridden powers и custom subfeatures сохраняются.
Для native Riftwalk repair исправлен class tag original14 на actual10; вспомогательный landing
power явно включён в Sorcerer power-rank ownership. DC от CHA и фактический PB остаются исходными.
Наличие этих adapters/definitions не подтверждает их боевой результат в Unity.

## Мультикласс и незавершённые области

Допускаются Sorcerer+немагический Fighter/Barbarian/Monk/Rogue с их собственными текущими
настройками. Sorcerer13 закрыт, затем нужен другой допустимый класс. Требования CHA≥13 для
входа/выхода Sorcerer и другие требования не снимаются. При CHA<13, трёх классах без
допустимого продолжения либо отсутствии альтернативы на cap12 возникает явный отказ, а не
подъём до13 или пустой шаг выбора класса. Класс с одними включёнными кастующими подклассами
не предлагается как безопасный выход.

Пока закрыты Sorcerer12/Warlock8, Sorcerer12/Wizard8, Sorcerer12/Paladin8,
Sorcerer8/Warlock6/Fighter6, а также прочие вторые caster classes/subclasses в любом порядке.
Wizard guards сохранены. Pact Magic не объединяется с другими ресурсами и остаётся отдельным
неподдержанным сочетанием. Полная cross-class Spell Points интеграция — PENDING.

Feat choices, явно создающие второй НЕрасовый CastSpell repertoire, отклоняются для Sorcerer.
При финальной проверке его spell panel проверяются также pending feats/fighting styles.
Нестандартные кастующие fighting styles, grants через custom callbacks и сторонние моды не
сертифицированы: не выбирать их для теста. Если второй repertoire всё же оказался у героя,
следующее повышение блокируется; это не делает текущий смешанный save совместимым.
Расовая магия исключена из счётчика multicaster и требует отдельного теста.

Полное исполнение UI, Auto/Reset/Back/Cancel, редкие комбинации дополнительных known-spell
providers/исчерпания списка, временные resource modifiers, все боевые subclass callbacks,
respec и save/load ещё не проверены в игре. Именно эти ограничения запрещают считать этап
полностью завершённым. Ни один другой новый класс в этой работе не реализован.

## Respec и сохранения

EnableRespecAction=true остаётся целевой пользовательской настройкой. Существующий
ToolsContext создаёт нового героя, копирует базовые ability scores/XP/часть условий и заменяет
старого. Этот модуль не копирует старые usedSorceryPoints/PowerSpellPoints или известные spells
в нового героя и не начисляет дополнительный ресурс поверх native grants. Пулы выдаются
обычными class features, метамагия и known spells выбираются заново при восстановлении уровней.
Это снижает риск двойного собственного grant, но НЕ является доказательством безопасности respec.

Возможны перенос старых effect conditions (Innate Sorcery/Apotheosis/подклассы), stale powers,
особенности восстановления XP/уровней, повторные выбора метамагии и неверный остаток ресурсов
при swap/save/load. Отдельной проверенной миграции этих состояний нет. Старые saves не читаются
и не изменяются инструментами разработки. Wizard respec/spellbook ограничения прошлого этапа
этим Sorcerer-этапом не исправлены.

## Автоматические проверки и сборка

После реализации —625 проверок:441 прежняя+184 новых. Release Workflow:0 ошибок,
0 предупреждений. DLL существует и не пуста, git diff --check проходит. Переустановки зависимостей нет.

Исполняются production KnownSpellsProgressionPlan/SpellcastingProgressionPlan и общий transformer
с managed doubles. По compiled UB/game metadata проверяются реальные slots/known/Spell Points/
conversion cost таблицы, resource identities, bootstrap/2024/ownership/caps и Harmony bindings.
Покрыты события/порядок/повторные награды,15 known+19 replacements, optional skip/replay,
замена ранее изученного, запрет неверных транзакций, порядок UI steps, коллизия class tag12,
одноразовая/disabled initialization, классовые/слотовые границы и регрессии предыдущих пяти классов.
Проверки metadata НЕ исполняют Harmony, реальные экраны, combat, rest, conversion или serialization.

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

## Ручной игровой план

1. Отдельный профиль/новый герой, CHA≥13 и STR либо DEX≥13 для последующего martial multiclass.
   Включить прототип, перезапустить, сверить log активации/список origins/шесть флагов2024.
   Не выбирать feats/styles со вторым repertoire. Сохранить baseline после создания.
2. Sorcerer1:2 известных spells,4 cantrips,4 Spell Points,0 базовых Sorcery Points; обычные HP/PB.
   Sorcerer2:2 Sorcery Points, исходный Flexible Casting; проверить origin1 или3, метамагию2 или3.
3. Повышаться по одному actual level и сверять каждую строку таблицы. Сверить пять ASI и
   дополнительные feats, независимые одновременные choices. На actual8 и11 добавить по1
   Metamagic option, итог4; не выбирать один вариант дважды между пулами.
4. Actual6: original6 unlearn/learn с кругом≤3, затем original7 unlearn/learn с кругом≤4.
   Во второй паре заменить spell из первой; в более поздней паре снова изучить ранее забытый.
   Нельзя дважды забыть одну и ту же текущую запись или повторно изучить уже известную.
5. На actual6/8/11/12 выполнить Back, Reset, Auto, Skip и полный Cancel после частичных выборов;
   повторить. Поздние зависимые choices сбрасываются, ресурсы выбора не пополняются бесплатно,
   cantrips и known spells не дублируются. Независимые feat/ASI choices сохраняют своё происхождение.
6. Actual11: original16 допускает круг8, original17 —9; known15,17 Sorcery Points,107 Spell Points.
   Проверить не только выбор/обычный cast девятого круга, но и реакцию/upcast menu до9.
7. Actual12: три самостоятельных пары replacements18/19/20,0 базовых новых spells, итог15 базовых
   известных и6 cantrips. Выполнить0,1,2,3 замены в разных копиях теста: итог известного количества
   не растёт и не падает, каждая замена фиксируется в native repertoire после завершения.
8. Проверить20 Sorcery Points отдельно от133 Spell Points. Тратить каждый ресурс отдельно,
   пробовать конверсию в обе стороны, граничные остатки и попытку повторения цикла. Ни один
   цикл не создаёт ресурс бесплатно. Высокие circles6–9 нельзя использовать без исходного
   ограничения повторно только потому, что остались Spell Points.
9. Short/long rest: legacy capstone4 SP на actual12;2024 half-down recovery10 при допустимом
   rest/power-use лимите. Проверить depleted/частично depleted/full resources и повторный отдых.
   Arcane Apotheosis: исходные Innate/turn условия, возврат metamagic cost, отсутствие повторного
   бесплатного trigger; Innate/Sorcery Incarnate доступность на actual6.
10. Отдельные новые герои после перезапуска: обе ветви шести2024 флагов, особенно origin1/3,
    metamagic2/3, legacy/apotheosis20→12, AC. Не должны одновременно появляться обе версии capstone.
11. По одному герою каждого из10 origins: выполнить конкретные проверки из таблицы подклассов
    на actual6/10/12; проверить auto/extended spells на4/5/6/7/8/9 кругах, fixed/PB/Sorcery pools,
    DC, условия, частоту реакций и override старых powers. Для Psion отдельно fixed→paid fallback.
12. SpellBlade+Fighter: Extra Attack не складывается лишний раз, cantrip replacement не добавляет
    лишних действий. Wild Magic: Controlled Chaos actual10 и Bombardment actual12 без дублей
    triggers. Child Rift: landing damage на actual10 до общего уровня14 и после save/load.
13. Sorcerer12→Fighter8 и наоборот Sorcerer8/Fighter12: actual total20, прежние HP/PB/history,
    базовые ресурсы20/133 и11/73 соответственно. Выбрать немагический subclass. Третий класс
    допускается только при исходных атрибутах/MaxAllowedClasses. Sorcerer13 недоступен.
14. Попробовать Warlock/Wizard/Paladin и caster subclass в обоих порядках; они должны быть
    недоступны/отклонены. При CHA<13 на cap12 проверить понятный отказ без снятия требования.
    Отдельно проверить профиль, где у Fighter включены только caster subclasses.
15. Сохранить и загрузить чистого Sorcerer12 и Sorcerer12/Fighter8 в неизменном режиме. Сверить
    known/bonus spells, meta options, conditions/powers и оба resource counters до/после rest.
16. Только на копии: respec Sorcerer→Sorcerer со сменой origin, Sorcerer→martial→Sorcerer,
    повторный respec и отмена. После каждого восстановить уровни, сверить known15+bonus,
    meta4+legitimate extras, оба ресурса, removal старых powers/conditions и save/load. Любой
    дубль/потеря — дефект, а не доказанная особенность совместимости.
17. Другой новый герой после перезапуска с Spell Points OFF: обычные слоты и конверсии.
    Затем выключить прототип, перезапустить и проверить НОВОГО обычного Sorcerer до20.
    Не открывать сжатый save в выключенном режиме. Прогнать прежние классовые регрессии.

## Изменённые файлы

Новые production-модули:

- `SolastaUnfinishedBusiness/Models/SorcererProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/SorcererProgressionRules.cs`
- `SolastaUnfinishedBusiness/Models/SorcererSpellcastingContext.cs`
- `SolastaUnfinishedBusiness/Models/KnownSpellsProgressionPlan.cs`

Интеграция:

- `SolastaUnfinishedBusiness/Settings.cs`
- `SolastaUnfinishedBusiness/Displays/ClassesDisplay.cs`
- `SolastaUnfinishedBusiness/Models/BootContext.cs`
- `SolastaUnfinishedBusiness/Models/2024SorcererContext.cs`
- `SolastaUnfinishedBusiness/Models/SharedSpellsContext.cs`
- `SolastaUnfinishedBusiness/Models/SpellPointsContext.cs`
- `SolastaUnfinishedBusiness/Models/WizardProgressionContext.cs`
- `SolastaUnfinishedBusiness/Api/Helpers/MulticlassInOutRulesHelper.cs`
- `SolastaUnfinishedBusiness/Behaviors/ModifyPowerPoolAmount.cs`
- `SolastaUnfinishedBusiness/CustomUI/MulticlassGameUi.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetActorPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterHeroPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetImplementationManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Subclasses/SorcerousPsion.cs`
- `SolastaUnfinishedBusiness/Subclasses/SorcerousSpellBlade.cs`
- `SolastaUnfinishedBusiness/Subclasses/SorcerousWildMagic.cs`

Проверки/отчёт:

- `Tests/FighterProgression/SorcererTests.cs` (новый)
- `Tests/FighterProgression/FighterProgression.Tests.csproj`
- `Tests/FighterProgression/Program.cs`
- `Documentation/SorcererProgressionPrototype.md` (этот файл)

Всего26 файлов. Коммит содержит только source/tests/documentation; proprietary/generated DLLs,
Settings.xml, bin/obj и временные исследования не включаются. Публикации mod package/GitHub Release
и merge в release нет. Следующий новый класс не реализовывался.
