using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;

namespace DualDrill.CLSL.Language.FunctionBody;

public class StackInstructionFunctionBody : IFunctionBody
{
    ControlFlowGraph<StackInstructionBasicBlock> Graph { get; }
    DominatorTree DominatorTree { get; }

    public StackInstructionFunctionBody(
        IEnumerable<StackInstructionBasicBlock> blocks
        // ControlFlowGraph<StackInstructionBasicBlock> graph
    )
    {
        ImmutableArray<StackInstructionBasicBlock> bks = [..blocks];
        Debug.Assert(bks.Length > 0);
        var graph = new ControlFlowGraph<StackInstructionBasicBlock>(
            bks[0].Label,
            bks.ToDictionary(
                b => b.Label,
                b => new ControlFlowGraph<StackInstructionBasicBlock>.NodeDefinition(b.Successor, b))
        );
        Graph = graph;
        DominatorTree = DominatorTree.CreateFromControlFlowGraph(Graph);
        LocalContext = new LocalDeclarationContext(graph.Labels().Select(l => graph[l]));
        foreach (var l in LocalContext.Labels)
        {
            Debug.Assert(graph[l].Successor == graph.Successor(l));
        }
    }

    public Label Entry => Graph.EntryLabel;
    public StackInstructionBasicBlock this[Label label] => Graph[label];


    public ISuccessor Successor(Label label)
        => Graph.Successor(label);

    public void Dump(IndentedTextWriter writer)
    {
        Dump(LocalContext, writer);
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

    public ILocalDeclarationContext LocalContext { get; }
}