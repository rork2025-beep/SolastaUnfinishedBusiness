# Stage 3 — Rogue 20→12

**PARTIALLY READY.** Немагическая прогрессия готова к контролируемому игровому тестированию.
Полная магическая прогрессия ShadowCaster/Arcane Scoundrel отложена явно. В игре этот этап
не запускался. Основные сохранения не использовать.

## Проверка предыдущих этапов

Перед изменениями: ветка feature/progression-20-to-12, HEAD bd004f9283; Fighter, Barbarian,
Monk сохранены в коммитах. Предыдущие 237 проверок и Release Workflow прошли с нулём ошибок
и предупреждений. Использован существующий checkout, без worktree и новой системы прогрессии.

## Реализация

Отдельный выключенный по умолчанию EnableRogueCompressedProgressionPrototype использует
тот же CompressedProgressionDefinitions и карту 1,2,3,4,5,7,9,11,13,15,17,20. Преобразование
происходит после финальных UB/2024 definitions, Spell Points и предыдущих прототипов.
Сохраняются события, исходный порядок, отдельные повторные пулы/их происхождение и ссылки
на улучшения. Первые пять уровней сохраняют идентичность для штатных фильтров multiclass.

Sneak Attack не выдаётся заново при каждом увеличении кубов. Единственный исходный provider
получает эффективный классовый ранг через существующий damage dispatcher. Обычная таблица
даёт на фактических уровнях1–12: 1,1,2,2,3,4,5,6,7,8,9,10 d6. Частота OncePerTurn,
FinesseOrRangeWeapon, преимущество/помощь союзника и критические модификаторы не переписаны.
Это не OncePerRound: атака реакцией на чужом ходу проверяется по исходным правилам UB.

Expertise, Cunning Action, Uncanny Dodge, Evasion, Reliable Talent, Blindsense, Slippery Mind,
Elusive18→12 и Stroke of Luck20→12 выдаются по карте с учётом текущих замен UB. Rogue получает
шесть ASI исходных4/8/10/12/16/19 → фактические4/7/8/9/11/12. При целевых ASI+feat и обоих
extra-feat options сохраняются 12 классовых feat-очков и 12 очков характеристик, отдельно от
начального бонуса1/расы/подкласса. Специальные два bonus-feat очка исходного Rogue10 остаются
отдельным пулом, несмотря на соседство ASI+feat того же исходного уровня.

Порог Improved Cunning Strike11→8, Devious Strike14→10. Уровневые формулы дополнительных
повреждений, powers, условий и invocation prerequisites маршрутизируются для Rogue отдельно.
Адаптированы BladeCaller13, Opportunist, UmbralStalker healing, ArcaneScoundrel19 и внешние
проверки Duelist13, Slayer Chain of Execution и ArcaneScoundrel в FixesContext.

Фактические уровни, hit dice, HP, общий PB, история не меняются. В частности, явно заданное
CharacterLevel-усиление Essence Theft Arcane Scoundrel оставлено зависимым от общего уровня:
не каждый источник CharacterLevel является ошибкой классовой прогрессии.

## 2024: фактический выбор правил

Модуль НЕ включает автоматически ни один Rogue2024-флаг. Читает готовую базу после
Tabletop2024Context; при активации пишет в лог реальные значения:
EnableRogueReliableTalent2024, EnableRogueSlipperyMind2024, EnableRogueSteadyAim2024,
EnableRogueCunningStrike2024. Также учитывается RemoveRogueBlindSense2024.

Без запуска игры активный личный Settings.xml здесь не установлен. Пресеты репозитория
empty/hiddenHax/zappastuff различаются и не считаются доказательством личной конфигурации.

- Reliable Talent2024 исходный7→фактический6; старый вариант11→8.
- Slippery Mind меняет proficiency по текущему флагу, исходный15→10.
- Steady Aim при включении остаётся на3.
- Cunning Strike остаётся на5; улучшение11→8; Devious14→10.
- Существующий код расходов берёт CostPerUse выбранных powers, создаёт ConditionReduceSneakDice
  и уменьшает конечные sneak dice. Меняется только источник классового уровня/порог выбора,
  а не условия срабатывания, цены, число Sneak Attack применений или механизм критов.

Disarm, Poison, Trip, Withdraw, Daze, KnockOut/Obscure, отмена реакции и расход нескольких
вариантов должны быть проверены в игре. Тесты метаданных не доказывают их боевое выполнение.

## Подклассы

Алгоритм использует subclass choices Rogue, регистрацию SubclassesContext и реальные
CharacterSubclassDefinition, исключая Hidden. Никакого включения скрытых/отключённых опций.

