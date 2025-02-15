using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Block<TInstruction>(
    Label Label,
    StructuredControlFlowElementSequence<TInstruction> Body
) : IStructuredControlFlowRegion<TInstruction>
    where TInstruction : IInstruction
{
    public Label Label { get; } = Label;
    public StructuredControlFlowElementSequence<TInstruction> Body { get; } = Body;


    public IEnumerable<TInstruction> Instructions => Body.Instructions;

    public IEnumerable<Label> ReferencedLabels => [Label, .. Body.Labels];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.LocalVariables;
}