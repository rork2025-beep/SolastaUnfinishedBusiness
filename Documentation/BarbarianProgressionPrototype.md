# Stage 1 — Barbarian 20→12

Статус: готов к контролируемому игровому тестированию, не к основной кампании.
Игра в Codex не запускалась. Все результаты ниже относятся к сборке, чистому планировщику,
модели определений и чтению метаданных, а не к прохождению игровых сценариев.

## Сохранение Fighter

До разработки Barbarian повторены все 30 проверок Fighter и сборка Release Workflow:
0 ошибок, 0 предупреждений. Checkpoint: `6c226e662c` —
`Preserve opt-in Fighter 20-to-12 progression prototype`, отправлен в ветку
`feature/progression-20-to-12`. Исходный переключатель Fighter сохранён. Его имена
генерируемых определений и тегов не менялись при выделении общей инфраструктуры.

## Охват

Отдельный выключенный по умолчанию переключатель Barbarian. Карта эффективных уровней:
`1,2,3,4,5,7,9,11,13,15,17,20` для фактических классовых уровней 1–12.

Сохраняются все события выдачи, порядок и ссылки на цепочки замен. Пулы выбора получают
различимые определения и теги исходного события. Общий преобразователь проверяет
количество и порядок листовых наград до замены списков. Для клонируемых пулов и наборов
копируется внешний реестр пользовательских subfeatures UB: Unity-клон сам его не копирует.

Уровни персонажа, история, hit dice, прирост HP за фактический уровень и proficiency bonus
не заменяются виртуальными уровнями. Увеличение Constitution от capstone законно влияет
на HP через штатные механизмы; это не выдача дополнительных классовых уровней.
Другие классы не сжимаются (кроме уже существующего независимого прототипа Fighter).

Адаптированы:

- Rage: последовательность добавок uses и damage, обычные/persistent powers и их замены.
  Нормальный лимит UB получается 6 uses и +4 damage к фактическому уровню 11.
- Reckless Attack, Danger Sense, Extra Attack, Fast Movement, Feral Instinct: штатные
  определения выдаются по сжатой карте; исходные уровни 1–5 остаются без подмены ссылок.
- Brutal Critical при правилах 2014 либо Brutal Strike и улучшения при правилах 2024.
  Исходные пороги 9/13/17 переходят в 7/9/11; два выбора Brutal Strike открываются на 11.
- Instinctive Pounce 7→6; Relentless Rage 11→8, его лечение 2024 берёт эффективный
  уровень (на фактическом 12 — исходный 20, то есть классовая часть лечения 40).
- Persistent Rage 15→10, его штатные правила восстановления и продолжительности 2024.
- Indomitable Might 18→12, Primal Champion 20→12, исходный ASI19→12.
- ClassLevel-ранги дополнительного урона, ClassLevel-эффекты powers, источники величины
  условий и требования пользовательских invocation pools для Barbarian.
- Формулы Elements, Light, Reaver, Savagery, Spirits и Yeoman: урон, лечение, временные HP,
  переключение persistent rage и пороги улучшений. Powers, выдаваемые через условия,
  также масштабируются при явном ClassHolder.Barbarian (например Wild Magic).

Количество uses/расход RagePoints, short/long-rest восстановление и PB-based DC остаются
в существующих механизмах UB. Новые уровни выдачи не означают глобальную подмену PB.

## Подклассы

Модуль объединяет список class subclass choices с регистрацией SubclassesContext,
затем выбирает реальные CharacterSubclassDefinition из игровой базы и исключает Hidden.
Никаких автоматических включений, правки видимости или жёсткого списка имён в алгоритме
обнаружения нет. В логе записывается фактически преобразованный список.

В этой версии исходников обычный инвентарь:

- Официальные: PathBerserker, PathClaw, PathMagebane, PathStone.
- UB: PathOfTheBattlerager, PathOfTheBeast, PathOfTheElements, PathOfTheLight,
  PathOfTheRavager, PathOfTheReaver, PathOfTheSavagery, PathOfTheSpirits,
  PathOfTheWildMagic, PathOfTheYeoman.

