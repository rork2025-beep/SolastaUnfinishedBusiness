# Inventor 20→12 — Stage 12

Статус: **PARTIALLY READY**. Реализован opt-in прототип; допустимы контролируемые игровые проверки на новых героях в отдельных сохранениях. Сборка и автономные проверки не подтверждают работу Unity, Harmony, инвентаря, GUI, боя, отдыха, respec или save/load.

## Baseline и границы

Checkout `/workspace/SolastaUnfinishedBusiness`, ветка `feature/progression-20-to-12`. Проверенный baseline Stage 11: `c3d023b82c6013fc455c0e9893f083dae5b2e2ee`, SDK 9.0.320, **5 523** проверки; Release Workflow **0 ошибок / 0 предупреждений**. Исходных failures нет. Зависимости уже установлены; повторная установка не потребовалась.

Сохранены предыдущие адаптеры Fighter, Barbarian, Monk, Rogue, Wizard, Sorcerer, Warlock, Bard, Cleric, Druid, Paladin и Ranger. В существующем адаптере финализации Warlock добавлена маршрутизация только списков инфузий Inventor; обычные Warlock-транзакции и Ranger-пулы сохраняют свои обработчики. Общая интеграция нескольких кастеров не реализуется.

Реальные уровни, история классов, HP, PB и INT героя не изменяются. Глобальной подмены GetClassLevel нет. Лимит Inventor — 12, общий лимит — 20; максимальное число классов и требования характеристик остаются в существующих UB guards (целевая настройка MaxAllowedClasses=3).

## Включение и отключение

1. В обычном интерфейсе UB включить Inventor и желаемые Innovations, если они скрыты. Прототип не включает класс или подклассы автоматически.
2. Проверить существующие `EnableLevel20=true`, `EnableMulticlass=true`. Остальные целевые настройки, включая Spell Points, ASI/feats, PCG, Epic Points и `EnableRespecAction=true`, пользователь задаёт самостоятельно: этот код не меняет их и не перезаписывает Settings.xml.
3. UB → Classes → **Inventor 20 to 12 prototype** (`EnableInventorCompressedProgressionPrototype=true`). По умолчанию OFF.
4. Полностью завершить игру и запустить заново. Создать нового тестового героя и отдельное сохранение. Маркер нового Inventor добавляется при создании; непомеченные старые Inventors отклоняются, автоматической миграции нет. Герои выше Inventor12 также не поддерживаются.
5. Для отключения снять флаг и полностью перезапустить игру. Обычные определения Inventor восстанавливаются из исходных UB definitions. Сохранения прототипа при отключённом режиме не использовать.

Прототип запускается после Inventor.Build, регистрации Innovations, LateLoadSpellStoringItem, 2024-переключателей, Spell Points и всех предыдущих progression-модулей. Требуется проверенная привязка нативной финализации custom invocations, установленное WholeList/Prepared/HalfRoundUp колдовство и хотя бы одна активная Innovation; при несовместимости активация отклоняется с диагностикой. Инициализация одноразовая. Смена режима/прогрессионных настроек, списка заклинаний или активных Innovations в текущем сеансе блокирует дальнейшее развитие/классовые действия до полного перезапуска.

## Прогрессия и ресурсы

Таблица описывает базовый Inventor без дополнительных модификаторов. «Подготовка» — добавка исходного уровня к настоящему модификатору INT и нативным affinity-бонусам, минимум одно заклинание. Значения slots указаны для режима обычных ячеек; при Spell Points UB использует собственные таблицы отображения доступных кругов.

