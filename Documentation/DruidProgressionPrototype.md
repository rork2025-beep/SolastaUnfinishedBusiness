# Druid 20→12 — Stage9

Оценка: **PARTIALLY READY**. Реализован opt-in прототип base Druid, четырёх официальных и шести UB
Circles. Он подготовлен к контролируемому игровому тесту на новых героях. Unity/Harmony execution,
реальные превращения, бой, отдых, respec и save/load здесь не запускались. Конвертации Wild Shape ↔
slots/points в этой стадии разрешены только в обычном облике; общий mixed-caster режим не реализован.
Полная совместимость сохранений/ресурсных зеркал в игре не объявляется подтверждённой.

## Baseline, сборка и тесты

Использован существующий checkout `/workspace/SolastaUnfinishedBusiness`, ветка
`feature/progression-20-to-12`, исходный HEAD `5ae1cac31b8f392f86764395b1239eb9646ace89`.
Baseline повторно подтверждён: **2212 проверок**, Release Workflow — **0 ошибок / 0 предупреждений**.
Итог: **2606 проверок** (394 новых), Release Workflow — **0 ошибок / 0 предупреждений**.
SDK **9.0.320**; повторная установка зависимостей не понадобилась. Выходная DLL существует и не пуста:
`SolastaFiles/Mods/SolastaUnfinishedBusiness/SolastaUnfinishedBusiness.dll`.
`git diff --check` пройден. Артефакты, игровые DLL, Settings.xml и saves в коммит не включены.

Все предыдущие suites выполняются: Fighter, Barbarian, Monk, Rogue, Wizard, Sorcerer, Warlock,
Warlock6.1 transactions, Bard Magical Secrets и Cleric Channel Divinity/preparation.
Их тесты не изменены. Shared adapters расширены только scoped Druid-ветвями.
Paladin/Ranger/Inventor и другие новые классы не реализованы.

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

## Включение и обратимость

UB → Classes → **Druid 20 to 12 prototype (NEW HEROES; restart; SINGLE CASTER ONLY)**
(`EnableDruidCompressedProgressionPrototype`), затем полный перезапуск игры и новый тестовый герой.
Флаг выключен по умолчанию, использует существующие Settings/UI. Требуются уже включённые
`EnableLevel20` и `EnableMulticlass`; модуль сам их не меняет. Для отключения снять новый флаг и
полностью перезапустить игру. Это восстанавливает обычные definitions новой сессии, но не конвертирует
сохранение обратно. Обычные и compressed saves/герои не взаимозаменяемы.

Целевые пользовательские настройки из задания сохраняются, включая максимум3 классов, WIS/прочие
multiclass требования, Spell Points, ASI+feat, оба extra feat графика и racial feat первого уровня,
PCG/Epic Points, UI выбора наград и `EnableRespecAction=true`. Автоматической записи Settings.xml нет.
Личного Settings.xml здесь нет: реальные значения игровой сессии неизвестны. При активации в UB log
пишутся восемь Druid flags, режим prepared table/points, реально обнаруженные Circles и form catalogs.

Восьми исходным Druid definition switches запрещена повторная перестройка после активации.
Изменение mode/rule choices требует рестарт; inconsistent session блокирует повышение/финализацию и
следующее Wild Shape action. Прочие native обработчики читают живые Settings, поэтому между изменением
правил и рестартом продолжать игру нельзя. Latch защищает от двойной инициализации, включая opt-off.
Существующие Druid выше12 не мигрируются и не допускаются к дальнейшему повышению.

## Прогрессия, колдовство и подготовка

