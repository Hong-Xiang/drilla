using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

/// <summary>
/// Control flow graph representation of function body,
/// LabelIndex(label) is reverse postorder visiting index
/// Predecessor, Dominators, DominatorTreeChildren return label ordered by LabelIndex asc
/// LabelIndex(Entry) = 0
/// </summary>
/// <typeparam name="TElement"></typeparam>
public interface IUnstructuredControlFlowFunctionBody<TElement>
    : IFunctionBody, ITextDumpable
    where TElement : IUnstructuredControlFlowElement
{
    Label Entry { get; }
    BasicBlock<TElement> this[Label label] { get; }
    ISuccessor Successor(Label label);
    IEnumerable<Label> Predecessor(Label label);
    IEnumerable<Label> Dominators(Label label);
    Label? ImmediateDominator(Label label);
    IEnumerable<Label> DominatorTreeChildren(Label label);

    IUnstructuredControlFlowFunctionBody<TResultElement>
        MapBody<TResultElement>(Func<BasicBlock<TElement>, BasicBlock<TResultElement>> f)
        where TResultElement : IUnstructuredControlFlowElement;
}

public sealed class CfgBody : IUnstructuredControlFlowFunctionBody<IStackStatement>
{
    public ControlFlowGraph<BasicBlock<IStackStatement>> Graph { get; }

    public CfgBody(ControlFlowGraph<BasicBlock<IStackStatement>> graph)
    {
        Graph = graph;
        Labels = [..graph.Labels()];
        Entry = graph.EntryLabel;
        LocalVariables =
        [
            ..Labels.Select(l => graph[l]).SelectMany(b => b.Elements.ToArray())
                    .SelectMany(e => e.ReferencedLocalVariables)
                    .Distinct()
        ];
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

    public void Dump(IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public Label Entry { get; }

    public BasicBlock<IStackStatement> this[Label label] => Graph[label];

    public ISuccessor Successor(Label label)
        => Graph.Successor(label);

    public IEnumerable<Label> Predecessor(Label label)
        => Graph.Predecessor(label);

    public IEnumerable<Label> Dominators(Label label)
    {
        throw new NotImplementedException();
    }

    public Label? ImmediateDominator(Label label)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Label> DominatorTreeChildren(Label label)
    {
        throw new NotImplementedException();
    }

    public IUnstructuredControlFlowFunctionBody<TResultElement> MapBody<TResultElement>(
        Func<BasicBlock<IStackStatement>, BasicBlock<TResultElement>> f)
        where TResultElement : IUnstructuredControlFlowElement
    {
        throw new NotImplementedException();
    }
}