using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface IStructuredControlFlowElement<out TInstruction>
    where TInstruction : IInstruction
{
    public IEnumerable<TInstruction> Instructions { get; }
    public IEnumerable<Label> ReferencedLabels { get; }
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables { get; }
}

public readonly record struct StructuredControlFlowElementSequence<TInstruction>(
    ImmutableArray<IStructuredControlFlowElement<TInstruction>> Elements)
    where TInstruction : IInstruction
{
    public IEnumerable<TInstruction> Instructions => Elements.SelectMany(e => e.Instructions);
    public IEnumerable<Label> Labels => Elements.SelectMany(e => e.ReferencedLabels);
    public IEnumerable<VariableDeclaration> LocalVariables => Elements.SelectMany(e => e.ReferencedLocalVariables);
}