| Actual Druid | Original events / effective | Круг | Обычные slots по кругам | Cantrips |2024 prepared base | Spell Points base |
|---|---|---|---|---|---|---|
| 1 | 1 /1 | 1 | 2 | 2 | 4 | 4 |
| 2 | 2 /2 | 1 | 3 | 2 | 5 | 6 |
| 3 | 3 /3 | 2 | 4/2 | 2 | 6 | 14 |
| 4 | 4 /4 | 2 | 4/3 | 3 | 7 | 17 |
| 5 | 5 /5 | 3 | 4/3/2 | 3 | 9 | 27 |
| 6 | 6,7 /7 | 4 | 4/3/3/1 | 3 | 11 | 38 |
| 7 | 8,9 /9 | 5 | 4/3/3/3/1 | 3 | 14 | 57 |
| 8 | 10,11 /11 | 6 | 4/3/3/3/2/1 | 4 | 16 | 73 |
| 9 | 12,13 /13 | 7 | 4/3/3/3/2/1/1 | 4 | 17 | 83 |
| 10 | 14,15 /15 | 8 | 4/3/3/3/2/1/1/1 | 4 | 18 | 94 |
| 11 | 16,17 /17 | 9 | 4/3/3/3/2/1/1/1/1 | 5 | 19 | 107 |
| 12 | 18,19,20 /20 | 9 | 4/3/3/3/3/2/2/1/1 | 5 | 22 | 133 |

Таблицы взяты из installed UB/game, не заменены внешними tabletop предположениями. В частности,
исходный `CastSpellDruid` даёт пятый cantrip на17→actual11. Бонусные cantrips Circles/Primal Order
остаются отдельными от этого ordinary бюджета.

Сохраняется **WholeList / Prepared / Wisdom / LongRest**: без Wizard spellbook и Sorcerer known-spell
events. Legacy ordinary preparation = **max(1, effective Druid + WIS modifier + native affinity bonuses)**.
Например, effective20 с WIS20 без надбавок даёт25. Generic2024 table используется из прежнего UB
`ComputeMaxPreparedSpells` adapter с effective индексом; обычный WIS не добавляется к base22 ещё раз,
но native PB/WIS-based affinity modifiers сохраняются. Circle Balance modifiers поэтому нужно учитывать
отдельно. Domain/circle auto-prepared spells не расходуют ordinary preparation limit.

Слотные rows и Spell Points rows проецируются после окончательных source switches. Points133 берутся
из существующей UB таблицы, режимы не суммируются. Native цены, high-circle ограничения, upcast и
long-rest recovery сохранены; индексы caster tables ограничены0–20. Circle modifiers/temporary slot
grants идут прежним UB путём; `ConvertAdditionalSlotsIntoSpellPoints...` не заменён новой экономикой.
Natural Recovery Land получает effective class capital, сохраняя native max-slot/recovery modal правила.

Auto-prepared groups клонируются с исходными spells/custom subfeatures и сжатыми class thresholds;
вложенные Land biome choices сохраняют exclusion/union режим. До выбора Circle его features/spells
не выдаются. Сохраняются конкретные extended lists, pool overrides и ritual restrictions, без произвольного
открытия Wizard/Druid/Cleric списков. Повторные executable auto-prepared entries дедуплицируются;
source features и независимые origins остаются. Обычная смена подготовки после отдыха — native.

Actual Druid, total level, HP, PB и history не подменяются; `GetClassLevel` глобально не патчится.
LateLoad после всех UB/2024 и Cleric использует общие planners/transformer, сохраняет порядок original
events, repeated choices, upgrade references и custom behaviors. ASI original4/8/12/16/19→actual4/7/9/11/12,
extra feats original2/6/10/14/18 и racial first-level reward не сливаются. На12: Beast Spells18, ASI19,
Archdruid20 в исходном порядке. Independent choice pools имеют отдельные origin tags и native grants.

## Wild Shape: доступ, CR, длительность и ресурсы

Installed Wild Shape — **ClassLevelListSelection** с конкретными MonsterDefinition/requiredLevel,
а не универсальный CR ceiling. Модуль снимает финальный каталог base/Night, проверяет его границы,
использует тот же original-level доступ в UI и action/application validation. Monster definitions,
CR, движение, атаки, natural armor и HP не переписываются. Eagle flight, Spider climbing и Water
Elemental movement приходят с соответствующей установленной формой и её сроком доступа.
Другие Circles не получают Night elemental/monstrous choices.

