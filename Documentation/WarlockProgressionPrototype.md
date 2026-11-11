# Warlock 20→12 — Stage 6.1

Статус: **PARTIALLY READY**. Основной дефект обычных инвокаций исправлен: безопасные цепочки
`A→B→C→D` и `A→B→A` проходят через событийный журнал, включая применение в исходном порядке.
Unity/игровая проверка не выполнена. Для цепочек с промежуточными выборочными наградами (Tome и аналогами),
неизвестными validators и EldritchSurge остаются явные ограничения/защитные блокировки, описанные ниже.

Stage 6.1 продолжает коммит Stage 6 `f15c4f4c7df6b3b74e728dd9530370e509492a7e`.
Перед исправлением заново подтверждены **882 проверки** и Release Workflow без ошибок/предупреждений.

Работа выполнена в существующем checkout `/workspace/SolastaUnfinishedBusiness`,
ветка `feature/progression-20-to-12`, база `1573832b51d7464c4dfeca54f8bb994595054aaa`.
Worktree не создавался. `release`, сохранения, игровые оригинальные сборки и `Settings.xml` не менялись.
Новые классы после Warlock не реализованы. Сборка создаёт только обычные локальные build-артефакты;
готовый мод-пакет или release не публиковался.

## Включение и выключение

В UB → Classes включить **Warlock 20 to 12 prototype (NEW HEROES; restart; SINGLE CASTER ONLY)**,
то есть `EnableWarlockCompressedProgressionPrototype = true`, затем полностью перезапустить игру.
По умолчанию новое поле `false`. Использовать только новых тестовых персонажей и отдельную тестовую кампанию.
Старые Warlock автоматически не мигрируются: определения меняются на сеанс целиком, поэтому загрузка старого
персонажа в новом режиме несовместима даже при отсутствии отдельного конвертера сохранений.

Требуются существующие `EnableLevel20 = true` и `EnableMulticlass = true`. При их отсутствии
прототип не активируется и пишет причину в лог. Сам эти настройки не включает.
`MaxAllowedClasses = 3`, требования характеристик, Spell Points, ASI/feat, стартовый feat и остальные
параметры пользователя сохраняются. Целевой `EnableRespecAction = true` не переключается программно.

Для выключения снять флажок и полностью перезапустить игру. Это восстанавливает обычные определения
Warlock при следующем старте, **но не конвертирует созданных в прототипе героев обратно**.
Не загружать их в обычном режиме. Изменение параметров прогрессии/2024/feat в текущем сеансе требует
перезапуска; проверка сигнатуры блокирует следующее повышение, если параметры изменились.
Нельзя продолжать играть после горячего переключения существующих 2024 опций UB: их собственные
обработчики могут сразу менять определения, а прототип не является механизмом горячей миграции.

В облачном checkout не найден пользовательский `Settings.xml`. Значения трёх Warlock2024 флагов
в реальной игре неизвестны. Они не выдуманы и не включены автоматически. При активации прототип
логирует фактические `EnableWarlock*2024` и список обнаруженных включённых покровителей.

## Прогрессия и ресурсы

| Фактический Warlock | Исходные события на повышении | Эффективный уровень | Pact: число × круг | Базовые известные заклинания | Базовые заговоры | Новый Arcanum |
|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 1 × 1 | 2 | 2 | — |
| 2 | 2 | 2 | 2 × 1 | 3 | 2 | — |
| 3 | 3 | 3 | 2 × 2 | 4 | 2 | — |
| 4 | 4 | 4 | 2 × 2 | 5 | 3 | — |
| 5 | 5 | 5 | 2 × 3 | 6 | 3 | — |
| 6 | 6, 7 | 7 | 2 × 4 | 8 | 3 | — |
| 7 | 8, 9 | 9 | 2 × 5 | 10 | 3 | — |
| 8 | 10, 11 | 11 | 3 × 5 | 11 | 4 | 6 |
| 9 | 12, 13 | 13 | 3 × 5 | 12 | 4 | 7 |
| 10 | 14, 15 | 15 | 3 × 5 | 13 | 4 | 8 |
| 11 | 16, 17 | 17 | 4 × 5 | 14 | 4 | 9 |
| 12 | 18, 19, 20 | 20 | 4 × 5 | 15 | 4 | — |

Таблица не включает дополнительные заговоры/заклинания от расы, Pact Tome, покровителя, feats
и дополнительную pact-ячейку Hive. Эти награды остаются самостоятельными.

