using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface IControlFlowGraph
{
    public Label EntryLabel { get; }
    public int LabelCount { get; }
    public IEnumerable<Label> GetPred(Label label);
    public IEnumerable<Label> GetSucc(Label label);
}

public sealed class ControlFlowGraph<TData> : IControlFlowGraph, IPrintable
{
    private readonly Action<ControlFlowGraph<TData>, IndentedTextWriter, PrettyPrintOption> prettyPrint;

    /// <summary>
    ///     Create a control flow graph labeled by Label and user TData
    ///     Note arguments must satisfy
    ///     * entry exists in key of nodes
    ///     * all successors of nodes exists in key of nodes
    /// </summary>
    /// <param name="entry">entry label</param>
    /// <param name="nodes">label to successor and data map</param>
    public ControlFlowGraph(
        Label entry,
        IReadOnlyDictionary<Label, NodeDefinition> nodes,
        Action<ControlFlowGraph<TData>, IndentedTextWriter, PrettyPrintOption>? prettyPrint = null)
    {
        if (!nodes.ContainsKey(entry))
            throw new ArgumentException("Entry label not found in nodes definition", nameof(nodes));

        EntryLabel = entry;
        DefinitionOrder = [.. nodes.Keys];
        var predecessors = nodes.Keys.Select(n => KeyValuePair.Create(n, new HashSet<Label>())).ToDictionary();
        foreach (var (l, s) in nodes.Select(kv => (kv.Key, kv.Value.Successor)))
            s.Traverse(t =>
            {
                if (!nodes.ContainsKey(t))
                    throw new ArgumentException($"{t} is successor of {s}, but not exists in nodes definition",
                        nameof(nodes));

                predecessors[t].Add(l);
            });

        Nodes = nodes.Select(kv =>
        {
            var node = new NodeData(kv.Value.Successor, kv.Value.Data, predecessors[kv.Key].ToFrozenSet());
            return KeyValuePair.Create(kv.Key, node);
        }).ToFrozenDictionary();
        this.prettyPrint = prettyPrint ?? PrettyPrintDefault;
    }

    public int Count => Nodes.Count;

    public TData this[Label label] => Nodes[label].Data;
    private FrozenDictionary<Label, NodeData> Nodes { get; }
    private ImmutableArray<Label> DefinitionOrder { get; }
    public Label EntryLabel { get; }

    public IEnumerable<Label> GetPred(Label label) => Predecessor(label);

    public IEnumerable<Label> GetSucc(Label label) => Successor(label).AllTargets();

    public int LabelCount => Count;

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        prettyPrint(this, writer, option);

    public IReadOnlySet<Label> Predecessor(Label label) => Nodes[label].Predecessors;

    public ISuccessor Successor(Label label) => Nodes[label].Successor;

    private readonly record struct NodeData(
        ISuccessor Successor,
        TData Data,
        FrozenSet<Label> Predecessors)
    {
    }

    public readonly record struct NodeDefinition(
        ISuccessor Successor,
        TData Data
    )
    {
    }

    private static void PrettyPrintDefault(
        ControlFlowGraph<TData> graph,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        var reachable = graph.Labels().ToImmutableArray();
        var reachableSet = reachable.ToFrozenSet();
        var labels = reachable.AddRange(graph.DefinitionOrder.Where(label => !reachableSet.Contains(label)));
        var labelIds = labels.Select((label, index) => (label, index))
                             .ToFrozenDictionary(item => item.label, item => item.index);
        var successorFormatter = new GenericSuccessorFormatter(labelIds);

        writer.WriteLine("cfg generic (payload rendering: ToString)");
        writer.Write("entry=");
        writer.WriteLine(LabelName(graph.EntryLabel, labelIds));
        foreach (var label in labels)
        {
            writer.Write(LabelName(label, labelIds));
            writer.Write(" predecessors=[");
            writer.Write(string.Join(
                ", ",
                graph.Predecessor(label)
                     .OrderBy(predecessor => labelIds[predecessor])
                     .Select(predecessor => LabelName(predecessor, labelIds))));
            writer.Write("] successor=");
            writer.Write(graph.Successor(label).Evaluate(successorFormatter, default(Unit)));
            writer.Write(" data=");
            writer.WriteLine(graph[label] is { } data ? data.ToString() : "<null>");
        }
    }

    private static string LabelName(Label label, IReadOnlyDictionary<Label, int> labelIds) =>
        "^" + labelIds[label].ToString(CultureInfo.InvariantCulture) + "(" + (label.Name ?? "<unnamed>") + ")";

    private sealed class GenericSuccessorFormatter(IReadOnlyDictionary<Label, int> labelIds)
        : ISuccessorSemantic<Unit, Label, string>
    {
        public string Unconditional(Unit context, Label target) =>
            "br -> " + LabelName(target, labelIds);

        public string Conditional(Unit context, Label trueTarget, Label falseTarget) =>
            "br_if -> t: " + LabelName(trueTarget, labelIds) +
            " f: " + LabelName(falseTarget, labelIds);

        public string Terminate(Unit context) => "return";
    }
}