| Форма PowerDruidWildShape | Original | Actual | CR | Uses |
|---|---|---|---|---|
| WildShapeWolf | 2 | 2 | 0.25 | 1 |
| WildshapeBlackBear | 4 | 4 | 0.5 | 1 |
| WildShapeBadlandsSpider | 4 | 4 | 0.5 | 1 |
| WildShapeBrownBear | 6 | 6 | 1.0 | 1 |
| WildshapeDirewolf | 6 | 6 | 1.0 | 1 |
| WildShapeGiant_Eagle | 8 | 7 | 1.0 | 1 |
| WildshapeDeepSpider | 8 | 7 | 2.0 | 1 |
| WildshapeTiger_Drake | 8 | 7 | 2.0 | 1 |
| WildShapeApe | 10 | 8 | 3.0 | 1 |
| WildShapeTundraTiger | 13 | 9 | 4.0 | 1 |

| Форма PowerCircleOfTheNightWildShapeCombat | Original | Actual | CR | Uses |
|---|---|---|---|---|
| WildShapeBadlandsSpider | 2 | 2 | 0.5 | 1 |
| WildshapeDirewolf | 2 | 2 | 1.0 | 1 |
| WildShapeBrownBear | 2 | 2 | 1.0 | 1 |
| WildshapeDeepSpider | 4 | 4 | 2.0 | 1 |
| WildShapeDireBear | 4 | 4 | 2.0 | 1 |
| WildShapeApe | 6 | 6 | 3.0 | 1 |
| WildshapeTiger_Drake | 8 | 7 | 2.0 | 1 |
| WildShapeGiant_Eagle | 8 | 7 | 1.0 | 1 |
| WildShapeTundraTiger | 10 | 8 | 4.0 | 1 |
| WildShapeAirElemental | 10 | 8 | 5.0 | 2 |
| WildShapeFireElemental | 10 | 8 | 5.0 | 2 |
| WildShapeEarthElemental | 10 | 8 | 5.0 | 2 |
| WildShapeWaterElemental | 10 | 8 | 5.0 | 2 |
| WildShapeCrimsonSpider | 14 | 10 | 5.0 | 2 |
| WildShapeMinotaurElite | 14 | 10 | 5.0 | 2 |


Это10 base entries и15 Night entries с пересечениями, не25 уникальных монстров.
Night choices не появляются до получения Combat Wild Shape на actual2/3 по выбранным правилам.
При двух-use форме нужны обе доступные единицы **до** action; проверка повторяется до мутации героя.
На application boundary повторно проверяются source catalog/доступ; первый native charge к этому моменту
уже мог произойти. Чужой Shapechange/Polymorph через эти каталоги не разблокируется.

Длительность `HalfClassLevelHours` вычисляет native `ComputeRoundsDuration` с effective Druid,
а не с Fighter/Rogue/total level: по исходной формуле до10 часов при effective20. Формовые HP остаются
установленными creature HP; обычные HP героя не перезаписываются.2024 временные HP = effective Druid
и выдаются один раз после подтверждённой формы. Feat Awaken the Beast Within получает свой исходный
class-based множитель от effective Druid; native TempHP replacement rules сохраняются, не складываем
бонусы вручную. WIS/CHA/PB-based ресурсы и native summon proficiency rules не виртуализируются до20.

Legacy base pool —2 uses с acquisition2, ShortRest; Night разделяет именно этот pool.
В2024 base и Cosmos Starry Form получают исходные дополнительные uses на6/17→actual6/11:
**2/3/4 uses**. Base transformation BonusAction вместо Action, LongRest recharge, плюс исходный UB
short-rest recovery одной единицы. Для Cosmos начального Circle2 исправлен scoped recovery threshold:
в прототипе он не требует ошибочно Circle≥3. Starry Form остаётся отдельным pool со штатной взаимной
связью с Wild Shape, не превращается в третий бесплатный ресурс.

