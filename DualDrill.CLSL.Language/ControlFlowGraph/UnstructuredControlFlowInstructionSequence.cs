using DualDrill.CLSL.LinearInstruction;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

public record class UnstructuredControlFlowInstructionSequence(
    ImmutableArray<IInstruction> Instructions,
    FrozenDictionary<Label, LabelInstruction> LabelInstructions
) : ILabelScopeInstructionGroup
{
    public IEnumerable<IInstructionRegionElement> Children => throw new NotImplementedException();

    public ILabeledEntity GetLabelTarget(Label label)
        => LabelInstructions[label];
}
