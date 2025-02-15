using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using FluentAssertions;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;


public class ParseBodyTest(ITestOutputHelper Output)
{
    UnstructuredStackInstructionSequence ParseMethod(FunctionDeclaration f, MethodBase m)
    {
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(m), f);
        var parser = new RuntimeReflectionParser(context);
        return parser.ParseMethodBody(f);
    }

    [Fact]
    public void ParseBasicLiteralExpressionBodyShouldWork()
    {
        var f = new FunctionDeclaration(nameof(DevelopTestShaderModule.Return42), [], new FunctionReturn(ShaderType.I32, []), []);
        var result = ParseMethod(f, MethodHelper.GetMethod(DevelopTestShaderModule.Return42));
        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(42),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void MinimumLoadArgumentShouldWork()
    {
        var method = MethodHelper.GetMethod<int, int>(DevelopTestShaderModule.LoadArg);
        var parameters = method.GetParameters();
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var f = new FunctionDeclaration(
                    nameof(DevelopTestShaderModule.LoadArg),
                    [a],
                    new FunctionReturn(ShaderType.I32, []),
                    []);
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(method), f);
        context.AddParameter(parameters[0], a);
        var parser = new RuntimeReflectionParser(context);
        var result = parser.ParseMethodBody(f);

        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void BasicLoadArgumentShouldWork()
    {
        var method = MethodHelper.GetMethod<int, int>(DevelopTestShaderModule.APlus1);
        var parameters = method.GetParameters();
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.APlus1),
            [a],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(method), f);
        context.AddParameter(parameters[0], a);
        var parser = new RuntimeReflectionParser(context);

        var result = parser.ParseMethodBody(f);

        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(1),
            x => x.Should().BeOfType<BinaryOperationInstruction<NumericBinaryOperation<IntType<N32>, BinaryArithmetic.Add>>>(),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void IntAPlusBShouldWork()
    {
        // IL_0000: ldarg.0
        // IL_0001: ldarg.1
        // IL_0002: add
        // IL_0003: ret
        var method = MethodHelper.GetMethod(static (int a, int b) => a + b);
        var instructions = method.GetInstructions();
        var parameters = method.GetParameters();
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var b = new ParameterDeclaration("b", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(IntAPlusBShouldWork),
            [a, b],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(method), f);
        context.AddParameter(parameters[0], a);
        context.AddParameter(parameters[1], b);
        var parser = new RuntimeReflectionParser(context);

        var result = parser.ParseMethodBody(f);

        result.Instructions[2].Should().BeOfType<BinaryOperationInstruction<NumericBinaryOperation<IntType<N32>, BinaryArithmetic.Add>>>();
    }

    [Fact]
    public void UIntAPlusBShouldWork()
    {
        // IL_0000: ldarg.0
        // IL_0001: ldarg.1
        // IL_0002: add
        // IL_0003: ret
        var method = MethodHelper.GetMethod(static (uint a, uint b) => a + b);
        var instructions = method.GetInstructions();
        var parameters = method.GetParameters();
        var a = new ParameterDeclaration("a", ShaderType.U32, []);
        var b = new ParameterDeclaration("b", ShaderType.U32, []);
        var f = new FunctionDeclaration(
            nameof(IntAPlusBShouldWork),
            [a, b],
            new FunctionReturn(ShaderType.U32, []),
            []);
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(method), f);
        context.AddParameter(parameters[0], a);
        context.AddParameter(parameters[1], b);
        var parser = new RuntimeReflectionParser(context);

        var result = parser.ParseMethodBody(f);

        result.Instructions[2].Should().BeOfType<BinaryOperationInstruction<NumericBinaryOperation<UIntType<N32>, BinaryArithmetic.Add>>>();
    }

    [Fact]
    public void FloatAPlusBShouldWork()
    {
        // IL_0000: ldarg.0
        // IL_0001: ldarg.1
        // IL_0002: add
        // IL_0003: ret
        var method = MethodHelper.GetMethod(static (float a, float b) => a + b);
        var instructions = method.GetInstructions();
        var parameters = method.GetParameters();
        var a = new ParameterDeclaration("a", ShaderType.F32, []);
        var b = new ParameterDeclaration("b", ShaderType.F32, []);
        var f = new FunctionDeclaration(
            nameof(IntAPlusBShouldWork),
            [a, b],
            new FunctionReturn(ShaderType.F32, []),
            []);
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(method), f);
        context.AddParameter(parameters[0], a);
        context.AddParameter(parameters[1], b);
        var parser = new RuntimeReflectionParser(context);

        var result = parser.ParseMethodBody(f);

        result.Instructions[2].Should().BeOfType<BinaryOperationInstruction<NumericBinaryOperation<FloatType<N32>, BinaryArithmetic.Add>>>();
    }


    [Fact]
    public void SimpleVec4ConstructionShouldWork()
    {
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.SystemNumericVector4Creation),
            [],
            new FunctionReturn(ShaderType.Vec4F32, []),
            []
        );
        var result = ParseMethod(f, MethodHelper.GetMethod(DevelopTestShaderModule.SystemNumericVector4Creation));
        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<ConstInstruction<F32Literal>>().Which.Literal.Value.Should().Be(1.0f),
            x => x.Should().BeOfType<ConstInstruction<F32Literal>>().Which.Literal.Value.Should().Be(2.0f),
            x => x.Should().BeOfType<ConstInstruction<F32Literal>>().Which.Literal.Value.Should().Be(3.0f),
            x => x.Should().BeOfType<ConstInstruction<F32Literal>>().Which.Literal.Value.Should().Be(4.0f),
            x => x.Should().Satisfy<CallInstruction>(c =>
            {
                c.Callee.Parameters.Should().HaveCount(4).And.AllSatisfy(p =>
                {
                    p.Type.Should().Be(ShaderType.F32);
                });
                c.Callee.Return.Type.Should().Be(ShaderType.Vec4F32);
            }),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }


    [Fact]
    public void BasicMethodInvocationParseShouldWork()
    {
        var context = CompilationContext.Create();
        var fAdd = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.Add),
            [new ParameterDeclaration("a", ShaderType.I32, []),
             new ParameterDeclaration("b", ShaderType.I32, [])],
            new FunctionReturn(ShaderType.I32, []),
            []);
        context.AddFunctionDeclaration(Symbol.Function(MethodHelper.GetMethod<int, int, int>(DevelopTestShaderModule.Add)), fAdd);
        var fCall = new FunctionDeclaration(nameof(DevelopTestShaderModule.MethodInvocation), [], new FunctionReturn(ShaderType.I32, []), []);
        var method = MethodHelper.GetMethod(DevelopTestShaderModule.MethodInvocation);
        context.AddFunctionDefinition(Symbol.Function(method), fCall);
        var parser = new RuntimeReflectionParser(context);
        var result = parser.ParseMethodBody(fCall);
        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(1),
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(2),
            x => x.Should().BeOfType<CallInstruction>().Which.Callee.Should().Be(fAdd),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void BasicIfThenElseParseShouldWork()
    {
        var method = MethodHelper.GetMethod<int, int, int>(DevelopTestShaderModule.MaxByfThenElse);
        var instructions = method.GetInstructions();
        var parameters = method.GetParameters();
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var b = new ParameterDeclaration("b", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(DevelopTestShaderModule.MaxByfThenElse),
            [a, b],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(method), f);
        context.AddParameter(parameters[0], a);
        context.AddParameter(parameters[1], b);
        var parser = new RuntimeReflectionParser(context);

        var result = parser.ParseMethodBody(f);

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
        labels.Should().HaveCount(2);

        var l11 = labels[0].Label;
        var l16 = labels[1].Label;

        result.Instructions.Should().SatisfyRespectively(
        //  IL_0000: nop
            x => x.Should().BeOfType<NopInstruction>(),
        //  IL_0001: ldarg.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //  IL_0002: ldarg.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        //  IL_0003: clt
            x => x.Should().BeOfType<BinaryOperationInstruction<NumericBinaryOperation<IntType<N32>, BinaryRelation.Lt>>>(),
        //  IL_0005: ldc.i4.0
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>(),
        //  IL_0006: ceq
            x => x.Should().BeOfType<BinaryOperationInstruction<NumericBinaryOperation<IntType<N32>, BinaryRelation.Eq>>>(),
            x => x.Should().BeOfType<UnaryOperationInstruction<ScalarConversionOperation<IntType<N32>, BoolType>>>(),
        //  IL_0008: stloc.0
            x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //  IL_0009: ldloc.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //  IL_000a: brfalse.s IL_0011
            x => x.Should().BeOfType<LogicalNotInstruction>(),
            x => x.Should().BeOfType<BrIfInstruction>().Which.Target.Should().Be(l11),

        //  IL_000c: nop
            x => x.Should().BeOfType<NopInstruction>(),
        //  IL_000d: ldarg.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //  IL_000e: stloc.1
            x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //  IL_000f: br.s IL_0016
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l16),

            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l11),
        //  IL_0011: nop
            x => x.Should().BeOfType<NopInstruction>(),
        //  IL_0012: ldarg.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
        //  IL_0013: stloc.1
            x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
        //  IL_0014: br.s IL_0016
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l16),

            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l16),
        //  IL_0016: ldloc.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
        //  IL_0017: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void VectorSwizzleGetterShouldWork()
    {
        var v = new ParameterDeclaration("v", ShaderType.Vec2F32, []);
        var f = new FunctionDeclaration(
                nameof(DevelopTestShaderModule.VecSwizzleGetter),
                [],
                new FunctionReturn(ShaderType.Vec3F32, []),
                []
            );
        var result = ParseMethod(f, MethodHelper.GetMethod<vec2f32, vec3f32>(DevelopTestShaderModule.VecSwizzleGetter));

        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<LoadSymbolAddressInstruction<ParameterDeclaration>>().Which.Target.Should().Be(v),
            x => x.Should().BeOfType<CallInstruction>().Which.Callee.Should().Be(VectorSwizzleGetOperation<Swizzle.Pattern<N2, Swizzle.X, Swizzle.Y, Swizzle.X>, FloatType<N32>>.Instance.Function),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void SimpleVectorSwizzleSetShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.Vec4F32, []);
        var b = new ParameterDeclaration("b", ShaderType.Vec2F32, []);
        var f = new FunctionDeclaration(
            nameof(SimpleVectorSwizzleSetShouldWork),
            [a, b],
            new FunctionReturn(ShaderType.Vec4F32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static (vec4f32 a, vec2f32 b) =>
        {
            a.xy = b;
            return a;
        }));
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


        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: nop
            x => x.Should().BeOfType<NopInstruction>(),
            //  IL_0001: ldarga @a
            x => x.Should().BeOfType<LoadSymbolAddressInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0003: ldarg @b
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
            //  IL_0005: swizzle.set.vec4f32.xy
            //x => x.Should().BeOfType<VectorSwizzleSetInstruction<VecType<N4, FloatType<N32>>, Swizzle.Pattern<Swizzle.X, Swizzle.Y>>>(),
            x => x.Should().BeOfType<CallInstruction>().Which.Callee.Should().Be(VectorSwizzleSetOperation<Swizzle.Pattern<N4, Swizzle.X, Swizzle.Y>, FloatType<N32>>.Instance.Function),
            //  IL_000f: nop
            x => x.Should().BeOfType<NopInstruction>(),
            //  IL_0010: ldarg.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0011: stloc.0
            x => x.Should().BeOfType<StoreSymbolInstruction<VariableDeclaration>>(),
            //  IL_0012: br IL_0014
            x => x.Should().BeOfType<BrInstruction>(),

            x => x.Should().BeOfType<LabelInstruction>(),
            //  IL_0014: ldloc.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<VariableDeclaration>>(),
            //  IL_0015: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void Vec4DotTest()
    {
        var a = new ParameterDeclaration("a", ShaderType.Vec4F32, []);
        var b = new ParameterDeclaration("b", ShaderType.Vec4F32, []);
        var f = new FunctionDeclaration(
            nameof(SimpleVectorSwizzleSetShouldWork),
            [a, b],
            new FunctionReturn(ShaderType.F32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static (Vector4 a, Vector4 b) => Vector4.Dot(a, b)));
        //  IL_0000: ldarg.0
        //  IL_0001: ldarg.1
        //  IL_0002: call dot.vec4f32
        //  IL_0007: ret
        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: ldarg.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0001: ldarg.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),
            //  IL_0002: call dot.vec4f32
            x => x.Should().BeOfType<CallInstruction>(),
            //  IL_0007: ret
            x => x.Should().BeOfType<ReturnInstruction>()
         );
    }

    [Fact]
    public void CompileTaneryConditionalExpressionShouldWork()
    {
        //  IL_0000: ldarg.0
        //  IL_0001: ldc.i4.0
        //  IL_0002: ble.s IL_0007

        //  IL_0004: ldarg.2
        //  IL_0005: br.s IL_0008

        //  IL_0007: ldarg.1

        //  IL_0008: ret
        var method = MethodHelper.GetMethod(static (int a, int b, int c) => a <= 0 ? b : c);
        var instructions = method.GetInstructions();
        foreach (var inst in instructions)
        {
            Output.WriteLine($"{inst.Offset} - {inst.OpCode} - {inst.Operand}");
        }
        var parameters = method.GetParameters();
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var b = new ParameterDeclaration("b", ShaderType.I32, []);
        var c = new ParameterDeclaration("c", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(CompileTaneryConditionalExpressionShouldWork),
            [a, b, c],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(method), f);
        context.AddParameter(parameters[0], a);
        context.AddParameter(parameters[1], b);
        context.AddParameter(parameters[2], c);
        var parser = new RuntimeReflectionParser(context);

        var result = parser.ParseMethodBody(f);

        var labels = result.Instructions.OfType<LabelInstruction>().ToArray();
        labels.Should().HaveCount(2).And.OnlyHaveUniqueItems();

        var l7 = labels[0].Label;
        var l8 = labels[1].Label;

        Output.WriteLine($"l7: {l7}");
        Output.WriteLine($"l8: {l8}");

        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: ldarg.0
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0001: ldc.i4.0
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(0),
            //  IL_0002: ble.s IL_0007
            x => x.Should().BeOfType<BinaryOperationInstruction<NumericBinaryOperation<IntType<N32>, BinaryRelation.Le>>>(),
            x => x.Should().BeOfType<BrIfInstruction>().Which.Target.Should().Be(l7),

            //  IL_0004: ldarg.2
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(c),
            //  IL_0005: br.s IL_0008
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l8),

            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l7),
            //  IL_0007: ldarg.1
            x => x.Should().BeOfType<LoadSymbolValueInstruction<ParameterDeclaration>>().Which.Target.Should().Be(b),

            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l8),
            //  IL_0008: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }
}