`LateLoad` выполняется после Fixes, Level20, Spell Points, 2024 и предыдущих шести модулей.
Он читает итоговые определения, сохраняет порядок каждого исходного события, повторные награды
и ссылки переопределения powers. События выше 5 собираются в упорядоченные Union-контейнеры;
пулы выбора получают отдельные origin-теги. Истории классов, XP, HP и proficiency bonus
не заменяются эффективным уровнем. Глобального патча `GetClassLevel` нет.

Важна именно финальная таблица UB: диагностический исходный JSON Warlock содержит неправильные
строки 17–20 и пять заговоров; существующий `FixesContext` исправляет их на четыре pact-ячейки
5-го круга и четыре базовых заговора. Прототип читает уже исправленные таблицы и проверяет,
что они имеют pact-форму, а не форму обычного full caster. Строки и списки слотов копируются.
Индексы pact-таблиц ограничены 0–20; Arcanum-круг не превышает 9.

### Pact Magic, Mystic Arcanum, Spell Points

- Pact Magic использует прежний `CastSpellWarlock`, единственный круг ячеек и `ShortRest`.
  Изменены его таблицы на фактических уровнях и центральный Warlock helper в `SharedSpellsContext`.
  Имеющиеся расход, upcast, отображение и восстановление pact-ячеек продолжают использовать
  прежние пути `RulesetSpellRepertoirePatcher`. Дополнительная ячейка Hive учитывается прежним helper.
- Обычное обучение имеет 20 исходных событий, 15 базовых приобретений и 19 необязательных замен.
  На объединённом повышении шаги идут `удалить → изучить` отдельно для каждого исходного уровня.
  Более позднее событие может заменить заклинание, изученное на предыдущем событии этого повышения.
  Верхний круг обычных известных заклинаний всегда не выше 5; на раннем событии не разрешён
  круг следующего исходного события. Изменение раннего выбора сбрасывает зависимые поздние выборы.
- Arcanum 6/7/8/9 выдаётся отдельными исходными пулами на фактических 8/9/10/11.
  Сохраняются `ExtraSpellsTag = MysticArcanum`, точный круг каждого пула и отдельный
  `usedMysticArcanum`. Нативные `SpendMysticArcanum`, проверка доступности и восстановление
  не переписаны. В штатной системе это одно применение каждого выбранного круга до long rest;
  короткий отдых восстанавливает pact-ячейки, а не Arcanum. Это проверено по метаданным/IL,
  но ещё требует игрового прогона.
- Новый механизм переобучения Arcanum не придуман. Нативные пулы дают по одному выбору;
  изменение через отмену незавершённого повышения и существующий respec требует проверки в игре.
  Произвольная замена уже подтверждённого Arcanum не реализована.
- `UseAlternateSpellPointsSystem = true` не делает Warlock обычным full caster.
  Warlock не добавлен в `ClassCasterType`, таблицы Spell Points или обычную shared-caster
  подмену Wizard/Sorcerer. Его базовый обычный Spell Points budget остаётся 0, а не 133.
  Arcanum не превращён в свободные ячейки 6–9-го круга. Межклассовая конвертация пока блокируется.
  UI общих Spell Points всё ещё содержит исходные общие обработчики UB: их подписи/доступность
  при Warlock нужно проверить отдельно; изменение только расчёта не доказывает корректность UI.

### Инвокации и Pact Boons

Сохранены все исходные приобретения: **8 стандартных инвокаций по 2014**, **10 по 2024**,
без дополнительных feats. Каждый исходный уровень имеет свой шаг и бюджет. В стандартном новом
Warlock замены начинаются после первого получения инвокаций: 18 возможностей по 2014 (3–20),
19 по 2024 (2–20). На фактическом 12 остаются отдельные возможности исходных 18, 19, 20.
Число приобретений читается из финальных определений, а не заменяется константой.

Требования уровня проверяются по исходному событию: инвокацию с требованием 15 нельзя взять
на шаге 14 фактического уровня 10. Проверки known spell, RequiredPact и пользовательские validators
остаются. Вне окна выбора применяется эффективный Warlock-уровень.

В 2014 остаётся отдельный Pact Selection на уровне 3. В 2024 исходный UB убирает его и открывает
Pact Blade/Chain/Tome как инвокации; прототип не выдаёт обе схемы одновременно. Изменения
Otherworldly Leap и прочих prerequisite берутся из выбранного исходного варианта UB.

### Событийные транзакции инвокаций — Stage 6.1

