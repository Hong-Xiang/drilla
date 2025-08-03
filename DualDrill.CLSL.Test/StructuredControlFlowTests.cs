using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using FluentAssertions;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

using Inst = IInstruction;

public sealed class StructuredControlFlowTests(ITestOutputHelper Output)
{
    static ITerminatorSemantic<Unit, Label, Unit, ITerminator<Label, Unit>> T { get; } = Terminator.Factory<Label, Unit>();

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
    public void SimpleSingleBasicBlockShouldWork2()
    {
        var e = Label.Create("e");
        var ir = RegionDefinition.Create<ISuccessor>(e, x => x,
            (e, Successor.Terminate()));
        Output.WriteLine(ir.Show());
        Assert.Equal(0, ir.Definition.Body.Count);
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


    [Fact]
    public void SimpleSingleSelfLoopBasicBlockShouldWork2()
    {
        var e = Label.Create("e");
        var ir = RegionDefinition.Create<ISuccessor>(e, x => x,
            (e, Successor.Unconditional(e)));
        Output.WriteLine(ir.Show());
        Assert.Equal(e, ir.Label);
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
                        ifThenElse.TrueBody.Elements.Should().ContainSingle().Which.Should().BeOfType<Block>().Which
                                  .Body.Elements.Should()
                                  .SatisfyRespectively(
                                      tInst => tInst
                                               .Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value
                                               .Should().Be(1),
                                      ret => ret.Should().BeOfType<ReturnResultStackInstruction>()
                                  );
                        ifThenElse.FalseBody.Elements.Should().ContainSingle().Which.Should().BeOfType<Block>().Which
                                  .Body.Elements.Should()
                                  .SatisfyRespectively(
                                      fInst => fInst
                                               .Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value
                                               .Should().Be(2),
                                      ret => ret.Should().BeOfType<ReturnResultStackInstruction>()
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
    public void MinimumIfThenElseShouldWork2()
    {
        var e = Label.Create("e");
        var f = Label.Create("f");
        var t = Label.Create("t");
        var ir = RegionDefinition.Create<ISuccessor>(e, x => x,
            (e, Successor.Conditional(t, f)),
            (t, Successor.Terminate()),
            (f, Successor.Terminate()));
        Output.WriteLine(ir.Show());
        Assert.Equal(2, ir.Bindings.Count());
    }

    [Fact]
    public void MinimumIfThenElseMergeShouldWork2()
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
        var ir = RegionDefinition.Create<ITerminator<Label, Unit>>(e, x => x.ToSuccessor(),
            (e, Terminator.B.BrIf(default(Unit), t, f)),
            (t, Terminator.B.Br<Label, Unit>(m)),
            (f, Terminator.B.Br<Label, Unit>(m)),
            (m, Terminator.B.ReturnVoid<Label, Unit>()));
        Output.WriteLine(ir.Show());
        Assert.Equal(3, ir.Bindings.Count());
        Assert.Equal(m, ir.Bindings.First().Label);
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
                    .Which.Should().BeOfType<ReturnResultStackInstruction>()
        );
    }

    [Fact]
    public void SimpleLoopShouldWork()
    {
        // cfg:
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

        var b0 = Label.Create("b0");
        var b1 = Label.Create("b1");
        var b2 = Label.Create("b2");
        var b3 = Label.Create("b3");

        var v = new VariableDeclaration(DeclarationScope.Function, "v", ShaderType.I32, []);

        var cfg = new ControlFlowGraph<BasicBlock<IInstruction>>(
            b0,
            ControlFlowGraph.CreateDefinitions<BasicBlock<Inst>>(new()
            {
                [b0] = new(Successor.Conditional(b1, b3), BasicBlock<Inst>.Create([
                    ShaderInstruction.Const(Literal.Create(1)),
                    ShaderInstruction.Store(v),
                    ShaderInstruction.Const(Literal.Create(false))
                ])),
                [b1] = new(Successor.Conditional(b2, b3), BasicBlock<Inst>.Create([
                    ShaderInstruction.Const(Literal.Create(true))
                ])),
                [b2] = new(Successor.Unconditional(b1), BasicBlock<Inst>.Create([
                    ShaderInstruction.Const(Literal.Create(2)),
                    ShaderInstruction.Store(v)
                ])),
                [b3] = new(Successor.Terminate(), BasicBlock<Inst>.Create([
                    ShaderInstruction.Load(v),
                ])),
            })
        );

        cfg.Labels().Should().BeEquivalentTo([b0, b1, b2, b3]);
        var dt = cfg.GetDominatorTree();
        dt.GetChildren(b0).Should().BeEquivalentTo([b1, b3]);
        dt.GetChildren(b1).Should().BeEquivalentTo([b2]);
        cfg.IsMergeNode(b0).Should().Be(false);
        cfg.IsMergeNode(b1).Should().Be(true);
        cfg.IsMergeNode(b2).Should().Be(false);
        cfg.IsMergeNode(b3).Should().Be(true);

        // expected
        // block @b0
        //   ... b0 ...
        //   if
        //      loop @b1
        //          ...b1
        //          if
        //              ... b2 ...
        //              br b1
        //          else
        //          br b0
        // block @b3
        //   ...b3...

        var result = cfg.ToStructuredControlFlow();

        Dump(result);
    }

    [Fact]
    public void SimpleLoopShouldWork2()
    {
        // cfg:
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

        var b0 = Label.Create("b0");
        var b1 = Label.Create("b1");
        var b2 = Label.Create("b2");
        var b3 = Label.Create("b3");

        // block ^b0
        //    block ^b3
        //      ret
        //    loop ^b1
        //      block ^b2
        //        br b1
        //      brif b2 b3
        //    brif b1 b3 
        var ir = RegionDefinition.Create<ISuccessor>(b0, x => x,
            (b0, Successor.Conditional(b1, b3)),
            (b1, Successor.Conditional(b2, b3)),
            (b2, Successor.Unconditional(b1)),
            (b3, Successor.Terminate())
            );

        Output.WriteLine(ir.Show());
        Assert.Equal(2, ir.Bindings.Count());
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

    [Fact]
    public void MinimumConditionalLoopShouldWork2()
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

        var ir = RegionDefinition.Create<ITerminator<Label, Unit>>(a, x => x.ToSuccessor(),
            (a, Terminator.B.Br<Label, Unit>(b)),
            (b, Terminator.B.BrIf(default(Unit), a, c)),
            (c, Terminator.B.ReturnVoid<Label, Unit>()));

        Output.WriteLine(ir.Show());
        var rb = Assert.Single(ir.Bindings);
    }
}