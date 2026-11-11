# Bard 20→12 — Stage 7

Оценка: **PARTIALLY READY**. Реализован изолированный opt-in прототип Bard и десяти колледжей.
Его можно передать на контролируемую игровую проверку на новых тестовых персонажах.
Подтверждены компиляция и автономные проверки; Harmony, интерфейс, бой, отдых, respec и save/load
в игре не запускались. Обычная замена уже подтверждённых бонусных Magical Secrets не поддерживается
исходным `LoseSpell` и намеренно запрещена: это ограничение, а не завершённая реализация retraining Secrets.
Полная интеграция нескольких классов заклинателей остаётся за рамками Stage 7.

## Исходное состояние и проверка

Использован существующий checkout `/workspace/SolastaUnfinishedBusiness`, ветка
`feature/progression-20-to-12`, исходный HEAD `37ac8cdebdd17310b50e7d8078ad83fca432db80`.
До изменений повторно прошли **959 проверок** и **Release Workflow: 0 ошибок, 0 предупреждений**.
Реализации Fighter, Barbarian, Monk, Rogue, Wizard, Sorcerer и Warlock сохранены;
тесты транзакций инвокаций Warlock Stage 6.1 продолжают выполняться без изменения.

После изменений: **1803 проверки**, **Release Workflow: 0 ошибок, 0 предупреждений**,
`git diff --check` без замечаний. SDK **9.0.320**. Повторная установка зависимостей не потребовалась.
Выход сборки: `SolastaFiles/Mods/SolastaUnfinishedBusiness/SolastaUnfinishedBusiness.dll`.
Сборка создаёт локальный артефакт, который не входит в коммит.

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

## Активация и обратимость

В UB → Classes включить `Bard 20 to 12 prototype (NEW HEROES; restart; SINGLE CASTER ONLY)`
(`EnableBardCompressedProgressionPrototype`), затем полностью перезапустить игру.
По умолчанию настройка выключена. Требуются уже включённые `EnableLevel20` и `EnableMulticlass`;
если их нет, модуль пишет причину в лог и не активируется. Настройки пользователя автоматически
не переключаются. Целевые `MaxAllowedClasses=3`, ограничения характеристик, ASI/feats, Spell Points
и `EnableRespecAction=true` пользователь задаёт в существующем интерфейсе UB.
Другие параметры из задания, включая PCG, Epic Points, способы выбора наград и multi-level UI,
модуль не перезаписывает.

Для выключения убрать новый флаг и полностью перезапустить игру. Это восстанавливает определения
при новом запуске, но **не преобразует обратно героя**, созданного со сжатой прогрессией.
Не открывать обычные сохранения Bard в сжатом режиме и наоборот. Для проверки использовать отдельную
тестовую кампанию и новые герои. Существующий Bard выше 12 не мигрируется; продолжение повышения
блокируется с диагностикой. Изменение правил прогрессии/колледжей в текущей сессии требует немедленного
перезапуска. Семь переключателей Bard 2024 дополнительно защищены от немедленной повторной перестройки
уже сжатых определений; обработчики боя других правил всё равно могут читать текущие настройки,
поэтому продолжать игру между изменением правил и перезапуском нельзя.

Фактические значения семи Bard 2024 флагов, итоговая исходная таблица known spells, режим Spell Points
и список обнаруженных колледжей записываются при инициализации в UB log. Личного `Settings.xml`
в облачном checkout нет; значения пользовательской игровой сессии здесь неизвестны.
Проверки обоих вариантов не означают, что все опции включены у пользователя.

## Прогрессия и архитектура

| Фактический Bard | Исходные события | Эффективный уровень | Максимальный круг | Spell Points без модификаторов |
|---|---|---|---|---|
| 1 | 1 | 1 | 1 | 4 |
| 2 | 2 | 2 | 1 | 6 |
| 3 | 3 | 3 | 2 | 14 |
| 4 | 4 | 4 | 2 | 17 |
| 5 | 5 | 5 | 3 | 27 |
| 6 | 6, 7 | 7 | 4 | 38 |
| 7 | 8, 9 | 9 | 5 | 57 |
| 8 | 10, 11 | 11 | 6 | 73 |
| 9 | 12, 13 | 13 | 7 | 83 |
| 10 | 14, 15 | 15 | 8 | 94 |
| 11 | 16, 17 | 17 | 9 | 107 |
| 12 | 18, 19, 20 | 20 | 9 | 133 |

