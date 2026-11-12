# Ranger 20→12 — Stage 11

**PARTIALLY READY.** Реализован opt-in перенос прогрессии Ranger и адаптеры ресурсов/обучения. Боевые действия, Harmony, GUI, отдых, спутники, respec и сохранения в Solasta не запускались. Рекомендуется только контролируемый игровой прогон на новых тестовых героях. Автоматическое обновление уже призванного спутника при повышении заменено явным безопасным ограничением: сначала убрать спутника, затем повысить уровень и вызвать его заново.

## Исходная точка и включение

Checkout: `/workspace/SolastaUnfinishedBusiness`, ветка `feature/progression-20-to-12`. Baseline — Stage 10 `925a742d211750c54792448df84f23f5217ee011`: подтверждены **3679** проверок, SDK **9.0.320**, Release Workflow **0 ошибок / 0 предупреждений**. До проверки baseline исходники не менялись. Предыдущие одиннадцать классов и их guards сохранены. Ranger — единственный новый класс; Inventor и shared multicaster не реализуются.

В UB → Classes включить **Ranger 20 to 12 prototype**, полностью перезапустить игру и создать нового тестового Ranger. `EnableRangerCompressedProgressionPrototype` по умолчанию `false`; необходимы `EnableLevel20=true` и `EnableMulticlass=true`. Код их не включает. Для отключения снять флаг, перезапустить игру и использовать обычного контрольного героя. Отключение не конвертирует сжатого героя обратно.

Все пользовательские опции, включая `MaxAllowedClasses=3`, `EnableMinInOutAttributes`, Spell Points, casting с1, ASI/feat-переключатели, `DisableStreamlinedMultiLevelUp`, `EnableRespecAction`, Epic Points и PCG, остаются без автоматических изменений. `Settings.xml`, файлы сохранений и оригинальные игровые DLL не переписываются.

Использовать отдельные тестовые сохранения. Старые обычные Ranger, в том числе уровней1–12, не поддерживаются для автоматической конвертации. Ranger выше12 получает диагностику и блокировку дальнейшего повышения/магических классовых действий. Надёжного определения режима произвольного старого сохранения уровней1–12 нет: новый герой обязателен. Не сохранять героя после переключения режима/правил до полного перезапуска.

## Прогрессия

| Фактический Ranger | Оригинальные события повышения | Эффективный Ranger | Основные milestones выбранных правил |
|---|---|---|---|
| 1 | 1 | 1 | Legacy enemy/terrain либо Favored Enemy2024; Weapon Mastery по исходной опции; casting при старте1 |
| 2 | 2 | 2 | Fighting Style, casting при старте2; Deft Explorer2024 |
| 3 | 3 | 3 | Архетип; Primeval Awareness, если не удалён соответствующим флагом |
| 4 | 4 | 4 | ASI/feat |
| 5 | 5 | 5 | Extra Attack |
| 6 | 6–7 | 7 | Legacy enemy/terrain6; Roving6; способности архетипа7 |
| 7 | 8–9 | 9 | ASI8, Land's Stride8, Expertise2024 на9, третий круг |
| 8 | 10–11 | 11 | Terrain10, Hide in Plain Sight либо Tireless10; способности архетипа11 |
| 9 | 12–13 | 13 | ASI12; Relentless Hunter13 |
| 10 | 14–15 | 15 | Enemy14, Vanish либо Nature Shroud14; способности архетипа15 |
| 11 | 16–17 | 17 | ASI16, Precise Hunter17, пятый круг |
| 12 | 18–20 | 20 | Feral Senses18, ASI19, Foe Slayer20, последние события обучения/замен |

Трансформируются окончательные definitions после UB/2024, Spell Points, поздних subclass-изменений и всех предыдущих progression-модулей. Все события и порядок исходника сохраняются, включая повторные награды. Высокие события группируются в Union-обёртки; point pools и custom invocation pools сохраняют независимые origin-теги, типы, размеры и обработчики. Идентичности сил и их override-связи не заменяются универсальными силами.

Реальные class levels, история, HP и PB не виртуализируются. Глобального патча `GetClassLevel` нет. Только конкретные вычисления Ranger class-rank получают эффективное значение. ASI4/8/12/16/19 → actual4/7/9/11/12; дополнительные feats2/6/10/14/18 →2/6/8/10/12. При целевых трёх ASI/feat-опциях это пять ASI и десять классовых feats, плюс отдельная исходная награда первого уровня. На12 bonus feat18 и feat из ASI19 остаются двумя выборами. Расовые особенности первого уровня не переписываются.

