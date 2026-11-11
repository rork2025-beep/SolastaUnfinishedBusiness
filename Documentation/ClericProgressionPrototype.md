# Cleric 20→12 — Stage 8

Оценка: **PARTIALLY READY**. Opt-in прототип Cleric и15 доменов реализован и прошёл автономные проверки.
Его можно передать на контролируемую игровую проверку только на новых героях и отдельных тестовых saves.
Unity, Harmony execution, UI, бой, отдых, respec и save/load здесь не запускались.
Divine Intervention2024 в установленном UB отсутствует; DomainOrder имеет исходный пробел на уровне10.
Эти механики не объявляются завершёнными. Смешанные заклинатели остаются заблокированы.

## Baseline и результат проверки

- Checkout: `/workspace/SolastaUnfinishedBusiness`; ветка `feature/progression-20-to-12`.
- Исходный HEAD: `a5c12440396b9a8fc5a5a413bae99134d44fb10d` — Bard Stage7.
- Baseline заново проверен: **1803 проверки**, Release Workflow — **0 ошибок / 0 предупреждений**.
- После Stage8: **2212 проверок** (409 новых), Release Workflow — **0 ошибок / 0 предупреждений**.
- SDK **9.0.320**. Повторная установка зависимостей не потребовалась.
- Выходная DLL существует и не пуста: `SolastaFiles/Mods/SolastaUnfinishedBusiness/SolastaUnfinishedBusiness.dll`.
- `git diff --check` без замечаний. Сборочные DLL, игровые assemblies, Settings.xml и saves в коммит не входят.

Fighter, Barbarian, Monk, Rogue, Wizard, Sorcerer, Warlock и Bard сохранены; прежние тесты не изменены.
Выполняются регрессии Warlock6.1 invocation transactions и Bard Magical Secrets.
Общие расчёты расширены только узкими Cleric-ветвями. Druid/Paladin/Ranger/Inventor не сжимаются.

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

## Включение, выключение и безопасность

В UB → Classes включить **Cleric 20 to 12 prototype (NEW HEROES; restart; SINGLE CASTER ONLY)**
(`EnableClericCompressedProgressionPrototype`) и полностью перезапустить игру.
Флаг выключен по умолчанию; используются существующие Settings/UI UB.
Без `EnableLevel20=true` и `EnableMulticlass=true` модуль не активируется и пишет причину в лог.
Также до изменения definitions проверяется наличие native Divine Intervention adapter и форма его IL;
если адаптер не подтверждён, активация отклоняется.

Пользовательские настройки автоматически не изменяются. Сохранить целевые `MaxAllowedClasses=3`,
`EnableMinInOutAttributes=true`, Spell Points, ASI+feat и оба дополнительных feat-графика, feat первого
уровня, PCG/Epic Points, UI выбора наград и отключение streamlined multi-level-up.
`EnableRespecAction=true` — целевая пользовательская опция, модуль её не включает самостоятельно.
Личный Settings.xml в облачной среде отсутствует, поэтому фактические игровые значения здесь неизвестны.
При активации логируются пять доступных Cleric rule flags, generic prepared-table/Spell Points mode
и реально обнаруженные домены. Не предполагается, что все2024 опции включены.

Для выключения убрать флаг и полностью перезапустить игру. Новая сессия получает обычные definitions.
Сохранение сжатого героя этим не преобразуется обратно. Не открывать обычные Cleric saves в сжатом
режиме и наоборот. Существующие Clerics выше12 не поддержаны для автоматической конверсии;
их дальнейшее повышение блокируется с диагностикой. Другие старые Cleric saves также не мигрируются.
При изменении правил/доменных регистраций в сессии повышение требует рестарт. Пять Cleric definition
switches защищены от немедленной повторной перестройки сжатых definitions; обработчики других
механик могут читать живые Settings, поэтому после изменения правил нужно сразу перезапустить игру.

## Прогрессия и интеграция

