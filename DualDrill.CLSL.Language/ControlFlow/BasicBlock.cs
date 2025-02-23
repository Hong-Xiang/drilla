using DualDrill.CLSL.Language.LinearInstruction;
using System.Collections.Immutable;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface IBasicBlock<TElement, TResult, TTransfer>
{
    public ImmutableArray<TElement> Elements { get; }
    public TResult? Result { get; }
    public ImmutableArray<TTransfer> Inputs { get; }
    public ImmutableArray<TTransfer> Outputs { get; }
}

public sealed class BasicBlock<TElement> : IBasicBlock<TElement, TElement, Unit>
{
    public ImmutableArray<TElement> Elements { get; }
    public TElement? Result => default;
    public ImmutableArray<Unit> Inputs { get; } = [];
    public ImmutableArray<Unit> Outputs { get; } = [];

    public BasicBlock(ImmutableArray<TElement> elements)
    {
        Elements = elements;
    }

    public static BasicBlock<TElement> Create(IEnumerable<TElement> instructions)
    {
        return new([..instructions]);
    }
}