# Warlock 20→12 — Stage 6

Статус: **PARTIALLY READY**. Прототип компилируется; проверки планировщиков и метаданных проходят.
Полноценная игровая проверка не выполнена. Ограничение последовательной замены инвокаций описано ниже;
поэтому завершённой реализацией всех требований Stage 6 этот этап не объявляется.

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

**Неполная механика:** стандартная замена пока допускает только инвокацию, известную до начала
текущего фактического повышения. Запрещены замена приобретённой на предыдущем исходном событии
этого же повышения и повторное приобретение в этом же повышении ранее известной инвокации.
Причина: нативный `FinalizeCharacter` сначала применяет все удаления, затем все приобретения,
что не эквивалентно чередованию событий. Такое ограничение предотвращает опасную цепочку,
но не выполняет требование полной семантики всех промежуточных замен.

Повторное удаление одной старой инвокации запрещено; сначала нужно отменить новые приобретения,
затем отменять финансировавшую их замену. При очистке origin-тегов очищаются временные выбранные
инвокации и их пулы. Некоторые кнопки нативного UI могут оставлять уже выбранную для другого шага
старую инвокацию видимой; повторный запрос отклоняется без нового ресурса.
Пулы от Eldritch Adept сохраняются отдельно, однако сложные зависимости pending Pact/known spell
между инвокациями одного повышения и редкие ранние замены с Eldritch Adept ещё не закрыты.

EldritchSurge использует отдельную систему custom invocation pools: повторные Learn/Unlearn
получают исходные origin-теги, сохраняющие количество и порядок, а не сливаются по actual level.
Полная транзакционная семантика цепочек custom-инвокаций внутри одного повышения не подтверждена.
Это дополнительный повод не считать Stage 6 завершённым.

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

Возможное выделение в отдельный UMM patch: логика изолирована в четырёх Warlock-модулях,
но использует внутренние builders/планировщики UB, SharedSpellsContext и его UI. Отдельный patch
должен зависеть от совместимой версии UB, запускаться после его окончательного bootstrap и иметь
доступ к internals/reflection или публичному API. Независимый от UB готовый мод не реализован.

## Проверки и сборка

Baseline проверен заново: **625 checks passed**, Release Workflow — **0 errors / 0 warnings**.
После изменений: **882 checks passed**, из них **257 новых проверок**, Release Workflow —
**0 errors / 0 warnings**. SDK **9.0.320**. Повторная установка зависимостей не требовалась.
Существующая настройка Sorcerer progression/Spell Points/Metamagic сохранена;
его исходники не переписаны и все прежние проверки проходят.

Автоматически исполняются реальные production planners/transformer с managed doubles:
маппинг20 событий, исходные fixtures класса/12 патронов, порядок и повторные rewards,
ASI/feat, pact rows и known/replacement totals, Arcanum milestones, варианты2024,
one-shot initialization и выключенный latch. Cecil дополнительно проверяет вызовы
собранного мода, native Harmony targets/имена аргументов, scoped rank dispatch, guards
и раздельные поля использования/восстановления ресурсов.

Проверки fixtures и synthetic variants не исполняют реальный `LateLoad` внутри Unity,
не применяют Harmony и не доказывают игровые short/long rest, consumption, UI, respec или
save/load. Во время реального `LateLoad` есть дополнительная проверка сохранения signatures
преобразованных наград. В среде подготовлены игровые reference DLL, но игровой клиент здесь
не запускался. Успешная компиляция не является успехом в игре.

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
   приобретения, затем удаление. Цепочку замены новой инвокации в этом же повышении считать
   ожидаемо неподдержанной, а не обходить через UI/консоль. Проверить cancel/back/replay без дублей.
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

Рекомендация: допустим только ограниченный диагностический прогон на новых тестовых героях
с учётом перечисленных ограничений. Для завершения Stage 6 сначала требуется полноценная
транзакционная модель инвокаций/pending prerequisites и подтверждение игровых сценариев.

## Изменённые файлы

- `Documentation/WarlockProgressionPrototype.md`
- `SolastaUnfinishedBusiness/Api/Helpers/MulticlassInOutRulesHelper.cs`
- `SolastaUnfinishedBusiness/Behaviors/ModifyPowerPoolAmount.cs`
- `SolastaUnfinishedBusiness/CustomUI/CustomInvocationSelectionPanel.cs`
- `SolastaUnfinishedBusiness/CustomUI/MulticlassGameUi.cs`
- `SolastaUnfinishedBusiness/Displays/ClassesDisplay.cs`
- `SolastaUnfinishedBusiness/Models/2024WarlockContext.cs`
- `SolastaUnfinishedBusiness/Models/BootContext.cs`
- `SolastaUnfinishedBusiness/Models/SharedSpellsContext.cs`
- `SolastaUnfinishedBusiness/Models/WarlockInvocationContext.cs`
- `SolastaUnfinishedBusiness/Models/WarlockProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/WarlockProgressionRules.cs`
- `SolastaUnfinishedBusiness/Models/WarlockSpellcastingContext.cs`
- `SolastaUnfinishedBusiness/Models/WizardProgressionContext.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/GuiInvocationDefinitionPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetActorPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterHeroPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetImplementationManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Settings.cs`
- `SolastaUnfinishedBusiness/Subclasses/Builders/EldritchVersatility.cs`
- `SolastaUnfinishedBusiness/Subclasses/Builders/InvocationsBuilders.cs`
- `SolastaUnfinishedBusiness/Subclasses/PatronCelestial.cs`
- `SolastaUnfinishedBusiness/Subclasses/PatronEldritchSurge.cs`
- `SolastaUnfinishedBusiness/Subclasses/PatronMoonlitScion.cs`
- `SolastaUnfinishedBusiness/Subclasses/PatronMountain.cs`
- `SolastaUnfinishedBusiness/Subclasses/PatronSoulBlade.cs`
- `Tests/FighterProgression/FighterProgression.Tests.csproj`
- `Tests/FighterProgression/Program.cs`
- `Tests/FighterProgression/WarlockTests.cs`
