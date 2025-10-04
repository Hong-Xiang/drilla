using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Analysis;

public sealed class ControlFlowAnalysis
{
    public ControlFlowAnalysis(IControlFlowGraph controlFlowGraph)
    {
        ControlFlowGraph = controlFlowGraph;
        DFSTree = new ControlFlowDFSTree(controlFlowGraph);
        DominatorTree = DFSTree.GetDominatorTree();
        PostDominatorTree = new PostDominatorTree(DFSTree);
    }

    public IControlFlowGraph ControlFlowGraph { get; }
    public ControlFlowDFSTree DFSTree { get; }
    public DominatorTree DominatorTree { get; }
    public PostDominatorTree PostDominatorTree { get; }

    public ImmutableArray<Label> Labels => DFSTree.Labels;

    public bool IsLoop(Label label)
    {
        var predecessors = ControlFlowGraph.GetPred(label);
        return predecessors.Any(l => DominatorTree.Compare(label, l) <= 0);
    }

    public int IndexOf(Label label) => DFSTree.GetIndex(label);
}

public static class ControlFlowGraphExtensions
{
    public static ControlFlowAnalysis ControlFlowAnalysis(this IControlFlowGraph controlFlowGraph) =>
        new(controlFlowGraph);
}