| Реальный уровень | Исходные события | Основные события | Ячейки кругов 1–5 | SP | Добавка подготовки | Выбираемые cantrips | Инфузии известны / активны | Необязательные замены на этом повышении |
|---|---|---|---|---:|---:|---:|---|---:|
| 1 | 1 | Колдовство, Magical Tinkering, ритуалы, исходные профили | 2/0/0/0/0 | 4 | 0 | 2 | 0/0 | 0 |
| 2 | 2 | Infusions: изучить 4, пул 2 | 2/0/0/0/0 | 4 | 1 | 2 | 4/2 | 0 |
| 3 | 3 | Innovation, Right Tool for the Job | 3/0/0/0/0 | 6 | 1 | 2 | 4/2 | 1 |
| 4 | 4 | ASI4 | 3/0/0/0/0 | 6 | 2 | 2 | 4/2 | 1 |
| 5 | 5 | Innovation5 | 4/2/0/0/0 | 14 | 2 | 2 | 4/2 | 1 |
| 6 | 6–7 | Изучить 2, пул +1, Tool Expertise, Flash of Genius | 4/3/0/0/0 | 17 | 3 | 2 | 6/3 | 2 |
| 7 | 8–9 | ASI8, Innovation9 | 4/3/2/0/0 | 27 | 4 | 2 | 6/3 | 2 |
| 8 | 10–11 | Изучить 2, пул +1, Magic Item Adept, Spell-Storing Item | 4/3/3/0/0 | 32 | 5 | 3 | 8/4 | 2 |
| 9 | 12–13 | ASI12 | 4/3/3/1/0 | 38 | 6 | 3 | 8/4 | 2 |
| 10 | 14–15 | Изучить 2, пул +1, Savant, Innovation15 | 4/3/3/2/0 | 44 | 7 | 4 | 10/5 | 2 |
| 11 | 16–17 | ASI16, последние Innovation bonus spells | 4/3/3/3/1 | 57 | 8 | 4 | 10/5 | 2 |
| 12 | 18–20 | Изучить 2, пул +1, ASI19, Soul of Artifice20 | 4/3/3/3/2 | 64 | 10 | 4 | 12/6 | 3 |

Сохраняются все исходные feature events, включая повторные награды; wrapper union сохраняет порядок исходного уровня и индекс события. Независимые обычные point pools получают собственные origin tags. Исходные ASI4/8/12/16/19 доступны на реальных 4/7/9/11/12. При целевых настройках ASI+feat и обеих опциях дополнительных feats сохраняются пять ASI, пять сопутствующих feats и пять дополнительных классовых feats; расовый/первый feat остаётся отдельным.

WholeList/Prepared, INT, собственный spell list и Prepared rituals остаются нативными. Нет spellbook, scribing или системы Sorcerer known spells. Independent cantrip progression — исходные 2, затем 3 на10→8 и 4 на14→10; бонусный Light остаётся отдельной исходной наградой. Innovation auto-prepared группы 3/5/9/13/17 проецируются в 3/5/7/9/11. Автоподготовленные заклинания не образуют второй классовый repertoire и не расходуют обычный лимит подготовки.

HalfRoundUp даёт вклад кастера ceil(исходный Inventor/2): на реальном12 — caster10, **64 SP**, максимум круг5, не full caster20/133 SP. Martial-уровни не увеличивают этот вклад. Стоимость заклинаний 2/3/5/6/7 SP, upcasting и long rest остаются исходными UB. В установленной реализации нет Inventor-specific 2024 ruleset; общая таблица подготовки 2024 обслуживает другие классы, Inventor сохраняет INT + floor(исходный уровень/2).

## Инфузии и предметная безопасность

Изучение: 2:4; 6/10/14/18:+2. Отдельный optional unlearn на каждом исходном уровне 3–20: **18** бюджетов. Это custom pools `InvocationPoolTypeCustom.Pools.Infusion`, а не обычные Warlock point pools.

Планировщик получает расписание из финальных зарегистрированных custom pools. Клонируются PoolType, Points, IsUnlearn и custom subfeatures. Tags содержат исходный уровень: исходные18/19/20 никогда не объединяются. Внутри исходного события UI сначала предлагает необязательное забывание, затем обязательное изучение и оплаченную замену. Пропуск забывания не создаёт бесплатную награду. На уровнях без базового изучения создаётся нулевой learn step: один выбранный unlearn увеличивает только его бюджет.

