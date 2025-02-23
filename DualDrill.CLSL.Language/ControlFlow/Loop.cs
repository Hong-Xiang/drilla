using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Loop(
    Label label,
    StructuredControlFlowElementSequence body)
    : ILabeledStructuredControlFlowRegion
{
    public Label Label { get; } = label;
    public StructuredControlFlowElementSequence Body { get; } = body;


    public IEnumerable<Label> ReferencedLabels => [Label, .. Body.Labels];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.LocalVariables;

    public IStatement BrCurrentStatement() => SyntaxFactory.Continue();

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"loop {context.LabelName(Label)}:");
        using (writer.IndentedScope())
        {
            Body.Dump(context, writer);
        }
    }
}