Выключенные пользователем/скрытые подклассы не включаются ради этого списка.
Их следует включать штатным UI UB до перезапуска, если нужны отдельные испытания.
Добавленные сторонними модами подклассы могут быть обнаружены, но их произвольные runtime
формулы не считаются автоматически поддержанными. Эта работа проверяет только текущий форк.

## Primal Champion и respec

В сжатом режиме исходный callback, изменяющий BaseValue, заменён отдельным определением:

- +4 Strength и +4 Constitution — обычные снимаемые FeatureDefinitionAttributeModifier.
- +4 к MaxValue/MaxEditableValue — отдельный callback, не изменяющий BaseValue.
- Повторное Apply/Remove в одной сессии учитывается отдельно по герою и тегу; отмена
  выдачи и повторная выдача не должны повторять изменение максимума.
- Для первого Remove у загруженного героя предполагается, что сохранённый максимум уже
  включает +4. Это проверено как переход чистой модели, а не реальная сериализация игры.

Respec копирует BaseValue старого героя, поэтому новый +4 capstone не переносится в базу.
Обычный несжатый Primal Champion и старые сохранения не переписываются.

НЕ ПРОВЕРЕНО В ИГРЕ: сериализация максимумов, поведение callback после загрузки, отмена
respec на всех стадиях, взаимодействие с предметами/томами, ASI и другими постоянными
изменениями. В исходной respec-системе UB есть перенос базовых значений, включая ранее
полученные ASI; этот общий механизм не переписан здесь. Также у Reaver есть старый
Draconic Resilience callback `HitPoints.maxValue += 3` с пустым RemoveFeature — его
жизненный цикл при respec остаётся отдельным риском, а не подтверждённо исправленной
частью этого этапа. Изменять общую HP-систему ради этого прототипа не стали.

## Кап и существующие герои

Barbarian13 блокируется только при активном Barbarian-прототипе: в списке классов,
AssignClassLevel и AddClassLevel. Оба прототипа учитываются одновременно при фильтрации.
Требования входа/выхода и пользовательский MaxAllowedClasses остаются исходными.
С целевым MaxAllowedClasses=3 максимум три класса, общий предел уровня — 20.

Если из-за требований характеристик или ограничений классов нет альтернативы после
Barbarian12, повышение блокируется и причина записывается в лог. Прямой вход в экран
повышения также защищён диалогом. Требования не отключаются и характеристики не меняются.

Герои с фактическим Barbarian>12 не мигрируют; их уровни не переписываются. Runtime-адаптер
оставляет их исходный уровень, а дальнейшее повышение блокируется. Загружать такие
сохранения с прототипом для обычной игры не рекомендуется: определения базы меняются
для всей игровой сессии. Отключите прототип и перезапустите игру перед загрузкой старой
кампании. Для существующих Barbarian<=12 также нет автоматической догрантовки наград.

## Включение без ручного редактирования XML

1. Используйте отдельную тестовую установку/профиль и новые тестовые сохранения.
2. Скопируйте собранный мод обычным способом своей установки. Cloud-выход находится в
   `SolastaFiles/Mods/SolastaUnfinishedBusiness/`; релизный архив не создавался.
3. Откройте UI UB → страницу классов. Включите
   `Barbarian 20 to 12 prototype (NEW TEST HEROES ONLY; restart required)`.
4. Сохраните настройки штатным интерфейсом UMM и полностью перезапустите игру.
5. Проверьте в логе `Barbarian 20-to-12 TEST prototype active`, список подклассов и
   сообщения `Compressed progression verified` для Barbarian и каждого выбранного подкласса.

Параметр: `EnableBarbarianCompressedProgressionPrototype`, по умолчанию false.
EnableLevel20 и EnableMulticlass должны уже быть включены; иначе модуль отказывается
активироваться. Пользовательские options автоматически не меняются, Settings.xml не
перезаписывается установочными скриптами. Fighter имеет свой отдельный переключатель.