## Магия и Spell Points

Установленный `CastSpellRanger` — **Selection / AllKnown**, собственный half-caster с WIS. `EnableRangerSpellCastingAtLevel1` выбирает HalfRoundUp/начало1 либо Half/начало2. В одиночной прогрессии UB обе таблицы слотов и бюджет очков округляют половину вверх, но legacy Half не колдует на1. Это отличается от округления Half вниз при объединении нескольких магических классов; такое объединение здесь заблокировано.

Строки слотов проецируются из окончательной таблицы UB, а не создаются как full-caster. Spell Points берутся из существующей `SpellPointsByLevel` по одиночному Ranger-вкладу. Martial levels не прибавляются. Уже проецированные строки индексируются фактическим уровнем; повторной компрессии нет.

| Actual | Effective | Макс. круг | Spell Points | Known legacy | Known table2024 | Необязательных замен в этом повышении | Бесплатных Mark2024 / long rest |
|---|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 4 | 2 | 2 | 0 | 2 |
| 2 | 2 | 1 | 4 | 2 | 3 | 0 | 2 |
| 3 | 3 | 1 | 6 | 3 | 4 | 1 | 2 |
| 4 | 4 | 1 | 6 | 3 | 5 | 1 | 2 |
| 5 | 5 | 2 | 14 | 4 | 6 | 1 | 3 |
| 6 | 7 | 2 | 17 | 5 | 7 | 2 | 3 |
| 7 | 9 | 3 | 27 | 6 | 9 | 2 | 4 |
| 8 | 11 | 3 | 32 | 7 | 10 | 2 | 4 |
| 9 | 13 | 4 | 38 | 8 | 11 | 2 | 5 |
| 10 | 15 | 4 | 44 | 9 | 12 | 2 | 5 |
| 11 | 17 | 5 | 57 | 10 | 14 | 2 | 6 |
| 12 | 20 | 5 | 64 | 11 | 15 | 3 | 6 |

Таблица соответствует целевому началу casting с1. При старте2 на actual1 нет casting, ячеек/очков и обычных known spells; независимая сила Favored Enemy2024 сохраняет свои исходные бесплатные применения. Обычные ячейки на12 — **4/3/3/3/2**, максимум круг5. Очки — **64**, не133; круги6–9 не выдаются. Стоимости1–5 —2/3/5/6/7. В режиме очков отдельный полный пул обычных ячеек сверху не добавляется. Сохраняется исходная особенность UB: при старте1 `SwitchFeatureDefinitionCastSpellSlots` использует обычную HalfRoundUp-таблицу и для режима очков; модуль проецирует выбранный источник без отдельного исправления этого поведения.

### Обучение, замены и «prepared2024»

`EnablePreparedSpellsTables2024` в текущем UB **меняет cumulative KnownSpells**, достигая15 вместо11 на original20. Он не переключает Ranger на WholeList/Prepared и не добавляет ежедневный свободный выбор всего списка. Такой отсутствующий механизм не заявлен реализованным. Внешняя замена определения на WholeList отклоняется как неподдерживаемая.

`KnownSpellsProgressionPlan` и `SpellcastingProgressionPlan` сохраняют каждый исходный learning/replacement event. На actual12 последовательно обрабатываются original18,19,20: три отдельных необязательных замены, а original19 ещё даёт обычное обучение. Всего сохраняются18 replacement events original3–20. Замена не превращается в бесплатное добавление: новый spell требует реального удаления либо learning award. `data.CanUnlearn` учитывается.

Для каждого original event берётся собственный максимальный круг из окончательной строки слотов. На actual7 original8 ещё ограничен вторым кругом, original9 уже допускает третий. Аналогично original12/13 и16/17. Разрешена замена spell, выученного на более раннем original event того же повышения. Удалить неизвестный/будущий spell, выбрать дубль, обойти исходный список или пропустить обязательное обучение нельзя.