| Actual Cleric | Исходные события | Effective Cleric | Максимальный круг | Spell Points без модификаторов |
|---|---|---|---|---|
| 1 | 1 | 1 | 1 | 4 |
| 2 | 2 | 2 | 1 | 6 |
| 3 | 3 | 3 | 2 | 14 |
| 4 | 4 | 4 | 2 | 17 |
| 5 | 5 | 5 | 3 | 27 |
| 6 | 6,7 | 7 | 4 | 38 |
| 7 | 8,9 | 9 | 5 | 57 |
| 8 | 10,11 | 11 | 6 | 73 |
| 9 | 12,13 | 13 | 7 | 83 |
| 10 | 14,15 | 15 | 8 | 94 |
| 11 | 16,17 | 17 | 9 | 107 |
| 12 | 18,19,20 | 20 | 9 | 133 |

Поздняя трансформация выполняется после UB/2024, Spell Points и предыдущих восьми модулей.
Переиспользуются `FighterProgressionPlan`, `CompressedProgressionDefinitions`,
`SpellcastingProgressionPlan` и initialization latch. Учитывается финальная выбранная прогрессия,
а не жёстко заданная legacy-таблица особенностей. Сохраняются отдельные события, repeated rewards,
исходный порядок, overridden references и custom subfeatures. Choice pools получают отдельные origins.
Низкоуровневые идентичности, важные для multiclass proficiency filters, остаются штатными.

Cleric использует `FilterByDeity`: официальный список `SubclassChoice.Subclasses` пуст.
Поэтому discovery берёт реальные subclasses доступных божеств, явные class choices и включённые
UB-регистрации, исключая hidden definitions. Никакие выключенные домены принудительно не включаются.

Actual class/character levels, HP, PB, class history и ability requirements не меняются.
Глобального патча `GetClassLevel` нет. Эффективный Cleric используется только в соответствующих
class-rank power/effect/resource/condition расчётах и прямых Cleric callbacks.
WIS/PB источники сохраняют реальные значения. Law17 threshold→actual11, Mischief temp HP и Oblivion
healing pool адаптированы; прямое масштабирование Divine Spark heal также адаптировано.

Классовые ASI original4/8/12/16/19→actual4/7/9/11/12 сохранены. С ASI+feat это пять отдельных class
ASI и пять class feat awards. Дополнительные UB feat events original2/6/10/14/18 остаются отдельными;
racial first-level feat не дублируется. Divine Order, domain skills/cantrips и ASI/feat origins не сливаются.

## Колдовство и подготовка

Cleric сохраняет **WholeList / Prepared / Wisdom / LongRest**. Не создаются Wizard spellbook,
Sorc known-spell budgets, сcribing или replacement события. Обычный cantrip прогресс:
**3,3,3,4,4,4,4,5,5,5,5,5** на actual1–12. Доменные/Thaumaturge bonus cantrips используют свои pools/tags
и не подменяют ordinary cantrip бюджет. Nature Druid cantrip selection остаётся выбором заклинания
для Cleric repertoire и не становится Druid class/repertoire.

На actual11 доступны исходные17 ячейки, включая9 круг. На12 обычная слотная строка исходного20:
**4/3/3/3/3/2/2/1/1** для кругов1–9. В Spell Points режиме проецируются именно финальные исходные
UB rows и собственный бюджет full caster20=**133** из существующей таблицы, без новой константы бюджета.
Режимы slots/points не суммируются; native цены, high-circle ограничения, upcast и long-rest восстановление
не переписываются. Индексы caster tables ограничены0–20. В martial multiclass бюджет зависит от
эффективного Cleric, а не от общего character level. Другим классам этот бюджет не присваивается.

Legacy ordinary prepared limit = **max(1, effective Cleric + WIS modifier + native affinity bonuses)**.
PB/WIS-based affinity надбавки остаются штатными и добавляются один раз; минимум применяется в конце.
Например, Cleric12 с WIS20 без надбавок готовит25 обычных заклинаний.

`EnablePreparedSpellsTables2024=true` использует существующую UB таблицу Cleric/Druid с индексом
эффективного Cleric, сохраняя её affinity modifiers. Проекция actual1–12:
**4,5,6,7,9,11,14,16,17,18,19,22**. Обычный WIS modifier к этой таблице дополнительно не приписывается.
Таблица других классов не изменена.

