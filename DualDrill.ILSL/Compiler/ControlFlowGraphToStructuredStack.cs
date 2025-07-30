﻿using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Region;
using DualDrill.Common;

namespace DualDrill.CLSL.Compiler;

using BasicBlock = BasicBlock<IInstruction>;
using InstructionRegion = IStructuredControlFlowRegion;

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
        Stack<Block> blocks = [];


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

        IEnumerable<IStructuredControlFlowElement> DoBranch(
            Label source, Label target)
        {
            if (cfr.IsLoop(target))
            {
                return [ShaderInstruction.Br(target)];
            }

            if (controlFlowGraph.IsMergeNode(target))
            {
                return [ShaderInstruction.Br(target)];
            }

            return [DoTree(target)];
        }

        // Block ToBlock(
        //     IEnumerable<IStructuredControlFlowElement> elements
        // )
        // {
        //     ImmutableArray<IStructuredControlFlowElement> es = [.. elements];
        //     return es switch
        //     {
        //         [Block b] => b,
        //         _ => new Block(Label.Create(), new(es))
        //     };
        // }

        IEnumerable<IStructuredControlFlowElement> NodeWithin(Label target,
            ImmutableArray<Label> children)
        {
            var bb = controlFlowGraph[target];


            return children switch
            {
                [] => controlFlowGraph.Successor(target) switch
                {
                    TerminateSuccessor ret =>
                    [
                        .. bb.Elements,
                        ShaderInstruction.ReturnResult()
                    ],
                    UnconditionalSuccessor unc => [.. bb.Elements, .. DoBranch(target, unc.Target)],
                    ConditionalSuccessor brIf =>
                    [
                        ..bb.Elements,
                        new IfThenElse(
                            new([
                                ..DoBranch(target, brIf.TrueTarget)
                            ]),
                            new([
                                ..DoBranch(target, brIf.FalseTarget)
                            ])
                        ),
                    ],
                    _ => throw new NotSupportedException()
                },
                [var head, .. var rest] =>
                [
                    new Block(
                        head,
                        new([..NodeWithin(target, rest)])
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
                                  .Reverse()
                                  .ToImmutableArray();
            if (cfr.IsLoop(label))
            {
                return new Loop(label,
                    new([.. NodeWithin(label, mergeChildren)]));
            }
            else
            {
                return new Block(Label.Create(),
                    new([.. NodeWithin(label, mergeChildren)]));
            }
        }

        return DoTree(controlFlowGraph.EntryLabel);
    }

    public static RegionDefinition<Label, TP, TB> ToShaderIR<TP, TB>(
        this ControlFlowGraph<Unit> cfg,
        IReadOnlyDictionary<Label, RegionDefinition<Label, TP, TB>> regions)
    {
        // TODO: argument validation

        var dt = DominatorTree.CreateFromControlFlowGraph(cfg);

        RegionDefinition<Label, TP, TB> ToRegion(Label l)
        {
            var children = dt.GetChildren(l);
            var childrenExpressions = children.Select(ToRegion).ToImmutableArray();
            return regions[l].WithBindings(childrenExpressions);
        }
        return ToRegion(cfg.EntryLabel);
    }

    public static ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>>
        ToStructuredControlFlowStackModel(
            this ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IInstruction>> module
        )
    {
        throw new NotImplementedException();
        // return module.MapBody(
        //     (m, f, b) => new StackInstructionFunctionBody(
        //         b.Graph.ToStructuredControlFlow()
        //     ));
    }
}