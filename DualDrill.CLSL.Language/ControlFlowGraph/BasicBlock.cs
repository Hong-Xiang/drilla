using DualDrill.CLSL.LinearInstruction;
using System.Collections.Immutable;

namespace DualDrill.CLSL.ControlFlowGraph;

public sealed record class BasicBlock(int Offset) : ILabel
{
    public int Index
    {
        get;
        set;
    } = -1;
    public ImmutableArray<IInstruction> Instructions { get; set; } = [];

    public BasicBlock? Successor { get; set; } = null;
    public BasicBlock? ConditionalSuccessor { get; set; } = null;
}

public sealed record class ControlFlowGraph(
    BasicBlock EntryBlock,
    ImmutableHashSet<BasicBlock> Blocks
)
{
}

