using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Analysis;

public sealed record BlockControlFacts(
    int ReversePostOrderIndex,
    Label? ImmediateDominator,
    Label? ImmediatePostDominator,
    bool IsLoopHeader);

public static class ControlFlowFacts
{
    public static ControlFlowGraph<Annotated<TBlock, BlockControlFacts>> Annotate<TBlock>(
        ControlFlowGraph<TBlock> graph,
        Action<TBlock, BlockControlFacts, IndentedTextWriter, PrettyPrintOption> blockPrettyPrint)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(blockPrettyPrint);

        var labels = graph.Labels().ToImmutableArray();
        if (labels.Length != graph.Count)
            throw new ArgumentException(
                "Control facts require every graph definition to be reachable from the entry.",
                nameof(graph));

        var analysis = graph.ControlFlowAnalysis();
        var definitions = labels.ToDictionary(
            label => label,
            label => new ControlFlowGraph<Annotated<TBlock, BlockControlFacts>>.NodeDefinition(
                graph.Successor(label),
                new Annotated<TBlock, BlockControlFacts>(
                    graph[label],
                    new BlockControlFacts(
                        analysis.IndexOf(label),
                        analysis.DominatorTree.ImmediateDominator(label),
                        analysis.PostDominatorTree.ImmediatePostDominator(label),
                        analysis.IsLoop(label)),
                    blockPrettyPrint)));

        return new ControlFlowGraph<Annotated<TBlock, BlockControlFacts>>(
            graph.EntryLabel,
            definitions,
            PrettyPrint);
    }

    private static void PrettyPrint<TBlock>(
        ControlFlowGraph<Annotated<TBlock, BlockControlFacts>> graph,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        writer.WriteLine("control-facts-cfg");
        foreach (var label in graph.Labels())
            graph[label].PrettyPrint(writer, option);
    }
}