Первопричина: native `FinalizeCharacter` последовательно обходит все списки `UnlearnedInvocations`,
затем все `LevelupTrainedInvocations`. Для `A→B→C→D` это даёт попытки удалить A/B/C до появления B/C,
а затем добавить B/C/D. В Stage 6 поэтому стоял запрет заменять только что приобретённую инвокацию.

Теперь `WarlockInvocationTransaction<T>` — production-код без зависимостей Unity — копирует исходный
инвентарь и каждый origin event: original level, постоянные awards, optional replacement budget,
упорядоченные удаления и приобретения. Он строит отдельные before/after snapshots каждого события.
Каждая замена сначала удаляет известную на этом шаге инвокацию, затем разрешает столько приобретений,
сколько дают постоянная награда и фактическое удаление. Skip не даёт бесплатного приобретения.
Раннее неполное событие блокирует последующее; неизвестные удаления, повторное знание и выход за бюджет
отклоняются. Позднее законное повторное приобретение разрешено. Это не сведение истории к итоговому set.

При обычном выборе и на границе Train/Unlearn применяется тот же валидатор. UI-запросы
`IsInvocationTrained`/`IsInvocationKnownOrTrained` получают виртуальный инвентарь текущего события
только в class-origin Warlock шагах. Поэтому B, приобретённая на18, доступна для замены на19;
A, удалённая раньше, снова доступна для приобретения позже. Feat и чужие custom UI идут прежним путём.

Prerequisites проверяются на **original**, а не на конечном effective level:

- RequiredLevel остаётся исходным числом; инвокация19 недоступна на шаге18 actual12.
- RequiredPact учитывает виртуальные Pact Blade/Chain/Tome. Старые invocation-origin active features
  не считаются действующим Pact, если его уже нет в виртуальном инвентаре. Legacy Pact берётся
  из подтверждённых class features.
- RequiredKnownSpell использует Warlock spell events до текущего original включительно, выбранные
  cantrips и подтверждённые независимые репертуары/extra spells. Будущий spell event не помогает
  ранней инвокации. Потерянные extra spell tags от удалённой инвокации не используются как prerequisite.
- Инспектированные UB validators Hex (Malediction/Bestow Curse/Sign of Ill Omen/SoulBlade) и
  Devouring Blade (Thirsting Blade) имеют точечные адаптеры по идентичности source delegate.
  Они не читают произвольные будущие pending selections, как исходные обработчики UB.
- Неизвестные validators не исполняются на вымышленном герое. Если для них требуется виртуальное,
  сжатое либо незавершённое spell state, выбор консервативно отклоняется. Зависимости между UI-стадиями
  доступны только после появления фактических pending spell choices; планировщик не угадывает будущий выбор.

После изменения/отмены раннего выбора `InvalidatedBy` определяет более поздние origin events.
Их временные learn/unlearn списки очищаются через native undo, бюджеты пересчитываются.
Undo удаления сначала очищает приобретения, оплаченные этим удалением, и зависимые поздние события.
Back, Reset и bulk undo используют тот же путь с защитой от рекурсии. Отмена всего повышения очищает
Warlock origin selections и их временные spell/cantrip choices, не записывая их в героя.

### Применение и откат

1. В начале существующего UB finalize prefix выполняется полная проверка бюджета, prerequisites,
   custom/feat конфликтов и снимок неизменяемого class journal. Она предшествует выдаче race/custom/feat
   наград этим prefix. Если проверка не пройдена, original и UB postfix не выполняются.
2. Scoped transpiler адаптирует ровно два native call site: `UnlearnInvocations` и `TrainInvocations`
   внутри `FinalizeCharacter`. При другой форме native IL финализация прототипа блокируется.
   Вне текущей Warlock-транзакции wrappers вызывают прежние native методы.
3. При первом принадлежащем журналу списке применяется **весь original-ordered journal**:
   remove18→train18→remove19→train19→remove20→train20. Последующие native обращения к тем же
   class-origin спискам не применяют его повторно. Feat и custom списки не поглощаются адаптером.
4. Native `TrainInvocations` только пополняет trained definitions; реальная сборка RulesetInvocation,
   powers и ресурсов остаётся штатной финальной работой игры. Промежуточные B/C поэтому не получают
   отдельные расходуемые ресурсы, если окончательно остаётся D. Повторные immediate level-up callbacks,
   pool rewards и выдачи feats/fighting styles защищены отдельным guard.
