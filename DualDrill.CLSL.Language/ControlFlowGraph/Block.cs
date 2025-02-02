using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

public sealed class Block<TInstruction>(
    Label Label,
    ImmutableArray<Block<TInstruction>.IElement> Body
) : ILabeledEntity
  , ILabeledStructuredControlFlowRegion<TInstruction>
{
    /// <summary>
    /// Encoding of Loop | Block | IfThenElse | BasicBlock
    /// </summary>
    public interface IElement
    {
        public interface IElementVisitor<TResult>
        {
            TResult VisitBlock(Block<TInstruction> block);
            TResult VisitLoop(Loop<TInstruction> loop);
            TResult VisitIfThenElse(IfThenElse<TInstruction> ifThenElse);
            TResult VisitBasicBlock(BasicBlock<TInstruction> basicBlock);
        }
        public TResult AcceptElementVisitor<TResult>(IElementVisitor<TResult> visitor);
    }

    public Label Label { get; } = Label;
    public ImmutableArray<IElement> Body { get; } = Body;
    public TResult AcceptRegionVisitor<TResult>(IStructuredControlFlowRegion<TInstruction>.IRegionVisitor<TResult> visitor)
        => visitor.VisitBlock(this);

    public static Block<TInstruction> Empty() => new Block<TInstruction>(Label.Create(), []);
    public static Block<TInstruction> Create(ReadOnlySpan<BasicBlock<TInstruction>> body) => new Block<TInstruction>(Label.Create(), [.. body]);

    public TResult AcceptElementVisitor<TResult>(IElement.IElementVisitor<TResult> visitor) => visitor.VisitBlock(this);
}