Обнаружено: UB dispatch вызывает только первый `IPowerOrSpellFinishedByMe`; Night callback маскирует
добавленный2024 HP callback. Кроме того, callbacks ожидают разные ссылки actor до/после замены.
В прототипе перед native charge снимается journal одного action. Общий completion находит реальную
новую форму по original hero/monster, списывает ровно1/2 из snapshot, синхронизирует hero/form mirrors,
однократно выдаёт2024 TempHP. Pure production journal защищает finish/refund от повторных callbacks.
Prefix запускается раньше существующего UB replacement `ExecuteImpl`; inherited UsePower execution
патчится на правильном declaring type с проверкой конкретного action, прочие magic actions пропускаются.

Concentration, перенос условий, native abilities/proficiencies, добровольное/forced reversion и смерть
формы используют существующие UB/игровые обработчики. Они не переписаны целиком и требуют Unity-теста.
При failed/cancelled form application журнал не придумывает успех или бесплатный refund.
Полного rollback effects/position/условий после native исключения нет; повреждённый тест не сохранять.

## Beast Spells, Spell Points в форме и Archdruid

Beast Spells18→12 открывает штатный casting path **только для распознанного Wild Shape** и сохраняет
прочие native запреты, включая rage/components/concentration. Никакого универсального `CanCastSpells=true`
для всех transformations нет. Для формы projected spell tables индексируются **actual Druid**,
а не total CharacterLevel и не effective повторно: Druid5/Fighter12 не получает casting17/20.

Supported Wild Shape использует один Spell Points pool исходного героя. Native form mirror связывается
с тем же usable-power объектом, чтение/расход и repertoire caster resolution адресуют original hero.
Binding повторяется на form refresh, включая путь после load; повторное обращение не заменяет уже
связанную запись. Это предотвращает отдельный бюджет формы/возврат потраченных points от stale mirror
по замыслу реализации. Реальная сериализация/reversion этой связи ещё не проверена в Solasta.

Legacy Archdruid20→12 сохраняет исходные hands-full casting modifiers и возврат полного Wild Shape
pool после успешной base/Night transformation. Refund journal обновляет оба mirrors один раз.
Это не отдельный бесконечный счётчик и не автоматический refund за Wildfire Spirit/Bark Ward/Starry Form.
Если pool исчерпан другими расходами, действуют исходные ограничения использования power.

2024 Archdruid заменяет legacy feature выбранным source FeatureSet: Evergreen восстанавливает1 WS
при initiative, если pool0; Nature Magician — исходный LongRest pool и slot2/4/6/8 grants за1/2/3/4 WS.
Сохраняется проверка достаточного WS; если Wildshape2024 выключен и максимум2, варианты с ценой3/4
не становятся доступными автоматически. Оба capstones одновременно не выдаются.

Wild Resurgence5: slot→1 WS только при0 WS, native TurnStart limit и фактическая оплата lowest available
slot/его UB points cost; недостаточные points отклоняются. Обратное направление —1 WS за временную
slot1 grant, native LongRest limit. Slots/points/WS не создаются за Skip/нулевую цену. Nature Magician
также сохраняет свой LongRest limit. **Ограничение:** все эти конвертации требуют обычного облика;
прототип явно запрещает их в substitute form до отдельной игровой проверки. Общая конверсионная
транзакция при произвольном native exception не реализована.

## D&D2024 и Circles