5. Перед применением снимаются копии `TrainedInvocations` и `ExtraSpellsByTag` репертуаров:
   native Unlearn может удалять extra spells из FeatureSet pools. У direct pool 2024 Tome добавлена
   явная очистка соответствующего Warlock extra tag. При исключении адаптера восстанавливаются снимки.
   Журнал нельзя применить дважды; неудавшийся rollback делает его непригодным к повторному применению.

**Граница гарантии:** откат охватывает поверхность мутации этого invocation adapter. Вся native
финализация персонажа (skills, feats, conditions, inventory, другие моды) не превращена в общую
транзакцию. При любом исключении финализации дальнейший retry блокируется; нужно отменить/загрузить
тестовый save, а не принимать или сохранять повреждённый результат. Валидационные ошибки проверяются
до мутаций; сценарий исключения внешнего native кода требует проверки в игре. Совместимость с другими
модами, патчащими FinalizeCharacter, не подтверждена.

### Оставшиеся ограничения

- **Tome/другие choice-granting или immediate-granting инвокации:** одно итоговое приобретение и обычное
  удаление старой инвокации сохраняют native путь выбора. Но новая инвокация, которая выдаёт отдельный
  pool, SpellCasting, feat/fighting style либо ICustomLevelUpLogic, не может быть промежуточным звеном
  с удалением/повторным приобретением на этом же actual level. Native transient pools не различают
  несколько incarnations одной награды. Вместо риска повторных spells/resources стоит явный guard.
  Удаление уже подтверждённой инвокации с immediate custom/feat/repertoire grants также блокируется:
  native Unlearn не откатывает такие изменения всего героя. Это не относится к обычной очистке Tome extra spells.
- **Eldritch Adept:** подтверждённая feat-инвокация входит в исходный инвентарь; новый feat-origin выбор
  сохраняет отдельный бюджет и native финализацию. Дубликат между class и feat origins отклоняется.
  Новый feat-origin выбор не используется как источник prerequisites сжатых class events:
  общая хронология feat/class в этой работе не реализована. Неоднозначная cross-origin замена
  не проводится автоматически. Одновременные дополнительные
  spell/cantrip grants и взаимодействия с feat UI требуют игрового теста. Если class и feat invocations
  используют один native transient pool, второй выбор отклоняется до выделения пула; ambiguous grants
  не объединяются и не перезаписываются. При undo очищаются только соответствующие spell/cantrip channels.
- **EldritchSurge/custom pools:** прежние independent origins и native UI сохранены. На приёмке каждый
  фактический class/subclass custom pool проверяется тем же production engine отдельно по PoolType,
  с исходными Learn/Unlearn бюджетами. Требования к общему уровню персонажа остаются фактическими;
  только Warlock class/subclass requirements используют исходное событие. Повторно удалить уже
  потерянную custom-инвокацию нельзя.
  Но цепочка, где custom-инвокация и приобретается, и удаляется в этом actual advancement, блокируется
  до UB finalize prefix: native custom UI и ICustomLevelUpLogic не имеют доказанного событийного отката.
  Полная custom-chain поддержка **не объявляется**; обычные и custom pools не объединены.
- Произвольные validators, несогласованные pending spell choices, respec и save/load не получили
  вымышленного подтверждения. Смешанное Sorcerer/Wizard + Warlock колдовство остаётся заблокировано.

### D&D 2024 и капстоуны

Поддерживаются включённый и выключенный варианты трёх исходных переключателей:

| Настройка | Поведение |
|---|---|
| `EnableWarlockMagicalCunningAndImprovedEldritchMaster2024` | Cunning на 2; проверка исходного20 на фактическом12; восстановление всех pact-ячеек вместо половины. Иначе legacy Eldritch Master на12. |
| `EnableWarlockToLearnPatronAtLevel3` | Выбор и начальные особенности патрона на1 либо3; корректировки ранних EldritchSurge замен берутся из UB. |
| `EnableWarlockInvocationProgression2024` | 8 либо10 приобретений; отдельный Pact Selection либо pacts как инвокации; исходные скрытые варианты сохраняются. |

Legacy Eldritch Master в UB клонирует Wizard Arcane Recovery. Только при активном прототипе
его эффект перехватывается и обнуляет использованные pact-ячейки текущего круга. Время Minute1,
расход power и LongRest recharge остаются штатными; Arcanum/Spell Points не восстанавливаются.
Ранний Magical Cunning использует прежнее округление половины и прежний максимум с модификаторами.
Исходный switch удаляет альтернативный капстоун до компрессии, поэтому обе версии не выдаются вместе.

