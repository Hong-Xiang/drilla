using DualDrill.CLSL.LinearInstruction;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

[Obsolete]
public sealed record class BasicBlockLegacy(int Offset)
    : ILabel
{
    public int Index
    {
        get;
        set;
    } = -1;
    public ImmutableArray<IInstruction> Instructions { get; set; } = [];
}

public sealed class BasicBlock<TInstruction> : Block<TInstruction>.IElement
{
    public ReadOnlyMemory<TInstruction> Instructions { get; }
    public BasicBlock(ReadOnlyMemory<TInstruction> instructions)
    {
        Instructions = instructions;
    }

    public static BasicBlock<TInstruction> Create(ReadOnlySpan<TInstruction> instructions)
    {
        return new(instructions.ToImmutableArray().AsMemory());
    }

    public TResult AcceptElementVisitor<TResult>(Block<TInstruction>.IElement.IVisitor<TResult> visitor)
        => visitor.VisitBasicBlock(this);
}


