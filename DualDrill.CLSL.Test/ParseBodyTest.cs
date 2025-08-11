using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using FluentAssertions;
using System.CodeDom.Compiler;
using System.Numerics;
using System.Reflection;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public class ParseBodyTest(ITestOutputHelper Output)
{
    FunctionBody4 ParseMethod(FunctionDeclaration f, MethodBase m)
    {
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(m), f);
        var parameters = m.GetParameters();
        foreach (var (ip, p) in f.Parameters.Index())
        {
            context.AddParameter(Symbol.Parameter(parameters[ip]), p);
        }

        var parser = new RuntimeReflectionParser(context);
        var result = parser.ParseMethodBody3(f);
        Output.WriteLine(result.Dump());
        return result;
    }

    void AssertBasicBlockEquals(StackIRBasicBlock expected, StackIRBasicBlock actual)
    {
        Assert.Equal(expected.Label, actual.Label);
        Assert.Equal(expected.Terminator, actual.Terminator);
        Assert.Equal(expected.Inputs, actual.Inputs);
        Assert.Equal(expected.Outputs, actual.Outputs);
        expected.Instructions.Should().Equal(actual.Instructions);
    }


    [Fact]
    public void ParseBasicLiteralExpressionBodyShouldWork()
    {
        var f = new FunctionDeclaration(nameof(DevelopTestShaderModule.Return42), [],
            new FunctionReturn(ShaderType.I32, []), []);
        var result = ParseMethod(f, MethodHelper.GetMethod(DevelopTestShaderModule.Return42));
    }


    [Fact]
    public void VectorComponentSetShouldWork()
    {
        var f = new FunctionDeclaration(nameof(DevelopTestShaderModule.SetComponent), [
                new ParameterDeclaration("x", ShaderType.F32, []),
                new ParameterDeclaration("y", ShaderType.F32, [])
            ],
            new FunctionReturn(ShaderType.Vec2F32, []), []);
        var result = ParseMethod(f,
            MethodHelper.GetMethod<float, float, vec2f32>(DevelopTestShaderModule.SetComponent));
        //var entry = result[result.Entry];

        //result[result.Entry].Elements.Should().SatisfyRespectively(
        //    s => s.Should().BeOfType<ConstInstruction<F32Literal>>()
        //          .Which.Literal.Value.Should().Be(0.0f),
        //    s => s.Should().BeOfType<CallInstruction>()
        //          .Which.Callee.Return.Type.Should().Be(ShaderType.Vec2F32),
        //    s => s.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //    s => s.Should().BeOfType<LoadSymbolAddressInstruction<VariableDeclaration>>(),
        //    s => s.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(f.Parameters[0]),
        //    s => s.Should()
        //          .BeOfType<BinaryStatementOperationInstruction<
        //              VectorComponentSetOperation<N2, VecType<N2, FloatType<N32>>, Swizzle.X>>>(),
        //    s => s.Should().BeOfType<LoadSymbolAddressInstruction<VariableDeclaration>>(),
        //    s => s.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(f.Parameters[1]),
        //    s => s.Should()
        //          .BeOfType<BinaryStatementOperationInstruction<
        //              VectorComponentSetOperation<N2, VecType<N2, FloatType<N32>>, Swizzle.Y>>>(),
        //    s => s.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //    s => s.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //    s => s.Should().BeOfType<BrInstruction>()
        //);

        //result[result.Entry].Successor.Should().BeOfType<UnconditionalSuccessor>()
        //                    .Which.Target.Should().Satisfy<Label>(l =>
        //                    {
        //                        var successor = result[l];
        //                        successor
        //                            .Should().BeAssignableTo<IBasicBlock2<IInstruction, IShaderType, IShaderType>>()
        //                            .Which.Elements.Should().SatisfyRespectively(
        //                                x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //                                x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //                            );
        //                    });


        // entry.Elements.Should().SatisfyRespectively(
        //     s => s.Should().BeOfType<SimpleAssignmentStatement>(),
        //     s => s.Should().Satisfy<VectorComponentSetStatement<N2, FloatType<N32>, Swizzle.X>>(assign =>
        //     {
        //         assign.Target.Should().BeOfType<AddressOfExpression>()
        //               .Which.Base.Should().BeOfType<VariableIdentifierExpression>();
        //         assign.Value.Should().Satisfy<FormalParameterExpression>(e => e.Parameter.Should().Be(f.Parameters[0]));
        //     }),
        //     s => s.Should().Satisfy<VectorComponentSetStatement<N2, FloatType<N32>, Swizzle.Y>>(assign =>
        //     {
        //         assign.Target.Should().BeOfType<AddressOfExpression>()
        //               .Which.Base.Should().BeOfType<VariableIdentifierExpression>();
        //         assign.Value.Should().Satisfy<FormalParameterExpression>(e => e.Parameter.Should().Be(f.Parameters[1]));
        //     }),
        //     s => s.Should().BeOfType<SimpleAssignmentStatement>()
        // );
    }

    [Fact]
    public void BroadcastVectorMulShouldWork()
    {
        var f = new FunctionDeclaration(nameof(DevelopTestShaderModule.BroadcastVectorOperation), [
                new ParameterDeclaration("a", ShaderType.Vec2F32, []),
                new ParameterDeclaration("b", ShaderType.F32, [])
            ],
            new FunctionReturn(ShaderType.Vec2F32, []), []);
        var result = ParseMethod(f,
            MethodHelper.GetMethod<vec2f32, float, vec2f32>(DevelopTestShaderModule.BroadcastVectorOperation));
        //var entry = result[result.Entry];
        //entry.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(f.Parameters[0]),
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(f.Parameters[1]),
        //    x => x.Should().BeOfType<BinaryExpressionOperationInstruction<
        //        VectorScalarExpressionNumericOperation<N2, FloatType<N32>, BinaryArithmetic.Mul>>>(),
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);
    }

    [Fact]
    public void MinimumLoadArgumentShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.LoadArg),
            [a],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod<int, int>(DevelopTestShaderModule.LoadArg));
        //var entry = result[result.Entry];
        //entry.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(a),
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);
    }


    [Fact]
    public void LoadArgPlus1ShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.APlus1),
            [a],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod<int, int>(DevelopTestShaderModule.APlus1));
        //var entry = result[result.Entry];

        //entry.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(a),
        //    x => x.Should().BeOfType<ConstInstruction<I32Literal>>()
        //          .Which.Literal.Value.Should().Be(1),
        //    x => x.Should().BeOfType<BinaryExpressionOperationInstruction<
        //        NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>>>(),
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);
    }


    [Fact]
    public void SystemNumericVector4ConstructionShouldWork()
    {
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.SystemNumericVector4Creation),
            [],
            new FunctionReturn(VecType<N4, FloatType<N32>>.Instance, []),
            []
        );
        var result = ParseMethod(f, MethodHelper.GetMethod(DevelopTestShaderModule.SystemNumericVector4Creation));
        //var entry = result[result.Entry];
        //entry.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<ConstInstruction<F32Literal>>()
        //          .Which.Literal.Value.Should().Be(1.0f),
        //    x => x.Should().BeOfType<ConstInstruction<F32Literal>>()
        //          .Which.Literal.Value.Should().Be(2.0f),
        //    x => x.Should().BeOfType<ConstInstruction<F32Literal>>()
        //          .Which.Literal.Value.Should().Be(3.0f),
        //    x => x.Should().BeOfType<ConstInstruction<F32Literal>>()
        //          .Which.Literal.Value.Should().Be(4.0f),
        //    x => x.Should().BeOfType<CallInstruction>()
        //          .Which.Callee.Return.Type.Should().Be(VecType<N4, FloatType<N32>>.Instance),
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);
    }

    [Fact]
    public void ParseImplicitConvertUIntMaxShouldWork()
    {
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.ImplicitConvertUIntMax),
            [new ParameterDeclaration("a", ShaderType.U32, [])],
            new FunctionReturn(ShaderType.Bool, []),
            []
        );
        var result = ParseMethod(f,
            MethodHelper.GetMethod<uint, bool>(DevelopTestShaderModule.ImplicitConvertUIntMax));

        //var entry = result[result.Entry];
        //entry.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(f.Parameters[0]),
        //    x => x.Should().BeOfType<ConstInstruction<I32Literal>>()
        //          .Which.Literal.Value.Should().Be(-1),
        //    x => x.Should().BeOfType<BinaryExpressionOperationInstruction<
        //        NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>>>(),
        //    x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //    x => x.Should().BeOfType<BrInstruction>()
        //);

        //entry.Successor.Should().BeOfType<UnconditionalSuccessor>()
        //     .Which.Target.Should().Satisfy<Label>(l =>
        //     {
        //         var successor = result[l];
        //         successor.Should().BeAssignableTo<IBasicBlock2<IInstruction, IShaderType, IShaderType>>()
        //                  .Which.Elements.Should().SatisfyRespectively(
        //                      x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //                      x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //                  );
        //     });
    }

    [Fact]
    public void BasicMethodInvocationParseShouldWork()
    {
        var context = CompilationContext.Create();
        var fAdd = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.Add),
            [
                new ParameterDeclaration("a", ShaderType.I32, []),
                new ParameterDeclaration("b", ShaderType.I32, [])
            ],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var add = MethodHelper.GetMethod<int, int, int>(DevelopTestShaderModule.Add);
        context.AddFunctionDeclaration(Symbol.Function(add), fAdd);
        var fCall = new FunctionDeclaration(nameof(BasicMethodInvocationParseShouldWork), [],
            new FunctionReturn(ShaderType.I32, []), []);
        var method = MethodHelper.GetMethod(DevelopTestShaderModule.MethodInvocation);
        context.AddFunctionDefinition(Symbol.Function(method), fCall);
        var parser = new RuntimeReflectionParser(context);
        var result = parser.ParseMethodBody3(fCall);
        Output.WriteLine(result.Dump());
        //DumpNew(result);
        //var entry = result[result.Entry];

        //entry.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<ConstInstruction<I32Literal>>()
        //          .Which.Literal.Value.Should().Be(1),
        //    x => x.Should().BeOfType<ConstInstruction<I32Literal>>()
        //          .Which.Literal.Value.Should().Be(2),
        //    x => x.Should().BeOfType<CallInstruction>().Which.Callee.Should().Be(fAdd),
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);
    }


    [Fact]
    public void ParseMaxByIfThenElseShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var b = new ParameterDeclaration("b", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.MaxByIfThenElse),
            [a, b],
            new FunctionReturn(ShaderType.I32, []),
            []
        );
        var result = ParseMethod(f, MethodHelper.GetMethod<int, int, int>(DevelopTestShaderModule.MaxByIfThenElse));
        //  IL_0000: nop
        //  IL_0001: ldarg.0
        //  IL_0002: ldarg.1
        //  IL_0003: clt
        //  IL_0005: ldc.i4.0
        //  IL_0006: ceq
        //  IL_0008: stloc.0
        //  IL_0009: ldloc.0
        //  IL_000a: brfalse.s IL_0011

        //  IL_000c: nop
        //  IL_000d: ldarg.0
        //  IL_000e: stloc.1
        //  IL_000f: br.s IL_0016

        //  IL_0011: nop
        //  IL_0012: ldarg.1
        //  IL_0013: stloc.1
        //  IL_0014: br.s IL_0016

        //  IL_0016: ldloc.1
        //  IL_0017: ret

        var labels = result.DeclarationContext.Labels;
        labels.Should().HaveCount(4);
        labels[0].Should().Be(result.Entry);
        var l11 = labels[1];
        var l0c = labels[2];
        var l16 = labels[3];

        result[result.Entry].Successor.Should().Satisfy<ConditionalSuccessor>(sIf =>
        {
            sIf.TrueTarget.Should().Be(l11);
            sIf.FalseTarget.Should().Be(l0c);
        });

        result.Successor(l11).Should().BeOfType<UnconditionalSuccessor>().Which.Target.Should().Be(l16);
        result.Successor(l0c).Should().BeOfType<UnconditionalSuccessor>().Which.Target.Should().Be(l16);

        //var variables = result.DeclarationContext.LocalVariables;
        //variables.Should().HaveCount(2);
        //var loc0 = variables[0];
        //var loc1 = variables[1];


        // entryBB.Elements.Should().SatisfyRespectively(
        //     s => s.Should().Satisfy<SimpleAssignmentStatement>(s_ =>
        //     {
        //         s_.L.Should().BeOfType<VariableIdentifierExpression>()
        //           .Which.Variable.Should().Be(loc0);
        //         s_.R.Should().Satisfy<BinaryOperationExpression<LogicalBinaryOperation<BinaryRelational.Eq>>>(elt =>
        //         {
        //             elt.L.Should()
        //                .Satisfy<BinaryOperationExpression<
        //                    NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>>>(
        //                    clt =>
        //                    {
        //                        clt.L.Should().BeOfType<FormalParameterExpression>()
        //                           .Which.Parameter.Should().Be(a);
        //                        clt.R.Should().BeOfType<FormalParameterExpression>()
        //                           .Which.Parameter.Should().Be(b);
        //                    }
        //                );
        //             elt.R.Should().BeOfType<LiteralValueExpression>()
        //                .Which.Literal.Should().BeOfType<BoolLiteral>()
        //                .Which.Value.Should().Be(false);
        //         });
        //     }),
        //     s => s.Should().BeOfType<PushStatement>()
        //           .Which.Expr.Should().BeOfType<UnaryOperationExpression<LogicalNotOperation>>()
        //           .Which.Source.Should().BeOfType<VariableIdentifierExpression>()
        //           .Which.Variable.Should().Be(loc0)
        // );

        // result[l0c].Elements.Should().ContainSingle()
        //            .Which.Should().Satisfy<SimpleAssignmentStatement>(
        //                s =>
        //                {
        //                    s.L.Should().BeOfType<VariableIdentifierExpression>()
        //                     .Which.Variable.Should().Be(loc1);
        //                    s.R.Should().BeOfType<FormalParameterExpression>()
        //                     .Which.Parameter.Should().Be(a);
        //                }
        //            );

        // result[l11].Elements.Should().ContainSingle()
        //            .Which.Should().Satisfy<SimpleAssignmentStatement>(
        //                s =>
        //                {
        //                    s.L.Should().BeOfType<VariableIdentifierExpression>()
        //                     .Which.Variable.Should().Be(loc1);
        //                    s.R.Should().BeOfType<FormalParameterExpression>()
        //                     .Which.Parameter.Should().Be(b);
        //                }
        //            );
        // result[l16].Elements.Should().ContainSingle()
        //            .Which.Should().BeOfType<PushStatement>()
        //            .Which.Expr.Should().BeOfType<VariableIdentifierExpression>()
        //            .Which.Variable.Should().Be(loc1);

        // ImmutableArray<LabelInstruction> labels = [.. result.Instructions.OfType<LabelInstruction>()];
        //
        // labels.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        //
        // var l11 = labels[0];
        // var l16 = labels[1];
        //
        //result[result.Entry].Elements.Should().SatisfyRespectively(
        //    //  IL_0000: nop
        //    // x => x.Should().BeOfType<NopInstruction>(),
        //    //  IL_0001: ldarg.0
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //    //  IL_0002: ldarg.1
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        //    // IL_0003: clt
        //    x => x.Should()
        //          .BeOfType<BinaryExpressionOperationInstruction<
        //              NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>>>(),
        //    // IL_0005: ldc.i4.0
        //    x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(0),
        //    // IL_0006: ceq
        //    x => x.Should()
        //          .BeOfType<BinaryExpressionOperationInstruction<
        //              NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>>>(),
        //    // IL_0008: stloc.0
        //    x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //    // IL_0009: ldloc.0
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //    x => x.Should().BeOfType<UnaryExpressionOperationInstruction<LogicalNotOperation>>(),
        //    // IL_000a: brfalse.s IL_0011
        //    x =>
        //    {
        //        var bif = x.Should().BeOfType<BrIfInstruction>();
        //        bif.Which.TrueTarget.Should().Be(l11);
        //        bif.Which.FalseTarget.Should().Be(l0c);
        //    }
        //);
        //result[l0c].Elements.Should().SatisfyRespectively(
        //    // IL_000c: nop
        //    // x => x.Should().BeOfType<NopInstruction>(),
        //    // IL_000d: ldarg.0
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //    // IL_000e: stloc.1
        //    x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //    // IL_000f: br.s IL_0016
        //    x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l16)
        //);
        //result[l11].Elements.Should().SatisfyRespectively(
        //    // x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l11),
        //    // IL_0011: nop
        //    // x => x.Should().BeOfType<NopInstruction>(),
        //    // IL_0012: ldarg.1
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        //    // IL_0013: stloc.1
        //    x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //    // IL_0014: br.s IL_0016
        //    x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l16)
        //);
        //result[l16].Elements.Should().SatisfyRespectively(
        //    // x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l16),
        //    // IL_0016: ldloc.1
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //    // IL_0017: ret
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);
    }

    [Fact]
    public void VectorSwizzleGetterShouldWork()
    {
        //  IL_0000: nop
        //  IL_0001: ldarga.s a
        //  IL_0003: swizzle.get.vec4f32.xyx [ref<vec4f32>] -> [vec3f32]
        //  IL_0008: stloc.0
        //  IL_0009: br.s IL_000b

        //  IL_000b: ldloc.0
        //  IL_000c: ret

        var v = new ParameterDeclaration("v", VecType<N2, FloatType<N32>>.Instance, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.VecSwizzleGetter),
            [v],
            new FunctionReturn(VecType<N3, FloatType<N32>>.Instance, []),
            []
        );
        var result = ParseMethod(f,
            MethodHelper.GetMethod<vec2f32, vec3f32>(DevelopTestShaderModule.VecSwizzleGetter));

        var entry = result[result.Entry];

        // entry.Elements.Should().ContainSingle()
        //      .Which.Should().BeOfType<PushStatement>()
        //      .Which.Expr.Should().Satisfy<IUnaryExpression>(e =>
        //      {
        //          e.Operation.Should().BeOfType<VectorSwizzleGetOperation<
        //              Swizzle.Pattern<N2, Swizzle.X, Swizzle.Y, Swizzle.X>,
        //              FloatType<N32>
        //          >>();
        //      });


        //result[result.Entry].Elements.Should().SatisfyRespectively(
        //    //  IL_0000: nop
        //    // x => x.Should().BeOfType<NopInstruction>(),
        //    //  IL_0001: ldarga.s a
        //    x => x.Should().BeOfType<LoadSymbolAddressInstruction<ParameterDeclaration>>().Which.Target.Should().Be(v),
        //    //  IL_0003: swizzle.vec4f32.xyx [ref<vec4f32>] -> [vec3f32]
        //    x => x.Should()
        //          .BeOfType<
        //              UnaryExpressionOperationInstruction<
        //                  VectorSwizzleGetExpressionOperation<Swizzle.Pattern<N2, Swizzle.X, Swizzle.Y, Swizzle.X>,
        //                      FloatType<N32>>>>(),
        //    //  IL_0008: stloc.0
        //    // x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //    //  IL_0009: br.s IL_000b
        //    // x => x.Should().BeOfType<BrInstruction>(),
        //    // x => x.Should().BeOfType<LabelInstruction>(),
        //    //  IL_000b: ldloc.0
        //    // x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //    //  IL_000c: ret
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);
    }


    [Fact]
    public void VectorSwizzleSetShouldWork()
    {
        var pa = new ParameterDeclaration("a", ShaderType.Vec4F32, []);
        var pb = new ParameterDeclaration("b", ShaderType.Vec2F32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.VecSwizzleSetter),
            [pa, pb],
            new FunctionReturn(ShaderType.Vec4F32, []),
            []);
        var result = ParseMethod(f,
            MethodHelper.GetMethod<vec4f32, vec2f32, vec4f32>(DevelopTestShaderModule.VecSwizzleSetter));

        //result.DeclarationContext.LocalVariables.Should().ContainSingle();

        //var loc0 = result.DeclarationContext.LocalVariables[0];
        //loc0.Type.Should().Be(ShaderType.Vec4F32);

        var entry = result[result.Entry];

        //entry.Elements.Should().SatisfyRespectively(
        //    // x => x.Should().BeOfType<NopInstruction>(),
        //    //  IL_0001: ldarga @a
        //    x => x.Should().BeOfType<LoadSymbolAddressInstruction<ParameterDeclaration>>().Which.Target.Should().Be(pa),
        //    //  IL_0003: ldarg @b
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(pb),
        //    //  IL_0005: swizzle.set.vec4f32.xy
        //    x => x.Should()
        //          .BeOfType<BinaryStatementOperationInstruction<VectorSwizzleSetOperation<
        //              Swizzle.Pattern<N4, Swizzle.X, Swizzle.Y>, FloatType<N32>>>>(),
        //    //  IL_0010: ldarg.0
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(pa),
        //    //  IL_0011: stloc.0
        //    x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //    //  IL_0012: br IL_0014
        //    x => x.Should().BeOfType<BrInstruction>()
        //    // s =>
        //    // {
        //    //     s.Should()
        //    //      .Satisfy<VectorSwizzleSetStatement<N4, FloatType<N32>, Swizzle.Pattern<N4, Swizzle.X, Swizzle.Y>>>(
        //    //          assign =>
        //    //          {
        //    //              assign.Target.Should().BeOfType<AddressOfExpression>()
        //    //                    .Which.Base.Should().BeOfType<FormalParameterExpression>()
        //    //                    .Which.Parameter.Should().Be(pa);
        //    //              assign.Value.Should().Satisfy<FormalParameterExpression>(e => e.Parameter.Should().Be(pb));
        //    //          });
        //    // },
        //    // s =>
        //    // {
        //    //     s.Should().Satisfy<SimpleAssignmentStatement>(assign =>
        //    //     {
        //    //         assign.L.Should().BeOfType<VariableIdentifierExpression>()
        //    //               .Which.Variable.Should().Be(loc0);
        //    //         assign.R.Should().BeOfType<FormalParameterExpression>()
        //    //               .Which.Parameter.Should().Be(pa);
        //    //     });
        //    // }
        //);

        //entry.Successor.Should().Satisfy<UnconditionalSuccessor>(s =>
        //{
        //    result[s.Target].Elements.Should().SatisfyRespectively(
        //        x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //        x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //    );
        //    // var bb = result[s.Target];
        //    // bb.Elements.Should().ContainSingle()
        //    //   .Which.Should().BeOfType<PushStatement>()
        //    //   .Which.Expr.Should().Satisfy<VariableIdentifierExpression>(e => e.Variable.Should().Be(loc0));
        //});


        ////  IL_0000: nop
        ////  IL_0001: ldarga @a
        ////  IL_0003: ldarg @b
        ////  IL_0005: swizzle.set.vec4f32.xy
        ////  IL_000f: nop
        ////  IL_0010: ldarg.0
        ////  IL_0011: stloc.0
        ////  IL_0012: br.s IL_0014

        ////  IL_0014: ldloc.0
        ////  IL_0015: ret


        //// result[result.Entry].Elements.Should().SatisfyRespectively(
        ////     //  IL_0000: nop
        ////     x => x.Should().BeOfType<NopInstruction>(),
        ////     //  IL_0001: ldarga @a
        ////     x => x.Should().BeOfType<LoadSymbolAddressInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        ////     //  IL_0003: ldarg @b
        ////     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        ////     //  IL_0005: swizzle.set.vec4f32.xy
        ////     x => x.Should()
        ////           .BeOfType<VectorSwizzleSetOperation<Swizzle.Pattern<N4, Swizzle.X, Swizzle.Y>, FloatType<N32>>>(),
        ////     //  IL_000f: nop
        ////     x => x.Should().BeOfType<NopInstruction>(),
        ////     //  IL_0010: ldarg.0
        ////     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        ////     //  IL_0011: stloc.0
        ////     x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        ////     //  IL_0012: br IL_0014
        ////     x => x.Should().BeOfType<BrInstruction>(),
        ////     // x => x.Should().BeOfType<LabelInstruction>(),
        ////     //  IL_0014: ldloc.0
        ////
        //// );
    }

    [Fact]
    public void Vec4DotTest()
    {
        var a = new ParameterDeclaration("a", ShaderType.Vec4F32, []);
        var b = new ParameterDeclaration("b", ShaderType.Vec4F32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.Vector4Dot),
            [a, b],
            new FunctionReturn(ShaderType.F32, []),
            []);
        var result = ParseMethod(f,
            MethodHelper.GetMethod<Vector4, Vector4, float>(DevelopTestShaderModule.Vector4Dot));
        var entry = result[result.Entry];

        // entry.Elements.Should().ContainSingle()
        //      .Which.Should().BeOfType<PushStatement>()
        //      .Which.Expr.Should().Satisfy<FunctionCallExpression>(
        //          call =>
        //          {
        //              call.Callee.Return.Type.Should().BeOfType<FloatType<N32>>();
        //              call.Callee.Parameters.Should().HaveCount(2)
        //                  .And.OnlyContain(p => p.Type is VecType<N4, FloatType<N32>>);
        //              call.Arguments.Should().SatisfyRespectively(
        //                  x => x.Should().BeOfType<FormalParameterExpression>()
        //                        .Which.Parameter.Should().Be(a),
        //                  x => x.Should().BeOfType<FormalParameterExpression>()
        //                        .Which.Parameter.Should().Be(b)
        //              );
        //          }
        //      );

        // //  IL_0000: ldarg.0
        // //  IL_0001: ldarg.1
        // //  IL_0002: call dot.vec4f32
        // //  IL_0007: ret
        //result[result.Entry].Elements.Should().SatisfyRespectively(
        //    //  IL_0000: ldarg.0
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //    //  IL_0001: ldarg.1
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        //    //  IL_0002: call dot.vec4f32
        //    x => x.Should().BeOfType<CallInstruction>(),
        //    //  IL_0007: ret
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);
    }

    [Fact]
    public void ParseMaxByTernaryOperatorShouldWork()
    {
        //        IL_0000: nop
        //        IL_0001: ldarg.0      // a
        //        IL_0002: ldarg.1      // b
        //        IL_0003: bge.s        IL_0008

        //        IL_0005: ldarg.1      // b
        //        IL_0006: br.s         IL_0009

        //        IL_0008: ldarg.0      // a

        //        IL_0009: stloc.0      // V_0
        //        IL_000a: br.s         IL_000c

        //        IL_000c: ldloc.0      // V_0
        //        IL_000d: ret

        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var b = new ParameterDeclaration("b", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.MaxByTernaryOperator),
            [a, b],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var result = ParseMethod(f,
            MethodHelper.GetMethod<int, int, int>(DevelopTestShaderModule.MaxByTernaryOperator));

        var labels = result.DeclarationContext.Labels;
        labels.Should().HaveCount(5);

        var l0 = result.Entry;
        var l5 = labels[2];
        var l8 = labels[1];
        var l9 = labels[3];
        var lc = labels[4];

        result.Successor(l0).Should().Satisfy<ConditionalSuccessor>(s =>
        {
            s.TrueTarget.Should().Be(l8);
            s.FalseTarget.Should().Be(l5);
        });
        result.Successor(l5).Should().BeOfType<UnconditionalSuccessor>()
              .Which.Target.Should().Be(l9);
        result.Successor(l8).Should().BeOfType<UnconditionalSuccessor>()
              .Which.Target.Should().Be(l9);
        result.Successor(l9).Should().BeOfType<UnconditionalSuccessor>()
              .Which.Target.Should().Be(lc);
        var ebb = result[result.Entry];
        var eSucc = Assert.IsType<ConditionalSuccessor>(ebb.Successor);
        var tbb = result[eSucc.TrueTarget];
        var fbb = result[eSucc.FalseTarget];
        var mbb = result[Assert.IsType<UnconditionalSuccessor>(tbb.Successor).Target];
        fbb.Successor.Should().BeOfType<UnconditionalSuccessor>()
           .Which.Target.Should().Be(mbb.Label);
        var rbb = result[Assert.IsType<UnconditionalSuccessor>(mbb.Successor).Target];

        var variables = result.DeclarationContext.LocalVariables;

        //ebb.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(a),
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(b),
        //    x => x.Should().BeOfType<BinaryExpressionOperationInstruction<
        //        NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Ge>>>(),
        //    x => x.Should().BeOfType<BrIfInstruction>()
        //);

        //tbb.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(a),
        //    x => x.Should().BeOfType<BrInstruction>()
        //);

        //fbb.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>()
        //          .Which.Target.Should().Be(b),
        //    x => x.Should().BeOfType<BrInstruction>()
        //);

        //mbb.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>()
        //          .Which.Target.Should().Be(variables[0]),
        //    x => x.Should().BeOfType<BrInstruction>()
        //);

        //rbb.Elements.Should().SatisfyRespectively(
        //    x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>()
        //          .Which.Target.Should().Be(variables[0]),
        //    x => x.Should().BeOfType<ReturnResultStackInstruction>()
        //);


        // result[l0].Elements.Should().ContainSingle()
        //           .Which.Should().BeOfType<PushStatement>()
        //           .Which.Expr.Should().Satisfy<IBinaryExpression>(e =>
        //           {
        //               e.Operation.Should().Satisfy<IBinaryExpressionOperation>(operation =>
        //               {
        //                   operation.LeftType.Should().BeOfType<IntType<N32>>();
        //                   operation.RightType.Should().BeOfType<IntType<N32>>();
        //                   operation.BinaryOp.Should().BeOfType<BinaryRelational.Ge>();
        //               });
        //               e.L.Should().BeOfType<FormalParameterExpression>()
        //                .Which.Parameter.Should().Be(a);
        //               e.R.Should().BeOfType<FormalParameterExpression>()
        //                .Which.Parameter.Should().Be(b);
        //           });

        // result[l5].Elements.Should().ContainSingle()
        //           .Which.Should().Satisfy<SimpleAssignmentStatement>(
        //               s =>
        //               {
        //                   s.L.Should().BeOfType<VariableIdentifierExpression>()
        //                    .Which.Variable.Should().Be(variables[0]);
        //                   s.R.Should().BeOfType<FormalParameterExpression>()
        //                    .Which.Parameter.Should().Be(b);
        //               }
        //           );

        // result[l8].Elements.Should().ContainSingle()
        //           .Which.Should().Satisfy<SimpleAssignmentStatement>(
        //               s =>
        //               {
        //                   s.L.Should().BeOfType<VariableIdentifierExpression>()
        //                    .Which.Variable.Should().Be(variables[1]);
        //                   s.R.Should().BeOfType<FormalParameterExpression>()
        //                    .Which.Parameter.Should().Be(a);
        //               }
        //           );

        // result[l9].Elements.Should().ContainSingle()
        //           .Which.Should().Satisfy<SimpleAssignmentStatement>(
        //               s =>
        //               {
        //                   s.L.Should().BeOfType<VariableIdentifierExpression>()
        //                    .Which.Variable.Should().Be(variables[2]);
        //                   s.R.Should().BeOfType<VariableIdentifierExpression>()
        //                    .Which.Variable.Should().Be(variables[3]);
        //               }
        //           );

        // result[lc].Elements.Should().ContainSingle()
        //           .Which.Should().BeOfType<PushStatement>()
        //           .Which.Expr.Should().BeOfType<VariableIdentifierExpression>()
        //           .Which.Variable.Should().Be(variables[2]);

        // var labels = result.Instructions.OfType<LabelInstruction>().ToArray();
        // labels.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        //
        // var l8 = labels[0];
        // var l9 = labels[1];
        // var lc = labels[2];
        //
        // result.Instructions.Should().SatisfyRespectively(
        //     //  IL_0000: nop
        //     x => x.Should().BeOfType<NopInstruction>(),
        //     //  IL_0001: ldarg.1
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //     //  IL_0002: ldc.i4.0
        //     x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(0),
        //     //  IL_0003: ble.s IL_0008
        //     x => x.Should()
        //           .BeOfType<BinaryExpressionOperationInstruction<
        //               NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Le>>>(),
        //     x => x.Should().BeOfType<BrIfInstruction>().Which.Target.Should().Be(l8),
        //
        //     //  IL_0005: ldarg.3
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(c),
        //     //  IL_0006: br.s IL_0009
        //     x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l9),
        //     x => x.Should().BeOfType<LabelInstruction>().Should().Be(l8),
        //     //  IL_0008: ldarg.2
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        //     x => x.Should().BeOfType<LabelInstruction>().Should().Be(l9),
        //     //  IL_0009: stloc.0
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //     //  IL_000a: br.s IL_000c
        //     x => x.Should().BeOfType<BrInstruction>().Should().Be(lc),
        //     x => x.Should().BeOfType<LabelInstruction>().Should().Be(lc),
        //     //  IL_000c: ldloc.0
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //     //  IL_000d: ret
        //     x => x.Should().BeOfType<ReturnInstruction>()
        // );
    }

    [Fact]
    public void TernaryConditionalSwizzleShouldWork()
    {
        var p = new ParameterDeclaration("p", ShaderType.Vec3F32, []);
        var c = new ParameterDeclaration("cond", ShaderType.Bool, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.MaxByTernaryOperator),
            [p, c],
            new FunctionReturn(ShaderType.F32, []),
            []);
        var result = ParseMethod(f,
            MethodHelper.GetMethod<vec3f32, bool, float>(DevelopTestShaderModule.TernaryConditionalSwizzle));

        //result.DeclarationContext.LocalVariables.Should().ContainSingle();

        //var loc0 = result.DeclarationContext.LocalVariables[0];
        //loc0.Type.Should().Be(ShaderType.Vec4F32);

        var entry = result[result.Entry];
    }

}