## Покровители: явное покрытие

Обнаружение выполняется по реальным subclass choices и включённым UB-регистрациям. Ниже —
проверенный инвентарь, а не утверждение, что все 12 включены в неизвестном пользовательском файле.
Общие рубежи патронов: начальный1/3, исходный6→actual6, 10→8, 14→10.
Новых вымышленных событий 18/20 у патронов не добавлено.

| Покровитель | Проверенный код/определения и адаптация | Осталось проверить в игре |
|---|---|---|
| Fiend | Expanded list, Dark One's Blessing/Luck, Resilience, Hurl Through Hell; общие scoped ranks powers/conditions | temp HP, выбор сопротивления, стоимость/возврат Hurl |
| Hive | Pheromones, Magic Counter, Carapace, Antimagic Chitin; дополнительная pact-ячейка сохранена | реакция и дополнительная ячейка при расходе/отдыхе |
| Timekeeper | Curse of Time, Time Shift, Accelerate, Time Warp в исходном порядке | прерывания, длительности и экономика действий |
| Tree | Piercing Branch, Blessing, Explosive Growth, One With the Tree | условия, DC и восстановление powers |
| Archfey | Fey Presence, Misty Escape, Beguiling Defenses, Dark Delirium; исходные bundles/reactions | реакция телепортации, charm/fear, общий расход variants |
| Celestial | Healing Light: 1+effective level; Resistance temp HP: effective class rank; прочие Radiant Soul/Searing Vengeance сохранены | на12 базово21 кость Healing Light; стоимость выбранных костей, temp HP себе/союзникам, возвращение в бой |
| EldritchSurge | Все повторные custom-пулы; MaxPoints использует effective Warlock; специальная формула лучей сохраняет штраф за другие **реальные** уровни | Learn/Unlearn UI, pending choices, condition refresh, spend/recover, Blast Reload/Overload |
| Elementalist | Forms и Enhanced Forms/shared pools, Knowledge, Minor Elemental сохраняют acquisition и power links | общие uses, переключение формы, resistances/кантрип |
| MoonlitScion | Lunar Cloak/Midnight Blessing/Embrace/Guise; прямые class thresholds заменены scoped rank | пороги6/10 и расход/действие Guise; class dice advancement |
| Mountain | Barrier: 2×effective level+CHA; прочие Knowledge/Clinging Strength/Guardian/Wakes/Icebound Soul сохранены | на12 Barrier40+CHA, temp HP, clinging, immunities |
| RiftWalker | Blink, Rift Step/Strike, Portal заменяет Step наactual8, Cloak на10; сохраняется IPreventRemoveConcentrationOnPowerUse | bonus/reaction, teleport targeting, banishment возврат, концентрация и Cloak bond |
| SoulBlade | Hex/Master Hex override, SoulShield, Summon Pact Weapon; healing effective level+CHA, weapon dice thresholds10→actual8 и14→10 | CHA/PB правила оружия, крит/щит, совместимость Pact Blade, healing, отсутствие дубля Hex |

Дополнительно адаптирован Tomb of Frost: class-dependent temp HP использует effective Warlock
(на12 — 200); базовые HP персонажа от этого не растут. Обычный Eldritch Blast продолжает
масштабироваться от общего фактического уровня. Только специальный EldritchSurge использует
свою прежнюю class-based формулу с effective Warlock и остальными actual levels один раз.
Эффекты powers, source-class conditions и class-based pool modifiers получают scoped rank.
Расчёты, зависящие от CHA или PB, не подменены уровнем20.

## Multiclass, respec и сохранения

Разрешены Warlock + подходящие некастующие классы/подклассы. Warlock13 запрещён и в списке,
и на границах команд AssignClassLevel/AddClassLevel. Actual total остаётся максимум20;
число классов и In/Out характеристики определяет существующая пользовательская конфигурация.

| Комбинация | Stage 6 |
|---|---|
| Warlock12 / Fighter8 | Допускается с некастующим подклассом и выполненными требованиями |
| Warlock8 / Rogue12 | Допускается с некастующим подклассом и выполненными требованиями |
| Warlock12 / Sorcerer8 | Заблокирована: Pact + обычный caster/Spell Points deferred |
| Sorcerer12 / Warlock8 | Заблокирована; прежний Sorcerer guard сохранён |
| Sorcerer8 / Warlock6 / Fighter6 | Заблокирована по смешанному колдовству; лимит3 не отменяет guard |
| Wizard + Warlock | Заблокирована; прежний Wizard guard сохранён |

