using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
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
    : IFunctionBody, ITextDumpable, IFunctionBodyData
    where TElement : IBasicBlockElement
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
        where TResultElement : IBasicBlockElement;
}

public sealed class CfgBody<TElement> : IUnstructuredControlFlowFunctionBody<TElement>
    where TElement : IBasicBlockElement
{
    public ControlFlowGraph<BasicBlock<TElement>> Graph { get; }
    public DominatorTree DominatorTree { get; }

    private readonly FrozenDictionary<Label, int> LabelIndices;
    private readonly FrozenDictionary<VariableDeclaration, int> LocalVariableIndices;

    public CfgBody(ControlFlowGraph<BasicBlock<TElement>> graph)
    {
        Graph = graph;
        DominatorTree = DominatorTree.CreateFromControlFlowGraph(Graph);
        Labels = [..graph.Labels()];
        Entry = graph.EntryLabel;
        LocalVariables =
        [
            ..Labels.Select(l => graph[l]).SelectMany(b => b.Elements)
                    .SelectMany(e => e.ReferencedLocalVariables)
                    .Distinct()
        ];
        LabelIndices = Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        LocalVariableIndices = LocalVariables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
    }

    public int LabelIndex(Label label)
        => LabelIndices[label];

    public int VariableIndex(VariableDeclaration variable)
        => LocalVariableIndices[variable];

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    IEnumerable<Label> IFunctionBodyData.FunctionBodyDataLabels => Labels;

    IEnumerable<VariableDeclaration> IFunctionBodyData.FunctionBodyDataLocalVariables => LocalVariables;

    public ImmutableArray<Label> Labels { get; }

    public void Dump(IndentedTextWriter writer)
    {
        Dump(this, writer);
    }

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public Label Entry { get; }

    public BasicBlock<TElement> this[Label label] => Graph[label];

    public ISuccessor Successor(Label label)
        => Graph.Successor(label);

    public IEnumerable<Label> Predecessor(Label label)
        => Graph.Predecessor(label);

    public IEnumerable<Label> Dominators(Label label)
        => DominatorTree.Dominators(label);

    public Label? ImmediateDominator(Label label)
        => DominatorTree.ImmediateDominator(label);

    public IEnumerable<Label> DominatorTreeChildren(Label label)
        => DominatorTree.GetChildren(label);

    public IUnstructuredControlFlowFunctionBody<TResultElement> MapBody<TResultElement>(
        Func<BasicBlock<TElement>, BasicBlock<TResultElement>> f)
        where TResultElement : IBasicBlockElement
    {
        return new CfgBody<TResultElement>(new ControlFlowGraph<BasicBlock<TResultElement>>(
            Entry,
            Labels.ToFrozenDictionary(l => l, l => new ControlFlowGraph<BasicBlock<TResultElement>>.NodeDefinition(
                Successor(l),
                f(this[l])
            ))
        ));
    }
}