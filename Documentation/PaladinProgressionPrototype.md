# Paladin 20→12 — Stage 10

Статус: **PARTIALLY READY**. Реализована адаптация прогрессии и ресурсов; код пригоден для изолированного игрового прогона. Исполнение Harmony, бой, GUI, отдых, respec и сохранение/загрузка в Solasta здесь не запускались. Это не подтверждение игровой совместимости.

## Границы и включение

Разработка только в `feature/progression-20-to-12`, поверх Stage 9 `9ae8e50fb00f0fe94e2c34e8f29e31ac7cc297d3`. `release` не меняется. Ranger, Inventor и объединение нескольких магических классов не реализуются.

В UB → Classes включить **Paladin 20 to 12 prototype**, затем полностью перезапустить игру. Настройка `EnablePaladinCompressedProgressionPrototype` по умолчанию `false`. Необходимы уже включённые `EnableLevel20` и `EnableMulticlass`; модуль сам их не включает. Все существующие пользовательские настройки сохраняют свои значения: готовый `Settings.xml` не устанавливается и не переписывается.

Использовать только нового тестового героя и отдельные тестовые сохранения. Для отключения снять переключатель и полностью перезапустить игру; открыть обычное сохранение обычного героя. Не открывать сжатого героя в обычном режиме или наоборот. Автоматической конвертации нет, включая существующих Paladin1–12. Paladin выше12 явно неподдерживаем: повышение и классовые действия блокируются.

Модуль запускается после UB/2024, Spell Points и десяти предыдущих модулей. Он преобразует окончательные определения класса и доступных клятв один раз за процесс. Повторная инициализация и включение без перезапуска после первоначального `false` исключены. Переключатели Paladin2024, Smite Spells и общего ritual casting после активации откладываются до перезапуска. Изменение значимых настроек/состава клятв блокирует дальнейший level-up/finalization и магические действия Paladin до перезапуска. Не сохранять тестового героя после изменения режима без перезапуска.

## Базовая прогрессия

Сохраняются все события окончательного источника, исходный порядок, отдельные выборы и повторные награды. Для высоких уровней используются упорядоченные Union-обёртки и независимые origin-теги пулов. Идентичности сил, overridden-power связи, пользовательские обработчики, реальные уровни, история классов, HP, Харизма и proficiency bonus сохраняются. Глобального изменения `GetClassLevel` нет.

| Фактический уровень | Оригинальные уровни этого повышения | Эффективный уровень | Ключевые события базы без дополнительных пользовательских наград |
|---|---|---|---|
| 1 | 1 | 1 | Divine Sense, Lay on Hands, лечение яда/болезни; casting при опции начала с1 |
| 2 | 2 | 2 | Fighting Style, выбранный Divine Smite, casting в legacy |
| 3 | 3 | 3 | Channel Divinity, выбор клятвы |
| 4 | 4 | 4 | ASI/feat |
| 5 | 5 | 5 | Extra Attack |
| 6 | 6–7 | 7 | Aura of Protection, способности клятвы7 |
| 7 | 8–9 | 9 | ASI/feat; Abjure Foes при соответствующей опции |
| 8 | 10–11 | 11 | Aura of Courage, Improved Divine Smite; дополнительный CD2024 |
| 9 | 12–13 | 13 | ASI/feat, следующие oath spells |
| 10 | 14–15 | 15 | Cleansing Touch либо Restoring Touch, способности клятвы15 |
| 11 | 16–17 | 17 | ASI/feat, пятый круг и последние oath spells |
| 12 | 18–20 | 20 | Улучшения аур18, ASI19, капстоуны20 и выбранное восстановление |

Пять исходных ASI/feat-событий4/8/12/16/19 приходятся на4/7/9/11/12. При целевых трёх feat/ASI-переключателях сохраняются пять ASI и десять классовых feats, плюс отдельный первый racial feat; на12 две независимые feat-награды от original18 и19. Дополнительные награды UB, включая первый feat и обе опции EveryFourLevels, берутся из окончательного источника; существующие настройки не меняются. Fighting Style остаётся на2, Extra Attack — на5; дополнительные атаки клятвы сохраняют исходную семантику. Weapon Mastery не заменяется новой реализацией.

## Заклинания и Spell Points

