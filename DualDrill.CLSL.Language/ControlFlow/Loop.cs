using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Loop<TInstruction>(Label label,
    StructuredControlFlowElementSequence<TInstruction> body)
    : ILabeledStructuredControlFlowRegion<TInstruction>
    where TInstruction : IInstruction
{
    public Label Label { get; } = label;
    public StructuredControlFlowElementSequence<TInstruction> Body { get; } = body;

    public IEnumerable<TInstruction> Instructions => Body.Instructions;

    public IEnumerable<Label> ReferencedLabels => [Label, .. Body.Labels];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.LocalVariables;

    public IStatement BrCurrentStatement() => SyntaxFactory.Continue();
}