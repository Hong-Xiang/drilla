using DualDrill.CLSL.Language.ControlFlowGraph;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Loop<TInstruction>(
    Label Label,
    Block<TInstruction> Body)
  : ILabeledEntity
  , ILabeledStructuredControlFlowRegion<TInstruction>
{
    public Label Label { get; } = Label;
    public Block<TInstruction> Body { get; } = Body;

    public IEnumerable<TInstruction> Instructions => Body.Instructions;

    public IEnumerable<Label> Labels => [Label, ..Body.Labels];

    public TResult AcceptElementVisitor<TResult>(Block<TInstruction>.IElement.IElementVisitor<TResult> visitor)
        => visitor.VisitLoop(this);
    public TResult AcceptRegionVisitor<TResult>(IStructuredControlFlowRegion<TInstruction>.IRegionVisitor<TResult> visitor)
        => visitor.VisitLoop(this);
}
