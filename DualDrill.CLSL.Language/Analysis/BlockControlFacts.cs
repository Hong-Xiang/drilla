using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Analysis;

public record struct IncomingControlArm(
    Label Source,
    int SuccessorIndex,
    bool IsBackedge);

public sealed record BlockControlFacts(
    int ReversePostOrderIndex,
    Label? ImmediateDominator,
    Label? ImmediatePostDominator,
    ImmutableArray<IncomingControlArm> IncomingArms)
{
    public bool IsLoopHeader => IncomingArms.Any(static arm => arm.IsBackedge);
}

public static class ControlFlowFacts
{
    public static ControlFlowGraph<Annotated<TBlock, BlockControlFacts>> Annotate<TBlock>(
        ControlFlowGraph<TBlock> graph,
        Action<TBlock, BlockControlFacts, IndentedTextWriter, PrettyPrintOption> blockPrettyPrint)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(blockPrettyPrint);

        var analysis = graph.ControlFlowAnalysis();
        var labels = analysis.Labels;
        var incomingArms = labels.ToDictionary(
            static label => label,
            static _ => ImmutableArray.CreateBuilder<IncomingControlArm>());
        foreach (var source in labels)
        {
            var successorIndex = 0;
            foreach (var target in graph.Successor(source).AllTargets())
            {
                incomingArms[target].Add(new IncomingControlArm(
                    source,
                    successorIndex,
                    analysis.DominatorTree.Dominators(source).Contains(target)));
                successorIndex++;
            }
        }

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
                        incomingArms[label].ToImmutable()),
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