Виртуальный список инфузий отражает результат предыдущих исходных событий этого повышения. Инфузию, изученную на18, можно заменить на19, затем замену — на20. Требования проверяются на исходном событии, включая native validators; требование14 не выполняется на13. Back/Reset очищает зависимые последующие события; Cancel удаляет черновые выборы. Перед финализацией повторно проверяются все обязательные бюджеты, дубли, исходные требования и происхождение. Подготовленная транзакция применяет события по порядку один раз. При исключении восстанавливается снимок инфузий; остальной нативный level-up не транзакционен, требуется Cancel/reload.

В исходниках/blueprints обнаружены **35** выборов инфузий: 10 основных и 25 реплик. Дополнительные elemental/resistance subpowers принадлежат их master infusion. Сохраняются исходные prerequisites1/6/10/14, фильтры предметов/экипировки, условия, attunement и нативные заряды. Улучшения Enhance Focus/Defense/Weapon исходного10 срабатывают на реальном8.

Активный пул: исходные2=2; 6=3; 10=4; 14=5; 18=6. Использованный ресурс и максимальный пул учитываются отдельно от известных инфузий. Armor на исходном9→реальном7 добавляет **два к активному пулу**, не к изученным: на реальном12 у Armor получается 8 вместо базовых6. Нативные ограничения Armorer-предметов остаются.

Дополнительные guards перед использованием: текущая инфузия должна оставаться изученной, нужны оставшиеся использования пула, свободная активная ёмкость и отсутствие второй копии той же master infusion, включая разные elemental-варианты. Повторный callback одного родительского действия не запускает создание ещё раз. Дочерний CastNoCost не считается повторным использованием родительского power.

**Консервативное ограничение:** перед любым level-up/retraining и respec отменить активные инфузии/созданные Spell-Storing Items и распустить конструкции, включая tiny cannon conditions. Учитываются эффекты владельца по GUID и tracked item/property GUIDs, даже если вещь находится у союзника. Повторное создание SSI с ещё активным старым предметом отклоняется до нативного удаления старого предмета. Нет автоматического удаления вещей, глобального inventory rollback или исправления чужого инвентаря. Очистка выполняется самим игроком штатным UB действием; отмена инфузий доступна даже при изменившихся настройках. Если штатный UI не позволяет очистить конкретный persistent effect, дальнейшее развитие/respec остаётся запрещённым: загрузить тестовое сохранение до создания предмета. Это существенный оставшийся риск, не обещание полного item lifecycle.

## Spell-Storing Item и базовые способности

Оба базовых компонента SSI и созданные LateLoad Innovation options доступны с исходного11→реального8. Сначала строятся все dynamic powers; затем выполняется сжатие. Сохраняются Action spells кругов1/2 из Inventor list и подходящие Innovation auto-spells исходных групп3/5, shared pool, оригинальное действие создания, источник/класс, DC/attack от создателя и fixed **6 зарядов** предмета. INT×2 charges в исходном UB — TODO, этот этап его не подменяет. Заряд предмета не становится бесплатной ячейкой/SP. После исчерпания нативный предмет уничтожается исходным механизмом. Общая unique-effect group расширена на Innovation-only options, чтобы они не обходили общую уникальность.

Tool Expertise6→6 сохраняет все семь исходных записей ToolOrExpertise. Flash of Genius7→6 сохраняет aura/реакцию, source GUID, реальные INT bonus/uses, проверки расстояния/видимости, long rest и расход после подтверждения. Добавлена проверка совместимости текущего сеанса перед реакцией.

Magic Item Adept10→8 и Savant14→10 сохраняют crafting/use affinities и по одному дополнительному attunement slot. Без других модификаторов лимит3→4→5; Soul of Artifice сам шестой слот в этом UB не добавляет. Опция IncreaseMaxAttunedItems, если включена пользователем, продолжает опираться на реальный общий уровень.

Soul of Artifice20→12: подтверждённая реакция стабилизации использует исходный уровень Inventor20 и восстанавливает20 HP. Сохраняются исходный одноразовый long-rest ресурс и native reaction flow. Бонус saving throw считается по фактически настроенным предметам, не по effective level. Проверка отмены/повторной реакции требует игры.

## Матрица Innovations

