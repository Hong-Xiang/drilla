using DualDrill.CLSL.Language.LinearInstruction;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlow;

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

    public TResult AcceptElementVisitor<TResult>(Block<TInstruction>.IElement.IElementVisitor<TResult> visitor)
        => visitor.VisitBasicBlock(this);
}


