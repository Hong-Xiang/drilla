using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using System.Numerics;
using System.Reflection;
using FluentAssertions;
using System.Collections.Immutable;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.CLSL.Language.ControlFlow;

namespace DualDrill.CLSL.Test;

public class ParseBodyTest
{
    IExpression? ParseExpressionBodyMethod(MethodBase m)
    {
        var stmts = ParseStatementsMethod(m);
        Assert.Single(stmts);
        Assert.IsType<ReturnStatement>(stmts[0]);
        return ((ReturnStatement)stmts[0]).Expr;
    }

    IReadOnlyList<IStatement> ParseStatementsMethod(MethodBase m)
    {
        //var compiler = new MethodBodyCompiler();
        //var parser = new ShaderModuleMetadataParser();
        //parser.ParseMethodMetadata(m);
        //var methodContext = parser.Context.GetMethodContext(m);
        //var body = methodParser.ParseMethodBody(methodContext, m);
        //return body.Statements;
        throw new NotImplementedException();
    }

    UnstructuredStackInstructionSequence ParseMethod(FunctionDeclaration f, MethodBase m)
    {
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(m), f);
        var parser = new RuntimeReflectionParser(context);
        return parser.ParseMethodBody(f);
    }


    IUnstructuredControlFlowFunctionBody<IStackStatement> ParseMethod2(FunctionDeclaration f, MethodBase m)
    {
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(m), f);
        var parameters = m.GetParameters();
        foreach (var (ip, p) in f.Parameters.Index())
        {
            context.AddParameter(Symbol.Parameter(parameters[ip]), p);
        }

        var parser = new RuntimeReflectionParser(context);
        return parser.ParseMethodBody2(f);
    }

    [Fact]
    public void ParseBasicLiteralExpressionBodyShouldWork()
    {
        var f = new FunctionDeclaration(nameof(DevelopTestShaderModule.Return42), [],
            new FunctionReturn(ShaderType.I32, []), []);
        var result = ParseMethod2(f, MethodHelper.GetMethod(DevelopTestShaderModule.Return42));
        var entry = result[result.Entry];
        entry.Elements.Should().ContainSingle()
             .Which.Should().BeOfType<ReturnStatement>()
             .Which.Expr.Should().BeOfType<LiteralValueExpression>()
             .Which.Literal.Should().BeOfType<I32Literal>()
             .Which.Value.Should().Be(42);
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
        var result = ParseMethod2(f, MethodHelper.GetMethod<int, int>(DevelopTestShaderModule.LoadArg));
        var entry = result[result.Entry];
        entry.Elements.Should().ContainSingle()
             .Which.Should().BeOfType<ReturnStatement>()
             .Which.Expr.Should().BeOfType<FormalParameterExpression>()
             .Which.Parameter.Should().Be(a);
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
        var result = ParseMethod2(f, MethodHelper.GetMethod<int, int>(DevelopTestShaderModule.APlus1));
        var entry = result[result.Entry];
        entry.Elements.Should().ContainSingle()
             .Which.Should().BeOfType<ReturnStatement>()
             .Which.Expr.Should().Satisfy<IBinaryExpression>(e =>
             {
                 e.Operation.Should().Satisfy<IBinaryExpressionOperation>(operation =>
                 {
                     operation.LeftType.Should().BeOfType<IntType<N32>>();
                     operation.RightType.Should().BeOfType<IntType<N32>>();
                     operation.BinaryOp.Should().BeOfType<BinaryArithmetic.Add>();
                 });
                 e.L.Should().BeOfType<FormalParameterExpression>()
                  .Which.Parameter.Should().Be(a);
                 e.R.Should().BeOfType<LiteralValueExpression>()
                  .Which.Literal.Should().Be(Literal.Create(1));
             });
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
        var result = ParseMethod2(f, MethodHelper.GetMethod(DevelopTestShaderModule.SystemNumericVector4Creation));
        var entry = result[result.Entry];
        entry.Elements.Should().ContainSingle()
             .Which.Should().BeOfType<ReturnStatement>()
             .Which.Expr.Should().Satisfy<FunctionCallExpression>(
                 call =>
                 {
                     call.Callee.Return.Type.Should().BeOfType<VecType<N4, FloatType<N32>>>();
                     call.Callee.Parameters.Should().HaveCount(4)
                         .And.OnlyContain(p => p.Type is FloatType<N32>);
                     call.Arguments.Should().SatisfyRespectively(
                         x => x.Should().BeOfType<LiteralValueExpression>()
                               .Which.Literal.Should().BeOfType<F32Literal>()
                               .Which.Value.Should().Be(1.0f),
                         x => x.Should().BeOfType<LiteralValueExpression>()
                               .Which.Literal.Should().BeOfType<F32Literal>()
                               .Which.Value.Should().Be(2.0f),
                         x => x.Should().BeOfType<LiteralValueExpression>()
                               .Which.Literal.Should().BeOfType<F32Literal>()
                               .Which.Value.Should().Be(3.0f),
                         x => x.Should().BeOfType<LiteralValueExpression>()
                               .Which.Literal.Should().BeOfType<F32Literal>()
                               .Which.Value.Should().Be(4.0f)
                     );
                 }
             );
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
        var result = parser.ParseMethodBody2(fCall);
        var entry = result[result.Entry];
        entry.Elements.Should().ContainSingle()
             .Which.Should().BeOfType<ReturnStatement>()
             .Which.Expr.Should().Satisfy<FunctionCallExpression>(
                 call =>
                 {
                     call.Callee.Should().Be(fAdd);
                     call.Arguments.Should().SatisfyRespectively(
                         a => a.Should().BeOfType<LiteralValueExpression>()
                               .Which.Literal.Should().BeOfType<I32Literal>()
                               .Which.Value.Should().Be(1),
                         b => b.Should().BeOfType<LiteralValueExpression>()
                               .Which.Literal.Should().BeOfType<I32Literal>()
                               .Which.Value.Should().Be(2)
                     );
                 }
             );
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

        ImmutableArray<LabelInstruction> labels = [.. result.Instructions.OfType<LabelInstruction>()];

        labels.Should().HaveCount(2).And.OnlyHaveUniqueItems();

        var l11 = labels[0];
        var l16 = labels[1];

        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: nop
            x => x.Should().BeOfType<NopInstruction>(),
            //  IL_0001: ldarg.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0002: ldarg.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
            // IL_0003: clt
            x => x.Should()
                  .BeOfType<BinaryExpressionOperationInstruction<
                      NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>>>(),
            // IL_0005: ldc.i4.0
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(0),
            // IL_0006: ceq
            x => x.Should()
                  .BeOfType<BinaryExpressionOperationInstruction<
                      NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>>>(),
            // IL_0008: stloc.0
            x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
            // IL_0009: ldloc.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
            // IL_000a: brfalse.s IL_0011
            x => x.Should().BeOfType<BrIfInstruction>().Which.Target.Should().Be(l11),

            // IL_000c: nop
            x => x.Should().BeOfType<NopInstruction>(),
            // IL_000d: ldarg.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            // IL_000e: stloc.1
            x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
            // IL_000f: br.s IL_0016
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l16),
            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l11),
            // IL_0011: nop
            x => x.Should().BeOfType<NopInstruction>(),
            // IL_0012: ldarg.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
            // IL_0013: stloc.1
            x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
            // IL_0014: br.s IL_0016
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l16),
            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l16),
            // IL_0016: ldloc.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
            // IL_0017: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
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
        var result = ParseMethod2(f,
            MethodHelper.GetMethod<vec2f32, vec3f32>(DevelopTestShaderModule.VecSwizzleGetter));

        var entry = result[result.Entry];

        entry.Elements.Should().ContainSingle()
             .Which.Should().BeOfType<ReturnStatement>()
             .Which.Expr.Should().Satisfy<IUnaryExpression>(e =>
             {
                 e.Operation.Should().BeOfType<VectorSwizzleGetOperation<
                     Swizzle.Pattern<N2, Swizzle.X, Swizzle.Y, Swizzle.X>,
                     FloatType<N32>
                 >>();
             });


        // result.Instructions.Should().SatisfyRespectively(
        //     //  IL_0000: nop
        //     x => x.Should().BeOfType<NopInstruction>(),
        //     //  IL_0001: ldarga.s a
        //     x => x.Should().BeOfType<LoadSymbolAddressInstruction<ParameterDeclaration>>().Which.Target.Should().Be(v),
        //     //  IL_0003: swizzle.vec4f32.xyx [ref<vec4f32>] -> [vec3f32]
        //     x => x.Should()
        //           .BeOfType<
        //               VectorSwizzleGetOperation<Swizzle.Pattern<N4, Swizzle.X, Swizzle.Y, Swizzle.X>,
        //                   FloatType<N32>>>(),
        //     //  IL_0008: stloc.0
        //     x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //     //  IL_0009: br.s IL_000b
        //     x => x.Should().BeOfType<BrInstruction>(),
        //     x => x.Should().BeOfType<LabelInstruction>(),
        //     //  IL_000b: ldloc.0
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //     //  IL_000c: ret
        //     x => x.Should().BeOfType<ReturnInstruction>()
        // );
    }

    [Fact]
    public void VectorSwizzleSetShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.Vec4F32, []);
        var b = new ParameterDeclaration("b", ShaderType.Vec2F32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.VecSwizzleSetter),
            [a, b],
            new FunctionReturn(ShaderType.Vec4F32, []),
            []);
        var result = ParseMethod2(f,
            MethodHelper.GetMethod<vec4f32, vec2f32, vec4f32>(DevelopTestShaderModule.VecSwizzleSetter));

        result.LocalVariables.Should().ContainSingle();

        var local = result.LocalVariables[0];
        local.Type.Should().Be(ShaderType.Vec4F32);

        var entry = result[result.Entry];

        entry.Elements.Should().ContainSingle()
             .Which.Should().Satisfy<SimpleAssignmentStatement>(
                 s => { }
             );

        result.Successor(result.Entry).Should().Satisfy<UnconditionalSuccessor>(s =>
        {
            var bb = result[s.Target];
            bb.Elements.Should().ContainSingle()
              .Which.Should().BeOfType<ReturnStatement>()
              .Which.Expr.Should().Satisfy<VariableIdentifierExpression>(e => e.Variable.Should().Be(local));
        });


        //  IL_0000: nop
        //  IL_0001: ldarga @a
        //  IL_0003: ldarg @b
        //  IL_0005: swizzle.set.vec4f32.xy
        //  IL_000f: nop
        //  IL_0010: ldarg.0
        //  IL_0011: stloc.0
        //  IL_0012: br.s IL_0014

        //  IL_0014: ldloc.0
        //  IL_0015: ret


        // result.Instructions.Should().SatisfyRespectively(
        //     //  IL_0000: nop
        //     x => x.Should().BeOfType<NopInstruction>(),
        //     //  IL_0001: ldarga @a
        //     x => x.Should().BeOfType<LoadSymbolAddressInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //     //  IL_0003: ldarg @b
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        //     //  IL_0005: swizzle.set.vec4f32.xy
        //     x => x.Should()
        //           .BeOfType<VectorSwizzleSetOperation<Swizzle.Pattern<N4, Swizzle.X, Swizzle.Y>, FloatType<N32>>>(),
        //     //  IL_000f: nop
        //     x => x.Should().BeOfType<NopInstruction>(),
        //     //  IL_0010: ldarg.0
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //     //  IL_0011: stloc.0
        //     x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //     //  IL_0012: br IL_0014
        //     x => x.Should().BeOfType<BrInstruction>(),
        //     x => x.Should().BeOfType<LabelInstruction>(),
        //     //  IL_0014: ldloc.0
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //     //  IL_0015: ret
        //     x => x.Should().BeOfType<ReturnInstruction>()
        // );
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
        var result = ParseMethod2(f,
            MethodHelper.GetMethod<Vector4, Vector4, float>(DevelopTestShaderModule.Vector4Dot));
        var entry = result[result.Entry];

        entry.Elements.Should().ContainSingle()
             .Which.Should().BeOfType<ReturnStatement>()
             .Which.Expr.Should().Satisfy<FunctionCallExpression>(
                 call =>
                 {
                     call.Callee.Return.Type.Should().BeOfType<FloatType<N32>>();
                     call.Callee.Parameters.Should().HaveCount(2)
                         .And.OnlyContain(p => p.Type is VecType<N4, FloatType<N32>>);
                     call.Arguments.Should().SatisfyRespectively(
                         x => x.Should().BeOfType<FormalParameterExpression>()
                               .Which.Parameter.Should().Be(a),
                         x => x.Should().BeOfType<FormalParameterExpression>()
                               .Which.Parameter.Should().Be(b)
                     );
                 }
             );

        // //  IL_0000: ldarg.0
        // //  IL_0001: ldarg.1
        // //  IL_0002: call dot.vec4f32
        // //  IL_0007: ret
        // result.Instructions.Should().SatisfyRespectively(
        //     //  IL_0000: ldarg.0
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //     //  IL_0001: ldarg.1
        //     x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        //     //  IL_0002: call dot.vec4f32
        //     x => x.Should().BeOfType<CallInstruction>(),
        //     //  IL_0007: ret
        //     x => x.Should().BeOfType<ReturnInstruction>()
        // );
    }

    [Fact]
    public void CompileTaneryConditionalExpressionShouldWork()
    {
        //  IL_0000: nop
        //  IL_0001: ldarg.1
        //  IL_0002: ldc.i4.0
        //  IL_0003: ble.s IL_0008

        //  IL_0005: ldarg.3
        //  IL_0006: br.s IL_0009

        //  IL_0008: ldarg.2

        //  IL_0009: stloc.0
        //  IL_000a: br.s IL_000c

        //  IL_000c: ldloc.0
        //  IL_000d: ret
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var b = new ParameterDeclaration("b", ShaderType.I32, []);
        var c = new ParameterDeclaration("c", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(CompileTaneryConditionalExpressionShouldWork),
            [a, b, c],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static (int a, int b, int c) => a <= 0 ? b : c));

        var labels = result.Instructions.OfType<LabelInstruction>().ToArray();
        labels.Should().HaveCount(3).And.OnlyHaveUniqueItems();

        var l8 = labels[0];
        var l9 = labels[1];
        var lc = labels[2];

        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: nop
            x => x.Should().BeOfType<NopInstruction>(),
            //  IL_0001: ldarg.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0002: ldc.i4.0
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(0),
            //  IL_0003: ble.s IL_0008
            x => x.Should()
                  .BeOfType<BinaryExpressionOperationInstruction<
                      NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Le>>>(),
            x => x.Should().BeOfType<BrIfInstruction>().Which.Target.Should().Be(l8),

            //  IL_0005: ldarg.3
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(c),
            //  IL_0006: br.s IL_0009
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l9),
            x => x.Should().BeOfType<LabelInstruction>().Should().Be(l8),
            //  IL_0008: ldarg.2
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
            x => x.Should().BeOfType<LabelInstruction>().Should().Be(l9),
            //  IL_0009: stloc.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
            //  IL_000a: br.s IL_000c
            x => x.Should().BeOfType<BrInstruction>().Should().Be(lc),
            x => x.Should().BeOfType<LabelInstruction>().Should().Be(lc),
            //  IL_000c: ldloc.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
            //  IL_000d: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }
}