`BardProgressionContext.LateLoad` вызывается после финальных UB/2024, Spell Points и прежних модулей.
Используются существующие `FighterProgressionPlan`, `CompressedProgressionDefinitions`,
`KnownSpellsProgressionPlan`, `SpellcastingProgressionPlan` и защита однократной инициализации.
Читаются финальные определения, включая выбранные правила и реально доступные регистрации колледжей.
Скрытые/выключенные колледжи не включаются. Копии choice pools получают отдельные origin tags;
повторная одинаковая definition не считается повторной выдачей одной и той же награды.
Вложенные особенности сохраняют порядок, upgrade/overridden ссылки и custom subfeatures.
Автоподготовка клонируется с пересчётом class-level порогов, не меняя общие исходные определения.

Настоящий Bard, история классов, общий уровень, HP и PB не подменяются. `GetClassLevel` глобально
не патчится. Точечные адаптеры используют эффективный Bard для class-based powers, ресурсов,
source-class условий, auto-prepared порогов и прямых проверок Thespian/Valiance.
Бонусы от CHA/PB остаются бонусами от реальных CHA/PB. С выключенным флагом адаптеры пропускают
штатную обработку. Общий предел 20 и максимум классов остаются существующими правилами UB;
новый модуль не меняет пользовательское значение максимума классов.

## Заклинания, известные заклинания и замены

Обычный слотный режим на Bard 12 использует исходную строку полного заклинателя20:
**4/3/3/3/3/2/2/1/1** ячеек кругов1–9. В режиме Spell Points применяется исходная UB таблица,
включая её цены, ограничения высоких кругов, ограничения ячеек и long-rest восстановление.
Это отдельные режимы, они не суммируются. Индекс caster-level ограничен 0–20.
Новых Wizard spellbook, Sorcery Points или pact ресурсов Bard не получает.
College of Life сохраняет исходные affinity/war-list модификаторы; это не второй репертуар.

Важно: поздний `SwitchOneDndPreparedSpellsTables` заменяет раннюю таблицу `Level20Context`.
Поэтому конечные числа взяты из **финального UB**, а не из внешней таблицы D&D:

| Таблица known | Исходные1–20 | Фактические1–12 |
|---|---|---|
| `EnablePreparedSpellsTables2024=false` | 4,5,6,7,8,9,10,11,12,14,15,15,16,18,19,19,20,22,22,22 | 4,5,6,7,8,10,12,15,16,19,20,22 |
| `EnablePreparedSpellsTables2024=true` | 4,5,6,7,9,10,11,12,14,15,16,16,17,17,18,18,19,20,21,22 | 4,5,6,7,9,11,14,16,17,18,19,22 |

Это **обычные** известные заклинания, без бонусных extra-tag Secrets, auto-prepared и внешних affinity
надбавок. Независимый переключатель Magical Secrets 2024 не заменяет этот переключатель таблицы.
Числа не исправляются прототипом под предполагаемую внешнюю норму. Обычные cantrips по фактическим
уровням: **2,2,2,3,3,3,3,4,4,4,4,4**. Нулевые placeholders исходных17–20 не отнимают уже известные cantrips.

Все20 исходных событий обучения сохранены. При штатной таблице замен это19 независимых необязательных
замен. На Bard8 шаг10 предшествует11; на12 отдельно обрабатываются18,19,20. Каждый шаг имеет свой
бюджет обучения, unlearn и допустимый круг. Skip замены не даёт дополнительное заклинание.
Заклинание, выученное на10, можно заменить на11. Изменение раннего выбора инвалидирует зависимые
поздние временные выборы. Cantrips, обычные known и extra-tag bonus spells учитываются раздельно.
Уже известное бонусное заклинание нельзя повторно выбрать; штатное пересечение auto-prepared/ordinary
сохраняется и не теряет bonus источник после удаления ordinary версии.

