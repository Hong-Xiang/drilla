using System.Collections.Immutable;
using System.Diagnostics;

namespace DualDrill.ILSL.LinearIR;

public sealed record class BasicBlock(int Offset)
{
    public int Index
    {
        get;
        set;
    } = -1;
    public ImmutableArray<IInstruction> Instructions { get; set; } = [];
}
