#if SOLASTA_FLEXIBLE_CASTING_QA
using System;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.ModKit;
using SolastaUnfinishedBusiness.Models;
using UnityEngine;
using UnityModManagerNet;

namespace SolastaUnfinishedBusiness.Displays;

[UsedImplicitly]
internal sealed class FlexibleCastingQaDisplay : IMenuSelectablePage
{
    private RulesetCharacterHero selected;
    private string status = "";
    public string Name => "QA 13.7C.2 — EDITOR ONLY";
    public int Priority => 950;
    public void OnGUI(UnityModManager.ModEntry modEntry)
    {
        UI.Label("QA diagnostic build / " + FlexibleCastingQaPlan.BuildId);
        UI.Label("Commit: " + FlexibleCastingQaContext.SourceCommit);
        UI.Label("НЕ ГОТОВО для conversion smoke: admission, XP и native QA conversions закрыты.");
        UI.Label(FlexibleCastingQaPlan.Reason(FlexibleCastingQaContext.Admission()));
        UI.ActionButton("Подготовить следующего НОВОГО editor Sorcerer (C.2)", () =>
        {
            try { QaEditorFinalizationContext.Arm(); status = "Armed: создайте нового одноразового Sorcerer. Это НЕ admission в кампанию."; }
            catch (Exception e) { status = e.Message; }
        }, UI.AutoWidth());
        UI.Label("Герой для чтения диагностик (выбор НЕ допускает героя к QA):");
        var heroes = FlexibleCastingQaContext.Heroes();
        if (Array.IndexOf(heroes, selected) < 0) selected = heroes.Length == 0 ? null : heroes[0];
        foreach (var hero in heroes)
            UI.ActionButton(hero.Name + " " + hero.SurName + " [" + hero.Guid + "]", () => selected = hero, UI.AutoWidth());
        UI.Label(QaEditorFinalizationContext.Report(selected));
        UI.ActionButton("Открыть native editor draft Sorcerer2 (без XP/party)", () =>
        {
            try { QaEditorFinalizationContext.OpenLevelTwo(selected); status = "Native editor level-up requested; Unity behavior unverified."; }
            catch (Exception e) { status = e.Message; }
        }, UI.AutoWidth());
        var enabled = GUI.enabled;
        try
        {
            GUI.enabled = false;
            UI.ActionButton("Grant XP to next level — BLOCKED: нет admitted original owner", () => FlexibleCastingQaContext.CheckXp(selected), UI.AutoWidth());
        }
        finally { GUI.enabled = enabled; }
        UI.ActionButton("Сохранить диагностический снимок", () =>
        {
            try { FlexibleCastingQaContext.SaveReport(selected); status = "Сохранено: " + FlexibleCastingQaContext.ReportPath; }
            catch (Exception e) { status = "Report failed: " + e.Message; }
        }, UI.AutoWidth());
        UI.ActionButton("End QA session", () => { FlexibleCastingQaContext.End(); status = "QA session завершена. Перезапустите игру с обычной сборкой."; }, UI.AutoWidth());
        UI.Label("Log: " + FlexibleCastingQaContext.ReportPath);
        UI.Label(status);
        GUILayout.TextArea(FlexibleCastingQaContext.Snapshot(selected));
        UI.Label("Пятый маршрут Sorcery→standard slot: PendingAtomicJournal до debit. Ничего не проверено в Unity.");
    }
}
#endif
