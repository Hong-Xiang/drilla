using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Block(
    Label Label,
    StructuredControlFlowElementSequence Body
) : ILabeledStructuredControlFlowRegion
  , IControlFlowElement<Block>
{
    public Label Label { get; } = Label;
    public StructuredControlFlowElementSequence Body { get; } = Body;
    public IEnumerable<Label> ReferencedLabels => [Label, .. Body.Labels];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.LocalVariables;
    public IEnumerable<IShaderValue> ReferencedValues => Body.LocalValues;

    public TResult Accept<TResult>(IStructuredControlFlowRegion.IRegionPatternVisitor<TResult> pattern)
        => pattern.VisitBlock(this);

    public IStatement BrCurrentStatement() => SyntaxFactory.Break();

    public Block ApplyTransform<TSourceBasicBlock, TResultBasicBlock>(
        IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock> transform)
        where TSourceBasicBlock : IBasicBlock2 where TResultBasicBlock : IBasicBlock2
        => new(Label, Body.ApplyTransform(transform));

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"block {context.LabelName(Label)}:");
        using (writer.IndentedScope())
        {
            Body.Dump(context, writer);
        }
    }
}