Для выключения снимите Barbarian-флажок, сохраните и полностью перезапустите игру.
Используйте исходную копию/нового контрольного героя; миграция сжатого героя обратно в
обычный режим не реализована. Не переключайте релевантные options и видимость подклассов
в середине сессии: обнаруженная смена настроек блокирует повышение до перезапуска.

## Проверки и воспроизведение

Из корня `/workspace/SolastaUnfinishedBusiness`:

```bash
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.setup-solasta/dotnet-home
export NUGET_PACKAGES=/workspace/.setup-solasta/nuget
export SolastaInstallDir=/workspace/SolastaUnfinishedBusiness/SolastaFiles
dotnet build SolastaUnfinishedBusiness.sln --no-restore -c "Release Workflow" -m:2
dotnet run --project Tests/FighterProgression/FighterProgression.Tests.csproj
git diff --check
```

Проверки включают:

- Исходные 30 Fighter-проверок.
- Реальный общий преобразователь, подключённый в тесты с минимальной managed-моделью
  API: порядок/количество наград, повторные пулы, вложенные ASI+feat, происхождение,
  сохранность источников и override-ссылок, перенос subfeatures, disabled/повторный запуск.
- Все четыре официальных и десять UB диагностических snapshot-подклассов; десять
  UB-построителей дополнительно обнаруживаются из compiled ownership metadata.
- Целевую комбинацию feat-наград Barbarian: пять ASI и десять классовых feat-очков,
  отдельно от первого расового/начального бонуса.
- Чистые формулы капа, эффективного уровня и числа выборов Brutal Strike.
- Чистое состояние Apply/Remove максимумов Primal Champion и отсутствие BaseValue
  writer в скомпилированном callback.
- Rage metadata, наличие short-rest recovery 2024, thresholds замен и порядок startup.
- Цели и имена аргументов всех новых Harmony-патчей по игровым DLL без запуска Unity.

Результат: 123 проверки прошли; полная Release Workflow сборка: 0 ошибок, 0 предупреждений.

Managed doubles не реализуют движок: они моделируют копирование списков и идентичность
определений. Диагностические JSON — snapshots, не текущая runtime-база. Фактическое
обнаружение подклассов, клонирование Unity, Harmony, регистрация пулов выбора и save/load
должны быть проверены в игре. Startup-инварианты проверят актуальные листовые награды
во время реального запуска. Автопроверки не доказывают успешную игру.

## Короткий игровой чек-лист

1. Прототип выключен: новый контрольный Barbarian, уровни/выборы исходного UB, доступен
   Barbarian13. Затем включить прототип и создать ДРУГОГО нового героя.
2. Задать целевые options из запроса через UI, включая все восемь Barbarian options,
   ASI+feat, оба feat-переключателя, MinInOutAttributes, Weapon Mastery, Spell Points и
   EnableRespecAction=true. Полностью перезапустить. Для ускорения только в тестовом
   профиле можно вручную включить штатный UB Campaigns/NoExperienceOnLevelUp.
3. Начать со Strength>=13 и допустимым вторым классом. Поднимать по одному уровню.
   На каждом проверить один hit die/запись истории, обычный PB по общему уровню.
4. Проверить 5→Extra Attack; 6→Feral Instinct/Pounce; 7→Rage damage/Brutal Strike;
   8→Relentless Rage; 9→улучшение Brutal Strike; 10→Persistent Rage;
   11→второй выбор Brutal Strike, 6 rages/+4 damage; 12→Indomitable Might/ASI19/capstone.
5. На 12 проверить одновременно feat18, ASI+feat19 и capstone20. Всего базовых Barbarian
   ASI: 4,7,9,11,12. Дополнительные feats: 2,6,8,10,12. При целевых настройках это
   пять ASI и десять классовых feat-очков плюс отдельные стартовые/расовые источники.
   На каждом объединённом выборе дважды сделать Назад/вперёд и отменить всё повышение.
6. Проверить Rage: расход, короткий отдых (2024 возвращает один использованный rage),
   длинный отдых, persistent восстановление на инициативе, отмену реакции и длительность.
   При выключенном BrutalStrike2024 повторить на новой копии развитие Brutal Critical.
