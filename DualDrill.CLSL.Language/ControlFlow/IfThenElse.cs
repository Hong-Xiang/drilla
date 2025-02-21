using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class IfThenElse<TInstruction>(
    StructuredControlFlowElementSequence TrueBody,
    StructuredControlFlowElementSequence FalseBody
) : IStructuredControlFlowRegion<TInstruction>
    where TInstruction : IInstruction
{
    public StructuredControlFlowElementSequence TrueBody { get; } = TrueBody;
    public StructuredControlFlowElementSequence FalseBody { get; } = FalseBody;


    public IEnumerable<Label> ReferencedLabels =>
        TrueBody.Labels.Concat(FalseBody.Labels);

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        TrueBody.LocalVariables.Concat(FalseBody.LocalVariables);
}