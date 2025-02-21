using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Loop<TInstruction>(
    Label label,
    StructuredControlFlowElementSequence body)
    : ILabeledStructuredControlFlowRegion<TInstruction>
    where TInstruction : IInstruction
{
    public Label Label { get; } = label;
    public StructuredControlFlowElementSequence Body { get; } = body;


    public IEnumerable<Label> ReferencedLabels => [Label, .. Body.Labels];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.LocalVariables;

    public IStatement BrCurrentStatement() => SyntaxFactory.Continue();
}