Production `BardSpellLearningState<T>` строит неизменяемое состояние перед каждым шагом.
Все обычные и Secrets события класса/подкласса объединены по исходному уровню, затем по типу события.
Lore6 Secrets применяются **до** ordinary7. До native финализации проверяются бюджеты, списки, круги,
дубли и финальное ordinary количество. Native выдачи идут в этом же порядке и учитываются в журнале
по фактическому повышению: повторный grant того же origin пропускается, следующий Bard уровень разрешён.
Back/Reset/Cancel очищают временные выборы. Если сторонний/native код падает после начала выдачи,
повторная финализация того же повышения блокируется: требуется отмена/загрузка тестового save.
**Полной атомарности всех изменений героя при исключении здесь нет.**

## Magical Secrets: два варианта

| Источник | Исходный уровень → фактический | Бюджет | Предел круга именно этого события |
|---|---|---|---|
| Legacy класс | 10→8 | 2 | 5 |
| Legacy класс | 14→10 | 2 | 7 |
| Legacy класс | 18→12 | 2 | 9 |
| Lore Additional Secrets, оба режима | 6→6 | 2 | 3 |
| Magical Secrets2024 | 10→8 | Расширение списков, без legacy6 выборов | Обычный круг соответствующего original события |

Сохраняются native `CantripOrSpell`, MinSpellLevel, ritual и source-list ограничения pools.
На actual8 обычный шаг11 уже допускает6 круг, но Secrets исходного10 всё ещё ограничены5.
Аналогично Lore6 не даёт4 круг из-за сжатого effective7. Native legacy switch повторно использует
definition `PointPoolBardMagicalSecrets14` на10 и14: отдельные клоны по origin предотвращают слияние.

Legacy даёт **6 отдельных бонусных выборов** к обычным22; Lore ещё2. Среди них могут быть cantrips,
хранящиеся в extra-tag канале, поэтому их нельзя вычитать из обычного бюджета4 cantrips.
В 2024 вместо legacy pools выбирается существующий FeatureSet с тремя MagicAffinity:
Cleric/Druid/Wizard extended lists. Это доступ для обычного Bard обучения с исходного10,
включая применимые cantrips в native UI, без создания чужого `CastSpell`/репертуара.
Lore2 остаётся независимо от выбора base-класса. Все выбранные Secrets принадлежат Bard repertoire.
Expanded lists не считаются вторым заклинателем защитой multiclass.

**Ограничение замены:** native `RulesetSpellRepertoire.LoseSpell` удаляет только `KnownSpells`,
не `ExtraSpellsByTag`. Поэтому подтверждённые legacy/Lore bonus Secrets не предлагаются для обычного
unlearn; иначе игрок получил бы замену, не потеряв прежний bonus spell. Незавершённый Secrets выбор
можно отменить/перевыбрать в своём шаге. Изменение подтверждённого выбора через respec требует
отдельной игровой проверки и не объявляется безопасной миграцией. Обычные cross-list spells 2024,
выбранные в ordinary known, используют обычную событийную замену с действующим списком/кругом.

## Особенности Bard и D&D 2024

| Особенность/опция | Сохранённое поведение |
|---|---|
| Bardic Inspiration dice | D6 с1, D8 с5, D10 с8 (original10), D12 с10 (original15); native атрибут die, без нескольких независимых источников |
| Inspiration uses | Исходный CHA modifier и minimum1; расход вдохновений и цели/реакции native |
| `EnableBardicInspiration2024` | 10 минут / 1 час по исходному варианту, без изменения CHA или PB |
| Font of Inspiration | Original5→5, native fast Bardic Inspiration recovery при short rest |
| Jack of All Trades | Сохраняет реальный character PB и его штатное округление |
| `EnableBardExpertiseOneLevelBefore2024` | Legacy3,10→actual3,8; 2024 2,9→actual2,7; два независимых выбора по2 Expertise |
| `EnableBardCounterCharm2024` | Original6/7→actual6; legacy Action либо исходная реакция2024; native реакция расходуется обработчиком, дополнительная цена Inspiration не придумана |
| `EnableBardMagicalSecrets2024` | Legacy три pools либо extended C/D/W lists, как описано выше; Lore независимо |
| `EnableBardSuperiorInspiration2024` | Legacy original20 либо2024 original18→actual12; один выбранный capstone, native восстановление минимум1/2 Inspiration при начале боя |
| `EnableBardWordsOfCreation2024` | Original20→12: auto-prepared Power Word Heal/Kill и исходный two-target modifier; native стоимость slots/points, бесплатные применения не добавляются |
| `RemoveBardSongOfRest2024` | Если включено — особенность отсутствует; иначе получение2 и die D6/D8/D10/D12 сactual2/7/9/11 |