Paladin остаётся единственным **half-caster** с собственной WholeList/Prepared-репертуаром. При `EnablePaladinSpellCastingAtLevel1=true` сохраняется `HalfRoundUp`, иначе `Half`. Проецируются окончательные строки слотов UB по эффективному **уровню Paladin**, а не по суммарному уровню героя. Индекс уже спроецированной таблицы — фактический уровень: повторной компрессии нет.

Особенность установленного UB: таблицы слотов и бюджет Spell Points **одиночного** half-caster округляют вверх в обоих вариантах; различается старт1/2. Обычный алгоритм объединения нескольких классов округляет Half вниз, но такое объединение на этом этапе заблокировано. Оно не используется вместо прогрессии единственного Paladin.

| Paladin | Эффективный | Вклад одиночного caster при старте1 | Максимальный круг | Обычные слоты кругов1–5 | Spell Points | Базовая таблица prepared2024 |
|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 1 | 2/0/0/0/0 | 4 | 2 |
| 2 | 2 | 1 | 1 | 2/0/0/0/0 | 4 | 3 |
| 3 | 3 | 2 | 1 | 3/0/0/0/0 | 6 | 4 |
| 4 | 4 | 2 | 1 | 3/0/0/0/0 | 6 | 5 |
| 5 | 5 | 3 | 2 | 4/2/0/0/0 | 14 | 6 |
| 6 | 7 | 4 | 2 | 4/3/0/0/0 | 17 | 7 |
| 7 | 9 | 5 | 3 | 4/3/2/0/0 | 27 | 9 |
| 8 | 11 | 6 | 3 | 4/3/3/0/0 | 32 | 10 |
| 9 | 13 | 7 | 4 | 4/3/3/1/0 | 38 | 11 |
| 10 | 15 | 8 | 4 | 4/3/3/2/0 | 44 | 12 |
| 11 | 17 | 9 | 5 | 4/3/3/3/1 | 57 | 14 |
| 12 | 20 | 10 | 5 | 4/3/3/3/2 | 64 | 15 |

Для legacy-старта2 строка Paladin1 имеет нулевые ячейки/очки и не имеет casting; остальные строки совпадают. В режиме Spell Points проецируется именно окончательная таблица этого режима, включая исходную особенность `SwitchFeatureDefinitionCastSpellSlots`: при старте1 UB выбирает обычную `HalfRoundUpCastingSlots` и для режима очков. Таблица выше показывает обычные ячейки для сравнения, а не запас независимых дополнительных ячеек поверх очков.

Бюджет берётся из существующей `SpellPointsByLevel`, затраты кругов1–5 —2/3/5/6/7. Максимум64, не133. Круги6–9 не выдаются. Fighter/Rogue/Monk/Barbarian без магических подклассов не увеличивают этот бюджет. Восстановление, временные ячейки и upcasting остаются в исходной системе UB; дополнительного списания или дополнительной системы возврата модуль не вводит.

Legacy prepared: `max(1, floor(эффективный Paladin / 2) + реальный модификатор CHA + исходные affinity-бонусы)`. Это деление вниз подтверждено IL установленного `ComputeMaxPreparedSpells`, включая режим casting с1. При CHA20 на Paladin12 базовый лимит15. Для `EnablePreparedSpellsTables2024` используется существующая таблица выше; обычная CHA не добавляется повторно, исходные affinity-модификаторы сохраняются.

Oath auto-prepared groups переводятся с сохранением заклинаний, порядка, класса-владельца и пользовательских обработчиков. Группы13/17 приходятся на9/11. Oath spells не создают второй репертуар и остаются отдельными от обычного лимита подготовки по правилам движка. Неизменяемые группы до5 сохраняют идентичность: это необходимо для маркера бесплатного Divine Smite2024. Независимые cantrip/spell-пулы клятв и стилей используют собственные origin-теги; дубли или недопустимые выборы отклоняются до finalization.

## Divine Smite

При `EnablePaladinSmite2024=false` сохраняется исходный legacy additional damage: подтверждённый удар, выбор ячейки/очков, SlotLevel scaling, критическое удвоение, бонус против Undead/Fiend и исходные ограничения оружия/реакций. Improved Divine Smite11→8 остаётся отдельным пассивным событием.

