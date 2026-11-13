using System;
using System.Collections.Generic;
using System.Linq;
using P = SolastaUnfinishedBusiness.Models.AuxiliarySpellSourcesPlan;

namespace SolastaUnfinishedBusiness.Models;

// Paths retain occurrences; a union/exclusion is not flattened into unconditional ownership.
internal static class AuxiliarySpellSourceGraphPlan
{
    internal enum Mode { Leaf, Union, Exclusion, Conditional, Unsupported }
    internal sealed class Node(string feature,Mode mode=Mode.Leaf,IEnumerable<Node> children=null,string condition=null)
    {
        internal string Feature { get; } = feature;
        internal Mode Type { get; } = mode;
        internal IReadOnlyList<Node> Children { get; } = Array.AsReadOnly((children??Array.Empty<Node>()).ToArray());
        internal string Condition { get; } = condition;
    }
    internal sealed class Occurrence(string feature,P.Variant variant)
    { internal string Feature { get; }=feature;internal P.Variant Variant { get; }=variant; }
    internal static IReadOnlyList<Occurrence> Walk(string root,Node graph)
    {
        var result=new List<Occurrence>();
        void Visit(Node node,string[] path,List<KeyValuePair<string,string>> branches,List<string> conditions,bool unsupported,HashSet<Node> stack)
        {
            if(node==null || string.IsNullOrWhiteSpace(node.Feature) || !Enum.IsDefined(typeof(Mode),node.Type) || node.Type==Mode.Leaf && node.Children.Count>0 || !stack.Add(node))
                throw new P.Rejection(P.Code.UnsupportedSource,root+"/invalid or cyclic graph");
            var own=path.Concat(new[]{node.Feature}).ToArray();
            var tests=new List<string>(conditions);
            if(node.Type==Mode.Conditional)
            {
                if(string.IsNullOrWhiteSpace(node.Condition))throw new P.Rejection(P.Code.UnverifiedCondition,root+"/"+node.Feature);
                tests.Add(node.Condition);
            }
            unsupported|=node.Type==Mode.Unsupported;
            result.Add(new Occurrence(node.Feature,new P.Variant(root,own,branches,tests,unsupported)));
            for(var i=0;i<node.Children.Count;i++)
            {
                var choices=new List<KeyValuePair<string,string>>(branches);
                var ordinal=node.Children.Take(i).Count(c=>c.Feature==node.Children[i].Feature);
                if(node.Type==Mode.Exclusion)choices.Add(new KeyValuePair<string,string>(P.Variant.PathId(root,own),ordinal+":"+node.Children[i].Feature));
                Visit(node.Children[i],own.Concat(new[]{ordinal.ToString()}).ToArray(),choices,tests,unsupported,new HashSet<Node>(stack));
            }
        }
        Visit(graph,Array.Empty<string>(),new(),new(),false,new());return result.AsReadOnly();
    }
    // Resolve by nearest registered structural scope and explicit native spell tags only.
    // Shared spell list/ability and selected hero class are not ownership evidence.
    internal static Occurrence BindPool(Occurrence pool,IEnumerable<Occurrence> casting,Func<string,string> nativeCastTag,string poolTag)
    {
        var casts=casting.Where(c=>c.Variant.Root==pool.Variant.Root && !c.Variant.Branches.Any(b=>pool.Variant.Branches.TryGetValue(b.Key,out var p) && p!=b.Value)).ToArray();
        // Only complete ancestor feature paths are scopes; a sibling occurrence ordinal is not.
        for(var length=pool.Variant.Path.Count-2;length>=1;length-=2)
        {
            var prefix=pool.Variant.Path.Take(length).ToArray();
            var scoped=casts.Where(c=>c.Variant.Path.Take(length).SequenceEqual(prefix)).ToArray();
            if(scoped.Length==0)continue;
            if(scoped.Length==1)return scoped[0];
            var tagged=scoped.Where(c=>!string.IsNullOrEmpty(poolTag) && nativeCastTag(c.Feature)==poolTag).ToArray();
            if(tagged.Length==1)return tagged[0];
            throw new P.Rejection(P.Code.AmbiguousTag,pool.Variant.Id+"/multiple casting owners");
        }
        throw new P.Rejection(P.Code.UnknownSource,pool.Variant.Id+"/no registered casting owner");
    }
}