Native rest handler выбирает Song of Rest rank по общему уровню персонажа. Новый адаптер меняет
только бонус с точным provider `RestHealingModifierBardSongOfRest` на эффективный **Bard class** rank;
прочие источники отдыха остаются штатными. Сами отдых/лечение в игре ещё не проверены.
Words of Creation auto-prepared groups клонируются вместе с custom subfeatures; ранний порог внутри
группы не выдаёт spell раньше получения самой особенности на12.

Классовые ASI original4,8,12,16,19→actual4,7,9,11,12 сохранены без удаления повторных definitions.
`EnablesAsiAndFeat`, дополнительные графики feats и feat первого уровня остаются исходными наградами UB.
Их origin, Expertise, обычное обучение и Secrets не объединяются. Duplicate-expertise/proficiency
ограничения остаются native; GUI повторных выборов проверяется по плану ниже.

## Матрица колледжей

Покрыты четыре официальных и шесть зарегистрированных UB классов. При запуске обрабатываются только
реально доступные колледжи; приведённый список не включает их принудительно. Для всех сохранены
исходные события/порядок, costs, recovery, feature references и особые выборы.

| Колледж | События и особенности | Ресурсы/численные эффекты и что проверить в игре |
|---|---|---|
| Official Heroism | 3 Bolster Morale/Heroic Tale;6→6 Thundering Voice;14→10 At Road's End | CHA/Inspiration эффекты; Heroic Tale Action ShortRest1; поздняя reaction ShortRest; не выдавать второй источник |
| Official Hope | 3 Healing Ballad/bonus auto-prepared; Words of Hope6/9/13/17→6/7/9/11; Song of Hope14→10 | Цепочка overridden powers6→9→13→17; Bardic die healing/CHA; Prayer of Healing3 и Mass Healing Word/Revivify6; групповой отдых/лечение |
| Official Lore | 3 skills/Cutting Words; Additional Secrets6→6; Peerless Skill14→10 | Native Inspiration reactions; отдельные2 Secrets max3; skill/Expertise повторные выборы |
| Official Traditions | 3 Ancient Tradition/Aura; Verbal Onslaught6→6; Manacalon's Perfection14→10 | Inspiration reaction и saving effects; поздний BonusAction ShortRest1; aura/conditions |
| UB Audacity | 3 proficiencies/fighting style/Audacious Whirl; Extra Attack/cantrip replacement6→6; Masterful Whirl14→10 | Native Inspiration cost и free D6 при Masterful; die/attack replacement, без вымышленного расхода |
| UB Elegance | 3 Grace/Elegant Fighting; Evasive Footwork/Extra Attack6→6; Amazing Display14→10 | Inspiration maneuvers, reactions/movement conditions; PB-based uses остаются реальными |
| UB Guts | 3 proficiencies/Combat Magic Improved; Extra Attack/cantrip replacement6→6; Arcane Deflection14→10 | Native бой с заклинанием и реакция, стоимость/атрибуты; нет нового casting repertoire |
| UB Life | 3 Heightened affinity; Transmute Force/Necrotic Resistance6→6; hardened Necrotic/Command Undead14→10 | War-list slot modifier2 сохраняется; общий PB-based native long-rest pool, BonusAction bundle (сопротивления/полёт/heal/revive и др.); не размножать pool при compressed grants |
| UB Thespian | 3 Combat Inspiration/Terrific Performance/style; Finale/Extra Attack6→6; Improved Terrific Performance14→10 | Прямая проверка Bard14 заменена эффективным Bard; power upgrade references, native BI die/расход/возврат |
| UB Valiance | 3 Captivating Presence/Disheartening Performance; Steadfast/Tongues6→6; Heroic Inspiration14→10 | Прямой subclass-level6 порог адаптирован; Tongues auto-prepared; BI use, Heroic Inspiration BonusAction LongRest2/targets2 |

