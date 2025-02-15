using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class IfThenElse<TInstruction>(
    StructuredControlFlowElementSequence<TInstruction> TrueBody,
    StructuredControlFlowElementSequence<TInstruction> FalseBody
) : IStructuredControlFlowRegion<TInstruction>
    where TInstruction : IInstruction
{
    public StructuredControlFlowElementSequence<TInstruction> TrueBody { get; } = TrueBody;
    public StructuredControlFlowElementSequence<TInstruction> FalseBody { get; } = FalseBody;

    public IEnumerable<TInstruction> Instructions =>
        TrueBody.Instructions.Concat(FalseBody.Instructions);

    public IEnumerable<Label> ReferencedLabels =>
        TrueBody.Labels.Concat(FalseBody.Labels);

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        TrueBody.LocalVariables.Concat(FalseBody.LocalVariables);
}