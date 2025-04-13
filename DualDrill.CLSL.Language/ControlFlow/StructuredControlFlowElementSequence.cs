using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Value;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface IStructuredControlFlowElement : ILocalDeclarationReferencingElement
{
}

public readonly record struct StructuredControlFlowElementSequence(
    ImmutableArray<IStructuredControlFlowElement> Elements)
    : ITextDumpable<ILocalDeclarationContext>
{
    public IEnumerable<Label> Labels => Elements.SelectMany(e => e.ReferencedLabels);
    public IEnumerable<VariableDeclaration> LocalVariables => Elements.SelectMany(e => e.ReferencedLocalVariables);
    public IEnumerable<IValue> LocalValues => Elements.SelectMany(e => e.ReferencedValues);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        foreach (var element in Elements)
        {
            element.Dump(context, writer);
        }
    }
}