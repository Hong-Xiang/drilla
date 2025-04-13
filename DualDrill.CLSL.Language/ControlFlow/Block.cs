using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Value;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Block(
    Label Label,
    StructuredControlFlowElementSequence Body
) : ILabeledStructuredControlFlowRegion
{
    public Label Label { get; } = Label;
    public StructuredControlFlowElementSequence Body { get; } = Body;
    public IEnumerable<Label> ReferencedLabels => [Label, .. Body.Labels];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.LocalVariables;
    public IEnumerable<IValue> ReferencedValues => Body.LocalValues;

    public TResult Accept<TResult>(IStructuredControlFlowRegion.IRegionPatternVisitor<TResult> pattern)
        => pattern.VisitBlock(this);

    public IStatement BrCurrentStatement() => SyntaxFactory.Break();

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"block {context.LabelName(Label)}:");
        using (writer.IndentedScope())
        {
            Body.Dump(context, writer);
        }
    }
}