using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Behaviors;
using SolastaUnfinishedBusiness.Feats;
using SolastaUnfinishedBusiness.Validators;
using P = SolastaUnfinishedBusiness.Models.AuxiliarySpellSourcesPlan;
using R = SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresPlan;
using G = SolastaUnfinishedBusiness.Models.AuxiliarySpellSourceGraphPlan;
using C = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;

namespace SolastaUnfinishedBusiness.Models;

// Registered roots, not hero names/spell-list guesses. Native acquisition is blocked;
// only restoration of an existing independent native repertoire writes its slot debt.
internal static class AuxiliarySpellSourcesContext
{
    internal static bool RuntimeEnabled => StandardMulticasterResourcesContext.RuntimeEnabled;
    private sealed class Entry(P.Source source, BaseDefinition root, FeatureDefinitionCastSpell cast,
        FeatureDefinition feature, FeatureDefinitionPointPool[] pools, InvocationDefinition invocation = null)
    {
        internal readonly P.Source Source = source;
        internal readonly BaseDefinition Root = root;
        internal readonly FeatureDefinitionCastSpell Cast = cast;
        internal readonly FeatureDefinition Feature = feature;
        internal readonly FeatureDefinitionPointPool[] Pools = pools;
        internal readonly InvocationDefinition Invocation = invocation;
    }
    private static P registry;
    private static Entry[] entries;
    internal static IReadOnlyList<P.Source> Coverage => registry == null ? Array.Empty<P.Source>() : registry.Sources.Values.ToArray();
    private static FeatureDefinition[] Flatten(IEnumerable<FeatureDefinition> features)
    {
        IEnumerable<FeatureDefinition> Visit(FeatureDefinition feature,HashSet<FeatureDefinition> path)
        {
            if(!feature || !path.Add(feature))throw new P.Rejection(P.Code.UnsupportedSource,"null/cyclic auxiliary feature graph");
            yield return feature;
            if(feature is FeatureDefinitionFeatureSet set)
                foreach(var child in set.FeatureSet.SelectMany(f=>Visit(f,new HashSet<FeatureDefinition>(path))))yield return child;
        }
        return features.SelectMany(f=>Visit(f,new())).Distinct().ToArray();
    }
    private static IReadOnlyList<G.Occurrence> Graph(BaseDefinition root,IEnumerable<FeatureDefinition> features)
    {
        G.Node Node(FeatureDefinition feature,HashSet<FeatureDefinition> stack)
        {
            if(!feature || !stack.Add(feature))throw new P.Rejection(P.Code.UnsupportedSource,root.Name+"/null or cyclic native graph");
            if(feature is not FeatureDefinitionFeatureSet set)return new G.Node(feature.Name);
            var mode=set.Mode switch
            {
                FeatureDefinitionFeatureSet.FeatureSetMode.Union=>G.Mode.Union,
                FeatureDefinitionFeatureSet.FeatureSetMode.Exclusion=>G.Mode.Exclusion,
                _=>G.Mode.Unsupported
            };
            return new G.Node(feature.Name,mode,set.FeatureSet.Select(f=>Node(f,new HashSet<FeatureDefinition>(stack))));
        }
        return G.Walk(root.Name,new G.Node("root",G.Mode.Union,features.Select(f=>Node(f,new()))));
    }
    private static R.Spell[] Spells(SpellListDefinition list) => list == null ? Array.Empty<R.Spell>() : list.SpellsByLevel.SelectMany(r=>r.Spells).Distinct().Select(s=>new R.Spell(s.Name,s.SpellLevel,s.SchoolOfMagic)).ToArray();
    private static int[] Rows(IReadOnlyList<int> rows) => Enumerable.Range(0,20).Select(i=>rows.Count==0?0:rows[Math.Min(i,rows.Count-1)]).ToArray();
    private static bool SpellPool(FeatureDefinitionPointPool pool) => pool.PoolType is HeroDefinitions.PointsPoolType.Cantrip or HeroDefinitions.PointsPoolType.Spell or HeroDefinitions.PointsPoolType.CantripOrSpell;
    internal static void CaptureInstalledDefinitions()
    {
        if(registry!=null)return;
        try
        {
            EldritchInvocationSourcesContext.CaptureInstalledDefinitions();
            var output=new List<Entry>();
            void Invocation(BaseDefinition root,P.Origin kind,InvocationDefinition inv,FeatureDefinitionCastSpell cast,string owner,P.Variant variant=null)
            {
                if(!inv.GrantedSpell)return;
                var recharge=inv.HasSubFeatureOfType<RechargeInvocationOnShortRest>()?"ShortRest":inv.LongRestRecharge?"LongRest":"AtWill";
                var spec=new P.Source(kind+":"+root.Name+":invocation:"+inv.Name+(variant==null?"":":"+variant.Id),kind,root.Name,inv.Name,"Invocation",cast.spellcastingAbility,owner,
                    inv.ConsumesSpellSlot?(EldritchInvocationSourcesContext.IsRegisteredPaid(inv)?P.Domain.PaidInvocation:P.Domain.Unsupported):P.Domain.NativeInvocation,owner==null?0:Math.Max(1,inv.RequiredLevel),recharge,"FixedList",
                    new[]{new R.Spell(inv.GrantedSpell.Name,inv.GrantedSpell.SpellLevel)},atWillInvocation:recharge=="AtWill",prerequisites:new[]{inv.RequiredPact?.Name,inv.RequiredKnownSpell?.Name}.Where(n=>n!=null),castingFeature:cast.Name,variant:variant);
                output.Add(new Entry(spec,root,cast,null,Array.Empty<FeatureDefinitionPointPool>(),inv));
            }
            void Root(BaseDefinition root,P.Origin kind,IEnumerable<FeatureDefinition> original,int unlock=1,string ownerClass=null)
            {
                var originals=original.ToArray();
                var features=Flatten(originals);var byName=features.ToDictionary(f=>f.Name,StringComparer.Ordinal);
                var graph=Graph(root,originals);
                var casting=graph.Where(o=>byName.TryGetValue(o.Feature,out var f) && f is FeatureDefinitionCastSpell).ToArray();
                var nativePools=graph.Where(o=>byName.TryGetValue(o.Feature,out var f) && f is FeatureDefinitionPointPool pool && SpellPool(pool)).ToArray();
                var bindings=(casting.Length==0?Array.Empty<G.Occurrence>():nativePools).Select(o=>(Pool:o,Cast:G.BindPool(o,casting,
                    name=>((FeatureDefinitionCastSpell)byName[name]).GetFirstSubFeatureOfType<FeatHelpers.SpellTag>()?.Name,
                    ((FeatureDefinitionPointPool)byName[o.Feature]).ExtraSpellsTag))).ToArray();
                foreach(var occurrence in casting)
                {
                    var cast=(FeatureDefinitionCastSpell)byName[occurrence.Feature];
                    var bound=bindings.Where(b=>ReferenceEquals(b.Cast,occurrence)).Select(b=>b.Pool).ToArray();
                    var pools=bound.Select(o=>(FeatureDefinitionPointPool)byName[o.Feature]).ToArray();
                    var choices=bound.Select(o=>
                    {
                        var pool=(FeatureDefinitionPointPool)byName[o.Feature];var list=Spells(pool.SpellListOverride??cast.SpellListDefinition);
                        return new P.Pool(pool.Name+":"+o.Variant.Id,list,pool.PoolAmount,pool.PoolType switch
                        {
                            HeroDefinitions.PointsPoolType.Cantrip=>P.ChoiceKind.Cantrip,
                            HeroDefinitions.PointsPoolType.Spell=>P.ChoiceKind.Spell,
                            _=>P.ChoiceKind.CantripOrSpell
                        },pool.MinSpellLevel,pool.MaxSpellLevel,pool.ExtraSpellsTag,pool.SpellListOverride?.Name,pool.RitualOnly,
                        list.Where(spell=>DatabaseHelper.GetDefinition<SpellDefinition>(spell.Id).Ritual).Select(spell=>spell.Id),variant:o.Variant,authorizedList:(pool.SpellListOverride??cast.SpellListDefinition)?.Name);
                    }).ToList();
                    if(pools.Length==0 && cast.SpellKnowledge==RuleDefinitions.SpellKnowledge.Selection)
                    {
                        var count=Rows(cast.KnownCantrips)[Math.Max(0,unlock-1)];
                        if(count>0)choices.Add(new P.Pool(cast.Name+":native-cantrips",Spells(cast.SpellListDefinition).Where(spell=>spell.Circle==0),count,true));
                    }
                    var supported=cast.SpellCastingOrigin==FeatureDefinitionCastSpell.CastingOrigin.Race && !occurrence.Variant.Unsupported;
                    var spec=new P.Source(kind+":"+root.Name+":"+cast.Name+":"+occurrence.Variant.Id,kind,root.Name,cast.Name,cast.SpellCastingOrigin.ToString(),cast.spellcastingAbility,ownerClass,
                        supported?P.Domain.NativeSlots:P.Domain.Unsupported,unlock,cast.SlotsRecharge.ToString(),cast.SpellKnowledge.ToString(),Spells(cast.SpellListDefinition),choices,
                        Enumerable.Range(0,20).Select(i=>cast.SlotsPerLevels.Count==0?new int[9]:cast.SlotsPerLevels[Math.Min(i,cast.SlotsPerLevels.Count-1)].Slots.Take(9).Concat(new int[9]).Take(9)),
                        Rows(cast.KnownSpells),Rows(cast.KnownCantrips),variant:occurrence.Variant,spellListId:cast.SpellListDefinition?.Name);
                    output.Add(new Entry(spec,root,cast,cast,pools));
                }
                foreach(var grant in graph.Where(o=>byName.TryGetValue(o.Feature,out var f) && f is FeatureDefinitionGrantInvocations))
                {
                    var carrier=G.BindPool(grant,casting,name=>((FeatureDefinitionCastSpell)byName[name]).GetFirstSubFeatureOfType<FeatHelpers.SpellTag>()?.Name,"");
                    if(carrier.Variant.Constrained)throw new P.Rejection(P.Code.PendingAtomicJournal,root.Name+"/conditional invocation carrier requires native identity proof");
                    foreach(var inv in ((FeatureDefinitionGrantInvocations)byName[grant.Feature]).Invocations)
                        Invocation(root,kind,inv,(FeatureDefinitionCastSpell)byName[carrier.Feature],null,grant.Variant);
                }
                foreach(var occurrence in graph.Where(o=>byName.TryGetValue(o.Feature,out var f) && f is FeatureDefinitionPointPool p && p.PoolType==HeroDefinitions.PointsPoolType.Invocation))
                {
                    var invocationPool=(FeatureDefinitionPointPool)byName[occurrence.Feature];
                    var adept=root.Name=="FeatEldritchAdept" && invocationPool.Name=="PointPoolFeatEldritchAdept";
                    var spec=new P.Source(kind+":"+root.Name+":"+invocationPool.Name+":"+occurrence.Variant.Id,kind,root.Name,invocationPool.Name,
                        adept?"InvocationChoice":"UnresolvedInvocation",adept?"InvocationSpecific":"UnresolvedNativeAbility",null,
                        adept?P.Domain.InvocationSelection:P.Domain.Unsupported,adept?2:1,"NativeInvocation","InvocationChoice",Array.Empty<R.Spell>(),variant:occurrence.Variant);
                    output.Add(new Entry(spec,root,null,invocationPool,new[]{invocationPool}));
                }
                foreach(var occurrence in graph.Where(o=>byName.TryGetValue(o.Feature,out var f) && f is FeatureDefinitionAutoPreparedSpells))
                {
                    var auto=(FeatureDefinitionAutoPreparedSpells)byName[occurrence.Feature];
                    var matcher=auto.GetFirstSubFeatureOfType<RepertoireValidForAutoPreparedFeature>();
                    var supported=auto.SpellcastingClass || kind==P.Origin.Feat && ReferenceEquals(matcher,ValidateRepertoireForAutoprepared.AnyClassOrSubclass);
                    var spec=new P.Source(kind+":"+root.Name+":"+auto.Name+":"+occurrence.Variant.Id,kind,root.Name,auto.Name,"Attachment","NativeClassAbility",auto.SpellcastingClass?.Name,
                        supported?P.Domain.AttachedClass:P.Domain.Unsupported,unlock,"NativeClass","AutoPrepared",auto.AutoPreparedSpellsGroups.SelectMany(g=>g.SpellsList).Distinct().Select(s=>new R.Spell(s.Name,s.SpellLevel)),castingFeature:null,variant:occurrence.Variant);
                    output.Add(new Entry(spec,root,null,auto,Array.Empty<FeatureDefinitionPointPool>()));
                }
            }
            foreach(var race in DatabaseRepository.GetDatabase<CharacterRaceDefinition>())
                foreach(var group in race.FeatureUnlocks.GroupBy(u=>u.FeatureDefinition).Where(g=>Flatten(new[]{g.Key}).Any(f=>f is FeatureDefinitionCastSpell)))
                    Root(race,P.Origin.Race,new[]{group.Key},group.Min(u=>u.Level));
            foreach(var feat in DatabaseRepository.GetDatabase<FeatDefinition>())Root(feat,P.Origin.Feat,feat.Features);
            foreach(var style in DatabaseRepository.GetDatabase<FightingStyleDefinition>())Root(style,P.Origin.Style,style.Features);
            foreach(var background in DatabaseRepository.GetDatabase<CharacterBackgroundDefinition>())Root(background,P.Origin.Background,background.Features);
            var warlock=DatabaseHelper.CharacterClassDefinitions.Warlock;
            var warlockCast=DatabaseHelper.FeatureDefinitionCastSpells.CastSpellWarlock;
            void AttachedPool(BaseDefinition root,P.Origin kind,FeatureDefinition feature,int level)
            {
                var features=Flatten(new[]{feature}).ToDictionary(f=>f.Name,StringComparer.Ordinal);
                foreach(var occurrence in Graph(root,new[]{feature}).Where(o=>features.TryGetValue(o.Feature,out var f) && f is FeatureDefinitionPointPool p && SpellPool(p)))
                {
                    var pool=(FeatureDefinitionPointPool)features[occurrence.Feature];
                    var list=Spells(pool.SpellListOverride??warlockCast.SpellListDefinition);
                    var choice=new P.Pool(pool.Name,list,pool.PoolAmount,pool.PoolType switch
                    {
                        HeroDefinitions.PointsPoolType.Cantrip=>P.ChoiceKind.Cantrip,
                        HeroDefinitions.PointsPoolType.Spell=>P.ChoiceKind.Spell,
                        _=>P.ChoiceKind.CantripOrSpell
                    },pool.MinSpellLevel,pool.MaxSpellLevel,pool.ExtraSpellsTag,pool.SpellListOverride?.Name,pool.RitualOnly,
                        list.Where(spell=>DatabaseHelper.GetDefinition<SpellDefinition>(spell.Id).Ritual).Select(spell=>spell.Id),variant:occurrence.Variant,
                        authorizedList:(pool.SpellListOverride??warlockCast.SpellListDefinition).Name);
                    var spec=new P.Source(kind+":"+root.Name+":"+pool.Name+":"+occurrence.Variant.Id,kind,root.Name,pool.Name,"Attachment",warlockCast.spellcastingAbility,warlock.Name,
                        occurrence.Variant.Unsupported?P.Domain.Unsupported:pool.RitualOnly?P.Domain.NativeRitual:P.Domain.AttachedClass,
                        level,"NativeClass","ChoicePool",choice.Allowed,new[]{choice},castingFeature:warlockCast.Name,variant:occurrence.Variant);
                    output.Add(new Entry(spec,root,warlockCast,pool,new[]{pool}));
                }
            }
            var tome=DatabaseHelper.FeatureDefinitionFeatureSets.FeatureSetPactTome;
            AttachedPool(tome,P.Origin.Subclass,tome,3);
            foreach(var inv in DatabaseRepository.GetDatabase<InvocationDefinition>().Where(i=>i is not InvocationDefinitionCustom))
            {
                Invocation(inv,P.Origin.Invocation,inv,warlockCast,warlock.Name);
                if(inv.GrantedFeature)
                {
                    if(Flatten(new[]{inv.GrantedFeature}).OfType<FeatureDefinitionCastSpell>().Any())Root(inv,P.Origin.Invocation,new[]{inv.GrantedFeature},Math.Max(1,inv.RequiredLevel));
                    else AttachedPool(inv,P.Origin.Invocation,inv.GrantedFeature,Math.Max(1,inv.RequiredLevel));
                }
            }
            // Standard class/subclass casting and Bard Secrets retain their existing adapters.
            // Discover auxiliary Race-origin casting under every registered subclass instead.
            foreach(var sub in DatabaseRepository.GetDatabase<CharacterSubclassDefinition>())
                foreach(var group in sub.FeatureUnlocks.GroupBy(u=>u.Level))
                {
                    var auxiliary=group.Select(u=>u.FeatureDefinition).ToArray();
                    if(!Flatten(auxiliary).OfType<FeatureDefinitionCastSpell>().Any(c=>c.SpellCastingOrigin==FeatureDefinitionCastSpell.CastingOrigin.Race))continue;
                    var owner=DatabaseRepository.GetDatabase<CharacterClassDefinition>().FirstOrDefault(c=>c.FeatureUnlocks.SelectMany(u=>Flatten(new[]{u.FeatureDefinition})).OfType<FeatureDefinitionSubclassChoice>().Any(choice=>choice.Subclasses.Contains(sub.Name)));
                    if(!owner)throw new P.Rejection(P.Code.WrongOwner,sub.Name+"/no registered parent class");
                    Root(sub,P.Origin.Subclass,auxiliary,group.Key,owner.Name);
                }
            var candidate=new P(output.Select(e=>e.Source));
            entries=output.ToArray();registry=candidate;
            foreach(var e in entries)Main.Info("Auxiliary spell source: "+e.Source.Id+" casting="+e.Source.CastingFeature+" ability="+e.Source.Ability+" domain="+e.Source.Resource+" recharge="+e.Source.Recharge);
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException)
        { Main.Error("Auxiliary source capture unavailable; mixed admission remains closed: "+ex.Message); }
    }
    private static void Ready(){if(registry==null)throw new P.Rejection(P.Code.UnknownSource,"registry unavailable");}
    private static bool Owned(Entry e,RulesetCharacterHero hero) => e.Root switch
    {
        CharacterRaceDefinition race=>hero.RaceDefinition==race || hero.SubRaceDefinition==race,
        FeatDefinition feat=>hero.TrainedFeats.Contains(feat),
        FightingStyleDefinition style=>hero.TrainedFightingStyles.Contains(style),
        CharacterBackgroundDefinition background=>hero.BackgroundDefinition==background,
        CharacterSubclassDefinition sub=>hero.ClassesAndSubclasses.Values.Contains(sub),
        InvocationDefinition inv=>hero.Invocations.Any(i=>i.InvocationDefinition==inv),
        FeatureDefinition feature=>hero.HasAnyFeature(feature), _=>false
    };
    private static int Level(Entry e,RulesetCharacterHero hero) => (e.Source.OwnerClass=="Warlock" || e.Root is InvocationDefinition { RequiredLevel: > 1 })?WarlockInvocationContext.PrerequisiteLevel(hero):
        e.Source.OwnerClass==null?hero.ClassesHistory.Count:hero.ClassesAndLevels.FirstOrDefault(p=>p.Key.Name==e.Source.OwnerClass).Value;
    private static Entry Independent(RulesetSpellRepertoire rep)
    {
        Ready();var hero=rep.GetCaster() as RulesetCharacterHero ?? DruidWildshapeContext.StandardResourceOwner(rep.GetCaster());
        if(hero==null || !hero.SpellRepertoires.Contains(rep) && !rep.GetCaster().SpellRepertoires.Contains(rep))throw new P.Rejection(P.Code.WrongOwner,"no confirmed repertoire owner");
        var matches=entries.Where(e=>e.Cast==rep.SpellCastingFeature && e.Source.NativeOrigin!="Invocation" && e.Source.NativeOrigin!="Attachment" && Owned(e,hero)).ToArray();
        if(matches.Length!=1)throw new P.Rejection(matches.Length>1?P.Code.AmbiguousTag:P.Code.UnknownSource,rep.SpellCastingFeature.Name+"/unowned or ambiguous native source identity");
        if(rep.SpellCastingRace && rep.SpellCastingRace!=hero.RaceDefinition && rep.SpellCastingRace!=hero.SubRaceDefinition)throw new P.Rejection(P.Code.WrongOwner,"native race identity");
        var entry=matches[0];entry.Source.ValidateOwner(entry.Root.Name,rep.SpellCastingFeature.Name,rep.SpellCastingAbility,rep.SpellCastingClass?.Name,Level(entry,hero));
        return entry;
    }
    internal static bool IsIndependent(RulesetSpellRepertoire rep)
    {
        if(!RuntimeEnabled)return false;
        if(rep.SpellCastingFeature.SpellCastingOrigin==FeatureDefinitionCastSpell.CastingOrigin.Race ||
            rep.SpellCastingFeature.SpellCastingOrigin==FeatureDefinitionCastSpell.CastingOrigin.Class && !rep.SpellCastingClass)
        { Independent(rep);return true; }
        return false;
    }
    internal static void ValidatePaidSpell(RulesetEffectSpell effect)
    {
        if(!RuntimeEnabled)return;
        var rep=effect.SpellRepertoire;var spell=effect.SpellDefinition;
        if(IsIndependent(rep))
        {
            Independent(rep).Source.Route(spell.Name,false);
            if(!rep.HasKnowledgeOfSpell(spell))throw new P.Rejection(P.Code.InvalidChoice,rep.SpellCastingFeature.Name+"/unknown native spell");
            return;
        }
        Ready();
        var ritualTags=entries.Where(e=>e.Source.Resource==P.Domain.NativeRitual).SelectMany(e=>e.Pools.Select(p=>p.ExtraSpellsTag)).Distinct().ToArray();
        if(ritualTags.Any(t=>rep.ExtraSpellsByTag.TryGetValue(t,out var list) && list.Contains(spell)) && !rep.KnownSpells.Contains(spell) && !rep.AutoPreparedSpells.Contains(spell) &&
            !rep.ExtraSpellsByTag.Where(p=>!ritualTags.Contains(p.Key)).Any(p=>p.Value.Contains(spell)))
            throw new P.Rejection(P.Code.UnsupportedSource,spell.Name+"/ritual-only grant cannot debit standard or Pact slots");
    }
    internal static void ValidateNativeSpend(RulesetSpellRepertoire rep,int circle)
    {
        if(!RuntimeEnabled || !IsIndependent(rep))return;
        var entry=Independent(rep);
        if(circle<0 || circle>9 || circle>0 && entry.Source.NativeSlotRows[Math.Max(0,Math.Min(19,rep.GetCaster().GetOriginalHero().ClassesHistory.Count-1))][circle-1]==0)
            throw new P.Rejection(P.Code.WrongOwner,entry.Source.Id+"/native free budget cannot use shared slots");
    }
    internal static IReadOnlyList<C.AuxiliaryInput> SnapshotInputs(RulesetCharacterHero hero)
    {
        Ready();var active=entries.Where(e=>Owned(e,hero) && Level(e,hero)>=e.Source.RequiredLevel).ToArray();
        foreach(var e in active)e.Source.ValidateOwner(e.Root.Name,e.Source.Feature,e.Source.Ability,null,Level(e,hero));
        return active.Select(e=>e.Source.CalculatorInput(Level(e,hero))).ToArray();
    }
    internal static void ValidateSnapshot(RulesetCharacterHero hero,C.Result result)
    {
        if(!RuntimeEnabled)return;
        P.ValidateInputs(SnapshotInputs(hero),result.Input.AuxiliarySources);
        foreach(var rep in hero.SpellRepertoires.Where(r=>r.SpellCastingFeature.SpellCastingOrigin is FeatureDefinitionCastSpell.CastingOrigin.Race or FeatureDefinitionCastSpell.CastingOrigin.Monster ||
                    r.SpellCastingFeature.SpellCastingOrigin==FeatureDefinitionCastSpell.CastingOrigin.Class && !r.SpellCastingClass))Independent(rep);
    }
    internal static IEnumerable<SpellDefinition> AttachedAutoSpells(RulesetCharacter actor,RulesetSpellRepertoire repertoire)
    {
        if(!RuntimeEnabled)yield break;
        Ready();var hero=actor as RulesetCharacterHero ?? DruidWildshapeContext.StandardResourceOwner(actor);
        foreach(var e in entries.Where(e=>e.Source.Resource==P.Domain.AttachedClass && e.Feature is FeatureDefinitionAutoPreparedSpells && Owned(e,hero)))
        {
            var auto=(FeatureDefinitionAutoPreparedSpells)e.Feature;
            var matcher=auto.GetFirstSubFeatureOfType<RepertoireValidForAutoPreparedFeature>();
            var matches=matcher!=null?matcher(repertoire,actor):auto.SpellcastingClass?.Name==MultipleSpellRepertoiresContext.RepertoireClassId(repertoire);
            foreach(var spell in auto.AutoPreparedSpellsGroups.Where(g=>g.ClassLevel<=actor.GetSpellcastingLevel(repertoire)).SelectMany(g=>g.SpellsList))
                if(matches && e.Source.Route(spell.Name,true,true)==P.Domain.AttachedClass)yield return spell;
        }
    }
    internal static bool ValidateInvocation(RulesetCharacterHero hero,RulesetInvocation invocation)
    {
        if(!RuntimeEnabled || !invocation.InvocationDefinition.GrantedSpell)return true;
        try
        {
            Ready();var match=entries.Where(e=>e.Invocation==invocation.InvocationDefinition && Owned(e,hero)).ToArray();
            if(match.Length!=1)throw new P.Rejection(P.Code.UnknownSource,invocation.InvocationDefinition.Name);
            var e=match[0];
            if(invocation.spellCastingFeature!=e.Cast)throw new P.Rejection(P.Code.WrongOwner,e.Source.Id+"/invocation repertoire");
            e.Source.ValidateOwner(e.Root.Name,e.Source.Feature,e.Source.Ability,null,Level(e,hero));
            e.Source.Route(invocation.InvocationDefinition.GrantedSpell.Name,false);
            if(!e.Source.CanUseNativeInvocation(invocation.used))return false;
            return true;
        }
        catch(P.Rejection ex){Main.Error("Auxiliary spell rejected: "+ex.Message);return false;}
    }
    private static Entry[] RootEntries(BaseDefinition root) { Ready();return entries.Where(e=>e.Root==root).ToArray(); }
    private static bool Relevant(IEnumerable<FeatureDefinition> features) => Flatten(features).Any(f=>f is FeatureDefinitionCastSpell or FeatureDefinitionAutoPreparedSpells or FeatureDefinitionGrantInvocations || f is FeatureDefinitionPointPool p && (SpellPool(p) || p.PoolType==HeroDefinitions.PointsPoolType.Invocation));
    internal static bool ValidateRootChoice(RulesetCharacterHero hero,BaseDefinition root,IEnumerable<FeatureDefinition> features)
    {
        if(!RuntimeEnabled)return true;
        try
        {
            var found=RootEntries(root);
            if(found.Length==0 && Relevant(features))throw new P.Rejection(P.Code.UnknownSource,root.Name);
            var current=Flatten(features);
            foreach(var cast in current.OfType<FeatureDefinitionCastSpell>())
                if(!found.Any(e=>e.Cast==cast))throw new P.Rejection(P.Code.UnknownSource,root.Name+"/"+cast.Name);
            foreach(var auto in current.OfType<FeatureDefinitionAutoPreparedSpells>())
                if(!found.Any(e=>e.Feature==auto))throw new P.Rejection(P.Code.UnknownSource,root.Name+"/"+auto.Name);
            foreach(var inv in current.OfType<FeatureDefinitionGrantInvocations>().SelectMany(f=>f.Invocations).Where(i=>i.GrantedSpell))
                if(!found.Any(e=>e.Invocation==inv))throw new P.Rejection(P.Code.UnknownSource,root.Name+"/"+inv.Name);
            foreach(var e in found)
            {
                if(e.Source.Resource==P.Domain.InvocationSelection)EldritchInvocationSourcesContext.RequireAdeptJournal();
                e.Source.Variant.Active(null);
                foreach(var pool in e.Source.Pools)pool.Variant?.Active(null);
                if(e.Source.Resource==P.Domain.Unsupported)throw new P.Rejection(P.Code.UnsupportedSource,e.Source.Id);
            }
            if(root is FeatDefinitionWithPrerequisites feat && !feat.Validate(feat,hero).result)throw new P.Rejection(P.Code.PrematureSource,root.Name);
            return true;
        }
        catch(P.Rejection ex){Main.Error("Auxiliary pending choice rejected before grants: "+ex.Message);return false;}
    }
    // Pure planner validates source/pool budgets before any existing grant helper is entered.
    // Native current-class prefix is used ONLY to reproduce its execution tag, never ownership.
    private static bool Preflight(RulesetCharacterHero hero,bool complete)
    {
        Ready();if(!hero.TryGetHeroBuildingData(out var data))throw new P.Rejection(P.Code.WrongOwner,"missing building data");
        var pending=new List<Entry>();
        foreach(var feat in data.LevelupTrainedFeats.Values.SelectMany(f=>f).Distinct())
        {
            if(feat.Name=="FeatEldritchAdept")EldritchInvocationSourcesContext.RequireAdeptJournal();
            if(!ValidateRootChoice(hero,feat,feat.Features))throw new P.Rejection(P.Code.UnsupportedSource,feat.Name);
            pending.AddRange(RootEntries(feat));
        }
        foreach(var style in hero.TrainedFightingStyles)
        {
            if(!ValidateRootChoice(hero,style,style.Features))throw new P.Rejection(P.Code.UnsupportedSource,style.Name);
            pending.AddRange(RootEntries(style).Where(e=>e.Cast && hero.SpellRepertoires.All(r=>r.SpellCastingFeature!=e.Cast)));
        }
        foreach(var inv in data.LevelupTrainedInvocations.Values.SelectMany(i=>i).Distinct())
            pending.AddRange(RootEntries(inv));
        pending.AddRange(entries.Where(e=>e.Cast && e.Source.NativeOrigin=="Race" && e.Source.Kind is P.Origin.Race or P.Origin.Background or P.Origin.Subclass &&
            Owned(e,hero) && Level(e,hero)>=e.Source.RequiredLevel && hero.SpellRepertoires.All(r=>r.SpellCastingFeature!=e.Cast)));
        if(pending.Count==0)return false;
        var selected=pending.Distinct().ToArray();
        var journal=new P.Selection(registry,selected.Select(e=>e.Source.Id));
        if(!complete)return true;
        var claimed=new HashSet<string>(StringComparer.Ordinal);
        var rewards=selected.SelectMany(e=>e.Source.Pools.Select(p=>(Entry:e,Pool:p))).ToArray();
        P.ValidateNativeTags(rewards.Select(r=>new KeyValuePair<string,P.Pool>(r.Entry.Source.Id,r.Pool)));
        foreach(var e in selected)
        foreach(var pool in e.Source.Pools)
        {
            var definitions=e.Pools.Where(p=>pool.Id==p.Name || pool.Id.StartsWith(p.Name+":",StringComparison.Ordinal)).Distinct().ToArray();
            if(definitions.Length>1)throw new P.Rejection(P.Code.AmbiguousTag,e.Source.Id+"/native pool definition");
            var definition=definitions.SingleOrDefault();
            if(definition==null)throw new P.Rejection(P.Code.PendingAtomicJournal,e.Source.Id+"/native race selection binding requires13.7");
            var nativeTag=definition.ExtraSpellsTag;
            if(pool.Shared)
            {
                var sharedKeys=data.AcquiredCantrips.Keys.Concat(data.AcquiredSpells.Keys).Distinct().Where(k=>!string.IsNullOrEmpty(nativeTag) && k.EndsWith(nativeTag+nativeTag,StringComparison.Ordinal)).ToArray();
                if(sharedKeys.Length!=1 || !claimed.Add("cantrip:"+sharedKeys[0]) || !claimed.Add("spell:"+sharedKeys[0]))throw new P.Rejection(P.Code.AmbiguousTag,e.Source.Id+"/shared budget origin");
                var key=sharedKeys[0];var cantrips=data.AcquiredCantrips.TryGetValue(key,out var c)?c: new List<SpellDefinition>();
                var spells=data.AcquiredSpells.TryGetValue(key,out var s)?s:new List<SpellDefinition>();
                journal.Select(e.Source.Id,pool.Id,cantrips.Concat(spells).Select(spell=>spell.Name));
                continue;
            }
            var map=pool.Cantrip?data.AcquiredCantrips:data.AcquiredSpells;
            var keys=map.Keys.Where(k=>!string.IsNullOrEmpty(nativeTag) && k.EndsWith(nativeTag+nativeTag,StringComparison.Ordinal)).ToArray();
            if(keys.Length!=1 || !claimed.Add((pool.Cantrip?"cantrip:":"spell:")+keys[0]))throw new P.Rejection(P.Code.AmbiguousTag,e.Source.Id+"/"+nativeTag);
            journal.Select(e.Source.Id,pool.Id,map[keys[0]].Select(s=>s.Name));
        }
        journal.Commit();return true;
    }
    internal static bool ValidateBeforeFinalize(RulesetCharacterHero hero)
    {
        if(!RuntimeEnabled)return true;
        try
        {
            if(Preflight(hero,true))throw new P.Rejection(P.Code.PendingAtomicJournal,"auxiliary selections require Stage13.7");
            return true;
        }
        catch(P.Rejection ex){Main.Error("Auxiliary finalization rejected before native grants: "+ex.Message);return false;}
    }
    internal static void RequireGrantPreflight(RulesetCharacterHero hero)
    {
        if(RuntimeEnabled && !ValidateBeforeFinalize(hero))throw new P.Rejection(P.Code.PendingAtomicJournal,"native helper has no atomic journal");
    }
    internal static bool AllowNativeGrant(CharacterHeroBuildingData data,FeatureDefinitionCastSpell casting)
    {
        if(!RuntimeEnabled)return true;
        try
        {
            if(casting==null)throw new P.Rejection(P.Code.UnknownSource,"null casting feature");
            if(Preflight(data.HeroCharacter,false))throw new P.Rejection(P.Code.PendingAtomicJournal,casting.Name);
            if(casting.SpellCastingOrigin is FeatureDefinitionCastSpell.CastingOrigin.Race or FeatureDefinitionCastSpell.CastingOrigin.Monster ||
                entries.Any(e=>e.Cast==casting && e.Source.NativeOrigin=="Class"))
                throw new P.Rejection(P.Code.PendingAtomicJournal,casting.Name+"/auxiliary native mutation not journaled");
            if(!MultipleSpellRepertoiresContext.IsRegisteredCastingFeature(casting))throw new P.Rejection(P.Code.UnknownSource,casting.Name);
            return true;
        }
        catch(P.Rejection ex){Main.Error("Auxiliary native grant rejected: "+ex.Message);return false;}
    }
    internal static void FinishNativeRest(RulesetCharacterHero hero,RuleDefinitions.RestType rest,bool simulate)
    {
        if(!RuntimeEnabled || simulate)return;
        foreach(var repertoire in hero.SpellRepertoires.Where(r=>r.SpellCastingFeature.SpellCastingOrigin==FeatureDefinitionCastSpell.CastingOrigin.Race))
            if(Independent(repertoire).Source.RestoresOn(rest.ToString(),simulate))repertoire.RestoreAllSpellSlots();
    }
    private static bool GuardCharacterGrant(RulesetCharacter actor,FeatureDefinitionCastSpell casting)
    {
        if(!RuntimeEnabled || actor is not RulesetCharacterHero hero)return true;
        if(hero.TryGetHeroBuildingData(out var data))return AllowNativeGrant(data,casting);
        Main.Error(new P.Rejection(P.Code.WrongOwner,casting.Name+"/missing building journal").Message);return false;
    }
    [HarmonyPatch(typeof(RulesetCharacter),nameof(RulesetCharacter.GrantSpellRepertoire))]
    internal static class RepertoireGuard
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(RulesetCharacter __instance,FeatureDefinitionCastSpell __0,CharacterClassDefinition __1,CharacterSubclassDefinition __2)
        {
            if(!RuntimeEnabled || __instance is not RulesetCharacterHero hero)return true;
            try
            {
                Ready();if(__0==null)throw new P.Rejection(P.Code.UnknownSource,"null casting feature");
                var known=entries.Any(e=>e.Cast==__0 && e.Source.NativeOrigin is not ("Invocation" or "Attachment") && Owned(e,hero));
                if(__0.SpellCastingOrigin is FeatureDefinitionCastSpell.CastingOrigin.Race or FeatureDefinitionCastSpell.CastingOrigin.Monster || known || __0.SpellCastingOrigin==FeatureDefinitionCastSpell.CastingOrigin.Class && !__1)
                    throw new P.Rejection(known?P.Code.PendingAtomicJournal:P.Code.UnknownSource,__0.Name);
                if(!MultipleSpellRepertoiresContext.IsRegisteredCastingSource(__0,__1,__2))throw new P.Rejection(P.Code.UnknownSource,__0.Name+"/unregistered class source");
                return true;
            }
            catch(P.Rejection ex){Main.Error("Auxiliary repertoire mutation rejected: "+ex.Message);return false;}
        }
    }
    [HarmonyPatch(typeof(RulesetCharacter),nameof(RulesetCharacter.GrantSpell))]
    internal static class SpellGrantGuard
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(RulesetCharacter __instance,FeatureDefinitionCastSpell __1) => GuardCharacterGrant(__instance,__1);
    }
    [HarmonyPatch(typeof(RulesetCharacter),nameof(RulesetCharacter.GrantCantrip))]
    internal static class CantripGrantGuard
    {
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(RulesetCharacter __instance,FeatureDefinitionCastSpell __1) => GuardCharacterGrant(__instance,__1);
    }
}