UI идёт по original events: unlearn8 → learn8 → unlearn9 → learn9, а не сначала все удаления. Back/Reset раннего выбора очищает зависимые поздние решения; Cancel чистит собственные origins/ledger. Перед finalization проверяются события и итоговый счёт. Native `LoseSpell` выполняется до `GrantSpell` внутри каждого тега; добавлена защита от повторной выдачи одного тега на том же actual level.

Subclass auto-prepared groups переводятся с сохранением списков/порядка/класса-владельца. Original13/17 → actual9/11. Bonus spells не создают второй class repertoire. Source-list extensions учитывают исходный уровень class/subclass affinity. Druidic Warrior и аналогичные независимые Race-origin cantrip repertoires остаются в штатном пути и не увеличивают class caster budget. Нерacial-вторая магическая прогрессия блокируется.

## Hunter's Mark и Favored Enemy

В legacy режиме сохраняются исходные spell, concentration, enemy/terrain rewards и native damage/resource pipeline. Прототип не выдаёт бесплатную2024-силу сам по себе. При `EnableRangerFavoredEnemy2024` сохраняются исходные auto-prepared Hunter's Mark и отдельная bonus-action LongRest power. Fixed uses у неё0; native modifier даёт2/3/4/5/6 на original1/5/9/13/17 → actual1/5/7/9/11. На12 — шесть. Это не количество от WIS/PB.

Сила оплачивает одно применение собственного пула, затем запускает существующий `CastNoCost(HuntersMark, 0, ...)`. Нового списания Spell Points/ячеек или отдельного возврата модуль не добавляет. Journal проверяет начало до исходного ExecuteImpl и допускает только один child cast. Важная защита: child CastNoCost наследует параметры, включая UsablePower родителя, но не считается второй активацией силы — последнее доступное применение не блокируется повторной проверкой уже исчерпанного пула.

Урон от нескольких атак, critical doubling, условия цели, bonus-action timing, перенос метки и native recovery сохраняются. Дополнительный `EnableOneDndHuntersMarkSpell` учтён: исходный2014 SameAsBaseDamage и2024 Force-вариант не подменяются новой системой. Их runtime-поведение требует сравнения в игре.

`Relentless Hunter13→9` разделён только внутри включённого прототипа:

- Основной флагOFF: обычная концентрация, защиты от damage checks нет.
- ОсновнойON, AsNoConcentrationOFF: концентрация требуется, damage checks защищены только для Hunter's Mark.
- ОбаON: Hunter's Mark не требует концентрации; параллельная «защита проверок» не активируется.
- Один AsNoConcentration без основного флага ничего не выдаёт.

Это исправляет пересечение двух интерпретаций в исходном callback UB; при выключенном прототипе оставлен исходный путь обычного Ranger. Другие concentration spells сохраняют собственное требование.

`Precise Hunter17→11` использует отдельный scoped modifier: преимущество только по цели с `ConditionMarkedByHunter`, созданным этим владельцем. Чужая метка, просто наличие метки где-то в бою или метка на самом атакующем не дают универсальное преимущество.

Отмена после native оплаты, контрзаклинания, очереди действий, концентрация старого активного эффекта при получении нового feature и переподключение метки после загрузки не исполнены в Unity. Journal не является общим откатом action economy или всей игры. После исключения в начавшейся finalization требуется отмена/загрузка тестового сохранения.

## Enemy/terrain, Feral Senses и Foe Slayer

В legacy сохраняются три terrain-выбора original1/6/10 → actual1/6/8 и три enemy-выбора original1/6/14 →1/6/10. Custom pools клонируются отдельно по owner/original event/child path; тип, число points и поведение сохраняются. Теги назначаются до группировки custom UI, чтобы однотипные награды и feat-origin choices не сливались. Повторный выбор уже известной invocation, чужой тип пула и незавершённая награда отклоняются. Back/Cancel удаляет только собственные временные origins.

Сохраняются доступные installed UB эффекты preferred enemy/terrain — damage, tracking, exploration, skill/terrain affinity и связанные исходные proficiency effects; новые языки или дополнительные tabletop-бонусы не выдумываются. При Favored Enemy2024 исходный switch UB убирает и legacy terrain, и enemy pools. Прототип не возвращает их автоматически; Deft/Roving/Expertise — отдельные настройки. Дополнительно исключаются остаточные raw legacy choices, если revised-режим или уже имеющийся custom equivalent иначе дали бы двойную награду.

