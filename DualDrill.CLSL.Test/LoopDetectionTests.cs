using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.CLSL.Test;

public sealed class LoopDetectionTests
{
    [Fact]
    public void SimpleSelfLoopNodeShouldNotBeLoop()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Terminate(), default)
        }));

        var cfr = cfg.ControlFlowAnalysis();

        Assert.False(cfr.IsLoop(e));
    }

    [Fact]
    public void SimpleSelfLoopNodeShouldBeLoop()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Unconditional(e), default)
        }));

        var cfr = cfg.ControlFlowAnalysis();

        Assert.True(cfr.IsLoop(e));
    }

    [Fact]
    public void SimpleConditionalLoopShouldWork()
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

        Assert.True(cfr.IsLoop(a));
        Assert.False(cfr.IsLoop(b));
        Assert.False(cfr.IsLoop(c));
    }
}
