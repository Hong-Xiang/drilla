using DualDrill.CLSL.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

using BasicBlock = BasicBlock<IStructuredStackInstruction>;
using Block = Block<IStructuredStackInstruction>;
using InstructionRegion = IStructuredControlFlowRegion<IStructuredStackInstruction>;
using Loop = Loop<IStructuredStackInstruction>;



// TODO Move following implementation into CLSL.Compiler project
static partial class StructuredControlFlow
{
    /// <summary>
    /// unstructrued control flow into structured control flow.
    /// unstructrued control flow is represened by control flow graph
    /// structured control flow is represented by properly nested block/loop/if-then-else
    /// </summary>
    /// <typeparam name="TInstruction"></typeparam>
    /// <param name="controlFlowGraph"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static InstructionRegion GetStructuredControlFlow(this ControlFlowGraph<BasicBlock> controlFlowGraph)
    {
        // based on algorithm introduced by https://dl.acm.org/doi/10.1145/3547621

        var cfr = controlFlowGraph.ControlFlowAnalysis();

        InstructionRegion DoTree(Label label)
        {
            var bb = controlFlowGraph[label];
            var succ = controlFlowGraph.Successor(label);
            //var isLoop = controlFlowGraph.Predecessor(label).Any(l => dominatorTree)
            if (cfr.IsLoop(label))
            {
                return new Loop(
                    label,
                    Block.Create([bb, BasicBlock.Create([ShaderInstruction.Br(label)])]));
            }
            else
            {
                return new Block(label, [bb]);
            }
        }

        return DoTree(controlFlowGraph.EntryLabel);
    }
}
