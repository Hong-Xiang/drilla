using DualDrill.CLSL.Language.LinearInstruction;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class BasicBlock<TInstruction>
    where TInstruction : IInstruction
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
}