При `true` несовместимая legacy-запись исключается из сжатой базы, а исходный `FeatureSetDivineSmite2024` и его auto-prepared spell сохраняются. Native refresh и боевой legacy-фильтр используют зафиксированный вариант правил. Обработчик free use видит исходную идентичность auto-spell feature; бесплатное применение до long rest остаётся частью исходных правил UB.

Никакого второго обработчика повреждения или оплаты не добавлено. Установленная реализация2024 использует `OnAttackHit`, проверяет доступность bonus action, запрещает применение при расходовании bonus action самой атакой и обрабатывает выбор заклинания/оплаты через исходные reaction requests. Сохраняются критические попадания и семейства целей, а не подставляются предположения по настольной2024.

Thunder проверяет20 через эффективный уровень: его дополнительный legacy Smite rank работает на12. В исходном2024-пути отдельной аналогичной Thunder-прибавки нет; новая2024-механика не выдумывается. Old/Remaster Demon Hunter сохраняют crossbow-валидаторы; Remaster масштабирует свои проверки и кубик Trial Mark по эффективному подклассу, вплоть до D12 при оригинальном18→12.

**Ограничение проверки:** отсутствие двойного списания/урона подтверждено устройством адаптера и эксклюзивностью определений, но реакционные очереди, отмена после подтверждения, critical hit и бесплатный2024 Smite в игре ещё не проверены. Нельзя считать их доказанными одной компиляцией или Cecil.

## Lay on Hands, Channel Divinity и ауры

Исходные HealingPool modifiers: `Set 5` и `MultiplyByClassLevel`. Меняется только Paladin-множитель: базовый максимум на12 — **100** до иных модификаторов. Cure Disease и Neutralize Poison сохраняют стоимость5 из того же пула. `EnablePaladinLayOnHands2024` меняет на bonus action именно Lay on Hands и Neutralize Poison; действие Cure Disease сохраняется таким, каким оно определено UB.

Оригинальный14→10 выдаёт либо Cleansing Touch с реальными CHA-based uses/long rest, либо Restoring Touch2024. Для Restoring Touch сохраняются исходные shared powers и цена5 HealingPool за снятие состояния. Отдельный HealingPool не создаётся.

Legacy Channel Divinity:1 применение с3. В2024:2 с3, ещё+1 на оригинальном11→8, всего3. Abjure Foes9→7 включается только соответствующей опцией; число целей остаётся реальной CHA по исходной формуле. Legacy short rest восстанавливает обычный запас,2024 — одно использованное применение. Устранена утечка глобального Cleric2024 в сжатого legacy Paladin; ранее исправленный обратный случай для сжатого Cleric сохранён. Смешение Cleric+Paladin всё ещё запрещено.

Aura of Protection6→6 и Aura of Courage10→8 сохраняют исходные эффекты и CHA. Их улучшения18→12, как и улучшения официальных/UB клятв, сохраняют исходный `targetParameter=13`, тип цели, условия и ссылки `OverriddenPower`. Модуль не переводит этот параметр в выдуманный радиус и не добавляет второй независимый бафф. Реальное подавление старой ауры, обновление условий при движении/потере сознания и перекрытие аур проверяются в игре.

## Покрытие клятв и капстоунов

Выборка выполняется по фактической `SubclassChoicePaladinSacredOaths` и включённым регистрациям UB; hidden/disabled не включаются принудительно. В журнал выводится точный список текущей сессии. Проверены четыре официальных и семь исходных UB builders; установленный Old Demon Hunter зарегистрирован отдельно и не скрыт исходником, поэтому поддерживается **только если пользователь его включил**. Remaster имеет ID `OathOfDemonHunterRemaster`, в отличие от Old с ID `OathOfDemonHunter`.

Во всех строках ниже реализовано сохранение событий/определений и необходимых прямых проверок уровня. **Игровые эффекты каждой строки не валидированы в Unity**; таблица не означает подтверждённую игровую полноту.