Feral Senses18→12: legacy — `DetectInvisibility`,2024 — `Blindsight`, исходная дальность6. Это разные sense definitions.

Foe Slayer20→12 сохраняет выбранную версию. Legacy установленного UB добавляет реальный WIS modifier к weapon damage через `IModifyWeaponAttackMode`; отдельного once-per-turn ограничения в этом обработчике нет, и это не полная реализация tabletop-формулировки. Прототип сохраняет этот исходник. В2024 меняется die Hunter's Mark на D10; прямой Ranger20 guard переведён на effective20/actual12. Автоматической выдачи обоих Foe Slayer нет. Native ограничения оружия/частоты других эффектов не переписываются.

Tireless10→8: исходный temp HP `1d8 + ceil(effective Ranger/2)`, на12 —1d8+10; uses по реальному WIS. Nature Shroud14→10 либо legacy Vanish: сохранена исходная bonus-action invisibility, WIS/long-rest uses и duration до начала следующего хода. Primeval Awareness3 сохраняется либо удаляется только выбранным флагом. Great Weapon/Protection в AnyFightingStyle2024 и штатный Weapon Mastery сохраняются.

## Покрытие подклассов

Выборка основана на `SubclassChoiceRangerArchetypes` и включённых регистрациях UB, а не на предположении, что все имена включены. Hidden/disabled не включаются автоматически. В журнал записываются реально обработанные archetypes. Для всех четырёх official и восьми UB доступны milestones **3/7/11/15 → actual3/6/8/10**; иных class-specific subclass events18–20 текущие источники не добавляют. Базовые Ranger18/19/20 сохранены отдельно.

| ID | Источник | Основные сохранённые механики | Адаптация / предел подтверждения |
|---|---|---|---|
| RangerHunter | official | Hunter's Prey, Defensive Tactics, Multiattack choice, Superior Hunter's Defense | Все exclusion choices и порядок сохраняются; фактический GUI/бой не запускались |
| RangerMarksman | official | Tool choice, Reaction Shot, Stepback/Recycler, Fast Aim, Extra Attack15 | Milestones/действия/ресурсы исходника; не проверено в игре |
| RangerShadowTamer | official | Tunnel Wisdom, Dark Slayer, Rope Grapple/Know Darkness, Swift Retaliation, At Home in the Dark | Исходные ограничения тьмы, реакции и защита сохранены; не проверено в игре |
| RangerSwiftBlade | official | Quick Step/Blade Dance, Battle Focus, Extra Attack11, Uncatchable | Attack grants и powers на нужных milestones; не проверено в игре |
| RangerArcanist | UB, если включён | Arcane Detonation/Mark, auto-spells, Arcane Pulse и upgrades | Class-rank damage и ownership через общий scoped Ranger-dispatch; не проверено в игре |
| RangerFeyWanderer | UB, если включён | Dreadful Strikes, Otherworldly Glamour, Beguiling Twist, Fey Reinforcements, Misty Wanderer | Прямые11/15 predicates адаптированы; summons/телепортация требуют игрового прогона |
| RangerGloomStalker | UB, если включён | Dread Ambusher, Umbral Sight, Iron Mind, Stalker's Flurry, Shadowy Dodge | WIS/первый ход/оружие/реакции исходника сохранены; прямого Ranger-level scaler в этих callbacks нет |
| RangerHellWalker | UB, если включён | Cursed Tongue, Damming Strike, Firebolt, Burning Constitution, Mark of the Dammed, Fiendish Spawn | Class-rank dice через Ranger-dispatch; fiends/их определения/длительность/общий summon pool сохранены |
| RangerLightBearer | UB, если включён | Blessed Warrior/Life Bringer, Light, Blessed Glow, Angelic Form, Warding Light | Damage11, temp HP и HealingPool получают effective Ranger; WIS/PB остаются реальными |
| RangerSkyWarrior | UB, если включён | Aerial Agility, Gift of the Wind, Ghostly Howl/Intangible Form, Death from Above, Cloud Dance | Исходные полёт/скрытность/ограничения/ресурсы сохранены; не проверено в игре |
| RangerSurvivalist | UB, если включён | Disabling Strike, Wandering Outcast, Analytical Mind, Improved Disabling Strike, Blessing Wilderness | LateLoad Executioner сохраняется; его прямой original11 predicate переведён на actual8 |
| RangerWildMaster | UB, если включён | Bear/Eagle/Wolf companion, Advanced Training7, True Expertise11, Kill Command15 | HP/class rank и формы11 адаптированы; живое обновление старого summon не реализовано — нужен dismiss/resummon |

