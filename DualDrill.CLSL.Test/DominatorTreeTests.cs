using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.Common;

namespace DualDrill.CLSL.Test;

public sealed class DominatorTreeTests
{
    [Fact]
    public void SimpleSingleNodeShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Terminate(), default)
        }));

        var dt = DominatorTree.CreateFromControlFlowGraph(cfg);

        Assert.Empty(dt.GetChildren(e));
        Assert.Null(dt.ImmediateDominator(e));
        Assert.Equal([e], dt.Dominators(e));
    }

    [Fact]
    public void SimpleSingleNodeSelfLoopShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Unconditional(e), default)
        }));

        var dt = DominatorTree.CreateFromControlFlowGraph(cfg);

        Assert.Empty(dt.GetChildren(e));
        Assert.Null(dt.ImmediateDominator(e));
        Assert.Equal([e], dt.Dominators(e));
    }

    [Fact]
    public void Simple2NodeChainShouldWork()
    {
        // a: br.if a
        // b
        var a = Label.Create("a");
        var b = Label.Create("b");
        var cfg = new ControlFlowGraph<Unit>(a, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [a] = new(Successor.Conditional(a, b), default),
            [b] = new(Successor.Terminate(), default)
        }));

        var dt = DominatorTree.CreateFromControlFlowGraph(cfg);

        Assert.Equal(b, Assert.Single(dt.GetChildren(a)));
        Assert.Null(dt.ImmediateDominator(a));
        Assert.Equal([a], dt.Dominators(a));

        Assert.Empty(dt.GetChildren(b));
        Assert.Equal(a, dt.ImmediateDominator(b));
        Assert.Equal([a, b], dt.Dominators(b));

        Assert.True(dt.Compare(a, b) < 0);
    }

    [Fact]
    public void ComplexControlFlowShouldWork()
    {
        // cfg:
        //       A
        //      /  \
        //      B   D
        //     / \ / \
        //    C   E  |
        //     \ /   |
        //      F <--*
        //

        // dominator tree:
        //     A
        //  / | | \
        // B  D E  F
        // |
        // C

        var a = Label.Create("a");
        var b = Label.Create("b");
        var c = Label.Create("c");
        var d = Label.Create("d");
        var e = Label.Create("e");
        var f = Label.Create("f");

        var cfg = new ControlFlowGraph<Unit>(a, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [a] = new(Successor.Conditional(d, b), default),
            [b] = new(Successor.Conditional(e, c), default),
            [c] = new(Successor.Unconditional(f), default),
            [d] = new(Successor.Conditional(f, e), default),
            [e] = new(Successor.Unconditional(f), default),
            [f] = new(Successor.Terminate(), default),
        }));

        var dt = DominatorTree.CreateFromControlFlowGraph(cfg);

        Assert.Equal([b, d, e, f], dt.GetChildren(a));
        Assert.Equal([c], dt.GetChildren(b));
        Assert.Empty(dt.GetChildren(c));
        Assert.Empty(dt.GetChildren(d));
        Assert.Empty(dt.GetChildren(e));
        Assert.Empty(dt.GetChildren(f));

        Assert.Equal([a], dt.Dominators(a));
        Assert.Equal([a, b], dt.Dominators(b));
        Assert.Equal([a, b, c], dt.Dominators(c));
        Assert.Equal([a, d], dt.Dominators(d));
        Assert.Equal([a, e], dt.Dominators(e));
        Assert.Equal([a, f], dt.Dominators(f));

        Assert.Null(dt.ImmediateDominator(a));
        Assert.Equal(a, dt.ImmediateDominator(b));
        Assert.Equal(b, dt.ImmediateDominator(c));
        Assert.Equal(a, dt.ImmediateDominator(d));
        Assert.Equal(a, dt.ImmediateDominator(e));
        Assert.Equal(a, dt.ImmediateDominator(f));

        var sorted = new List<Label> { a, b, c, d, e, f };
        sorted.Sort(dt);
        Assert.Equal([a, b, c, d, e, f], sorted);
    }
}
