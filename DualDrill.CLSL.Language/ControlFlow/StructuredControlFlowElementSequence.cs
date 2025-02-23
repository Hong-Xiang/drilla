using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface IStructuredControlFlowElement : ILocalDeclarationReferencingElement
{
}

public readonly record struct StructuredControlFlowElementSequence(
    ImmutableArray<IStructuredControlFlowElement> Elements)
{
    public IEnumerable<Label> Labels => Elements.SelectMany(e => e.ReferencedLabels);
    public IEnumerable<VariableDeclaration> LocalVariables => Elements.SelectMany(e => e.ReferencedLocalVariables);
}