Ни одна строка не означает пройденную проверку Unity. Полнота source events/definitions и наличие scoped callbacks проверены отдельно от реального исполнения их эффектов.

### Спутники и summon state

Wild Master использует исходные creature definitions03/11: новые общие monsters не создаются и существующие не редактируются. Форма11 выбирается с effective11, то есть actual8. Классовые HP-условия получают effective Ranger через существующий SourceClassLevel path. Native five-fold HP bonus и дополнительный three-fold bear bonus сохранены: при исходном base HP5 на effective20 это105 для wolf/eagle и165 для bear до иных модификаторов. Это проверка формулы исходника, не измерение в игре.

Armor/attack/damage используют исходные **реальные PB + WIS**, True Expertise и Kill Command — исходного владельца/команды/PB. Martial levels не добавляют Ranger class HP-бонус; рост реального PB при multiclass, напротив, остаётся законным. Shared pool, action/bonus-action costs, длительность UntilLongRest и source-owner GUID не меняются.

Новый вызов Wild Master блокируется до оплаты, если уже есть другой активный companion effect; текущий только что созданный action effect исключён из проверки. Повышение уровня с активным Wild Master требует убрать его/дождаться штатного окончания на long rest и затем вызвать заново. Скрытого удаления, бесплатного resummon или подмены creature asset нет. Это ограничение даёт корректные новый class rank/форму/PB/WIS при следующем исходном summon, но не реализует живое обновление уже стоящего существа.

Fey Reinforcements и Fiendish Spawn сохраняют свои исходные определения, ресурсы, длительности и правила. Общего глобального переписывания summons нет. Перед respec требуется убрать все принадлежащие Ranger summons, включая spell summons: это предотвращает очевидное сохранение старого владельца/клятвы после замены героя. GUID/condition lifetime, переход между локациями, смерть спутника, команды и загрузка ещё требуют Unity.

## Multiclass, respec и смена правил

Допустимы Ranger12/Fighter8, Ranger12/Rogue8, Ranger8/Fighter6/Rogue6 с немагическими подклассами и законными DEX/WIS/иными исходными требованиями. Сохраняются общий cap20, пользовательский max3, реальные HP/PB/история. Ranger13 запрещён только при включённом прототипе. Если после12 нет разрешённой альтернативы, advancement блокируется с конкретной причиной в UB log и диагностикой при входе в level-up; требования не ослабляются.

Ranger + другой caster (включая Paladin, Warlock, magical martial archetypes, нерacial casting feats/styles) заблокирован на выборе и перед finalization. Все предыдущие caster guards сохранены. Race-origin независимые способности не становятся вкладом в Ranger casting. Auto-prepared subclass spells принадлежат Ranger и не считаются второй магической прогрессией.

`EnableRespecAction=true` остаётся пользовательским значением. До старта respec требуется завершить Hunter's Mark и убрать owned summons; проверка не очищает старого героя. Успешный штатный перенос TagEffect дополнительно исключает self-owned Ranger/Mark conditions; внешние условия и другие классы сохраняют прежний путь. Пересоздание выбранных enemy/terrain, known/replacement origins, expertise, powers, feats, концентрации и ресурсов требует ручного теста. Штатный reset ресурсов при respec UB не переписан; защита от всех способов эксплуатации повторного respec не доказана.

Инициализация одноразовая, включая исходноеOFF. Все13 Ranger switches, общий Hunter's Mark spell switch, prepared-table/ritual switches, ASI/feat/mastery и Ranger custom-pool rebuild откладываются после активации до перезапуска. Попытка смены фиксирует требование restart, даже если вернуть флаг обратно. Это не позволяет повторно наложить несжатые definitions поверх обёрток. Значимые настройки и состав archetypes сверяются перед повышением/finalization/магическими действиями.

## Автоматическая проверка

Итоговый прогон: **5523 успешных проверок**, из них **3679** прежних и **1844** новых. Release Workflow: **0 ошибок / 0 предупреждений**. `git diff --check` проходит. Выходная DLL существует и не пуста. Повторная установка зависимостей не понадобилась; DLL, игровые assemblies, saves и пользовательские settings не включаются в Git.