Domain auto-prepared groups клонируются: original1/3/5/7/9→actual1/3/5/6/7; Defiler Dying Light17→11.
Сами spells/source lists и custom behaviors сохраняются. Если домен выбирается на3, группы1 и3
не выдаются до приобретения его особенности. Auto-prepared entries остаются вне ordinary подготовки;
при нескольких совпадающих источниках исполняемый список дедуплицируется, сами feature sources сохранены.
Changing preparation, rest activities, ritual rules и сохранение PreparedSpells остаются native UB.
Нет свободного доступа ко всему Wizard/Druid списку: разрешаются только исходные domain lists,
конкретные pool overrides и реально выданные extended affinities, с их кругами/ritual restrictions.

При Acquire проверяются бюджет/список/круг и повторный выбор. Origins поздних bonus pools проходят
через native Cleric grant с once-per-actual-level ledger. Повторный grant того же origin не выдаёт
вторую награду; reuse building-data на следующем уровне разрешён. Back/Cancel очищают временные
Cleric origins. Перед финализацией проверяются spell pools и новые casting feats/styles.
Это защита против очевидного дублирования, а не полный transactional rollback всей native финализации.
После исключения уже начатой финализации повторная выдача блокируется: отменить/загрузить тестовый save.

## Channel Divinity, Turn/Sear и Divine Intervention

| Механика | Поведение |
|---|---|
| Legacy Channel Divinity | Uses1 на2,2 на6,3 на12 (original18); один общий attribute/counter |
| Channel Divinity2024 | Uses2 на2,3 на6,4 на12; Divine Spark с2 |
| Legacy recovery | Штатное полное short/long-rest восстановление |
|2024 recovery | Штатный UB путь:1 use на short rest, полное long-rest восстановление; simulate не расходует/не выдаёт ресурс |
| Destroy Undead | Original5/8/11/14/17→actual5/7/8/10/11; CR0.5/1/2/3/4, исходные replacement links |
| Divine Spark | Healing dice1/2/3/4 при original1/7/13/18→actual1/6/9/12; получение самой способности2; damage class-rank и WIS сохранены |
| Sear Undead | Получение5; исходный WIS-based radiant dice/save, общий Channel ресурс; legacy Destroy tiers не выдаются |
| Legacy Divine Intervention | Original10→actual8, существующий summon Avatar Cleric/Paladin/Wizard по домену; native Action/LongRest1 и failure path |
| Improved Intervention | Original20→actual12; исходный overridden power с `HasCastingFailure=false`, без второго независимого пула |

Вероятность legacy Intervention native код вычисляет по `CharacterLevel`. Узкий transpiler в
`CharacterActionUsePower.CheckExecutionFailure` iterator подставляет effective Cleric только для трёх
точных installed Intervention definitions. Сохраняются RNG1–100, сравнение, уведомления, failure/use/recharge
обработчики. На actual8 effective11 даёт порог11; на12 улучшение снимает failure целиком.
Ни действительный CharacterLevel, ни вероятность чужих powers не изменяются.
Tabletop семидневный cooldown не добавлен: установленные definitions имеют LongRest1.

Обнаруженные исходные несогласованности и обработка:

1. `SwitchClericSearUndead` не убирает `PowerClericTurnUndead8`. Только в opt-in Sear режиме этот
   оставшийся legacy unlock исключён из входа преобразования: не появляется пара Sear+Destroy.
2. Исходный Sear effect handler проверяет только глобальный флаг и может действовать до unlock5.
   В прототипе добавлен порог5; при отключённом Sear/до5 удаляется именно **копия** damage form:
   native `EffectDescription.Copy` клонирует forms, поэтому удаление static template по reference не работает.
   Эти изменения ограничены героями с Cleric при активном модуле; обычный режим прежний.
3. Исходный rest prefix использует OR Paladin2024/Cleric2024 и тем самым может запрещать legacy Cleric
   short-rest recovery из-за чужой настройки. В прототипе sign-preservation gate выбирает собственное
   правило Cleric. При выключенном модуле и для других классов сохраняется исходный OR.
4. **Divine Intervention2024 отсутствует:** switch и UI находятся под `#if false`, свойства нет в
   скомпилированном Settings. Здесь нет применимого второго варианта, и он не изобретён.
5. **DomainOrder не выдаёт legacy Intervention10 в исходном конструкторе**, но выдаёт улучшение20.
   Прототип сохраняет этот инвентарь и не добавляет отсутствующий unlock. Полная общая Divine Intervention
   progression для Order до12 не объявляется реализованной; источник пробела — UB baseline.