| Фактический ID | Источник | Оригинальные milestones → фактические | Особые механики / капстоун20→12 | Исходный платный recharge20 |
|---|---|---|---|---|
| OathOfDevotion | official | 3/7/15/18/20 →3/6/10/12/12 | Sacred Weapon/Turn Unholy, Devotion aura, Purity of Spirit, Holy Nimbus | да |
| OathOfJugement | official, исходное написание | 3/7/15/18/20 →3/6/10/12/12 | Weight of Justice/Purge Corruption, Righteousness aura, Retribution, Final Judgement | да |
| OathOfTheMotherland | official | 3/7/15/18/20 →3/6/10/12/12 | Fiery powers, Volcanic aura/Heart of Lava, Flames of Motherland | да |
| OathOfTirmar | official | 3/7/15/18/20 →3/6/10/12/12 | Golden Speech/Smite the Hidden, Truth aura, Soraks Bane, Inquisitor Zeal | да |
| OathOfAltruism | UB, если включён | 3/7/15/18/20 →3/6/10/12/12 | Defensive Strike/Spiritual Shielding, Guardian aura/Take the Pain, Exalted Protector; реальные HP/PB/CHA | нет: постоянная сила |
| OathOfAncients | UB, если включён | 3/7/15/18/20 →3/6/10/12/12 | Nature's Wrath/Turn Faithless, Warding aura, Undying Sentinel/Elder Champion | да |
| OathOfDemonHunter | UB Old, если включён | 3/7/15/20 →3/6/10/12 | Trial Mark, Light Energy Crossbow, Divine Crossbow/доп. атака, Demon Slayer; старые валидаторы сохранены | нет: FeatureSet |
| OathOfDemonHunterRemaster | UB Remaster, если включён | 3/7/15/20 →3/6/10/12 | Light Energy Crossbow/Trial Mark, Hunter Sight/Divine Crossbow, Hunter Step/Demon Hunter, Demon Slayer/CD-on-kill; кубик/порог по эффективному подклассу | нет: FeatureSet |
| OathOfDread | UB, если включён | 3/7/15/18/20 →3/6/10/12/12 | Mark/Presence, Domination aura с class-scaled damage, Harrowing Crusade/Aspect of Dread | да |
| OathOfHatred | UB, если включён | 3/7/15/20 →3/6/10/12 | Favored foe/Channel powers, Dauntless Pursuer/Resistance, Ardent Hate | да |
| OathOfThunder | UB, если включён | 3/7/15/20 →3/6/10/12 | Divine Bolt/Rebuke/God of Thunder, Bifrost, Storm Herald/flight/immunities; прямые пороги и legacy Smite20 адаптированы | нет: FeatureSet/PB-uses |

`EnablePaladinRechargeLv20Feature` сохраняет исходный loader: он создаёт recharge только для непосредственно выданных на20 LongRest powers. Силы из FeatureSet, постоянные ауры и PB-based Storm Herald не получают выдуманного универсального recharge. Капстоун каждой клятвы остаётся её собственностью.

На12 recharge требует: подходящий режим/герой, владение **обеими** силами, исчерпанный капстоун, доступная ячейка5 либо7 Spell Points. Исходный callback единожды оплачивает `SpendSpellSlot(5)` и возвращает одно применение. Повторный callback одного действия блокируется production-ledger; после траты восстановленного капстоуна новое **оплаченное** действие разрешено. Отдельного «один recharge за long rest» исходник не задаёт: ограничения — ресурс, владение и исходные uses капстоуна. Бесплатного бесконечного восстановления модуль не добавляет. При исключении внутри начавшейся finalization/recharge нет общего отката всего игрового мира: отменить/перезагрузить тестовое сохранение.

## Multiclass, respec и сохранения

Paladin ограничен12 только при включённом модуле; общий cap20, max3 и реальные требования STR/CHA/других классов остаются в UB. Примеры для игрового теста: Paladin12/Fighter8, Paladin12/Rogue8, Paladin8/Fighter6/Rogue6 с подходящими атрибутами и немагическими подклассами. Фактические HP, PB и история не превращаются в уровни20+8.

Paladin+Wizard/Sorcerer/Cleric/Bard/Druid/Warlock, магические martial subclasses, feats/styles с отдельной нерacial-репертуаром блокируются на границах выбора/finalization. Расовые репертуары допускаются. Oath spells и независимые cantrip-пулы без второго casting feature не считаются вторым классом. Native ability requirements не обходятся. Если на Paladin12 нет разрешённой альтернативы, level-up блокируется с modal/log-диагностикой; это ожидаемый тупик при недостаточных атрибутах или иных ограничениях, не автоматическая смена правил.

