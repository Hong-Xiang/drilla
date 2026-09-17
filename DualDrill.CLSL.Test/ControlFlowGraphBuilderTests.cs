using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Test;

public class ControlFlowGraphBuilderTests
{
    public readonly record struct SimpleNode(int Start, int Count, ISuccessor Successor);

    static SimpleNode CreateNode(
        Label label,
        ControlFlowGraphBuilder.InstructionRange range,
        ISuccessor successor) =>
        new(range.Start, range.Count, successor);

    static ControlFlowGraph<SimpleNode> Build(ControlFlowGraphBuilder builder) =>
        builder.Build(CreateNode, static node => node.Successor);

    [Fact]
    public void SimpleSingleNodeShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(3, Label.Create);

        var cfg = Build(builder);

        Assert.Equal(1, cfg.Count);

        var e = cfg.EntryLabel;
        Assert.IsType<TerminateSuccessor>(cfg.Successor(e));
        Assert.Empty(cfg.Predecessor(e));

        Assert.Equal(new SimpleNode(0, 3, cfg.Successor(e)), cfg[e]);

        Assert.Equal([e], cfg.Labels());
    }

    [Fact]
    public void SingleSelfLoopNodeShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(3, Label.Create);

        builder.AddBr(2, 0);
        var cfg = Build(builder);

        Assert.Equal(1, cfg.Count);

        var e = cfg.EntryLabel;
        var se = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(e));
        Assert.Equal(e, se.Target);
        Assert.Equal([e], cfg.Predecessor(e));

        Assert.Equal(new SimpleNode(0, 3, cfg.Successor(e)), cfg[e]);

        Assert.Equal([e], cfg.Labels());
    }

    [Fact]
    public void TwoNodeChainShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(5, Label.Create);

        var n = builder.AddBr(2, 3);
        var cfg = Build(builder);

        var e = cfg.EntryLabel;
        Assert.Equal(2, cfg.Count);

        var se = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(e));
        Assert.Equal(n, se.Target);
        Assert.Equal([], cfg.Predecessor(e));

        Assert.IsType<TerminateSuccessor>(cfg.Successor(n));
        Assert.Equal([e], cfg.Predecessor(n));

        Assert.Equal(new SimpleNode(0, 3, cfg.Successor(e)), cfg[e]);
        Assert.Equal(new SimpleNode(3, 2, cfg.Successor(n)), cfg[n]);


        Assert.Equal([e, n], cfg.Labels());
    }

    [Fact]
    public void PenultimateBlockShouldFallThroughToFinalInstruction()
    {
        var builder = new ControlFlowGraphBuilder(3, Label.Create);

        var final = builder.AddBr(0, 2);
        var cfg = Build(builder);
        var penultimate = Assert.Single(cfg.Predecessor(final), label => label != cfg.EntryLabel);

        Assert.Equal(new SimpleNode(1, 1, cfg.Successor(penultimate)), cfg[penultimate]);
        Assert.Equal(final, Assert.IsType<UnconditionalSuccessor>(cfg.Successor(penultimate)).Target);
        Assert.Equal(new SimpleNode(2, 1, cfg.Successor(final)), cfg[final]);
        Assert.IsType<TerminateSuccessor>(cfg.Successor(final));
    }

    [Fact]
    public void TwoNodeBrIfLoopShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(5, Label.Create);

        var e2 = builder.AddBrIf(2, 0);
        var cfg = Build(builder);

        Assert.Equal(2, cfg.Count);

        var e = cfg.EntryLabel;
        Assert.Equal(e2, e);

        var se = Assert.IsType<ConditionalSuccessor>(cfg.Successor(e));
        Assert.Equal(e, se.TrueTarget);
        var n = se.FalseTarget;
        Assert.Equal([e], cfg.Predecessor(e));

        Assert.IsType<TerminateSuccessor>(cfg.Successor(n));
        Assert.Equal([e], cfg.Predecessor(n));

        Assert.Equal(new SimpleNode(0, 3, cfg.Successor(e)), cfg[e]);
        Assert.Equal(new SimpleNode(3, 2, cfg.Successor(n)), cfg[n]);

        Assert.Equal([e, n], cfg.Labels());
    }

    [Fact]
    public void MinimumIfThenElseShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(10, Label.Create);
        // e : 0 - 2
        // f : 3 - 4
        // t : 5 - 7
        // m : 8 - 9

        var t = builder.AddBrIf(2, 5);
        var m = builder.AddBr(4, 8);

        var cfg = Build(builder);

        Assert.Equal(4, cfg.Count);

        var e = cfg.EntryLabel;
        var se = Assert.IsType<ConditionalSuccessor>(cfg.Successor(e));
        Assert.Equal(t, se.TrueTarget);
        var f = se.FalseTarget;
        Assert.Equal([], cfg.Predecessor(e));

        var st = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(t));
        Assert.Equal(m, st.Target);
        Assert.Equal([e], cfg.Predecessor(t));

        var sf = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(f));
        Assert.Equal(m, st.Target);
        Assert.Equal([e], cfg.Predecessor(f));

        Assert.IsType<TerminateSuccessor>(cfg.Successor(m));
        Assert.Contains(t, cfg.Predecessor(m));
        Assert.Contains(f, cfg.Predecessor(m));

        Assert.Equal(new SimpleNode(0, 3, cfg.Successor(e)), cfg[e]);
        Assert.Equal(new SimpleNode(5, 3, cfg.Successor(t)), cfg[t]);
        Assert.Equal(new SimpleNode(3, 2, cfg.Successor(f)), cfg[f]);
        Assert.Equal(new SimpleNode(8, 2, cfg.Successor(m)), cfg[m]);


        Assert.Equal([e, f, t, m], cfg.Labels());
    }

    [Fact]
    public void MinimumIfThenElseWithAddtionalBrShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(10, Label.Create);
        // e : 0 - 2
        // f : 3 - 4
        // t : 5 - 7
        // m : 8 - 9

        var t = builder.AddBrIf(2, 5);
        var m = builder.AddBr(4, 8);
        var m2 = builder.AddBr(7, 8);
        Assert.Equal(m, m2);

        var cfg = Build(builder);

        Assert.Equal(4, cfg.Count);
    }

    [Fact]
    public void ChainedLoop3NodeShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(9, Label.Create);
        // a : 0 - 2
        // b : 3 - 5
        // c : 6 - 8
        //
        // a -> b -> c
        // ^    |
        // | -- |
        var b = builder.AddBr(2, 3);
        var a = builder.AddBrIf(5, 0);

        var cfg = Build(builder);

        Assert.Equal(3, cfg.Count);

        Assert.Equal(a, cfg.EntryLabel);
        var sa = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(a));
        Assert.Equal(b, sa.Target);
        Assert.Equal([b], cfg.Predecessor(a));


        var sb = Assert.IsType<ConditionalSuccessor>(cfg.Successor(b));
        var c = sb.FalseTarget;
        Assert.Equal(a, sb.TrueTarget);
        Assert.Equal([a], cfg.Predecessor(b));


        Assert.IsType<TerminateSuccessor>(cfg.Successor(c));
        Assert.Equal([b], cfg.Predecessor(c));

        Assert.Equal(new SimpleNode(0, 3, cfg.Successor(a)), cfg[a]);
        Assert.Equal(new SimpleNode(3, 3, cfg.Successor(b)), cfg[b]);
        Assert.Equal(new SimpleNode(6, 3, cfg.Successor(c)), cfg[c]);

        Assert.Equal([a, b, c], cfg.Labels());
    }


    [Fact]
    public void ComplexMergeBranchShouldWork()
    {
        // a: 0 - 3, br.if d b
        // b: 4 - 6, br.if e c
        // c: 7 - 9, br f
        // d: 10 - 12, br.if f e
        // e: 13 - 15, (implicit br f)
        // f: 16 - 18

        var builder = new ControlFlowGraphBuilder(19, Label.Create);

        var d = builder.AddBrIf(3, 10);
        var e = builder.AddBrIf(6, 13);
        var f = builder.AddBr(9, 16);
        var f2 = builder.AddBrIf(12, 16);
        Assert.Equal(f, f2);

        var cfg = Build(builder);

        Assert.Equal(6, cfg.Count);

        var a = cfg.EntryLabel;
        var sa = Assert.IsType<ConditionalSuccessor>(cfg.Successor(a));
        var b = sa.FalseTarget;
        var sb = Assert.IsType<ConditionalSuccessor>(cfg.Successor(b));
        var c = sb.FalseTarget;
        var sc = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(c));
        var sd = Assert.IsType<ConditionalSuccessor>(cfg.Successor(d));
        var se = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(e));
        var sf = Assert.IsType<TerminateSuccessor>(cfg.Successor(f));

        Assert.Equal(d, sa.TrueTarget);
        Assert.Equal(e, sb.TrueTarget);
        Assert.Equal(f, sc.Target);
        Assert.Equal(f, sd.TrueTarget);
        Assert.Equal(f, se.Target);

        Assert.Equal(new SimpleNode(0, 4, cfg.Successor(a)), cfg[a]);
        Assert.Equal(new SimpleNode(4, 3, cfg.Successor(b)), cfg[b]);
        Assert.Equal(new SimpleNode(7, 3, cfg.Successor(c)), cfg[c]);
        Assert.Equal(new SimpleNode(10, 3, cfg.Successor(d)), cfg[d]);
        Assert.Equal(new SimpleNode(13, 3, cfg.Successor(e)), cfg[e]);
        Assert.Equal(new SimpleNode(16, 3, cfg.Successor(f)), cfg[f]);

        Assert.Equal([], cfg.Predecessor(a));
        Assert.Equal([a], cfg.Predecessor(b));
        Assert.Equal([b], cfg.Predecessor(c));
        Assert.Equal([a], cfg.Predecessor(d));
        Assert.Equal([b, d], cfg.Predecessor(e));
        Assert.Equal([c, d, e], cfg.Predecessor(f));

        Assert.Equal([a, b, c, d, e, f], cfg.Labels());
    }

    [Fact]
    public void ConditionalBranchRequiresFallthroughInstruction()
    {
        var builder = new ControlFlowGraphBuilder(1, Label.Create);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddBrIf(0, 0));
    }

    [Fact]
    public void BranchTargetMustReferenceInstruction()
    {
        var builder = new ControlFlowGraphBuilder(2, Label.Create);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddBr(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddBr(0, -1));
    }
}