## Правила2024

| Настройка | Поддержка |
|---|---|
| `EnableClericBlessedStrikes2024` | Финальные source switches: exclusive Potent Spellcasting/Primal Strike на original7→6, improvement14→10; заменённые legacy Divine Strike не дублируются; WIS/cantrip/weapon/cost semantics исходные |
| `EnableClericChannelDivinity2024` | Counts, Spark и rest путь согласно таблице выше; исходные6/18 дополнительные uses сохранены |
| `EnableClericDivineOrder2024` | Original1: одна exclusion choice Protector/Thaumaturge; броня/оружие либо bonus cantrip и WIS-based Arcana/Religion modifier; не выдаются оба выбора |
| `EnableClericToLearnDomainAtLevel3` | Domain choice1 либо3 и перенесённые начальные domain features берутся из final definitions; ранней выдачи до выбора нет |
| `EnableClericSearUndead2024` | Sear5 вместо Destroy chain, с описанными scoped исправлениями |
| `EnableClericDivineIntervention2024` | Недоступно в этом build, не поддержка вымышленного варианта |
| `EnablePreparedSpellsTables2024` | Существующая таблица, effective индекс и native affinity bonuses |

Generic ritual и остальные spell switches сохраняются в финальных source definitions/native обработчиках.
Для выбранных2024 Blessed Strikes domain changes, damage-type options, Improved temp HP и weapon/cantrip
restrictions не добавляется второй legacy источник. Реальная GUI/action economy требует Unity-проверки.

## Матрица доменов

Во всех15 сохраняются исходные auto-prepared spells и отдельный Cleric repertoire. Общие пороги:
initial1/3 согласно варианту,6→6, legacy8→7 либо Blessed7→6,14→10,17→11,20→12.
Скрытые/неактивные UB домены не включаются этой таблицей. Название `DomainElementalLighting` — реальный
идентификатор источника, включая его написание.

