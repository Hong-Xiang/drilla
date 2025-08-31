using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;
public sealed class StructuredControlFlowTests(ITestOutputHelper Output)
{
    [Fact]
    public void SimpleSingleBasicBlockShouldWork()
    {
        // === cfg ===
        // e

        // === dominator tree ===
        // e

        // === ir ===
        // block ^e:
        //     return

        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Terminate(), default)
        }));
        var ir = RegionTree.Create(cfg.ControlFlowAnalysis(),
            (e, Successor.Terminate()));
        Output.WriteLine(ir.Show());
        Assert.Equal(e, ir.Label);
        Assert.Empty(ir.Bindings);
        Assert.Equal(Successor.Terminate(), ir.Body);
    }

    [Fact]
    public void SimpleSingleSelfLoopBasicBlockShouldWork()
    {
        // === cfg ==
        // e <-+
        // |   |
        // +---|

        // === dominator tree ===
        // e

        // === ir ===
        // loop ^e:
        //   br e

        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Unconditional(e), default)
        }));
        var ir = RegionTree.Create(cfg.ControlFlowAnalysis(),
            (e, Successor.Unconditional(e)));
        Output.WriteLine(ir.Show());
        Assert.Equal(e, ir.Label);
        Assert.Empty(ir.Bindings);
        Assert.Equal(Successor.Unconditional(e), ir.Body);
    }



    [Fact]
    public void MinimumIfThenElseShouldWork()
    {
        // === cfg ===
        // e
        // |\
        // t f

        // === dominator tree ===
        // e
        // ├─ t
        // └─ f

        // === ir ===
        // block ^e:
        //   block ^t:
        //     return
        //   block ^f:
        //     return
        //   brif t f

        var e = Label.Create("e");
        var f = Label.Create("f");
        var t = Label.Create("t");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Conditional(t, f), default),
            [t] = new(Successor.Terminate(), default),
            [f] = new(Successor.Terminate(), default)
        }));
        var ir = RegionTree.Create<ISuccessor>(cfg.ControlFlowAnalysis(),
            (e, Successor.Conditional(t, f)),
            (t, Successor.Terminate()),
            (f, Successor.Terminate()));
        Output.WriteLine(ir.Show());
        Assert.Equal(e, ir.Label);
        Assert.Equal(2, ir.Bindings.Count());
        Assert.Equal(Successor.Conditional(t, f), ir.Body);
    }

    [Fact]
    public void MinimumIfThenElseMergeShouldWork()
    {
        // === cfg ===
        //  e
        //  |\
        //  t f 
        //  |/
        //  m

        // === dominator tree ===
        // e
        // ├─ t
        // ├─ f
        // └─ m

        // === ir ===
        // block ^e:
        //   block ^m:
        //     return
        //   block ^f:
        //       br m
        //   block ^t:
        //       br m
        //   brif t f

        var e = Label.Create("e");
        var f = Label.Create("f");
        var t = Label.Create("t");
        var m = Label.Create("m");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Conditional(t, f), default),
            [t] = new(Successor.Unconditional(m), default),
            [f] = new(Successor.Unconditional(m), default),
            [m] = new(Successor.Terminate(), default)
        }));
        var ir = RegionTree.Create<ITerminator<Label, Unit>>(cfg.ControlFlowAnalysis(),
            (e, Terminator.B.BrIf(default(Unit), t, f)),
            (t, Terminator.B.Br<Label, Unit>(m)),
            (f, Terminator.B.Br<Label, Unit>(m)),
            (m, Terminator.B.ReturnVoid<Label, Unit>()));
        Output.WriteLine(ir.Show());
        Assert.Equal(e, ir.Label);
        var bindings = ir.Bindings.ToImmutableArray();
        Assert.Equal(3, bindings.Length);
        Assert.Equal(m, bindings[0].Label);
        Assert.Equal(t, bindings[1].Label);
        Assert.Equal(f, bindings[2].Label);
    }


    [Fact]
    public void SimpleLoopShouldWork()
    {
        // === cfg ===
        // b0  (loop init)--false-+
        // |                      |
        // true                   |
        // |                      |
        // + ----------- +        |
        // |             |        |
        // v             |        |
        // b1 - true -> b2        |
        // |                      |
        // false                  |
        // |                      |
        // v                      |  
        // b3<--------------------+

        // === dominator tree ===
        // b0
        // ├─ b1
        // │  └─ b2
        // └─ b3

        // === ir ===
        // block ^b0:
        //   block ^b3:
        //     return
        //   loop ^b1:
        //     block ^b2:
        //       br b1
        //     brif b2 b3
        //   brif b1 b3 

        var b0 = Label.Create("b0");
        var b1 = Label.Create("b1");
        var b2 = Label.Create("b2");
        var b3 = Label.Create("b3");
        var cfg = new ControlFlowGraph<Unit>(b0, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [b0] = new(Successor.Conditional(b1, b3), default),
            [b1] = new(Successor.Conditional(b2, b3), default),
            [b2] = new(Successor.Unconditional(b1), default),
            [b3] = new(Successor.Terminate(), default)
        }));

        var ir = RegionTree.Create<ISuccessor>(cfg.ControlFlowAnalysis(),
            (b0, Successor.Conditional(b1, b3)),
            (b1, Successor.Conditional(b2, b3)),
            (b2, Successor.Unconditional(b1)),
            (b3, Successor.Terminate())
            );

        Output.WriteLine(ir.Show());
        Assert.Equal(b0, ir.Label);
        Assert.Equal(2, ir.Bindings.Count());
        Assert.Equal(Successor.Conditional(b1, b3), ir.Body);
    }

    [Fact]
    public void MinimumConditionalLoopShouldWork()
    {
        // === cfg ===
        //  a <-+
        //  |   |
        //  v   |
        //  b --+  
        //  |
        //  v
        //  c

        // === dominator tree ===
        // a
        // └─ b
        //    └─ c

        // === ir ===
        // loop ^a:
        //   block ^b:
        //     block ^c:
        //       return
        //     brif a c
        //   br b

        var a = Label.Create("a"); // loop header
        var b = Label.Create("b"); // loop body
        var c = Label.Create("c"); // loop exit

        var cfg = new ControlFlowGraph<Unit>(a, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [a] = new(Successor.Unconditional(b), default),
            [b] = new(Successor.Conditional(a, c), default),
            [c] = new(Successor.Terminate(), default)
        }));

        var ir = RegionTree.Create<ITerminator<Label, Unit>>(cfg.ControlFlowAnalysis(),
            (a, Terminator.B.Br<Label, Unit>(b)),
            (b, Terminator.B.BrIf(default(Unit), a, c)),
            (c, Terminator.B.ReturnVoid<Label, Unit>()));

        Output.WriteLine(ir.Show());
        Assert.Equal(a, ir.Label);
        Assert.Single(ir.Bindings);
        var rb = ir.Bindings.Single();
        Assert.Equal(b, rb.Label);
        var rc = Assert.Single(rb.Bindings);
        Assert.Equal(c, rc.Label);
    }
}