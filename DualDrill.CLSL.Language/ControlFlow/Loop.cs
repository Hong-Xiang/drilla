using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Loop<TInstruction>
    : IStructuredControlFlowRegion<TInstruction>
    where TInstruction : IInstruction
{
    public Loop(Label label,
        StructuredControlFlowElementSequence<TInstruction> body)
    {
        Label = label;
        Body = body;
    }

    public Label Label { get; }
    public StructuredControlFlowElementSequence<TInstruction> Body { get; }


    public IEnumerable<TInstruction> Instructions => Body.Instructions;

    public IEnumerable<Label> ReferencedLabels => [Label, .. Body.Labels];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.LocalVariables;
}