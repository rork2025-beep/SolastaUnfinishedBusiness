using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using Q = SolastaUnfinishedBusiness.Models.QaEditorFinalizationPlan;
using M = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using P = SolastaUnfinishedBusiness.Models.PactMagicResourcePlan;

internal static class QaEditorFinalizationTests
{
    private sealed class NativeFixture : Q.IHost
    {
        internal readonly object Original = new(), Builder = new(), NativeRepertoire = new();
        internal object OwnerRef;
        public object Owner => OwnerRef;
        public object Manager => Builder;
        public string Rules { get; set; } = "unchanged-native-rules";
        public int Actual { get; private set; }
        public string IntentFingerprint => Actual + "/" + Payload;
        internal string Payload = "selected-native-spells", Fault;
        internal bool LocalEditor = true, Complete = true;
        internal readonly Q Journal;
        internal readonly S.Tables Tables;
        internal readonly C Calculator;
        internal readonly P.CastingLedger Ledger;
        internal readonly M.Profile Profile;
        internal M.HeroState Repertoires = new(Array.Empty<M.Repertoire>());
        internal readonly HashSet<string> NativeKnown = new(), NativeCantrips = new(), NativePrepared = new(), Features = new();
        internal int NativeSorcery = 0, NativeSorceryMax, Grants, Publications, Observers, Refunds = 0, Xp = 0;
        internal int NativeSlots, NativePoints, Failures;
        internal object NativeOwner;
        internal Action Observer;
        private readonly bool replacement;
        private string[] added = Array.Empty<string>(), removed = Array.Empty<string>(), cantrips = Array.Empty<string>();
        private int nonce;
        internal NativeFixture(S.ResourceMode mode, S.Tables tables, bool prepared = false, bool replace = false)
        {
            replacement = replace;
            OwnerRef = Original; Tables = tables;
            var circles = Enumerable.Range(1, 20).Select(n => Math.Min(9, (n + 1) / 2)).ToArray();
            Profile = new M.Profile("Sorcerer", "CastSpellSorcerer", "Charisma", "SorcererList", M.Knowledge.Selection, prepared ? M.Preparation.AbilityOnly : M.Preparation.None,
                new[] { new M.Spell("A",1),new M.Spell("B",1),new M.Spell("C",1),new M.Spell("D",1),new M.Spell("c1",0),new M.Spell("c2",0),new M.Spell("c3",0),new M.Spell("c4",0) },
                circles, Enumerable.Repeat(4,20), Enumerable.Range(1,20).Select(n => n == 1 ? 2 : 3), Enumerable.Range(1,20).Select(n => replace && n == 2 ? 1 : 0), new int[20]);
            Calculator = new C("C2-fixture", new[] { new C.ClassProfile("Sorcerer",true,new C.CastingProfile("CastSpellSorcerer",C.CasterKind.Full,1,true,circles)) });
            Ledger = new P.CastingLedger(new S.Budget(tables, S.Allocation.StandardPortion(tables, Result(0), mode)),
                new P.Budget(new P.Allocation(new P.Tables(Enumerable.Range(1,20).Select(_ => new[] { 2,0,0,0,0,0,0,0,0 }), Array.Empty<P.Arcanum>()), Result(0))));
            Journal = new Q(Original, Builder, Rules, () => "issued-entry-" + ++nonce);
        }
        internal C.Result Result(int n) => Calculator.Calculate(new C.Snapshot(n, n == 0 ? Array.Empty<C.ClassInput>() : new[] { new C.ClassInput("Sorcerer",n,true) }, Enumerable.Repeat("Sorcerer",n)));
        public void RequireEditor() { if (!LocalEditor) throw new InvalidOperationException("loaded/campaign/network/stale editor"); }
        public void Log(Exception error) { Failures++; }
        internal Q.Ticket Assign(string selected = "Sorcerer")
        {
            var ticket = Journal.Issue(this, selected); Actual++;
            added = Actual == 1 ? new[] { "A", "B" } : replacement ? new[] { "C", "D" } : new[] { "C" };
            removed = Actual == 2 && replacement ? new[] { "A" } : Array.Empty<string>();
            cantrips = Actual == 1 ? new[] { "c1", "c2", "c3", "c4" } : Array.Empty<string>();
            return ticket;
        }
        internal void UndoDraft() { Journal.Cancel(Journal.Current); Actual--; }
        public Q.Prepared Prepare(Q.Ticket ticket)
        {
            if (NativeSorcery != 0 || NativePoints != Ledger.Standard.RemainingPoints) throw new InvalidOperationException("stale native resource witness");
            var tx = new M.Transaction(ticket.Id, Repertoires, Profile, Actual, true);
            tx.Select(new M.Choice(Actual, Complete ? added : added.Take(added.Length - 1), removed, cantrips));
            var fingerprint = IntentFingerprint;
            return new Q.Prepared(fingerprint, tx, Result(Actual), () =>
            {
                if (Fault == "before" || IntentFingerprint != fingerprint) throw new InvalidOperationException("stale draft/resource proof before mutation");
                SorcererFinalizePreflight.RequireQaDraft(ticket, tx);
            }, () =>
            {
                var expected = tx.Preview().Repertoires["Sorcerer"];
                if (!ReferenceEquals(NativeOwner, Original) || !NativeKnown.SetEquals(expected.Known) || !NativeCantrips.SetEquals(expected.Cantrips) ||
                    NativePrepared.Any(s => !expected.Known.Contains(s)) || NativeSorcery != 0 || NativeSorceryMax != (Actual == 2 ? 2 : 0) || !Features.Contains("CastSpellSorcerer") || Xp != 0)
                    throw new InvalidOperationException("bad native result, ownership or inventory");
            });
        }
        internal void NativeFinalize(Q.Ticket ticket)
        {
            Grants++; Features.Add("CastSpellSorcerer");
            if (Fault == "native-grant") throw new InvalidOperationException("native failed after feature grant");
            foreach (var spell in removed) NativeKnown.Remove(spell);
            NativeKnown.UnionWith(added); NativeCantrips.UnionWith(cantrips);
            if (Fault == "result") NativeKnown.Add("ForeignWizardSpell");
            if (Profile.PreparedRule != M.Preparation.None) { NativePrepared.Clear(); NativePrepared.UnionWith(NativeKnown); }
            NativeOwner = Original; NativeSorceryMax = Actual == 2 ? 2 : 0;
            if (Actual == 2) Features.Add("FlexibleCasting");
            NativeSlots = Tables.SlotRow(Actual)[0]; NativePoints = Tables.Points(Actual);
            Journal.NativeReturned(Original, ticket); // same marker as the injected native tail
        }
        public Q.Publication PreparePublication(Q.Ticket ticket)
        {
            if (Fault == "packet") throw new InvalidOperationException("publication preparation failed");
            var allocation = S.Allocation.StandardPortion(Tables, ticket.Draft.Progression, Ledger.Standard.Current.Mode);
            return new Q.Publication(() => { }, () =>
            {
                Publications++;
                Ledger.Standard.Refresh(allocation);
                if (Fault == "sync") throw new InvalidOperationException("synchronization failed in process");
                var committed = ticket.Draft.Spells.Commit(); var view = committed.Repertoires["Sorcerer"];
                Repertoires = committed.With(new M.Repertoire(view.Definition, view.ActualLevel, view.Compressed, view.Known, view.Cantrips, NativePrepared, view.History, view.Bonus));
                NativeSlots = allocation.Slots[0]; NativePoints = Ledger.Standard.RemainingPoints;
            }, () =>
            {
                if (Ledger.Standard.Current.CasterLevel != Actual || Repertoires.Repertoires["Sorcerer"].ActualLevel != Actual ||
                    !NativeKnown.SetEquals(Repertoires.Repertoires["Sorcerer"].Known)) throw new InvalidOperationException("wrong owner projection");
            }, new Action[] { () => { Observers++; Observer?.Invoke(); if (Fault == "notify") throw new InvalidOperationException("listener failed after commitment"); } });
        }
        internal void Run(Q.Ticket ticket)
        {
            if (!Journal.Begin(this, ticket)) { Journal.Finish(this, ticket, null, false); return; }
            Exception error = null;
            try { NativeFinalize(ticket); } catch (Exception e) { error = e; }
            Journal.Finish(this, ticket, error, true);
        }
        internal string Resources => Ledger.Standard.Current.CasterLevel + "/" + Ledger.Standard.RemainingPoints + "/" + Ledger.Standard.RemainingSlots(1) + "/" + Ledger.Pact.Remaining;
    }
    internal static void Run(Action<bool,string> check, AssemblyDefinition game, TypeDefinition[] types)
    {
        var behavior = 0; var metadata = 0;
        void B(bool ok,string name) { behavior++; check(ok,"C.2 production finalization adapter/controlled native fixture: "+name); }
        void A(bool ok,string name) { metadata++; check(ok,"C.2 native/compiled audit: "+name); }
        int[] Values(string type,string field) => CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name==type),field).Where(n=>n>=0).ToArray();
        var flat = Values("FeatureDefinitionCastSpellBuilder","SlotsByCasterLevel");
        var tables = new S.Tables(Enumerable.Range(1,20).Select(n=>flat.Skip(n*10).Take(9)), Values("ModifyPowerPoolAmountPowerSpellPoints","SpellPointsByLevel"), Values("SpellPointsContext","SpellCostByLevel"));
        bool Reject(Action action) { try { action(); return false; } catch(InvalidOperationException) { return true; } }
        foreach (var mode in new[] { S.ResourceMode.Slots,S.ResourceMode.SpellPoints }) foreach (var prepared in new[] { false, true })
        {
            var f = new NativeFixture(mode,tables,prepared); var originalBudget = f.Ledger.Standard;
            for (var level=1;level<=2;level++)
            {
                var ticket=f.Assign();
                B(ticket.Target==level&&f.Journal.ConfirmedActual==level-1,"native draft is real actual level, not a confirmed grant");
                B(f.Journal.Begin(f,ticket),"complete original-level spell choices authorize one native body");
                B(f.Journal.ConfirmedActual==level-1&&f.Publications==level-1,"preflight is not success or resource refresh");
                f.NativeFinalize(ticket);
                f.Journal.NativeReturned(f.Original,ticket);
                B(f.Journal.ConfirmedActual==level-1,"native tail alone is not committed acknowledgement");
                f.Observer=()=>B(ticket.State==Q.Phase.Committed&&f.Journal.ConfirmedActual==level&&f.Ledger.Standard.Current.CasterLevel==level,"our observers see confirmed receipt plus coherent owner");
                f.Journal.Finish(f,ticket,null,true);
                var view=f.Repertoires.Repertoires["Sorcerer"];
                B(view.ActualLevel==level&&view.EffectiveLevel==level&&ticket.Draft.Progression.Input.ActualClassHistory.SequenceEqual(Enumerable.Repeat("Sorcerer",level)),"actual/effective/history preserved from calculator; no class rewrite");
                B(ReferenceEquals(f.Ledger.Standard,originalBudget)&&f.Ledger.Pact.Remaining==0&&f.NativeSorcery==0&&f.NativeSorceryMax==(level==2?2:0),"same level0 Standard/Pact owner, native Sorcery preserved");
                B(view.Known.Count==(level==1?2:3)&&view.Cantrips.Count==4&&view.History.All(o=>o.ClassId=="Sorcerer"&&o.FeatureId=="CastSpellSorcerer"),"native choices and original class ownership retained");
                B(NativeSet(view.Prepared,f.NativePrepared)&&view.Definition.Ability=="Charisma","native preparation and original spellcasting ability retained");
                B(f.NativePoints==tables.Points(level)&&f.NativeSlots==f.Ledger.Standard.Current.Slots[0],"native resource views match production installed tables");
                var resources=f.Resources; var grants=f.Grants; var observers=f.Observers;
                f.Run(ticket); f.Journal.Finish(f,ticket,null,true);
                B(f.Grants==grants&&f.Resources==resources&&f.Observers==observers,"duplicate finalize/tail callback cannot repeat grant, refresh or observer");
            }
            B(f.Grants==2&&f.Publications==2&&f.Refunds==0&&f.Xp==0,"0->1->2 grants/synchronizes once each, no refund or XP");
            B(Reject(()=>f.Journal.Issue(f,"Wizard"))&&Reject(()=>f.Journal.Issue(f,"Warlock")),"Wizard1/Warlock1 explicitly blocked before assignment, no admission guard relaxed");
            f.Payload="changed-confirmed-selection";
            B(Reject(()=>f.Run(f.Journal.Current))&&f.Grants==2,"conflicting committed payload rejected");
        }
        foreach (var fault in new[] { "incomplete","before","owner","rules","editor","cancel","sorcery","standard" })
        {
            var f=new NativeFixture(S.ResourceMode.Slots,tables);var ticket=f.Assign();var resources=f.Resources;
            switch(fault) {case "incomplete":f.Complete=false;break;case "owner":f.OwnerRef=new object();break;case "rules":f.Rules="changed";break;case "editor":f.LocalEditor=false;break;case "cancel":f.Journal.Cancel(ticket);break;case "sorcery":f.NativeSorcery=1;break;case "standard":f.NativePoints=1;break;default:f.Fault=fault;break;}
            B(Reject(()=>f.Run(ticket))&&f.Grants==0&&f.Publications==0&&f.Resources==resources&&f.Journal.ConfirmedActual==0,"rejection before native grant: "+fault);
        }
        foreach (var mode in new[] { S.ResourceMode.Slots, S.ResourceMode.SpellPoints })
        {
            var f = new NativeFixture(mode, tables, replace:true);
            f.Run(f.Assign()); f.Run(f.Assign());
            var view = f.Repertoires.Repertoires["Sorcerer"];
            B(f.NativeKnown.SetEquals(new[] { "B", "C", "D" }) && view.Known.SequenceEqual(new[] { "B", "C", "D" }) &&
                view.History.Any(o => o.SpellId == "A" && o.Removed && o.Original == 2),
                "independent staged native replacement removes earlier spell, preserves original ownership/origin and exact acquisition count");
        }
        foreach (var fault in new[] { "native-grant","result","packet","sync","missing-tail","skipped-body","after-native-error","after-native-rules" })
        {
            var f=new NativeFixture(S.ResourceMode.SpellPoints,tables){Fault=fault};var ticket=f.Assign();
            B(f.Journal.Begin(f,ticket),"prepared failure-boundary fixture "+fault);
            Exception error=null;
            if(fault is not ("missing-tail" or "skipped-body"))try{f.NativeFinalize(ticket);}catch(Exception e){error=e;}
            if(fault=="after-native-error")error=new InvalidOperationException("later UB postfix failed");
            if(fault=="after-native-rules")f.Rules="changed after native grant";
            B(Reject(()=>f.Journal.Finish(f,ticket,error,fault!="skipped-body"))&&ticket.State==Q.Phase.Indeterminate&&f.Journal.ConfirmedActual==0,"indeterminate native result never acknowledged: "+fault);
            var grants=f.Grants;var resources=f.Resources;
            B(Reject(()=>f.Run(ticket))&&f.Grants==grants&&f.Resources==resources&&f.Refunds==0,"no repeated partial grants or invented refund: "+fault);
        }
        {
            var f=new NativeFixture(S.ResourceMode.Slots,tables);var t=f.Assign();f.UndoDraft();var next=f.Assign();
            B(next.Id!=t.Id&&t.State==Q.Phase.Cancelled&&Reject(()=>f.Journal.Begin(f,t)),"legitimate new assignment after native undo gets new identity; old ticket stays stale");
            f.Fault="notify";f.Observer=()=>B(Reject(()=>f.Run(next)),"reentrant listener cannot finalize again");f.Run(next);
            B(next.State==Q.Phase.Committed&&next.NotificationFailures==1&&f.Publications==1,"throwing listener is committed delivery failure");
            f.Run(next);B(f.Grants==1&&f.Observers==1,"notification failure never retries debit/grant/publication");
        }
        {
            var f=new NativeFixture(S.ResourceMode.Slots,tables);var t=f.Assign();
            B(f.Journal.Begin(f,t)&&Reject(()=>f.Journal.Begin(f,t))&&t.State==Q.Phase.Executing,"reentry before native return rejects without changing outer phase");
            B(Reject(()=>f.Journal.NativeReturned(new object(),t))&&t.State==Q.Phase.Executing,"wrong native return owner cannot acknowledge or poison outer result");
            f.NativeFinalize(t);f.Journal.Finish(f,t,null,true);
            B(t.State==Q.Phase.Committed&&f.Grants==1,"outer native invocation survives rejected reentry");
        }
        var native=game.MainModule.Types.Single(t=>t.Name=="CharacterBuildingManager");
        var finalize=native.Methods.Single(m=>m.Name=="FinalizeCharacter");
        A(finalize.Body.Instructions.Count(i=>i.OpCode.Code==Code.Ret)==1&&finalize.Body.Instructions.Last(i=>i.Operand is MethodReference).Operand is MethodReference last&&last.Name=="UpdateCharacter","single native return follows UpdateCharacter");
        var update=native.Methods.Single(m=>m.Name=="UpdateCharacter");
        A(new[]{"AutoPrepareSpells","RestoreAllSpellSlots","GrantInvocations","GrantPowers","ComputeCharacterLevel"}.All(name=>update.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name==name)),"native final resource/feature/actual-level work precedes tail marker");
        A(native.Methods.Single(m=>m.Name=="ReleaseCharacter").Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="ClearLists"),"native release clears draft choices; not proof of successful finalization");
        A(!types.Any(t=>t.Name=="QaEditorFinalizationContext")&&!C.MixedGameplayReady,"ordinary assembly has no QA native bridge and gameplay gate stays false");
        var standard=types.Single(t=>t.Name=="StandardMulticasterResourcesContext");
        A(standard.Methods.Single(m=>m.Name=="BindNewHero").Body.Instructions.Single(i=>i.Operand is MethodReference r&&r.Name=="BindNewHeroCore").Previous.OpCode.Code==Code.Ldc_I4_0 &&
            standard.Methods.Single(m=>m.Name=="RequireQaEditorZero").Body.Instructions.Any(i=>i.OpCode.Code==Code.Throw),
            "ordinary binder passes QA=false and explicit QA binding guard throws before owners are added");
        Console.WriteLine($"Stage13.7C.2: {behavior} executed production-adapter/controlled-native-fixture checks; {metadata} native/compiled audits; NO Unity execution. Sorcerer0->1->2 only; Wizard/Warlock blocked.");
    }
    private static bool NativeSet(IEnumerable<string> model,IEnumerable<string> native) => new HashSet<string>(model).SetEquals(native);
    internal static void AuditQa(Action<bool,string> check,AssemblyDefinition qa,AssemblyDefinition game)
    {
        var context=qa.MainModule.Types.Single(t=>t.Name=="QaEditorFinalizationContext");
        foreach(var name in new[]{"CreatePatch","AssignPatch","ClassPanelPatch","LevelUpPatch","FinalizePatch","CancelClassPatch","ReleasePatch"})
        {
            var patch=context.NestedTypes.Single(t=>t.Name==name);
            var attr=patch.CustomAttributes.Single(a=>a.AttributeType.Name=="HarmonyPatch");
            var type=(TypeReference)attr.ConstructorArguments[0].Value;var method=(string)attr.ConstructorArguments[1].Value;
            var targets=game.MainModule.Types.Single(t=>t.FullName==type.FullName).Methods.Where(m=>m.Name==method).ToArray();
            check(targets.Length==1&&patch.Methods.Where(m=>m.Name is "Prefix" or "Finalizer").SelectMany(m=>m.Parameters)
                .Where(p=>!p.Name.StartsWith("__",StringComparison.Ordinal)).All(p=>targets[0].Parameters.Any(a=>a.Name==p.Name)),
                "C.2 QA native hook target/arguments checked: "+name);
        }
        var finalize=context.NestedTypes.Single(t=>t.Name=="FinalizePatch");
        check(finalize.Methods.Single(m=>m.Name=="Tail").Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="NativeReturned"),"C.2 real native return marker feeds production journal");
        check(finalize.Methods.Single(m=>m.Name=="Finalizer").Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="Finish"),"C.2 Harmony exception/finalizer callback feeds production commit adapter");
        var standard=qa.MainModule.Types.Single(t=>t.Name=="StandardMulticasterResourcesContext");
        check(standard.Methods.Single(m=>m.Name=="QaBindEditorZero").Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="BindNewHeroCore"),"C.2 reuses existing zero-level resource binding");
        check(standard.Methods.Single(m=>m.Name=="get_RuntimeEnabled").Body.Instructions.Any(i=>i.OpCode.Code==Code.Ldc_I4_0),"C.2 editor binding never opens the global campaign gate");
        check(context.Methods.Where(m=>m.HasBody).Concat(context.NestedTypes.SelectMany(t=>t.Methods).Where(m=>m.HasBody))
            .SelectMany(m=>m.Body.Instructions).All(i=>i.Operand is not MethodReference r||r.Name is not ("GrantExperience" or "GrantSpell" or "GrantCantrip" or "AddClassLevel" or "CreateSpellSlot" or "CreateSorceryPoints")),
            "C.2 bridge has no direct XP/level/spell/conversion grant path; original finalizer owns native grants");
        var cancel=context.NestedTypes.Single(t=>t.Name=="CancelClassPatch").Methods.Single(m=>m.Name=="Prefix");
        check(cancel.ReturnType.FullName=="System.Boolean"&&cancel.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPriority"&&Convert.ToInt32(a.ConstructorArguments[0].Value)==0),
            "C.2 cancellation prefix respects UB suppression of preview-only class unassignment");
    }
}
