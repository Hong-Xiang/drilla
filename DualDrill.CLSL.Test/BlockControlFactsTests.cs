using System.CodeDom.Compiler;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Test;

public sealed class BlockControlFactsTests
{
    [Fact]
    public void LocalFactsMatchExistingAnalysisAcrossSupportedGraphShapes()
    {
        foreach (var graph in Graphs())
        {
            var baseline = graph.ControlFlowAnalysis();
            var annotated = ControlFlowFacts.Annotate(graph, PrintNothing);

            Assert.Same(graph.EntryLabel, annotated.EntryLabel);
            Assert.Equal(graph.Count, annotated.Count);
            Assert.Equal(graph.Labels(), annotated.Labels());
            foreach (var label in graph.Labels())
            {
                var row = annotated[label];
                Assert.Same(graph[label], row.Node);
                Assert.Same(graph.Successor(label), annotated.Successor(label));
                Assert.Equal(baseline.IndexOf(label), row.Annotation.ReversePostOrderIndex);
                Assert.Same(
                    baseline.DominatorTree.ImmediateDominator(label),
                    row.Annotation.ImmediateDominator);
                AssertPostDominance(
                    baseline.PostDominatorTree.ExitPostDominance(label),
                    row.Annotation.PostDominance);
                Assert.Equal(baseline.IsLoop(label), row.Annotation.IsLoopHeader);
            }
        }
    }

    [Fact]
    public void AnnotationPreservesSameTargetConditionalArmAndPayloadIdentity()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var conditional = new ConditionalSuccessor(target, target);
        var payload = new object();
        var graph = new ControlFlowGraph<object>(
            entry,
            new Dictionary<Label, ControlFlowGraph<object>.NodeDefinition>
            {
                [target] = new(new TerminateSuccessor(), new object()),
                [entry] = new(conditional, payload)
            });

        var annotated = ControlFlowFacts.Annotate(graph, PrintNothing);
        var preserved = Assert.IsType<ConditionalSuccessor>(annotated.Successor(entry));

