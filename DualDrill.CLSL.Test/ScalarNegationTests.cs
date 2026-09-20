using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Transform;
using DualDrill.Common.Nat;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class ScalarNegationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(nameof(NegateInt32), "i32")]
    [InlineData(nameof(NegateInt64), "i64")]
    [InlineData(nameof(NegateFloat32), "f32")]
    [InlineData(nameof(NegateFloat64), "f64")]
    public void NativeNegationPreservesExactTypesThroughValueIr(string methodName, string typeName)
    {
        var method = Method(methodName);
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var neg = Assert.Single(raw.Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Neg);
        var pre = Body(stages.Pre, method).Code.Instructions.Single(
            instruction => instruction.Node.Index == neg.Index);
        var expectedType = ScalarType(typeName);

        Assert.Equal(expectedType, Assert.Single(pre.Annotation.Types).ShaderType);
        var returned = Assert.Single(Body(stages.Pre, method).Code.Instructions,
            instruction => instruction.Node.Instruction.OpCode == OpCodes.Ret);
        Assert.Equal(expectedType, Assert.Single(returned.Annotation.Types).ShaderType);

        var shaderInstruction = Assert.Single(
            Body(stages.ShaderStack, method).Blocks.Blocks.SelectMany(block => block.Body.Elements),
            instruction => instruction.Annotation.Provenance.OriginalIndex == neg.Index);
        var shaderOperation = Assert.IsType<ShaderStackInstruction.Operation>(shaderInstruction.Node);
        AssertNegationType(shaderOperation.Instruction.Operation, typeName);
        Assert.Equal(expectedType, shaderOperation.Instruction.Result);
        Assert.Equal(1, shaderOperation.PopCount);
        var operand = Assert.IsType<ShaderStackOperand.Depth>(
            Assert.Single(shaderOperation.Instruction.Operands));
        Assert.Equal(0, operand.Index);
        Assert.Equal(expectedType, operand.Type);
        Assert.Equal(expectedType, Assert.Single(shaderInstruction.Annotation.Pre));
        Assert.Equal(expectedType, Assert.Single(shaderInstruction.Annotation.Post));

        var values = Body(stages.ValueControlFlow, method);
        var valueOperation = Assert.Single(
            values.Graph.Labels().SelectMany(label => values.Graph[label].Body.Elements),
            instruction => instruction.Payload is ShaderStackProvenance provenance &&
                           provenance.OriginalIndex == neg.Index);
        AssertNegationType(valueOperation.Operation, typeName);
        Assert.Equal(expectedType, valueOperation.Result?.Type);
        Assert.Equal(expectedType, Assert.Single(valueOperation.Operands).Type);
    }

    [Fact]
    public void IntegerNegationIsUncheckedAndDoubleNegationIsIdentity()
    {
        var i32 = CompilerTestPipeline.ValueControlFlow(Method(nameof(NegateInt32)));
        var i64 = CompilerTestPipeline.ValueControlFlow(Method(nameof(NegateInt64)));
        int[] fixed32 = [0, 1, -1, 7, -7, int.MinValue, int.MaxValue];
        long[] fixed64 = [0, 1, -1, 7, -7, long.MinValue, long.MaxValue];

        foreach (var value in fixed32)
        {
            var result = RunValueCfg(i32, [new Value.Integer(value)]).Result;
            Assert.Equal(
                new Value.Integer(unchecked(-value)),
                result);
            output.WriteLine($"runtime scalar oracle i32: neg({value}) = {result}");
        }
        foreach (var value in fixed64)
        {
            var result = RunValueCfg(i64, [new Value.LongInteger(value)]).Result;
            Assert.Equal(
                new Value.LongInteger(unchecked(-value)),
                result);
            output.WriteLine($"runtime scalar oracle i64: neg({value}) = {result}");
        }

        var random = new Random(153);
        foreach (var value in fixed32.Concat(
                     Enumerable.Range(0, 128).Select(_ =>
                         (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1))))
        {
            var once = RunValueCfg(i32, [new Value.Integer(value)]).Result;
            Assert.Equal(new Value.Integer(value), RunValueCfg(i32, [once]).Result);
        }
        foreach (var value in fixed64.Concat(
                     Enumerable.Range(0, 128).Select(_ =>
                         random.NextInt64(long.MinValue, long.MaxValue))))
        {
            var once = RunValueCfg(i64, [new Value.LongInteger(value)]).Result;
            Assert.Equal(new Value.LongInteger(value), RunValueCfg(i64, [once]).Result);
        }
        output.WriteLine("runtime scalar oracle: 128 deterministic generated double-negation cases passed per integer type.");
    }

    [Fact]
    public void FloatingNegationPreservesSignClassificationAndExistingF32Behavior()
    {
        var f32 = CompilerTestPipeline.ValueControlFlow(Method(nameof(NegateFloat32)));
        var f64 = CompilerTestPipeline.ValueControlFlow(Method(nameof(NegateFloat64)));

        foreach (var (input, expected) in new (float Input, float Expected)[]
                 {
                     (0.0f, -0.0f),
                     (-0.0f, 0.0f),
                     (float.PositiveInfinity, float.NegativeInfinity),
                     (float.NegativeInfinity, float.PositiveInfinity),
                     (7.0f, -7.0f),
                     (-7.0f, 7.0f)
                 })
        {
            var actual = Assert.IsType<Value.Float32>(
                RunValueCfg(f32, [new Value.Float32(input)]).Result).Data;
            Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
            output.WriteLine(
                $"runtime scalar oracle f32: input=0x{BitConverter.SingleToInt32Bits(input):X8} " +
                $"result=0x{BitConverter.SingleToInt32Bits(actual):X8}");
        }

        foreach (var (input, expected) in new (double Input, double Expected)[]
                 {
                     (0.0, -0.0),
                     (-0.0, 0.0),
                     (double.PositiveInfinity, double.NegativeInfinity),
                     (double.NegativeInfinity, double.PositiveInfinity),
                     (7.0, -7.0),
                     (-7.0, 7.0)
                 })
        {
            var actual = Assert.IsType<Value.Float64>(
                RunValueCfg(f64, [new Value.Float64(input)]).Result).Data;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            output.WriteLine(
                $"runtime scalar oracle f64: input=0x{BitConverter.DoubleToInt64Bits(input):X16} " +
                $"result=0x{BitConverter.DoubleToInt64Bits(actual):X16}");
        }

        Assert.True(float.IsNaN(Assert.IsType<Value.Float32>(
            RunValueCfg(f32, [new Value.Float32(float.NaN)]).Result).Data));
        Assert.True(double.IsNaN(Assert.IsType<Value.Float64>(
            RunValueCfg(f64, [new Value.Float64(double.NaN)]).Result).Data));
    }

    [Theory]
    [InlineData(nameof(NegateInt32), "i32")]
    [InlineData(nameof(NegateInt64), "i64")]
    [InlineData(nameof(NegateFloat32), "f32")]
    [InlineData(nameof(NegateFloat64), "f64")]
    public void LoweredSlangAndScalarOracleExecuteNativeNegation(string methodName, string typeName)
    {
        var method = Method(methodName);
        var stages = CompilerTestPipeline.CompileStages(method);
        var region = Body(stages.Compiled, method);
        var lowered = new StablePointerRegionParameterPass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(region));
        var slang = Emit(method);
        IEnumerable<Value> arguments = typeName switch
        {
            "i32" => new[] { 0, 1, -1, 7, -7, int.MinValue, int.MaxValue }
                .Select(value => new Value.Integer(value)),
            "i64" => new[] { 0L, 1L, -1L, 7L, -7L, long.MinValue, long.MaxValue }
                .Select(value => new Value.LongInteger(value)),
            "f32" => new[] { 0.0f, -0.0f, 7.0f, -7.0f, float.PositiveInfinity, float.NegativeInfinity, float.NaN }
                .Select(value => new Value.Float32(value)),
            "f64" => new[] { 0.0, -0.0, 7.0, -7.0, double.PositiveInfinity, double.NegativeInfinity, double.NaN }
                .Select(value => new Value.Float64(value)),
            _ => throw new ArgumentOutOfRangeException(nameof(typeName))
        };

        var emitted = new EmittedScalarProgram(lowered, slang);
        foreach (var argument in arguments)
        {
            var expected = RunValueCfg(Body(stages.ValueControlFlow, method), [argument]);
            var original = RunCfg(region, [argument]);
            var target = RunCfg(lowered, [argument]);
            var text = emitted.Run([argument]);
            AssertEquivalent(original, target);
            AssertEquivalent(original, text);
            AssertExactScalar(expected.Result, original.Result);
            AssertExactScalar(expected.Result, target.Result);
            AssertExactScalar(expected.Result, text.Result);
        }
        output.WriteLine($"runtime scalar oracle and emitted-text interpreter: {typeName} boundary vectors agree.");
        Assert.Contains("let ", slang);
        Assert.Contains($" : {typeName} = - ", slang);
        Assert.DoesNotContain($"neg.{typeName}.{typeName}(", slang);
    }

    [Fact]
    public async Task WideSlangIsValidWhilePublicWgslRejectsWithoutDemotion()
    {
        foreach (var shader in new ISharpShader[] { new Int64NegationShader(), new Float64NegationShader() })
        {
            var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
            await new SlangService().ValidateAsync(slang);
            Assert.Contains("typealias i64 = int64_t;", slang);
            Assert.Contains("typealias f64 = double;", slang);
            Assert.Contains(" = - ", slang);

            var exception = Assert.Throws<NotSupportedException>(
                () => new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader));
            Assert.Contains("WGSL output does not support native", exception.Message);
            Assert.Contains("not truncated or demoted", exception.Message);
        }
    }

    [Fact]
    public void SupportedNegationCompilesThroughPublicWgsl()
    {
        var i32 = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(new Int32NegationShader());
        var f32 = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(new Float32NegationShader());

        Assert.Contains("@fragment", i32);
        Assert.Contains("- ", i32);
        Assert.Contains("@fragment", f32);
        Assert.Contains("- ", f32);
    }

    [Fact]
    public void HalfNativeNegationIsRejectedAndUintPromotionEndsInI64Negation()
    {
        var half = HalfNegationFixture;
        var halfModule = CompilerTestPipeline.ParseRaw(half);
        Assert.Contains(
            CompilerTestPipeline.RawBody(halfModule, half).Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Neg);
        var halfError = Assert.Throws<ValidationException>(() => CilPreStackPass.Run(halfModule));
        Assert.Contains("requires a supported scalar", halfError.Message);

        var promoted = Method(nameof(NegateUInt32));
        var stages = CompilerTestPipeline.CompileStages(promoted);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, promoted).Code.Instructions;
        var neg = Assert.Single(raw, instruction => instruction.Instruction.OpCode == OpCodes.Neg);
        Assert.Contains(raw, instruction => instruction.Instruction.OpCode == OpCodes.Conv_U8);
        var instruction = Assert.Single(
            Body(stages.ShaderStack, promoted).Blocks.Blocks.SelectMany(block => block.Body.Elements),
            instruction => instruction.Annotation.Provenance.OriginalIndex == neg.Index);
        var operation = Assert.IsType<ShaderStackInstruction.Operation>(instruction.Node);
        Assert.IsType<UnaryNumericArithmeticExpressionOperation<IntType<N64>, UnaryArithmetic.Negate>>(
            operation.Instruction.Operation);
        var promotionError = Assert.Throws<NotSupportedException>(() => Emit(promoted));
        Assert.Contains("i32-to-u64 conversion; unsigned widening is not implemented", promotionError.Message);
    }

    [Fact]
    public void ScalarOracleSupportsExactI64LiteralZeroAndSignature()
    {
        Assert.Equal(
            new Value.LongInteger(7),
            RunValueCfg(CompilerTestPipeline.ValueControlFlow(I64LiteralFixture), []).Result);
        Assert.Equal(
            new Value.LongInteger(0),
            RunValueCfg(CompilerTestPipeline.ValueControlFlow(I64ZeroFixture), []).Result);
    }

    [Fact]
    public void CaptureActualNegationStagesAndTargets()
    {
        foreach (var method in new[]
                 {
                     Method(nameof(NegateInt32)),
                     Method(nameof(NegateInt64)),
                     Method(nameof(NegateFloat32)),
                     Method(nameof(NegateFloat64)),
                     Method(nameof(NegateUInt32))
                 })
        {
            var stages = CompilerTestPipeline.CompileStages(method);
            output.WriteLine($"=== {method.Name} raw CIL ===");
            output.WriteLine(CompilerTestPipeline.RawBody(stages.Raw, method).PrettyPrint());
            output.WriteLine($"=== {method.Name} Pre ===");
            output.WriteLine(Body(stages.Pre, method).PrettyPrint());
            output.WriteLine($"=== {method.Name} Shader Stack ===");
            output.WriteLine(Body(stages.ShaderStack, method).PrettyPrint());
            output.WriteLine($"=== {method.Name} value IR ===");
            output.WriteLine(Body(stages.ValueControlFlow, method).PrettyPrint());
            if (method.Name == nameof(NegateUInt32))
            {
                output.WriteLine($"=== {method.Name} Slang diagnostic (conversion outside scalar-negation support) ===");
                output.WriteLine(Assert.Throws<NotSupportedException>(() => Emit(method)).Message);
            }
            else
            {
                output.WriteLine($"=== {method.Name} Slang ===");
                output.WriteLine(Emit(method));
            }
        }

        output.WriteLine("=== target compilation only (not GPU execution): supported i32 public WGSL ===");
        output.WriteLine(new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(new Int32NegationShader()));
        output.WriteLine("=== target compilation only (not GPU execution): supported f32 public WGSL ===");
        output.WriteLine(new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(new Float32NegationShader()));
        foreach (var shader in new ISharpShader[] { new Int64NegationShader(), new Float64NegationShader() })
        {
            output.WriteLine($"=== {shader.GetType().Name} public WGSL diagnostic ===");
            output.WriteLine(Assert.Throws<NotSupportedException>(
                () => new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader)).Message);
        }
    }

    private static void AssertNegationType(IOperation operation, string typeName)
    {
        switch (typeName)
        {
            case "i32":
                Assert.IsType<
                    UnaryNumericArithmeticExpressionOperation<IntType<N32>, UnaryArithmetic.Negate>>(operation);
                break;
            case "i64":
                Assert.IsType<
                    UnaryNumericArithmeticExpressionOperation<IntType<N64>, UnaryArithmetic.Negate>>(operation);
                break;
            case "f32":
                Assert.IsType<
                    UnaryNumericArithmeticExpressionOperation<FloatType<N32>, UnaryArithmetic.Negate>>(operation);
                break;
            case "f64":
                Assert.IsType<
                    UnaryNumericArithmeticExpressionOperation<FloatType<N64>, UnaryArithmetic.Negate>>(operation);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(typeName));
        }
    }

    private static void AssertExactScalar(Value expected, Value actual)
    {
        switch (expected)
        {
            case Value.Float32 value:
                var f32 = Assert.IsType<Value.Float32>(actual).Data;
                if (float.IsNaN(value.Data))
                    Assert.True(float.IsNaN(f32));
                else
                    Assert.Equal(BitConverter.SingleToInt32Bits(value.Data), BitConverter.SingleToInt32Bits(f32));
                break;
            case Value.Float64 value:
                var f64 = Assert.IsType<Value.Float64>(actual).Data;
                if (double.IsNaN(value.Data))
                    Assert.True(double.IsNaN(f64));
                else
                    Assert.Equal(BitConverter.DoubleToInt64Bits(value.Data), BitConverter.DoubleToInt64Bits(f64));
                break;
            default:
                Assert.Equal(expected, actual);
                break;
        }
    }

    private static IShaderType ScalarType(string name) => name switch
    {
        "i32" => ShaderType.I32,
        "i64" => ShaderType.I64,
        "f32" => ShaderType.F32,
        "f64" => ShaderType.F64,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static string Emit(MethodInfo method)
    {
        var module = CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method))
            .RunPass(new FunctionToOperationPass())
            .RunPass(new StablePointerRegionParameterPass());
        return new SlangEmitter(new SlangTargetLowering().Lower(module)).Emit();
    }

    private static TBody Body<TBody>(
        ShaderModuleDeclaration<TBody> module,
        MethodBase method)
        where TBody : IFunctionBody =>
        Assert.Single(module.FunctionDefinitions,
            pair => pair.Key.Name == method.Name).Value;

    private static MethodInfo Method(string name) =>
        typeof(ScalarNegationTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static Type FixtureType { get; } = BuildFixtureType();
    private static MethodInfo HalfNegationFixture => Fixture("NegateHalf");
    private static MethodInfo I64LiteralFixture => Fixture("I64Literal");
    private static MethodInfo I64ZeroFixture => Fixture("I64Zero");

    private static Type BuildFixtureType()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ScalarNegationFixtures"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("ScalarNegationFixtures");
        var type = module.DefineType(
            "ScalarNegationFixtures",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var half = type.DefineMethod(
            "NegateHalf",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(Half),
            [typeof(Half)]);
        half.DefineParameter(1, ParameterAttributes.None, "value");
        var halfIl = half.GetILGenerator();
        halfIl.Emit(OpCodes.Ldarg_0);
        halfIl.Emit(OpCodes.Neg);
        halfIl.Emit(OpCodes.Ret);

        var literal = type.DefineMethod(
            "I64Literal",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(long),
            Type.EmptyTypes);
        var literalIl = literal.GetILGenerator();
        literalIl.Emit(OpCodes.Ldc_I8, 7L);
        literalIl.Emit(OpCodes.Ret);

        var zero = type.DefineMethod(
            "I64Zero",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(long),
            Type.EmptyTypes);
        zero.InitLocals = true;
        var zeroIl = zero.GetILGenerator();
        var local = zeroIl.DeclareLocal(typeof(long));
        zeroIl.Emit(OpCodes.Ldloc, local);
        zeroIl.Emit(OpCodes.Ret);

        return type.CreateType()
               ?? throw new InvalidOperationException("Failed to create scalar negation fixture type.");
    }

    private static MethodInfo Fixture(string name) =>
        FixtureType.GetMethod(name)
        ?? throw new InvalidOperationException($"Failed to find scalar negation fixture {name}.");

    private static int NegateInt32(int value) => -value;
    private static long NegateInt64(long value) => -value;
    private static float NegateFloat32(float value) => -value;
    private static double NegateFloat64(double value) => -value;
    private static long NegateUInt32(uint value) => -value;

    private sealed class Int32NegationShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static int fs([Location(0)] int value) => -value;
    }

    private sealed class Int64NegationShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static long fs([Location(0)] long value) => -value;
    }

    private sealed class Float32NegationShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float fs([Location(0)] float value) => -value;
    }

    private sealed class Float64NegationShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static double fs([Location(0)] double value) => -value;
    }
}