## Multiclass и тупики

| Комбинация | Состояние Stage7 |
|---|---|
| Bard12/Rogue8 | Разрешается при исходных prerequisites и неколдующем подклассе Rogue |
| Bard12/Fighter8 | Разрешается при исходных prerequisites и неколдующем подклассе Fighter |
| Bard8/Rogue6/Fighter6 | Разрешается при лимите3, исходных prerequisites и неколдующих подклассах |
| Bard12/Sorcerer8 | Заблокировано: два заклинателя, будущая стадия |
| Bard12/Warlock8 | Заблокировано, pact + full-caster бюджет не объединяется |
| Bard + Wizard/Cleric/Druid/Paladin/Ranger/Inventor/другой caster | Заблокировано; новые классы не реализованы |
| Второй casting subclass/feat | Защита выбора и финализации; racial repertoire сам по себе допускается |

Остаются прежние guards Wizard/Sorcerer/Warlock. Spell Points/slots Bard считаются по его эффективному
уровню только для поддержанного single-caster героя, в том числе с martial классами. Реальный PB
зависит от общего уровня. Bard13 запрещён на UI/AssignClass/AddClass границах.
Если на Bard12 нельзя выйти из класса (например, CHA<13), отсутствует подходящая альтернатива,
достигнут лимит классов либо все альтернативы запрещены caster guards, повышение блокируется с
диагностикой причины. Характеристики автоматически не исправляются. Это возможный явный тупик,
а не разрешение пустого/невалидного повышения. Для планируемого martial multiclass заранее выдержать
существующие требования входа/выхода. Указанный в задании максимум3 нужно сохранить в настройках UB.

## Respec, сохранения и границы гарантии

Исходный UB respec пересоздаёт героя и переносит часть идентичности/состояния. Новый модуль не пишет
сохранения, `Settings.xml`, игровые бинарники и не добавляет миграцию. Очистка временных spell/Secrets
origins и Expertise choices уменьшает риск повторной выдачи при Back/Cancel. Это не доказательство
чистоты полного respec: известные spells, bonus tags, auto-prepared, ресурсы, использованные Inspiration,
активные conditions и выбор другого колледжа нужно проверять в Unity.

Нельзя гарантировать работу сторонних модов, изменение definitions после LateLoad, сериализацию новых
origin tags, повторный respec, рестарт на середине level-up, совместимость обычных и compressed saves,
полный rollback при native исключении. Уже подтверждённые bonus Secrets не заменяются ordinary retrain.
Комбинации нескольких заклинателей сознательно не поддержаны. Это основания оценки PARTIALLY READY.

## Что проверяют автоматические проверки

Из1803 проверок959 прежних проходят неизменёнными,844 добавлены для Bard.
Поведенческие проверки исполняют production planners/transformer на managed doubles и production
`BardSpellLearningState`/grant ledger: все исходные события, точный mapping,10 college fixtures,
полнота/порядок наград, origin repeated pools, неизменность входных definitions, opt-off/однократность,
slot rows/points/known/cantrip проекции, обычные цепочки замен, duplicate rejection и bonus alias,
событийный порядок Lore/Secrets, повторные grants и переход к следующему actual уровню.
Все128 сочетаний семи Bard2024 флагов представлены модельными source fixtures и передаются реальному
transformer. Это **не** запуск128 игровых конфигураций и не исполнение native switch methods.

Отдельные Cecil/metadata проверки читают скомпилированные UB/game assemblies и JSON blueprints:
реальные final known arrays,2024 switches/extended lists, legacy pools, inspiration die/counts,
Font/Song/Words/Superior определения, места интеграции и Harmony target/argument имена.
Они устанавливают структуру bindings и наличие нужных вызовов, но не исполняют Harmony/Unity,
нативный интерфейс, отдых, сериализацию или respec. Сборка не является игровым успехом.

## Ручной игровой план — только новые тестовые герои