Также отсекаются feats/новые подклассы, создающие второй нерacial casting repertoire.
Расовые репертуары не считаются обычным вторым class caster. Нельзя обходить ограничения
загрузкой уже смешанного героя: продолжение повышения такого персонажа отклоняется.

**Неизбежный deadlock:** если на Warlock12 нельзя выйти по требованиям характеристик
(например, CHA ниже13) или все разрешённые альтернативы недоступны из-за лимита классов,
капов/колдовства, дальнейшее повышение блокируется с диагностикой. Требования не отключаются;
пустой class selection намеренно не открывается. Для нового тестового героя заранее выбрать
характеристики для Warlock и будущего некастующего класса.

Проверен `ToolsContext.FunctorRespec`: создаётся новый герой, переносится идентичность/часть данных
и effect conditions, затем заменяется исходный герой. Это не доказывает очистку pact/Arcanum,
покровительских powers/conditions и совместимость всех origin-тегов. Специальной миграции
сохранений или переписывания respec в этой работе нет. Полная безопасность respec не заявляется.
Особенно проверить перенос EldritchSurge support condition, familiars, pact weapon,
потреблённых ресурсов и изменение патрона. Обычный режим и прототип — разные тестовые сохранения.

Возможное выделение в отдельный UMM patch: логика изолирована в Warlock-модулях,
но использует внутренние builders/планировщики UB, SharedSpellsContext и его UI. Отдельный patch
должен зависеть от совместимой версии UB, запускаться после его окончательного bootstrap и иметь
доступ к internals/reflection или публичному API. Независимый от UB готовый мод не реализован.

## Проверки и сборка

Baseline Stage 6.1: **882 checks passed**, Release Workflow — **0 errors / 0 warnings**.
Текущий результат: **959 checks passed**, Release Workflow — **0 errors / 0 warnings**,
SDK **9.0.320**. Переустановка зависимостей не требовалась.
Набор вырос на **77 checks**: 62 проверки нового transaction runner и 15 дополнительных Harmony binding checks.

Весь прежний набор сохранён: Fighter, Barbarian, Monk, Rogue, Wizard, Sorcerer и Warlock.
Две прежние structural assertions актуализированы: старый запрет заменён проверкой transaction validator;
очистка теперь проверяет native undo через защищённый callback, а не только прямой вызов.
Остальные прежние проверки Pact Magic, Arcanum, spell learning, caps, 2024, patron/reward counts проходят.

Новые проверки исполняют **реальный production WarlockInvocationTransaction**, связанный в тестовый проект:
A→B→C→D, A→B→A, Skip, новый previous-event inventory, неизвестные удаления, дубликаты, original-level
prerequisites, pending Pact/known spell callbacks, invalidation/Undo/redo/Cancel, несколько наград,
2014/2024, independent feat/custom origin budgets, idempotence, validation-before-mutation,
injected application failure и rollback/poisoned journal. Отдельно Cecil проверяет compiled integration:
порядок pre-validation, native call sites, wrappers, scoped pending adapters и Harmony bindings.

Это не выполнение Unity, Harmony transpiler, реального отдыха, UI, respec или загрузки сохранения.
Сами native методы Train/Unlearn/Finalize проинспектированы в подготовленной Assembly-CSharp;
игровой клиент Solasta здесь не запускался. Прохождение production-планировщика и компиляция
не являются подтверждением игрового успеха или полного отката персонажа.

```bash
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.setup-solasta/dotnet-home
export NUGET_PACKAGES=/workspace/.setup-solasta/nuget DOTNET_CLI_TELEMETRY_OPTOUT=1
export SolastaInstallDir=/workspace/SolastaUnfinishedBusiness/SolastaFiles
cd /workspace/SolastaUnfinishedBusiness
dotnet --list-sdks
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
git diff --check
```

Выходная `SolastaFiles/Mods/SolastaUnfinishedBusiness/SolastaUnfinishedBusiness.dll` существует
и не пуста. DLL и остальной сгенерированный `SolastaFiles/Mods/` в коммит не входят.
Release Install на Linux не применялся.

## Ручной план: новый тестовый герой

