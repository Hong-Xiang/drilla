using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

public class StackInstructionFunctionBody : IFunctionBody
{
    ControlFlowGraph<StackInstructionBasicBlock> Graph { get; }
    DominatorTree DominatorTree { get; }
    private readonly FrozenDictionary<Label, int> LabelIndices;
    private readonly FrozenDictionary<VariableDeclaration, int> LocalVariableIndices;

    public StackInstructionFunctionBody(
        ControlFlowGraph<StackInstructionBasicBlock> graph
    )
    {
        Graph = graph;
        DominatorTree = DominatorTree.CreateFromControlFlowGraph(Graph);
        Labels = [..graph.Labels()];
        LocalVariables =
        [
            ..Labels.Select(l => graph[l]).SelectMany(b => b.Elements)
                    .SelectMany(e => e.ReferencedLocalVariables)
                    .Where(v => v.DeclarationScope == DeclarationScope.Function)
                    .Distinct()
        ];
        LabelIndices = Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        LocalVariableIndices = LocalVariables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
    }

    public Label Entry => Graph.EntryLabel;
    public StackInstructionBasicBlock this[Label label] => Graph[label];

    public int LabelIndex(Label label)
        => LabelIndices[label];

    public int VariableIndex(VariableDeclaration variable)
        => LocalVariableIndices[variable];


    public ISuccessor Successor(Label label)
        => Graph.Successor(label);

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }

    public void Dump(IndentedTextWriter writer)
    {
        Dump(this, writer);
    }

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        foreach (var v in LocalVariables)
        {
            writer.WriteLine($"{context.VariableName(v)} : {v.Type.Name}");
        }

        writer.WriteLine();

        foreach (var label in Labels)
        {
            writer.WriteLine(this.LabelName(label));
            using (writer.IndentedScope())
            {
                writer.WriteLine("successor: ");
                using (writer.IndentedScope())
                {
                    Graph.Successor(label).Dump(context, writer);
                }
            }

            writer.WriteLine();
            using (writer.IndentedScope())
            {
                var bb = this[label];
                bb.Dump(context, writer);
            }

            writer.WriteLine();
        }
    }
}