| Домен | Реальные особенности, реакции/ресурсы и поздняя прогрессия | Статус |
|---|---|---|
| Official Battle | Battle Magic/Divine Wrath; Decisive Strike Channel upgrades5/8/11/14→5/7/8/10; Herald6 aura; Paragon17→11 resistances/Improved Herald; Paladin Intervention10/20→8/12 | Преобразован; бой/aura проверить |
| Official Elemental Cold | Bonus cold cantrip, Primal Harmony; Ice Lance Channel; Disciple reaction ShortRest6; Herald WIS LongRest8→7; Summon Blizzard17→11 (два Ice Elementals); Wizard Intervention | Преобразован; summons/recovery проверить |
| Official Elemental Fire | Bonus fire cantrip, harmony; Fire Burst Channel; Disciple reaction6; Herald WIS8→7; Summon Inferno17→11 (два Fire Elementals); Wizard Intervention | Преобразован; reactions/summons проверить |
| Official Elemental Lighting | Bonus lightning cantrip; Lightning Blade Channel; Disciple6/Herald8→7; Living Tempest17→11 flight sprout/dismiss; Wizard Intervention | Преобразован; flight/conditions проверить |
| Official Insight | Intuition/Diplomat; Foreknowledge Channel; Divine Eye6; Divine Lore8→7; Avatar of Knowledge17→11 upgrades Foreknowledge/aura +INT save proficiency; Cleric Intervention | Преобразован; knowledge/skill interactions проверить |
| Official Law | Commanding Presence/Unyielding Enforcer; Retribution/Force of Law Channel; Word of Law6; Anathema8/14→7/10; Final Word17→11 и direct17 concentration callbacks; Paladin Intervention | Преобразован; callback cleanup/reactions проверить |
| Official Life | Armor/Disciple of Life; Preserve Life Channel, class-rank healing; Blessed Healer6; Divine Strike8→7 либо выбранный Blessed; Supreme Healing17→11; Cleric Intervention | Преобразован; healing/rest проверить |
| Official Mischief | Trickster/Borrow Luck; Strike of Chaos Channel upgrades5/8/11/14/17/20→5/7/8/10/11/12, Elusive Target6; Fortune Favor the Bold17→11 и effective temp HP; Wizard Intervention | Преобразован; random conditions/upgrade chain проверить |
| Official Oblivion | Gatekeeper/bonus cantrip; Herald of Pain Channel/Peaceful Rest; Strike Oblivion6; Mark of Fate8/14→7/10; Keeper17→11 и effective class healing pool; Cleric Intervention | Преобразован; per-turn/group healing проверить |
| Official Sun | Bonus cantrip/Holy Radiance; Herald of Sun Channel; Indomitable Light Channel/Soothing Hand WIS LongRest6; legacy strike/Blessed; Rising Dawn17→11 resistances +radiant/fire behavior; Wizard Intervention | Преобразован; healing/damage affinities проверить |
| UB Defiler | Bonus cantrip/Insidious Death Magic; Defile Life Channel с effective class bonus; Beacon6; strike/Blessed; Dying Light17→11 toggle/LongRest power и Circle of Death/Finger of Death auto-prepared; Paladin Intervention | Преобразован; toggle/resources проверить |
| UB Nature | Bonus proficiency/skill и отдельный Druid cantrip pool; Charm Animals/Plants Channel; Dampen Elements6; strike/Blessed; Master of Nature17→11 Channel/control/conditions; Wizard Intervention | Преобразован; source-list, control и реакции проверить |
| UB Order | Bonus armor/skill; Voice of Authority и attack reaction; Order's Demand Channel; Embodiment of Law6 WIS LongRest и native бесплатный Enchantment quicken adapter; strike/Blessed; Order's Wrath17→11; improved Paladin Intervention20→12 | **Ограничение:** нет исходного Intervention10; native metamagic/minimum SorceryPoints scaffolding сохранён, новый Sorcerer ресурс модулем не создаётся |
| UB Smith | Proficiencies; Adamant Benediction Channel; Reinforce Armor1/6/11/16→1/6/8/11, ShortRest override chain; Forge Mastery6; strike/Blessed; Blessed Metal17→11; Paladin Intervention | Преобразован; armor conditions/upgrade/recovery проверить |
| UB Tempest | Proficiencies; Destructive Wrath Channel; Wrath of Storm WIS LongRest lightning/thunder subpowers; Thunderous Strike6; strike/Blessed; Stormborn17→11 flight bonus-action sprout/dismiss; Paladin Intervention | Преобразован; shared counters/damage/flight проверить |

## Multiclass, respec и сохранения

При исходных attribute prerequisites разрешены **Cleric12/Fighter8**, **Cleric12/Rogue8**,
**Cleric8/Fighter6/Rogue6**, с неколдующими subclasses. Общий предел20 и максимум классов — прежние
правила UB (целевое значение3 не переписывается). PB считается по настоящему общему уровню.
Cleric13 закрыт в enumeration, AssignClass и AddClass. Guards предыдущих Wizard/Sorcerer/Bard/Warlock
сохраняются. Cleric+Wizard/Sorcerer/Bard/Warlock/Paladin/прочие casters, casting subclasses и feats,
дающие второй нерacial repertoire, запрещены. Доменные spells/bonus cantrips/extended affinities не
считаются вторым заклинателем. Full mixed Spell Points/shared caster integration не реализована.

На Cleric12 без подходящего выхода (например, WIS<13, неподходящие другие характеристики, лимит3,
нет безопасного класса/подкласса) повышение блокируется и объясняется в сообщении/логе.
Характеристики не повышаются автоматически и пустой level-up flow не принимается.

Native UB respec пересоздаёт героя и переносит часть состояния; модуль не добавляет save migration.
Очищаются временные Cleric spell/choice origins, а повторные grants ограничиваются одним повышением.
Это не доказывает отсутствие stale domain powers, used Channel counters, Intervention cooldowns,
Divine Order choice, prepared/domain spells, active conditions, Spell Points и иных ресурсов после respec.
Полный rollback всего `FinalizeCharacter` при native/стороннем исключении не реализован.
Повторный respec, cancellation, смена домена/профиля, rest previews, multiplayer и serialisation требуют игры.
Не сохранять повреждённый результат исключения; отменить/загрузить отдельный тестовый save.

## Автоматические проверки и их границы

