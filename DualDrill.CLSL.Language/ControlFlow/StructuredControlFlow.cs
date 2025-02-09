using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

/// <summary>
/// Encoding of Loop | Block | IfThenElse
/// </summary>
public interface IStructuredControlFlowRegion<TInstruction>
    : Block<TInstruction>.IElement
    where TInstruction : IInstruction
{
    public interface IRegionVisitor<TResult>
    {
        TResult VisitBlock(Block<TInstruction> block);
        TResult VisitLoop(Loop<TInstruction> loop);
        TResult VisitIfThenElse(IfThenElse<TInstruction> ifThenElse);
    }
    TResult AcceptRegionVisitor<TResult>(IRegionVisitor<TResult> visitor);

    IEnumerable<TInstruction> Instructions { get; }
    IEnumerable<Label> Labels { get; }
    IEnumerable<VariableDeclaration> LocalVariables { get; }
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
    public static ControlFlowAnalysisResult<TData> ControlFlowAnalysis<TData>(this ControlFlowGraph<TData> controlFlowGraph)
            => new ControlFlowAnalysisResult<TData>(controlFlowGraph);
}