1. Создать отдельную тестовую кампанию. Сначала при выключенном прототипе после полного рестарта
   создать обычного Bard: проверить несжатые unlocks, slots, рост выше12, отсутствие новых origin tags.
   Записать исходные настройки и baseline counters. Затем включить прототип, проверить целевые UB
   настройки из задания (включая Respec=true), выбрать правила и доступные колледжи; полный рестарт.
   Проверить в UB log actual flags/таблицу/колледжи. Использовать нового Bard с CHA≥13 и подходящими
   martial prerequisites, без casting feats. Старых героев не загружать.
2. Сделать отдельные серии для legacy Secrets и2024 Secrets; независимо повторить с generic prepared
   table off/on. Для семи Bard флагов минимально проверить all-off, all-on и каждый изолированный флаг
   на fresh hero/restart; автоматические128 fixtures не заменяют реальные комбинации правил.
3. На каждом actual1–12 сверить исходные события с таблицей выше, реальный HP/class history/общий
   уровень/PB, ordinary known и cantrips. На4/7/9/11/12 записать отдельные ASI, class feats и выбранные
   дополнительные feats, включая first-level feat; награды не теряются и не дублируются после Back.
   Expertise проверить два отдельных выбора по2:3/8 либо2/7. Повтор уже взятой Expertise не предлагается.
4. Inspiration: на1/5/8/10 проверить D6/D8/D10/D12, число применений по CHA, стоимость и реакцию.
   Израсходовать счётчик: до Font восстановить long rest, после5 проверить short rest. Jack of All Trades
   сверить с фактическим PB. Для обеих Countercharm схем на6 проверить action/reaction и спасброски.
   Song включён: после2/7/9/11 проверить D6/D8/D10/D12 и отдых группы; выключен — его вообще нет.
5. На actual6 (orig6/7) выбрать Lore Secrets: ровно2 bonus cantrip/spell, max3 круг, отдельный Bard tag.
   Ordinary шаг7 допускает4 круг, но Lore6 — нет. Затем ordinary7 не должен повторно выбрать Lore spell.
   Повторить без Lore и убедиться, что эти2 выбора отсутствуют.
6. На actual8 legacy проверить ordinary10→Secrets10→ordinary11. Secrets ровно2 max5 круг;
   ordinary11 max6. Выучить A на ordinary10 и заменить A на B на11: в готовом repertoire A отсутствует,
   B присутствует один раз, общий known верен. Skip unlearn не даёт бесплатный spell.
   На10 Secrets14 ровно2 max7, хотя ordinary15 допускает8; на12 Secrets18 ровно2 max9.
   Выбор cantrip через Secrets не увеличивает ordinary cantrip count и расходует bonus selection.
7. Повторить шаг6 в2024: legacy class pools отсутствуют, ordinary original10 допускает spells C/D/W
   соответствующего круга; не создаются Cleric/Druid/Wizard repertoires. Обычный cross-list spell
   заменяется на следующем original событии. Lore2 сохраняется. Проверить cantrip доступ в момент
   появления2024 affinity. Подтверждённый legacy/Lore Secrets не предлагается для ordinary unlearn.
8. На actual12 пройти три отдельных шага18/19/20 и цепочку A→B→C с пропусками замен. На каждом
   многошаговом уровне использовать Back, Reset, Skip, Cancel; изменить ранний выбор и убедиться,
   что зависимые поздние выборы очищены и запрашиваются снова. Отменить всё повышение и повторить:
   ни spells, ни Secrets, Expertise, feats и ресурсы не прибавляются дважды. Проверить ordinary spell,
   который одновременно auto-prepared: удаление ordinary не отнимает bonus источник.
9. Сначала слотный, затем отдельный fresh-hero Spell Points профиль: сверить таблицу на каждом уровне,
   actual11 даёт9 круг, actual12 без модификаторов133 points. Проверить реальные стоимости, upcast,
   ограничение повторных spells высоких кругов, short/long rest и отсутствие двойного slot+point бюджета.
   College of Life проверить отдельно с его war-list модификатором. PB-based ресурсы martial мультикласса
   считать по реальному общему уровню, не20 из-за effective Bard.
