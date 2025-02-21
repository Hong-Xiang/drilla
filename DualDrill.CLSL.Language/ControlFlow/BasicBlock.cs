using DualDrill.CLSL.Language.LinearInstruction;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class BasicBlock<TElement>
{
    public ImmutableArray<TElement> Elements { get; }

    public BasicBlock(ImmutableArray<TElement> elements)
    {
        Elements = elements;
    }

    public static BasicBlock<TElement> Create(ReadOnlySpan<TElement> instructions)
    {
        return new([..instructions]);
    }
}