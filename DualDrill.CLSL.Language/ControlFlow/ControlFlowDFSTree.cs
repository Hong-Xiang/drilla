using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class ControlFlowDFSTree
{
    public ControlFlowDFSTree(IControlFlowGraph controlFlowGraph)
    {
        ControlFlowGraph = controlFlowGraph;


        HashSet<Label> visited = [];
        Dictionary<Label, HashSet<Label>> children = [];
        Dictionary<Label, int> labelIndices = [];

        void Visit(Label l)
        {
            if (visited.Contains(l)) throw new InvalidOperationException($"Multiple visiting {l}");
            visited.Add(l);
            HashSet<Label> c = [];
            foreach (var s in controlFlowGraph.GetSucc(l))
                if (!visited.Contains(s))
                {
                    c.Add(s);
                    Visit(s);
                }

            labelIndices.Add(l, labelIndices.Count);
            children.Add(l, c);
        }

        Visit(controlFlowGraph.EntryLabel);

        LabelCount = visited.Count;
        var labels = new Label[LabelCount];
        foreach (var kv in labelIndices) labels[LabelCount - kv.Value - 1] = kv.Key;
        Labels = [.. labels];
        LabelIndices = Labels.Select((l, i) => (l, i)).ToFrozenDictionary(x => x.l, x => x.i);
        LabelChildren = children.ToFrozenDictionary(
            x => x.Key,
            x => x.Value.OrderBy(l => LabelIndices[l]).ToImmutableArray());
        LabelLoopFlags =
            Labels.ToFrozenDictionary(l => l, l => controlFlowGraph.GetPred(l).Any(p => GetIndex(p) >= GetIndex(l)));
    }

    public IControlFlowGraph ControlFlowGraph { get; }

    public int LabelCount { get; }
    public ImmutableArray<Label> Labels { get; }
    private FrozenDictionary<Label, int> LabelIndices { get; }
    private FrozenDictionary<Label, ImmutableArray<Label>> LabelChildren { get; }
    private FrozenDictionary<Label, bool> LabelLoopFlags { get; }

    public int GetIndex(Label label) => LabelIndices[label];

    public bool IsLoop(Label label) => LabelLoopFlags[label];

    public T Fold<T>(IFoldSemantic<T, T> semantic) => FoldLabel(semantic, ControlFlowGraph.EntryLabel);

    private T FoldLabel<T>(IFoldSemantic<T, T> semantic, Label l)
    {
        var children = LabelChildren[l].Select(c => FoldLabel(semantic, c));
        return semantic.Node(l, GetIndex(l), children);
    }

    public interface IFoldSemantic<in TI, out TO>
    {
        TO Node(Label label, int index, IEnumerable<TI> children);
    }
}

public static class ControlFlowExtension
{
    public static ControlFlowDFSTree GetDFSTree(this IControlFlowGraph controlFlowGraph) => new(controlFlowGraph);
}