| Подкласс | Покрытие / ограничение |
|---|---|
| RoguishDarkweaver | Unlocks, powers, замены и классовое масштабирование; игровой прогон нужен |
| RoguishHoodlum | То же; исходный non-finesse Sneak provider и ограничения сохранены |
| RoguishShadowCaster | Feature unlocks; **полное spellcasting не сжато** |
| RoguishThief | Unlocks и поздние способности, исходная экономика действий |
| RoguishAcrobat | Unlocks, selectable features/powers |
| RoguishArcaneScoundrel | Feature unlocks, эффекты/порог19; **полное spellcasting не сжато** |
| RoguishBladeCaller | Классовый урон/ранги, Blade Surge13 и остальные unlocks |
| RoguishDuelist | Исходные условия Daring Duel, порог13, unlocks |
| RoguishOpportunist | Классовые формулы/поздние улучшения |
| RoguishRavenScion | ClassLevel damage provider, unlocks |
| RoguishSlayer | Chain of Execution и ранги13/17, unlocks |
| RoguishSwashbuckler | Зарегистрированный builder, unlocks/исходные условия 1v1 |
| RoguishUmbralStalker | Deadly Shadows metadata, классовое лечение, unlocks |

Для Swashbuckler нет диагностического JSON в текущем репозитории. Автотест отдельно проверяет
скомпилированную регистрацию и четыре группы выдачи конструктора; не выдаёт это за игровой
тест всех его наград. Во время реальной активации общий transformer проверяет листовые
награды всех фактически найденных подклассов, в том числе Swashbuckler.

## Явный предел поддержки магии

Для ShadowCaster и ArcaneScoundrel сохранены ссылки на CastSpell definitions, принадлежность
репертуаров и классификация OneThird в существующем SharedSpellsContext. Модуль не делает
их full caster и не выдаёт 9-й круг. Эти определения намеренно не заменены новыми репертуарами.

НЕ РЕАЛИЗОВАНО: полное преобразование slots/known/scribed/replacement/prepared/auto-prepared
таблиц, общих multiclass caster levels, интерфейса выбора заклинаний и Spell Points для
эффективного Rogue20. Магические таблицы продолжают использовать обычную фактическую
прогрессию. Поэтому обещание полной прогрессии магии к Rogue12 НЕ выполняется.

Ограничение видно в названии UI-переключателя и startup-логе. Для текущих игровых проверок
предпочтительны немагические подклассы; magic subclasses — только отдельные исследовательские
копии. Это конкретная зависимость следующей общей магической фазы, а не скрытая готовность.

## Активация и отключение

1. Отдельная тестовая установка/профиль, НОВЫЙ персонаж и отдельные сохранения.
2. UI UB → страница классов → `Rogue 20 to 12 prototype (NEW TEST HEROES; restart; caster magic NOT compressed)`.
3. Включить, сохранить штатным интерфейсом UMM и полностью перезапустить игру.
4. Проверить лог: `Rogue 20-to-12 TEST prototype active`, список подклассов, выбранные2024
   значения и сообщение о несжатой магии. EnableLevel20/EnableMulticlass должны быть
   включены пользователем заранее; иначе модуль не активируется.
5. Для выключения снять флаг, сохранить, перезапустить; проверять обычный режим на
   исходной копии/новом герое. Миграция сжатого сохранения в обычный режим не реализована.

Другие options, личный Settings.xml и сохранения кодом не переписываются. Не менять режим,
Rogue2024/feats/multiclass options или видимость подклассов без перезапуска. Rogue13 запрещён
в списке классов и обоих путях назначения уровня только при активном режиме. Требования
входа/выхода сохраняются. Нет допустимых альтернатив после12 — повышение блокируется с
диагностикой, а не открывает пустой список. Существующие Rogue>12 не конвертируются и не
могут продолжить повышение в этом режиме; старые кампании загружать с выключенным режимом.

## Respec

Дополнительных постоянных изменений BaseValue у Rogue не добавляется. Общая инфраструктура
очищает временные пулы по исходному class/subclass тегу на ClearPrevious и удалении наград.
Одинарная инициализация предотвращает повторное преобразование definitions.

Не доказаны без игры: удаление старых Expertise/подклассовых способностей, правильный откат
выборов, отсутствие дубликатов репертуаров после respec, реакционные состояния Cunning Strike,
save/load. Система respec UB и ранее отмеченные проблемы переноса ASI/Spell Points не
переписывались. Не заявляется исправление всех таких ошибок.

## Проверки

Команды из корня с подготовленными переменными окружения:

