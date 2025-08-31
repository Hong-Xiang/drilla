using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Test;

public sealed class ControlFlowAnalysisTests
{

    [Fact]
    public void SimpleSingleNode()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Terminate(), default)
        }));

        var cfr = cfg.ControlFlowAnalysis();

        Assert.Equal(0, cfr.IndexOf(e));
        Assert.False(cfr.IsLoop(e));
    }

    [Fact]
    public void SimpleSelfLoopNode()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Unconditional(e), default)
        }));

        var cfr = cfg.ControlFlowAnalysis();

        Assert.Equal(0, cfr.IndexOf(e));
        Assert.True(cfr.IsLoop(e));
    }

    [Fact]
    public void SimpleConditionalLoop()
    {
        // cfg:
        //  a <-*
        //  |   |
        //  v   |
        //  b --*  
        //  |
        //  v
        //  c
        var a = Label.Create("a"); // children (b, c) into loop
        var b = Label.Create("b"); // children (c) into if-else
        var c = Label.Create("c"); // inside else branch

        var cfg = new ControlFlowGraph<Unit>(
            a,
            ControlFlowGraph.CreateDefinitions<Unit>(new()
            {
                [a] = new(Successor.Unconditional(b), default),
                [b] = new(Successor.Conditional(a, c), default),
                [c] = new(Successor.Terminate(), default),
            })
        );

        var cfr = cfg.ControlFlowAnalysis();

        Assert.Equal(0, cfr.IndexOf(a));
        Assert.Equal(1, cfr.IndexOf(b));
        Assert.Equal(2, cfr.IndexOf(c));

        Assert.True(cfr.IsLoop(a));
        Assert.False(cfr.IsLoop(b));
        Assert.False(cfr.IsLoop(c));
    }
}
