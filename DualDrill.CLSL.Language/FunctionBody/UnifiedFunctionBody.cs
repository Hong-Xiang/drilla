using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.FunctionBody;

public static class UnifiedFunctionBody
{
    public static UnifiedFunctionBody<TBasicBlock> Create<TBasicBlock>(
        IEnumerable<IStructuredControlFlowElement> blocks)
        where TBasicBlock : IBasicBlock2
        => new(blocks);
}

public class UnifiedFunctionBody<TBasicBlock> : IUnifiedFunctionBody<TBasicBlock>
    where TBasicBlock : IBasicBlock2
{
    ControlFlowGraph<TBasicBlock> Graph { get; }
    DominatorTree DominatorTree { get; }

    internal UnifiedFunctionBody(
        IEnumerable<IStructuredControlFlowElement> bodyElements
        // ControlFlowGraph<StackInstructionBasicBlock> graph
    )
    {
        Body = new StructuredControlFlowElementSequence([..bodyElements]);
        BasicBlocks = [..Body.GetBasicBlocks<TBasicBlock>()];

        Debug.Assert(BasicBlocks.Length > 0);
        var graph = new ControlFlowGraph<TBasicBlock>(
            BasicBlocks[0].Label,
            BasicBlocks.ToDictionary(
                b => b.Label,
                b => new ControlFlowGraph<TBasicBlock>.NodeDefinition(b.Successor, b))
        );
        Graph = graph;
        DominatorTree = DominatorTree.CreateFromControlFlowGraph(Graph);
        DeclarationContext = new LocalDeclarationContext(graph.Labels().Select(l => (IDeclarationUser)graph[l]));
        foreach (var l in DeclarationContext.Labels)
        {
            Debug.Assert(graph[l].Successor == graph.Successor(l));
        }
    }

    private StructuredControlFlowElementSequence Body { get; }
    private ImmutableArray<TBasicBlock> BasicBlocks { get; }
    public Label Entry => BasicBlocks[0].Label;
    public TBasicBlock this[Label label] => Graph[label];


    public ISuccessor Successor(Label label)
        => Graph.Successor(label);

    public void Dump(IndentedTextWriter writer)
    {
        Dump(DeclarationContext, writer);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        foreach (var v in context.LocalVariables)
        {
            writer.WriteLine($"{context.VariableName(v)} : {v.Type.Name}");
        }

        writer.WriteLine();

        foreach (var bb in context.Labels.Select(label => this[label]))
        {
            bb.Dump(context, writer);
            writer.WriteLine();
        }
    }

    public ILocalDeclarationContext DeclarationContext { get; }


    public IUnifiedFunctionBody<TResultBasicBlock> ApplyTransform<TResultBasicBlock>(
        IBasicBlockTransform<TBasicBlock, TResultBasicBlock> transform) where TResultBasicBlock : IBasicBlock2
    {
        throw new NotImplementedException();
    }

    public TResult Traverse<TElementResult, TResult>(
        IControlFlowElementSequenceTraverser<TBasicBlock, TElementResult, TResult> traverser)
    {
        throw new NotImplementedException();
    }

    public UnifiedFunctionBody<TResultBasicBlock>
        ApplyBasicBlockTransform<TResultBasicBlock>(
            IBasicBlockTransform<TBasicBlock, TResultBasicBlock> transform)
        where TResultBasicBlock : IBasicBlock2
    {
        var traverser = new FunctionBodyBasicBlockTransformTraverser<
            TBasicBlock, TResultBasicBlock>(transform);
        var resultElements = Body.Traverse(traverser);
        return UnifiedFunctionBody.Create<TResultBasicBlock>(resultElements.Elements);
    }
}