| Setting | Поддержанное source поведение |
|---|---|
| `EnableDruidArchDruid2024` | Legacy refund/affinity либо Evergreen/Nature Magician20→12 |
| `EnableDruidElementalFury2024` | Exclusive Potent Spellcasting/Primal Strike7→6, improvement15→10; source damage types, WIS, once-per-turn/cost/weapon restrictions и increased cantrip range |
| `EnableDruidMetalArmor2024` | Исходный metal forbidden-tag switch, без изменения других классов |
| `EnableDruidPrimalOrder2024` | Magician/Warden1: одна choice, отдельный cantrip/INT skill bonus либо medium armor/martial proficiencies |
| `EnableDruidWeaponProficiency2024` | Исходный simple-weapons либо прежний Druid list |
| `EnableDruidWildResurgence2024` | Существующие два conversion powers на5, с описанными guards |
| `EnableDruidWildshape2024` | Action/recharge/extra uses/TempHP и Cosmos recovery вариант |
| `EnableDruidToLearnCircleAtLevel3` | Circle2 либо3 и final initial unlocks до компрессии |
| Generic prepared/ritual rules | Существующие таблицы/native permissions, без Wizard mechanics |

Discovery читает реальные class choices и включённые UB registrations; hidden/disabled Circles не
активируются. Ниже перечислены все10 найденных implementations. Для всех сохранены spell lists,
independent choices, powers/custom callbacks, source costs/rest и feature replacement references.

| Circle | Покрытие и специальные механики | Что ещё проверить в игре |
|---|---|---|
| Official CircleBalance | Auto-prepared/Gift of Life2/3; Survival of Wisest6; Cold Embrace10→8; Balance of Power14→10; WIS/preparation/healing modifiers остаются native | Подготовка с affinity bonus, healing/damage |
| Official CircleKindred | Spirit choice/call/bond2/3; Magical Spirit6; Shared Pain10→8; Rage14→10; исходные creature references, summon conditions и ресурсы | Выбранный companion, HP/PB, действия, смерть/перепризыв/persistence |
| Official CircleLand | Bonus cantrip/Natural Recovery2/3; отдельный biome spell choice3; Land's Stride6; Nature's Ward10→8; Sanctuary14→10; recovery capital по effective class | Все biome lists, независимые cantrip/choice pools, отдых и slots/points recovery |
| Official CircleWinds | Carried by Wind2/3; Sheltering Breeze6; Guiding Winds10→8; Unfettered14→10; powers/reactions/conditions | Movement, реакции и recharge |
| UB CircleOfTheAncientForest | Bonus cantrip/auto-spells/Regrowth2/3; Herbal Brew/Life Sap6; Photosynthesis/Rooted10→8; Barkskin/Wall of Thorns14→10; Regrowth5×effective subclass, Life Sap effective healing | Consumable brews, WIS/PB uses, healing caps, rest, conditions |
| UB CircleOfTheCosmos | Constellation Map/Starry pool/Archer-Chalice-Dragon2/3; Omen6; Twinkling Stars10→8; Nova Star14→10; direct10/14 thresholds и class dice адаптированы; отдельный linked Starry pool | Выбор/смена Starry Form, short/long rest, mutual WS расход без удвоения, buffs и concentration |
| UB CircleOfTheForestGuardian | Bark Ward/auto-spells/Sylvan Durability2/3; Extra Attack/cantrip replacement6; improved Ward10→8; superior14→10; shared WS и TempHP4/6/8/10 по source tiers | WS стоимость, TempHP, retaliation/ally effects, Extra Attack |
| UB CircleOfTheLife | Auto-spells/Verdancy2/3; Seed of Life6; Revitalizing Boon10→8; Harmonious Bloom14→10; GUID-based class healing и direct thresholds адаптированы, исходный healing war-list modifier | Source GUID во время формы/load, persistent healing conditions, rest и spell modifiers |
| UB CircleOfTheNight | Combat Wild Shape2/3; magical Primal Strike6; healing1/2/3/4d8 на2/3,6,8,10 с реальным PB-based uses; elemental10→8, monstrous14→10; общий WS cost1/2 | Полный каталог, action economy, exact2 cost, damage/saves, возврат/forced reversion, legacy/2024 TempHP |
| UB CircleOfTheWildfire | Auto-spells/Summon Spirit2/3 с WS shared cost; Enhanced Bond6; Cauterizing Flames10→8; Blazing Revival14→10; spirit class-sourced HP/damage effects, real PB/spell attack и persistence restrictions | Spirit commands/teleport, HP/damage, unconscious/death, перепризыв и cleanup после отдыха/respec |

