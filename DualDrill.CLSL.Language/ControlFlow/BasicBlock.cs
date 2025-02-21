using DualDrill.CLSL.Language.LinearInstruction;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class BasicBlock<TElement>
{
    public ReadOnlyMemory<TElement> Elements { get; }

    public BasicBlock(ReadOnlyMemory<TElement> elements)
    {
        Elements = elements;
    }

    public static BasicBlock<TElement> Create(ReadOnlySpan<TElement> instructions)
    {
        return new(instructions.ToImmutableArray().AsMemory());
    }
}