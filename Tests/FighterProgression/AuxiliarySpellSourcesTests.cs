using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using P = SolastaUnfinishedBusiness.Models.AuxiliarySpellSourcesPlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;

// Production registration, routing, selection journals and calculator execute on net9.
// Blueprint/native signature audits are explicitly separate. No Unity/native mutation is executed.
internal static class AuxiliarySpellSourcesTests
{
    private static readonly string[] Files=Directory.GetFiles("Diagnostics","*.json",SearchOption.AllDirectories);
    private static readonly Dictionary<string,JsonElement> Cache=new();
    private static P registry;
    private static S.Tables nativeTables;
    private static readonly int[] Full={1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
    private static string Id(string s)=>s?.Split(':')[1];
    private static JsonElement Read(string name,string kind=null)
    {
        var key=kind+":"+name;if(Cache.TryGetValue(key,out var cached))return cached;
        var path=Files.Where(p=>Path.GetFileNameWithoutExtension(p)==name && (kind==null || Path.GetFileName(Path.GetDirectoryName(p))==kind)).OrderBy(p=>p.Contains("OfficialBlueprints")?0:1).First();
        using var doc=JsonDocument.Parse(File.ReadAllText(path));return Cache[key]=doc.RootElement.Clone();
    }
    private static IEnumerable<JsonElement> Graph(IEnumerable<string> names)
    {
        foreach(var name in names.Distinct())
        {
            var d=Read(name);yield return d;
            if(d.TryGetProperty("featureSet",out var children))foreach(var child in Graph(children.EnumerateArray().Select(c=>Id(c.GetString()))))yield return child;
        }
    }
    private static R.Spell[] Spells(string list)=>Read(list,"SpellListDefinition").GetProperty("spellsByLevel").EnumerateArray()
        .SelectMany(r=>r.GetProperty("spells").EnumerateArray().Select(s=>new R.Spell(Id(s.GetString()),r.GetProperty("level").GetInt32()))).GroupBy(s=>s.Id).Select(g=>g.First()).ToArray();
    private static int[] Rows(JsonElement json,string key)
    {var raw=json.GetProperty(key).EnumerateArray().Select(x=>x.GetInt32()).ToArray();return Enumerable.Range(0,20).Select(i=>raw.Length==0?0:raw[Math.Min(i,raw.Length-1)]).ToArray();}
    private static int[][] Slots(JsonElement cast)
    {var rows=cast.GetProperty("slotsPerLevels").EnumerateArray().Select(r=>r.GetProperty("slots").EnumerateArray().Select(x=>x.GetInt32()).Concat(new int[9]).Take(9).ToArray()).ToArray();return Enumerable.Range(0,20).Select(i=>rows.Length==0?new int[9]:rows[Math.Min(i,rows.Length-1)]).ToArray();}
    private static bool Reject(Action action,P.Code? code=null)
    {try{action();return false;}catch(P.Rejection ex){return code==null || ex.Diagnostic==code;}catch(InvalidOperationException){return code==null;}catch(ArgumentException){return code==null;}}
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m,string owner,string name)=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name==owner && r.Name==name);
    private static IEnumerable<P.Source> Fixtures()
    {
        foreach(var pair in new[]{("CharacterRaceDefinition",P.Origin.Race),("FeatDefinition",P.Origin.Feat),("FeatDefinitionWithPrerequisites",P.Origin.Feat),("FightingStyleDefinition",P.Origin.Style)})
        foreach(var path in Files.Where(p=>Path.GetFileName(Path.GetDirectoryName(p))==pair.Item1))
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));var root=doc.RootElement;var rootId=root.GetProperty("name").GetString();
            var refs=root.TryGetProperty("features",out var features)?features.EnumerateArray().Select(f=>Id(f.GetString())):
                root.GetProperty("featureUnlocks").EnumerateArray().Select(u=>Id(u.GetProperty("featureDefinition").GetString()));
            var graph=Graph(refs).ToArray();var casts=graph.Where(f=>f.GetProperty("$type").GetString().StartsWith("FeatureDefinitionCastSpell,",StringComparison.Ordinal)).ToArray();
            foreach(var cast in casts)
            {
                var castId=cast.GetProperty("name").GetString();var list=Spells(Id(cast.GetProperty("spellListDefinition").GetString()));
                var pools=new List<P.Pool>();
                foreach(var pool in graph.Where(f=>f.GetProperty("$type").GetString().StartsWith("FeatureDefinitionPointPool,",StringComparison.Ordinal)))
                {
                    var type=pool.GetProperty("poolType").GetString();if(type is not ("Cantrip" or "Spell"))continue;
                    var overrideList=pool.GetProperty("spellListOverride");var allowed=overrideList.ValueKind==JsonValueKind.Null?list:Spells(Id(overrideList.GetString()));
                    var min=pool.GetProperty("minSpellLevel").GetInt32();var max=R.ChoiceMaximum(pool.GetProperty("maxSpellLevel").GetInt32(),type=="Cantrip");
                    pools.Add(new P.Pool(pool.GetProperty("name").GetString()+":"+(type=="Cantrip"?"cantrip":"spell"),allowed.Where(s=>(s.Circle==0)==(type=="Cantrip") && s.Circle>=min && s.Circle<=max),pool.GetProperty("poolAmount").GetInt32(),type=="Cantrip"));
                }
                if(pools.Count==0 && cast.GetProperty("spellKnowledge").GetString()=="Selection" && Rows(cast,"knownCantrips")[0]>0)
                    pools.Add(new P.Pool(castId+":native-cantrips",list.Where(s=>s.Circle==0),Rows(cast,"knownCantrips")[0],true));
                yield return new P.Source(pair.Item2+":"+rootId+":"+castId,pair.Item2,rootId,castId,cast.GetProperty("spellCastingOrigin").GetString(),cast.GetProperty("spellcastingAbility").GetString(),null,
                    P.Domain.NativeSlots,1,cast.GetProperty("slotsRecharge").GetString(),cast.GetProperty("spellKnowledge").GetString(),list,pools,Slots(cast),Rows(cast,"knownSpells"),Rows(cast,"knownCantrips"));
            }
        }
        foreach(var inv in new[]{"InvocationPactTome","BookAncientSecrets"})
        {
            var json=Read(inv);var feature=Id(json.GetProperty("grantedFeature").GetString());
            foreach(var pool in Graph(new[]{feature}).Where(f=>f.GetProperty("$type").GetString().StartsWith("FeatureDefinitionPointPool,",StringComparison.Ordinal)))
            {
                var cantrip=pool.GetProperty("poolType").GetString()=="Cantrip";var ritual=pool.GetProperty("ritualOnly").GetBoolean();
                var list=Spells(Id(pool.GetProperty("spellListOverride").GetString())).Where(s=>s.Circle>=pool.GetProperty("minSpellLevel").GetInt32() && s.Circle<=R.ChoiceMaximum(pool.GetProperty("maxSpellLevel").GetInt32(),cantrip) &&
                    (!ritual || Read(s.Id,"SpellDefinition").GetProperty("ritual").GetBoolean())).ToArray();
                var id=pool.GetProperty("name").GetString();
                yield return new P.Source("Invocation:"+inv+":"+id,P.Origin.Invocation,inv,id,"Attachment","Charisma","Warlock",ritual?P.Domain.NativeRitual:P.Domain.AttachedClass,
                    json.GetProperty("requiredLevel").GetInt32(),"NativeClass","ChoicePool",list,new[]{new P.Pool(id,list,pool.GetProperty("poolAmount").GetInt32(),cantrip)});
            }
        }
    }
    private static C Calculator()=>new("Aux13.5D-fixture-v1",new[]{new C.ClassProfile("Wizard",true,new C.CastingProfile("CastSpellWizard",C.CasterKind.Full,1,true,Full)),
        new C.ClassProfile("Sorcerer",true,new C.CastingProfile("CastSpellSorcerer",C.CasterKind.Full,1,true,Full)),new C.ClassProfile("Warlock",true,new C.CastingProfile("CastSpellWarlock",C.CasterKind.Pact,1,true,Enumerable.Repeat(5,20))),
        new C.ClassProfile("Rogue",true,null,3,new[]{new C.SubclassProfile("RoguishShadowCaster",true,true,new C.CastingProfile("CastSpellShadowcaster",C.CasterKind.OneThird,3,false,Enumerable.Range(1,20).Select(n=>n<3?0:n<7?1:n<13?2:n<19?3:4)))})});
    private static C.Result Calculate(IEnumerable<C.ClassInput> classes,IEnumerable<C.AuxiliaryInput> aux)
    {var rows=classes.ToArray();return Calculator().Calculate(new C.Snapshot(rows.Sum(c=>c.ActualLevel),rows,rows.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel)),aux));}
    private static void Complete(P.Selection tx,P.Source source)
    {foreach(var pool in source.Pools)tx.Select(source.Id,pool.Id,pool.Allowed.Take(pool.Amount).Select(s=>s.Id));}
    internal static void Run(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var behavior=0;var metadata=0;
        void Behavior(bool ok,string label){behavior++;check(ok,"Auxiliary production behavior: "+label);}
        void Audit(bool ok,string label){metadata++;check(ok,"Auxiliary fixture/compiled audit: "+label);}
        var compiled=mod.MainModule.Types.SelectMany(Types).ToArray();
        var flat=CompressedMulticasterTests.AddedIntegers(compiled.Single(t=>t.Name=="FeatureDefinitionCastSpellBuilder"),"SlotsByCasterLevel");
        nativeTables=new S.Tables(Enumerable.Range(1,20).Select(n=>flat.Skip(n*10).Take(9)),
            CompressedMulticasterTests.AddedIntegers(compiled.Single(t=>t.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel"),
            CompressedMulticasterTests.AddedIntegers(compiled.Single(t=>t.Name=="SpellPointsContext"),"SpellCostByLevel"));
        var sources=Fixtures().ToArray();registry=new P(sources);
        Audit(sources.Count(s=>s.Kind==P.Origin.Race)==16 && sources.Count(s=>s.Kind==P.Origin.Feat)==40 && sources.Count(s=>s.Kind==P.Origin.Style)==2,
            "16 racial-root casting sources,40 feat sources,2 styles in available snapshots (runtime capture also discovers attached/invocation/background sources)");
        foreach(var source in sources)
        {
            source.ValidateOwner(source.Root,source.Feature,source.Ability,null,20);
            var classes=source.OwnerClass==null?Array.Empty<C.ClassInput>():new[]{new C.ClassInput(source.OwnerClass,1)};
            var result=Calculate(classes,new[]{source.CalculatorInput()});
            Behavior(result.IsValid && result.ResourceTableIndex==0 && result.Classes.Count==classes.Length,source.Id+": auxiliary adds no class, participant or standard resource");
            Behavior(source.Ability is "Intelligence" or "Wisdom" or "Charisma" && source.NativeSlotRows.Count==20 && source.NativeKnown.Count==20 && source.NativeCantrips.Count==20,
                source.Id+": explicit ability and original native acquisition/resource rows retained");
            if(source.Pools.Count>0)
            {
                var journal=new P.Selection(registry,new[]{source.Id});Complete(journal,source);var selected=journal.Commit();
                Behavior(ReferenceEquals(selected,journal.Commit()) && selected.Values.Sum(v=>v.Count)==source.Pools.Sum(p=>p.Amount),source.Id+": exact native point-pool awards, idempotent managed commit");
            }
        }
        Combinations(Behavior); Journals(Behavior); ResourcesAndUnknown(Behavior,Audit); Bindings(Audit,game,mod);
        Console.WriteLine($"Stage13.5D: {behavior} executed production behavior checks; {metadata} fixture/compiled-binding checks (no Unity runtime).");
    }
    private static void Combinations(Action<bool,string> check)
    {
        var race=registry.Sources.Values.First(s=>s.Root=="ElfHigh");var feat=registry.Sources.Values.First(s=>s.Root=="FeatMagicInitiateWizard");var style=registry.Sources.Values.First(s=>s.Root=="BlessedWarrior");
        foreach(var source in new[]{race,feat,style})
        {
            var standalone=Calculate(Array.Empty<C.ClassInput>(),new[]{source.CalculatorInput(1)});
            var one=Calculate(new[]{new C.ClassInput("Wizard",8)},new[]{source.CalculatorInput(1)});
            var two=Calculate(new[]{new C.ClassInput("Wizard",12,true),new C.ClassInput("Sorcerer",8,true)},new[]{source.CalculatorInput(1)});
            var third=Calculate(new[]{new C.ClassInput("Rogue",8,true,"RoguishShadowCaster")},new[]{source.CalculatorInput(1)});
            check(standalone.IsValid && standalone.CastingParticipantCount==0 && standalone.ResourceTableIndex==0,source.Root+": race/feat/style only has no standard casting class");
            check(one.IsValid && one.CastingParticipantCount==1 && one.ResourceTableIndex==8 && two.IsValid && two.Classes.Count==2 && two.ResourceTableIndex==20,
                source.Root+": real class budgets unchanged with one or two casters");
            check(third.IsValid && third.Classes.Single().EffectiveClassLevel==11 && third.Classes.Single().EffectiveCastingLevel==8 && third.ResourceTableIndex==3,
                source.Root+": third-caster subclass plus auxiliary magic preserves actual casting and solo rounding");
        }
        check(style.Ability=="Charisma" && registry.Sources.Values.Single(s=>s.Root=="DruidicWarrior").Ability=="Wisdom" && race.Ability=="Intelligence",
            "installed Blessed Warrior CHA, Druidic Warrior WIS and High Elf INT remain independent");
        var initiate=registry.Sources.Values.Single(s=>s.Root=="FeatMagicInitiateWizard");
        check(initiate.NativeKnown[0]==0 && initiate.NativeKnown[4]==2 && initiate.Pools.Single(p=>!p.Cantrip).Amount==1 && initiate.Pools.Single(p=>p.Cantrip).Amount==2 && initiate.CalculatorInput(1).MaximumSpellCircle==1,
            "Magic Initiate raw Flat known0 before5/known2 from5 does not replace actual pool budgets: one spell, two cantrips, own first-circle resources");
    }
    private static void Journals(Action<bool,string> check)
    {
        var race=registry.Sources.Values.Single(s=>s.Root=="ElfHigh");var feat=registry.Sources.Values.Single(s=>s.Root=="FeatMagicInitiateWizard");
        var tx=new P.Selection(registry,new[]{race.Id,feat.Id});
        var shared=race.Pools[0].Allowed.Intersect(feat.Pools.Single(p=>p.Cantrip).Allowed,new SpellIdentity()).First().Id;
        tx.Select(race.Id,race.Pools[0].Id,new[]{shared});
        var featCantrips=feat.Pools.Single(p=>p.Cantrip);tx.Select(feat.Id,featCantrips.Id,new[]{shared,featCantrips.Allowed.First(s=>s.Id!=shared).Id});
        tx.Select(feat.Id,feat.Pools.Single(p=>!p.Cantrip).Id,feat.Pools.Single(p=>!p.Cantrip).Allowed.Take(1).Select(s=>s.Id));
        var before=tx.Preview();tx.Back(race.Id);
        check(tx.Preview().Values.Sum(v=>v.Count)==3 && Reject(()=>tx.Commit()),"same spell has two legal origins; Back race preserves feat selections and mandatory race budget");
        Complete(tx,race);var complete=tx.Commit();
        check(complete.Values.Count(v=>v.Contains(shared))==2,"two confirmed origins are not merged or deleted as duplicate spells");
        var reset=new P.Selection(registry,new[]{race.Id},complete);Complete(reset,race);reset.Reset();
        check(reset.Preview().Count==complete.Count && Reject(()=>reset.Back(feat.Id),P.Code.WrongOwner),"Reset/Back cannot modify a foreign confirmed source");
        var cancelled=new P.Selection(registry,new[]{race.Id,feat.Id});Complete(cancelled,race);Complete(cancelled,feat);cancelled.Cancel();
        check(cancelled.Preview().Count==0 && Reject(()=>cancelled.Commit()),"Cancel clears only provisional auxiliary grants, no native/class mutation");
        var duplicate=new P.Selection(registry,new[]{feat.Id});
        check(Reject(()=>duplicate.Select(feat.Id,featCantrips.Id,new[]{shared,shared}),P.Code.InvalidChoice) && duplicate.Preview().Count==0,"duplicate selection rejected before publication");
        check(Reject(()=>duplicate.Select(feat.Id,featCantrips.Id,new[]{"Unknown",shared}),P.Code.InvalidChoice) && Reject(()=>duplicate.Select(race.Id,race.Pools[0].Id,new[]{shared}),P.Code.WrongOwner),
            "unknown spell and foreign pending source rejected, not assigned to the selected class");
    }
    private sealed class SpellIdentity:IEqualityComparer<R.Spell>
    {public bool Equals(R.Spell a,R.Spell b)=>a.Id==b.Id;public int GetHashCode(R.Spell s)=>s.Id.GetHashCode();}
    private static void ResourcesAndUnknown(Action<bool,string> check,Action<bool,string> audit)
    {
        var feat=registry.Sources.Values.Single(s=>s.Root=="FeatMagicInitiateWizard");var spell=feat.Pools.Single(p=>!p.Cantrip).Allowed.First().Id;
        check(feat.Route(spell,false)==P.Domain.NativeSlots && Reject(()=>feat.Route(spell,true),P.Code.WrongOwner),"Magic Initiate native free slot cannot accidentally debit an unrelated shared pool");
        check(feat.RestoresOn("LongRest",false) && !feat.RestoresOn("ShortRest",false) && !feat.RestoresOn("LongRest",true),
            "own native long-rest free slots restore only on the right rest, never simulation/short rest");
        var tome=registry.Sources.Values.Single(s=>s.Root=="InvocationPactTome");var rituals=registry.Sources.Values.Single(s=>s.Root=="BookAncientSecrets");
        check(tome.OwnerClass=="Warlock" && tome.Pools.Single().Amount==3 && rituals.Resource==P.Domain.NativeRitual && rituals.Pools.Single().Amount==2,
            "Tome three-cantrip and Book two ritual awards retain explicit Warlock attachment without an extra class");
        check(Reject(()=>rituals.Route(rituals.Spells.First().Id,true,true),P.Code.WrongOwner),"ritual-only acquisition does not become a paid ordinary known spell or free Pact slot");
        var nativeFey=Read("CustomInvocationFeyTeleportMistyStepIntelligence","InvocationDefinitionCustom");
        audit(!nativeFey.GetProperty("consumesSpellSlot").GetBoolean() && !nativeFey.GetProperty("longRestRecharge").GetBoolean(),
            "observed Fey custom invocation free casting flags retained; short-rest marker handled separately by native consume/recharge patches");
        var free=new P.Source("Feat:free-inv",P.Origin.Feat,"FeatFeyTeleportationInt","CustomInvocationFeyTeleportMistyStepIntelligence","Invocation","Intelligence",null,
            P.Domain.NativeInvocation,1,"ShortRest","FixedList",new[]{new R.Spell("MistyStep",2)});
        var attached=new P.Source("Feat:attached",P.Origin.Feat,"FeatFeyTeleportationInt","AutoPreparedSpellsFeyTeleportation","Attachment","NativeClassAbility",null,
            P.Domain.AttachedClass,1,"NativeClass","AutoPrepared",new[]{new R.Spell("MistyStep",2)});
        check(free.Route("MistyStep",false)==P.Domain.NativeInvocation && free.Recharge=="ShortRest" && attached.Route("MistyStep",true,true)==P.Domain.AttachedClass,
            "same Fey spell has independent native free invocation and explicitly matched class-paid route");
        var tables=nativeTables;
        var result=Calculate(new[]{new C.ClassInput("Wizard",8)},new[]{free.CalculatorInput(),attached.CalculatorInput()});
        var budget=new S.Budget(tables,new S.Allocation(tables,result,S.ResourceMode.SpellPoints));var original=budget.RemainingPoints;
        var route=free.Route("MistyStep",false);
        check(route==P.Domain.NativeInvocation && budget.RemainingPoints==original && attached.Route("MistyStep",true,true)==P.Domain.AttachedClass &&
            budget.Spend("explicit-native-class-paid",2) && budget.RemainingPoints==original-3,
            "production auxiliary routing composes with one real standard ledger: native free route leaves points intact, explicit class-paid route debits once");
        check(free.CanUseNativeInvocation(false) && !free.CanUseNativeInvocation(true),"limited native free invocation respects its used flag without touching standard resources");
        var atWill=new P.Source("Invocation:at-will",P.Origin.Invocation,"ShroudOfShadow","ShroudOfShadow","Invocation","Charisma","Warlock",P.Domain.NativeInvocation,15,"AtWill","FixedList",
            new[]{new R.Spell("Invisibility",2)},atWillInvocation:true);
        check(atWill.CanUseNativeInvocation(false) && atWill.CanUseNativeInvocation(true),"observed at-will invocation domain does not acquire a one-use cap");
        check(Reject(()=>free.Route("MistyStep",true),P.Code.WrongOwner) && Reject(()=>attached.Route("MistyStep",true,false),P.Code.WrongOwner),"free and paid routing cannot silently switch owners/domains");
        var unsupported=new P.Source("UnknownClassFeat",P.Origin.Feat,"NewFeat","CastUnknown","Class","Intelligence",null,P.Domain.Unsupported,1,"LongRest","Selection",new[]{new R.Spell("Shield",1)});
        var local=new P(new[]{unsupported});
        check(Reject(()=>unsupported.ValidateOwner("NewFeat","CastUnknown","Intelligence",null,20),P.Code.UnsupportedSource) && Reject(()=>new P.Selection(local,new[]{unsupported.Id}),P.Code.UnsupportedSource),
            "null Class-origin owner is not guessed; unsupported source blocked before any pending grants");
        check(Reject(()=>registry.Require("Unregistered"),P.Code.UnknownSource) && Reject(()=>new P(registry.Sources.Values.Append(feat)),P.Code.DuplicateSource) &&
            Reject(()=>new P.Selection(registry,new[]{feat.Id,feat.Id}),P.Code.DuplicateSource),"unknown, duplicate registry and duplicate pending sources produce structured diagnostics");
        check(Reject(()=>feat.ValidateOwner(feat.Root,feat.Feature,"Wisdom",null,20),P.Code.WrongOwner) && Reject(()=>feat.ValidateOwner(feat.Root,feat.Feature,feat.Ability,"Wizard",20),P.Code.WrongOwner),
            "Wizard spell list/ClassHolder hint is not an actual casting-class owner or permission to replace ability");
        var expected=new[]{feat.CalculatorInput(1)};
        P.ValidateInputs(expected,expected);
        check(Reject(()=>P.ValidateInputs(expected,Array.Empty<C.AuxiliaryInput>()),P.Code.UnknownSource) &&
            Reject(()=>P.ValidateInputs(expected,new[]{new C.AuxiliaryInput("unregistered",C.AuxiliaryRole.Feat)}),P.Code.UnknownSource),
            "admission snapshot must include every active confirmed source and cannot manufacture a registered-looking feat ID");
        check(Reject(()=>P.ValidateInputs(expected,new[]{new C.AuxiliaryInput(feat.Id,C.AuxiliaryRole.Feat,9,"Wizard")}),P.Code.WrongOwner) &&
            Reject(()=>P.ValidateInputs(expected,expected.Concat(expected)),P.Code.DuplicateSource),
            "early snapshot rejects guessed Wizard ownership, inflated auxiliary access and duplicate registrations");
        foreach(var path in Files.Where(p=>Path.GetFileName(Path.GetDirectoryName(p))=="InvocationDefinition"))
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));var inv=doc.RootElement;
            if(inv.GetProperty("grantedSpell").ValueKind==JsonValueKind.Null)continue;
            var id=inv.GetProperty("name").GetString();var spellId=Id(inv.GetProperty("grantedSpell").GetString());var definition=Read(spellId,"SpellDefinition");
            var paid=inv.GetProperty("consumesSpellSlot").GetBoolean();var longRest=inv.GetProperty("longRestRecharge").GetBoolean();
            var native=new P.Source("Invocation:"+id,P.Origin.Invocation,id,id,"Invocation","Charisma","Warlock",paid?P.Domain.PaidInvocation:P.Domain.NativeInvocation,
                Math.Max(1,inv.GetProperty("requiredLevel").GetInt32()),longRest?"LongRest":"AtWill","FixedList",new[]{new R.Spell(spellId,definition.GetProperty("spellLevel").GetInt32())},atWillInvocation:!longRest);
            check(paid?Reject(()=>native.Route(spellId,false),P.Code.PendingAtomicJournal):native.Route(spellId,false)==P.Domain.NativeInvocation && native.CanUseNativeInvocation(false) && native.CanUseNativeInvocation(true)==!longRest,
                "observed invocation "+id+": exact spell, native at-will/limited flags, unsafe paid route explicitly rejected");
        }
        check(!C.MixedGameplayReady,"M05 audit leaves mixed activation OFF and native journal unfinished");
    }
    private static void Bindings(Action<bool,string> check,AssemblyDefinition game,AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();var ctx=types.Single(t=>t.Name=="AuxiliarySpellSourcesContext");
        var final=types.Single(t=>t.Name=="CharacterBuildingManagerPatcher").NestedTypes.Single(t=>t.Name=="FinalizeCharacter_Patch").Methods.Single(m=>m.Name=="Prefix");
        var calls=final.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
        check(Array.FindIndex(calls,m=>m.DeclaringType.Name==ctx.Name && m.Name=="ValidateBeforeFinalize")<Array.FindIndex(calls,m=>m.Name=="GrantRaceFeatures") &&
            Array.FindIndex(calls,m=>m.DeclaringType.Name==ctx.Name && m.Name=="ValidateBeforeFinalize")<Array.FindIndex(calls,m=>m.Name=="GrantCustomFeaturesFromFeats"),
            "aux preflight runs before race/style/feat native finalization mutations");
        var helper=types.Single(t=>t.Name=="LevelUpHelper");
        check(Calls(helper.Methods.Single(m=>m.Name=="GrantCustomFeaturesFromFeats"),ctx.Name,"RequireGrantPreflight") &&
            Calls(helper.Methods.Single(m=>m.Name=="GrantSpellsOrCantripsFromFeatCastSpell"),ctx.Name,"RequireGrantPreflight"),"direct feat helpers also refuse unjournaled mutations before native invocations/repertoires/spells");
        foreach(var patch in ctx.NestedTypes.Where(t=>t.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPatch")))
        {
            var attribute=patch.CustomAttributes.Single(a=>a.AttributeType.Name=="HarmonyPatch");var owner=(TypeReference)attribute.ConstructorArguments[0].Value;var name=(string)attribute.ConstructorArguments[1].Value;
            var target=game.MainModule.Types.Single(t=>t.FullName==owner.FullName).Methods.Single(m=>m.Name==name);
            var prefix=patch.Methods.Single(m=>m.Name=="Prefix");
            check(prefix.Parameters.Where(p=>p.Name.StartsWith("__",StringComparison.Ordinal) && int.TryParse(p.Name.Substring(2),out _)).All(p=>target.Parameters[int.Parse(p.Name.Substring(2))].ParameterType.FullName==p.ParameterType.FullName),
                "native signature "+patch.Name+": exact typed index argument (compiled inspection only)");
        }
        check(Calls(types.Single(t=>t.Name=="StandardMulticasterResourcesContext").Methods.Single(m=>m.Name=="ValidateHero"),ctx.Name,"ValidateSnapshot") &&
            Calls(types.Single(t=>t.Name=="MultipleSpellRepertoiresContext").Methods.Single(m=>m.Name=="AutoPrepared"),ctx.Name,"AttachedAutoSpells"),
            "standard owner validates auxiliary snapshot; source-authorized auto-spells retain native attachment");
        check(Calls(ctx.Methods.Single(m=>m.Name=="FinishNativeRest"),"RulesetSpellRepertoire","RestoreAllSpellSlots") &&
            !Calls(ctx.Methods.Single(m=>m.Name=="FinishNativeRest"),"RulesetCharacter","RestoreAllSpellSlots"),
            "suppressed global rest gets scoped auxiliary native restoration without refilling standard/Pact pools");
        foreach(var name in new[]{"TrainFeat_Patch","TrainFightingStyle_Patch"})
        {
            var prefix=types.Single(t=>t.Name=="CharacterBuildingManagerPatcher").NestedTypes.Single(t=>t.Name==name).Methods.Single(m=>m.Name=="Prefix");
            check(prefix.ReturnType.FullName=="System.Boolean" && Calls(prefix,ctx.Name,"ValidateRootChoice"),name+": unknown pending source can reject before pool/native training writes");
        }
        check(Calls(ctx.NestedTypes.Single(t=>t.Name=="RepertoireGuard").Methods.Single(m=>m.Name=="Prefix"),"MultipleSpellRepertoiresContext","IsRegisteredCastingSource") &&
            Calls(types.Single(t=>t.Name=="StandardMulticasterResourcesContext").Methods.Single(m=>m.Name=="EnterSpell"),ctx.Name,"ValidatePaidSpell"),
            "native mutation validates registered class ownership; paid spending validates ritual-only/independent source domains before Pact dispatch");
        var mutations=Types(ctx).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>();
        check(!mutations.Any(m=>m.Name is "GrantSpellRepertoire" or "GrantSpell" or "GrantCantrip" or "GrantPowerSpellPoints" or "SpendSpellSlot"),
            "adapter creates/grants no native spells/repertoires/pools or shared spending; native auxiliary rest is separately scoped");
    }
}
