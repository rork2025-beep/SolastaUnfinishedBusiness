using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;

// Production planners/transformer execute with managed doubles. Compiled binding/resource checks use Cecil only.
// This runner never executes Unity, Harmony, rest, conversion dialogs, respec, or save/load.
internal static class SorcererTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m, string name, string owner = null) => m.HasBody &&
        m.Body.Instructions.Any(x => x.Operand is MethodReference r && r.Name == name && (owner == null || r.DeclaringType.Name == owner));
    internal static void Run(Action<bool, string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        var context = types.Single(x => x.Name == "SorcererProgressionContext");
        var casting = types.Single(x => x.Name == "SorcererSpellcastingContext");
        var builders = types.Where(x => x.BaseType?.Name == "AbstractSubclass" && x.Methods.Any(m => m.Name == "get_Klass" && Calls(m, "get_Sorcerer"))).ToArray();
        var expected = new[] { "SorcerousDivineHeart", "SorcerousFieldManipulator", "SorcerousPsion", "SorcerousSorrAkkath", "SorcerousSpellBlade", "SorcerousWildMagic" };
        check(builders.Select(x => x.Name).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), "Sorcerer: six actual UB registrations discovered from compiled class ownership");
        var official = new[] { "SorcerousChildRift", "SorcerousDraconicBloodline", "SorcerousHauntedSoul", "SorcerousManaPainter" };
        var fixtures = new List<(string Name, string Folder)> { ("Sorcerer", "OfficialBlueprints") };
        fixtures.AddRange(official.Select(x => (x, "OfficialBlueprints"))); fixtures.AddRange(builders.Select(x => (x.Name, "UnfinishedBusinessBlueprints")));
        foreach (var (name, folder) in fixtures)
        {
            using var j = JsonDocument.Parse(File.ReadAllText("Diagnostics/" + folder + "/" + (name == "Sorcerer" ? "CharacterClassDefinition/" : "CharacterSubclassDefinition/") + name + ".json"));
            var source = j.RootElement.GetProperty("featureUnlocks").EnumerateArray().Select(x => new FeatureUnlockByLevel(
                new FeatureDefinition { Name = x.GetProperty("featureDefinition").GetString() }, x.GetProperty("level").GetInt32())).ToList();
            var before = source.Select(x => (x.FeatureDefinition, x.Level)).ToArray();
            var output = new CompressedProgressionDefinitions("TestSorcerer" + name).Prepare(source, name);
            check(output.SelectMany(x => CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).Where(x => x is not FeatureDefinitionFeatureSet)
                .SequenceEqual(source.OrderBy(x => x.Level).Select(x => x.FeatureDefinition)), "Sorcerer: every original fixture event/order preserved: " + name);
            check(source.Select(x => (x.FeatureDefinition, x.Level)).SequenceEqual(before) && output.All(x => x.Level <= 12), "Sorcerer: source unchanged/output capped: " + name);
        }
        CheckTables(check, types, game); CheckLearning(check, types); CheckRewards(check);
        check(Enumerable.Range(0, 21).All(n => SorcererProgressionRules.EffectiveLevel(false, n) == n) && SorcererProgressionRules.EffectiveLevel(true, 13) == 13,
            "Sorcerer: disabled and unsupported existing levels remain actual");
        check(!SorcererProgressionRules.IsCapped(true, 11) && SorcererProgressionRules.IsCapped(true, 12) && SorcererProgressionRules.IsCapped(true, 13) &&
            !SorcererProgressionRules.IsCapped(false, 12), "Sorcerer: class13 blocked only when opted in");
        var init = new FighterProgressionInitialization(); var count = 0;
        for (var n = 0; n < 3; n++) if (init.ShouldInitialize(true)) count++;
        var off = new FighterProgressionInitialization();
        check(count == 1 && !off.ShouldInitialize(false) && !off.ShouldInitialize(true), "Sorcerer: repeated/disabled initialization uses existing one-shot session latch");
        var startup = types.SelectMany(x => x.Methods).Single(x => Calls(x, "LateLoad", "SorcererProgressionContext"));
        int Position(string owner) => startup.Body.Instructions.Select((x, i) => (x, i)).Single(x => x.x.Operand is MethodReference m && m.Name == "LateLoad" && m.DeclaringType.Name == owner).i;
        check(Position("SorcererProgressionContext") > Position("WizardProgressionContext") && Position("SorcererProgressionContext") > Position("Tabletop2024Context") &&
            Position("SorcererProgressionContext") > Position("SpellPointsContext"), "Sorcerer: late bootstrap preserves final rules and previous modules (metadata)");
        check(Calls(context.Methods.Single(x => x.Name == "DiscoverSubclasses"), "get_KlassListSubclassEnabled"), "Sorcerer: discovery reads enabled registrations (metadata)");
        var settings = types.Single(x => x.FullName == "SolastaUnfinishedBusiness.Settings");
        check(settings.Methods.Where(x => x.IsConstructor && !x.IsStatic).All(m => !m.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name.Contains("EnableSorcererCompressedProgressionPrototype"))),
            "Sorcerer: CLR false opt-in default (metadata)");
        check(Calls(context.Methods.Single(x => x.Name == "LateLoad"), "get_PowerSorcererChildRiftRiftwalkLandingDamage"),
            "Sorcerer: auxiliary Riftwalk landing power is included in scoped power-rank ownership (metadata)");
        var level20 = types.Single(x => x.Name == "Level20Context").Methods.Single(x => x.Name == "SorcererLoad");
        check(new[] { 17,19,20 }.All(n => level20.Body.Instructions.Any(i => Integer(i) == n)) && Calls(level20, "EnumerateReplacedSpells"),
            "Sorcerer: original17 metamagic,19ASI,20 capstone and replacements retained in level20 loader (metadata)");
        var sub20 = types.Single(x => x.Name == "Level20SubclassesContext").Methods.Single(x => x.Name == "SorcererLoad");
        check(official.All(x => Calls(sub20, "get_" + x)) && sub20.Body.Instructions.Any(i => Integer(i) == 18) && Calls(sub20, "SetOverriddenPower"),
            "Sorcerer: all four official18 upgrades and Mana Overflow replacement exist (metadata)");
        var rules2024 = types.Single(x => x.Name == "Tabletop2024Context");
        foreach (var (method, flag) in new[] { ("SwitchSorcererArcaneApotheosis", "EnableSorcererArcaneApotheosis2024"),
                     ("SwitchSorcererMetamagic", "EnableSorcererMetamagic2024"), ("SwitchSorcererOriginLearningLevel", "EnableSorcererOrigin2024"),
                     ("SwitchSorcererInnateSorcery", "EnableSorcererInnateSorcery2024"), ("SwitchSorcererSorcerousRestorationAtLevel5", "EnableSorcererSorcerousRestoration2024"),
                     ("SwitchSorcererDraconicBloodlineAC", "EnableSorcererDraconicBloodlineAC2024") })
            check(Calls(rules2024.Methods.Single(x => x.Name == method), "get_" + flag), "Sorcerer: actual2024 flag remains authoritative: " + flag);
        check(Types(rules2024).SelectMany(x => x.Methods).Any(x => Calls(x, "EffectiveLevel", "SorcererProgressionContext")), "Sorcerer: 2024 runtime thresholds use scoped class level (metadata)");
        var capSwitch = rules2024.Methods.Single(x => x.Name == "SwitchSorcererArcaneApotheosis");
        check(Calls(capSwitch, "RemoveAll"), "Sorcerer: legacy/2024 capstone source switch removes mutually exclusive variants (metadata)");
        foreach (var sub in new[] { "SorcerousPsion", "SorcerousSpellBlade", "SorcerousWildMagic" })
            check(Types(types.Single(x => x.Name == sub)).SelectMany(x => x.Methods).Any(x => Calls(x, "EffectiveLevel", "SorcererProgressionContext") ||
                Calls(x, "EffectiveSubclassLevel", "SorcererProgressionContext")), "Sorcerer: direct subclass threshold adapted: " + sub + " (metadata)");
        var helper = types.Single(x => x.Name == "MulticlassInOutRulesHelper");
        check(Types(helper).SelectMany(x => x.Methods).Any(x => Calls(x, "IsCapped", "SorcererProgressionContext") && Calls(x, "CanSelectClass", "WizardSpellcastingContext") &&
            Calls(x, "CanSelectClass", "SorcererSpellcastingContext")) && helper.Methods.Any(x => x.Name == "ApproveMultiClassInOut"), "Sorcerer: cap/caster/Wizard guards compose with existing ability requirements (metadata)");
        check(Calls(casting.Methods.Single(x => x.Name == "CanSelectClass"), "IsCaster") && Types(casting).SelectMany(x => x.Methods).Any(x => Calls(x, "get_Subclasses")),
            "Sorcerer: class eligibility also rejects classes with no enabled noncasting subclass choice (metadata)");
        check(Calls(casting.Methods.Single(x => x.Name == "IsSupportedHero"), "IsMulticaster", "SharedSpellsContext"), "Sorcerer: shared-caster path is guarded rather than silently accepted (metadata)");
        check(Calls(types.Single(x => x.Name == "SharedSpellsContext").Methods.Single(x => x.Name == "GetSharedCasterLevel"), "SingleCasterLevel", "SorcererSpellcastingContext"),
            "Sorcerer: reaction/upcast helper receives scoped solo-caster effective level (metadata)");
        check(Calls(casting.Methods.Single(x => x.Name == "SingleCasterLevel"), "get_Active", "SorcererProgressionContext") &&
            Calls(casting.Methods.Single(x => x.Name == "SingleCasterLevel"), "IsSupportedHero"),
            "Sorcerer: shared helper override is gated by opt-in and no second repertoire, preserving unrelated casters (metadata)");
        check(SorcererProgressionRules.EffectiveLevel(true, 12) == 20 && FighterProgressionPlan<int>.EffectiveLevel(8) == 11 && 12+8 == 20 &&
            SorcererProgressionRules.EffectiveLevel(true, 8) == 11, "Sorcerer:12/Fighter8 and8/Fighter12 preserve actual20 and independent effective levels");
        foreach (var owner in new[] { context, casting })
        foreach (var patch in owner.NestedTypes)
        {
            var attr = patch.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "HarmonyPatch"); if (attr == null) continue;
            var type = (TypeReference)attr.ConstructorArguments[0].Value; var name = (string)attr.ConstructorArguments[1].Value;
            var targets = game.MainModule.Types.Single(x => x.FullName == type.FullName).Methods.Where(x => x.Name == name || x.Name == "get_" + name).ToArray();
            check(targets.Length > 0, "Sorcerer: Harmony target exists: " + patch.Name);
            foreach (var p in patch.Methods.Where(x => x.Name is "Prefix" or "Postfix").SelectMany(x => x.Parameters).Where(x => !x.Name.StartsWith("__")))
                check(targets.Any(x => x.Parameters.Any(y => y.Name == p.Name)), "Sorcerer: Harmony argument binding: " + patch.Name + "." + p.Name);
        }
    }
    private static void CheckTables(Action<bool,string> check, TypeDefinition[] types, AssemblyDefinition game)
    {
        var builder = types.Single(x => x.Name == "FeatureDefinitionCastSpellBuilder");
        var instructions = builder.Methods.Single(x => x.Name == ".cctor").Body.Instructions;
        var end = instructions.Single(x => x.OpCode.Code == Code.Stsfld && x.Operand is FieldReference f && f.Name == "BonusSpellsKnownByCasterLevel");
        var data = ((FieldReference)instructions.TakeWhile(x => x != end).Last(x => x.OpCode.Code == Code.Ldtoken).Operand).Resolve().InitialValue;
        var bonus = Enumerable.Range(0, data.Length/4).Select(n => BitConverter.ToInt32(data,n*4)).ToArray();
        var known = bonus.Skip(1).Take(20).Select(n => n+2).ToArray();
        var replacements = Enumerable.Range(1,20).Select(n => n == 1 ? 0 : 1).ToArray();
        var plan = new KnownSpellsProgressionPlan(known,replacements);
        check(known.Length == 20 && known[19] == 15 && plan.Events(12).All(x => x.Learned == 0) && plan.Events(12).Sum(x => x.Replaced) == 3,
            "Sorcerer: compiled UB known table reaches15; original18/19/20 preserve three optional replacements without false new-known awards");
        check(SpellcastingProgressionPlan.ProjectCumulative(known).Take(12).SequenceEqual(new[] { 2,3,4,5,6,8,10,12,13,14,15,15 }), "Sorcerer: cumulative known totals at actual1-12");
        check(Enumerable.Range(1,12).SelectMany(plan.Events).Select(x => x.Original).SequenceEqual(Enumerable.Range(1,20)) &&
            Enumerable.Range(1,12).SelectMany(plan.Events).Sum(x => x.Learned) == 15 && Enumerable.Range(1,12).SelectMany(plan.Events).Sum(x => x.Replaced) == 19,
            "Sorcerer: all20 learning events,15 net known spells and19 independent replacement opportunities retained");
        check(plan.CompressedReplacements().Take(12).SequenceEqual(new[] { 0,1,1,1,1,2,2,2,2,2,2,3 }), "Sorcerer: correct total replacement opportunities per compressed advancement");
        var slotsFlat = ReadAddedIntegers(builder, "SlotsByCasterLevel");
        var rows = Enumerable.Range(1,20).Select(n => slotsFlat.Skip(n*10).Take(10).ToArray()).ToArray();
        var projected = SpellcastingProgressionPlan.ProjectRows(rows);
        var circles = new[] { 1,1,2,2,3,4,5,6,7,8,9,9 };
        for(var n=1;n<=12;n++)
            check(projected[n-1].SequenceEqual(rows[SorcererProgressionRules.EffectiveLevel(true,n)-1]) && Array.IndexOf(projected[n-1],0) == circles[n-1],
                "Sorcerer: exact compiled slot row and maximum circle at actual"+n);
        check(projected[10][8] == 1 && projected[11].Take(9).SequenceEqual(new[] { 4,3,3,3,3,2,2,1,1 }), "Sorcerer: ninth circle at actual11 and full original20 slots at actual12");
        var points = ReadAddedIntegers(types.Single(x => x.Name == "ModifyPowerPoolAmountPowerSpellPoints"), "SpellPointsByLevel");
        check(Enumerable.Range(1,12).Select(n => points[SpellcastingProgressionPlan.CasterLevel(n)]).SequenceEqual(new[] { 4,6,14,17,27,38,57,73,83,94,107,133 }), "Sorcerer: original UB Spell Points table reaches133 without using total character level");
        check(Enumerable.Range(-2,26).All(n => SpellcastingProgressionPlan.CasterLevel(n) is >=0 and <=20), "Sorcerer: spellcasting index boundaries0-20");
        using var multiplier=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionAttributeModifier/AttributeModifierSorcererSorceryPointsMultiplier.json"));
        using var basis=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionAttributeModifier/AttributeModifierSorcererSorceryPointsBase.json"));
        check(multiplier.RootElement.GetProperty("modifiedAttribute").GetString()=="SorceryPoints" && multiplier.RootElement.GetProperty("modifierOperation").GetString()=="MultiplyByClassLevelBeforeAdditions" &&
            basis.RootElement.GetProperty("modifierValue").GetInt32()==1, "Sorcerer: actual source SP attribute uses base1 times class rank before additions");
        check(Enumerable.Range(1,12).Select(n=>n<2?0:SorcererProgressionRules.EffectiveLevel(true,n)).SequenceEqual(new[] { 0,2,3,4,5,7,9,11,13,15,17,20 }), "Sorcerer: base Sorcery Points progression (unlocked at2) reaches20 independently of133 Spell Points");
        var actor = types.Single(x=>x.Name=="RefreshAttributes_Patch" && x.DeclaringType?.Name=="RulesetActorPatcher");
        check(actor.Methods.Any(x=>Calls(x,"EffectiveLevel","SorcererProgressionContext")), "Sorcerer: native attribute multiplier dispatch uses effective Sorcerer (metadata)");
        var remaining = game.MainModule.Types.Single(x=>x.Name=="RulesetCharacter").Methods.Single(x=>x.Name=="get_RemainingSorceryPoints");
        check(remaining.Body.Instructions.Any(x=>x.Operand as string=="SorceryPoints") &&
            types.Single(x=>x.Name=="SpellPointsContext").Fields.Any(x=>x.Name=="PowerSpellPoints"), "Sorcerer: native SorceryPoints attribute and UB PowerSpellPoints remain distinct identities (metadata)");
        var costs = ReadAddedIntegers(types.Single(x=>x.Name=="SpellPointsContext"),"SpellCostByLevel");
        var native=game.MainModule.Types.Single(x=>x.Name=="RuleDefinitions").Methods.Single(x=>x.Name==".cctor").Body.Instructions;
        var finish=native.Select((x,i)=>(x,i)).Single(x=>x.x.OpCode.Code==Code.Stsfld && x.x.Operand is FieldReference f && f.Name=="SpellSlotCreationCost").i;
        var start=native.Take(finish).Select((x,i)=>(x,i)).Where(x=>x.x.OpCode.Code==Code.Stsfld).Last().i;
        var creation=native.Skip(start+1).Take(finish-start-1).Where(x=>x.Operand is MethodReference m && m.Name=="Add")
            .ToDictionary(x=>Integer(x.Previous.Previous).Value,x=>Integer(x.Previous).Value);
        check(creation.Keys.OrderBy(x=>x).SequenceEqual(new[] { 1,2,3,4,5 }) && creation.All(x=>x.Value>=x.Key && costs[x.Key]>=x.Key),
            "Sorcerer: compiled native conversion costs restrict created slots1-5; converting back cannot generate Sorcery Points profit");
        check(creation.All(x=>costs[x.Key]<=x.Value), "Sorcerer: generated Spell Points value does not exceed native Sorcery Point expenditure");
        var pointRows=rows.Select(x=>x.Select(v=>v==0?0:1).ToArray()).ToArray();
        check(SpellcastingProgressionPlan.ProjectRows(pointRows)[11].SequenceEqual(new[] { 1,1,1,1,1,1,1,1,1,0 }), "Sorcerer: alternate table retains one limiting slot per circle, including6-9");
        using var json=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionCastSpell/CastSpellSorcerer.json"));
        var raw=json.RootElement.GetProperty("knownCantrips").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        check(raw[15]==6 && raw[16]==5 && SpellcastingProgressionPlan.ProjectCumulative(KnownSpellsProgressionPlan.RetainedCantrips(raw)).Take(12)
            .SequenceEqual(new[] { 4,4,4,5,5,5,5,6,6,6,6,6 }) && raw[16]==5, "Sorcerer: opt-in projection retains acquired cantrips despite native17+ table decrease; original fixture unchanged");
        var recovery=types.Single(x=>x.Name=="ApplySpellSlotsForm_Patch");
        check(recovery.Methods.Any(x=>Calls(x,"EffectiveLevel","SorcererProgressionContext")), "Sorcerer: half-level resource restoration uses effective Sorcerer rank (metadata)");
        foreach(var (power,rate,cost) in new[] { ("PowerSorcerousPsionPsychokinesisFixed","LongRest",1), ("PowerSorcerousPsionPsychokinesisPoints","SorceryPoints",1) })
        {
            using var p=JsonDocument.Parse(File.ReadAllText("Diagnostics/UnfinishedBusinessBlueprints/FeatureDefinitionPower/"+power+".json"));
            check(p.RootElement.GetProperty("rechargeRate").GetString()==rate && p.RootElement.GetProperty("costPerUse").GetInt32()==cost, "Sorcerer: distinct Psion resource/recharge metadata retained: "+power);
        }
    }
    private static void CheckLearning(Action<bool,string> check,TypeDefinition[] types)
    {
        var start=new[] { "OldA","OldB" };
        var six=KnownSpellsProgressionPlan.ApplyEvent(start,new[] { "OldA" },new[] { "New6","Replacement6" });
        var seven=KnownSpellsProgressionPlan.ApplyEvent(six,new[] { "New6" },new[] { "OldA","New7" });
        check(seven.SequenceEqual(new[] { "OldB","Replacement6","OldA","New7" }) && start.SequenceEqual(new[] { "OldA","OldB" }),
            "Sorcerer: later original event can replace newly learned spell and relearn a previously removed spell; source unchanged");
        check(KnownSpellsProgressionPlan.ApplyEvent(start,Array.Empty<string>(),Array.Empty<string>()).SequenceEqual(start), "Sorcerer: skipping optional replacement leaves known spells untouched");
        var rejected=0;
        try { KnownSpellsProgressionPlan.ApplyEvent(start,new[] { "Unknown" },Array.Empty<string>()); } catch(InvalidOperationException){rejected++;}
        try { KnownSpellsProgressionPlan.ApplyEvent(start,Array.Empty<string>(),new[] { "OldB" }); } catch(InvalidOperationException){rejected++;}
        try { new KnownSpellsProgressionPlan(new int[19],new int[20]); } catch(ArgumentException){rejected++;}
        try { new KnownSpellsProgressionPlan(Enumerable.Range(1,20).Reverse(),new int[20]); } catch(ArgumentException){rejected++;}
        check(rejected==4,"Sorcerer: invalid replacements/duplicate known acquisition/malformed cumulative tables rejected");
        check(KnownSpellsProgressionPlan.ApplyEvent(start,new[] { "OldA" },new[] { "New6" }).SequenceEqual(
            KnownSpellsProgressionPlan.ApplyEvent(start,new[] { "OldA" },new[] { "New6" })), "Sorcerer: cancel/replay calculation from unchanged initial repertoire is deterministic");
        var nativeSteps = new[] { ("Cantrip",0,0), ("Unlearn6",6,0), ("Unlearn7",7,0), ("Learn6",6,1), ("Learn7",7,1), ("Feat",0,0) };
        check(KnownSpellsProgressionPlan.OrderSteps(nativeSteps).SequenceEqual(new[] { "Cantrip","Unlearn6","Learn6","Unlearn7","Learn7","Feat" }),
            "Sorcerer: actual native grouped input becomes ordered per-original-level unlearn/learn pairs, retaining unrelated steps");
        check(nativeSteps[2].Item1 == "Unlearn7" && KnownSpellsProgressionPlan.OrderSteps(new[] { ("Race",0,0), ("Feat",0,0) }).SequenceEqual(new[] { "Race","Feat" }),
            "Sorcerer: step planning never mutates source or unrelated-only flows");
        const string parent = "03ClassSorcerer12";
        var tags = new[] { parent, parent+"Sorcerer12Original19", parent+"Sorcerer12Original20", parent+"Sorcerer12Original19", "03ClassWizard12" };
        check(KnownSpellsProgressionPlan.SupplementaryTags(parent,"Sorcerer12",tags).SequenceEqual(new[] { parent+"Sorcerer12Original19", parent+"Sorcerer12Original20" }),
            "Sorcerer: actual12 tag contains prototype prefix yet routes both extra events exactly once");
        check(KnownSpellsProgressionPlan.SupplementaryTags(parent+"Sorcerer12Original19","Sorcerer12",tags).Length == 0,
            "Sorcerer: nested native grant routing cannot recursively grant its own event again");
        var casting=types.Single(x=>x.Name=="SorcererSpellcastingContext");
        check(Calls(casting.Methods.Single(x=>x.Name=="KnownBefore"),"ApplyEvent","KnownSpellsProgressionPlan"),"Sorcerer: runtime virtual repertoire uses the tested production event model (metadata)");
        check(Calls(casting.NestedTypes.Single(x=>x.Name=="GrantLearningPatch").Methods.Single(x=>x.Name=="Postfix"),"GrantCantripsAndSpellsByTag"),"Sorcerer: supplementary events use native lose-then-grant, not Wizard scribing (metadata)");
        check(Calls(casting.Methods.Single(x=>x.Name=="InvalidateLater"),"ConfigureLearning"),"Sorcerer: edits invalidate later dependent event choices before budgets refresh (metadata)");
    }
    private static void CheckRewards(Action<bool,string> check)
    {
        var feat=new FeatureDefinitionPointPool { Name="Feat",PoolType="Feat",PoolAmount=1 };
        var asi=new FeatureDefinitionFeatureSet { Name="ASIAndFeat",Mode=FeatureDefinitionFeatureSet.FeatureSetMode.Union };
        asi.FeatureSet.Add(new FeatureDefinitionPointPool { Name="Ability",PoolType="AbilityScore",PoolAmount=2 });asi.FeatureSet.Add(feat);
        foreach(var meta2024 in new[] { false,true })
        foreach(var cap2024 in new[] { false,true })
        {
            var source=new[] { 4,8,12,16,19 }.Select(x=>new FeatureUnlockByLevel(asi,x)).ToList();
            source.AddRange(new[] { 2,6,10,14,18 }.Select(x=>new FeatureUnlockByLevel(feat,x)));
            source.Add(new(new FeatureDefinitionPointPool { Name="MetaInitial",PoolType="Metamagic",PoolAmount=2 },meta2024?2:3));
            var extra=new FeatureDefinitionPointPool { Name="MetaAdditional",PoolType="Metamagic",PoolAmount=1 };
            source.Add(new(extra,10));source.Add(new(extra,17));source.Add(new(new FeatureDefinitionPower { Name=cap2024?"Apotheosis20":"Restoration20" },20));
            var before=source.Select(x=>(x.FeatureDefinition,x.Level)).ToArray();var transform=new CompressedProgressionDefinitions("SorcRewards"+meta2024+cap2024);
            var output=transform.Prepare(source,"Sorcerer");var pools=output.SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPointPool>().ToArray();
            check(pools.Where(x=>x.PoolType=="Feat").Sum(x=>x.PoolAmount)==10 && pools.Where(x=>x.PoolType=="AbilityScore").Sum(x=>x.PoolAmount)==10,
                "Sorcerer: five ASIs plus both extra-feat options retain ten feat and ten ability points: "+meta2024+"/"+cap2024);
            check(pools.Where(x=>x.PoolType=="Metamagic").Sum(x=>x.PoolAmount)==4 && output.Any(x=>x.Level==8) && output.Any(x=>x.Level==11),
                "Sorcerer: metamagic2/3 initial two choices plus original10/17 choices survive: "+meta2024+"/"+cap2024);
            var origins=pools.Where(transform.PoolOrigins.ContainsKey).Select(x=>transform.PoolOrigins[x]).ToArray();
            check(origins.Distinct().Count()==origins.Length && source.Select(x=>(x.FeatureDefinition,x.Level)).SequenceEqual(before),
                "Sorcerer: repeated reward origins remain independent and source fixture unmodified: "+meta2024+"/"+cap2024);
            check(output.Where(x=>x.Level==12).SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPower>().Single().Name==
                (cap2024?"Apotheosis20":"Restoration20"),"Sorcerer: exactly the selected20 capstone appears at actual12: "+meta2024+"/"+cap2024);
        }
    }
    private static int[] ReadAddedIntegers(TypeDefinition type,string field)
    {
        var list=type.Methods.Single(x=>x.Name==".cctor").Body.Instructions;
        var end=list.Select((x,i)=>(x,i)).Single(x=>x.x.OpCode.Code==Code.Stsfld && x.x.Operand is FieldReference f && f.Name==field).i;
        var start=list.Take(end).Select((x,i)=>(x,i)).Where(x=>x.x.OpCode.Code==Code.Stsfld).Select(x=>x.i).DefaultIfEmpty(-1).Last();
        return list.Skip(start+1).Take(end-start-1).Where(x=>x.Operand is MethodReference m && m.Name=="Add" &&
            m.DeclaringType is GenericInstanceType g && g.GenericArguments.Single().MetadataType==MetadataType.Int32).Select(x=>Integer(x.Previous).Value).ToArray();
    }
    private static int? Integer(Instruction i)=>i.OpCode.Code switch { Code.Ldc_I4_M1=>-1,Code.Ldc_I4_0=>0,Code.Ldc_I4_1=>1,Code.Ldc_I4_2=>2,Code.Ldc_I4_3=>3,
        Code.Ldc_I4_4=>4,Code.Ldc_I4_5=>5,Code.Ldc_I4_6=>6,Code.Ldc_I4_7=>7,Code.Ldc_I4_8=>8,Code.Ldc_I4 or Code.Ldc_I4_S=>Convert.ToInt32(i.Operand),_=>null };
}
