using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;

// Real shared transformer execution with managed doubles; metadata inspection is reported separately.
// No test here executes the game's eligibility, reactions, critical hits, spellcasting, respec or save/load.
internal static class RogueTests
{
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    internal static void Run(Action<bool,string> check, AssemblyDefinition game, AssemblyDefinition mod)
    {
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();
        var context=types.Single(x=>x.Name=="RogueProgressionContext");
        var builders=mod.MainModule.Types.Where(t=>t.BaseType?.Name=="AbstractSubclass" &&
            t.Methods.Any(m=>m.Name=="get_Klass" && Calls(m,"get_Rogue"))).ToArray();
        check(builders.Length==9,"nine Rogue subclass builders registered by compiled class ownership");
        var fixtures=new List<(string,string)>{("Rogue","OfficialBlueprints")};
        fixtures.AddRange(new[]{"RoguishDarkweaver","RoguishHoodlum","RoguishShadowCaster","RoguishThief"}
            .Select(x=>(x,"OfficialBlueprints")));
        foreach(var b in builders)
        {
            if(File.Exists("Diagnostics/UnfinishedBusinessBlueprints/CharacterSubclassDefinition/"+b.Name+".json"))
                fixtures.Add((b.Name,"UnfinishedBusinessBlueprints"));
            else
            {
                check(b.Name=="RoguishSwashbuckler","identify missing snapshot explicitly: "+b.Name);
                var ctor=b.Methods.Single(x=>x.IsConstructor && !x.IsStatic);
                check(ctor.Body.Instructions.Count(x=>x.Operand is MethodReference r && r.Name=="AddFeaturesAtLevel")==4,
                    "Swashbuckler constructor retains four acquisition groups (no runtime execution claim)");
            }
        }
        foreach(var (name,folder) in fixtures)
        {
            using var j=JsonDocument.Parse(File.ReadAllText("Diagnostics/"+folder+"/"+
                (name=="Rogue"?"CharacterClassDefinition/":"CharacterSubclassDefinition/")+name+".json"));
            var source=j.RootElement.GetProperty("featureUnlocks").EnumerateArray().Select(x=>new FeatureUnlockByLevel(
                new FeatureDefinition{Name=x.GetProperty("featureDefinition").GetString()},x.GetProperty("level").GetInt32())).ToList();
            var original=source.Select(x=>(x.FeatureDefinition,x.Level)).ToArray();
            var output=new CompressedProgressionDefinitions("TestRogue"+name).Prepare(source,name);
            check(output.SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition))
                .Where(x=>x is not FeatureDefinitionFeatureSet).SequenceEqual(source.OrderBy(x=>x.Level).Select(x=>x.FeatureDefinition)),
                "real transform preserves every fixture event and order: "+name);
            check(source.Select(x=>(x.FeatureDefinition,x.Level)).SequenceEqual(original) && output.All(x=>x.Level<=12),
                "fixture remains unchanged and output obeys cap: "+name);
        }
        check(Enumerable.Range(1,12).Select(x=>RogueProgressionRules.EffectiveLevel(true,x))
            .SequenceEqual(new[]{1,2,3,4,5,7,9,11,13,15,17,20}),"Rogue effective mapping");
        check(Enumerable.Range(0,21).All(x=>RogueProgressionRules.EffectiveLevel(false,x)==x) &&
            RogueProgressionRules.EffectiveLevel(true,13)==13,"disabled and unsupported old Rogue scaling unchanged");
        check(!RogueProgressionRules.IsCapped(true,11) && RogueProgressionRules.IsCapped(true,12) &&
            !RogueProgressionRules.IsCapped(false,12),"Rogue cap is opt-in");
        check(RogueProgressionRules.CunningStrikeChoices(RogueProgressionRules.EffectiveLevel(true,7))==1 &&
            RogueProgressionRules.CunningStrikeChoices(RogueProgressionRules.EffectiveLevel(true,8))==2,
            "Improved Cunning Strike actual8 reaches original11 two options");
        using var sneak=JsonDocument.Parse(File.ReadAllText("Diagnostics/OfficialBlueprints/FeatureDefinitionAdditionalDamage/AdditionalDamageRogueSneakAttack.json"));
        var data=sneak.RootElement;
        var dice=data.GetProperty("diceByRankTable").EnumerateArray().ToDictionary(x=>x.GetProperty("rank").GetInt32(),x=>x.GetProperty("diceNumber").GetInt32());
        check(Enumerable.Range(1,12).All(n=>dice[RogueProgressionRules.EffectiveLevel(true,n)]==
            (RogueProgressionRules.EffectiveLevel(true,n)+1)/2),"ordinary Sneak Attack actual1-12 ranks match source table");
        check(dice[RogueProgressionRules.EffectiveLevel(true,12)]==10 && data.GetProperty("damageDieType").GetString()=="D6",
            "ordinary Rogue12 Sneak Attack reaches10d6");
        check(data.GetProperty("limitedUsage").GetString()=="OncePerTurn" &&
            data.GetProperty("requiredProperty").GetString()=="FinesseOrRangeWeapon" &&
            data.GetProperty("triggerCondition").GetString()=="AdvantageOrNearbyAlly",
            "source eligibility/frequency metadata stays distinct from rank transformation");
        var glbm=types.Single(x=>x.Name=="GLBM");
        check(glbm.Methods.Any(m=>Calls(m,"ScalingLevel","RogueProgressionContext")),
            "damage dispatcher calls class-scoped Rogue rank adapter");
        var fixing=types.Single(x=>x.Name=="ModifyAdditionalDamageRogueSneakAttack");
        var modify=fixing.Methods.Single(x=>x.Name=="ModifyAdditionalDamage");
        check(modify.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Sub) &&
            Calls(modify,"GetAllConditionsOfType") && Calls(modify,"Max"),
            "existing Cunning Strike condition deduction remains compiled (not a combat execution test)");
        var rules=types.Single(x=>x.Name=="Tabletop2024Context");
        check(Calls(rules.Methods.Single(x=>x.Name=="SwitchRogueCunningStrike"),"get_EnableRogueCunningStrike2024") &&
            Calls(rules.Methods.Single(x=>x.Name=="SwitchRogueCunningStrike"),"RemoveAll"),
            "Cunning Strike remains controlled by actual user option with replacement cleanup");
        var cunning=types.Where(x=>x.FullName.Contains("CustomBehaviorCunningStrike")).SelectMany(x=>x.Methods).ToArray();
        check(cunning.Any(m=>Calls(m,"CunningStrikeChoices","RogueProgressionRules")) &&
            cunning.Any(m=>Calls(m,"get_CostPerUse")),"Cunning Strike effective threshold and original power costs coexist");
        var high=new List<FeatureUnlockByLevel>{new(new FeatureDefinition{Name="Elusive18"},18),
            new(new FeatureDefinition{Name="ASI19"},19),new(new FeatureDefinitionPower{Name="StrokeOfLuck20"},20)};
        var highOutput=new CompressedProgressionDefinitions("TestRogue20").Prepare(high,"Rogue");
        check(highOutput.Count==1 && highOutput[0].Level==12 &&
            CompressedProgressionDefinitions.Flatten(highOutput[0].FeatureDefinition).Skip(1).Select(x=>x.Name)
                .SequenceEqual(new[]{"Elusive18","ASI19","StrokeOfLuck20"}),"Rogue18-20 fixture preserves final acquisition order at actual12");
        var shared=types.Single(x=>x.Name=="SharedSpellsContext");
        var sharedCtor=shared.Methods.Single(x=>x.IsConstructor&&x.IsStatic);
        check(Calls(sharedCtor,"get_RoguishShadowCaster"),
            "ShadowCaster remains registered in existing shared magic metadata (full magic integration is NOT tested)");
        CheckRewards(check);
        foreach(var other in new[]{"FighterProgressionContext","BarbarianProgressionContext","MonkProgressionContext"})
            check(types.SelectMany(x=>x.Methods).Any(m=>Calls(m,"IsCapped",other) && Calls(m,"IsCapped","RogueProgressionContext")),
                "Rogue cap composes with "+other);
        check(RogueProgressionRules.EffectiveLevel(true,12)==20 && FighterProgressionPlan<int>.EffectiveLevel(8)==11 && 12+8==20,
            "Rogue12/Fighter8: actual20, independent effective20/11");
        check(RogueProgressionRules.EffectiveLevel(true,12)==20 && MonkProgressionRules.EffectiveLevel(true,8)==11 && 12+8==20,
            "Rogue12/Monk8: actual20, independent effective20/11");
        check(RogueProgressionRules.EffectiveLevel(true,12)==20 && BarbarianProgressionRules.EffectiveLevel(true,8)==11 && 12+8==20,
            "Rogue12/Barbarian8: actual20, independent effective20/11");
        check(RogueProgressionRules.EffectiveLevel(true,8)==11 && FighterProgressionPlan<int>.EffectiveLevel(6)==7 &&
            MonkProgressionRules.EffectiveLevel(true,6)==7 && 8+6+6==20,"three-class8/6/6 keeps actual20 and independent effective11/7/7");
        foreach(var patch in context.NestedTypes)
        {
            var attr=patch.CustomAttributes.FirstOrDefault(x=>x.AttributeType.Name=="HarmonyPatch");
            if(attr==null)continue;
            var t=(TypeReference)attr.ConstructorArguments[0].Value;var n=(string)attr.ConstructorArguments[1].Value;
            var targets=game.MainModule.Types.Single(x=>x.FullName==t.FullName).Methods.Where(x=>x.Name==n||x.Name=="get_"+n).ToArray();
            check(targets.Length>0,"Rogue Harmony target exists: "+patch.Name);
            foreach(var parameter in patch.Methods.Where(x=>x.Name is "Prefix" or "Postfix").SelectMany(x=>x.Parameters).Where(x=>!x.Name.StartsWith("__")))
                check(targets.Any(x=>x.Parameters.Any(p=>p.Name==parameter.Name)),"Rogue Harmony binding: "+patch.Name+"."+parameter.Name);
        }
    }
    private static void CheckRewards(Action<bool,string> check)
    {
        var feat=new FeatureDefinitionPointPool{Name="RogueFeat",PoolType="Feat",PoolAmount=1};
        var asi=new FeatureDefinitionFeatureSet{Name="RogueASI",Mode=FeatureDefinitionFeatureSet.FeatureSetMode.Union};
        asi.FeatureSet.Add(new FeatureDefinitionPointPool{Name="ASI",PoolType="AbilityScore",PoolAmount=2});asi.FeatureSet.Add(feat);
        var source=new List<FeatureUnlockByLevel>();
        foreach(var n in new[]{4,8,10,12,16,19})source.Add(new(asi,n));
        foreach(var n in new[]{2,6,14,18})source.Add(new(feat,n));
        source.Add(new(new FeatureDefinitionPointPool{Name="RogueExtraTwoFeats10",PoolType="Feat",PoolAmount=2},10));
        var expertise=new FeatureDefinitionPointPool{Name="Expertise",PoolType="Expertise",PoolAmount=2};
        source.Add(new(expertise,1));source.Add(new(expertise,6));
        var transformer=new CompressedProgressionDefinitions("TestRogueRewards");
        var original=source.ToArray();
        var latch=new FighterProgressionInitialization();var writes=0;
        List<FeatureUnlockByLevel> output=null;
        for(var i=0;i<3;i++)if(latch.ShouldInitialize(true)){output=transformer.Prepare(source,"Rogue");writes++;}
        var pools=output.SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPointPool>().ToArray();
        check(pools.Where(x=>x.PoolType=="Feat").Sum(x=>x.PoolAmount)==12 && pools.Where(x=>x.PoolType=="AbilityScore").Sum(x=>x.PoolAmount)==12,
            "Rogue six ASIs and twelve class feat points retain special level10 award");
        check(pools.Where(x=>x.PoolType=="Expertise").Sum(x=>x.PoolAmount)==4,"both Expertise acquisitions retain their choices");
        check(writes==1 && source.SequenceEqual(original),"real Rogue transformation is one-shot and preserves input");
        var merged=output.Where(x=>x.Level==8).SelectMany(x=>CompressedProgressionDefinitions.Flatten(x.FeatureDefinition)).OfType<FeatureDefinitionPointPool>().ToArray();
        check(merged.All(transformer.PoolOrigins.ContainsKey) && merged.Select(x=>transformer.PoolOrigins[x]).Distinct().Count()==merged.Length,
            "Rogue10 rewards merged at actual8 retain independent origins");
        var off=new FighterProgressionInitialization();check(!off.ShouldInitialize(false)&&!off.ShouldInitialize(true),"disabled Rogue session remains untransformed until restart");
    }
    private static bool Calls(MethodDefinition m,string name,string owner=null)=>m.HasBody && m.Body.Instructions.Any(x=>x.Operand is MethodReference r&&r.Name==name&&(owner==null||r.DeclaringType.Name==owner));
}
