using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Loop(
    Label label,
    StructuredControlFlowElementSequence body)
    : ILabeledStructuredControlFlowRegion
    , IControlFlowElement<Loop>
{
    public Label Label { get; } = label;
    public StructuredControlFlowElementSequence Body { get; } = body;


    public IEnumerable<Label> ReferencedLabels => [Label, .. Body.Labels];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.LocalVariables;
    public IEnumerable<IShaderValue> ReferencedValues => Body.LocalValues;

    public Loop ApplyTransform<TSourceBasicBlock, TResultBasicBlock>(
        IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock> transform)
        where TSourceBasicBlock : IBasicBlock2 where TResultBasicBlock : IBasicBlock2
    {
        return new(Label, Body.ApplyTransform(transform));
    }

    public TResult Accept<TResult>(IStructuredControlFlowRegion.IRegionPatternVisitor<TResult> pattern)
        => pattern.VisitLoop(this);

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