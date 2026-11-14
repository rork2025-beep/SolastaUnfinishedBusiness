# QA 13.7C.1 — краткий checklist (diagnostic-only)

**STOP для conversion smoke: admission/XP закрыты. Это не playable QA build.** Четыре backend-маршрута пока недоступны через QA UI. Unity не проверена. Нужны disposable heroes и отдельные test saves; настоящие saves не открывать. Полная установка/откат — в FlexibleCastingQaBuild.md рядом с этим файлом.

| Шаг | Действие и ожидаемый результат | Что вернуть |
|---|---|---|
| 1 | Закрыть игру, сохранить обычную UB папку вне Mods, установить только QA. В UMM виден `13.7C.1-DIAGNOSTIC-BLOCKED`. Открыть QA panel, сравнить полный SHA с QA-MANIFEST.json. | `01-build.png`: полный SHA и надпись BLOCKED; manifest. |
| 2 | В panel нажать «Сохранить диагностический снимок». Ожидается ADMITTED=false, причина отказа, XP action disabled. | `02-admission.png`; копия latest.txt как `02-admission.txt`. |
| 3 | При желании создать **нового одноразового** героя обычным UI. QA лишь наблюдает level0 объект; это не resource binding. В panel выбрать героя, сверить actual classes/history, XP/threshold, Sorcery/SP/Pact/repertoires. При переходе в кампанию новый loaded object не должен считаться автоматически admitted. | `03-hero.png`; `03-hero.txt`. Не выдавать опыт через Party Editor/console. |
| 4 | Ничего не конвертировать и не обходить guards. Если Flexible Casting всё же открывается: не нажимать Confirm повторно; сохранить снимок/UMM log и остановиться при любом debit. В этой сборке QA campaign entry/modal conversion должны отклоняться; родительские location actions не проверены. | При неожиданном поведении: before/after screenshots, `04-rejection.txt`, полный UMM log. |
| 5 | Нажать End QA session. Закрыть игру полностью, переместить QA папку вне Mods, восстановить обычную UB папку и исходные настройки. QA пункта больше нет. | `05-end.png`, `05-end.txt`; ошибка отката, если была. |

Отчёт: `<Solasta>/Mods/SolastaUnfinishedBusiness/QaDiagnostics/FlexibleCasting-QA-latest.txt`. Каждый снимок заменяет предыдущий — копируйте его сразу под именем из таблицы. Приложите ZIP SHA256, manifest, UMM log; не нужны saves/личные Settings.xml.

**После будущего исправления admission (не выполнять сейчас):** Sorcerer2 через long-rest level-up → Wizard1; отдельный Sorcerer/Warlock run; Slots: slot→Sorcery; новый SP run: SP→Sorcery и Sorcery→temporary SP; Pact normal/Shift/priority; short/long rest с before/after ресурсами. Пятый маршрут Sorcery→standard slot должен дать PendingAtomicJournal без debit/condition/успешного parent. Третий класс только после успешного первого smoke.

| Поздний checkpoint — всё НЕ ПРОВЕРЕНО | Actual classes (пример) | Ожидаемая feature projection |
|---|---|---|
| Character2 / 3 | Sorcerer2 / Sorcerer2+Wizard1 | original Sorcerer2 / Wizard1 |
| Character12 | Sorcerer11+Wizard1 | original Sorcerer17 / Wizard1 |
| Character13 | Sorcerer12+Wizard1 | original Sorcerer20; Sorcerer13 запрещён |
| Character20 | Sorcerer12+Wizard8 | original20/11; total cap20, classes≤3 |