## Multiclass, respec, сохранения и ограничения

Разрешены eligible неколдующие Druid12/Fighter8, Druid12/Rogue8, Druid8/Fighter6/Rogue6.
Сохраняются native WIS/остальные prerequisites, total cap20 и настроенный максимум классов3.
Druid13 блокируется в enumeration/AssignClass/AddClass. Все предыдущие caster guards остаются:
Druid+Cleric/Wizard/Ranger/Sorcerer/Warlock/прочие casters и casting subclasses/нерacial casting feats
запрещены до будущей shared-magic стадии. Circle bonus spells не считаются вторым repertoire.
При Druid12 без legal alternative повышение блокируется с диагностикой, без автоматического WIS13
и без пустого принятого level-up. Fighter/Rogue уровни не усиливают Druid forms/casting/duration.

Повышение требует выхода из physical/Starry Form. Respec также требует окончания Starry Form,
возврата в обычный облик и удаления активных summon effects/companions; guard срабатывает до создания
replacement hero. Он консервативен и может потребовать отменить продолжающийся summon/proxy spell.
Это предотвращает очевидный перенос старых form/pet states, но не доказывает общую чистоту respec:
native UB переносит часть условий между героями. Нужно сверять pools, spent uses, class/circle choices,
prepared/circle spells, Spell Points, TempHP, remaining conditions и GUID sources после каждой попытки.

Автоматической save migration нет. World-level rollback, multiplayer, конкурирующие Harmony mods,
изменения form catalog после LateLoad, произвольные transformations и serialized alias/journal behavior
не подтверждены. Конвертации в форме заблокированы. Старые saves и обычная кампания не рекомендуются.
После исключения native финализации/превращения не повторять операцию на повреждённом состоянии и
не сохранять результат; загрузить исходный тестовый save. Это основания статуса PARTIALLY READY.

## Что проверено автоматически

Production rules, shared planners/transformer на managed doubles, spell-grant ledger и shape-use journal
исполняются без Unity: mapping, source inventories base+10 Circles, order/repeated awards/overrides,
source immutability, slots/points/preparation/cantrips, auto-prepared groups/biomes, каталог25 entries
в двух независимых списках, original eligibility на каждом actual уровне, Night cost2, WS2024 extras,
Resurgence paid/zero-use gates, once-only finish/refund, Starry guards и opt-off/init latch.
32 модельные комбинации пяти acquisition flags проходят production transformer. Это не игровые
прогоны32 профилей и не execution всех source switches.

Отдельные JSON/Cecil assertions читают actual registrations/creature HP/CR/definitions/tables,
восемь2024 switches, callbacks/recovery/conversions, Circle GUID/threshold adapters, Beast/Arch source,
declaring types/arguments/priority Harmony и места shared интеграции. **Это metadata checks**,
не доказательство выполнения Unity/Harmony, GUI, resource mirrors, отдыха или сохранений.

## Подробный игровой checklist

1. Отдельная тестовая кампания, сначала fresh normal Druid при opt-off после restart: ordinary unlocks,
   preparation/WS и рост выше12. Затем целевые настройки из задания, opt-in, полный restart, fresh Druid
   с WIS≥13 и характеристиками для martial выхода. Проверить actual rule values/catalog/Circles в логе.
   Никаких existing normal saves в compressed режиме.
2. По одному повышению1–12 сверять таблицу, actual class/total levels, HP/PB/history, cantrips и preparation.
   Записать независимые ASI/feats/Primal Order/Circle/biome choices. На12 отдельно Beast18, ASI19, Arch20.
   Использовать Back/Reset/Skip/Cancel на многонаградных6/8/10/11/12, повторить повышение без дубликатов.
