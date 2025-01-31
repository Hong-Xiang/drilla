using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.LinearInstruction;

namespace DualDrill.CLSL.Test;

using Inst = IStructuredStackInstruction;

public sealed class StructuredControlFlowTests
{
    [Fact]
    public void SimpleSingleBasicBlockShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<BasicBlock<Inst>>(
            e,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [e] = new(Successor.Terminate(), BasicBlock<Inst>.Create([]))
            })
        );
        var ir = cfg.GetStructuredControlFlow();
        var b = Assert.IsType<Block<Inst>>(ir);
    }

    [Fact]
    public void SimpleSingleSelfLoopBasicBlockShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<BasicBlock<Inst>>(
            e,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [e] = new(Successor.Unconditional(e), BasicBlock<Inst>.Create([]))
            })
        );
        var ir = cfg.GetStructuredControlFlow();
        // loop @e:
        //   body:
        //     ... e ...
        //     br e
        var b = Assert.IsType<Loop<Inst>>(ir);
        Assert.Equal(e, b.Label);
        var instBrIf = Assert.IsType<BrInstruction>(Assert.Single(b.BodyBlock.Body));
        Assert.Equal(e, instBrIf.Target);
    }

    [Fact]
    public void MinimumIfThenElseShouldWork()
    {
        // e
        // |\
        // f t
        var e = Label.Create("e");
        var f = Label.Create("f");
        var t = Label.Create("t");
        var cfg = new ControlFlowGraph<BasicBlock<Inst>>(
            e,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [e] = new(Successor.Conditional(t, f), BasicBlock<Inst>.Create([ShaderInstruction.Const(Literal.Create(true))])),
                [t] = new(Successor.Terminate(), BasicBlock<Inst>.Create([ShaderInstruction.Const(Literal.Create(1))])),
                [f] = new(Successor.Terminate(), BasicBlock<Inst>.Create([ShaderInstruction.Const(Literal.Create(2))])),
            })
        );
        var result = cfg.GetStructuredControlFlow();
        // block:
        //   ... e ...
        //   if
        //      ... t ...
        //      return
        //   else
        //      ... f ...
        //      return
        var re = Assert.IsType<Block<Inst>>(result);
        Assert.Equal(2, re.Body.Length);
        var bbe = Assert.IsType<BasicBlock<Inst>>(re.Body[0]);
        var rif = Assert.IsType<IfThenElse<Inst>>(re.Body[1]);
        var rt = Assert.IsType<Block<Inst>>(rif.TrueBlock);
        var bbt = Assert.IsType<BasicBlock<Inst>>(rt.Body[0]);
        var rf = Assert.IsType<Block<Inst>>(rif.FalseBlock);
        var bbf = Assert.IsType<BasicBlock<Inst>>(rf.Body[0]);
    }

    [Fact]
    public void MinimumIfThenElseMergeShouldWork()
    {
        //  e
        //  |\
        //  f t 
        //  |/
        //  m
        var e = Label.Create("e");
        var f = Label.Create("f");
        var t = Label.Create("t");
        var m = Label.Create("m");
        var cfg = new ControlFlowGraph<BasicBlock<Inst>>(
            e,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [e] = new(Successor.Conditional(t, f), BasicBlock<Inst>.Create([ShaderInstruction.Const(Literal.Create(true))])),
                [t] = new(Successor.Unconditional(m), BasicBlock<Inst>.Create([])),
                [f] = new(Successor.Unconditional(m), BasicBlock<Inst>.Create([])),
                [m] = new(Successor.Terminate(), BasicBlock<Inst>.Create([])),
            })
        );
        var result = cfg.GetStructuredControlFlow();
        // block
        //   ... e ...
        //   if
        //      ... t ...
        //   else
        //      ... f ...
        //   ... m ...
        var ir = Assert.IsType<Block<Inst>>(result);
        Assert.Equal(3, ir.Body.Length);
        var re = Assert.IsType<Block<Inst>>(ir.Body[0]);
        var rif = Assert.IsType<IfThenElse<Inst>>(ir.Body[1]);
        var rt = Assert.IsType<Block<Inst>>(rif.TrueBlock);
        var rf = Assert.IsType<Block<Inst>>(rif.FalseBlock);
        var rm = Assert.IsType<Block<Inst>>(ir.Body[2]);
    }

    [Fact]
    public void MinimumConditionalLoopShouldWork()
    {
        // cfg:
        //  a <-*
        //  |   |
        //  v   |
        //  b --*  
        //  |
        //  v
        //  c

        // dominator tree
        // a <- b <- c
        var a = Label.Create("a"); // children (b, c) into loop
        var b = Label.Create("b"); // children (c) into if-else
        var c = Label.Create("c"); // inside else branch

        var cfg = new ControlFlowGraph<BasicBlock<Inst>>(
            a,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [a] = new(Successor.Unconditional(b), BasicBlock<Inst>.Create([])),
                [b] = new(Successor.Conditional(a, c), BasicBlock<Inst>.Create([])),
                [c] = new(Successor.Terminate(), BasicBlock<Inst>.Create([])),
            })
        );
        var result = cfg.GetStructuredControlFlow();

        // loop @a
        //   ... a ...
        //   ... b ...
        //   if
        //      br a (continue)
        //   else
        //      ... c ...
        //      return
        //   end (if)
        // end (loop)

        var lp = Assert.IsType<Loop<Inst>>(result);
        Assert.Equal(3, lp.BodyBlock.Body.Length);
        var bba = Assert.IsType<BasicBlock<Inst>>(lp.BodyBlock.Body[0]);
        var bbb = Assert.IsType<BasicBlock<Inst>>(lp.BodyBlock.Body[1]);
        var rif = Assert.IsType<IfThenElse<Inst>>(lp.BodyBlock.Body[2]);
    }
}
