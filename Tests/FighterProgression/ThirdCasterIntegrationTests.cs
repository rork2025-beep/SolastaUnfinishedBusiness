using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using S = SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan;
using Third = SolastaUnfinishedBusiness.Models.ThirdCasterProgressionPlan;

// Production source adapter/calculator/repertoire transactions execute on net9. Native DB capture,
// Harmony and inventory are NOT executed. Fixtures/compiled-IL audits are counted separately.
internal static class ThirdCasterIntegrationTests
{
    private static readonly int[] Full={1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
    private static readonly Dictionary<string,JsonElement> Cache=new();
    private static readonly string[] Files=Directory.GetFiles("Diagnostics","*.json",SearchOption.AllDirectories);
    private static Third registry;
    private static C calculator;
    private static int[][] slots;
    private static S.Tables resourceTables;
    private static string Id(string reference)=>reference.Split(':')[1];
    private static JsonElement Read(string name,string kind=null)
    {
        var key=kind+":"+name;
        if(Cache.TryGetValue(key,out var found))return found;
        var path=Files.Where(p=>Path.GetFileNameWithoutExtension(p)==name && (kind==null || Path.GetFileName(Path.GetDirectoryName(p))==kind)).OrderBy(p=>p.Contains("OfficialBlueprints")?0:1).FirstOrDefault() ?? throw new InvalidOperationException("Missing fixture: "+kind+"/"+name);
        using var doc=JsonDocument.Parse(File.ReadAllText(path));return Cache[key]=doc.RootElement.Clone();
    }
    private static int[] Numbers(JsonElement json,string field)=>json.GetProperty(field).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m,string owner,string name)=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.Name==name && r.DeclaringType.Name==owner);
    private static bool Reject(Action action){try{action();return false;}catch(InvalidOperationException){return true;}catch(ArgumentException){return true;}}
    private static R.Spell[] SpellList(string name)=>Read(name).GetProperty("spellsByLevel").EnumerateArray()
        .SelectMany(row=>row.GetProperty("spells").EnumerateArray().Select(s=>new R.Spell(Id(s.GetString()),row.GetProperty("level").GetInt32(),Read(Id(s.GetString()),"SpellDefinition").GetProperty("schoolOfMagic").GetString())))
        .GroupBy(s=>s.Id).Select(g=>g.First()).ToArray();
    private static IEnumerable<(string Feature,int Original)> Features(string owner,bool parent=false)
    {
        IEnumerable<string> Walk(string name)
        {
            yield return name;
            if(Read(name).TryGetProperty("featureSet",out var children))foreach(var child in children.EnumerateArray())foreach(var nested in Walk(Id(child.GetString())))yield return nested;
        }
        return Read(owner).GetProperty("featureUnlocks").EnumerateArray().SelectMany(u=>Walk(Id(u.GetProperty("featureDefinition").GetString())).Select(f=>(f,u.GetProperty("level").GetInt32()))).ToArray();
    }
    private static int[] StaticArray(TypeDefinition builder,string name)
    {
        var code=builder.Methods.Single(m=>m.IsConstructor && m.IsStatic).Body.Instructions.ToArray();
        var end=Array.FindIndex(code,i=>i.OpCode.Code==Code.Stsfld && i.Operand is FieldReference f && f.Name==name);
        var data=code.Take(end).Reverse().First(i=>i.OpCode.Code==Code.Ldtoken && i.Operand is FieldReference).Operand as FieldReference;
        var bytes=data.Resolve().InitialValue;return Enumerable.Range(0,bytes.Length/4).Select(i=>BitConverter.ToInt32(bytes,i*4)).ToArray();
    }
    private static R.Profile Profile(string parent,string subclass,string cast,int unlock,int[] bonus,int[] bonusThird)
    {
        var json=Read(cast);var initial=Numbers(json,"knownSpells")[unlock-1];
        var grants=Features(subclass).Where(f=>Read(f.Feature).GetProperty("$type").GetString().StartsWith("FeatureDefinitionAutoPreparedSpells,",StringComparison.Ordinal))
            .SelectMany(f=>Read(f.Feature).GetProperty("autoPreparedSpellsGroups").EnumerateArray().Select((g,i)=>new R.Grant(subclass+":"+f.Feature+":"+i,
                Math.Max(f.Original,g.GetProperty("classLevel").GetInt32()),R.GrantKind.AutoPrepared,
                g.GetProperty("spellsList").EnumerateArray().Select(s=>new R.Spell(Id(s.GetString()),Read(Id(s.GetString()),"SpellDefinition").GetProperty("spellLevel").GetInt32())),usesFeatureProgression:true))).ToArray();
        return new R.Profile(parent,cast,json.GetProperty("spellcastingAbility").GetString(),Id(json.GetProperty("spellListDefinition").GetString()),
            Enum.Parse<R.Knowledge>(json.GetProperty("spellKnowledge").GetString()),R.Preparation.None,SpellList("SpellListWizard"),
            Enumerable.Range(1,20).Select(n=>n<unlock?0:Array.FindLastIndex(slots[(n+2)/3],x=>x>0)+1),
            Numbers(json,"knownCantrips").Take(20),Enumerable.Range(1,20).Select(n=>n<unlock?0:initial+bonus[(n+2)/3]+bonusThird[n]),
            Enumerable.Range(1,20).Select(n=>n>=4?1:0),new int[20],grants:grants,supportsCastingCompression:false,unlockOriginalLevel:unlock,restrictedSchools:json.GetProperty("restrictedSchools").EnumerateArray().Select(s=>s.GetString()));
    }
    private static C.Snapshot Snapshot(params C.ClassInput[] classes)=>new(classes.Sum(c=>c.ActualLevel),classes,classes.SelectMany(c=>Enumerable.Repeat(c.ClassId,c.ActualLevel)));
    private static C.ClassInput Input(Third.Source source,int actual,bool compressed=false)=>new(source.ClassId,actual,compressed,source.SubclassId);
    private static void Complete(R.Transaction tx,bool replace=false)
    {
        foreach(var e in tx.Events)
        {
            var own=tx.Preview().Repertoires[tx.ClassId];var used=own.Known.Concat(own.Cantrips).Concat(own.AutoPrepared).ToHashSet();
            var removed=replace && e.Replacements>0 && own.Known.Count>0?new[]{own.Known.Last()}:Array.Empty<string>();
            var added=tx.Definition.List.Values.Where(s=>tx.Definition.Allows(s.Id,e.Original) && !used.Contains(s.Id)).Take(e.Learned+removed.Length).Select(s=>s.Id).ToArray();
            var cantrips=tx.Definition.List.Values.Where(s=>tx.Definition.Allows(s.Id,e.Original,true) && !used.Contains(s.Id)).Take(e.Cantrips).Select(s=>s.Id).ToArray();
            tx.Select(new R.Choice(e.Original,added,removed,cantrips));
        }
    }
    private static R.HeroState Through(Third.Source source,int actual,bool compressed=false,R.Repertoire other=null)
    {
        var state=new R.HeroState(other==null?Array.Empty<R.Repertoire>():new[]{other});
        for(var n=source.Repertoire.UnlockOriginalLevel;n<=actual;n++)
        {var tx=new R.Transaction(source.SubclassId+":"+n,state,source.ForAdvancement(n,compressed),n,compressed);Complete(tx,true);state=tx.Commit();}
        return state;
    }
    private static R.Profile Other(string name)
    {
        var json=Read("CastSpell"+name);
        return new R.Profile(name,json.GetProperty("name").GetString(),json.GetProperty("spellcastingAbility").GetString(),"SpellList"+name,R.Knowledge.Selection,R.Preparation.None,
            SpellList("SpellList"+name),Full,Enumerable.Repeat(3,20),Enumerable.Repeat(4,20),new int[20],new int[20]);
    }
    internal static void Run(Action<bool,string> check,AssemblyDefinition mod)
    {
        var behavior=0;var audits=0;
        void Behavior(bool ok,string label){behavior++;check(ok,"Third-caster production behavior: "+label);}
        void Audit(bool ok,string label){audits++;check(ok,"Third-caster fixture/compiled audit: "+label);}
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();var builder=types.Single(t=>t.Name=="FeatureDefinitionCastSpellBuilder");
        var flat=CompressedMulticasterTests.AddedIntegers(builder,"SlotsByCasterLevel");
        slots=Enumerable.Range(0,21).Select(n=>flat.Skip(n*10).Take(9).ToArray()).ToArray();
        resourceTables=new S.Tables(slots.Skip(1),CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="ModifyPowerPoolAmountPowerSpellPoints"),"SpellPointsByLevel"),
            CompressedMulticasterTests.AddedIntegers(types.Single(t=>t.Name=="SpellPointsContext"),"SpellCostByLevel"));
        var bonus=StaticArray(builder,"BonusSpellsKnownByCasterLevel");var thirdBonus=StaticArray(builder,"BonusSpellsKnownThirdCaster");
        Audit(bonus.Length==21 && thirdBonus.Length==21 && slots.Length==21,"installed builder bonus arrays and slot rows loaded from compiled data");
        var sources=new List<Third.Source>();var parents=new List<C.ClassProfile>();
        foreach(var parent in new[]{"Fighter","Rogue"})
        {
            var choice=Features(parent,true).Single(f=>Read(f.Feature).GetProperty("$type").GetString().StartsWith("FeatureDefinitionSubclassChoice,",StringComparison.Ordinal));
            var official=Read(choice.Feature).GetProperty("subclasses").EnumerateArray().Select(s=>s.GetString());
            var ub=types.Where(t=>t.BaseType?.Name=="AbstractSubclass" && t.Methods.Any(m=>m.Name=="get_Klass" && m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.Name=="get_"+parent))).Select(t=>t.Name);
            var subclasses=official.Concat(ub).Distinct().ToArray();
            parents.Add(new C.ClassProfile(parent,true,null,choice.Original,subclasses.Select(id=>new C.SubclassProfile(id,parent=="Rogue" || id is "MartialChampion" or "MartialWeaponMaster",parent=="Rogue" || id is "MartialChampion" or "MartialWeaponMaster"))));
            foreach(var subclass in subclasses)
            {
                if(!Files.Any(p=>Path.GetFileNameWithoutExtension(p)==subclass && Path.GetFileName(Path.GetDirectoryName(p))=="CharacterSubclassDefinition"))
                {
                    var compiled=types.Single(t=>t.Name==subclass);
                    Audit(subclass=="RoguishSwashbuckler" && !compiled.Methods.Where(m=>m.IsConstructor && m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference r &&
                        (r.DeclaringType.Name=="FeatureDefinitionCastSpellBuilder" || r.Name.StartsWith("get_CastSpell",StringComparison.Ordinal))),
                        subclass+": no blueprint snapshot; compiled constructor has no casting-feature builder/reference (not game execution)");
                    continue;
                }
                var casts=Features(subclass).Where(f=>Read(f.Feature).GetProperty("$type").GetString().StartsWith("FeatureDefinitionCastSpell,",StringComparison.Ordinal)).ToArray();
                foreach(var auxiliary in casts.Where(c=>Read(c.Feature).GetProperty("spellCastingOrigin").GetString()!="Subclass"))
                    Audit(Read(auxiliary.Feature).GetProperty("spellCastingOrigin").GetString()=="Race",subclass+": discovered innate cantrip casting is auxiliary, not a registered OneThird class contribution");
                foreach(var cast in casts.Where(c=>Read(c.Feature).GetProperty("spellCastingOrigin").GetString()=="Subclass"))
                {
                    var p=Profile(parent,subclass,cast.Feature,cast.Original,bonus,thirdBonus);
                    sources.Add(new Third.Source(parent,subclass,choice.Original,parent=="Rogue",p));
                    Audit(casts.Length==1 && p.Ability=="Intelligence" && Read(cast.Feature).GetProperty("spellCastingOrigin").GetString()=="Subclass" && Read(cast.Feature).GetProperty("spellReadyness").GetString()=="AllKnown",
                        parent+"/"+subclass+": actual registered feature, INT/Selection/AllKnown; no table-top inferred grants");
                }
            }
        }
        registry=new Third(sources);
        Audit(sources.Select(s=>s.SubclassId).OrderBy(x=>x).SequenceEqual(new[]{"MartialSpellblade","MartialSpellShield","RoguishArcaneScoundrel","RoguishShadowCaster"}.OrderBy(x=>x)),
            "recursive actual Fighter/Rogue registrations discover exactly four caster subclasses");
        var profiles=parents.Select(registry.AdaptParent).Concat(new[]{"Wizard","Cleric","Bard","Sorcerer"}.Select(id=>new C.ClassProfile(id,true,new C.CastingProfile("CastSpell"+id,C.CasterKind.Full,1,true,Full))))
            .Append(new C.ClassProfile("Barbarian",true)).ToArray();
        calculator=new C("Third13.5C-installed-fixture-v1",profiles);
        Careers(Behavior); Combinations(Behavior); Rounding(Behavior); Transactions(Behavior); Rejections(Behavior); Schools(Behavior); Bindings(Audit,types);
        Console.WriteLine($"Stage13.5C: {behavior} executed production behavior checks; {audits} fixture/compiled-binding checks (no Unity runtime).");
    }
    private static void Careers(Action<bool,string> check)
    {
        foreach(var source in registry.Sources.Values)
        foreach(var compressed in source.AvailableWithCompressedClass?new[]{false,true}:new[]{false})
        {
            var state=new R.HeroState(Array.Empty<R.Repertoire>());var p=source.Repertoire;
            for(var n=p.UnlockOriginalLevel;n<=(compressed?12:20);n++)
            {
                var tx=new R.Transaction("career:"+source.SubclassId+compressed+n,state,source.ForAdvancement(n,compressed),n,compressed);
                Complete(tx,true);state=tx.Commit();var own=state.Repertoires[source.ClassId];var result=calculator.Calculate(Snapshot(Input(source,n,compressed)));var c=result.Classes.Single();
                check(result.IsValid && c.ActualClassLevel==n && c.EffectiveClassLevel==(compressed?FighterProgressionPlan<int>.EffectiveLevel(n):n) && c.EffectiveSubclassLevel==c.EffectiveClassLevel,
                    source.SubclassId+"/"+compressed+"/"+n+": actual parent and independent feature levels");
                check(c.EffectiveCastingLevel==n && c.SpellAccessLevel==n && own.EffectiveLevel==n && own.EffectiveFeatureLevel==c.EffectiveClassLevel && c.MaximumSpellCircle==p.MaximumCircles[n-1],
                    source.SubclassId+"/"+compressed+"/"+n+": actual subclass casting/access preserved despite feature mapping");
                check(own.Known.Count==p.Known[n-1] && own.Cantrips.Count==p.Cantrips[n-1] && tx.Events.Count==1 && tx.Events.Single().Original==n,
                    source.SubclassId+"/"+compressed+"/"+n+": exact source known/cantrip counts and ONE actual learning/replacement event");
                check(own.History.All(h=>h.ClassId==source.ClassId && h.FeatureId==p.FeatureId && h.Original<=n) && result.ResourceTableIndex==(n+2)/3 && result.CastingParticipantCount==1,
                    source.SubclassId+"/"+compressed+"/"+n+": native casting-feature ownership, solo ceil, no second parent contribution");
            }
            var final=state.Repertoires[source.ClassId];
            check(final.MaximumCircle==(compressed?2:4) && final.Known.Count==(compressed?(source.SubclassId=="RoguishShadowCaster"?8:9):source.ClassId=="Fighter" && source.SubclassId=="MartialSpellShield" || source.SubclassId=="RoguishArcaneScoundrel"?14:13),
                source.SubclassId+"/"+compressed+": exact installed final spell access/known total, never full casting");
        }
        var arcane=registry.Sources["RoguishArcaneScoundrel"];
        foreach(var compressed in new[]{false,true})
        foreach(var n in new[]{8,9,12})
        {
            var own=Through(arcane,n,compressed).Repertoires["Rogue"];
            check(own.AutoPrepared.Contains("Counterspell")== (compressed && n>=9) && own.MaximumCircle==2,
                "ArcaneScoundrel/"+compressed+"/"+n+": original13 Counterspell is a subclass FEATURE grant; no casting-level or ordinary circle3 inflation");
        }
    }
    private static void Combinations(Action<bool,string> check)
    {
        var pairs=new[]{("MartialSpellblade","Wizard"),("MartialSpellShield","Cleric"),("RoguishArcaneScoundrel","Bard"),("RoguishShadowCaster","Sorcerer")};
        foreach(var pair in pairs)
        foreach(var compressedOther in new[]{false,true})
        foreach(var compressedParent in registry.Sources[pair.Item1].AvailableWithCompressedClass?new[]{false,true}:new[]{false})
        {
            var source=registry.Sources[pair.Item1];var other=new R.Repertoire(Other(pair.Item2),12,compressedOther);
            var snapshot=Snapshot(Input(source,8,compressedParent),new C.ClassInput(pair.Item2,12,compressedOther));var result=calculator.Calculate(snapshot);
            var own=result.Classes.Single(c=>c.ClassId==source.ClassId);var final=Through(source,8,compressedParent,other);
            var expected=(compressedOther?20:12)+2;
            check(result.IsValid && result.RawSharedCasterLevel==expected && result.ResourceTableIndex==Math.Min(20,expected) && result.Classes.Count==2 && own.ContributionNumerator==8 && own.ContributionDenominator==3,
                pair+"/"+compressedParent+"/"+compressedOther+": full + floor(actual third/3), capped20, exactly two class contributors");
            check(own.MaximumSpellCircle==2 && final.Repertoires[source.ClassId].MaximumCircle==2 && !source.Repertoire.Allows("Fireball",8) && ReferenceEquals(final.Repertoires[pair.Item2],other),
                pair+"/"+compressedParent+"/"+compressedOther+": high shared circle never expands third learning or alters another repertoire");
            check(final.Repertoires[source.ClassId].Definition.Ability=="Intelligence" && other.Definition.Ability==Other(pair.Item2).Ability && snapshot.ActualClassHistory.SequenceEqual(result.Input.ActualClassHistory),
                pair+": independent installed casting ability, spell lists and real history");
            foreach(var mode in new[]{S.ResourceMode.Slots,S.ResourceMode.SpellPoints})
            {
                var allocation=new S.Allocation(resourceTables,result,mode);var budget=new S.Budget(resourceTables,allocation);
                var success=budget.Spend("native-third-effect",1) && budget.Spend("native-full-effect",2);
                check(success && (mode==S.ResourceMode.SpellPoints?budget.RemainingPoints==allocation.PointMaximum-5:budget.RemainingSlots(1)==allocation.Slots[0]-1 && budget.RemainingSlots(2)==allocation.Slots[1]-1),
                    pair+"/"+mode+": different source features debit one authoritative shared budget");
            }
        }
        var shadow=registry.Sources["RoguishShadowCaster"];
        var three=calculator.Calculate(Snapshot(Input(shadow,8,true),new C.ClassInput("Wizard",6,true),new C.ClassInput("Barbarian",6,true)));
        check(three.IsValid && three.RawSharedCasterLevel==9 && three.Classes.Single(c=>c.ClassId=="Barbarian").ContributionNumerator==0 && three.Input.ActualCharacterLevel==20,
            "Rogue8C/Wizard6C/Barbarian6C: actual20, feature11/casting8, full7+third2, no martial resource contribution");
    }
    private static void Rounding(Action<bool,string> check)
    {
        var fighter=registry.Sources["MartialSpellblade"];var rogue=registry.Sources["RoguishShadowCaster"];
        for(var a=3;a<=17;a++)for(var b=3;b<=20-a;b++)
        {
            var result=calculator.Calculate(Snapshot(Input(fighter,a),Input(rogue,b)));
            check(result.IsValid && result.ResourceTableIndex==(a+b)/3 && result.Buckets.Single(x=>x.Kind==C.CasterKind.OneThird).LevelSum==a+b && result.CastingParticipantCount==2,
                "two registered third casters "+a+"+"+b+": grouped floor(sum/3), not sum of per-class floors");
        }
        foreach(var source in registry.Sources.Values)
        for(var n=3;n<=19;n++)
        {
            var solo=calculator.Calculate(Snapshot(Input(source,n)));var martial=calculator.Calculate(Snapshot(Input(source,n),new C.ClassInput("Barbarian",1)));
            var mixed=calculator.Calculate(Snapshot(Input(source,n),new C.ClassInput("Wizard",1)));
            check(solo.ResourceTableIndex==(n+2)/3 && martial.ResourceTableIndex==solo.ResourceTableIndex && mixed.ResourceTableIndex==1+n/3 && mixed.Mode==C.RoundingMode.UbGroupedMulticlass,
                source.SubclassId+"/"+n+": solo ceil, martial does not trigger casting rounding, real second caster uses grouped floor");
        }
    }
    private static void Transactions(Action<bool,string> check)
    {
        foreach(var source in registry.Sources.Values)
        foreach(var compressed in source.AvailableWithCompressedClass?new[]{false,true}:new[]{false})
        {
            var other=new R.Repertoire(Other("Wizard"),8,false,new[]{"ForeignOwned"});var before=Through(source,7,compressed,other);
            var tx=new R.Transaction("transaction:"+source.SubclassId+compressed,before,source.ForAdvancement(8,compressed),8,compressed);
            Complete(tx,true);var expected=tx.Preview().Repertoires[source.ClassId].Known.ToArray();
            tx.Back(8);check(tx.Preview().Repertoires[source.ClassId].Known.SequenceEqual(before.Repertoires[source.ClassId].Known) && ReferenceEquals(tx.Preview().Repertoires["Wizard"],other),source.SubclassId+": Back restores only the casting subclass choices");
            Complete(tx,true);tx.Reset();check(tx.Preview().Repertoires[source.ClassId].Known.SequenceEqual(before.Repertoires[source.ClassId].Known),source.SubclassId+": Reset does not change feature/casting policy or spell origins");
            Complete(tx,true);var final=tx.Commit();
            check(final.Repertoires[source.ClassId].Known.SequenceEqual(expected) && ReferenceEquals(final,tx.Commit()) && ReferenceEquals(final.Repertoires["Wizard"],other),source.SubclassId+": repeated finalize grants once, preserves the other class");
            var cancel=new R.Transaction("cancel",before,source.ForAdvancement(8,compressed),8,compressed);Complete(cancel);
            check(ReferenceEquals(cancel.Cancel(),before) && Reject(()=>cancel.Commit()),source.SubclassId+": Cancel cannot leak spell choices/resources");
        }
    }
    private static void Rejections(Action<bool,string> check)
    {
        foreach(var source in registry.Sources.Values)
        {
            var early=calculator.Calculate(Snapshot(new C.ClassInput(source.ClassId,2)));
            var premature=calculator.Calculate(Snapshot(Input(source,2)));var missing=calculator.Calculate(Snapshot(new C.ClassInput(source.ClassId,3)));
            check(early.IsValid && early.CastingParticipantCount==0 && early.ResourceTableIndex==0 && !premature.IsValid && premature.Diagnostics.Any(d=>d.Code==C.DiagnosticCode.PrematureSubclass) && missing.Diagnostics.Any(d=>d.Code==C.DiagnosticCode.MissingSubclass),
                source.SubclassId+": no casting before actual3 or without a real subclass choice");
            check(Reject(()=>source.ForAdvancement(2,false)) && Reject(()=>new R.Transaction("early",new R.HeroState(Array.Empty<R.Repertoire>()),source.Repertoire,2,false)) &&
                Reject(()=>new R.Transaction("late-import",new R.HeroState(Array.Empty<R.Repertoire>()),source.Repertoire,8,false)),source.SubclassId+": first casting source starts at real unlock, not early/late save import");
            var initial=new R.Transaction("unlock",new R.HeroState(Array.Empty<R.Repertoire>()),source.ForAdvancement(3,false),3,false);Complete(initial);
            check(initial.Events.Single().Original==3 && initial.Commit().Repertoires[source.ClassId].Known.Count==source.Repertoire.Known[2],source.SubclassId+": native-style first subclass repertoire receives the exact initial award");
            check(source.Matches(null,source.SubclassId,source.Repertoire.FeatureId) && source.Matches(source.ClassId,source.SubclassId,source.Repertoire.FeatureId) &&
                !source.Matches("Wizard",source.SubclassId,source.Repertoire.FeatureId) && !source.Matches(null,null,source.Repertoire.FeatureId) && !source.Matches(null,source.SubclassId,"CastSpellWizard"),
                source.SubclassId+": null native class resolved from exact registered subclass/feature; ambiguous ownership rejected");
            if(source.ClassId=="Fighter")
            {
                var unsupported=calculator.Calculate(Snapshot(Input(source,8,true)));
                check(!unsupported.IsValid && unsupported.Diagnostics.Any(d=>d.Code==C.DiagnosticCode.UnsupportedCompressedSubclass) && Reject(()=>source.ForAdvancement(8,true)),
                    source.SubclassId+": compressed Fighter caster remains explicitly unsupported in calculator AND source adapter");
            }
        }
        var shadow=registry.Sources["RoguishShadowCaster"];var arcane=registry.Sources["RoguishArcaneScoundrel"];
        var original=Through(shadow,7,true);
        check(Reject(()=>new R.Transaction("subclass-switch",original,arcane.ForAdvancement(8,true),8,true)),"changing registered casting feature cannot reuse another subclass's spell history");
        check(!calculator.Calculate(Snapshot(Input(shadow,13,true))).IsValid && Reject(()=>shadow.ForAdvancement(13,true)),"Rogue12 class cap remains intact despite ordinary casting tables through20");
        check(!calculator.Calculate(Snapshot(Input(shadow,4),Input(shadow,4))).IsValid && Reject(()=>new Third(new[]{shadow,shadow})),"duplicate parent contribution and duplicate registered source rejected");
        check(Reject(()=>registry.AdaptParent(new C.ClassProfile("Rogue",true,new C.CastingProfile("FakeFull",C.CasterKind.Full,1,true,Full),3,new[]{new C.SubclassProfile(shadow.SubclassId,true),new C.SubclassProfile(arcane.SubclassId,true)}))),
            "class+subclass casting is not duplicated as a second contributor");
        var p=shadow.Repertoire;
        var unsafeCastingMode=new R.Profile(p.ClassId,p.FeatureId,p.Ability,p.ListId,p.Mode,p.PreparedRule,p.List.Values,p.MaximumCircles,p.Cantrips,p.Known,p.Replacements,p.Scribed,
            grants:p.Grants,supportsCastingCompression:true,unlockOriginalLevel:p.UnlockOriginalLevel,restrictedSchools:p.RestrictedSchools);
        check(Reject(()=>new R.Transaction("implicit-casting-mode-switch",original,unsafeCastingMode,8,true)),"same native feature ID cannot silently change actual Rogue casting into compressed casting");
        check(!C.MixedGameplayReady,"mixed gameplay remains disabled; no new activation switch");
    }
    private static void Schools(Action<bool,string> check)
    {
        foreach(var source in registry.Sources.Values)
        {
            var p=source.Repertoire;
            var unrestricted=new R.Profile(p.ClassId,p.FeatureId,p.Ability,p.ListId,p.Mode,p.PreparedRule,p.List.Values,p.MaximumCircles,p.Cantrips,p.Known,p.Replacements,p.Scribed,
                grants:p.Grants,supportsCastingCompression:false,unlockOriginalLevel:p.UnlockOriginalLevel);
            foreach(var spell in p.List.Values.Where(s=>s.Circle<=1))
                check(p.Allows(spell.Id,3,spell.Circle==0)==(p.RestrictedSchools.Count==0 || p.RestrictedSchools.Contains(spell.School)) && unrestricted.Allows(spell.Id,3,spell.Circle==0),
                    source.SubclassId+"/"+spell.Id+": installed school restriction and explicit removal variant preserve circle/cantrip rules");
            var high=p.List.Values.First(s=>s.Circle==3 && (p.RestrictedSchools.Count==0 || p.RestrictedSchools.Contains(s.School)));
            check(!p.Allows(high.Id,12) && p.Allows(high.Id,13),source.SubclassId+": allowed SCHOOL never bypasses its own third-caster circle boundary");
        }
    }
    private static void Bindings(Action<bool,string> check,TypeDefinition[] types)
    {
        var context=types.Single(t=>t.Name=="MultipleSpellRepertoiresContext");
        check(Calls(context.Methods.Single(m=>m.Name=="CaptureCore"),context.Name,"CaptureThirdCasterDefinitions") && Calls(context.Methods.Single(m=>m.Name=="CaptureThirdCasterDefinitions"),"ThirdCasterProgressionPlan","AdaptParent"),
            "boot source registry consumes the tested reusable parent adapter before compression");
        check(Calls(context.Methods.Single(m=>m.Name=="MatchesDefinition"),"Source","Matches") && Calls(context.Methods.Single(m=>m.Name=="BeginAdvancement"),"Source","ForAdvancement"),
            "native ownership/advancement use tested registered source policies");
        check(Calls(types.Single(t=>t.Name=="PactMagicMulticasterContext").Methods.Single(m=>m.Name=="Spend"),context.Name,"RepertoireClassId"),
            "existing Pact dispatcher resolves native subclass owner without changing resource selection/costs");
        var shared=types.Single(t=>t.Name=="SharedSpellsContext");var code=shared.Methods.Single(m=>m.IsConstructor && m.IsStatic).Body.Instructions.ToArray();
        var end=Array.FindIndex(code,i=>i.OpCode.Code==Code.Stsfld && i.Operand is FieldReference f && f.Name=="SubclassCasterType");
        var start=Array.FindLastIndex(code,end-1,i=>i.OpCode.Code==Code.Stsfld);
        check(code.Skip(start+1).Take(end-start-1).Count(i=>i.Operand is MethodReference r && r.Name=="Add")==4,
            "actual installed SubclassCasterType has exactly four entries; no undiscovered fifth third caster");
        var level20=types.Single(t=>t.Name=="Level20Context");
        check(Calls(level20.Methods.Single(m=>m.Name=="MartialSpellBladeLoad"),"FeatureDefinitionCastSpellBuilder","EnumerateKnownSpells") && Calls(level20.Methods.Single(m=>m.Name=="RoguishShadowcasterLoad"),"FeatureDefinitionCastSpellBuilder","EnumerateSlotsPerLevel"),
            "official snapshot late rows are replaced by installed UB builder through20");
        check(context.Methods.Single(m=>m.Name=="get_RuntimeEnabled").Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name=="StandardMulticasterResourcesContext") &&
            !Calls(context.Methods.Single(m=>m.Name=="ValidateBeforeFinalize"),context.Name,"PublishValidatedCommit"),"closed resource gate and pending native finalization journal boundary preserved");
    }
}