1. В отдельной тестовой установке выбрать настройки пользователя, включая Spell Points,
   ASI+feat, оба дополнительных feat расписания, стартовый feat1, respec=true, multiclass3,
   Level20. Записать три Warlock2024 флага и включённые патроны. Включить прототип, перезапустить.
   Проверить в логе активацию, список патронов и отсутствие ошибок преобразования.
2. Создать нового Warlock с CHA≥13 и подходящими STR/DEX для будущего Fighter/Rogue.
   Не использовать ранее созданного героя. Повышать **по одному actual level**, без streamlined
   multi-level. На каждом уровне записывать actual class/total, HP, PB, known/cantrips, pact uses,
   invocations, feat/ASI и выбранные powers. Сверять с таблицей выше; racial/Tome/feat бонусы
   учитывать отдельно. На1 проверить ровно один пользовательский стартовый feat.
3. На6 пройти исходные6/7 spell events последовательно: заменить старое A на B на6,
   на7 заменить B на C. Вернуться назад, поменять A, убедиться в сбросе зависимых поздних выборов.
   Отменить повышение целиком, повторить; ни spell, ни origin rewards не должны дублироваться.
   На6 нельзя изучить заклинание4-го круга на шаге6, оно появляется на шаге7.
4. На7 проверить исходные8/9: отдельные feat/ASI и инвокации; нельзя перейти дальше
   с незаполненными обязательными независимыми пулами. На8 проверить 3 pact slots,
   четвёртый базовый cantrip и один отдельный Arcanum6. На9/10/11 получить Arcanum7/8/9.
   На12 проверить четыре pact slots5,15 базовых known,4 базовых cantrips и все четыре Arcanum.
5. Потратить один pact slot заклинанием ниже5-го круга: проверить upcast до текущего pact-круга,
   расход ровно одной ячейки и отсутствие затрат обычных Spell Points. Потратить каждый Arcanum:
   второй каст того же круга недоступен, другие круги остаются независимыми. Повторить после
   save/load в **том же** режиме. Проверить, что обычный spell не получает свободную ячейку6–9.
6. После short rest pact uses восстанавливаются, Arcanum остаётся потраченным. После long rest
   восстанавливаются оба по отдельности. Повторить с Hive extra slot и после временных
   resource modifiers: не должно быть отрицательных/лишних ячеек или повторного восстановления.
7. В2014 на12 потратить все pact slots и Arcanum, применить legacy Eldritch Master:
   восстановлены все pact uses, Arcanum/Spell Points не восстановлены, power потрачен один раз
   до long rest, применение занимает Minute1. На отдельном новом герое2024 проверить Cunning
   на2 (половина с округлением вверх) и на12 (все pact slots); legacy power одновременно отсутствует.
8. В2014 проверить 8 стандартных инвокаций; в2024 —10. Дополнительные Eldritch Adept считать
   отдельно. Наactual10 инвокация с требованием15 недоступна на исходном шаге14 и доступна на15,
   если выполнены Pact/known spell validators. Наactual12 проверить три отдельных replacement шага
   18/19/20: выбирать разные инвокации из известных **до** повышения. Сначала отменять новые
   приобретения, затем удаление. Проверить обычную цепочку новой инвокации по сценарию Stage 6.1 ниже;
   Tome/custom-chain guard не обходить через UI/консоль. Проверить cancel/back/replay без дублей.
9. В2014 создать отдельных Blade/Chain/Tome героев; проверить оружие/знакомца/3 Tome cantrips.
   В2024 повторить с pact invocations, без дополнительного Pact Selection. Проверить управление
   дополнительными spell/cantrip pools при выборе/отмене Tome и spell-granting invocations.
   Проверить prerequisite, которому нужен уже подтверждённый Pact. Pending dependencies внутри
   одного повышения пока не считать поддержанными.
10. Прогнать каждого **включённого** патрона из таблицы до12. Проверить рубежи6/8/10,
    отдельно каждый указанный в столбце «Осталось проверить» эффект. Для EldritchSurge сравнить
    количество custom Learn/Unlearn событий, MaxPoints20 на12, обычные и специальные лучи;
    затем добавить martial levels и проверить, что штраф формулы считает другие actual levels один раз.
    Не включать отключённые/скрытые патроны ради проверки discovery.
11. Получить Warlock12/Fighter8 и Warlock8/Rogue12 с некастующими подклассами: фактический total20,
    HP/PB/история остаются обычными, Warlock13 отсутствует. Проверить отказ Wizard/Sorcerer/других
    caster classes, caster subclasses и feats со вторым репертуаром. Три mixed-caster комбинации
    из таблицы должны оставаться заблокированы. Проверить расовый repertoire отдельно.