Production planners, общий transformer на managed doubles и чистые Cleric rules исполняются:
mapping,20 событий, весь base inventory,15 domain inventories, повторные rewards/origins, source immutability,
override references, slots/points/cantrips, legacy preparation/minimum/affinities, source2024 table projection,
auto-prepared threshold availability,32 модельные комбинации пяти доступных Cleric flags, Channel counts,
Sear/Destroy presence, Divine Order exclusion, Blessed replacement, opt-off/init latch, exact tag boundaries,
once-only grant ledger и восемь сочетаний short-rest recovery flags.

Отдельные Cecil/JSON проверки устанавливают actual registrations/blueprint values, official17/20 loaders,
отсутствие Intervention2024, исходный Order gap, source switches, scoped direct level adapters, места
интеграции, native iterator shape/deep copy и Harmony target/argument имена. **Это metadata checks**,
не исполнение Harmony patches.32 модельные комбинации не равны32 игровым прогонам.
Autoprepared GUI, реальная смена подготовки, die rolls, rest simulation и save/respec не тестировались в Unity.

## Ручной игровой checklist

1. Создать отдельную тестовую кампанию. После полного рестарта с выключенным флагом проверить нового
   обычного Cleric: несжатые unlocks, preparation, slots, рост выше12. Старые saves не загружать.
   Затем выставить целевые пользовательские опции (включая Respec=true), включить прототип, выбрать
   доступные домены/правила и полностью перезапустить игру. Проверить log flags, список доменов,
   native adapter readiness. Новый герой: WIS≥13 и характеристики для планируемых martial классов.
2. Пройти actual1–12 по одному повышению, записывая actual class/total level, HP, PB, историю, ASI/feats,
   cantrips, prepared count, slots/points, доменные spells, Channel maximum/used и powers. Ни один actual
   Cleric не становится20 в карточке. ASI на4/7/9/11/12 и дополнительные feats сохраняют свои выборы.
3. Отдельные новые герои для legacy preparation и prepared-table2024, независимо от слотного/points
   режима. При WIS20 на Cleric12:25 ordinary legacy либо22 table2024 без affinity bonuses; доменные spells
   не занимают эти места. На actual11 доступен9 круг, на12 обычные slots4/3/3/3/3/2/2/1/1 либо базовые133
   Spell Points. Проверить upcast, цены, high-circle ограничения и long-rest восстановление без двойного бюджета.
4. На каждом уровне и после long rest изменить ordinary prepared spells: снять A, выбрать B, отменить
   подготовку и повторить. Domain spells остаются доступными, prepared cap не меняется из-за дубликатов.
   Проверить ritual доступ по выбранным UB rules. Wizard/Druid whole lists не открываются без реального
   источника. Пройти domain groups original7→actual6,9→7; до выбора домена на3 его spells отсутствуют.
5. Проверить все15 доменов матрицы отдельными героями. Пройти middle upgrades,17→11 и20→12,
   source lists, reactions, native costs/recharge и все override chains. Для Smith1/6/8/11 проверить
   один действующий Reinforce tier; для Mischief11/12 — Chaos17/20 и Fortune conditions; для Law11 —
   concentration callbacks без зависших подписок; для Oblivion11 — class-level healing, реальный PB неизменен.
6. Divine Order off/on: один Protector либо Thaumaturge. Thaumaturge — один отдельный cantrip,
   Arcana/Religion бонус по WIS; повторный skill/cantrip выбор запрещён. Nature: Druid bonus cantrip
   попадает в Cleric repertoire, без второго casting class. Back/Reset/Cancel не сохраняют старый выбор.
7. Для domain1 и domain3 режимов проверить initial features до/после выбора; на3 могут быть несколько
   independent rewards. Blessed off/on: legacy strike наactual7 либо exclusive Blessed на6 и улучшение10;
   Potent/Primal не выдаются вместе без legit источника, divine damage once-per-turn, scaling и WIS temp HP верны.
8. Channel legacy/2024: на2/6/11/12 проверить1/2/2/3 либо2/3/3/4 max uses. Разные domain powers,
   Turn и Spark расходуют общий счётчик. Legacy short rest полностью восстанавливает,2024 — ровно1 use,
   long rest — всё. Повторить legacy Cleric с Paladin2024 глобально on/off: оба результата одинаковы.
   Rest preview/simulate не меняет counter, фактический отдых не восстанавливает дважды.
