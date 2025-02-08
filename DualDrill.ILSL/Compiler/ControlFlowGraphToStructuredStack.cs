using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;

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
        var dt = cfr.DominatorTree;

        Stack<Label[]> childrenLabels = [];
        Stack<Block<IStructuredStackInstruction>> blocks = [];


        // there is only two ways to enter a basic block
        //  - fall through
        //  - br instructions

        // and to leave a block
        //  - fall through
        //  - br (including return)

        // for structured control flow, we need to ensure all brs are properly nested

        // for a node in dominator tree
        //   if a control flow graph is reducible, every loop has a loop head and dominates all nodes in the loop 
        //   thus in dominator tree, any node which could potentially br to loop head is under its children sub tree
        //   so that we can translate loop node into Loop region, and all its children is nested in the Loop region
        //   the question its children nodes could still br to other nodes,
        //   how can we ensure those nodes are properly nested?

        IEnumerable<Block<IStructuredStackInstruction>.IElement> DoBranch(Label source, Label target)
        {
            if (cfr.IsLoop(target))
            {
                return [BasicBlock<IStructuredStackInstruction>.Create([ShaderInstruction.Br(target)])];
            }
            if (controlFlowGraph.IsMergeNode(target))
            {
                return [BasicBlock<IStructuredStackInstruction>.Create([ShaderInstruction.Br(target)])];
            }
            return [DoTree(target)];
        }

        Block<IStructuredStackInstruction> ToBlock(
            IEnumerable<Block<IStructuredStackInstruction>.IElement> elements
        )
        {
            ImmutableArray<Block<IStructuredStackInstruction>.IElement> es = [.. elements];
            return es switch
            {
            [Block<IStructuredStackInstruction> b] => b,
                _ => new Block<IStructuredStackInstruction>(Label.Create(), es)
            };
        }

        IEnumerable<Block<IStructuredStackInstruction>.IElement> NodeWithin(Label target, ImmutableArray<Label> children)
        {
            ImmutableArray<Label> childrenLabels = [.. children];
            var bb = controlFlowGraph[target];


            return childrenLabels switch
            {
            [] => controlFlowGraph.Successor(target) switch
            {
                ReturnOrTerminateSuccessor ret =>
                [bb,
                 BasicBlock<IStructuredStackInstruction>.Create([ ShaderInstruction.Return() ])],
                BrOrNextSuccessor unc => [bb, .. DoBranch(target, unc.Target)],
                BrIfSuccessor brIf =>
                    [
                        bb,
                        new IfThenElse<IStructuredStackInstruction>(
                            ToBlock(DoBranch(target, brIf.TrueTarget)),
                            ToBlock(DoBranch(target, brIf.FalseTarget))
                        ),
                    ],
                _ => throw new NotSupportedException()
            },
            [var head, .. var rest] =>
                [
                    new Block<IStructuredStackInstruction>(
                        head,
                        [..NodeWithin(target, rest)]
                    ),
                    DoTree(head)
                ]
            };
        }

        InstructionRegion DoTree(Label label)
        {
            var bb = controlFlowGraph[label];
            var mergeChildren = dt.GetChildren(label)
                                  .Where(controlFlowGraph.IsMergeNode)
                                  .ToImmutableArray();
            if (cfr.IsLoop(label))
            {
                return new Loop<IStructuredStackInstruction>(label,
                    new Block<IStructuredStackInstruction>(
                        Label.Create(),
                        [.. NodeWithin(label, mergeChildren)]));
            }
            else
            {
                return new Block<IStructuredStackInstruction>(Label.Create(), [.. NodeWithin(label, mergeChildren)]);
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


