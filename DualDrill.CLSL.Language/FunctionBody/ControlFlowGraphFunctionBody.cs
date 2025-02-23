using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class ControlFlowGraphFunctionBody : IFunctionBody
{
    public ControlFlowGraphFunctionBody(ControlFlowGraph<BasicBlock<IStructuredStackInstruction>> graph)
    {
        Graph = graph;
        Labels = [..graph.Labels().Distinct()];
        LocalVariables =
        [
            ..graph.Labels().SelectMany(l => graph[l].Elements.SelectMany(e => e.ReferencedLocalVariables)).Distinct()
        ];
    }

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public int LabelIndex(Label label)
    {
        throw new NotImplementedException();
    }

    public int VariableIndex(VariableDeclaration variable)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }
    public ControlFlowGraph<BasicBlock<IStructuredStackInstruction>> Graph { get; }
}