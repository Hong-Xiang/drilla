using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class IfThenElse(
    StructuredControlFlowElementSequence TrueBody,
    StructuredControlFlowElementSequence FalseBody
) : IStructuredControlFlowRegion
  , IControlFlowElement<IfThenElse>
{
    public StructuredControlFlowElementSequence TrueBody { get; } = TrueBody;
    public StructuredControlFlowElementSequence FalseBody { get; } = FalseBody;


    public IEnumerable<Label> ReferencedLabels =>
        [..TrueBody.Labels, ..FalseBody.Labels];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        [..TrueBody.LocalVariables, ..FalseBody.LocalVariables];

    public IEnumerable<IValue> ReferencedValues => [..TrueBody.LocalValues, ..FalseBody.LocalValues];

    public TResult Accept<TResult>(IStructuredControlFlowRegion.IRegionPatternVisitor<TResult> pattern)
        => pattern.VisitIfThenElse(this);

    public IfThenElse ApplyTransform<TSourceBasicBlock, TResultBasicBlock>(
        IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock> transform)
        where TSourceBasicBlock : IBasicBlock2 where TResultBasicBlock : IBasicBlock2
        => new(TrueBody.ApplyTransform(transform), FalseBody.ApplyTransform(transform));

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("if:");
        using (writer.IndentedScope())
        {
            TrueBody.Dump(context, writer);
        }

        writer.WriteLine("else:");
        using (writer.IndentedScope())
        {
            FalseBody.Dump(context, writer);
        }
    }
}