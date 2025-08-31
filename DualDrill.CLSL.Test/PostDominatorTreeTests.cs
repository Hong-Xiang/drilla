using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Test;

public sealed class PostDominatorTreeTests
{
    [Fact]
    public void SimpleSingleNodeShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Terminate(), default)
        }));


        var cfa = cfg.ControlFlowAnalysis();
        var pdt = cfa.PostDominatorTree;

        Assert.Null(pdt.ImmediatePostDominator(e));
    }

    [Fact]
    public void SimpleSingleNodeSelfLoopShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Unconditional(e), default)
        }));


        var cfa = cfg.ControlFlowAnalysis();
        var pdt = cfa.PostDominatorTree;

        Assert.Null(pdt.ImmediatePostDominator(e));
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


        var cfa = cfg.ControlFlowAnalysis();
        var pdt = cfa.PostDominatorTree;

        Assert.Equal(b, pdt.ImmediatePostDominator(a));
        Assert.Null(pdt.ImmediatePostDominator(b));
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


        var cfa = cfg.ControlFlowAnalysis();
        var pdt = cfa.PostDominatorTree;

        Assert.Equal(f, pdt.ImmediatePostDominator(a));
        Assert.Equal(f, pdt.ImmediatePostDominator(b));
        Assert.Equal(f, pdt.ImmediatePostDominator(c));
        Assert.Equal(f, pdt.ImmediatePostDominator(d));
        Assert.Equal(f, pdt.ImmediatePostDominator(e));
        Assert.Null(pdt.ImmediatePostDominator(f));
    }
}
