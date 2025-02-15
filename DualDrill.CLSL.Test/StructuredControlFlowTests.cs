using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;
using Xunit.Abstractions;
using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Operation;
using DualDrill.Common.Nat;
using FluentAssertions;

namespace DualDrill.CLSL.Test;

using Inst = IStructuredStackInstruction;

public sealed class StructuredControlFlowTests(ITestOutputHelper Output)
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
        var ir = cfg.ToStructuredControlFlow();
        Dump(ir);
        var b = Assert.IsType<Block<Inst>>(ir);
    }

    [Fact]
    public void SimpleSingleSelfLoopBasicBlockShouldWork()
    {
        var e = Label.Create("e");
        var v = new VariableDeclaration(DeclarationScope.Function, 0, "x", ShaderType.I32, []);
        var cfg = new ControlFlowGraph<BasicBlock<Inst>>(
            e,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [e] = new(Successor.Unconditional(e), BasicBlock<Inst>.Create([
                    ShaderInstruction.Load(v),
                    ShaderInstruction.Const(new I32Literal(1)),
                    BinaryOperationInstruction<NumericBinaryOperation<IntType<N32>, BinaryArithmetic.Add>>.Instance,
                    ShaderInstruction.Store(v),
                ]))
            })
        );
        var ir = cfg.ToStructuredControlFlow();
        Output.WriteLine(ir.GetType().FullName);
        Dump(ir);
        // loop @e:
        //   body:
        //     ... e ...
        //     br e
        var b = Assert.IsType<Loop<Inst>>(ir);
        Assert.Equal(e, b.Label);
    }

    void Dump(IStructuredControlFlowRegion<Inst> result)
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        result.Dump(l => l.Name ?? "<unnamed>", v => v.Name, isw);
        Output.WriteLine(sw.ToString());
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
                [e] = new(Successor.Conditional(t, f),
                    BasicBlock<Inst>.Create([ShaderInstruction.Const(Literal.Create(true))])),
                [t] = new(Successor.Terminate(), BasicBlock<Inst>.Create([ShaderInstruction.Const(Literal.Create(1))])),
                [f] = new(Successor.Terminate(), BasicBlock<Inst>.Create([ShaderInstruction.Const(Literal.Create(2))])),
            })
        );
        var result = cfg.ToStructuredControlFlow();
        Dump(result);
        // block:
        //   ... e ...
        //   if
        //      ... t ...
        //      return
        //   else
        //      ... f ...
        //      return
        var re = Assert.IsType<Block<Inst>>(result);
        Assert.Equal(2, re.Body.Elements.Length);
        var bbe = Assert.IsType<BasicBlock<Inst>>(re.Body.Elements[0]);
        var rif = Assert.IsType<IfThenElse<Inst>>(re.Body.Elements[1]);
        var rt = Assert.IsType<Block<Inst>>(rif.TrueBody);
        var bbt = Assert.IsType<BasicBlock<Inst>>(rt.Body.Elements[0]);
        var rf = Assert.IsType<Block<Inst>>(rif.FalseBody);
        var bbf = Assert.IsType<BasicBlock<Inst>>(rf.Body.Elements[0]);
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
                [e] = new(Successor.Conditional(t, f), BasicBlock<Inst>.Create([
                    ShaderInstruction.Const(Literal.Create(true))
                ])),
                [t] = new(Successor.Unconditional(m), BasicBlock<Inst>.Create([
                    ShaderInstruction.Const(Literal.Create(1))
                ])),
                [f] = new(Successor.Unconditional(m), BasicBlock<Inst>.Create([
                    ShaderInstruction.Const(Literal.Create(2))
                ])),
                [m] = new(Successor.Terminate(), BasicBlock<Inst>.Create([])),
            })
        );
        var result = cfg.ToStructuredControlFlow();
        // block
        //   block m:
        //      ... e ...
        //      if
        //          ... t ...
        //          br m
        //      else
        //          ... f ...
        //          br m
        //   ... m ...
        var ir = Assert.IsType<Block<Inst>>(result);
        Dump(ir);
        ir.Body.Elements.Should().SatisfyRespectively(
            b0 => b0.Should().BeOfType<Block<Inst>>()
                .Which.Should().Satisfy<Block<Inst>>(b =>
                {
                    b.Label.Should().Be(m);
                    b.Body.Elements.Should().SatisfyRespectively(
                        bb => bb.Should().BeOfType<BasicBlock<Inst>>(),
                        ifThenElse => ifThenElse.Should().BeOfType<IfThenElse<Inst>>()
                    );
                }),
            b1 => b1.Should().BeOfType<Block<Inst>>()
                .Which.Instructions.Should().ContainSingle()
                .Which.Should().BeOfType<ReturnInstruction>()
        );
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

        var v = new VariableDeclaration(DeclarationScope.Function, 0, "v", ShaderType.I32, []);

        var cfg = new ControlFlowGraph<BasicBlock<Inst>>(
            a,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [a] = new(Successor.Unconditional(b),
                    BasicBlock<Inst>.Create([
                        ShaderInstruction.Const(Literal.Create(1)),
                        ShaderInstruction.Store(v)
                    ])),
                [b] = new(Successor.Conditional(a, c), BasicBlock<Inst>.Create([
                    ShaderInstruction.Const(Literal.Create(2)),
                    ShaderInstruction.Store(v),
                    ShaderInstruction.Const(Literal.Create(true))
                ])),
                [c] = new(Successor.Terminate(), BasicBlock<Inst>.Create([
                    ShaderInstruction.Const(Literal.Create(3)),
                    ShaderInstruction.Store(v),
                ])),
            })
        );
        var result = cfg.ToStructuredControlFlow();
        Dump(result);

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

        result.Should().BeOfType<Loop<Inst>>()
            .Which.Body.Elements.Should().SatisfyRespectively(
                (bb) => bb.Should().BeOfType<BasicBlock<Inst>>(),
                b => b.Should().BeOfType<Block<Inst>>()
            );
    }
}