10. На12 израсходовать Inspiration и начать бой: только выбранный Superior вариант восстанавливает
    минимум1/2. Words of Creation on — Heal/Kill auto-prepared, native две цели и штатная цена;
    off — бонуса нет. Проверить повторный бой/отдых, отсутствие второго capstone/лишних применений.
11. Для каждого из10 колледжей из матрицы создать отдельного героя и проверить все указанные ступени,
    late feature14→10, стоимости/реакции, upgrade chains и отдых. Для Hope особенно6/7/9/11 healing
    upgrades, Thespian улучшенную способность на10, Valiance Tongues/Steadfast6 и Heroic Inspiration10,
    Audacity/Elegance/Guts Extra Attack/cantrip replacement, Life shared pool без умножения.
12. Проверить Bard12/Rogue8, Bard12/Fighter8, Bard8/Rogue6/Fighter6 с разрешёнными subclass/атрибутами:
    total≤20, classes≤3, Bard13 недоступен, новый martial level не добавляет Bard learning events.
    Bard+Sorc, Bard+Warlock, Bard+Wizard и casting subclasses/feats должны отклоняться до принятия.
    Magical Secrets при этом не блокируются. Отдельный герой с CHA<13 после Bard12 должен получить
    объяснимую блокировку; не должен попасть в пустой выбор и не должен получить автоматическое CHA13.
13. При Respec=true записать ordinary/bonus/auto-prepared spells, Expertise, class/subclass, Inspiration
    die/used count, points/slots, powers и conditions. Respec в тот же колледж, другой колледж и другой
    разрешённый класс; повторить и отменить на середине. После каждого шага сверить origins и все
    счётчики: старые Lore/Secrets/conditions/ресурсы не должны накапливаться. При несоответствии остановить
    тест, сохранить лог и загрузить исходный тестовый save; не объявлять эту серию совместимой.
14. Сохранить и загрузить завершённого тестового Bard в том же профиле; проверить spell tags, доступ,
    боевые реакции, ресурсы и следующее повышение. Изменить правило в сессии: повышение должно
    потребовать рестарт. Проверку выключения выполнять на другом свежем обычном герое, не пытаться
    откатить compressed save. При исключении финализации не повторять её и не сохранять результат.

## Изменённые файлы

Новые production файлы (5):

- `Models/BardProgressionRules.cs` — opt-in mapping и cap.
- `Models/BardProgressionContext.cs` — поздняя трансформация, origins, cap/deadlock, scaling/rest/restart guards.
- `Models/BardSpellcastingContext.cs` — таблицы, independent learning steps, UI/commands и ordered native grants.
- `Models/BardMagicalSecretsContext.cs` — Secrets metadata, source-list/circle validation, interleaved choices и finalize guard.
- `Models/BardSpellLearningState.cs` — immutable ordinary/bonus inventory и once-per-level grant ledger.

Изменённые production файлы (14), все пути относительно `SolastaUnfinishedBusiness/`:

- `Api/Helpers/MulticlassInOutRulesHelper.cs`
- `Behaviors/ModifyPowerPoolAmount.cs`
- `CustomUI/MulticlassGameUi.cs`
- `Displays/ClassesDisplay.cs`
- `Models/BootContext.cs`
- `Models/SharedSpellsContext.cs`
- `Models/SpellPointsContext.cs`
- `Models/WizardProgressionContext.cs`
- `Patches/CharacterBuildingManagerPatcher.cs`
- `Patches/RulesetActorPatcher.cs`
- `Patches/RulesetCharacterHeroPatcher.cs`
- `Settings.cs`
- `Subclasses/CollegeOfThespian.cs`
- `Subclasses/CollegeOfValiance.cs`

Тесты (3): `Tests/FighterProgression/BardTests.cs`, `FighterProgression.Tests.csproj`, `Program.cs`.
Документация (1): этот файл. Итого23 файла:19 production,3 test,1 documentation.
Proprietary DLL, generated artifacts, личные настройки и сохранения в эти изменения не входят.
Другие новые классы не реализованы. Для отдельного UMM patch потребуются независимая загрузка/порядок
после UB, собственные bindings к его planners/settings и проверка конфликтов Harmony; готовым отдельным
UMM модом текущая интеграция не является.
