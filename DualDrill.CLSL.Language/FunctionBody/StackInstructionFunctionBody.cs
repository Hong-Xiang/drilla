using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Value;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

public class StackInstructionFunctionBody : IFunctionBody
{
    ControlFlowGraph<StackInstructionBasicBlock> Graph { get; }
    DominatorTree DominatorTree { get; }

    public StackInstructionFunctionBody(
        ControlFlowGraph<StackInstructionBasicBlock> graph
    )
    {
        Graph = graph;
        DominatorTree = DominatorTree.CreateFromControlFlowGraph(Graph);
        Labels = [..graph.Labels()];
        LocalContext = new LocalDeclarationContext(Labels.Select(l => graph[l]));
    }

    public Label Entry => Graph.EntryLabel;
    public StackInstructionBasicBlock this[Label label] => Graph[label];


    public ISuccessor Successor(Label label)
        => Graph.Successor(label);

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }
    public ImmutableArray<IValue> Values { get; }

    public void Dump(IndentedTextWriter writer)
    {
        Dump(LocalContext, writer);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        foreach (var v in LocalVariables)
        {
            writer.WriteLine($"{context.VariableName(v)} : {v.Type.Name}");
        }

        writer.WriteLine();

        foreach (var bb in Labels.Select(label => this[label]))
        {
            bb.Dump(context, writer);
            writer.WriteLine();
        }
    }

    public ILocalDeclarationContext LocalContext { get; }
}