Behavioral checks исполняют production mapping, transformer, known/replacement planner, `RangerSpellLearningState`, custom invocation validation/origins и free-mark/grant journals с managed doubles. Проверяются полные source inventories, промежуточные события, независимые награды, half casting, final budgets, source-list/circle rejection, замены ранее выученного spell внутри одного повышения, optional skips, Back/stale choices, one-time/disabled paths, Mark thresholds и взаимоисключающие concentration modes.

Модель256 комбинаций прогрессионных флагов подаётся в настоящий transformer; независимые Feral/Foe варианты также проверяются. Эта модель **не исполняет** native Tabletop2024 switches. Статические source/blueprint inspections и Cecil подтверждают source tables, registration coverage, прямые class-rank adapters, код оплаты/нулевой child slot, порядок native LoseSpell/GrantSpell, Harmony target/argument bindings и наличие guards. Они **не запускают Harmony, GUI, бой, отдых, summons, respec или save/load**. Число checks не является числом игровых сценариев.

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

Windows Release Install не используется. Полный diff проверяется перед коммитом; push выполняется только в development branch. Commit SHA и подтверждение push указываются в итоговом отчёте чата, поскольку документ входит в тот же коммит.

## Точный ручной план

1. Использовать новый тестовый профиль/сохранения и отдельного обычного Ranger для OFF-regression. Зафиксировать версию игры/UB, actual2024 flags, подготовку/Spell Points и список archetypes из log. Включить только новый prototype, перезапустить. Проверить defaultOFF, prerequisite diagnostics и отсутствие автоматической смены прочих user options.
2. Создать Ranger1 с законными DEX/WIS для желаемых martial multiclasses. Последовательно пройти **каждое** повышение до12. Проверять actual class/history/HP/PB, milestones таблицы, пять ASI и все отдельные feats. На12 должны быть раздельные награды original18/19; выбирать обе, затем испытать Back/Cancel/Reset и повторный вход без двойной выдачи.
3. Отдельными новыми героями после полных перезапусков проверить четыре casting/table режима (начало1/2 × legacy/2024 known) и отдельно slots/points. Сверить таблицу, отсутствие casting на1 в legacy, круг3 на7, круг4 на9, круг5 на11/12, final64 points и обычные4/3/3/3/2 slots. Нет кругов6–9 и133 points. Проверить оплату каждого circle, upcast, временные slots, исчерпание и long-rest recovery без двух независимых ресурсов.
4. На actual7,9,11 и12 пройти все original spell events по порядку. Выучить spell в раннем событии, заменить в следующем того же actual level; отменить/переделать ранний выбор и убедиться, что поздний стал недействительным. На7 original8 не должен давать circle3, original9 — должен. На12 отдельно проверить replacements18/19/20 и learn19; можно пропускать замену, нельзя получать её новый spell без удаления. Попытаться выбрать дубль/чужой список/будущий круг. Повторить Auto, Back, Reset, Cancel и повторную finalization.
5. Проверить subclass auto-spells по original3/5/9/13/17 (если есть соответствующие группы), самостоятельные cantrip/styles и long-rest UI. 2024 table должна дать15 ordinary known на12, а не ежедневную WholeList-подготовку. Druidic Warrior/Race-origin cantrips сохраняют свой tag и не увеличивают64 points. Нерacial вторая casting progression должна быть недоступна либо отклонена до мутации.
6. **Legacy enemy/terrain:** на1/6/8/10 пройти все независимые selections, сверить три terrains и три enemies, выбрать разные варианты и проверить native damage/exploration/skill/terrain effects только в подходящих условиях. Перепроверить ранее выбранные типы после Back/Reset и после multiclass. При revised Favored Enemy нет автоматической выдачи обеих legacy групп; отдельные Deft/Roving/Expertise проверять своими flags.
7. **Mark2024 PRIMARY:** проверять free pool на actual1/5/7/9/11/12:2/3/4/5/6/6. Использовать **последнее** применение: оно должно запускать один child spell. До/после записать free remaining и Spell Points: free cast расходует только один power use, не Spell Points. Отменять выбор цели, повторно нажимать/подтверждать, тестировать недопустимую/умершую цель, silence, отсутствие бонусного действия и исчерпание. Повторный callback не должен давать второй эффект. Платный Mark должен расходовать свой slot/points обычным путём. Проверить long-rest reset отдельно от остальной магии.
8. На обычных врагах проверить Mark damage с одним/двумя/дополнительными subclass attacks, melee/ranged/reactions и критом; перенос метки после смерти цели и новый выбор цели. Только правильная цель получает bonus damage, старые conditions прекращаются своим native путём. Проверить2014 SameAsBaseDamage и отдельный2024 Force flag. Сверять журнал с обычным Ranger соответствующего original level; не подставлять tabletop-ожидание вместо установленного UB.
9. **Relentless:** отдельные новые герои/перезапуски для всех сочетаний двух flags. До9 Mark требует обычную концентрацию. После9 protection-режим сохраняет concentration и защищает только damage checks Mark; no-concentration режим допускает второй concentration spell и не выдаёт параллельно protection branch. ОсновнойOFF/вспомогательныйON ничего не меняет. Проверить окончание/перекаст старой метки при получении feature и применение других concentration spells.
10. **Precise/Foe:** на11 преимущество только против своей marked target. Другая цель, чужая метка и marked attacker не дают универсального преимущества. На12 проверить2024 D10 только для Hunter's Mark, а legacy — исходный WIS weapon-damage modifier. Раздельно проверить обе Foe и обе Feral настройки, их независимые сочетания; старый и новый feature одновременно не выдаются. Feral сравнить на невидимом враге в радиусе/за радиусом и в условиях зрения, где DetectInvisibility и Blindsight различаются.
11. **Roving/Expertise/Tireless/Nature:** Deft2 languages/expertise, Roving6→6, Expertise9→7, Tireless10→8 и Nature/Vanish14→10. Проверить ограничения движения/брони исходника, число независимых choices, WIS-based uses без виртуального PB, temp HP на8 и12 (d8+6/d8+10), исчерпание и отдых. Primeval Awareness отсутствует только при remove flag. Any Fighting Style и Weapon Mastery проверить вместе с caster-style/Race origin и обычным melee/ranged combat.
12. Для **каждого из12 archetypes** создать отдельного героя либо выполнить контролируемый respec нового тестового героя после окончания эффектов. На3/6/8/10 проверить все choices, subclass spells, attacks, reactions, stealth, defence и resources из матрицы. Особо: Hunter exclusion UI; Gloom first-turn/WIS/Flurry/Dodge; Arcanist damage upgrades; Fey Dreadful Strikes D6 с8, Fey summon и Misty passenger с10; Light Bearer healing/temp HP; Survivalist Executioner predicate с8.
13. **Wild Master:** отдельно Bear/Eagle/Wolf на3,6,8,12. Сверить creature03 до8 и11 с8, HP/class bonuses, grouped attacks, native action costs, Advanced Training6, True Expertise8 и Kill Command10. Снять реальные PB/WIS bonuses, ownership GUID и источник каждого persistent condition. Попытка второго summon до удаления первого блокируется без дополнительного расхода. Проверить смерть, dismiss, long rest, смену локации и повторный summon. Перед level-up убрать/дождаться окончания питомца; после повышения вызвать заново и сверить новые форму/HP/PB/WIS. Старое живое существо автоматически не обновляется — это ограничение.
14. **Другие summons:** у Hell Walker на10 проверить выбор исходных fiends, общий LongRest pool, action и1-minute duration; у Fey — исходный power/spell pipeline, концентрацию/uses и duration. Respec с owned summon должен иметь раннюю диагностику; после dismiss/окончания не остаётся второго существа или старого GUID. Проверить загрузку с живым summon на отдельном файле без respec.
15. **Multiclass:** пройти Ranger12/Fighter8, Ranger12/Rogue8, Ranger8/Fighter6/Rogue6 до20. Martial levels не увеличивают class caster points/круг/Mark uses/Ranger HP scaling спутника, но реальный PB/HP растёт нормально. С питомцем сначала dismiss/re-summon после повышения. Ranger13 и character21 запрещены, max3/DEX/WIS сохранены. Попытаться взять каждого другого caster, magical martial subclass и нерacial casting feat/style; отказ должен быть до finalization. Отдельно герой без legal alternatives на12: конкретная cap/prerequisite диагностика, без обхода требований и пустого level-up.
16. **Respec:** с потраченными resources/выбранными terrains/enemies/known spells/feats повторить Cancel/Back/Reset, затем успешную смену archetype и немагического класса. С active Mark/summons — отказ до создания replacement. После завершения эффектов — штатный respec, отсутствие старых preferred enemies, expertise, spell/replacement origins, Hunter's Mark counters, subclass powers, conditions и pending pools. Проверить fresh source-owner GUID, ресурсы новой конфигурации и несколько повторов. Не считать source reset ресурсов UB доказанной защитой от всех respec exploits.
17. Save/load на actual6/7/9/11/12 и multiclass20, со spent/free resources, активной концентрацией/меткой и отдельно companion. Сверить known/auto spells, origins, used/max, commands, GUID и условия после загрузки. При исключении finalization/action отменить/перезагрузить, не продолжать сохранять частичное состояние. Смена правил с возвратом флага всё равно требует restart. OFF-regression выполнять только с обычным контрольным героем после полного restart; проверить предыдущие классы отдельно в игре.