3. Fresh legacy/table2024 preparation и slots/points профили: WIS20 на12 даёт25 либо base22 плюс source
   affinities. Domain/circle auto spells не занимают ordinary budget; после long rest менять подготовку,
   отменять и повторять без потерь. Проверить ritual/extended list ограничения. Land — каждый biome.
4. На11 проверить9 круг, на12 full slots либо133 base points, цены/upcast/high-circle once limits,
   short/long rest, Circle modifiers и Natural Recovery. Ни Wizard book, ни Sorcery/Pact pools не появляются.
5. Base WS каталог по таблице: wolf2; bears/spiders4/6; eagle и tier8 наactual7; ape8; tundra9.
   Проверить CR, flight/climb/swimming, стандартные creature HP/AC/атаки/saves. Night формы не доступны
   другому Circle; Night до выбора на3 отсутствует в circle3 режиме.
6. Night: на4 Dire Bear;6 Ape;7 Eagle;8 Tundra и4 elementals;10 Crimson Spider/Minotaur. Special формы
   с2 units, при1 unit выбор/command отклоняется. После успешного превращения ровно2 списания, одинаковые
   hero/form counters, повторный callback не списывает ещё1. Проверить все4 healing tiers и реальные PB uses.
7. Legacy WS:2 uses и full short-rest recovery.2024:BonusAction, uses2/3/4 на2/6/11, LongRest и1 use short
   rest. TempHP = effective Druid (например7 наactual6,20 на12), не actual total и не второй бонус Night.
   С feat Awaken Beast проверить native замещение TempHP, не сумму. Duration по effective Druid,
   не по martial levels; пройти несколько часов, закончить форму добровольно и истечением времени.
8. Cosmos: Circle2 и3 с Wildshape2024 off/on, Starry short/long rest и1 use short-rest recovery с самого
   Circle2 в прототипе. Archer/Chalice/Dragon, thresholds10/14→8/10, Omen/Nova, взаимный расход WS/Starry
   и отсутствие второго бесплатного pool. Завершить форму перед повышением/respec.
9. С подготовленным concentration spell войти в WS, проверить сохранение concentration/proxy effects,
   получение урона/Con saves, mental proficiencies/настоящий PB. На11 Beast Spells ещё нет, на12 есть:
   проверить разрешённый spell, components, action cost, rage запрет. Polymorph/Shapechange не получают
   универсального разрешения из этого адаптера.
10. В points профиле на12 записать pool133, превратиться, потратить spell costs, вернуться: points не
    восстановились и не списались дважды. Повторить форму, принудительное снятие на0 HP и save/load в форме.
    Проверить high-circle usage counters, отсутствие второго бюджета/regen от stale mirror. При расхождении
    остановить тест и загрузить исходный save. Для slots профиля аналогично сверить used slots/repertoire.
11. Resurgence в humanoid: только0 WS разрешает slot→1 use; native lowest slot/точные points оплачены,
    нет покупки при недостаточных points; повтор ограничен TurnStart. Обратная slot1 grant требует1 WS,
    разрешена один раз LongRest; проверить повтор после отдыха, без free loop. В форме оба направления
    отклоняются до расхода. Nature Magician на12 проверить2/4/6/8 за1/2/3/4 WS и общий LongRest limit.
12. Legacy Arch на12: после обычной/Night формы весь WS pool восстановлен один раз, оба mirrors равны;
    расход Spirit/Bark Ward/Starry сам по себе не делает refund.2024 Arch: legacy отсутствует, Evergreen
    восстанавливает ровно1 при initiative и0 uses, Nature Magician как выше. Проверить смешанные независимые
    Arch/Wildshape flags; дорогие subpowers не обходят доступный maximum.
13. Для всех10 Circles матрицы создать отдельных героев: every middle/late feature, spells/choices,
    HP/heal/damage/costs/recharge/conditions. Kindred — commands, bond, companion death/resummon;
    Wildfire — Spirit HP/class effects/teleport, unconscious/death/persistence/Revival; Life — GUID healing
    в обычном/animal облике и после load; Ancient Forest — Regrowth pool/Life Sap/brews/Wall of Thorns;
    Forest Guardian — Ward TempHP/retaliation/shared WS; Balance/Winds — все native реакции и modifiers.
