using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Test;

public sealed class PostDominatorAnalysisTests
{
    [Fact]
    public void SimpleSingleNodeShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Terminate(), default)
        }));
        var pda = PostDominatorAnalysis.Create(cfg);
        Assert.Null(pda.GetMergeImmediatePostDominator(e));
    }

    [Fact]
    public void SimpleSingleNodeSelfLoopShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Unconditional(e), default)
        }));
        var pda = PostDominatorAnalysis.Create(cfg);
        Assert.Null(pda.GetMergeImmediatePostDominator(e));
    }

    [Fact]
    public void Simple2NodeChainShouldWork()
    {
        var a = Label.Create("a");
        var b = Label.Create("b");
        var cfg = new ControlFlowGraph<Unit>(a, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [a] = new(Successor.Conditional(a, b), default),
            [b] = new(Successor.Terminate(), default)
        }));
        var pda = PostDominatorAnalysis.Create(cfg);
        Assert.Null(pda.GetMergeImmediatePostDominator(a));
        Assert.Null(pda.GetMergeImmediatePostDominator(b));
    }

    [Fact]
    public void ConditionalMerge()
    {
        var e = Label.Create("e");
        var t = Label.Create("t");
        var f = Label.Create("f");
        var m = Label.Create("m");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Conditional(t, f), default),
            [t] = new(Successor.Unconditional(m), default),
            [f] = new(Successor.Unconditional(m), default),
            [m] = new(Successor.Terminate(), default)
        }));
        var pda = PostDominatorAnalysis.Create(cfg);
        Assert.Equal(m, pda.GetMergeImmediatePostDominator(e));
        Assert.Equal(m, pda.GetMergeImmediatePostDominator(t));
        Assert.Equal(m, pda.GetMergeImmediatePostDominator(f));
        Assert.Null(pda.GetMergeImmediatePostDominator(m));
    }

    [Fact]
    public void MinimumLoop()
    {
        // cfg
        // e -> l <--+
        //     / \   |
        //    b   c -+
        //    |
        //    +-> t

        // block e -> l
        //   loop l -> t
        //     block b -> t
        //       block t -> null
        //         return
        //       br t
        //     block c -> l
        //       br l
        //     br_if b c
        //   br l

        // ... e ...
        // loop l {
        //     ... l ...
        //     if {
        //        ... b ...
        //     } else {
        //        ... c ...
        //     } 
        //     ... t ...
        // }


        var e = Label.Create("e");
        var l = Label.Create("l");
        var c = Label.Create("c");
        var b = Label.Create("b");
        var t = Label.Create("t");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Unconditional(l), default),
            [l] = new(Successor.Conditional(c, b), default),
            [c] = new(Successor.Unconditional(l), default),
            [b] = new(Successor.Unconditional(t), default),
            [t] = new(Successor.Terminate(), default)
        }));
        var pda = PostDominatorAnalysis.Create(cfg);
        Assert.Equal(l, pda.GetMergeImmediatePostDominator(e));
        Assert.Equal(t, pda.GetMergeImmediatePostDominator(l));
        Assert.Null(pda.GetMergeImmediatePostDominator(c));
        Assert.Equal(t, pda.GetMergeImmediatePostDominator(b));
        Assert.Null(pda.GetMergeImmediatePostDominator(t));
    }

    [Fact]
    public void MergedLoopBreak()
    {
        // cfg
        // e -> l <--+
        // |   / \   |
        // |  b   c -+
        // v  |
        // t<-+

        // blk ^e: -> null
        //   blk ^t: -> null
        //      return
        //   loop ^l: -> t
        //      blk ^b: -> t
        //          br ^t
        //      blk ^c: -> l
        //          br ^l
        //      br_if ^b ^c
        //   br_if ^l ^t 


        var e = Label.Create("e");
        var l = Label.Create("l");
        var c = Label.Create("c");
        var b = Label.Create("b");
        var t = Label.Create("t");
        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Conditional(l, t), default),
            [l] = new(Successor.Conditional(c, b), default),
            [c] = new(Successor.Unconditional(l), default),
            [b] = new(Successor.Unconditional(t), default),
            [t] = new(Successor.Terminate(), default)
        }));
        var pda = PostDominatorAnalysis.Create(cfg);
        Assert.Equal(t, pda.GetMergeImmediatePostDominator(e));
        Assert.Equal(t, pda.GetMergeImmediatePostDominator(l));
        Assert.Null(pda.GetMergeImmediatePostDominator(c));
        Assert.Equal(t, pda.GetMergeImmediatePostDominator(b));
        Assert.Null(pda.GetMergeImmediatePostDominator(t));
    }
    [Fact]
    public void NestedLoop()
    {
        //       +-----------------+
        //       |                 |
        //       v                 | 
        // e -> l1-->b2--+-->l2-->b5
        //       |       |    |
        //       |       |    +-->b4
        //       |       |         |
        //       |       ----------+
        //       |
        //       +-->b6            


        // blk ^e: -> l1
        //   loop ^l1: -> l2
        //     blk ^b2: -> l2
        //       loop ^l2 -> l1
        //         blk ^b5: -> l1
        //           br l1
        //         blk ^b4: -> l2
        //           br l2
        //         br_if b4 b5
        //       br ^l2
        //     blk ^b6: -> null
        //       return
        //     br_if ^b2 ^b6
        //   br ^l1

        // ... e ...
        // loop l1 {
        //   ... l1 ...
        //   if {
        //      ... b2 ...
        //   } else {
        //      ... b6 ...
        //   }
        //   loop l2 {
        //      if {
        //        ... b4 ...
        //        continue;
        //      } else {
        //        ... b5 ...
        //        break; (requires next(l1) == l2)
        //      }
        //   }
        // }

        var e = Label.Create("e");
        var l1 = Label.Create("l1");
        var l2 = Label.Create("l2");
        var b2 = Label.Create("b2");
        var b4 = Label.Create("b4");
        var b5 = Label.Create("b5");
        var b6 = Label.Create("b6");

        var cfg = new ControlFlowGraph<Unit>(e, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [e] = new(Successor.Unconditional(l1), default),
            [l1] = new(Successor.Conditional(b2, b6), default),
            [b2] = new(Successor.Unconditional(l2), default),
            [l2] = new(Successor.Conditional(b4, b5), default),
            [b4] = new(Successor.Unconditional(l2), default),
            [b5] = new(Successor.Unconditional(l1), default),
            [b6] = new(Successor.Terminate(), default),
        }));

        var pda = PostDominatorAnalysis.Create(cfg);

        Assert.Equal(l1, pda.GetMergeImmediatePostDominator(e));
        Assert.Equal(l2, pda.GetMergeImmediatePostDominator(l1));
        Assert.Equal(l1, pda.GetMergeImmediatePostDominator(l2));
        Assert.Equal(l2, pda.GetMergeImmediatePostDominator(b2));
        Assert.Null(pda.GetMergeImmediatePostDominator(b4));
        Assert.Equal(l1, pda.GetMergeImmediatePostDominator(b5));
        Assert.Null(pda.GetMergeImmediatePostDominator(b6));
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


        // blk a -> f
        //   blk f -> null
        //     return
        //   blk e -> f
        //       br f
        //   blk d -> f
        //      br_if e f 
        //   blk b -> f
        //     blk c -> f
        //       br f
        //     br_if c e
        //   br_if b d


        // ... a ...
        // if {
        //   ... b ...
        //   if {
        //      ... c ...
        //      goto f? HOW? a explicit loop with break could work
        //   } 
        // } else {
        //   ... d ...
        //   if {
        //      
        //   }
        // }
        // ... e ...
        // ... f ...

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

        var pda = PostDominatorAnalysis.Create(cfg);

        Assert.Equal(f, pda.GetMergeImmediatePostDominator(a));
        Assert.Equal(f, pda.GetMergeImmediatePostDominator(b));
        Assert.Equal(f, pda.GetMergeImmediatePostDominator(c));
        Assert.Equal(f, pda.GetMergeImmediatePostDominator(d));
        Assert.Equal(f, pda.GetMergeImmediatePostDominator(e));
        Assert.Null(pda.GetMergeImmediatePostDominator(f));
    }

    [Fact]
    public void ComplexControlFlowShouldWork2()
    {
        // cfg:
        //       A
        //      /  \
        //      B   D
        //     /   / \
        //    C   E  |
        //     \ /   |
        //      F <--*
        //

        var a = Label.Create("a");
        var b = Label.Create("b");
        var c = Label.Create("c");
        var d = Label.Create("d");
        var e = Label.Create("e");
        var f = Label.Create("f");

        var cfg = new ControlFlowGraph<Unit>(a, ControlFlowGraph.CreateDefinitions<Unit>(new()
        {
            [a] = new(Successor.Conditional(d, b), default),
            [b] = new(Successor.Unconditional(c), default),
            [c] = new(Successor.Unconditional(f), default),
            [d] = new(Successor.Conditional(f, e), default),
            [e] = new(Successor.Unconditional(f), default),
            [f] = new(Successor.Terminate(), default),
        }));

        var pda = PostDominatorAnalysis.Create(cfg);

        Assert.Equal(f, pda.GetMergeImmediatePostDominator(a));
        Assert.Equal(c, pda.GetMergeImmediatePostDominator(b));
        Assert.Equal(f, pda.GetMergeImmediatePostDominator(c));
        Assert.Equal(f, pda.GetMergeImmediatePostDominator(d));
        Assert.Equal(f, pda.GetMergeImmediatePostDominator(e));
        Assert.Null(pda.GetMergeImmediatePostDominator(f));
    }
}