Inventor — UB-класс, официальных игровых Innovations для него нет. Покрываются реально зарегистрированные и видимые подклассы; скрытые варианты не включаются автоматически. Все шесть реализаций ниже присутствуют в текущем источнике и проходят fixture inventory checks. При загрузке журнал сообщает фактически обнаруженные активные имена.

| Innovation | Исходные → реальные milestones | Сохранённые механики | Основные проверки в игре |
|---|---|---|---|
| Alchemy | 3/5/9/11/15 → 3/5/7/8/10 | Bombs, Fast Hands, elemental/advanced bombs, Overcharge/Extra/Master, pool/refund, bonus spells/SSI | Расход/возврат пула, short rest, conversion из ячеек/SP, урон и source INT |
| Armor | 3/5/9/11/15 → 3/5/7/8/10 | Armor modes, Defensive Field, Extra Attack, Armor Modification(+2 active), Perfected Armor, spells/SSI | Две модели, предметные ограничения, class-rank temp HP, снятие модификаций, respec guard |
| Artillerist | 3/5/9/11/15 → 3/5/7/8/10 | Cannon types/summon/tiny modes, Arcane Firearm, Explosive Cannon, Fortified Position, spells/SSI | Лимит1→2 на реальном10, dismiss/command, duration, costs, destruction/rest, owner GUID, source-class HP/dice |
| Vitriolist | 3/5/9/11/15 → 3/5/7/8/10 | Mixture/Infusion/Arsenal/Paragon, original-level dice variants, spells/SSI | Thresholds7/14/18, riders/conditions, conversion и resource recovery |
| Vivisectionist | 3/5/9/11/15 → 3/5/7/8/10 | Surgical damage, Emergency Cure upgrades, class-rank dice, spells/SSI | Ограничения атак, лечение/charges, реакции и recovery |
| Weapon | 3/5/9/11/15 → 3/5/7/8/10 | Battle Ready, Steel Defender/Improved Defender, Extra Attack, Arcane Jolt, spells/SSI | HP/ремонт/commands/PB/INT construct, Arcane Jolt2d6→4d6 на реальном10, cancel, owner GUID |

Адресный resolver владельца действует только для `InventorConstruct`, не для всех summons. SourceClassLevel-условия HP и class-level power dice используют исходный уровень Inventor; PB, INT, spell DC, source GUID и исходная action economy сохраняются. Нативно скопированный CharacterLevel-атрибут конструкта не переписывается и не используется как замена классовому рангу. У Arcane Jolt отдельно скорректировано старое CharacterLevel-based поле dice для этого единственного power: martial уровни не увеличивают классовый rank.

Существующие upstream TODO остаются: например, Arcane Jolt в Weapon не имеет полной healing/Steel Defender trigger части, некоторые инфузии упрощают attunement/concentration. Этот этап сохраняет установленный вариант, не обещает недостающие RAW-механики.

## Multiclass, respec и сохранения

Разрешены подходящие некостующие martial combinations: Inventor12/Fighter8, Inventor12/Rogue8, Inventor8/Fighter6/Rogue6. Учитываются реальные INT и остальные multiclass prerequisites. Wizard, Sorcerer, Warlock и другие вторые casting classes/subclasses/repertoires блокируются, независимо от того, сжата ли вторая прогрессия. Расовые spells отдельно допускаются. Кастующие feats/подклассы, создающие второй repertoire, отклоняются. Innovation auto-spells не являются вторым casting class.

Три независимых guards закрывают отображение доступных классов, AssignClassLevel и AddClassLevel. При Inventor12 без законной альтернативы дальнейшее повышение отклоняется с сообщением и логом. Требования характеристик не ослабляются; если INT/другие требования и максимум классов создают deadlock, нужен новый подходящий тестовый герой/законное изменение билдом, а не скрытый bypass. Inventor13 недоступен.

Перед respec дополнительно снять настройку предметов, закончить concentration и временные Innovation/class effects. Старые self-owned Inventor/Innovation conditions не переносятся на нового героя; сохраняется исходный механизм очистки старого героя. Новый герой строит spells, infused choices, resources и subclass заново; charge/refund/save-перенос между режимами не реализуется. Выданные союзникам предметы и constructs особенно рискованны — guard не пытается их принудительно исправлять. Повторный respec/cancel, attunement, Spell Points, pending choices и восстановление после исключения требуют Unity-проверки. Миграции старых героев/сохранений нет.