7. На Barbarian12 проверить Strength/Constitution +4 к текущему значению и максимуму,
   базовый score не должен получить этот +4. При обычном max20 ожидается max24; при
   пользовательском другом max сравнивать дельту +4. Проверить HP от Constitution.
8. Сохранить, полностью закрыть игру и загрузить на уровнях 11,12 и после второго класса.
   Повторить три respec: Barbarian12 → Fighter/другой класс → Barbarian12 → другой подкласс.
   Проверить характеристики, максимумы, HP, feats, старые способности и ресурсы. Отменить
   один respec. Для Reaver отдельно проверить старый +3HP callback.
9. Повторить для всех доступных подклассов. Приоритет: Elements (выборы/классовые эффекты),
   Ravager (кубы Frenzy), Wild Magic (power из condition), Savagery (THP/persistent),
   Yeoman (rage damage), Light/Reaver (лечение), Spirits (выборы/длительность), затем
   Berserker/Claw/Magebane/Stone/Battlerager/Beast. Проверять upgrades и смену выбора.
10. Включить оба прототипа после перезапуска: новые Barbarian12/Fighter8 и
    Fighter12/Barbarian8, общий20. Проверить число атак (ForceIfBetter, без суммирования),
    недоступность 13-го класса-уровня, обычный PB и предел трёх классов.
11. Deadlock: отдельный герой Strength<13 достигает Barbarian12 с включёнными требованиями
    выхода. Без легального выхода повышение блокируется с диагностикой, пустой экран
    не открывается. Характеристики и требования не меняются автоматически.
12. Выключить Barbarian, перезапустить и проверить обычную прогрессию на новом герое,
    затем Fighter-only regression по `FighterProgressionPrototype.md`.

При потере выбора, накоплении характеристик/HP, ошибке загрузки или поломке отмены
прекратить тест на этой копии и сохранить лог/шаги. Основные сохранения не использовать.

## Ограничения

Нет игровой проверки, автоматической миграции, поддержки горячего переключения,
обещания безопасного удаления для уже развитого героя или полной гарантии respec.
Существующие ошибки UB в Spell Points/переносе ASI не исправляются этим этапом.
Сторонние неизвестные subclasses/runtime формулы не сертифицированы. Monk, Rogue и
остальные новые классы не реализовывались. Стабильная release-ветка не менялась.

## Изменённые файлы этапа

Новые модели: `Models/BarbarianProgressionContext.cs`, `Models/BarbarianProgressionRules.cs`,
`Models/CompressedProgressionDefinitions.cs`.

Общая инфраструктура/интеграция: `Models/FighterProgressionContext.cs`, `Models/BootContext.cs`,
`Settings.cs`, `Displays/ClassesDisplay.cs`, `Api/GameExtensions/CustomizedSubFeatureDefinitions.cs`,
`Api/Helpers/MulticlassInOutRulesHelper.cs`, `Definitions/InvocationPoolTypeCustom.cs`,
`Patches/CharacterBuildingManagerPatcher.cs`, `Patches/RulesetCharacterHeroPatcher.cs`.

Runtime: `Models/2024BarbarianContext.cs`, `Behaviors/Specific/_GLBM.cs`,
`Behaviors/UpgradeEffectDamageBonusBasedOnClassLevel.cs`, `Patches/RulesetActorPatcher.cs`,
`Patches/RulesetCharacterPatcher.cs`, `Subclasses/PathOfTheElements.cs`, `PathOfTheLight.cs`,
`PathOfTheReaver.cs`, `PathOfTheSavagery.cs`, `PathOfTheSpirits.cs`, `PathOfTheYeoman.cs`.

Тесты: `Tests/FighterProgression/{FighterProgression.Tests.csproj,Program.cs,BarbarianTests.cs,DefinitionTestDoubles.cs}`.
Документация: этот файл и уточнение имени startup-лога в `FighterProgressionPrototype.md`.
