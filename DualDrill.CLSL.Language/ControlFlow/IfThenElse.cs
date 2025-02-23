using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class IfThenElse(
    StructuredControlFlowElementSequence TrueBody,
    StructuredControlFlowElementSequence FalseBody
) : IStructuredControlFlowRegion
{
    public StructuredControlFlowElementSequence TrueBody { get; } = TrueBody;
    public StructuredControlFlowElementSequence FalseBody { get; } = FalseBody;


    public IEnumerable<Label> ReferencedLabels =>
        TrueBody.Labels.Concat(FalseBody.Labels);

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        TrueBody.LocalVariables.Concat(FalseBody.LocalVariables);

    public TResult Accept<TResult>(IStructuredControlFlowRegion.IRegionPatternVisitor<TResult> pattern)
        => pattern.VisitIfThenElse(this);

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