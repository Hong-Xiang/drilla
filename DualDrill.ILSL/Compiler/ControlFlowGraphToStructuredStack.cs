using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.LinearInstruction;

namespace DualDrill.CLSL.Compiler;

using BasicBlock = BasicBlock<IStructuredStackInstruction>;
using InstructionRegion = IStructuredControlFlowRegion<IStructuredStackInstruction>;

internal class ControlFlowGraphToStructuredStack
{
}

public static partial class ShaderModuleExtension
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
    public static InstructionRegion ToStructuredControlFlow(this ControlFlowGraph<BasicBlock> controlFlowGraph)
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
                throw new NotImplementedException();
                //return new Loop(
                //    label,
                //    Block.Create([bb, BasicBlock.Create([ShaderInstruction.Br(label)])]));
            }
            else
            {
                return new Block<IStructuredStackInstruction>(label, [bb]);
            }
        }

        return DoTree(controlFlowGraph.EntryLabel);
    }

    public static ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> ToStructuredControlFlowStackModel(
        this ShaderModuleDeclaration<ControlFlowGraphFunctionBody> module
    )
    {
        return module.MapBody((m, f, b) => new StructuredStackInstructionFunctionBody(b.Graph.ToStructuredControlFlow()));
    }
}