`EnableRespecAction=true` остаётся целевой пользовательской настройкой; код её не устанавливает. Перед respec нужно завершить временные oath/capstone/Smite-эффекты (при free-Smite marker может требоваться long rest). Постоянные ауры `Permanent` и `PermanentUnlessIncapacitated` не требуют невозможного ручного отключения: штатный finalizer завершает старые эффекты. Успешный перенос TagEffect дополнительно исключает старые self-owned Paladin/oath/Smite-условия; внешние условия и другие классы сохраняют прежний путь. До подтверждения respec состояние старого героя не очищается. Back/Cancel/Reset чистят временные compressed origins и ledger обучения; выборы новой клятвы не переиспользуют старые.

Остаются игровые риски: обновление аур на союзниках при смене GUID/клятвы, очистка ресурсов после многократного respec, сохранение выбранных заклинаний/стилей и глобальные особенности штатного respec UB (его замена героя и сброс классовых ресурсов не переписаны). Эксплуатация повторного respec как восстановления ресурсов отдельно не доказана предотвращённой. Автоматической миграции/редактирования файлов сохранений нет; совместимость существующих героев не обещается.

## Проверки и их пределы

Baseline проверен до правок: .NET SDK9.0.320, Release Workflow **0 ошибок / 0 предупреждений**, **2606** проверок. Итог: **3679**, то есть **1073** добавлено; предыдущие2606 включены и проходят, в том числе Warlock6.1, Bard Magical Secrets, Cleric CD и Druid Wild Shape/Archdruid.

Выполняются production planner/transformer/rules с managed doubles: mapping, полная упорядоченность leaf events, counts/origins, цепочки замены, одноразовый latch, disabled-mode, таблицы, class-cap, recharge-ledger и правило переноса условий. Инвентари берутся из blueprints и текущих конструкторов UB, включая отсутствующий в старом snapshot Demon Hunter Remaster. Модель64 комбинаций шести прогрессионных флагов подаётся в настоящий transformer; она **не исполняет** `Tabletop2024Context`. Lay on Hands action economy, fighting-style switch, остальные боевые свойства проверены по установленным исходникам/скомпилированным определениям и требуют игрового прогона.

Cecil-проверки устанавливают наличие/порядок вызовов, реальные константы UB, scoped resource/scaling paths, source flags, Harmony targets и имена аргументов. Они **не запускают Harmony, native switch methods, движок, GUI, бой, короткий/долгий отдых или сохранение**. Проверка полного автомата Smite/cancel, aura stacking и respec требует Solasta.

Команды из подготовленного checkout:

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

Windows Release Install не используется. Зависимости повторно не устанавливались. Выходная DLL существует и не пуста; DLL, game assemblies, настройки, сохранения и временные артефакты в коммит не включаются.

## Ручной игровой план

