using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.ControlFlow;

namespace DualDrill.CLSL.Language.FunctionBody;

public static class UnifiedFunctionBody
{
    public static StackInstructionFunctionBody Create(IEnumerable<StackInstructionBasicBlock> blocks)
    {
        // ImmutableArray<StackInstructionBasicBlock> bks = [..blocks];
        // Debug.Assert(bks.Length > 0);
        // var cfg = new ControlFlowGraph<StackInstructionBasicBlock>(
        //     bks[0].Label,
        //     bks.ToDictionary(
        //         b => b.Label,
        //         b => new ControlFlowGraph<StackInstructionBasicBlock>.NodeDefinition(b.Successor, b))
        // );
        return new(blocks);
    }
}