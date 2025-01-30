using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.Common;

namespace DualDrill.CLSL.Test;

public class ControlFlowGraphBuilderTests
{
    public readonly record struct SimpleNode(int Start, int Count)
    {
    }

    static SimpleNode CreateNode(Label labe, ControlFlowGraphBuilder.InstructionRange range) => new(range.Start, range.Count);

    [Fact]
    public void SimpleSingleNodeShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(3, Label.Create);

        var cfg = builder.Build(CreateNode);

        Assert.Equal(1, cfg.Count);

        var e = cfg.EntryLabel;
        Assert.IsType<ReturnOrTerminateSuccessor>(cfg.Successor(e));
        Assert.Empty(cfg.Predecessor(e));

        Assert.Equal(new(0, 3), cfg[e]);

        Assert.Equal([e], cfg.Labels());
    }

    [Fact]
    public void SingleSelfLoopNodeShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(3, Label.Create);

        builder.AddBr(2, 0);
        var cfg = builder.Build(CreateNode);

        Assert.Equal(1, cfg.Count);

        var e = cfg.EntryLabel;
        var se = Assert.IsType<BrOrNextSuccessor>(cfg.Successor(e));
        Assert.Equal(e, se.Target);
        Assert.Equal([e], cfg.Predecessor(e));

        Assert.Equal(new(0, 3), cfg[e]);

        Assert.Equal([e], cfg.Labels());
    }

    [Fact]
    public void TwoNodeChainShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(5, Label.Create);

        var n = builder.AddBr(2, 3);
        var cfg = builder.Build(CreateNode);

        var e = cfg.EntryLabel;
        Assert.Equal(2, cfg.Count);

        var se = Assert.IsType<BrOrNextSuccessor>(cfg.Successor(e));
        Assert.Equal(n, se.Target);
        Assert.Equal([], cfg.Predecessor(e));

        Assert.IsType<ReturnOrTerminateSuccessor>(cfg.Successor(n));
        Assert.Equal([e], cfg.Predecessor(n));

        Assert.Equal(new(0, 3), cfg[e]);
        Assert.Equal(new(3, 2), cfg[n]);


        Assert.Equal([e, n], cfg.Labels());
    }

    [Fact]
    public void TwoNodeBrIfLoopShouldWork()
    {
        var builder = new ControlFlowGraphBuilder(5, Label.Create);

        var e2 = builder.AddBrIf(2, 0);
        var cfg = builder.Build(CreateNode);

        Assert.Equal(2, cfg.Count);

        var e = cfg.EntryLabel;
        Assert.Equal(e2, e);

        var se = Assert.IsType<BrIfSuccessor>(cfg.Successor(e));
        Assert.Equal(e, se.TrueTarget);
        var n = se.FalseTarget;
        Assert.Equal([e], cfg.Predecessor(e));

        Assert.IsType<ReturnOrTerminateSuccessor>(cfg.Successor(n));
        Assert.Equal([e], cfg.Predecessor(n));

        Assert.Equal(new(0, 3), cfg[e]);
        Assert.Equal(new(3, 2), cfg[n]);

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

        var cfg = builder.Build(CreateNode);

        Assert.Equal(4, cfg.Count);

        var e = cfg.EntryLabel;
        var se = Assert.IsType<BrIfSuccessor>(cfg.Successor(e));
        Assert.Equal(t, se.TrueTarget);
        var f = se.FalseTarget;
        Assert.Equal([], cfg.Predecessor(e));

        var st = Assert.IsType<BrOrNextSuccessor>(cfg.Successor(t));
        Assert.Equal(m, st.Target);
        Assert.Equal([e], cfg.Predecessor(t));

        var sf = Assert.IsType<BrOrNextSuccessor>(cfg.Successor(f));
        Assert.Equal(m, st.Target);
        Assert.Equal([e], cfg.Predecessor(f));

        Assert.IsType<ReturnOrTerminateSuccessor>(cfg.Successor(m));
        Assert.Contains(t, cfg.Predecessor(m));
        Assert.Contains(f, cfg.Predecessor(m));

        Assert.Equal(new(0, 3), cfg[e]);
        Assert.Equal(new(5, 3), cfg[t]);
        Assert.Equal(new(3, 2), cfg[f]);
        Assert.Equal(new(8, 2), cfg[m]);


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

        var cfg = builder.Build(CreateNode);

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

        var cfg = builder.Build(CreateNode);

        Assert.Equal(3, cfg.Count);

        Assert.Equal(a, cfg.EntryLabel);
        var sa = Assert.IsType<BrOrNextSuccessor>(cfg.Successor(a));
        Assert.Equal(b, sa.Target);
        Assert.Equal([b], cfg.Predecessor(a));


        var sb = Assert.IsType<BrIfSuccessor>(cfg.Successor(b));
        var c = sb.FalseTarget;
        Assert.Equal(a, sb.TrueTarget);
        Assert.Equal([a], cfg.Predecessor(b));


        Assert.IsType<ReturnOrTerminateSuccessor>(cfg.Successor(c));
        Assert.Equal([b], cfg.Predecessor(c));

        Assert.Equal(new(0, 3), cfg[a]);
        Assert.Equal(new(3, 3), cfg[b]);
        Assert.Equal(new(6, 3), cfg[c]);

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

        var cfg = builder.Build(CreateNode);

        Assert.Equal(6, cfg.Count);

        var a = cfg.EntryLabel;
        var sa = Assert.IsType<BrIfSuccessor>(cfg.Successor(a));
        var b = sa.FalseTarget;
        var sb = Assert.IsType<BrIfSuccessor>(cfg.Successor(b));
        var c = sb.FalseTarget;
        var sc = Assert.IsType<BrOrNextSuccessor>(cfg.Successor(c));
        var sd = Assert.IsType<BrIfSuccessor>(cfg.Successor(d));
        var se = Assert.IsType<BrOrNextSuccessor>(cfg.Successor(e));
        var sf = Assert.IsType<ReturnOrTerminateSuccessor>(cfg.Successor(f));

        Assert.Equal(d, sa.TrueTarget);
        Assert.Equal(e, sb.TrueTarget);
        Assert.Equal(f, sc.Target);
        Assert.Equal(f, sd.TrueTarget);
        Assert.Equal(f, se.Target);

        Assert.Equal(new(0, 4), cfg[a]);
        Assert.Equal(new(4, 3), cfg[b]);
        Assert.Equal(new(7, 3), cfg[c]);
        Assert.Equal(new(10, 3), cfg[d]);
        Assert.Equal(new(13, 3), cfg[e]);
        Assert.Equal(new(16, 3), cfg[f]);

        Assert.Equal([], cfg.Predecessor(a));
        Assert.Equal([a], cfg.Predecessor(b));
        Assert.Equal([b], cfg.Predecessor(c));
        Assert.Equal([a], cfg.Predecessor(d));
        Assert.Equal([b, d], cfg.Predecessor(e));
        Assert.Equal([c, d, e], cfg.Predecessor(f));

        Assert.Equal([a, b, c, d, e, f], cfg.Labels());
    }
}