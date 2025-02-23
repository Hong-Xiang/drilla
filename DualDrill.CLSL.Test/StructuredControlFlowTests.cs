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

using Inst = IInstruction;

public sealed class StructuredControlFlowTests(ITestOutputHelper Output)
{
    [Fact]
    public void SimpleSingleBasicBlockShouldWork()
    {
        var e = Label.Create("e");
        var cfg = new ControlFlowGraph<BasicBlock<IInstruction>>(
            e,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [e] = new(Successor.Terminate(), BasicBlock<Inst>.Create([]))
            })
        );
        var ir = cfg.ToStructuredControlFlow();
        Dump(ir);
        var b = Assert.IsType<Block>(ir);
    }

    [Fact]
    public void SimpleSingleSelfLoopBasicBlockShouldWork()
    {
        var e = Label.Create("e");
        var v = new VariableDeclaration(DeclarationScope.Function, "x", ShaderType.I32, []);
        var cfg = new ControlFlowGraph<BasicBlock<IInstruction>>(
            e,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [e] = new(Successor.Unconditional(e), BasicBlock<Inst>.Create([
                    ShaderInstruction.Load(v),
                    ShaderInstruction.Const(new I32Literal(1)),
                    BinaryExpressionOperationInstruction<
                        NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>>.Instance,
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
        var b = Assert.IsType<Loop>(ir);
        Assert.Equal(e, b.Label);
    }

    void Dump(IStructuredControlFlowRegion result)
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
        var cfg = new ControlFlowGraph<BasicBlock<IInstruction>>(
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
        result.Should().Satisfy<Block>(bodyBlock =>
        {
            bodyBlock.Body.Elements.Should().SatisfyRespectively(
                eInst => eInst.Should().BeOfType<ConstInstruction<BoolLiteral>>().Which.Literal.Value.Should().Be(true),
                ifRegion => ifRegion.Should().Satisfy<IfThenElse>(
                    ifThenElse =>
                    {
                        ifThenElse.TrueBody.Elements.Should().ContainSingle().Which.Should().BeOfType<Block>().Which.Body.Elements.Should()
                                  .SatisfyRespectively(
                                      tInst => tInst
                                               .Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value
                                               .Should().Be(1),
                                      ret => ret.Should().BeOfType<ReturnInstruction>()
                                  );
                        ifThenElse.FalseBody.Elements.Should().ContainSingle().Which.Should().BeOfType<Block>().Which.Body.Elements.Should()
                                  .SatisfyRespectively(
                                      fInst => fInst.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value
                                                    .Should().Be(2),
                                      ret => ret.Should().BeOfType<ReturnInstruction>()
                                  );
                    })
            );
        });

        // var re = Assert.IsType<Block<Inst>>(result);
        // Assert.Equal(2, re.Body.Elements.Length);
        // var bbe = Assert.IsType<ConstInstruction<BoolLiteral>>(re.Body.Elements[0]);
        // var rif = Assert.IsType<IfThenElse<Inst>>(re.Body.Elements[1]);
        // var rt = Assert.IsType<Block<Inst>>(rif.TrueBody.Elements);
        // var bbt = Assert.IsType<BasicBlock<Inst>>(rt.Body.Elements[0]);
        // var rf = Assert.IsType<Block<Inst>>(rif.FalseBody);
        // var bbf = Assert.IsType<BasicBlock<Inst>>(rf.Body.Elements[0]);
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
        var cfg = new ControlFlowGraph<BasicBlock<IInstruction>>(
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
        var ir = Assert.IsType<Block>(result);
        Dump(ir);
        ir.Body.Elements.Should().SatisfyRespectively(
            b0 => b0.Should().BeOfType<Block>()
                    .Which.Should().Satisfy<Block>(b =>
                    {
                        b.Label.Should().Be(m);
                        b.Body.Elements.Should().SatisfyRespectively(
                            val => val.Should().BeOfType<ConstInstruction<BoolLiteral>>().Which.Literal.Value.Should()
                                      .Be(true),
                            ifThenElse => ifThenElse.Should().BeOfType<IfThenElse>()
                        );
                    }),
            b1 => b1.Should().BeOfType<Block>()
                    .Which.Body.Elements.Should().ContainSingle()
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

        var v = new VariableDeclaration(DeclarationScope.Function, "v", ShaderType.I32, []);

        var cfg = new ControlFlowGraph<BasicBlock<IInstruction>>(
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
        //   block:
        //      ... b ...
        //      if
        //          br a (continue)
        //      else
        //          ... c ...
        //          return
        //   end (if)
        // end (loop)

        result.Should().BeOfType<Loop>()
              .Which.Body.Elements.Should().SatisfyRespectively(
                  x => x.Should().BeOfType<ConstInstruction<I32Literal>>(),
                  x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
                  (b) => b.Should().BeOfType<Block>()
              );
    }
}