9. Destroy mode: проверить CR tiers на5/7/8/10/11 и один активный upgrade. Sear mode: до5 нет Sear
   damage, с5 WIS-based radiant/save; на7 не появляется остаточный Destroy8. Divine Spark с2,
   dice2/3/4 на6/9/12, WIS bonus и общий Channel расход; healing и оба damage subpowers проверить отдельно.
10. Legacy Intervention на8: правильный доменный Avatar, исходный Action/use/failure path и effective
    threshold11 в уведомлении. После failure/success проверить native cooldown/rest без ручной раздачи
    применений. На12 улучшение должно заменить legacy и не бросать failure, отдельный дополнительный
    ресурс не появляется. Для Order явно зафиксировать отсутствие base Intervention8 как известный пробел;
    проверить improved12. Несуществующий2024 toggle/эффекты не ожидать.
11. На многонаградных6/8/11/12 пройти Back/Reset/Skip/Cancel, изменить ранний выбор и повторить повышение.
    Spells, bonus cantrips, Divine Order, ASI/feats и ресурсы не растут повторно. Повторить подготовку на отдыхе,
    включая совпадение ordinary/domain spell. Все обязательные spell pools заполнены до принятия повышения.
12. Проверить Cleric12/Fighter8, Cleric12/Rogue8, Cleric8/Fighter6/Rogue6 с исходными prerequisites,
    total≤20/classes≤3, реальным PB и без увеличения Cleric ресурсов от martial levels. Casting class/subclass
    и feat должны блокироваться; domain spells не блокируются. Отдельно Cleric12 с WIS<13/без альтернатив:
    понятное сообщение, без пустого UI, без автоматического WIS13, без мутации героя.
13. При Respec=true записать prepared/domain spells, Divine Order/domain, Channel max/used, Intervention
    availability/cooldown, points/slots, powers/conditions. Respec в тот же домен, другой домен и безопасный
    другой класс; повторить и отменить в середине. Старые domain spells/abilities/choices/conditions/counters
    не должны оставаться. При несоответствии остановить тест и загрузить исходный тестовый save.
14. Save/load завершённого героя в том же режиме; проверить подготовку/домен/resources/rest и следующее
    повышение. Изменить rule flag в сессии: повышение требует рестарт. Выключение проверять на другом
    свежем обычном герое, не делать обратную миграцию compressed save. После любого исключения
    финализации не повторять её и не сохранять сомнительный результат.

## Изменённые файлы

Новые production файлы (4), относительно `SolastaUnfinishedBusiness/`:

- `Models/ClericProgressionRules.cs` — чистые mapping/cap/preparation/Spark/recovery правила и grant ledger.
- `Models/ClericProgressionContext.cs` — late transform/discovery/origins, cap/deadlock, scoped scaling/restart.
- `Models/ClericSpellcastingContext.cs` — WholeList slots/cantrips, preparation, domain clones, bonus choices/caster guards.
- `Models/ClericNativeProgressionPatches.cs` — узкий Intervention iterator adapter и readiness gate.

Изменённые production файлы (14):

- `Api/Helpers/MulticlassInOutRulesHelper.cs`
- `Behaviors/ModifyPowerPoolAmount.cs`
- `Displays/ClassesDisplay.cs`
- `Models/2024ClericContext.cs`
- `Models/BootContext.cs`
- `Models/Level20SubclassesContext.cs`
- `Models/SharedSpellsContext.cs`
- `Models/SpellPointsContext.cs`
- `Models/WizardProgressionContext.cs`
- `Patches/CharacterBuildingManagerPatcher.cs`
- `Patches/RulesetActorPatcher.cs`
- `Patches/RulesetCharacterHeroPatcher.cs`
- `Patches/RulesetCharacterPatcher.cs`
- `Settings.cs`

Тесты (3): `Tests/FighterProgression/ClericTests.cs`, `FighterProgression.Tests.csproj`, `Program.cs`.
Документация (1): этот файл. Итого22 файла:18 production,3 test,1 documentation.
Рекомендация: ограниченная игровая проверка по checklist; к обычной кампании и существующим saves
прототип пока не рекомендован. Release-ветка, worktrees, официальный release и новые классы не затронуты.