12. На отдельном тестовом герое с недостаточной CHA проверить объяснимый deadlock на12:
    требования не снимаются и пустой level-up не открывается. При наличии допустимой альтернативы
    повышение должно продолжаться в неё. Проверить лимит трёх классов и actual total20.
13. На новых тестовых героях respec на8 и12: сначала отменить, затем подтвердить смену покровителя,
    Pact и некастующего multiclass. До respec потратить pact/Arcanum/patron powers. Сверить known spells,
    four Arcanum choices/counters, invocations и origins, ASI/feat, familiars/pact weapon,
    support conditions и восстановление после отдыха. Старые powers/условия не должны оставаться,
    повторный respec не должен наращивать rewards. Любая аномалия означает запрет продолжать этот тест.
14. Повторить основные проверки на свежих героях для всех8 сочетаний трёх2024 флагов, с перезапуском
    между конфигурациями; отдельно Spell Points off/on. Для disabled regression выключить прототип,
    перезапустить и создать **ещё одного обычного** Warlock: оригинальные уровни/ячейки/инвокации,
    отсутствие Warlock12 origin definitions и запрета класса13 от этого модуля. Проверить прежние
    Fighter/Barbarian/Monk/Rogue/Wizard/Sorcerer в их прежних режимах.

## Короткий игровой сценарий A→B→C→D

1. Создать нового тестового Warlock и последовательно довести до actual11. A=`ArmorOfShadows` известна;
   B=`FiendishVigor`, C=`DevilsSight`, D=`EyesRuneKeeper` не изучены. Не использовать Tome/custom chain.
   Если перевод названий другой, ориентироваться на эти definition names. Оставить отдельный тестовый save.
2. На actual12, original18 заменить A→B **и выбрать ещё одну отдельную инвокацию E**: исходный18
   дополнительно даёт постоянную награду. E не должна совпадать с A/B/C/D или выдавать новый choice pool.
3. На original19 B должна быть видна для удаления: заменить B→C. На original20 заменить C→D.
   До подтверждения итог — D и E плюс остальные прежние инвокации; A/B/C отсутствуют, класс всё ещё12.
4. Вернуться на original18 и изменить B: шаги19/20 очищаются. Вернуть B, заново пройти B→C→D.
   Проверить Undo/Reset каждого шага и Skip: Skip не даёт бесплатного приобретения.
5. Отменить всё повышение и повторить с того же сохранения. Снова выполнить цепочку и подтвердить.
   Проверить, что остались только D/E, общее число стандартных инвокаций выросло на1, ресурсы/powers
   промежуточных B/C не остались и Spell Points/Pact Magic/Arcanum не изменились от замен.
6. Сохранить/загрузить **в том же режиме**. На отдельном прогоне проверить A→B→A, затем варианты2014/2024,
   подтверждённый Eldritch Adept, pending Thirsting/Devouring Blade, Hex и known spell requirements.
7. Попытка получить prerequisite19 на18, дважды удалить старую инвокацию, взять известную инвокацию
   или провести Tome/custom chain должна отказать без принятия повышения. После исключения финализации
   не сохранять результат: восстановить тестовый save; полный native hero rollback не заявляется.

Рекомендация: обычные resource-neutral цепочки готовы к **контролируемому** диагностическому прогону
на новых героях. Общий статус остаётся PARTIALLY READY из-за custom/choice-chain guards и отсутствия
Unity-проверки. Продолжение на Bard или другие классы в Stage 6.1 не выполнялось.

## Изменённые файлы Stage 6.1

- `Documentation/WarlockProgressionPrototype.md`
- `SolastaUnfinishedBusiness/Displays/ClassesDisplay.cs`
- `SolastaUnfinishedBusiness/Models/WarlockInvocationContext.cs`
- `SolastaUnfinishedBusiness/Models/WarlockInvocationTransaction.cs`
- `SolastaUnfinishedBusiness/Models/WarlockInvocationTransactionsContext.cs`
- `SolastaUnfinishedBusiness/Models/WarlockProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/WarlockSpellcastingContext.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/GuiInvocationDefinitionPatcher.cs`
- `Tests/FighterProgression/FighterProgression.Tests.csproj`
- `Tests/FighterProgression/Program.cs`
- `Tests/FighterProgression/WarlockInvocationTransactionTests.cs`
- `Tests/FighterProgression/WarlockTests.cs`
