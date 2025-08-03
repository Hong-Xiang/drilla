using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

public sealed class DominatorTree : IComparer<Label>
{
    FrozenDictionary<Label, int> LabelOrders { get; }
    /// <summary>
    /// Get all children of a node in dominator tree (ordered by reverse postorder numbering)
    /// which contains all nodes n which satisfies `ImmediateDominator(n) = node`
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public IEnumerable<Label> GetChildren(Label node)
        => ChildrenMaps[node];


    public Label? ImmediateDominator(Label label)
    {
        return AllLabelDominators[label] switch
        {
        [.., var d, _] => d,
            _ => null
        };
    }


    /// <summary>
    /// All dominators of given node, which is actually a path from root to given node in dominator tree
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public IEnumerable<Label> Dominators(Label node) => AllLabelDominators[node];

    FrozenDictionary<Label, ImmutableArray<Label>> AllLabelDominators { get; }
    FrozenDictionary<Label, ImmutableArray<Label>> ChildrenMaps { get; }
    ImmutableArray<Label> Labels { get; }
    public Label EntryLabel => Labels[0];

    /// <summary>
    /// Create dominator tree, with dominator sets etc 
    /// </summary>
    /// <param name="labels">labels of tree, ordered by reverse postorder numbering</param>
    /// <param name="donminatorSets">dominator sets of all labels, must be consistency with labels ordering</param>
    public DominatorTree(
        IEnumerable<Label> labels,
        IReadOnlyDictionary<Label, IEnumerable<Label>> dominatorSets
    )
    {
        Labels = [.. labels];
        LabelOrders = Labels.Select((l, i) => KeyValuePair.Create(l, i)).ToFrozenDictionary();
        AllLabelDominators = dominatorSets
                        .Select(kv => KeyValuePair.Create(
                            kv.Key,
                            kv.Value.OrderBy(l => LabelOrders[l]).ToImmutableArray()))
                        .ToFrozenDictionary();
        Debug.Assert(AllLabelDominators.Count == dominatorSets.Count);
        var childrenMap = new Dictionary<Label, List<Label>>();
        foreach (var l in labels)
        {
            childrenMap.Add(l, []);
        }
        foreach (var l in labels)
        {
            var p = ImmediateDominator(l);
            if (p is Label pl)
            {
                childrenMap[pl].Add(l);
            }
        }
        foreach (var l in labels)
        {
            childrenMap[l].Sort((a, b) => LabelOrders[a] - LabelOrders[b]);
        }
        ChildrenMaps = childrenMap.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.ToImmutableArray())).ToFrozenDictionary();

        Validate();
    }

    void Validate()
    {
        Debug.Assert(Labels.Length == AllLabelDominators.Count);
        Debug.Assert(Labels.Length == LabelOrders.Count);
        foreach (var l in Labels)
        {
            Debug.Assert(AllLabelDominators.ContainsKey(l));
        }
        foreach (var kv in AllLabelDominators)
        {
            Debug.Assert(kv.Key.Equals(kv.Value[^1]), "last element of dominators must be label itself");
        }
    }

    public static DominatorTree CreateFromControlFlowGraph<TNode>(ControlFlowGraph<TNode> graph)
    {
        ImmutableArray<Label> labels = [.. graph.Labels()];
        var dominatorSets = new Dictionary<Label, HashSet<Label>>();
        foreach (var label in labels)
        {
            dominatorSets[label] = [.. labels];
        }
        var e = graph.EntryLabel;
        dominatorSets[e] = [e];

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var label in labels)
            {
                if (label.Equals(e))
                {
                    continue;
                }
                HashSet<Label> tmp = [.. labels];
                foreach (var p in graph.Predecessor(label))
                {
                    tmp.IntersectWith(dominatorSets[p]);
                }
                tmp.Add(label);
                var cur = dominatorSets[label];
                if (cur.Count != tmp.Count)
                {
                    dominatorSets[label] = tmp;
                    changed = true;
                }
            }
        }
        Dictionary<Label, IEnumerable<Label>> ds = dominatorSets.Select(kv => KeyValuePair.Create(kv.Key, (IEnumerable<Label>)kv.Value)).ToDictionary();
        return new DominatorTree(labels, ds);
    }

    public int Compare(Label? x, Label? y)
    {
        return (x, y) switch
        {
            (null, null) => 0,
            (null, { }) => 1,
            ({ }, null) => -1,
            _ => LabelOrders[x] - LabelOrders[y]
        };
    }
}

public static partial class StructuredControlFlow
{
    public static DominatorTree GetDominatorTree<TData>(
        this ControlFlowGraph<TData> controlFlowGraph
    )
        => DominatorTree.CreateFromControlFlowGraph(controlFlowGraph);
}
