using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using SolastaUnfinishedBusiness.Models;
using static SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using Secrets = SolastaUnfinishedBusiness.Models.BardMagicalSecretsPlan;
using Caster = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;

// Executes the production source planner and owner-scoped journals. Blueprint/source-switch fixtures
// are NOT a live game database; compiled adapter checks are counted separately below.
internal static class BardMagicalSecretsIntegrationTests
{
    private static readonly int[] Full = {1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,9,9};
    private static readonly int[] LegacyKnown = {4,5,6,7,8,9,10,11,12,14,15,15,16,18,19,19,20,22,22,22};
    private static readonly int[] ModernKnown = {4,5,6,7,9,10,11,12,14,15,16,16,17,17,18,18,19,20,21,22};
    private static readonly string[] Files = Directory.GetFiles("Diagnostics", "*.json", SearchOption.AllDirectories);
    private static readonly Dictionary<string, JsonElement> JsonCache = new();
    private static readonly Dictionary<string, Spell[]> ListCache = new();
    private static string Name(string reference) => reference.Split(':')[1];
    private static JsonElement Read(string name)
    {
        if (JsonCache.TryGetValue(name, out var cached)) return cached;
        var path = Files.Where(p => Path.GetFileNameWithoutExtension(p) == name).OrderBy(p => p.Contains("OfficialBlueprints") ? 0 : 1).First();
        using var json = JsonDocument.Parse(File.ReadAllText(path)); return JsonCache[name] = json.RootElement.Clone();
    }
    private static Spell[] List(string name)
    {
        if (ListCache.TryGetValue(name, out var cached)) return cached;
        var spells=Read(name).GetProperty("spellsByLevel").EnumerateArray()
            .SelectMany(row=>row.GetProperty("spells").EnumerateArray().Select(s=>new Spell(Name(s.GetString()),row.GetProperty("level").GetInt32())));
        // Official blueprint snapshots predate UB ninth-circle registrations. Explicit installed
        // SpellsContext.RegisterSpell fixtures; eligibility varies with the user's enabled spells.
        var ninth=new Dictionary<string,string[]>
        {
            ["SpellListBard"]=new[]{"Foresight","PowerWordHeal","PowerWordKill","PsychicScream"},
            ["SpellListCleric"]=new[]{"MassHeal","PowerWordHeal"},
            ["SpellListDruid"]=new[]{"Foresight","Shapechange"},
            ["SpellListWizard"]=new[]{"Foresight","Invulnerability","MeteorSwarmSingleTarget","PowerWordKill","PsychicScream","TimeStop","Shapechange","Weird"}
        };
        var extra=name=="SpellListAllSpells" ? ninth.Values.SelectMany(x=>x) : ninth.TryGetValue(name,out var entries) ? entries : Array.Empty<string>();
        return ListCache[name]=spells.Concat(extra.Select(id=>new Spell(id,Read(id).GetProperty("spellLevel").GetInt32())))
            .GroupBy(s=>s.Id).Select(g=>g.First()).ToArray();
    }
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static bool Calls(MethodDefinition m,string owner,string name) => m.HasBody && m.Body.Instructions.Any(i => i.Operand is MethodReference r && r.Name == name && r.DeclaringType.Name == owner);
    private static bool Reject(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } catch (ArgumentException) { return true; } }
    private static Secrets.Source Pool(string definition,string owner,int original,string occurrence)
    {
        var pool = Read(definition);
        return new Secrets.Source(owner + ":" + occurrence + ":" + definition, definition, owner, original, Secrets.SourceKind.Selection,
            List(Name(pool.GetProperty("spellListOverride").GetString())), pool.GetProperty("poolAmount").GetInt32(),
            pool.GetProperty("minSpellLevel").GetInt32(), ChoiceMaximum(pool.GetProperty("maxSpellLevel").GetInt32(), false));
    }
    private static IEnumerable<Secrets.Source> SubclassSources(string college)
    {
        IEnumerable<Secrets.Source> Walk(string feature,int original,string path,HashSet<string> ancestors)
        {
            if (!ancestors.Add(feature)) throw new InvalidOperationException("Cyclic fixture graph.");
            var json = Read(feature);
            if (json.TryGetProperty("extraSpellsTag", out var tag) && tag.GetString() == "MagicalSecrets")
                yield return Pool(feature,college,original,path);
            if (!json.TryGetProperty("featureSet",out var children)) yield break;
            var index = 0;
            foreach (var child in children.EnumerateArray())
                foreach (var source in Walk(Name(child.GetString()),original,path + ":" + index++,new HashSet<string>(ancestors))) yield return source;
        }
        return Read(college).GetProperty("featureUnlocks").EnumerateArray().SelectMany((u,i) => Walk(Name(u.GetProperty("featureDefinition").GetString()),
            u.GetProperty("level").GetInt32(),i.ToString(),new HashSet<string>())).ToArray();
    }
    private static Secrets.Source[] Sources(string college,bool revised)
    {
        // Installed SwitchBardBardMagicalSecrets reuses Pool14 at10 AND14, replacing the stock Pool10.
        var baseSources = revised
            ? new[] { "Cleric", "Druid", "Wizard" }.Select(id => new Secrets.Source("Bard:expanded:" + id,
                "MagicAffinityBardMagicalSecrets" + id,"Bard",10,Secrets.SourceKind.ExpandedList,List("SpellList" + id)))
            : new[] { Pool("PointPoolBardMagicalSecrets14","Bard",10,"ten"), Pool("PointPoolBardMagicalSecrets14","Bard",14,"fourteen"),
                Pool("PointPoolBardMagicalSecrets18","Bard",18,"eighteen") }.AsEnumerable();
        return baseSources.Concat(SubclassSources(college)).ToArray();
    }
    private static Profile Bard(string college = "CollegeOfLore",bool revised = false,bool preparedTable = false)
    {
        var metadata = Read("CastSpellBard");
        return new Profile("Bard",metadata.GetProperty("name").GetString(),metadata.GetProperty("spellcastingAbility").GetString(),
            Name(metadata.GetProperty("spellListDefinition").GetString()),Knowledge.Selection,Preparation.None,List("SpellListBard"),Full,
            Enumerable.Range(1,20).Select(n => n < 4 ? 2 : n < 10 ? 3 : 4),preparedTable ? ModernKnown : LegacyKnown,
            Enumerable.Range(1,20).Select(n => n == 1 ? 0 : 1),new int[20],grants:Sources(college,revised).Select(s => Secrets.Grant(s,Full)));
    }
    private static Profile Other(string id)
    {
        var m=Read("CastSpell"+id);
        return new Profile(id,m.GetProperty("name").GetString(),m.GetProperty("spellcastingAbility").GetString(),"SpellList"+id,
            id == "Wizard" ? Knowledge.Spellbook : id == "Cleric" ? Knowledge.WholeList : Knowledge.Selection,
            id == "Sorcerer" ? Preparation.None : Preparation.AbilityPlusLevel,List("SpellList"+id),Full,
            Enumerable.Repeat(3,20),Enumerable.Repeat(4,20),new int[20],Enumerable.Repeat(2,20));
    }
    private static void Choose(Transaction tx,Secrets.Step step,bool replace=false)
    {
        var own=tx.Preview().Repertoires["Bard"];
        var occupied=own.Known.Concat(own.Cantrips).Concat(own.Bonus.Values.SelectMany(s=>s)).ToHashSet();
        var removed=replace && step.Replacements>0 && own.Known.Count>0 ? new[] {own.Known.Last()} : Array.Empty<string>();
        var allowed=step.GrantId==null ? tx.Definition.List.Values.Concat(tx.Definition.Grants.Where(g=>g.Kind==GrantKind.ExpandedList && g.Original<=step.Original).SelectMany(g=>g.Spells))
            .Where(s=>tx.Definition.Allows(s.Id,step.Original)) : tx.Definition.Grants.Single(g=>g.Id==step.GrantId).Spells;
        var added=allowed.Where(s=>!occupied.Contains(s.Id)).Select(s=>s.Id).Distinct().Take(step.Awards+removed.Length).ToArray();
        var cantrips=tx.Definition.List.Values.Where(s=>s.Circle==0 && !occupied.Contains(s.Id)).Take(step.Cantrips).Select(s=>s.Id).ToArray();
        tx.SelectBardStep(step,added,removed,cantrips);
    }
    private static void Complete(Transaction tx,bool replace=false)
    { foreach(var key in tx.BardSteps.Select(s=>s.Key).ToArray()) if(!tx.BardSelections.ContainsKey(key)) Choose(tx,tx.BardSteps.Single(s=>s.Key==key),replace); }
    private static HeroState Through(Profile p,int actual,Repertoire other=null)
    {
        var state=new HeroState(other==null ? Array.Empty<Repertoire>() : new[]{other});
        for(var n=1;n<=actual;n++) {var tx=new Transaction("through:"+n,state,p,n,true);Complete(tx,true);state=tx.Commit();}
        return state;
    }
    internal static void Run(Action<bool,string> check,AssemblyDefinition mod)
    {
        var behavioral=0;var metadata=0;
        void Behavior(bool ok,string label) {behavioral++;check(ok,"Secrets production behavior: "+label);}
        void Binding(bool ok,string label) {metadata++;check(ok,"Secrets compiled/fixture audit: "+label);}
        var types=mod.MainModule.Types.SelectMany(Types).ToArray();
        var official=Read("SubclassChoiceBardColleges").GetProperty("subclasses").EnumerateArray().Select(x=>x.GetString()).ToArray();
        var ub=types.Where(t=>t.BaseType?.Name=="AbstractSubclass" && t.Methods.Any(m=>m.Name=="get_Klass" && m.HasBody &&
            m.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.Name=="get_Bard"))).Select(t=>t.Name).ToArray();
        Binding(official.Length==4 && ub.Length==6,"discover all4 official and6 compiled UB registrations");
        foreach(var college in official.Concat(ub))
        {
            var subclass=SubclassSources(college).ToArray();
            Binding(college=="CollegeOfLore" ? subclass.Length==1 && subclass[0].Original==6 && subclass[0].Amount==2 : subclass.Length==0,
                college+": recursive feature inventory and no inferred tabletop Secrets");
            foreach(var revised in new[]{false,true}) foreach(var table in new[]{false,true})
            {
                var p=Bard(college,revised,table);var state=new HeroState(Array.Empty<Repertoire>());
                for(var actual=1;actual<=12;actual++)
                {
                    var tx=new Transaction(college+":"+revised+":"+table+":"+actual,state,p,actual,true);Complete(tx,true);state=tx.Commit();
                    var r=state.Repertoires["Bard"];var original=FighterProgressionPlan<int>.EffectiveLevel(actual);
                    var grants=p.Grants.Where(g=>Secrets.IsSecret(g) && g.Original<=original).ToArray();
                    Behavior(r.Known.Count==p.Known[original-1] && r.Cantrips.Count==p.Cantrips[original-1],college+"/"+revised+"/"+table+"/"+actual+": ordinary/cantrip count independent");
                    Behavior(r.Bonus.Count==grants.Length && grants.All(g=>r.Bonus[g.Id].Count==g.Choices),college+"/"+revised+"/"+table+"/"+actual+": every original independent award exactly once");
                    Behavior(r.History.All(h=>h.ClassId=="Bard" && h.FeatureId==p.FeatureId) && r.History.Select(h=>h.Original).SequenceEqual(r.History.Select(h=>h.Original).OrderBy(n=>n)),
                        college+"/"+revised+"/"+table+"/"+actual+": own native feature and chronological history");
                }
                Behavior(state.Repertoires.Count==1 && p.Ability=="Charisma" && state.Repertoires["Bard"].Bonus.Values.Sum(s=>s.Count)==(revised?0:6)+(college=="CollegeOfLore"?2:0),
                    college+"/"+revised+"/"+table+": complete original20 rewards without invented repertoires");
            }
        }
        AccessAndOwners(Behavior); Journals(Behavior); RejectionsAndOverlap(Behavior); Origins(Behavior); Bindings(Binding,types);
        Console.WriteLine($"Stage13.5B: {behavioral} executed production behavior checks; {metadata} fixture/compiled-binding checks (no Unity runtime).");
    }
    private static void AccessAndOwners(Action<bool,string> check)
    {
        var legacy=Bard();var secrets=legacy.Grants.Where(Secrets.IsSecret).ToArray();
        check(secrets.Select(g=>g.Original).OrderBy(x=>x).SequenceEqual(new[]{6,10,14,18}),"installed Lore6 + base10/14/18 independent sources");
        foreach(var g in secrets)check(g.Spells.Max(s=>s.Circle)==Full[g.Original-1],"Secrets"+g.Original+": own circle cap, never shared resource9");
        var sources=Sources("CollegeOfLore",false);
        check(sources.Where(s=>s.Owner=="Bard").Take(2).Select(s=>s.Definition).Distinct().Count()==1 && sources.Select(s=>s.Id).Distinct().Count()==4,
            "reused Pool14 at10/14 retains two occurrence IDs");
        var revised=Bard(revised:true);
        check(revised.Grants.Count(Secrets.IsSecret)==1 && revised.Grants.Count(g=>g.Kind==GrantKind.ExpandedList)==3,"2024 replaces only base extra selections; installed Lore6 retained");
        foreach(var id in new[]{"Wizard","Cleric","Druid"})
        {
            var borrowed=List("SpellList"+id).First(s=>s.Circle==5 && !revised.List.ContainsKey(s.Id));
            check(!revised.Allows(borrowed.Id,9) && revised.Allows(borrowed.Id,10),id+" expanded list begins original10/actual8, no shared-level early access");
            var high=List("SpellList"+id).First(s=>s.Circle==9 && !revised.List.ContainsKey(s.Id));
            check(!revised.Allows(high.Id,10) && revised.Allows(high.Id,17),id+" expanded lists retain Bard original circle cap");
        }
        foreach(var revisedRules in new[]{false,true}) foreach(var otherId in new[]{"Sorcerer","Cleric","Wizard"})
        {
            var p=Bard(revised:revisedRules);var foreign=new Repertoire(Other(otherId),8,false,new[]{"ForeignProtected"});
            var initial=Through(p,7,foreign);var tx=new Transaction("borrow:"+otherId+revisedRules,initial,p,8,true);
            var ordinary=tx.BardSteps.First();Choose(tx,ordinary);
            if(!revisedRules)
            {
                var step=tx.BardSteps.Single(s=>s.GrantId!=null);var grant=p.Grants.Single(g=>g.Id==step.GrantId);
                var borrowed=grant.Spells.Where(s=>s.Circle>0 && !p.List.ContainsKey(s.Id) && !tx.Preview().Repertoires["Bard"].Bonus.Values.SelectMany(x=>x).Contains(s.Id)).Take(2).Select(s=>s.Id).ToArray();
                tx.SelectBardStep(step,borrowed);
                check(tx.Preview().Repertoires["Bard"].Bonus[step.GrantId].SequenceEqual(borrowed),otherId+": borrowed spells in Bard ExtraSpellsByTag source, not foreign owner");
            }
            Complete(tx,true);var result=tx.Commit();
            check(ReferenceEquals(result.Repertoires[otherId],foreign) && result.Repertoires.Count==2 && result.Repertoires["Bard"].Definition.Ability=="Charisma",
                otherId+"/"+revisedRules+": other repertoire/attribute/history unchanged");
            var calculator=new Caster("Secrets-owner-v1",new[]{new Caster.ClassProfile("Bard",true,new Caster.CastingProfile(p.FeatureId,Caster.CasterKind.Full,1,true,Full)),
                new Caster.ClassProfile(otherId,true,new Caster.CastingProfile(foreign.Definition.FeatureId,Caster.CasterKind.Full,1,true,Full))});
            var projection=calculator.Calculate(new Caster.Snapshot(16,new[]{new Caster.ClassInput("Bard",8,true),new Caster.ClassInput(otherId,8,false)},
                Enumerable.Repeat("Bard",8).Concat(Enumerable.Repeat(otherId,8))));
            check(projection.IsValid && projection.Classes.Count==2 && projection.ResourceTableIndex==19 && result.Repertoires["Bard"].MaximumCircle==6,
                otherId+"/"+revisedRules+": Secrets add no caster contribution; shared circle9 does not change own class6");
        }
    }
    private static void Journals(Action<bool,string> check)
    {
        foreach(var actual in new[]{6,8,10,12})
        {
            var p=Bard();var foreign=new Repertoire(Other("Wizard"),4,false,new[]{"ForeignProtected"});var before=Through(p,actual-1,foreign);
            var tx=new Transaction("journal:"+actual,before,p,actual,true);var steps=tx.BardSteps.ToArray();
            check(steps.Select(s=>s.Original).SequenceEqual(steps.Select(s=>s.Original).OrderBy(x=>x)) && steps[0].GrantId==null && steps[1].GrantId!=null,
                "actual"+actual+": ordinary then Secrets then next original event");
            check(Reject(()=>Choose(tx,steps[1])),"actual"+actual+": future Secrets rejected before ordinary");
            Choose(tx,steps[0],true);var ordinary=tx.Preview().Repertoires["Bard"].Known.ToArray();
            check(Reject(()=>tx.Commit()),"actual"+actual+": partial ordinary selection cannot skip mandatory Secrets");
            check(Reject(()=>tx.SelectBardStep(steps[1])) && tx.BardSelections.Count==1,"actual"+actual+": Secrets not optional, failed selection leaves ordinary intact");
            var duplicate=ordinary.Last();var extra=p.Grants.Single(g=>g.Id==steps[1].GrantId).Spells.First(s=>!ordinary.Contains(s.Id)).Id;
            check(Reject(()=>tx.SelectBardStep(steps[1],new[]{duplicate,extra})),"actual"+actual+": duplicate ordinary/Secret rejected by production Bard state");
            Choose(tx,steps[1]);Complete(tx,true);var preview=tx.Preview().Repertoires["Bard"];
            check(ReferenceEquals(tx.Preview().Repertoires["Wizard"],foreign) && preview.History.Where(h=>h.Original==steps[0].Original).Last().Source.EndsWith(steps[1].GrantId,StringComparison.Ordinal),
                "actual"+actual+": ordinary history precedes independent Secrets; foreign class untouched");
            var revision=tx.BardRevision;var history=preview.History.ToArray();
            check(Reject(()=>tx.SelectBardStep(tx.BardSteps[1],new[]{duplicate,extra})) && tx.BardRevision==revision &&
                tx.Preview().Repertoires["Bard"].History.Select(h=>(h.SpellId,h.Source,h.Removed)).SequenceEqual(history.Select(h=>(h.SpellId,h.Source,h.Removed))),
                "actual"+actual+": invalid edit atomically restores the entire valid suffix and reward history");
            var stale=tx.BardSteps[1];tx.BackBardStep(stale);
            check(tx.Preview().Repertoires["Bard"].Known.SequenceEqual(ordinary) && tx.BardSelections.Count==1 && Reject(()=>Choose(tx,stale)),
                "actual"+actual+": Back on Secrets retains same-event ordinary, invalidates suffix and old revision");
            Complete(tx,true);var expected=tx.Preview().Repertoires["Bard"].Bonus.Values.Sum(x=>x.Count);
            // Editing a prior completed Secrets step removes dependent choices, never its budget.
            Choose(tx,tx.BardSteps[1]);check(tx.BardSelections.Count==2,"actual"+actual+": edit Secrets invalidates only dependent later events");
            Complete(tx,true);check(tx.Preview().Repertoires["Bard"].Bonus.Values.Sum(x=>x.Count)==expected,"actual"+actual+": edited reward retained exactly once");
            var old=tx.BardSteps[0];tx.Reset();
            check(tx.BardSelections.Count==0 && tx.Preview().Repertoires["Bard"].Known.SequenceEqual(before.Repertoires["Bard"].Known) && Reject(()=>Choose(tx,old)),
                "actual"+actual+": Reset discards only provisional Bard prefix and rejects stale events");
            Complete(tx,true);var committed=tx.Commit();
            check(ReferenceEquals(committed,tx.Commit()) && ReferenceEquals(committed.Repertoires["Wizard"],foreign),"actual"+actual+": repeat managed finalize idempotent");
            var cancelled=new Transaction("cancel:"+actual,before,p,actual,true);Complete(cancelled,true);
            check(ReferenceEquals(cancelled.Cancel(),before) && ReferenceEquals(cancelled.Preview(),before) && Reject(()=>cancelled.Commit()),"actual"+actual+": Cancel returns entire original immutable state");
            var retry=new Transaction("retry:"+actual,before,p,actual,true);Complete(retry,true);
            check(retry.Commit().Repertoires["Bard"].Bonus.Values.Sum(x=>x.Count)==expected,"actual"+actual+": repeated advancement no duplicated rewards");
        }
        {
            var p=Bard();var tx=new Transaction("eighteen-to-twenty",Through(p,11),p,12,true);var steps=tx.BardSteps.ToArray();
            Choose(tx,steps[0]);var learned=tx.BardSelections[steps[0].Key].Added.Last();Choose(tx,steps[1]);
            var next=tx.BardSteps.Single(s=>s.Original==19);var own=tx.Preview().Repertoires["Bard"];
            var replacement=p.List.Values.First(s=>p.Allows(s.Id,19) && !own.Known.Concat(own.Cantrips).Concat(own.Bonus.Values.SelectMany(x=>x)).Contains(s.Id)).Id;
            tx.SelectBardStep(next,new[]{replacement},new[]{learned});
            check(!tx.Preview().Repertoires["Bard"].Known.Contains(learned),"original19 legally replaces ordinary spell learned at original18 in same actual12");
            var last=tx.BardSteps.Single(s=>s.Original==20);tx.SelectBardStep(last);
            check(tx.Commit().Repertoires["Bard"].Bonus.Values.Sum(s=>s.Count)==8,"original20 optional replacement can Skip without losing Secrets18");
            var prior=Through(p,11);var secret=prior.Repertoires["Bard"].Bonus.Values.SelectMany(x=>x).First();
            var invalid=new Transaction("remove-extra",prior,p,12,true);
            var occupied=prior.Repertoires["Bard"];
            var fresh=p.List.Values.Where(s=>p.Allows(s.Id,18) && !occupied.Known.Concat(occupied.Bonus.Values.SelectMany(x=>x)).Contains(s.Id)).Take(3).Select(s=>s.Id).ToArray();
            check(Reject(()=>invalid.SelectBardStep(invalid.BardSteps[0],fresh,new[]{secret})),"Secrets are not ordinary replacement currency even with a valid learned-spell budget");
        }
    }
    private static void RejectionsAndOverlap(Action<bool,string> check)
    {
        var p=Bard();
        var before=Through(p,7);var tx=new Transaction("strict-tokens",before,p,8,true);
        check(Reject(()=>tx.SelectBardStep(null)) && Reject(()=>tx.BackBardStep(null)),"missing step tokens rejected without mutating the journal");
        var legacyEvent=new Transaction("old-event-journal",before,p,8,true);
        var oldOrdinary=legacyEvent.Events[0];var oldOwn=before.Repertoires["Bard"];
        var oldSpells=p.List.Values.Where(s=>p.Allows(s.Id,10) && !oldOwn.Known.Concat(oldOwn.Bonus.Values.SelectMany(x=>x)).Contains(s.Id)).Take(oldOrdinary.Learned).Select(s=>s.Id).ToArray();
        var oldCantrips=p.List.Values.Where(s=>s.Circle==0 && !oldOwn.Cantrips.Concat(oldOwn.Bonus.Values.SelectMany(x=>x)).Contains(s.Id)).Take(oldOrdinary.Cantrips).Select(s=>s.Id).ToArray();
        var oldGrant=p.Grants.Single(g=>Secrets.IsSecret(g) && g.Original==10);
        var oldSecrets=oldGrant.Spells.Where(s=>!oldSpells.Concat(oldCantrips).Concat(oldOwn.Known).Concat(oldOwn.Cantrips).Concat(oldOwn.Bonus.Values.SelectMany(x=>x)).Contains(s.Id)).Take(2).Select(s=>s.Id).ToArray();
        legacyEvent.Select(new Choice(10,oldSpells,cantrips:oldCantrips,bonus:new[]{new KeyValuePair<string,IReadOnlyList<string>>(oldGrant.Id,oldSecrets)}));
        check(Reject(()=>legacyEvent.BackBardStep(legacyEvent.BardSteps[0])) && Reject(()=>Choose(legacyEvent,legacyEvent.BardSteps[0])),
            "old original-event journal cannot silently switch to step journal through selection or Back");
        var ordinary=tx.BardSteps[0];Choose(tx,ordinary);var step=tx.BardSteps[1];
        var source=p.Grants.Single(g=>g.Id==step.GrantId);
        check(Reject(()=>tx.SelectBardStep(new Secrets.Step("foreign",step.Revision,step.Original,step.GrantId,step.Awards,0,0))),"foreign transaction token rejected");
        check(Reject(()=>tx.SelectBardStep(new Secrets.Step(tx.Id,step.Revision,step.Original,step.GrantId,0,0,0))),"fabricated lower reward budget rejected");
        var unowned=source.Spells.First(s=>s.Circle>0 && !p.List.ContainsKey(s.Id) && !tx.Preview().Repertoires["Bard"].Bonus.Values.SelectMany(x=>x).Contains(s.Id)).Id;
        check(Reject(()=>tx.SelectBardStep(step,new[]{unowned,unowned})),"duplicate selections inside one Secrets source rejected");
        var circle9=List("SpellListWizard").First(s=>s.Circle==9).Id;
        check(Reject(()=>tx.SelectBardStep(step,new[]{unowned,circle9})),"shared ninth-circle resources do not expand original10 Secrets access");
        var oldSecret=before.Repertoires["Bard"].Bonus.Values.SelectMany(x=>x).First();
        check(Reject(()=>tx.SelectBardStep(step,new[]{unowned,oldSecret})),"earlier Lore grant cannot be selected again as base Secrets");
        var foreign=new Repertoire(Other("Wizard"),8,false,new[]{unowned});var overlap=before.With(foreign);
        var allowed=new Transaction("cross-owner-overlap",overlap,p,8,true);Choose(allowed,allowed.BardSteps[0]);
        var secretStep=allowed.BardSteps[1];var own=allowed.Preview().Repertoires["Bard"];
        var second=source.Spells.First(s=>s.Circle>0 && s.Id!=unowned && !own.Known.Concat(own.Cantrips).Concat(own.Bonus.Values.SelectMany(x=>x)).Contains(s.Id)).Id;
        allowed.SelectBardStep(secretStep,new[]{unowned,second});Complete(allowed);
        check(allowed.Commit().Repertoires["Bard"].Bonus[secretStep.GrantId].Contains(unowned) && ReferenceEquals(allowed.Commit().Repertoires["Wizard"],foreign),
            "spell independently known by Wizard may be a Bard Secret without stealing its source or INT/CHA ownership");
        var cantripTx=new Transaction("secret-cantrip",before,p,8,true);Choose(cantripTx,cantripTx.BardSteps[0]);
        var cantripStep=cantripTx.BardSteps[1];var occupied=cantripTx.Preview().Repertoires["Bard"];
        var secretCantrip=source.Spells.First(s=>s.Circle==0 && !occupied.Cantrips.Concat(occupied.Bonus.Values.SelectMany(x=>x)).Contains(s.Id)).Id;
        cantripTx.SelectBardStep(cantripStep,new[]{secretCantrip,unowned});Complete(cantripTx);
        check(cantripTx.Commit().Repertoires["Bard"].Bonus[cantripStep.GrantId].Contains(secretCantrip) && !cantripTx.Commit().Repertoires["Bard"].Cantrips.Contains(secretCantrip),
            "CantripOrSpell Secrets retain native extra-tag origin instead of consuming ordinary cantrip budget");
        foreach(var id in new[]{"Sorcerer","Cleric","Wizard"})
        {
            var other=Other(id);var state=Through(p,12,new Repertoire(other,8,false));
            var calculator=new Caster("Secrets20-cap",new[]{new Caster.ClassProfile("Bard",true,new Caster.CastingProfile(p.FeatureId,Caster.CasterKind.Full,1,true,Full)),
                new Caster.ClassProfile(id,true,new Caster.CastingProfile(other.FeatureId,Caster.CasterKind.Full,1,true,Full))});
            var result=calculator.Calculate(new Caster.Snapshot(20,new[]{new Caster.ClassInput("Bard",12,true),new Caster.ClassInput(id,8,false)},
                Enumerable.Repeat("Bard",12).Concat(Enumerable.Repeat(id,8))));
            check(result.IsValid && result.ResourceTableIndex==20 && result.Classes.Count==2 && state.Repertoires["Bard"].MaximumCircle==9 && state.Repertoires[id].MaximumCircle==4,
                "Bard12/"+id+"8: capped single resource index20, independent original20/8 access9/4 and exactly two repertoires");
        }
    }
    private static void Origins(Action<bool,string> check)
    {
        var p=Bard();var state=Through(p,12);var r=state.Repertoires["Bard"];
        var native=new[]{new KeyValuePair<string,IReadOnlyList<string>>("MagicalSecrets",r.Bonus.Values.SelectMany(x=>x).ToArray()),
            new KeyValuePair<string,IReadOnlyList<string>>("UnrelatedBonus",new[]{"Untouched"})};
        var restored=Secrets.PreserveOrigins(p,r,native);
        check(r.Bonus.All(b=>restored[b.Key].SequenceEqual(b.Value)) && !restored.ContainsKey("MagicalSecrets") && restored["UnrelatedBonus"].Single()=="Untouched",
            "native aggregate ExtraSpellsByTag reconciles without discarding occurrence origins or unrelated tags");
        check(Reject(()=>Secrets.PreserveOrigins(p,r,Array.Empty<KeyValuePair<string,IReadOnlyList<string>>>())) &&
            Reject(()=>Secrets.PreserveOrigins(p,null,native)),"missing/unknown native Secrets rejected, no guessed migration or free awards");
        check(Reject(()=>Secrets.PreserveOrigins(Other("Wizard"),r,native)),"foreign repertoire cannot import Bard acquisition history");
        check(Reject(()=>Secrets.PreserveOrigins(p,new Repertoire(Other("Wizard"),8,false),Array.Empty<KeyValuePair<string,IReadOnlyList<string>>>())),
            "even an empty native aggregate cannot import a foreign prior repertoire");
        var both=Sources("CollegeOfLore",false).Concat(Sources("CollegeOfHope",true)).Select(s=>Secrets.Grant(s,Full));
        check(new Profile(p.ClassId,p.FeatureId,p.Ability,p.ListId,p.Mode,p.PreparedRule,p.List.Values,Full,p.Cantrips,p.Known,p.Replacements,p.Scribed,grants:both).Grants.Count==7,
            "planner preserves explicit installed coexistence without inventing it from a rule flag");
        var extra=Secrets.Grant(Pool("PointPoolBardMagicalSecrets14","Bard",10,"another-ten"),Full);
        var twoAtTen=new Profile(p.ClassId,p.FeatureId,p.Ability,p.ListId,p.Mode,p.PreparedRule,p.List.Values,Full,p.Cantrips,p.Known,p.Replacements,p.Scribed,
            grants:p.Grants.Append(extra));
        var simultaneous=new Transaction("two-Secrets10",Through(twoAtTen,7),twoAtTen,8,true);Complete(simultaneous,true);
        var tenSteps=simultaneous.BardSteps.Where(s=>s.Original==10 && s.GrantId!=null).Select(s=>s.GrantId).ToArray();
        var tenHistory=simultaneous.Commit().Repertoires["Bard"].History.Where(h=>h.Original==10 && tenSteps.Any(id=>h.Source.EndsWith(":"+id,StringComparison.Ordinal))).ToArray();
        check(tenHistory.Select(h=>h.Source).SequenceEqual(tenSteps.SelectMany(id=>Enumerable.Repeat(simultaneous.Id+":"+id,2))) &&
            tenSteps.All(id=>simultaneous.Commit().Repertoires["Bard"].Bonus[id].Count==2),
            "two explicit sources on the same original event execute in step order with independent awards and history");
        check(Reject(()=>Secrets.Grant(new Secrets.Source("bad","bad","Bard",21,Secrets.SourceKind.Selection,Array.Empty<Spell>(),2),Full)),"invalid original source fails closed");
    }
    private static void Bindings(Action<bool,string> check,TypeDefinition[] types)
    {
        var context=types.Single(t=>t.Name=="MultipleSpellRepertoiresContext");
        check(Calls(context.Methods.Single(m=>m.Name=="CaptureGrants"),context.Name,"CaptureBardGrant") && Calls(context.Methods.Single(m=>m.Name=="CaptureBardGrant"),"BardMagicalSecretsPlan","Grant"),
            "native final-rule source capture uses the tested production planner");
        check(Calls(context.Methods.Single(m=>m.Name=="SubmitBardStep"),"Transaction","SelectBardStep") && Calls(context.Methods.Single(m=>m.Name=="Synchronize"),"BardMagicalSecretsPlan","PreserveOrigins"),
            "native submission/reconciliation use tested owner-scoped journal and source origins");
        check(Calls(types.Single(t=>t.Name=="BardSpellcastingContext").Methods.Single(m=>m.Name=="FilterAvailableSpells"),context.Name,"FilterBardAvailable"),
            "legacy filter defers only registered gated multicaster Bard tags");
        check(!(bool)types.Single(t=>t.Name=="CompressedMulticasterProgressionPlan").Fields.Single(f=>f.Name=="MixedGameplayReady").Constant,
            "mixed gameplay readiness remains false; no activation switch");
        var finalize=context.Methods.Single(m=>m.Name=="ValidateBeforeFinalize");
        check(finalize.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.Name=="get_Count") && !Calls(finalize,context.Name,"PublishValidatedCommit"),
            "pending native finalization stays blocked awaiting13.7 atomic journal");
    }
}
