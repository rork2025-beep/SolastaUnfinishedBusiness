# QA13.7C.2 — editor candidate checklist

**Это не campaign smoke build.** C.2 проверен вне Unity; in-game smoke требует C.3 и реальной Unity validation. Campaign admission, XP controls, четыре conversions и пятый slot-condition route закрыты. Существующие герои/saves не импортировать. Установка/откат подробно в FlexibleCastingQaBuild.md.

| Шаг (ожидания, ещё НЕ выполнено в Unity) | Что проверить | Что вернуть |
|---|---|---|
| 1. Закрыть игру, сохранить ordinary UB папку вне Mods; установить только QA. | В panel `QA13.7C.2 — EDITOR ONLY`; full SHA совпадает с manifest. | `01-build.png`, manifest. |
| 2. С ранее включёнными Level20/Multiclass/Sorcerer compression перезапустить. В idle main-menu panel нажать «Подготовить следующего НОВОГО editor Sorcerer». | Один disposable candidate, не loaded/pool hero. QA настройки не меняются автоматически. | `02-arm.png`. |
| 3. Создать Sorcerer1 обычным UI: некастующие race/background/feat, заполнить все pools и spells, Finish. | Receipt Committed, confirmed actual1, собственный Sorcerer repertoire, native history и spell choices сохранены. Не переносить в кампанию. | `03-level1.png`, `03-level1.txt`. |
| 4. После закрытия первого редактора выбрать тот же candidate и «Открыть native editor draft Sorcerer2». Заполнить UI choices, Finish. | Новый issued ID; actual/confirmed2, history Sorcerer/Sorcerer, native Sorcery2, один repertoire/Standard owner, XP без изменения. | `04-level2-before.txt`, `04-level2-after.txt`, `04-level2.png`. |
| 5. Отдельно проверить Back/Cancel до Finish и incomplete draft. | Нет committed ack/grant от QA. Не повторять Indeterminate operation. Wizard/Warlock и уровни выше2 должны отклоняться. | `05-rejection.png`, `05-rejection.txt`, UMM log. |
| 6. End QA, закрыть процесс, переместить QA папку вне Mods и вернуть ordinary backup. | QA UI отсутствует; исходные Settings.xml/saves не изменены миграцией. | `06-end.png`, `06-end.txt`. |

Отчёт: `<Solasta>/Mods/SolastaUnfinishedBusiness/QaDiagnostics/FlexibleCasting-QA-latest.txt`. Каждый снимок заменяет предыдущий: копируйте сразу под именем из таблицы. При Indeterminate/несовпадении ресурсов остановиться, сохранить лог, не использовать/не сохранять candidate как игровой hero. Никаких console/PartyEditor XP, force-level или class-history edits.

**Не выполнять до C.3:** editor→party transfer, Sorcerer/Wizard/Warlock campaign, XP controls, Flexible Casting, rest после conversion, save/load и respec. Четыре backend routes сохранены; пятый всегда blocked до debit. Поздние checkpoints Character12/13/20 остаются NOT RUN; классу12 нельзя давать13, total cap20/classes≤3. Сейчас поддержан только editor Sorcerer0→1→2.