public static class ControlFlowGraph
{
    public static ControlFlowGraph<TBlock> Create<TBlock>(
        BlockList<TBlock> blocks,
        Func<TBlock, ISuccessor> getSuccessor,
        Action<ControlFlowGraph<TBlock>, IndentedTextWriter, PrettyPrintOption>? prettyPrint = null)
        where TBlock : ILabeledEntity =>
        new(
            blocks.EntryLabel,
            blocks.Blocks.ToDictionary(
                block => block.Label,
                block => new ControlFlowGraph<TBlock>.NodeDefinition(getSuccessor(block), block)),
            prettyPrint);

    public static ControlFlowGraph<Unit> Create(
        Label entry,
        IReadOnlyDictionary<Label, ISuccessor> definitions
    )
    {
        return new ControlFlowGraph<Unit>(entry, CreateDefinitions(definitions.ToDictionary(l => l.Key, l =>
            new ControlFlowGraph<Unit>.NodeDefinition
            {
                Data = default,
                Successor = l.Value
            })));
    }

    public static IReadOnlyDictionary<Label, ControlFlowGraph<TData>.NodeDefinition> CreateDefinitions<TData>(
        Dictionary<Label, ControlFlowGraph<TData>.NodeDefinition> definitions
    ) =>
        definitions;

    public static IReadOnlyDictionary<Label, ControlFlowGraph<IRegionDefinition<Label, TB>>.NodeDefinition>
        CreateDefinitions<TP, TB>(
            IEnumerable<IRegionDefinition<Label, TB>> regions,
            Func<TB, ISuccessor> successor
        )
    {
        return regions.ToDictionary(
            r => r.Label,
            r => new ControlFlowGraph<IRegionDefinition<Label, TB>>.NodeDefinition(
                successor(r.Body),
                r
            )
        );
    }


    public static void Traverse<TNode>(this ControlFlowGraph<TNode> graph, Action<Label> beforeSuccessor,
        Action<Label> afterSuccessor)
    {
        HashSet<Label> visited = [];

        void DoVisit(Label label)
        {
            if (visited.Contains(label)) return;

            visited.Add(label);
            beforeSuccessor(label);
            graph.Successor(label).Traverse(DoVisit);
            afterSuccessor(label);
        }

        DoVisit(graph.EntryLabel);
    }

    public static IEnumerable<Label> Labels<TNode>(this ControlFlowGraph<TNode> graph)
    {
        var labels = new List<Label>();
        graph.Traverse(static _ => { }, labels.Add);
        labels.Reverse();
        return labels;
    }

    public static bool IsMergeNode<TNode>(this ControlFlowGraph<TNode> graph, Label label) =>
        graph.Predecessor(label).Count > 1;
}

internal interface IControlFlowGraphAlgebra<TBasicBlockI, TBasicBlockO, TSuccessorI, TSuccessorO>
{
    TBasicBlockO BasicBlock(Label label, TSuccessorI successor);
    TSuccessorO Successor();
}

internal interface IListAlgebra<TE, TLI, TLO>
{
    TLO Nil();
    TLO Cons(TE head, TLI tail);
}

internal interface IFoldLAlgebra<in TE, in TI, out TO>
{
    TO Stop(TI seed);
    TO Step(TI accu, TE curr);
}

// foldl :: (b -> a -> b) -> b -> [a] -> b

// State s a = s -> (a, s)

// sequence of instructions -> basic blocks (stack instruction) sequence (unstructed control flow)
//                          -> map all basic blocks to SSA value instruction?
//                          -> to structured control flow
//                          -> transforms inter/intra basic blocks (need access to instructions)

// sequence of element -> nested structured control flow
//                     -> basic block transform // is it possible to working on different subset of language?

// cata (b :: B) (f :: (A, B) -> B) -> A[] -> B ~ alg <A, A, B>
// ana  (p :: B -> bool) (g :: B -> (A, B)) -> B -> A[] ~ b -> alg < A, B  >
//        (b -> Nil | Cons a b)

// ana (a -> f a) -> a -> Fix f

// f a ~ forall r. F<a, r> -> r
// g a ~ forall r. G<a, r> -> r
// f (g a) ~ f (forall r. G<a, r> -> r) ~ forall v. F<forall r. G<a, r> -> r, v> -> v

// Comonad d 
// counit :: D<a, a>, comult :: D<a, d (d a)>

// Functor f, Comonad d
//      dist :: f (d a) -> d (f a)
//          dist :: forall a. F<d a, d (f a)>

// ControlFlowGraph
//    Entry B
//  | 

//  DFS Tree?
//  Leaf Label, Successor visited