        Assert.Same(payload, annotated[entry].Node);
        Assert.Same(conditional, preserved);
        Assert.Same(target, preserved.TrueTarget);
        Assert.Same(target, preserved.FalseTarget);
    }

    [Fact]
    public void AnnotationRejectsDisconnectedDefinitionsBeforeAnalysis()
    {
        var entry = Label.Create("entry");
        var unreachable = Label.Create("unreachable");
        var graph = new ControlFlowGraph<object>(
            entry,
            new Dictionary<Label, ControlFlowGraph<object>.NodeDefinition>
            {
                [entry] = new(new TerminateSuccessor(), new object()),
                [unreachable] = new(new UnconditionalSuccessor(entry), new object())
            });

        var exception = Assert.Throws<ArgumentException>(() =>
            ControlFlowFacts.Annotate(graph, PrintNothing));

        Assert.Contains("every graph definition to be reachable", exception.Message);
    }

    [Fact]
    public void RegionConstructionUsesPublishedLoopFactWithoutReanalysis()
    {
        var entry = Label.Create("entry");
        var body = new TerminateSuccessor();
        var graph = new ControlFlowGraph<Annotated<ISuccessor, BlockControlFacts>>(
            entry,
            new Dictionary<Label,
                ControlFlowGraph<Annotated<ISuccessor, BlockControlFacts>>.NodeDefinition>
            {
                [entry] = new(
                    body,
                    new Annotated<ISuccessor, BlockControlFacts>(
                        body,
                        new BlockControlFacts(
                            0,
                            null,
                            ExitPostDominance.NoExitPath.Instance,
                            [new IncomingControlArm(entry, 0, true)]),
                        PrintNothing))
            });

        var region = RegionTree.Create(graph, static (_, node, _) => node);

        Assert.Equal(RegionKind.Loop, region.Definition.Kind);
    }

    [Fact]
    public void RegionConstructionRejectsFactLabelsOutsideTheGraph()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var foreign = Label.Create("foreign");
        var graph = new ControlFlowGraph<Annotated<object, BlockControlFacts>>(
            entry,
            new Dictionary<Label,
                ControlFlowGraph<Annotated<object, BlockControlFacts>>.NodeDefinition>
            {
                [entry] = Definition(
                    new UnconditionalSuccessor(exit),
                    new BlockControlFacts(
                        0,
                        null,
                        new ExitPostDominance.Block(foreign, false),
                        [])),
                [exit] = Definition(
                    new TerminateSuccessor(),
                    new BlockControlFacts(
                        1,
                        entry,
                        new ExitPostDominance.FunctionExit(false),
                        [new IncomingControlArm(entry, 0, false)]))
            });

        var exception = Assert.Throws<ArgumentException>(() =>
            RegionTree.Create(graph, static (_, node, _) => node));

        Assert.Contains("postdominator outside the graph", exception.Message);
    }

    private static void AssertPostDominance(
        ExitPostDominance expected,
        ExitPostDominance actual)
    {
        Assert.Equal(expected.MayDiverge, actual.MayDiverge);
        switch (expected)
        {
            case ExitPostDominance.Block block:
                Assert.Same(block.Target, Assert.IsType<ExitPostDominance.Block>(actual).Target);
                break;
            case ExitPostDominance.FunctionExit:
                Assert.IsType<ExitPostDominance.FunctionExit>(actual);
                break;
            case ExitPostDominance.NoExitPath:
                Assert.Same(ExitPostDominance.NoExitPath.Instance, actual);
                break;
        }
    }

    private static IEnumerable<ControlFlowGraph<object>> Graphs()
    {
        var single = Label.Create("single");
        yield return Graph(single, (single, new TerminateSuccessor()));

        var self = Label.Create("self");
        yield return Graph(self, (self, new UnconditionalSuccessor(self)));

        var diamondEntry = Label.Create("diamond-entry");
        var whenTrue = Label.Create("diamond-true");
        var whenFalse = Label.Create("diamond-false");
        var join = Label.Create("diamond-join");
        yield return Graph(
            diamondEntry,
            (diamondEntry, new ConditionalSuccessor(whenTrue, whenFalse)),
            (whenTrue, new UnconditionalSuccessor(join)),
            (whenFalse, new UnconditionalSuccessor(join)),
            (join, new TerminateSuccessor()));

        var outer = Label.Create("outer");
        var inner = Label.Create("inner");
        var innerBody = Label.Create("inner-body");
        var outerBackedge = Label.Create("outer-backedge");
        var exit = Label.Create("exit");
        yield return Graph(
            outer,
            (outer, new ConditionalSuccessor(inner, exit)),
            (inner, new ConditionalSuccessor(innerBody, outerBackedge)),
            (innerBody, new UnconditionalSuccessor(inner)),
            (outerBackedge, new UnconditionalSuccessor(outer)),
            (exit, new TerminateSuccessor()));

        var returns = Label.Create("returns");
        var leftReturn = Label.Create("left-return");
        var rightReturn = Label.Create("right-return");
        yield return Graph(
            returns,
            (returns, new ConditionalSuccessor(leftReturn, rightReturn)),
            (leftReturn, new TerminateSuccessor()),
            (rightReturn, new TerminateSuccessor()));

        var cycleEntry = Label.Create("cycle-entry");
        var cycleLeft = Label.Create("cycle-left");
        var cycleRight = Label.Create("cycle-right");
        yield return Graph(
            cycleEntry,
            (cycleEntry, new ConditionalSuccessor(cycleLeft, cycleRight)),
            (cycleLeft, new UnconditionalSuccessor(cycleRight)),
            (cycleRight, new UnconditionalSuccessor(cycleLeft)));

        var noExitLeft = Label.Create("no-exit-left");
        var noExitRight = Label.Create("no-exit-right");
        yield return Graph(
            noExitLeft,
            (noExitLeft, new UnconditionalSuccessor(noExitRight)),
            (noExitRight, new UnconditionalSuccessor(noExitLeft)));
    }

    private static ControlFlowGraph<object> Graph(
        Label entry,
        params IEnumerable<(Label Label, ISuccessor Successor)> nodes) =>
        new(
            entry,
            nodes.ToDictionary(
                node => node.Label,
                node => new ControlFlowGraph<object>.NodeDefinition(
                    node.Successor,
                    new object())));

    private static ControlFlowGraph<Annotated<object, BlockControlFacts>>.NodeDefinition Definition(
        ISuccessor successor,
        BlockControlFacts facts) =>
        new(
            successor,
            new Annotated<object, BlockControlFacts>(
                new object(),
                facts,
                PrintNothing));

    private static void PrintNothing<TBody>(
        TBody body,
        BlockControlFacts facts,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
    }
}
