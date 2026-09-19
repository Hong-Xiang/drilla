using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class IncomingControlArmTests(ITestOutputHelper output)
{
    [Fact]
    public void DiamondHasTwoOrderedForwardJoinArms() => AssertFixture(Diamond());

    [Fact]
    public void SharedTailHasExpectedLocalFacts() => AssertFixture(SharedTail());

    [Fact]
    public void SelfLoopHasOneBackedge() => AssertFixture(SelfLoop());

    [Fact]
    public void NestedLoopsHaveInnerAndOuterBackedges() => AssertFixture(NestedLoops());

    [Fact]
    public void EarlyReturnLoopExitHasOnlyHeaderBackedge() => AssertFixture(EarlyReturnLoopExit());

    [Fact]
    public void MultipleReturnsHaveNoLoopHeaders() => AssertFixture(MultipleReturns());

    [Fact]
    public void NoExitTwoNodeLoopHasOneBackedge() => AssertFixture(NoExitLoop());

    [Fact]
    public void ParallelSameTargetArmsRemainDistinct() => AssertFixture(ParallelArms());

    [Fact]
    public void IrreducibleCycleArmsAreForwardDespiteReversePostOrder()
    {
        var fixture = Irreducible();

        AssertFixture(fixture);

        var annotated = ControlFlowFacts.Annotate(fixture.Graph, PrintNothing);
        var a = fixture.Blocks.Single(block => block.Label.Name == "a").Label;
        var b = fixture.Blocks.Single(block => block.Label.Name == "b").Label;
        Assert.True(annotated[b].Annotation.ReversePostOrderIndex >
                    annotated[a].Annotation.ReversePostOrderIndex);
        Assert.Contains(
            new IncomingControlArm(b, 0, false),
            annotated[a].Annotation.IncomingArms);
    }

    [Fact]
    public void ParallelSelfArmsRemainDistinctBackedges()
    {
        var entry = Label.Create("entry");
        var graph = Graph(
            entry,
            (entry, new ConditionalSuccessor(entry, entry)));

        var facts = ControlFlowFacts.Annotate(graph, PrintNothing)[entry].Annotation;

        Assert.Equal<IncomingControlArm>(
            [
                new IncomingControlArm(entry, 0, true),
                new IncomingControlArm(entry, 1, true)
            ],
            facts.IncomingArms);
        Assert.True(facts.IsLoopHeader);
    }

    [Fact]
    public void EqualLabelNamesDoNotCollapseIdentity()
    {
        var entry = Label.Create("same");
        var target = Label.Create("same");
        var graph = Graph(
            entry,
            (entry, new UnconditionalSuccessor(target)),
            (target, new TerminateSuccessor()));

        var annotated = ControlFlowFacts.Annotate(graph, PrintNothing);

        Assert.NotSame(entry, target);
        Assert.Same(entry, annotated[target].Annotation.ImmediateDominator);
        var arm = Assert.Single(annotated[target].Annotation.IncomingArms);
        Assert.Same(entry, arm.Source);
        Assert.Equal(0, arm.SuccessorIndex);
        Assert.False(arm.IsBackedge);
    }

    [Fact]
    public void DisconnectedDefinitionIsRejectedAtSharedAnalysisBoundary()
    {
        var entry = Label.Create("entry");
        var unreachable = Label.Create("unreachable");
        var graph = Graph(
            entry,
            (entry, new TerminateSuccessor()),
            (unreachable, new UnconditionalSuccessor(entry)));

        var analysisException = Assert.Throws<ArgumentException>(() => graph.ControlFlowAnalysis());
        var annotationException = Assert.Throws<ArgumentException>(() =>
            ControlFlowFacts.Annotate(graph, PrintNothing));

        Assert.Equal("controlFlowGraph", analysisException.ParamName);
        Assert.Contains("every graph definition to be reachable", analysisException.Message);
        Assert.Equal(analysisException.Message, annotationException.Message);
    }

    [Fact]
    public void SmallReachableGraphsMatchIndependentDominanceOracle()
    {
        var graphCount = 0;
        foreach (var graphCase in SmallControlFlowGraphCorpus.Cases())
        {
            AssertGraphLaws(graphCase.Successors, graphCase.Code);
            graphCount++;
        }

        Assert.Equal(SmallControlFlowGraphCorpus.ExpectedCount, graphCount);
    }

    [Fact]
    public void CorpusPrettyPrintShowsGenericCfgAndC1Facts()
    {
        var report = new StringBuilder();
        foreach (var fixture in Fixtures())
        {
            report.AppendLine($"=== {fixture.Name}: generic CFG ===");
            report.Append(fixture.Graph.PrettyPrint());
            report.AppendLine($"=== {fixture.Name}: C1 local facts ===");

            var annotated = ControlFlowFacts.Annotate(fixture.Graph, PrintNothing);
            var labels = annotated.Labels().ToImmutableArray();
            var ids = labels.Select((label, index) => (label, index))
                            .ToDictionary(item => item.label, item => item.index);
            foreach (var label in labels)
            {
                var facts = annotated[label].Annotation;
                report.Append(LabelName(label, ids));
                report.Append(" { rpo=");
                report.Append(facts.ReversePostOrderIndex.ToString(CultureInfo.InvariantCulture));
                report.Append(" idom=");
                report.Append(facts.ImmediateDominator is { } dominator
                    ? LabelName(dominator, ids)
                    : "none");
                report.Append(" incoming=[");
                report.Append(string.Join(
                    ",",
                    facts.IncomingArms.Select(arm =>
                        $"{LabelName(arm.Source, ids)}[{arm.SuccessorIndex.ToString(CultureInfo.InvariantCulture)}]:" +
                        (arm.IsBackedge ? "backedge" : "forward"))));
                report.Append("] loop-header=");
                report.AppendLine(facts.IsLoopHeader ? "true }" : "false }");
            }
        }

        var text = report.ToString();
        output.WriteLine(text);

        Assert.Contains("cfg generic (payload rendering: ToString)", text);
        Assert.Contains("incoming=[^0(e)[0]:forward,^0(e)[1]:forward]", text);
        Assert.Contains("incoming=[^3(l)[0]:backedge]", text);
        Assert.DoesNotContain("ipdom", text, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertFixture(ControlArmFixture fixture)
    {
        var annotated = ControlFlowFacts.Annotate(fixture.Graph, PrintNothing);

        Assert.Same(fixture.Graph.EntryLabel, annotated.EntryLabel);
        Assert.Equal<Label>(
            fixture.Blocks.Select(static block => block.Label),
            annotated.Labels());
        foreach (var expected in fixture.Blocks)
        {
            var facts = annotated[expected.Label].Annotation;
            Assert.Same(fixture.Graph[expected.Label], annotated[expected.Label].Node);
            Assert.Same(expected.Successor, annotated.Successor(expected.Label));
            Assert.Equal(expected.ReversePostOrderIndex, facts.ReversePostOrderIndex);
            if (expected.ImmediateDominator is null)
                Assert.Null(facts.ImmediateDominator);
            else
                Assert.Same(expected.ImmediateDominator, facts.ImmediateDominator);
            Assert.Equal<IncomingControlArm>(expected.IncomingArms, facts.IncomingArms);
            Assert.Equal(expected.IsLoopHeader, facts.IsLoopHeader);
            Assert.Equal(
                facts.IncomingArms.Any(static arm => arm.IsBackedge),
                facts.IsLoopHeader);
        }
    }

    private static void AssertGraphLaws(ImmutableArray<ImmutableArray<int>> successors, int code)
    {
        var labels = Enumerable.Range(0, successors.Length)
                               .Select(index => Label.Create($"n{index}"))
                               .ToImmutableArray();
        var labelIndices = labels.Select((label, index) => (label, index))
                                 .ToDictionary(item => item.label, item => item.index);
        var definitions = labels.Select((label, index) =>
                KeyValuePair.Create(
                    label,
                    new ControlFlowGraph<int>.NodeDefinition(
                        ToSuccessor(successors[index], labels),
                        index)))
                                .ToDictionary();
        var graph = new ControlFlowGraph<int>(labels[0], definitions);
        var annotated = ControlFlowFacts.Annotate(graph, PrintNothing);
        var labelsByRpo = labels.OrderBy(label =>
                                    annotated[label].Annotation.ReversePostOrderIndex)
                                .ToImmutableArray();
        var expectedIncoming = labels.ToDictionary(
            static label => label,
            static _ => new List<IncomingControlArm>());

        foreach (var source in labelsByRpo)
        {
            var sourceIndex = labelIndices[source];
            for (var successorIndex = 0;
                 successorIndex < successors[sourceIndex].Length;
                 successorIndex++)
            {
                var targetIndex = successors[sourceIndex][successorIndex];
                expectedIncoming[labels[targetIndex]].Add(new IncomingControlArm(
                    source,
                    successorIndex,
                    Dominates(successors, targetIndex, sourceIndex)));
            }
        }

        Assert.Equal(
            successors.Sum(static arms => arms.Length),
            labels.Sum(label => annotated[label].Annotation.IncomingArms.Length));
        for (var nodeIndex = 0; nodeIndex < labels.Length; nodeIndex++)
        {
            var label = labels[nodeIndex];
            var facts = annotated[label].Annotation;
            Assert.Equal<IncomingControlArm>(expectedIncoming[label], facts.IncomingArms);
            Assert.Equal(
                expectedIncoming[label].Any(static arm => arm.IsBackedge),
                facts.IsLoopHeader);

            var expectedDominators = labels.Where((_, candidateIndex) =>
                                                Dominates(successors, candidateIndex, nodeIndex))
                                           .ToHashSet();
            var actualDominators = new HashSet<Label>();
            Label? current = label;
            while (current is not null)
            {
                Assert.True(
                    actualDominators.Add(current),
                    $"IDom cycle for graph {successors.Length}:{code}, node {nodeIndex}.");
                current = annotated[current].Annotation.ImmediateDominator;
            }

            Assert.True(
                expectedDominators.SetEquals(actualDominators),
                $"Dominators differ for graph {successors.Length}:{code}, node {nodeIndex}.");
        }
    }

    private static bool Dominates(
        ImmutableArray<ImmutableArray<int>> successors,
        int candidate,
        int node) =>
        candidate == node || !IsReachable(successors, node, candidate);

    private static bool IsReachable(
        ImmutableArray<ImmutableArray<int>> successors,
        int target,
        int? removed)
    {
        if (removed == 0)
            return false;

        HashSet<int> visited = [0];
        Queue<int> pending = new([0]);
        while (pending.TryDequeue(out var source))
        {
            if (source == target)
                return true;

            foreach (var successor in successors[source])
                if (successor != removed && visited.Add(successor))
                    pending.Enqueue(successor);
        }

        return false;
    }

    private static ISuccessor ToSuccessor(
        ImmutableArray<int> successors,
        ImmutableArray<Label> labels) =>
        successors switch
        {
            [] => new TerminateSuccessor(),
            [var target] => new UnconditionalSuccessor(labels[target]),
            [var trueTarget, var falseTarget] =>
                new ConditionalSuccessor(labels[trueTarget], labels[falseTarget]),
            _ => throw new InvalidOperationException("The test corpus only supports zero, one, or two arms.")
        };

    private static IEnumerable<ControlArmFixture> Fixtures()
    {
        yield return Diamond();
        yield return SharedTail();
        yield return SelfLoop();
        yield return NestedLoops();
        yield return EarlyReturnLoopExit();
        yield return MultipleReturns();
        yield return NoExitLoop();
        yield return ParallelArms();
        yield return Irreducible();
    }

    private static ControlArmFixture Diamond()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var r = Label.Create("r");
        var j = Label.Create("j");
        return Fixture(
            "diamond",
            e,
            Block(e, new ConditionalSuccessor(l, r), 0, null, false),
            Block(r, new UnconditionalSuccessor(j), 1, e, false, Arm(e, 1)),
            Block(l, new UnconditionalSuccessor(j), 2, e, false, Arm(e, 0)),
            Block(j, new TerminateSuccessor(), 3, e, false, Arm(r, 0), Arm(l, 0)));
    }

    private static ControlArmFixture SharedTail()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var r = Label.Create("r");
        var j = Label.Create("j");
        var t = Label.Create("t");
        return Fixture(
            "shared-tail",
            e,
            Block(e, new ConditionalSuccessor(l, r), 0, null, false),
            Block(r, new UnconditionalSuccessor(j), 1, e, false, Arm(e, 1)),
            Block(l, new UnconditionalSuccessor(j), 2, e, false, Arm(e, 0)),
            Block(j, new UnconditionalSuccessor(t), 3, e, false, Arm(r, 0), Arm(l, 0)),
            Block(t, new TerminateSuccessor(), 4, j, false, Arm(j, 0)));
    }

    private static ControlArmFixture SelfLoop()
    {
        var e = Label.Create("e");
        return Fixture(
            "self-loop",
            e,
            Block(e, new UnconditionalSuccessor(e), 0, null, true, Arm(e, 0, true)));
    }

    private static ControlArmFixture NestedLoops()
    {
        var o = Label.Create("o");
        var i = Label.Create("i");
        var b = Label.Create("b");
        var l = Label.Create("l");
        var x = Label.Create("x");
        return Fixture(
            "nested-loops",
            o,
            Block(o, new ConditionalSuccessor(i, x), 0, null, true, Arm(l, 0, true)),
            Block(x, new TerminateSuccessor(), 1, o, false, Arm(o, 1)),
            Block(i, new ConditionalSuccessor(b, l), 2, o, true, Arm(o, 0), Arm(b, 0, true)),
            Block(l, new UnconditionalSuccessor(o), 3, i, false, Arm(i, 1)),
            Block(b, new UnconditionalSuccessor(i), 4, i, false, Arm(i, 0)));
    }

    private static ControlArmFixture EarlyReturnLoopExit()
    {
        var h = Label.Create("h");
        var b = Label.Create("b");
        var x = Label.Create("x");
        var r = Label.Create("r");
        var l = Label.Create("l");
        return Fixture(
            "early-return-loop-exit",
            h,
            Block(h, new ConditionalSuccessor(b, x), 0, null, true, Arm(l, 0, true)),
            Block(x, new TerminateSuccessor(), 1, h, false, Arm(h, 1)),
            Block(b, new ConditionalSuccessor(r, l), 2, h, false, Arm(h, 0)),
            Block(l, new UnconditionalSuccessor(h), 3, b, false, Arm(b, 1)),
            Block(r, new TerminateSuccessor(), 4, b, false, Arm(b, 0)));
    }

    private static ControlArmFixture MultipleReturns()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var r = Label.Create("r");
        return Fixture(
            "multiple-returns",
            e,
            Block(e, new ConditionalSuccessor(l, r), 0, null, false),
            Block(r, new TerminateSuccessor(), 1, e, false, Arm(e, 1)),
            Block(l, new TerminateSuccessor(), 2, e, false, Arm(e, 0)));
    }

    private static ControlArmFixture NoExitLoop()
    {
        var a = Label.Create("a");
        var b = Label.Create("b");
        return Fixture(
            "no-exit-loop",
            a,
            Block(a, new UnconditionalSuccessor(b), 0, null, true, Arm(b, 0, true)),
            Block(b, new UnconditionalSuccessor(a), 1, a, false, Arm(a, 0)));
    }

    private static ControlArmFixture ParallelArms()
    {
        var e = Label.Create("e");
        var j = Label.Create("j");
        return Fixture(
            "parallel-arms",
            e,
            Block(e, new ConditionalSuccessor(j, j), 0, null, false),
            Block(j, new TerminateSuccessor(), 1, e, false, Arm(e, 0), Arm(e, 1)));
    }

    private static ControlArmFixture Irreducible()
    {
        var e = Label.Create("e");
        var a = Label.Create("a");
        var b = Label.Create("b");
        return Fixture(
            "irreducible",
            e,
            Block(e, new ConditionalSuccessor(a, b), 0, null, false),
            Block(a, new UnconditionalSuccessor(b), 1, e, false, Arm(e, 0), Arm(b, 0)),
            Block(b, new UnconditionalSuccessor(a), 2, e, false, Arm(e, 1), Arm(a, 0)));
    }

    private static ControlArmFixture Fixture(
        string name,
        Label entry,
        params ExpectedBlock[] blocks)
    {
        var definitions = blocks.ToDictionary(
            static block => block.Label,
            static block => new ControlFlowGraph<string>.NodeDefinition(
                block.Successor,
                block.Label.Name ?? "<unnamed>"));
        return new ControlArmFixture(name, new ControlFlowGraph<string>(entry, definitions), [.. blocks]);
    }

    private static ExpectedBlock Block(
        Label label,
        ISuccessor successor,
        int reversePostOrderIndex,
        Label? immediateDominator,
        bool isLoopHeader,
        params IncomingControlArm[] incomingArms) =>
        new(
            label,
            successor,
            reversePostOrderIndex,
            immediateDominator,
            isLoopHeader,
            [.. incomingArms]);

    private static IncomingControlArm Arm(
        Label source,
        int successorIndex,
        bool isBackedge = false) =>
        new(source, successorIndex, isBackedge);

    private static ControlFlowGraph<object> Graph(
        Label entry,
        params IEnumerable<(Label Label, ISuccessor Successor)> nodes) =>
        new(
            entry,
            nodes.ToDictionary(
                static node => node.Label,
                static node => new ControlFlowGraph<object>.NodeDefinition(
                    node.Successor,
                    new object())));

    private static string LabelName(Label label, IReadOnlyDictionary<Label, int> ids) =>
        $"^{ids[label].ToString(CultureInfo.InvariantCulture)}({label.Name ?? "<unnamed>"})";

    private static void PrintNothing<TBody>(
        TBody body,
        BlockControlFacts facts,
        System.CodeDom.Compiler.IndentedTextWriter writer,
        PrettyPrintOption option)
    {
    }

    private sealed record ControlArmFixture(
        string Name,
        ControlFlowGraph<string> Graph,
        ImmutableArray<ExpectedBlock> Blocks);

    private sealed record ExpectedBlock(
        Label Label,
        ISuccessor Successor,
        int ReversePostOrderIndex,
        Label? ImmediateDominator,
        bool IsLoopHeader,
        ImmutableArray<IncomingControlArm> IncomingArms);
}
