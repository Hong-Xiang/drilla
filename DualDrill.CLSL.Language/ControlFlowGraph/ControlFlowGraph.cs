using System.Collections.Frozen;

namespace DualDrill.CLSL.Language.ControlFlowGraph;



public sealed partial class ControlFlowGraph<TNode>
{
    public Label Entry { get; }
    public int Count => Nodes.Count;
    public IReadOnlySet<Label> Predecessor(Label label) => Predecessors[label];
    public ISuccessor Successor(Label label) => Successors[label];
    public TNode this[Label label] => Nodes[label];


    FrozenDictionary<Label, TNode> Nodes { get; }
    FrozenDictionary<Label, ISuccessor> Successors { get; }
    FrozenDictionary<Label, FrozenSet<Label>> Predecessors { get; }
    internal ControlFlowGraph(
        Label entry,
        IDictionary<Label, TNode> nodes,
        IDictionary<Label, ISuccessor> successors)
    {
        Entry = entry;
        Nodes = nodes.ToFrozenDictionary();
        Successors = successors.ToFrozenDictionary();
        var predecessors = nodes.Keys.Select(n => KeyValuePair.Create(n, new HashSet<Label>())).ToDictionary();
        foreach (var (l, s) in successors)
        {
            s.Traverse((t) =>
            {
                predecessors[t].Add(l);
            });
        }
        Predecessors = predecessors.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.ToFrozenSet())).ToFrozenDictionary();
    }

    public void Traverse(Action<Label> beforeSuccessor, Action<Label> afterSuccessor)
    {
        HashSet<Label> visited = [];
        void DoVisit(Label label)
        {
            if (visited.Contains(label))
            {
                return;
            };
            visited.Add(label);
            beforeSuccessor(label);
            Successor(label).Traverse(DoVisit);
            afterSuccessor(label);
        }
        DoVisit(Entry);
    }
}

public static class ControlFlowGraph
{
    public static IEnumerable<Label> GetLabels<TNode>(this ControlFlowGraph<TNode> graph)
    {
        var labels = new List<Label>();
        graph.Traverse((l) => { }, labels.Add);
        labels.Reverse();
        return labels;
    }

    public static FrozenDictionary<Label, int> GetReversePostorderNumberingTable<TNode>(this ControlFlowGraph<TNode> graph)
    {
        return graph.GetLabels().Select((l, i) => KeyValuePair.Create(l, i)).ToFrozenDictionary();
    }
}
