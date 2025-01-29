using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

public sealed record class Block(
    ILabelScopeInstructionGroup? Root,
    Label Label,
    ImmutableArray<IInstructionRegion> Children,
    ImmutableArray<IShaderType> StackParameters,
    ImmutableArray<IShaderType> StackResults,
    FrozenSet<VariableDeclaration> LocalVariables
) : ILabeledEntity, ILabelScopeInstructionGroup, IInstructionRegionElement
{
    IEnumerable<IInstructionRegionElement> IInstructionRegion.Children => Children;

    public ILabeledEntity GetLabelTarget(Label label) =>
        Label.Equals(label)
        ? this
        : Root?.GetLabelTarget(label) ?? throw new LabelNotFoundException(label);
}
