using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class IfThenElse<TInstruction>(
    IStructuredControlFlowRegion<TInstruction> TrueBlock,
    IStructuredControlFlowRegion<TInstruction> FalseBlock
) : IStructuredControlFlowRegion<TInstruction>
    where TInstruction : IInstruction
{
    public IStructuredControlFlowRegion<TInstruction> TrueBlock { get; } = TrueBlock;
    public IStructuredControlFlowRegion<TInstruction> FalseBlock { get; } = FalseBlock;

    public IEnumerable<TInstruction> Instructions => TrueBlock.Instructions.Concat(FalseBlock.Instructions);

    public IEnumerable<Label> Labels => TrueBlock.Labels.Concat(FalseBlock.Labels);

    public IEnumerable<VariableDeclaration> LocalVariables => TrueBlock.LocalVariables.Concat(FalseBlock.LocalVariables);

    public TResult AcceptElementVisitor<TResult>(Block<TInstruction>.IElement.IElementVisitor<TResult> visitor)
        => visitor.VisitIfThenElse(this);
    public TResult AcceptRegionVisitor<TResult>(IStructuredControlFlowRegion<TInstruction>.IRegionVisitor<TResult> visitor)
        => visitor.VisitIfThenElse(this);

}
