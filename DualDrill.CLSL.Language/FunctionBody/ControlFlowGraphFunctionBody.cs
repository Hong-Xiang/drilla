using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Value;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

/// <summary>
/// Control flow graph representation of function body,
/// LabelIndex(label) is reverse postorder visiting index
/// Predecessor, Dominators, DominatorTreeChildren return label ordered by LabelIndex asc
/// LabelIndex(Entry) = 0
/// </summary>
/// <typeparam name="TElement"></typeparam>
public sealed class ControlFlowGraphFunctionBody<TElement> : IFunctionBody, ILocalDeclarationContext
    where TElement : IBasicBlockElement
{
    public ControlFlowGraph<BasicBlock<TElement>> Graph { get; }
    public DominatorTree DominatorTree { get; }

    private readonly FrozenDictionary<Label, int> LabelIndices;
    private readonly FrozenDictionary<VariableDeclaration, int> LocalVariableIndices;

    public ControlFlowGraphFunctionBody(ControlFlowGraph<BasicBlock<TElement>> graph)
    {
        Graph = graph;
        DominatorTree = DominatorTree.CreateFromControlFlowGraph(Graph);
        Labels = [..graph.Labels()];
        Entry = graph.EntryLabel;
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

    public int LabelIndex(Label label)
        => LabelIndices[label];

    public int ValueIndex(IValue value)
    {
        throw new NotImplementedException();
    }

    public int VariableIndex(VariableDeclaration variable)
        => LocalVariableIndices[variable];

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }
    public ImmutableArray<IValue> Values { get; }

    public void Dump(IndentedTextWriter writer)
    {
        Dump(this, writer);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
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
                Graph.Successor(label).Dump(context, writer);
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

    public ControlFlowGraphFunctionBody<TResultElement> MapBody<TResultElement>(
        Func<BasicBlock<TElement>, BasicBlock<TResultElement>> f)
        where TResultElement : IBasicBlockElement
    {
        return new ControlFlowGraphFunctionBody<TResultElement>(new ControlFlowGraph<BasicBlock<TResultElement>>(
            Entry,
            Labels.ToFrozenDictionary(l => l, l => new ControlFlowGraph<BasicBlock<TResultElement>>.NodeDefinition(
                Successor(l),
                f(this[l])
            ))
        ));
    }

    public ControlFlowGraphFunctionBody<TResultElement> MapBody<TResultElement>(
        Func<Label, BasicBlock<TElement>, BasicBlock<TResultElement>> f)
        where TResultElement : IBasicBlockElement
    {
        return new ControlFlowGraphFunctionBody<TResultElement>(new ControlFlowGraph<BasicBlock<TResultElement>>(
            Entry,
            Labels.ToFrozenDictionary(l => l, l => new ControlFlowGraph<BasicBlock<TResultElement>>.NodeDefinition(
                Successor(l),
                f(l, this[l])
            ))
        ));
    }

    public ILocalDeclarationContext DeclarationContext => this;
}