## Неподтверждённое и ограничения

Нет исполнения Solasta: combat, GUI/Harmony scheduling, rest callbacks, reload и respec не подтверждены. Не реализовано живое обновление текущего Wild Master при class/PB/WIS changes — используется dismiss/resummon guard. Не заявлены WholeList daily preparation, shared multicaster, поддержка неизвестных сторонних casting definitions, автоматическая конвертация старых героев или общий atomic rollback игрового мира. Повторный respec использует штатную замену героя/ресурсов UB; возможные exploits и stale state требуют ручной проверки.

Классификация — **PARTIALLY READY**. Следующий шаг — контролируемый прогон Mark/замен/спутников и только затем остальных боевых/сохранительных сценариев. Для обычного прохождения и основных сохранений прототип пока не рекомендован. После Ranger работа останавливается.

## Изменённые файлы

- `Documentation/RangerProgressionPrototype.md`
- `SolastaUnfinishedBusiness/Api/Helpers/MulticlassInOutRulesHelper.cs`
- `SolastaUnfinishedBusiness/Behaviors/ModifyPowerPoolAmount.cs`
- `SolastaUnfinishedBusiness/CustomUI/MulticlassGameUi.cs`
- `SolastaUnfinishedBusiness/Displays/ClassesDisplay.cs`
- `SolastaUnfinishedBusiness/FightingStyles/Executioner.cs`
- `SolastaUnfinishedBusiness/Models/2024RangerContext.cs`
- `SolastaUnfinishedBusiness/Models/BootContext.cs`
- `SolastaUnfinishedBusiness/Models/Level20Context.cs`
- `SolastaUnfinishedBusiness/Models/RangerCompanionContext.cs`
- `SolastaUnfinishedBusiness/Models/RangerInvocationContext.cs`
- `SolastaUnfinishedBusiness/Models/RangerInvocationPlan.cs`
- `SolastaUnfinishedBusiness/Models/RangerMarkContext.cs`
- `SolastaUnfinishedBusiness/Models/RangerProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/RangerProgressionRules.cs`
- `SolastaUnfinishedBusiness/Models/RangerSpellLearningContext.cs`
- `SolastaUnfinishedBusiness/Models/RangerSpellLearningState.cs`
- `SolastaUnfinishedBusiness/Models/RangerSpellcastingContext.cs`
- `SolastaUnfinishedBusiness/Models/SharedSpellsContext.cs`
- `SolastaUnfinishedBusiness/Models/SpellPointsContext.cs`
- `SolastaUnfinishedBusiness/Models/ToolsContext.cs`
- `SolastaUnfinishedBusiness/Models/WizardProgressionContext.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetActorPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterHeroPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetEffectPowerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetImplementationManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Settings.cs`
- `SolastaUnfinishedBusiness/Subclasses/RangerFeyWanderer.cs`
- `SolastaUnfinishedBusiness/Subclasses/RangerLightBearer.cs`
- `SolastaUnfinishedBusiness/Subclasses/RangerWildMaster.cs`
- `Tests/FighterProgression/FighterProgression.Tests.csproj`
- `Tests/FighterProgression/Program.cs`
- `Tests/FighterProgression/RangerTests.cs`