## Автоматическая проверка

Последний полный прогон: **6209 checks passed** (включая прежние5523; новое покрытие686). SDK9.0.320. Release Workflow: **0 ошибок / 0 предупреждений**. Финальное число и команды также приводятся в отчёте этапа.

Выполняются production mapping/definition planner на управляемых doubles с blueprint fixtures всех шести Innovations, production infusion schedule/WarlockInvocationTransaction для Inventor, chronological replacements18/19/20, prerequisite13/14, mandatory/optional budgets, дубли, rollback, one-time journal, cantrip/slot projections, INT preparation, active capacity guards и предыдущие regression suites. Capacity reference rule отдельно помечен: реальный лимит ресурса по-прежнему рассчитывает native modified pool.

Отдельно помечены **Cecil metadata / blueprint / source metadata**: наличие Harmony bindings и проверяемых native call sites, source item filters, 35 infusion prerequisite definitions/shared-pool power types, SSI/Innovation inventory, Tool Expertise, Flash of Genius, Soul, construct owner dispatch, class cap и multicaster boundaries. Проверка наличия кода не исполняет Harmony, реакцию, native TrainInvocations, инвентарь или UI.

Команды из корня checkout:

```bash
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_ROOT=/workspace/.dotnet DOTNET_CLI_HOME=/workspace/.setup-solasta/dotnet-home
export NUGET_PACKAGES=/workspace/.setup-solasta/nuget DOTNET_CLI_TELEMETRY_OPTOUT=1
export SolastaInstallDir=/workspace/SolastaUnfinishedBusiness/SolastaFiles
dotnet --list-sdks
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
git diff --check
```

Linux Release Install/packaging не применяется. DLL — только локальный результат сборки, без добавления в Git.

## Обязательный ручной чеклист

