using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlowGraph;


/// <summary>
/// Encoding of Loop | Block | IfThenElse
/// </summary>
public interface IStructuredControlFlowRegion<TInstruction> : IStructuredControlFlowElement
    where TInstruction : IInstruction
{
    sealed class FuncVisitor<TResult>(
        Func<Block<TInstruction>, TResult> Block,
        Func<Loop<TInstruction>, TResult> Loop,
        Func<IfThenElse<TInstruction>, TResult> IfThenElse
    ) : IRegionPatternVisitor<TResult>
    {
        public TResult VisitBlock(Block<TInstruction> block) => Block(block);
        public TResult VisitIfThenElse(IfThenElse<TInstruction> ifThenElse)
            => IfThenElse(ifThenElse);
        public TResult VisitLoop(Loop<TInstruction> loop)
            => Loop(loop);
    }

    public interface IRegionPatternVisitor<TResult>
    {
        TResult VisitBlock(Block<TInstruction> block);
        TResult VisitLoop(Loop<TInstruction> loop);
        TResult VisitIfThenElse(IfThenElse<TInstruction> ifThenElse);

        public static IRegionPatternVisitor<TResult> Create(
            Func<Block<TInstruction>, TResult> block,
            Func<Loop<TInstruction>, TResult> loop,
            Func<IfThenElse<TInstruction>, TResult> ifThenElse
        ) => new FuncVisitor<TResult>(block, loop, ifThenElse);
    }

    //public TResult Accept<TResult>(IRegionPatternVisitor<TResult> pattern);
}

/// <summary>
/// Encoding of Block | Loop
/// </summary>
/// <typeparam name="TInstruction"></typeparam>
public interface ILabeledStructuredControlFlowRegion<TInstruction>
    : IStructuredControlFlowRegion<TInstruction>
        , ILabeledEntity
    where TInstruction : IInstruction
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