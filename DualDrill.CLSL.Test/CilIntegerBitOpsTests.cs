using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.RegionFixture;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using Label = DualDrill.CLSL.Language.Symbol.Label;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class CilIntegerBitOpsTests(ITestOutputHelper output)
{
    private static readonly int[] Counts =
        [-1, 0, 1, 31, 32, 33, 63, 64, int.MinValue, int.MaxValue];
    private static readonly int[] Payloads =
        [0, 1, 0x7fffffff, int.MinValue, unchecked((int)0x80000001), -1, -123456789];

    [Theory]
    [InlineData(nameof(ShiftLeftInt), "shl", false)]
    [InlineData(nameof(ShiftRightInt), "shr", false)]
    [InlineData(nameof(ShiftLeftUInt), "shl", true)]
    [InlineData(nameof(ShiftRightUInt), "shr.un", true)]
    public async Task CSharpShiftsPreservePayloadAndMaskEveryDynamicCount(
        string name, string opcode, bool unsigned)
    {
        var method = Method(name);
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var shift = Assert.Single(raw.Code.Instructions, item => item.Instruction.OpCode.Name == opcode);
        Assert.Equal(new[]
        {
            OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Ldc_I4_S,
            OpCodes.And, shift.Instruction.OpCode, OpCodes.Ret
        }, raw.Code.Instructions.Select(item => item.Instruction.OpCode));
        Assert.Contains(raw.Code.Instructions, item => item.Instruction.OpCode == OpCodes.And);
        Assert.Contains(raw.Code.Instructions, item => item.Instruction.OpCode == OpCodes.Ldc_I4_S &&
            System.Convert.ToInt32(item.Instruction.Operand) == 31);
        var expansion = Body(stages.ShaderStack).Blocks.Blocks
            .SelectMany(block => block.Body.Elements)
            .Where(item => item.Annotation.Provenance.OriginalIndex == shift.Index)
            .ToArray();
        Assert.Contains(expansion, item => item.Node is ShaderStackInstruction.Operation
        {
            Instruction.Operation: NumericBinaryArithmeticOperation<
                IntType<N32>, BinaryArithmetic.BitwiseAnd>
        });
        var typedShift = Assert.Single(Instructions(Body(stages.ValueControlFlow)), item =>
            item.Operation is IBinaryExpressionOperation
            {
                BinaryOp: BinaryArithmetic.ShiftLeft or BinaryArithmetic.ShiftRight
            });
        Assert.Equal(opcode == "shr.un" ? ShaderType.U32 : ShaderType.I32, typedShift.Result?.Type);
        Assert.IsType<ShaderStackProvenance>(typedShift.Payload);
        if (opcode == "shr.un")
            Assert.Contains(expansion, item => item.Node is ShaderStackInstruction.Operation
            {
                Instruction.Operation: ScalarConversionOperation<IntType<N32>, UIntType<N32>>
            });
        var module = stages.Compiled
            .RunPass(new FunctionToOperationPass())
            .RunPass(new StablePointerRegionParameterPass());
        var slang = new SlangEmitter(new SlangTargetLowering().Lower(module)).Emit();
        Assert.Contains(opcode == "shl" ? " << " : " >> ", slang);
        await new SlangService().ValidateAsync(slang);
        var region = Body(stages.Compiled);
        var lowered = module.FunctionDefinitions.Values.Single();
        var emitted = new EmittedScalarProgram(lowered, slang);

        output.WriteLine($"configuration={GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration}; " +
            $"method={method.Module.ModuleVersionId}:{method.MetadataToken}; opcode={opcode}");
        output.WriteLine($"ACTUAL CIL:\n{raw.PrettyPrint()}");
        output.WriteLine($"ACTUAL shader stack:\n{Body(stages.ShaderStack).PrettyPrint()}");
        output.WriteLine($"ACTUAL typed IR:\n{Body(stages.ValueControlFlow).PrettyPrint()}");
        output.WriteLine($"ACTUAL Slang:\n{slang}");

        foreach (var bits in Payloads)
            foreach (var count in Counts)
            {
                var golden = opcode switch
                {
                    "shl" => unchecked(bits << (count & 31)),
                    "shr" => bits >> (count & 31),
                    "shr.un" => unchecked((int)((uint)bits >> (count & 31))),
                    _ => throw new ArgumentOutOfRangeException(nameof(opcode))
                };
                var clr = unsigned
                    ? unchecked((int)(uint)method.Invoke(null, [unchecked((uint)bits), count])!)
                    : (int)method.Invoke(null, [bits, count])!;
                var args = ImmutableArray.Create<Value>(
                    unsigned ? new Value.UnsignedInteger(unchecked((uint)bits)) : new Value.Integer(bits),
                    new Value.Integer(count));
                Value expected = unsigned
                    ? new Value.UnsignedInteger(unchecked((uint)golden)) : new Value.Integer(golden);
                Assert.Equal(golden, clr);
                Assert.Equal(expected, RunValueCfg(Body(stages.ValueControlFlow), args).Result);
                Assert.Equal(expected, RunCfg(region, args).Result);
                Assert.Equal(expected, emitted.Run(args).Result);
            }
    }

    [Theory]
    [InlineData(nameof(ComplementInt), false)]
    [InlineData(nameof(ComplementUInt), true)]
    public async Task ComplementIsBitwiseAndNotLogical(string name, bool unsigned)
    {
        var method = Method(name);
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var op = Assert.Single(raw.Code.Instructions, item => item.Instruction.OpCode == OpCodes.Not);
        var expansion = Body(stages.ShaderStack).Blocks.Blocks
            .SelectMany(block => block.Body.Elements)
            .Where(item => item.Annotation.Provenance.OriginalIndex == op.Index).ToArray();
        Assert.Contains(expansion, item => item.Node is ShaderStackInstruction.Operation
        {
            Instruction.Operation: UnaryNumericArithmeticExpressionOperation<
                IntType<N32>, UnaryArithmetic.BitwiseNot>
        });
        Assert.DoesNotContain(expansion, item => item.Node is ShaderStackInstruction.Operation
        {
            Instruction.Operation: LogicalNotOperation
        });
        var module = stages.Compiled.RunPass(new FunctionToOperationPass())
            .RunPass(new StablePointerRegionParameterPass());
        var slang = new SlangEmitter(new SlangTargetLowering().Lower(module)).Emit();
        Assert.Contains("~", slang);
        Assert.DoesNotContain("!", slang);
        await new SlangService().ValidateAsync(slang);
        output.WriteLine($"configuration={GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration}; " +
            $"method={method.Module.ModuleVersionId}:{method.MetadataToken}");
        output.WriteLine($"ACTUAL CIL:\n{raw.PrettyPrint()}");
        output.WriteLine($"ACTUAL typed IR:\n{Body(stages.ValueControlFlow).PrettyPrint()}");
        output.WriteLine($"ACTUAL Slang:\n{slang}");
        var emitted = new EmittedScalarProgram(module.FunctionDefinitions.Values.Single(), slang);
        foreach (var bits in Payloads)
        {
            var expected = unsigned
                ? (Value)new Value.UnsignedInteger(~unchecked((uint)bits))
                : new Value.Integer(~bits);
            var args = ImmutableArray.Create<Value>(
                unsigned ? new Value.UnsignedInteger(unchecked((uint)bits)) : new Value.Integer(bits));
            var clr = unsigned
                ? unchecked((int)(uint)method.Invoke(null, [unchecked((uint)bits)])!)
                : (int)method.Invoke(null, [bits])!;
            Assert.Equal(~bits, clr);
            Assert.Equal(expected, RunCfg(Body(stages.Compiled), args).Result);
            Assert.Equal(expected, emitted.Run(args).Result);
        }
    }

    [Theory]
    [InlineData("shl", 1, 31, unchecked((int)0x80000000))]
    [InlineData("shl", -1, 1, -2)]
    [InlineData("shr", int.MinValue, 1, unchecked((int)0xc0000000))]
    [InlineData("shr.un", int.MinValue, 1, 0x40000000)]
    [InlineData("shr", -1, 1, -1)]
    [InlineData("shr.un", -1, 1, 0x7fffffff)]
    public void IssueShiftGoldens(string opcode, int bits, int count, int expected)
    {
        var method = opcode switch
        {
            "shl" => Method(nameof(ShiftLeftInt)),
            "shr" => Method(nameof(ShiftRightInt)),
            _ => Method(nameof(ShiftRightUInt))
        };
        var args = opcode == "shr.un"
            ? ImmutableArray.Create<Value>(new Value.UnsignedInteger(unchecked((uint)bits)), new Value.Integer(count))
            : [new Value.Integer(bits), new Value.Integer(count)];
        var actual = RunCfg(CompilerTestPipeline.CompileBody(method), args).Result;
        Assert.Equal(unchecked((uint)expected), actual switch
        {
            Value.Integer signed => unchecked((uint)signed.Data),
            Value.UnsignedInteger result => result.Data,
            _ => throw new InvalidOperationException("Expected integer shift result.")
        });
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(-1, 0)]
    public void IssueComplementGoldens(int bits, int expected)
    {
        Assert.Equal(expected, ComplementInt(bits));
        Assert.Equal(new Value.Integer(expected), RunCfg(
            CompilerTestPipeline.CompileBody(Method(nameof(ComplementInt))),
            [new Value.Integer(bits)]).Result);
    }

    [Fact]
    public void DoubleComplementLawHoldsThroughTypedIr()
    {
        var method = Method(nameof(DoubleComplement));
        Assert.Equal(2, CompilerTestPipeline.RawBody(CompilerTestPipeline.ParseRaw(method), method)
            .Code.Instructions.Count(item => item.Instruction.OpCode == OpCodes.Not));
        var body = CompilerTestPipeline.CompileBody(method);
        var random = new Random(241);
        for (var i = 0; i < Payloads.Length + 1024; i++)
        {
            var bits = i < Payloads.Length ? Payloads[i] : unchecked((int)random.NextInt64());
            Assert.Equal(bits, DoubleComplement(bits));
            Assert.Equal(new Value.Integer(bits), RunCfg(body, [new Value.Integer(bits)]).Result);
        }
    }

    [Fact]
    public void CompiledShiftsArePeriodicOverGeneratedInt32Counts()
    {
        foreach (var (method, unsigned) in new[]
        {
            (Method(nameof(ShiftLeftInt)), false),
            (Method(nameof(ShiftLeftUInt)), true),
            (Method(nameof(ShiftRightInt)), false),
            (Method(nameof(ShiftRightUInt)), true)
        })
        {
            var body = CompilerTestPipeline.CompileBody(method);
            var random = new Random(241);
            for (var i = 0; i < Counts.Length + 1024; i++)
            {
                var bits = i < Counts.Length
                    ? Payloads[i % Payloads.Length]
                    : unchecked((int)random.NextInt64());
                var count = i < Counts.Length ? Counts[i] : unchecked((int)random.NextInt64());
                var value = unsigned
                    ? (Value)new Value.UnsignedInteger(unchecked((uint)bits))
                    : new Value.Integer(bits);
                var expected = method.Name switch
                {
                    nameof(ShiftLeftInt) => (Value)new Value.Integer(ShiftLeftInt(bits, count)),
                    nameof(ShiftLeftUInt) => new Value.UnsignedInteger(
                        ShiftLeftUInt(unchecked((uint)bits), count)),
                    nameof(ShiftRightInt) => new Value.Integer(ShiftRightInt(bits, count)),
                    nameof(ShiftRightUInt) => new Value.UnsignedInteger(
                        ShiftRightUInt(unchecked((uint)bits), count)),
                    _ => throw new InvalidOperationException($"Unexpected shift method {method.Name}.")
                };
                var original = RunCfg(body, [value, new Value.Integer(count)]).Result;
                var periodic = RunCfg(body, [value, new Value.Integer(unchecked(count + 32))]).Result;
                Assert.Equal(expected, original);
                Assert.Equal(original, periodic);
            }
        }
    }

    [Fact]
    public void BitOperationsAreKnownPure()
    {
        Assert.True(FunctionEffectAnalysis.IsKnownPureOperation(
            NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.ShiftLeft>.Instance));
        Assert.True(FunctionEffectAnalysis.IsKnownPureOperation(
            NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.ShiftRight>.Instance));
        Assert.True(FunctionEffectAnalysis.IsKnownPureOperation(
            NumericBinaryArithmeticOperation<UIntType<N32>, BinaryArithmetic.ShiftRight>.Instance));
        Assert.True(FunctionEffectAnalysis.IsKnownPureOperation(
            UnaryNumericArithmeticExpressionOperation<IntType<N32>, UnaryArithmetic.BitwiseNot>.Instance));
    }

    [Theory]
    [InlineData("shl")]
    [InlineData("shr")]
    [InlineData("shr.un")]
    public void RawCilCountPolicyMasksAllInt32Counts(string opcode)
    {
        var method = RawMethod(opcode switch
        {
            "shl" => OpCodes.Shl,
            "shr" => OpCodes.Shr,
            _ => OpCodes.Shr_Un
        }, typeof(int), typeof(int));
        var raw = CompilerTestPipeline.RawBody(CompilerTestPipeline.ParseRaw(method), method);
        Assert.DoesNotContain(raw.Code.Instructions, item => item.Instruction.OpCode == OpCodes.And);
        var body = CompilerTestPipeline.CompileBody(method);
        foreach (var count in Counts)
        {
            // Only 0..31 is portable raw CIL; the compiler defines low-five masking beyond it.
            var expected = opcode switch
            {
                "shl" => unchecked(int.MinValue << (count & 31)),
                "shr" => int.MinValue >> (count & 31),
                _ => unchecked((int)((uint)int.MinValue >> (count & 31)))
            };
            Assert.Equal(expected, (int)method.Invoke(null, [int.MinValue, count])!);
            Assert.Equal(new Value.Integer(expected), RunCfg(body,
                [new Value.Integer(int.MinValue), new Value.Integer(count)]).Result);
        }
    }

    [Fact]
    public void RawCilRetainsPrefixDupAndShiftProvenance()
    {
        var method = RawMethod(OpCodes.Shl, typeof(int), typeof(int), true);
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method).Code.Instructions;
        var shift = Assert.Single(raw, item => item.Instruction.OpCode == OpCodes.Shl);
        var expansion = Body(stages.ShaderStack).Blocks.Blocks.SelectMany(block => block.Body.Elements)
            .Where(item => item.Annotation.Provenance.OriginalIndex == shift.Index).ToArray();
        Assert.Collection(expansion,
            literal => Assert.Equal(0, literal.Annotation.Provenance.ExpansionOrdinal),
            mask => Assert.Equal(1, mask.Annotation.Provenance.ExpansionOrdinal),
            shifted => Assert.Equal(2, shifted.Annotation.Provenance.ExpansionOrdinal));
        Assert.All(expansion, item =>
        {
            Assert.Equal(shift.ByteOffset, item.Annotation.Provenance.ByteStart);
            Assert.Equal(shift.NextByteOffset, item.Annotation.Provenance.ByteEnd);
        });
        Assert.Collection(expansion[0].Annotation.Pre,
            type => Assert.Equal(ShaderType.I32, type),
            type => Assert.Equal(ShaderType.I32, type),
            type => Assert.Equal(ShaderType.I32, type));
        foreach (var count in Counts)
        {
            var expected = unchecked(3 + (1 << (count & 31)));
            Assert.Equal(expected, (int)method.Invoke(null, [1, count])!);
            Assert.Equal(new Value.Integer(expected), RunCfg(Body(stages.Compiled),
                [new Value.Integer(1), new Value.Integer(count)]).Result);
        }
    }

    [Fact]
    public void CallsEvaluateValueThenCountOnceAndBeforeShift()
    {
        var method = Method(nameof(ObservedShift));
        var parsed = CompilerTestPipeline.ParseRaw(method);
        var own = parsed.FunctionDefinitions.Single(pair => pair.Value.Code.Environment.Method == method);
        var raw = own.Value.Code.Instructions;
        var calls = raw.Where(item => item.Instruction.OpCode == OpCodes.Call).ToArray();
        Assert.Equal(2, calls.Length);
        Assert.True(calls[0].Index < calls[1].Index);
        Assert.True(calls[1].Index < raw.Single(item => item.Instruction.OpCode == OpCodes.Shl).Index);
        var isolated = new ShaderModuleDeclaration<RawCilFunctionBody>(parsed.Declarations,
            ImmutableDictionary<FunctionDeclaration, RawCilFunctionBody>.Empty.Add(own.Key, own.Value));
        var typed = Instructions(Body(ShaderStackToValuePass.Run(
            ShaderStackControlFlowPass.Run(CilToShaderStackPass.Run(
                CilBlockPartitionPass.Run(CilPreStackPass.Run(isolated)))))));
        var typedCalls = typed.Where(item => item.Operation is CallOperation).ToArray();
        Assert.Equal(2, typedCalls.Length);
        Assert.Equal(calls[0].Index,
            Assert.IsType<ShaderStackProvenance>(typedCalls[0].Payload).OriginalIndex);
        Assert.Equal(calls[1].Index,
            Assert.IsType<ShaderStackProvenance>(typedCalls[1].Payload).OriginalIndex);
        var roslynMaskIndex = raw.Single(item => item.Instruction.OpCode == OpCodes.And).Index;
        var shiftIndex = raw.Single(item => item.Instruction.OpCode == OpCodes.Shl).Index;
        var roslynMask = Assert.Single(typed, item =>
            item.Operation is NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.BitwiseAnd> &&
            item.Payload is ShaderStackProvenance provenance &&
            provenance.OriginalIndex == roslynMaskIndex);
        var compilerMask = Assert.Single(typed, item =>
            item.Operation is NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.BitwiseAnd> &&
            item.Payload is ShaderStackProvenance provenance &&
            provenance.OriginalIndex == shiftIndex);
        var shift = Assert.Single(typed, item =>
            item.Operation is NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.ShiftLeft>);
        Assert.Same(typedCalls[0].Result, shift.Operand0);
        Assert.Same(typedCalls[1].Result, roslynMask.Operand0);
        Assert.Same(roslynMask.Result, compilerMask.Operand0);
        Assert.Same(compilerMask.Result, shift.Operand1);
        evaluationOrder = 0;
        evaluationCount = 0;
        Assert.Equal(8, ObservedShift(4, 1));
        Assert.Equal(12, evaluationOrder);
        Assert.Equal(2, evaluationCount);
    }

    [Theory]
    [InlineData("shl", typeof(long), typeof(int))]
    [InlineData("shl", typeof(int), typeof(long))]
    [InlineData("shl", typeof(nint), typeof(int))]
    [InlineData("shr", typeof(int), typeof(nint))]
    [InlineData("shr", typeof(float), typeof(int))]
    [InlineData("shr.un", typeof(int), typeof(float))]
    [InlineData("shr.un", typeof(double), typeof(int))]
    [InlineData("not", typeof(long), null)]
    [InlineData("not", typeof(float), null)]
    [InlineData("not", typeof(nint), null)]
    public void UnsupportedRawOperandWidthsFailAtSourceBoundary(string name, Type value, Type? count)
    {
        var opcode = name switch
        {
            "shl" => OpCodes.Shl,
            "shr" => OpCodes.Shr,
            "shr.un" => OpCodes.Shr_Un,
            _ => OpCodes.Not
        };
        var method = RawMethod(opcode, value, count);
        var error = Assert.Throws<ValidationException>(() =>
            CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(method)));
        Assert.Contains(name, error.Message);
        Assert.Contains("IL_", error.Message);
    }

    [Fact]
    public void MalformedDirectIrNeverBecomesSlangAst()
    {
        var shift = NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.ShiftLeft>.Instance;
        var not = UnaryNumericArithmeticExpressionOperation<IntType<N32>, UnaryArithmetic.BitwiseNot>.Instance;
        var i32 = ShaderValue.Literal(new I32Literal(1));
        var u32 = ShaderValue.Literal(new U32Literal(1));
        var i64 = ShaderValue.Literal(new I64Literal(1));
        var vector = ShaderValue.Intermediate(VecType<N2, IntType<N32>>.Instance);
        var bad = new[]
        {
            Instruction<IShaderValue, IShaderValue>.Create(shift, ShaderValue.Intermediate(ShaderType.I32), [i32]),
            Instruction<IShaderValue, IShaderValue>.Create(shift,
                ShaderValue.Intermediate(ShaderType.I32), [i32, i32]) with { Operand1 = null },
            Instruction<IShaderValue, IShaderValue>.Create(shift,
                ShaderValue.Intermediate(ShaderType.I32), [i32, i32]) with { RestOperands = [i32] },
            Instruction<IShaderValue, IShaderValue>.Create(shift, ShaderValue.Intermediate(ShaderType.I32), [i32, u32]),
            Instruction<IShaderValue, IShaderValue>.Create(shift, ShaderValue.Intermediate(ShaderType.I32), [i32, i64]),
            Instruction<IShaderValue, IShaderValue>.Create(shift, ShaderValue.Intermediate(ShaderType.U32), [i32, i32]),
            Instruction<IShaderValue, IShaderValue>.Create(shift, null, [i32, i32]),
            Instruction<IShaderValue, IShaderValue>.Create(
                NumericBinaryArithmeticOperation<IntType<N64>, BinaryArithmetic.ShiftRight>.Instance,
                ShaderValue.Intermediate(ShaderType.I64), [i64, i64]),
            Instruction<IShaderValue, IShaderValue>.Create(
                VectorExpressionNumericBinaryExpressionOperation<N2, IntType<N32>, BinaryArithmetic.ShiftLeft>.Instance,
                ShaderValue.Intermediate(vector.Type), [vector, vector]),
            Instruction<IShaderValue, IShaderValue>.Create(not, ShaderValue.Intermediate(ShaderType.I32), [i32, i32]),
            Instruction<IShaderValue, IShaderValue>.Create(not,
                ShaderValue.Intermediate(ShaderType.I32), [i32]) with { Operand1 = i32 },
            Instruction<IShaderValue, IShaderValue>.Create(not, ShaderValue.Intermediate(ShaderType.U32), [i32]),
            Instruction<IShaderValue, IShaderValue>.Create(not, ShaderValue.Intermediate(ShaderType.I32), [u32]),
            Instruction<IShaderValue, IShaderValue>.Create(not, null, [i32]),
            Instruction<IShaderValue, IShaderValue>.Create(
                UnaryNumericArithmeticExpressionOperation<IntType<N64>, UnaryArithmetic.BitwiseNot>.Instance,
                ShaderValue.Intermediate(ShaderType.I64), [i64]),
            Instruction<IShaderValue, IShaderValue>.Create(
                VectorNumericUnaryOperation<N2, IntType<N32>, UnaryArithmetic.BitwiseNot>.Instance,
                ShaderValue.Intermediate(vector.Type), [vector])
        };
        foreach (var instruction in bad)
        {
            var label = Label.Create("entry");
            var declaration = new FunctionDeclaration("InvalidBitOp", [],
                new FunctionReturn(ShaderType.Unit, []), []);
            var body = CreateFunctionBody(declaration, RegionTree.Block(label, [],
                RegionFixture.Body(label, [], [instruction],
                    Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>()), null));
            var module = new ShaderModuleDeclaration<RegionFunctionBody>([declaration],
                ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body));
            Assert.Throws<NotSupportedException>(() => new SlangTargetLowering().Lower(module));
        }
    }

    [Fact]
    public void RawManagedPointersAreNotShiftValuesOrCounts()
    {
        foreach (var method in new[]
        {
            RawMethod(OpCodes.Shl, typeof(int).MakeByRefType(), typeof(int)),
            RawMethod(OpCodes.Shr_Un, typeof(int), typeof(int).MakeByRefType())
        })
        {
            var error = Assert.Throws<ValidationException>(() =>
                CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(method)));
            Assert.Contains("IL_", error.Message);
        }
    }

    private static int ShiftLeftInt(int value, int count) => value << count;
    private static int ShiftRightInt(int value, int count) => value >> count;
    private static uint ShiftLeftUInt(uint value, int count) => value << count;
    private static uint ShiftRightUInt(uint value, int count) => value >> count;
    private static int ComplementInt(int value) => ~value;
    private static uint ComplementUInt(uint value) => ~value;
    private static int DoubleComplement(int value) => ~(~value);
    private static int evaluationOrder;
    private static int evaluationCount;
    private static int MarkValue(int value)
    {
        evaluationOrder = evaluationOrder * 10 + 1;
        evaluationCount++;
        return value;
    }
    private static int MarkCount(int value)
    {
        evaluationOrder = evaluationOrder * 10 + 2;
        evaluationCount++;
        return value;
    }
    private static int ObservedShift(int value, int count) => MarkValue(value) << MarkCount(count);

    private static MethodInfo Method(string name) =>
        typeof(CilIntegerBitOpsTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static TBody Body<TBody>(ShaderModuleDeclaration<TBody> module)
        where TBody : IFunctionBody => Assert.Single(module.FunctionDefinitions.Values);

    private static ImmutableArray<Instruction<IShaderValue, IShaderValue>> Instructions(
        CilValueControlFlowBody body) =>
        [.. body.Graph.Labels().SelectMany(label => body.Graph[label].Body.Elements)];

    private static MethodInfo RawMethod(OpCode opcode, Type value, Type? count, bool prefix = false)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BitOpsRaw"),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("BitOpsRaw").DefineType("BitOpsRaw",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
        var method = type.DefineMethod("Operation", MethodAttributes.Public | MethodAttributes.Static,
            typeof(int), count is null ? [value] : [value, count]);
        method.DefineParameter(1, ParameterAttributes.None, "value");
        if (count is not null)
            method.DefineParameter(2, ParameterAttributes.None, "count");
        var il = method.GetILGenerator();
        if (prefix)
        {
            il.Emit(OpCodes.Ldc_I4_3);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Pop);
        }
        il.Emit(OpCodes.Ldarg_0);
        if (count is not null)
            il.Emit(OpCodes.Ldarg_1);
        il.Emit(opcode);
        if (prefix)
            il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Ret);
        return type.CreateType()!.GetMethod("Operation")!;
    }
}