14. Martial combinations из задания: actual total≤20/classes≤3, Druid13 отсутствует, Fighter/Rogue levels
    не повышают forms/casting. Особенно Druid5/Fighter12 в форме: только progression5, не17/20.
    Mixed caster/casting subclass/feat блокируются, circle extra spells — нет. Druid12 без WIS13/legal
    alternative получает объяснимую блокировку без изменения атрибутов/пустой принятой стадии.
15. При Respec=true сначала попытка с WS/Starry/companion: guard до создания героя; затем revert/end forms,
    dismiss summons, записать choices/known+prepared+circle spells/pools/used counters/TempHP/conditions.
    Respec в тот же Circle, другой Circle и safe другой класс; повторить, отменить посредине, сверить GUIDs
    и отсутствие старых pets/conditions/resources. Native respec cleanup не объявлять совместимым до проверки.
16. Save/load humanoid и каждой особой формы/Spirit, затем отдых/бой/следующее повышение. Проверить
    reversion/death/концентрацию/счётчики. Изменить правило/mode в сессии: restart требуется; не продолжать
    игру до него. Выключение проверять на другом fresh normal герое, не мигрировать compressed save.

## Изменённые файлы

Новые production файлы (4):

- `SolastaUnfinishedBusiness/Models/DruidProgressionRules.cs`
- `SolastaUnfinishedBusiness/Models/DruidProgressionContext.cs`
- `SolastaUnfinishedBusiness/Models/DruidSpellcastingContext.cs`
- `SolastaUnfinishedBusiness/Models/DruidWildshapeContext.cs`

Изменённые production файлы (26):

- `SolastaUnfinishedBusiness/Api/GameExtensions/RulesetSpellRepertoireExtensions.cs`
- `SolastaUnfinishedBusiness/Api/Helpers/MulticlassInOutRulesHelper.cs`
- `SolastaUnfinishedBusiness/Behaviors/ModifyPowerPoolAmount.cs`
- `SolastaUnfinishedBusiness/Displays/ClassesDisplay.cs`
- `SolastaUnfinishedBusiness/Feats/ClassFeats.cs`
- `SolastaUnfinishedBusiness/Models/2024DruidContext.cs`
- `SolastaUnfinishedBusiness/Models/BootContext.cs`
- `SolastaUnfinishedBusiness/Models/Level20Context.cs`
- `SolastaUnfinishedBusiness/Models/SharedSpellsContext.cs`
- `SolastaUnfinishedBusiness/Models/SpellPointsContext.cs`
- `SolastaUnfinishedBusiness/Models/ToolsContext.cs`
- `SolastaUnfinishedBusiness/Models/WizardProgressionContext.cs`
- `SolastaUnfinishedBusiness/Patches/CharacterBuildingManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetActorPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterHeroPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetCharacterPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetEffectPowerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetImplementationManagerLocationPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/RulesetImplementationManagerPatcher.cs`
- `SolastaUnfinishedBusiness/Patches/ShapeSelectionPanelPatcher.cs`
- `SolastaUnfinishedBusiness/Settings.cs`
- `SolastaUnfinishedBusiness/Subclasses/CircleOfTheAncientForest.cs`
- `SolastaUnfinishedBusiness/Subclasses/CircleOfTheCosmos.cs`
- `SolastaUnfinishedBusiness/Subclasses/CircleOfTheForestGuardian.cs`
- `SolastaUnfinishedBusiness/Subclasses/CircleOfTheLife.cs`
- `SolastaUnfinishedBusiness/Subclasses/CircleOfTheNight.cs`

Тесты (3): `Tests/FighterProgression/DruidTests.cs`, `FighterProgression.Tests.csproj`, `Program.cs`.
Документация (1): этот файл. Итого34 файла:30 production,3 test,1 documentation.
