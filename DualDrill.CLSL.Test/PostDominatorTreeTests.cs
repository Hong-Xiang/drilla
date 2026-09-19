using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class PostDominatorTreeTests(ITestOutputHelper output)
{
    [Fact]
    public void DiamondHasSharedFiniteExitPostDominator()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var r = Label.Create("r");
        var j = Label.Create("j");
        AssertFacts(
            nameof(DiamondHasSharedFiniteExitPostDominator),
            Graph(
                e,
                (e, new ConditionalSuccessor(l, r)),
                (l, new UnconditionalSuccessor(j)),
                (r, new UnconditionalSuccessor(j)),
                (j, new TerminateSuccessor())),
            (e, Block(j, false)),
            (l, Block(j, false)),
            (r, Block(j, false)),
            (j, FunctionExit(false)));
    }

    [Fact]
    public void SharedTailHasNearestRealPostDominators()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var r = Label.Create("r");
        var j = Label.Create("j");
        var t = Label.Create("t");
        AssertFacts(
            nameof(SharedTailHasNearestRealPostDominators),
            Graph(
                e,
                (e, new ConditionalSuccessor(l, r)),
                (l, new UnconditionalSuccessor(j)),
                (r, new UnconditionalSuccessor(j)),
                (j, new UnconditionalSuccessor(t)),
                (t, new TerminateSuccessor())),
            (e, Block(j, false)),
            (l, Block(j, false)),
            (r, Block(j, false)),
            (j, Block(t, false)),
            (t, FunctionExit(false)));
    }

    [Fact]
    public void SelfLoopHasNoExitPath()
    {
        var e = Label.Create("e");
        AssertFacts(
            nameof(SelfLoopHasNoExitPath),
            Graph(e, (e, new UnconditionalSuccessor(e))),
            (e, NoExitPath()));
    }

    [Fact]
    public void LegacySingleTerminalEndsAtFunctionExit()
    {
        var e = Label.Create("e");
        AssertFacts(
            nameof(LegacySingleTerminalEndsAtFunctionExit),
            Graph(e, (e, new TerminateSuccessor())),
            (e, FunctionExit(false)));
    }

    [Fact]
    public void LegacySelfLoopWithExitHasFiniteExitPostDominatorAndMayDiverge()
    {
        var a = Label.Create("a");
        var b = Label.Create("b");
        AssertFacts(
            nameof(LegacySelfLoopWithExitHasFiniteExitPostDominatorAndMayDiverge),
            Graph(
                a,
                (a, new ConditionalSuccessor(a, b)),
                (b, new TerminateSuccessor())),
            (a, Block(b, true)),
            (b, FunctionExit(false)));
    }

    [Fact]
    public void LegacyMinimumLoopRetainsExactFiveNodeTopology()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var c = Label.Create("c");
        var b = Label.Create("b");
        var t = Label.Create("t");
        AssertFacts(
            nameof(LegacyMinimumLoopRetainsExactFiveNodeTopology),
            Graph(
                e,
                (e, new UnconditionalSuccessor(l)),
                (l, new ConditionalSuccessor(c, b)),
                (c, new UnconditionalSuccessor(l)),
                (b, new UnconditionalSuccessor(t)),
                (t, new TerminateSuccessor())),
            (e, Block(l, true)),
            (l, Block(b, true)),
            (c, Block(l, true)),
            (b, Block(t, false)),
            (t, FunctionExit(false)));
    }

    [Fact]
    public void LegacyMergedLoopBreakRetainsExactFiveNodeTopology()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var c = Label.Create("c");
        var b = Label.Create("b");
        var t = Label.Create("t");
        AssertFacts(
            nameof(LegacyMergedLoopBreakRetainsExactFiveNodeTopology),
            Graph(
                e,
                (e, new ConditionalSuccessor(l, t)),
                (l, new ConditionalSuccessor(c, b)),
                (c, new UnconditionalSuccessor(l)),
                (b, new UnconditionalSuccessor(t)),
                (t, new TerminateSuccessor())),
            (e, Block(t, true)),
            (l, Block(b, true)),
            (c, Block(l, true)),
            (b, Block(t, false)),
            (t, FunctionExit(false)));
    }

    [Fact]
    public void LegacyNestedLoopRetainsExactSevenNodeTopology()
    {
        var e = Label.Create("e");
        var l1 = Label.Create("l1");
        var b2 = Label.Create("b2");
        var l2 = Label.Create("l2");
        var b4 = Label.Create("b4");
        var b5 = Label.Create("b5");
        var b6 = Label.Create("b6");
        AssertFacts(
            nameof(LegacyNestedLoopRetainsExactSevenNodeTopology),
            Graph(
                e,
                (e, new UnconditionalSuccessor(l1)),
                (l1, new ConditionalSuccessor(b2, b6)),
                (b2, new UnconditionalSuccessor(l2)),
                (l2, new ConditionalSuccessor(b4, b5)),
                (b4, new UnconditionalSuccessor(l2)),
                (b5, new UnconditionalSuccessor(l1)),
                (b6, new TerminateSuccessor())),
            (e, Block(l1, true)),
            (l1, Block(b6, true)),
            (b2, Block(l2, true)),
            (l2, Block(b5, true)),
            (b4, Block(l2, true)),
            (b5, Block(l1, true)),
            (b6, FunctionExit(false)));
    }

    [Fact]
    public void LegacyComplexControlFlowRetainsExactSixNodeTopology()
    {
        var a = Label.Create("a");
        var b = Label.Create("b");
        var c = Label.Create("c");
        var d = Label.Create("d");
        var e = Label.Create("e");
        var f = Label.Create("f");
        AssertFacts(
            nameof(LegacyComplexControlFlowRetainsExactSixNodeTopology),
            Graph(
                a,
                (a, new ConditionalSuccessor(d, b)),
                (b, new ConditionalSuccessor(e, c)),
                (c, new UnconditionalSuccessor(f)),
                (d, new ConditionalSuccessor(f, e)),
                (e, new UnconditionalSuccessor(f)),
                (f, new TerminateSuccessor())),
            (a, Block(f, false)),
            (b, Block(f, false)),
            (c, Block(f, false)),
            (d, Block(f, false)),
            (e, Block(f, false)),
            (f, FunctionExit(false)));
    }

    [Fact]
    public void LegacyComplexControlFlowChainRetainsExactSixNodeTopology()
    {
        var a = Label.Create("a");
        var b = Label.Create("b");
        var c = Label.Create("c");
        var d = Label.Create("d");
        var e = Label.Create("e");
        var f = Label.Create("f");
        AssertFacts(
            nameof(LegacyComplexControlFlowChainRetainsExactSixNodeTopology),
            Graph(
                a,
                (a, new ConditionalSuccessor(d, b)),
                (b, new UnconditionalSuccessor(c)),
                (c, new UnconditionalSuccessor(f)),
                (d, new ConditionalSuccessor(f, e)),
                (e, new UnconditionalSuccessor(f)),
                (f, new TerminateSuccessor())),
            (a, Block(f, false)),
            (b, Block(c, false)),
            (c, Block(f, false)),
            (d, Block(f, false)),
            (e, Block(f, false)),
            (f, FunctionExit(false)));
    }

    [Fact]
    public void NestedLoopsRetainFiniteExitChainAndMayDiverge()
    {
        var o = Label.Create("o");
        var i = Label.Create("i");
        var b = Label.Create("b");
        var l = Label.Create("l");
        var x = Label.Create("x");
        AssertFacts(
            nameof(NestedLoopsRetainFiniteExitChainAndMayDiverge),
            Graph(
                o,
                (o, new ConditionalSuccessor(i, x)),
                (i, new ConditionalSuccessor(b, l)),
                (b, new UnconditionalSuccessor(i)),
                (l, new UnconditionalSuccessor(o)),
                (x, new TerminateSuccessor())),
            (o, Block(x, true)),
            (i, Block(l, true)),
            (b, Block(i, true)),
            (l, Block(o, true)),
            (x, FunctionExit(false)));
    }

    [Fact]
    public void EarlyReturnAndLoopExitMeetAtFunctionExit()
    {
        var h = Label.Create("h");
        var b = Label.Create("b");
        var x = Label.Create("x");
        var r = Label.Create("r");
        var l = Label.Create("l");
        AssertFacts(
            nameof(EarlyReturnAndLoopExitMeetAtFunctionExit),
            Graph(
                h,
                (h, new ConditionalSuccessor(b, x)),
                (b, new ConditionalSuccessor(r, l)),
                (l, new UnconditionalSuccessor(h)),
                (r, new TerminateSuccessor()),
                (x, new TerminateSuccessor())),
            (h, FunctionExit(true)),
            (b, FunctionExit(true)),
            (l, Block(h, true)),
            (r, FunctionExit(false)),
            (x, FunctionExit(false)));
    }

    [Fact]
    public void MultipleReturnsMeetAtFunctionExit()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var r = Label.Create("r");
        AssertFacts(
            nameof(MultipleReturnsMeetAtFunctionExit),
            Graph(
                e,
                (e, new ConditionalSuccessor(l, r)),
                (l, new TerminateSuccessor()),
                (r, new TerminateSuccessor())),
            (e, FunctionExit(false)),
            (l, FunctionExit(false)),
            (r, FunctionExit(false)));
    }

    [Fact]
    public void TwoNodeLoopHasNoExitPath()
    {
        var a = Label.Create("a");
        var b = Label.Create("b");
        AssertFacts(
            nameof(TwoNodeLoopHasNoExitPath),
            Graph(
                a,
                (a, new UnconditionalSuccessor(b)),
                (b, new UnconditionalSuccessor(a))),
            (a, NoExitPath()),
            (b, NoExitPath()));
    }

    [Fact]
    public void ParallelArmsDoNotCreateFalseDivergence()
    {
        var e = Label.Create("e");
        var j = Label.Create("j");
        AssertFacts(
            nameof(ParallelArmsDoNotCreateFalseDivergence),
            Graph(
                e,
                (e, new ConditionalSuccessor(j, j)),
                (j, new TerminateSuccessor())),
            (e, Block(j, false)),
            (j, FunctionExit(false)));
    }

    [Fact]
    public void MixedReturnAndDivergenceUsesOnlyFiniteExitPathsForPostDominance()
    {
        var e = Label.Create("e");
        var r = Label.Create("r");
        var s = Label.Create("s");
        AssertFacts(
            nameof(MixedReturnAndDivergenceUsesOnlyFiniteExitPathsForPostDominance),
            Graph(
                e,
                (e, new ConditionalSuccessor(r, s)),
                (r, new TerminateSuccessor()),
                (s, new UnconditionalSuccessor(s))),
            (e, Block(r, true)),
            (r, FunctionExit(false)),
            (s, NoExitPath()));
    }

    [Fact]
    public void IrreducibleCycleHasNoExitPath()
    {
        var e = Label.Create("e");
        var a = Label.Create("a");
        var b = Label.Create("b");
        AssertFacts(
            nameof(IrreducibleCycleHasNoExitPath),
            Graph(
                e,
                (e, new ConditionalSuccessor(a, b)),
                (a, new UnconditionalSuccessor(b)),
                (b, new UnconditionalSuccessor(a))),
            (e, NoExitPath()),
            (a, NoExitPath()),
            (b, NoExitPath()));
    }

    [Fact]
    public void DivergingBranchCanStillShareARealFiniteExitTail()
    {
        var e = Label.Create("e");
        var l = Label.Create("l");
        var j = Label.Create("j");
        AssertFacts(
            nameof(DivergingBranchCanStillShareARealFiniteExitTail),
            Graph(
                e,
                (e, new ConditionalSuccessor(l, j)),
                (l, new ConditionalSuccessor(l, j)),
                (j, new TerminateSuccessor())),
            (e, Block(j, true)),
            (l, Block(j, true)),
            (j, FunctionExit(false)));
    }

    [Fact]
    public void EqualNamesRetainLabelIdentity()
    {
        var entry = Label.Create("same");
        var exit = Label.Create("same");
        var tree = Graph(
                entry,
                (entry, new UnconditionalSuccessor(exit)),
                (exit, new TerminateSuccessor()))
            .ControlFlowAnalysis()
            .PostDominatorTree;

        var block = Assert.IsType<ExitPostDominance.Block>(tree.ExitPostDominance(entry));

        Assert.NotSame(entry, exit);
        Assert.Same(exit, block.Target);
    }

    [Fact]
    public void UnknownLabelQueryThrows()
    {
        var entry = Label.Create("entry");
        var tree = Graph(entry, (entry, new TerminateSuccessor()))
            .ControlFlowAnalysis()
            .PostDominatorTree;

        Assert.Throws<KeyNotFoundException>(() =>
        {
            _ = tree.ExitPostDominance(Label.Create("unknown"));
        });
    }

    [Fact]
    public void BlockRejectsNullTarget()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExitPostDominance.Block(null!, false));
    }

    [Fact]
    public void SmallReachableGraphsMatchIndependentFinitePathAndCycleOracles()
    {
        var graphCount = 0;
        foreach (var graphCase in SmallControlFlowGraphCorpus.Cases())
        {
            AssertGraphLaws(graphCase);
            graphCount++;
        }

        Assert.Equal(SmallControlFlowGraphCorpus.ExpectedCount, graphCount);
    }

    [Fact]
    public void CorpusPrettyPrintShowsFiniteExitPostDominanceAndDivergence()
    {
        var report = new StringBuilder();
        foreach (var example in Corpus())
        {
            report.AppendLine($"=== {example.Name}: original CFG ===");
            report.Append(example.Graph.PrettyPrint());
            report.AppendLine($"=== {example.Name}: C2 finite-exit postdominance ===");
            var labels = example.Graph.Labels().ToImmutableArray();
            var ids = labels.Select((label, index) => (label, index))
                            .ToDictionary(item => item.label, item => item.index);
            var tree = example.Graph.ControlFlowAnalysis().PostDominatorTree;
            foreach (var label in labels)
            {
                var postDominance = tree.ExitPostDominance(label);
                report.Append(LabelName(label, ids));
                report.Append(" -> ");
                report.Append(postDominance switch
                {
                    ExitPostDominance.Block block => LabelName(block.Target, ids),
                    ExitPostDominance.FunctionExit => "function-exit",
                    ExitPostDominance.NoExitPath => "no-exit-path",
                    _ => throw new InvalidOperationException()
                });
                report.AppendLine(postDominance.MayDiverge ? " may-diverge" : " finite");
            }
        }

        var text = report.ToString();
        output.WriteLine(text);

        Assert.Contains("=== mixed-return-divergence: original CFG ===", text);
        Assert.Contains("^0(e) -> ^2(r) may-diverge", text);
        Assert.Contains("^1(s) -> no-exit-path may-diverge", text);
        Assert.Contains("function-exit finite", text);
    }

    private static void AssertGraphLaws(SmallControlFlowGraphCorpus.GraphCase graphCase)
    {
        var successors = graphCase.Successors;
        var labels = Enumerable.Range(0, successors.Length)
                               .Select(index => Label.Create($"n{index}"))
                               .ToImmutableArray();
        var graph = Graph(
            labels[0],
            labels.Select((label, index) =>
                (label, ToSuccessor(successors[index], labels))).ToArray());
        var tree = graph.ControlFlowAnalysis().PostDominatorTree;

        for (var node = 0; node < labels.Length; node++)
        {
            var canReachExit = CanReachTerminalAvoiding(successors, node, null);
            var expectedMayDiverge = CanReachCycle(successors, node);
            var actual = tree.ExitPostDominance(labels[node]);

            Assert.Equal(
                expectedMayDiverge,
                actual.MayDiverge);
            if (!canReachExit)
            {
                Assert.IsType<ExitPostDominance.NoExitPath>(actual);
                continue;
            }

            var strictPostDominators = Enumerable.Range(0, labels.Length)
                .Where(candidate =>
                    candidate != node &&
                    !CanReachTerminalAvoiding(successors, node, candidate))
                .ToArray();
            var expectedImmediate = strictPostDominators
                .SingleOrDefault(
                    candidate => strictPostDominators
                        .Where(other => other != candidate)
                        .All(other => !CanReachTerminalAvoiding(successors, candidate, other)),
                    -1);

            if (expectedImmediate < 0)
                Assert.IsType<ExitPostDominance.FunctionExit>(actual);
            else
                Assert.Same(
                    labels[expectedImmediate],
                    Assert.IsType<ExitPostDominance.Block>(actual).Target);

            HashSet<Label> chain = [];
            var current = labels[node];
            while (true)
            {
                Assert.True(
                    chain.Add(current),
                    $"Postdominator cycle for graph {successors.Length}:{graphCase.Code}, node {node}.");
                switch (tree.ExitPostDominance(current))
                {
                    case ExitPostDominance.Block block:
                        current = block.Target;
                        continue;
                    case ExitPostDominance.FunctionExit:
                        break;
                    default:
                        throw new Xunit.Sdk.XunitException(
                            $"Exit-reachable graph {successors.Length}:{graphCase.Code}, node {node} " +
                            "did not terminate at FunctionExit.");
                }

                break;
            }
        }
    }

    private static bool CanReachTerminalAvoiding(
        ImmutableArray<ImmutableArray<int>> successors,
        int start,
        int? removed)
    {
        if (start == removed)
            return false;

        HashSet<int> visited = [start];
        Queue<int> pending = new([start]);
        while (pending.TryDequeue(out var node))
        {
            if (successors[node].IsEmpty)
                return true;
            foreach (var successor in successors[node])
                if (successor != removed && visited.Add(successor))
                    pending.Enqueue(successor);
        }

        return false;
    }

    private static bool CanReachCycle(
        ImmutableArray<ImmutableArray<int>> successors,
        int start)
    {
        var states = new byte[successors.Length];

        bool Visit(int node)
        {
            states[node] = 1;
            foreach (var successor in successors[node])
            {
                if (states[successor] == 1)
                    return true;
                if (states[successor] == 0 && Visit(successor))
                    return true;
            }

            states[node] = 2;
            return false;
        }

        return Visit(start);
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
            _ => throw new InvalidOperationException()
        };

    private void AssertFacts(
        string name,
        ControlFlowGraph<Unit> graph,
        params (Label Label, Expected Expected)[] expectations)
    {
        var tree = graph.ControlFlowAnalysis().PostDominatorTree;
        var labels = graph.Labels().ToImmutableArray();
        var ids = labels.Select((label, index) => (label, index))
                        .ToDictionary(item => item.label, item => item.index);
        output.WriteLine($"=== {name}: original CFG ===");
        output.WriteLine(graph.PrettyPrint());
        output.WriteLine($"=== {name}: C2 finite-exit postdominance ===");
        foreach (var label in labels)
        {
            var postDominance = tree.ExitPostDominance(label);
            var target = postDominance switch
            {
                ExitPostDominance.Block block => LabelName(block.Target, ids),
                ExitPostDominance.FunctionExit => "function-exit",
                ExitPostDominance.NoExitPath => "no-exit-path",
                _ => throw new InvalidOperationException(
                    $"Unsupported exit-postdominance result {postDominance.GetType().FullName}.")
            };
            output.WriteLine(
                $"{LabelName(label, ids)} -> {target}" +
                (postDominance.MayDiverge ? " may-diverge" : " finite"));
        }

        Assert.Equal(graph.Count, expectations.Length);
        foreach (var (label, expected) in expectations)
        {
            var actual = tree.ExitPostDominance(label);
            Assert.Equal(expected.MayDiverge, actual.MayDiverge);
            switch (expected.Kind)
            {
                case ExpectedKind.Block:
                    Assert.Same(
                        expected.Target,
                        Assert.IsType<ExitPostDominance.Block>(actual).Target);
                    break;
                case ExpectedKind.FunctionExit:
                    Assert.IsType<ExitPostDominance.FunctionExit>(actual);
                    break;
                case ExpectedKind.NoExitPath:
                    Assert.Same(ExitPostDominance.NoExitPath.Instance, actual);
                    break;
            }
        }
    }

    private static IEnumerable<(string Name, ControlFlowGraph<Unit> Graph)> Corpus()
    {
        var e = Label.Create("e");
        var j = Label.Create("j");
        yield return (
            "parallel-arms",
            Graph(
                e,
                (e, new ConditionalSuccessor(j, j)),
                (j, new TerminateSuccessor())));

        e = Label.Create("e");
        var r = Label.Create("r");
        var s = Label.Create("s");
        yield return (
            "mixed-return-divergence",
            Graph(
                e,
                (e, new ConditionalSuccessor(r, s)),
                (r, new TerminateSuccessor()),
                (s, new UnconditionalSuccessor(s))));
    }

    private static ControlFlowGraph<Unit> Graph(
        Label entry,
        params (Label Label, ISuccessor Successor)[] nodes) =>
        new(
            entry,
            nodes.ToDictionary(
                node => node.Label,
                node => new ControlFlowGraph<Unit>.NodeDefinition(
                    node.Successor,
                    default)));

    private static Expected Block(Label target, bool mayDiverge) =>
        new(ExpectedKind.Block, target, mayDiverge);

    private static Expected FunctionExit(bool mayDiverge) =>
        new(ExpectedKind.FunctionExit, null, mayDiverge);

    private static Expected NoExitPath() =>
        new(ExpectedKind.NoExitPath, null, true);

    private static string LabelName(Label label, IReadOnlyDictionary<Label, int> ids) =>
        $"^{ids[label].ToString(CultureInfo.InvariantCulture)}({label.Name ?? "<unnamed>"})";

    private sealed record Expected(ExpectedKind Kind, Label? Target, bool MayDiverge);

    private enum ExpectedKind
    {
        Block,
        FunctionExit,
        NoExitPath
    }
}