```bash
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
git diff --check
```

Реальные managed-тесты используют общий production-transformer с doubles списков/definitions:
сохранение событий и порядка, неизменность источников, ранги по исходной таблице, Expertise,
специальные Rogue ASI/feat и origins, однократная/выключенная инициализация. Метаданные отдельно
проверяют регистрацию, Harmony targets/аргументы, существующие eligibility/frequency значения,
дедукцию Cunning dice и наличие актуального option gate. Это не заменяет боевые/GUI-тесты.

Финальный прогон: 307 проверок прошли, 0 проваленных; включены прежние 237 и 70 новых.
Release Workflow: 0 ошибок, 0 предупреждений. Это не игровой прогон. SHA и push — в отчёте задачи.

## Игровой чек-лист

- Сначала выключенный режим: новый контрольный Rogue, исходные уровни/выборы, Rogue13 доступен.
  Затем новый сжатый герой с Dexterity>=13 и подходящими характеристиками других классов.
- Уровни до12: сравнить effective1/2/3/4/5/7/9/11/13/15/17/20. Проверить все шесть ASI,
  специальные bonus feats10, оба Expertise, Elusive и Stroke of Luck. Назад/вперёд, отмена
  повышения и повтор выбора не должны создавать дополнительные награды.
- Sneak: по уровням до10d6; оружие finesse/ranged, advantage/neutral+ally/disadvantage;
  без подходящего оружия обычный unarmed НЕ становится Sneak. Проверить второй удар того
  же хода, реакцию на другом ходу и крит по активным UB-настройкам. Для Hoodlum отдельно
  проверить исходные subclass-исключения без распространения на всех Rogue.
- 2024: отдельные новые герои с Cunning off/on; цены1/2/5 и комбинации только в соответствии
  с реальными powers; один/два выбора на5/8, Devious на10. Проверить отмену, промах,
  исчерпание dice, KnockOut/Withdraw и отсутствие двойного Sneak/двойного списания.
- Rogue12/Fighter8 при обоих режимах: фактический20, Fighter effective11 и три атаки через
  ForceIfBetter; Action Surge не добавляет второй Sneak в тот же ход. Fighting styles и
  Weapon Mastery сохраняют исходные требования.
- Rogue12/Monk8: фактический20, Monk effective11; отличать monk weapon от unarmed для Sneak;
  проверить конкуренцию бонусных действий, Evasion, движение/экипировку и Focus отдельно.
- Rogue12/Barbarian8: STR-атака подходящим finesse оружием, Reckless, Rage и Uncanny Dodge;
  исходные ограничения брони и оружия, без добавления Sneak к любому удару.
- Rogue8/Fighter6/Monk6: фактический20 и три класса; effective11/7/7, отдельные ресурсы,
  максимум атак вместо суммирования, все награды на своих событиях.
- Отдельный deadlock при запрете выхода из Rogue12: сообщение/лог, без изменения требований
  или пустого незавершаемого экрана.
- Все13 активных подклассов: новые герои, поздние способности, реакции/замены. Магические
  два — с явной фиксацией несжатых spell tables, без ожидания caster20.
- Сохранение/закрытие/загрузка на8/12 и после второго класса. Три respec подряд между
  подклассами, отмена respec; проверить Expertise, feats, опции, Sneak и старые репертуары.
- Выключение с перезапуском и новые контрольные Fighter/Barbarian/Monk/Rogue.

При ошибках выбора/загрузки/накоплении наград остановить испытание на копии и сохранить лог.

## Изменённые файлы

Новые: Models/RogueProgressionContext.cs, Models/RogueProgressionRules.cs,
Tests/FighterProgression/RogueTests.cs, этот документ.

Интеграция: Settings.cs, Displays/ClassesDisplay.cs, Models/BootContext.cs,
Api/Helpers/MulticlassInOutRulesHelper.cs, Patches/CharacterBuildingManagerPatcher.cs,
Patches/RulesetCharacterHeroPatcher.cs, Patches/RulesetActorPatcher.cs,
Behaviors/Specific/_GLBM.cs, Behaviors/UpgradeEffectDamageBonusBasedOnClassLevel.cs,
Definitions/InvocationPoolTypeCustom.cs, Models/2024RogueContext.cs, Models/FixesContext.cs,
Subclasses/RoguishArcaneScoundrel.cs, RoguishBladeCaller.cs, RoguishOpportunist.cs,
RoguishUmbralStalker.cs и существующие Program.cs/тестовый csproj.

Wizard, Sorcerer, Warlock и остальные новые классы не реализовывались.
