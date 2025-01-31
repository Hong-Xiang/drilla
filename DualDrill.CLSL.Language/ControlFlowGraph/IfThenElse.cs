namespace DualDrill.CLSL.Language.ControlFlowGraph;

public sealed class IfThenElse<TInstruction>(
    Block<TInstruction> TrueBlock,
    Block<TInstruction> FalseBlock
) : IStructuredControlFlowRegion<TInstruction>
{
    public Block<TInstruction> TrueBlock { get; } = TrueBlock;
    public Block<TInstruction> FalseBlock { get; } = FalseBlock;
    public TResult AcceptElementVisitor<TResult>(Block<TInstruction>.IElement.IVisitor<TResult> visitor)
        => visitor.VisitIfThenElse(this);
    public TResult AcceptRegionVisitor<TResult>(IStructuredControlFlowRegion<TInstruction>.IVisitor<TResult> visitor)
        => visitor.VisitIfThenElse(this);
}