1. Сохранить рабочие пользовательские файлы вне тестового профиля. Запустить игру с модулем OFF и новым контрольным обычным Paladin; проверить обычные уровни/ресурсы. Включить только прототип, оставить исходные пользовательские опции, полностью перезапустить. Зафиксировать версию игры/UB, выбранные2024 flags и список oath из UB log. Создать нового героя со STR/CHA, допускающими желаемый multiclass, без casting feats.
2. Пройти **каждое** повышение1–12, включая1→2,5→6,7→8,11→12. Сверять события и ресурсы с таблицами, реальную историю классов, HP/PB и доступные действия. Включённые multi-choice UI и отключённый streamlined level-up должны показывать каждый ASI/feat/skill/cantrip-пул; выбирать все, проверять отсутствие потерь/дубликатов после Back/Cancel/Reset и повторного открытия.
3. Повторить прогон в двух отдельных профилях casting: начало1 и legacy начало2; отдельно slots и Spell Points, каждый раз с новым героем и полным перезапуском. На1 проверять наличие/отсутствие casting; на11 — круг5/57 очков, на12 — максимум5/64 очка. Проверить реальный расход каждого круга2/3/5/6/7, upcasting, недоступность6–9, исчерпание, long rest и временный slot effect без удвоения пула.
4. Подготовка: CHA с модификаторами+0/+3/+5, уровни5/6/9/12; сравнить legacy floor half-level и отдельный2024 table mode. Подготовить обычные spells до лимита, сменить их на отдыхе. Проверить oath spells на3/5/7/9/11, отсутствие потребления обычного лимита и повторных записей. Проверить разрешённый cantrip-style с отдельными choice pools; style/feat, создающий второй репертуар, должен быть недоступен либо отклонён до finalization.
5. **Legacy Smite:** в бою тестировать обычного врага, Undead и Fiend; попадание/промах/крит; оба удара Extra Attack; реакционную атаку; melee/throw/crossbow для соответствующих клятв. Сравнивать журнал урона, выбранный circle и ресурс до/после. Отменить reaction popup, затем следующий подтверждённый удар: отмена не должна давать damage или двойную оплату. Проверить Improved Smite на8 отдельно от платного урона. На Thunder12 проверить исходную прибавку rank и соответствующую стоимость выбранного slot, не дополнительную плату.
6. **Smite2024:** отдельный новый герой после перезапуска; legacy trigger отсутствует. Проверить auto-prepared spell, free use и его long-rest reset, затем все платные circles1–5 и их costs. Проверить занятый bonus action, bonus-action attack/Nick, Extra Attack, reaction, крит, семейства целей, silence и отсутствие ресурсов. Отменять оба popup (spell и slot/free), повторять подтверждение, проверять ровно один damage event и одну оплату. Сравнить с обычным Paladin той же эффективной ступени: исходные особенности UB не подменять ожиданиями tabletop.
7. **Lay on Hands:** на5/6/10/12 базовый максимум25/35/75/100 до добавочных modifiers. Лечить разное число HP, в том числе почти исчерпанным пулом, снимать poison/disease за5; проверить общий расход. Для2024 проверить именно исходные действия Lay on Hands/Neutralize Poison. На10 проверить альтернативные Cleansing/Restoring Touch, каждый снимаемый condition, cancel submenu, недостаток5 points, отсутствие повторной траты и long-rest recovery.
8. **CD/Abjure:** legacy1 use с3 против2024 двух, затем трёх с8. Использовать все до исчерпания и проверить short/long rest. Отдельные сочетания Cleric2024ON/Paladin2024OFF и наоборот на отдельных героях должны сохранять собственный режим восстановления. Abjure доступен с7 только при флаге; проверить CHA-dependent target count. У каждого oath проверить его собственные Channel powers из общего пула.
9. **Ауры, группа6:** на6 и8 проверить базовые Protection/Courage, на12 — upgrades и oath aura. Разместить союзников на границе/за границей, перемещать по одному; проверять спасброски, страх, CHA change, concentration benefits нужной клятвы, смерть/потерю сознания, повторную активацию. Не должно складываться старое+новое улучшение одной ауры. Сравнить геометрию с обычным Paladin18, затем перекрыть ауру другим Paladin; проверить источник и удаление условий на каждом союзнике.
10. Для **каждой строки матрицы** создать отдельного героя либо выполнить контролируемый respec после long rest. На6/10/12 проверить исходные milestone powers/conditions, oath spell lists, damage/weapon restrictions, независимые ресурсы и capstone. Remaster Trial Mark должен менять dice на original3/6/10/14/18 → actual3/6/8/10/12; проверить kill/CD recovery только по исходным правилам. Thunder проверить class-scaled Bolt/Rebuke/God of Thunder, Bifrost, flight/immunities/Storm Herald с реальным PB. Altruism — обмен реальных HP и постоянный Exalted Protector; Dread — Domination damage от своего эффективного класса.
11. **Recharge20:** на12 для семи исходно подходящих клятв исчерпать свою силу, оплатить recharge5/7points, использовать возвращённое применение. Проверить недоступность при неисчерпанном capstone, отсутствии ресурса, другой клятве и до12. Повторить отмену и новое оплаченное применение. Для Old/Remaster/Thunder/Altruism отсутствие универсального recharge соответствует source loader. Исходные long-rest/PB/постоянные ресурсы должны восстанавливаться своим путём, без бесплатного цикла.
12. **Multiclass:** довести Paladin12/Fighter8 и Paladin12/Rogue8 до20, затем Paladin8/Fighter6/Rogue6. Проверять casting/LoH/CD на каждом martial level: ресурсы не растут от чужих уровней, PB/HP растут реально. Paladin13 и уровень героя21 запрещены. Попытки каждого другого caster, magical martial subclass, casting feat/style со второй нерacial-прогрессией блокируются до мутации. На отдельном герое с неподходящими атрибутами проверить явную cap-deadlock диагностику; STR/CHA требования не ослабляются.
13. **Respec/cancel:** после расходования ресурсов, смены oath/prepared spells/style и получения нескольких feat-выборов выполнить Back/Cancel/Reset, повторный respec и respec в немагический класс/обратно. С временными эффектами должна быть ранняя диагностика; после их завершения постоянные ауры не должны блокировать respec. На всех союзниках удалить старые oath conditions и восстановить только новые. Проверять old/new GUID ownership, used/max resources, free-Smite marker, auto-spells, recharge ownership и отсутствие старых pending pools. Не считать штатный сброс ресурсов UB отдельной гарантией против эксплуатации respec.
14. Сохранить/загрузить отдельные тестовые файлы наPaladin6/8/11/12 и multiclass20, включая spent resources и aura conditions. Сверить до/после журнал, подготовку, избранные действия и оплаченность ресурсов. При исключении finalization/recharge отменить/перезагрузить, не продолжать сохранение частичного состояния. Проверить disabled regression только на обычном контрольном герое после полного перезапуска; проверить предыдущие классы и их transaction regressions в игре отдельно.

