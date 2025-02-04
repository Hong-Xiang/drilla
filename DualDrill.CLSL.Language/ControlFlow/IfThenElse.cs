using DualDrill.CLSL.Language.ControlFlowGraph;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class IfThenElse<TInstruction>(
    Block<TInstruction> TrueBlock,
    Block<TInstruction> FalseBlock
) : IStructuredControlFlowRegion<TInstruction>
{
    public Block<TInstruction> TrueBlock { get; } = TrueBlock;
    public Block<TInstruction> FalseBlock { get; } = FalseBlock;
    public TResult AcceptElementVisitor<TResult>(Block<TInstruction>.IElement.IElementVisitor<TResult> visitor)
        => visitor.VisitIfThenElse(this);
    public TResult AcceptRegionVisitor<TResult>(IStructuredControlFlowRegion<TInstruction>.IRegionVisitor<TResult> visitor)
        => visitor.VisitIfThenElse(this);

}