1. **Контроль OFF.** Полный restart с флагом OFF, новый обычный Inventor. Проверить исходные уровни6/7/10/11/14/15/18/20 и обычные ресурсы. Не открывать compressed save в этом режиме.
2. **Старт ON.** Проверить целевые пользовательские настройки, включить флаг, restart. Создать нового Inventor с известным INT; записать реальные HP/PB/INT/историю, ритуалы, Light и две независимые cantrip choices. Не выбирать повтор Light. Проверить старого непомеченного Inventor: ясный отказ, без конвертации/сохранения.
3. **Все уровни.** Повышать по одному уровню1–12; на каждом сопоставить таблицу выше с журналом выбора, слотами/SP, подготовкой, feats/ASI. Снять отдельные saves перед каждым объединённым повышением. Полные исходные18/19/20 и Soul должны появиться на12; Inventor13 отсутствует.
4. **Инфузии2.** Изучить четыре разных; попытаться повторить уже известную. Активировать две на законных предметах, проверить расход/лимит/экипировку/attunement. Отклонить неподходящий предмет, повтор той же master infusion (включая другой элемент) и третью активную копию; ресурс не расходуется на отказ. Проверить несколько атак/crit/возврат/repeating shot по установленным rules.
5. **Перед6.** С активным предметом у себя и затем у союзника попытаться повыситься: отказ без изменения вещей. Выполнить штатную отмену инфузий и dismiss constructs, повторить повышение. Исходные6 и7 дают два самостоятельных retrain-события и +2 обязательных learned. Проверить Skip, Back, Reset, Cancel и повтор входа; старые будущие выборы исчезают.
6. **Порядок18/19/20.** Перед реальным12 очистить предметы. Исходное18: выбрать два новых и необязательную замену. На19 заменить одну из новых18; на20 заменить результат19. Проверить конечный известный список, три отдельных происхождения/бюджета и отсутствие бесплатных замен. Повторить с полным Skip и с отменой на последнем шаге. Убедиться, что исходное14 требование недоступно на13.
7. **Capacity и enhancement.** На6/8/10/12 максимум3/4/5/6; у Armor после7 — +2. Изученные counts6/8/10/12 от capacity независимы. Проверить +1→+2 Enhance Focus/Defense/Weapon на реальном8, не раньше. Long rest восстанавливает ресурс, но не разрешает превысить число существующих активных эффектов.
8. **Предметный lifecycle.** Сохранить/загрузить героя с инфузией на себе, союзнике и в контейнере. Снять/передать/потерять/отменить предмет. Проверить tracked properties, ownership, отсутствие лишних enchantments и сиротских предметов. При недоступной штатной очистке прекратить тест и вернуться к save до создания; не обходить guard.
9. **SSI8.** Оба компонента и Innovation-specific spells присутствуют. Создать законную Action spell1/2 wand, проверить6 зарядов, DC/attack/INT владельца, расход одного заряда, отсутствие SP расхода/прибавки и уничтожение при0. Проверить отмену до подтверждения, повтор родительского действия, попытку второго item при активном первом, cleanup и создание после long rest. Проверить предмет у союзника и после save/load; нельзя получить spells3+ или чужой список.
10. **Подготовка.** Для INT modifiers0/3/5 проверить max(1,INT+floor(original/2)+affinity), отдельно Innovation auto-spells, ритуалы и смену prepared после отдыха. С общим prepared-table2024 ON после restart Inventor сохраняет свою формулу. На11 доступен круг5, на12 —64 SP; upcast, отмена cast, long rest и race spells не удваивают ресурсы.
11. **Базовые способности.** На6 все семь Tool Expertise и Flash of Genius. Проверить ally range/perception/source GUID, успех/неуспех check/save, подтверждение и Cancel реакции, реальные INT uses и long rest. На8/10 проверить Adept/Savant crafting/use restrictions и дополнительные attunements; отдельным запуском — пользовательский IncreaseMaxAttunedItems.
12. **Soul12.** С разным реальным числом attuned items проверить saving throw bonus. Урон до0: согласие восстанавливает20 HP и тратит один native use, Cancel не тратит его; повторный callback не даёт второго срабатывания, повтор после long rest допустим. В неподходящем контексте реакция не возникает.
13. **Каждая Innovation.** Завести отдельного нового героя для каждой строки матрицы. Проверить3/5/7/8/10 и bonus spell groups3/5/7/9/11; acquisition/replacement chains, restrictions, actions, reactions и recovery. Не считать прохождение одной Innovation подтверждением остальных.
14. **Artillerist.** Проверить все три типа и tiny/full variants. До реального10 только1, на10 —2; попытка третьего подчиняется native limiter без лишнего ресурса/сирот. Проверить command bonus action, dismissal, duration, разрушение, отдых, перенос локации и owner GUID. Сравнить HP/dice при Inventor12 и Inventor12/Fighter8: классовая часть одинакова, реальный PB может различаться.
15. **Weapon/Armor/Alchemy/Vitriolist/Vivisectionist.** Steel Defender HP/repair/commands/summon pool/advanced form, Arcane Jolt once-per-turn и2d6→4d6; Armor mode changes/Defensive Field/добавочные инфузии; все damage/heal/resource tiers остальных Innovations. После очистки повторно призвать construct; проверить отсутствие старых owner conditions.
16. **Multiclass.** С подходящими характеристиками проверить Inventor12/Fighter8, Inventor12/Rogue8, Inventor8/Fighter6/Rogue6. HP/PB/history — реальные; SP/infusion/cannon/Jolt class ranks не растут от martial levels. Отдельно отклонить Wizard/Sorcerer/Warlock, caster subclass и feat с другим repertoire. Auto-prepared Innovation spells не вызывают ложный multicaster отказ.
17. **Deadlock.** На Inventor12 с неподходящими требованиями либо тремя исчерпанными классами проверить явное сообщение, отсутствие пустого level-up и скрытого Inventor13. Не ослаблять атрибуты автоматически.
18. **Respec.** С активными вещами/constructs/attunement/concentration попытаться respec — отказ до изменений. После полной штатной очистки и unattune повторить; проверить Innovation, learned infusions, capacity/used resources, prepared/bonus spells, SP, charges и условия. Несколько циклов respec/Cancel; вещи союзников не должны исчезать от входа в respec. При persistent mode, который невозможно отменить, guard остаётся закрытым — это unresolved runtime limitation.
19. **Сохранения/настройки.** Save/load после каждого проблемного сценария, отменённого level-up и respec; повторный level-up после загрузки. Изменить флаг/правило/list/subclass в текущем сеансе: развитие/классовые действия отвергаются до restart. Cleanup инфузий остаётся доступным. Старые герои и автоматическое переключение compressed/ordinary save не поддерживаются.
20. **Регрессии.** Отдельные старые тестовые saves предыдущих классов: Ranger spell learning/Mark/companions, Paladin Smite, Druid Wild Shape, Cleric Channel Divinity, Bard Magical Secrets, Warlock transactions и остальные class adapters. Само прохождение автономных регрессий не подтверждает их native GUI/бой.