## Оставшиеся ограничения и оценка

Игровой прогон не выполнен. Критичные остаточные риски — очереди реакций и отмена Smite, синхронизация расходов/GUI, подавление и обновление aura conditions, short/long-rest callbacks, повторный respec и save/load. Общий atomic rollback игрового мира не реализован. Модификации сторонних модов после UB LateLoad не сертифицированы. Shared multicaster, любые новые классы и миграция старых героев отсутствуют намеренно.

Рекомендация: провести контролируемые тесты из списка на новых героях, начиная со Smite, аур и ресурсов. Для обычного прохождения или основных сохранений прототип не рекомендован. Финальная классификация — **PARTIALLY READY** до игрового подтверждения этих механизмов.

## Изменённые файлы

- `Documentation/PaladinProgressionPrototype.md`
- `SolastaUnfinishedBusiness/Api/Helpers/MulticlassInOutRulesHelper.cs`
- `SolastaUnfinishedBusiness/Behaviors/ModifyPowerPoolAmount.cs`
- `SolastaUnfinishedBusiness/Behaviors/Specific/_GLBM.cs`
- `SolastaUnfinishedBusiness/Displays/ClassesDisplay.cs`
- `SolastaUnfinishedBusiness/Models/2024PaladinContext.cs`
- `SolastaUnfinishedBusiness/Models/BootContext.cs`
- `SolastaUnfinishedBusiness/Models/ClericProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/PaladinPowerSafetyContext.cs`
- `SolastaUnfinishedBusiness/Models/PaladinProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/PaladinProgressionRules.cs`
- `SolastaUnfinishedBusiness/Models/PaladinSpellcastingContext.cs`
- `SolastaUnfinishedBusiness/Models/SharedSpellsContext.cs`
- `SolastaUnfinishedBusiness/Models/SmiteSpells2024Context.cs`
- `SolastaUnfinishedBusiness/Models/SpellPointsContext.cs`
- `SolastaUnfinishedBusiness/Models/ToolsContext.cs`
- `SolastaUnfinishedBusiness/Models/WizardProgressionContext.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetActorPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterHeroPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetEffectPowerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetImplementationManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Settings.cs`
- `SolastaUnfinishedBusiness/Subclasses/OathOfDemonHunter.cs`
- `SolastaUnfinishedBusiness/Subclasses/OathOfDemonHunterOld.cs`
- `SolastaUnfinishedBusiness/Subclasses/OathOfThunder.cs`
- `Tests/FighterProgression/FighterProgression.Tests.csproj`
- `Tests/FighterProgression/PaladinTests.cs`
- `Tests/FighterProgression/Program.cs`

Коммит и результат push указаны в итоговом отчёте чата; файл входит в этот же коммит. Сформированный мод-пакет не публикуется.
