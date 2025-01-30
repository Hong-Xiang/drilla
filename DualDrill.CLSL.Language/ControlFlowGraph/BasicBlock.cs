using DualDrill.CLSL.LinearInstruction;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

public sealed record class BasicBlock(int Offset) : ILabel, IControlFlowRegionElement
{
    public int Index
    {
        get;
        set;
    } = -1;
    public ImmutableArray<IInstruction> Instructions { get; set; } = [];
}