## Риски и независимый UMM patch

Основные непроверенные риски — custom UI→native commands, Back/Cancel и финализация после исключения, item/effect persistence, group uniqueness в Harmony runtime, восстановление charges, cleanup у союзников, constructs/attunement/respec и сериализация новых feature origins. Консервативные guards уменьшают поверхность автоматических мутаций, но могут потребовать возврата к раннему тестовому сохранению. Полной совместимости сохранений/respec нет.

Production planners можно выделить в отдельную библиотеку. Независимый UMM patch пока не готов: адаптер опирается на внутренние UB custom invocation UI, pools, power bundles, runtime owner helpers и порядок late-load. Для отделения нужны явные hooks после late-load и в custom invocation UI/finalizer, проверка версий UB/Harmony и собственные runtime tests. Shared multicaster integration в этот этап не входит.

Изменённые файлы перечислены ниже и в commit. Сгенерированные DLL, игровые assemblies, Settings.xml, saves и временные артефакты в commit не включаются. Release и upstream не изменяются; mod package/release не публикуется.

## Изменённые файлы

31 файл; только source, tests и документация:

- `Documentation/InventorProgressionPrototype.md`
- `SolastaUnfinishedBusiness/Api/Helpers/MulticlassInOutRulesHelper.cs`
- `SolastaUnfinishedBusiness/Behaviors/ModifyEffectDescriptionOnLevels.cs`
- `SolastaUnfinishedBusiness/Behaviors/ModifyPowerPoolAmount.cs`
- `SolastaUnfinishedBusiness/Classes/InventorClass.cs`
- `SolastaUnfinishedBusiness/CustomUI/CustomInvocationSelectionPanel.cs`
- `SolastaUnfinishedBusiness/Definitions/InvocationPoolTypeCustom.cs`
- `SolastaUnfinishedBusiness/Displays/ClassesDisplay.cs`
- `SolastaUnfinishedBusiness/Models/BootContext.cs`
- `SolastaUnfinishedBusiness/Models/InventorInfusionContext.cs`
- `SolastaUnfinishedBusiness/Models/InventorInfusionPlan.cs`
- `SolastaUnfinishedBusiness/Models/InventorItemSafetyContext.cs`
- `SolastaUnfinishedBusiness/Models/InventorProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/InventorProgressionRules.cs`
- `SolastaUnfinishedBusiness/Models/InventorSpellcastingContext.cs`
- `SolastaUnfinishedBusiness/Models/SharedSpellsContext.cs`
- `SolastaUnfinishedBusiness/Models/SpellPointsContext.cs`
- `SolastaUnfinishedBusiness/Models/ToolsContext.cs`
- `SolastaUnfinishedBusiness/Models/WarlockInvocationTransactionsContext.cs`
- `SolastaUnfinishedBusiness/Models/WizardProgressionContext.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetActorPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterHeroPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetEffectPowerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetImplementationManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Settings.cs`
- `SolastaUnfinishedBusiness/Subclasses/InnovationArtillerist.cs`
- `SolastaUnfinishedBusiness/Subclasses/InnovationVitriolist.cs`
- `Tests/FighterProgression/FighterProgression.Tests.csproj`
- `Tests/FighterProgression/InventorTests.cs`
- `Tests/FighterProgression/Program.cs`
