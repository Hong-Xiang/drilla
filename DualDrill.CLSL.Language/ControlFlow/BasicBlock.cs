using DualDrill.CLSL.Language.LinearInstruction;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class BasicBlock<TElement>
{
    public ReadOnlyMemory<TElement> Instructions { get; }

    public BasicBlock(ReadOnlyMemory<TElement> instructions)
    {
        Instructions = instructions;
    }

    public static BasicBlock<TElement> Create(ReadOnlySpan<TElement> instructions)
    {
        return new(instructions.ToImmutableArray().AsMemory());
    }
}