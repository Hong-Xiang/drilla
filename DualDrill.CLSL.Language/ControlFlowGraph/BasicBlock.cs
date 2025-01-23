using DualDrill.CLSL.LinearInstruction;
using System.Collections.Frozen;
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
    public ImmutableHashSet<IControlFlowEdge> Successors { get; set; } = [];
}

public sealed record class ControlFlowGraph(
    ImmutableArray<BasicBlock> BasicBlocks
)
{
}

public interface IControlFlowEdge { }

public sealed record class FallthroughEdge(ILabel Target) : IControlFlowEdge { }
public sealed record class UnconditionalEdge(ILabel Target) : IControlFlowEdge { }
public sealed record class ConditionalEdge(ILabel Target) : IControlFlowEdge { }
public sealed record class SwitchEdge(ILabel Target) : IControlFlowEdge { }
public sealed record class ReturnEdge() : IControlFlowEdge { }
