using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

/// <summary>
/// Encoding of Loop | Block | IfThenElse
/// </summary>
public interface IStructuredControlFlowRegion : IStructuredControlFlowElement
{
    sealed class FuncVisitor<TResult>(
        Func<Block, TResult> Block,
        Func<Loop, TResult> Loop,
        Func<IfThenElse, TResult> IfThenElse
    ) : IRegionPatternVisitor<TResult>
    {
        public TResult VisitBlock(Block block) => Block(block);

        public TResult VisitIfThenElse(IfThenElse ifThenElse)
            => IfThenElse(ifThenElse);

        public TResult VisitLoop(Loop loop)
            => Loop(loop);
    }

    public interface IRegionPatternVisitor<TResult>
    {
        TResult VisitBlock(Block block);
        TResult VisitLoop(Loop loop);
        TResult VisitIfThenElse(IfThenElse ifThenElse);

        public static IRegionPatternVisitor<TResult> Create(
            Func<Block, TResult> block,
            Func<Loop, TResult> loop,
            Func<IfThenElse, TResult> ifThenElse
        ) => new FuncVisitor<TResult>(block, loop, ifThenElse);
    }

    //public TResult Accept<TResult>(IRegionPatternVisitor<TResult> pattern);
}

/// <summary>
/// Encoding of Block | Loop
/// </summary>
/// <typeparam name="TInstruction"></typeparam>
public interface ILabeledStructuredControlFlowRegion
    : IStructuredControlFlowRegion
    , ILabeledEntity
{
    IStatement BrCurrentStatement();
}

public sealed class ControlFlowAnalysisResult<TData>
{
    public ControlFlowGraph<TData> ControlFlowGraph { get; }
    public DominatorTree DominatorTree { get; }

    public ControlFlowAnalysisResult(ControlFlowGraph<TData> controlFlowGraph)
    {
        ControlFlowGraph = controlFlowGraph;
        DominatorTree = ControlFlowGraph.GetDominatorTree();
    }

    public bool IsLoop(Label label)
    {
        var predecessors = ControlFlowGraph.Predecessor(label);
        return predecessors.Any(l => DominatorTree.Compare(label, l) <= 0);
    }
}

static partial class StructuredControlFlow
{
    public static ControlFlowAnalysisResult<TData> ControlFlowAnalysis<TData>(
        this ControlFlowGraph<TData> controlFlowGraph)
        => new ControlFlowAnalysisResult<TData>(controlFlowGraph);
}