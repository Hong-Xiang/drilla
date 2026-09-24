using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Reflection;
using DualDrill.Common.Nat;
using DualDrill.Graphics;
using DualDrill.Mathematics;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class WritableStructuredBufferTests(ITestOutputHelper output)
{
    [Fact]
    public void PublicWrapperIsAReadonlyShaderOnlyHandleWithGetterAndSetter()
    {
        var type = typeof(RWStructuredBuffer<float>);
        var indexer = type.GetProperty("Item") ??
                      throw new InvalidOperationException("RWStructuredBuffer indexer was not found.");

        Assert.True(type.IsValueType);
        Assert.True(type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false));
        Assert.Empty(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal(typeof(uint), type.GetProperty(nameof(RWStructuredBuffer<float>.Length))?.PropertyType);
        Assert.Equal(typeof(float), indexer.PropertyType);
        Assert.Equal(typeof(uint), Assert.Single(indexer.GetIndexParameters()).ParameterType);
        Assert.NotNull(indexer.GetMethod);
        Assert.NotNull(indexer.SetMethod);
        Assert.Throws<NotSupportedException>(() => default(RWStructuredBuffer<float>).Length);
        Assert.Throws<NotSupportedException>(() => default(RWStructuredBuffer<float>)[0]);
        Assert.Throws<NotSupportedException>(() => default(RWStructuredBuffer<float>)[0] = 1.0f);
    }

    [Theory]
    [InlineData(typeof(int), typeof(SignedCopyShader), "int32", "i32")]
    [InlineData(typeof(uint), typeof(UnsignedCopyShader), "uint32", "u32")]
    public async Task IntegerWritableBuffersKeepTypedCilIrAndCompiledReflection(
        Type element, Type shaderType, string scalar, string spelling)
    {
        var wrapper = typeof(RWStructuredBuffer<>).MakeGenericType(element);
        var indexer = wrapper.GetProperty("Item")!;
        Assert.Equal(element, indexer.PropertyType);
        Assert.Equal(typeof(uint), Assert.Single(indexer.GetIndexParameters()).ParameterType);
        Assert.NotNull(indexer.GetMethod);
        Assert.NotNull(indexer.SetMethod);
        Assert.Throws<TargetInvocationException>(() =>
            indexer.GetMethod!.Invoke(Activator.CreateInstance(wrapper), [0u]));
        Assert.Throws<TargetInvocationException>(() =>
            indexer.SetMethod!.Invoke(Activator.CreateInstance(wrapper),
                [0u, Activator.CreateInstance(element)]));

        var shader = (ISharpShader)Activator.CreateInstance(shaderType)!;
        var raw = new RuntimeReflectionParser().ParseShaderModule(shader);
        var outputField = shaderType.GetField("Output", BindingFlags.NonPublic | BindingFlags.Static)!;
        var resource = Assert.Single(raw.Declarations.OfType<VariableDeclaration>(),
            declaration => declaration.Name == "Output");
        IShaderType expectedType = element == typeof(int)
            ? ReadWriteStructuredBufferType<IntType<N32>>.Instance
            : ReadWriteStructuredBufferType<UIntType<N32>>.Instance;
        Assert.Same(expectedType, resource.Type);
        Assert.Same(StorageAddressSpace.Instance, resource.AddressSpace);
        var rawBody = CompilerTestPipeline.RawBody(raw, shaderType.GetMethod("Run")!);
        Assert.Contains(rawBody.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldsflda &&
            Equals(instruction.Instruction.Operand, outputField));
        Assert.Contains(rawBody.Code.Instructions, instruction =>
            Equals(instruction.Instruction.Operand, indexer.SetMethod));
        Assert.DoesNotContain(rawBody.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Callvirt ||
            instruction.Instruction.OpCode.Name?.Contains("ldelem", StringComparison.Ordinal) is true);

        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(raw);
        var instructions = Instructions(Function(compiled, "Run")).ToArray();
        var stores = instructions.Where(instruction =>
            instruction.Operation is IReadWriteStructuredBufferStoreOperation).ToArray();
        var store = Assert.Single(stores);
        Assert.True(ReadWriteStructuredBufferFamily.IsCanonicalStore(store.Operation));
        Assert.Null(store.Result);
        Assert.Same(resource.Value, store.Operand0);
        Assert.Equal([expectedType.GetPtrType(StorageAddressSpace.Instance), ShaderType.U32,
            element == typeof(int) ? ShaderType.I32 : ShaderType.U32],
            store.Operands.Select(operand => operand.Type));
        Assert.NotNull(store.Payload);
        Assert.Single(instructions, instruction =>
            instruction.Operation is IReadWriteStructuredBufferLengthOperation);
        Assert.Single(instructions, instruction =>
            instruction.Operation is IReadOnlyStructuredBufferLengthOperation);
        var sourceLoad = Assert.Single(instructions, instruction =>
            instruction.Operation is IReadOnlyStructuredBufferLoadOperation);
        Assert.Equal(element == typeof(int) ? ShaderType.I32 : ShaderType.U32,
            sourceLoad.Result?.Type);

        var reflection = new ShaderModuleReflection();
        var bindings = reflection.GetStorageBufferBindings(raw);
        var outputBinding = Assert.Single(bindings, binding => binding.Name == "Output");
        Assert.Equal(GPUBufferBindingType.Storage, outputBinding.Kind);
        Assert.Equal(GPUShaderStage.Compute, outputBinding.Visibility);
        Assert.Equal(4u, outputBinding.ElementStride);
        Assert.Equal(4ul, outputBinding.MinimumBindingSize);
        var layout = reflection.GetBindGroupLayoutDescriptor(raw, 0).Entries.ToArray();
        Assert.Equal(GPUBufferBindingType.Storage,
            Assert.Single(layout, entry => entry.Binding == 1).Buffer.Type);
        Assert.Equal(4ul, Assert.Single(layout, entry => entry.Binding == 1).Buffer.MinBindingSize);

        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        var reflectionJson = await new SlangService().ReflectAsync(slang);
        output.WriteLine(slang);
        output.WriteLine(wgsl);
        Assert.Contains($"RWStructuredBuffer<{spelling}>", slang);
        Assert.Contains($"array<{spelling}>", wgsl);
        Assert.Contains("var<storage, read_write>", wgsl);
        Assert.Contains("] = ", wgsl);
        if (element == typeof(uint))
            Assert.Matches(@"Output_0\[[^\]]+\]\s*=\s*u32\(i32\([^\n]*Input_0\[[^\]]+\]\)\)",
                wgsl);
        else
            Assert.Matches(@"Output_0\[[^\]]+\]\s*=\s*[^\n]*Input_0\[[^\]]+\]\s*-\s*i32\(7\)",
                wgsl);
        using var target = JsonDocument.Parse(reflectionJson);
        var parameter = Assert.Single(target.RootElement.GetProperty("parameters")
            .EnumerateArray(), item =>
            item.GetProperty("binding").GetProperty("index").GetInt32() == 1);
        var resultType = parameter.GetProperty("type");
        Assert.Equal("readWrite", resultType.GetProperty("access").GetString());
        Assert.Equal("structuredBuffer", resultType.GetProperty("baseShape").GetString());
        Assert.Equal(scalar, resultType.GetProperty("resultType")
            .GetProperty("scalarType").GetString());
    }

    [Fact]
    public void UnsignedCopyNormalizesLoadThenRestoresBothStoreOperands()
    {
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(new UnsignedCopyShader());
        var instructions = Instructions(Function(compiled, "Run")).ToArray();
        var load = Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLoadOperation<UIntType<N32>>);
        var normalized = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>> &&
            ReferenceEquals(instruction.Operand0, load.Result));
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<UIntType<N32>>);
        var restored = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>> &&
            ReferenceEquals(instruction.Result, store[2]));
        Assert.Same(normalized.Result, restored.Operand0);
        Assert.Equal(ShaderType.U32, store.Operand1?.Type);
        Assert.NotSame(store.Operand1, store[2]);
    }

    [Fact]
    public void HighBitIndexAndValueAreDistinctCompileOnlyStoreInputs()
    {
        var method = typeof(HighBitUnsignedStoreShader).GetMethod("Run")!;
        var raw = new RuntimeReflectionParser().ParseMethod(method);
        var cil = CompilerTestPipeline.RawBody(raw, method).Code.Instructions;
        Assert.Contains(cil, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldc_I4 &&
            instruction.Instruction.Operand is int value && value == int.MinValue);
        Assert.Contains(cil, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldc_I4_M1);

        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(raw);
        var instructions = Instructions(Function(compiled, "Run")).ToArray();
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<UIntType<N32>>);
        Assert.Null(store.Result);
        Assert.NotSame(store.Operand1, store[2]);
        Assert.Equal(ShaderType.U32, store.Operand1?.Type);
        Assert.Equal(ShaderType.U32, (store[2] ??
            throw new InvalidOperationException("Store is missing its value.")).Type);
        var index = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>> &&
            ReferenceEquals(instruction.Result, store.Operand1));
        var valueConversion = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>> &&
            ReferenceEquals(instruction.Result, store[2]));
        Assert.Equal(int.MinValue,
            Assert.IsType<I32Literal>(Assert.IsType<LiteralValue>(Assert.Single(instructions,
                instruction => ReferenceEquals(instruction.Result, index.Operand0)).Operand0).Value).Value);
        Assert.Equal(-1,
            Assert.IsType<I32Literal>(Assert.IsType<LiteralValue>(Assert.Single(instructions,
                instruction => ReferenceEquals(instruction.Result, valueConversion.Operand0)).Operand0).Value).Value);

        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL))
            .Emit(new HighBitUnsignedStoreShader());
        Assert.Contains("var<storage, read_write>", wgsl);
        Assert.Contains("u32(2147483648)", wgsl);
        Assert.Contains("u32(4294967295)", wgsl);
        output.WriteLine("Compile only: the high-bit index is deliberately out of bounds; never dispatch.");
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, false)]
    [InlineData(0x7fffffffu, false)]
    [InlineData(0x80000000u, false)]
    [InlineData(0x80000001u, true)]
    [InlineData(uint.MaxValue, true)]
    public void UnsignedCopyAndComparisonOraclesPreserveBothSidesOfSignBit(
        uint input, bool greaterThanHighBit)
    {
        Assert.Equal(input, unchecked((uint)(int)input));
        Assert.Equal(greaterThanHighBit, input > 0x80000000u);
    }

    [Fact]
    public void InPlaceUnsignedLoadAndStoreKeepDistinctTypedEffects()
    {
        var shader = new UnsignedInPlaceShader();
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(shader)
            .RunPass(new FunctionToOperationPass());
        var function = Function(module, "Run");
        var instructions = Instructions(function).ToArray();
        var length = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLengthOperation<UIntType<N32>>);
        var load = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLoadOperation<UIntType<N32>>);
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<UIntType<N32>>);
        var effects = FunctionEffectAnalysis.Analyze(module)[function.Declaration];
        Assert.True(effects.IsComplete);
        Assert.DoesNotContain(effects.RequirementSites, site =>
            ReferenceEquals(site.Operation, length.Operation));
        Assert.Equal(OperationRequirement.MemoryRead, Assert.Single(effects.RequirementSites,
            site => ReferenceEquals(site.Operation, load.Operation)).Requirements);
        Assert.Equal(OperationRequirement.MemoryWrite, Assert.Single(effects.RequirementSites,
            site => ReferenceEquals(site.Operation, store.Operation)).Requirements);
        Assert.Same(store.Payload, Assert.Single(effects.RequirementSites,
            site => ReferenceEquals(site.Operation, store.Operation)).Payload);
        Assert.Null(store.Result);
        Assert.Same(load.Operand0, store.Operand0);
        Assert.Equal(ShaderType.U32, (store[2] ??
            throw new InvalidOperationException("Store is missing its value.")).Type);
        Assert.Contains(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>> &&
            ReferenceEquals(instruction.Operand0, load.Result));
        Assert.Contains("var<storage, read_write>",
            new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader));
    }

    [Fact]
    public void WritableIntegerLengthCannotProveUniformityDespiteNoMemoryRequirement()
    {
        var operation = ReadWriteStructuredBufferLengthOperation<UIntType<N32>>.Instance;
        Assert.Equal(OperationRequirement.None, operation.Requirements);
        var instruction = Instruction<IShaderValue, IShaderValue>.Create(
            operation, ShaderValue.Intermediate(ShaderType.U32),
            [ShaderValue.Intermediate(operation.BufferPointerType)]);
        var label = DualDrill.CLSL.Language.Symbol.Label.Create("length");
        var completed = new Dictionary<FunctionDeclaration, CooperationFunctionUniformityFacts>();
        var formals = new Dictionary<ParameterPointerValue, int>();
        var requirements = new Dictionary<(DualDrill.CLSL.Language.Symbol.Label, int),
            OperationRequirementSite>();
        var unknowns = new Dictionary<(DualDrill.CLSL.Language.Symbol.Label, int),
            FunctionEffectUnknownSite>();
        var classification = CooperationUniformity.Classify(
            instruction, label, 0,
            static _ => CooperationUniformity.DependencyLattice.Empty,
            completed, formals, requirements, unknowns);
        Assert.IsType<CooperationUniformDependencies.Unknown>(classification.Dependencies);

        var arithmetic = Instruction<IShaderValue, IShaderValue>.Create(
            NumericBinaryArithmeticOperation<UIntType<N32>, BinaryArithmetic.Add>.Instance,
            ShaderValue.Intermediate(ShaderType.U32),
            [instruction.Result!, ShaderValue.Literal(new U32Literal(1u))]);
        var derived = CooperationUniformity.Classify(
            arithmetic, label, 1,
            value => ReferenceEquals(value, instruction.Result)
                ? classification.Dependencies
                : CooperationUniformity.DependencyLattice.Empty,
            completed, formals, requirements, unknowns);
        Assert.IsType<CooperationUniformDependencies.Unknown>(derived.Dependencies);
    }

    [Fact]
    public void SignedInPlaceLoadAndStoreRemainI32()
    {
        var shader = new SignedInPlaceShader();
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(shader);
        var instructions = Instructions(Function(compiled, "Run")).ToArray();
        var load = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLoadOperation<IntType<N32>>);
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<IntType<N32>>);
        Assert.Equal(ShaderType.I32, load.Result?.Type);
        Assert.Equal(ShaderType.I32, (store[2] ??
            throw new InvalidOperationException("Store is missing its value.")).Type);
        Assert.Null(store.Result);
        Assert.Contains("array<i32>",
            new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader));
    }

    [Fact]
    public void SignedNegativeValueRemainsI32AtTheResultlessStore()
    {
        var method = typeof(SignedConstantShader).GetMethod("Run")!;
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Compile(new RuntimeReflectionParser().ParseMethod(method));
        var instructions = Instructions(Function(compiled, "Run")).ToArray();
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<IntType<N32>>);
        Assert.Null(store.Result);
        Assert.Equal(ShaderType.I32, (store[2] ??
            throw new InvalidOperationException("Store is missing its value.")).Type);
        Assert.Equal(-7, Assert.IsType<I32Literal>(
            Assert.IsType<LiteralValue>(Assert.Single(instructions,
                instruction => ReferenceEquals(instruction.Result, store[2])).Operand0).Value).Value);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL))
            .Emit(new SignedConstantShader());
        Assert.Contains("i32(-7)", wgsl);
        Assert.Contains("array<i32>", wgsl);
    }

    [Fact]
    public void UnsignedStoreWithPrefixPreservesOriginalIndexValueAndPrefix()
    {
        var method = CreatePrefixStoreMethod<uint>();
        var raw = new RuntimeReflectionParser().ParseMethod(method);
        Assert.Contains(CompilerTestPipeline.RawBody(raw, method).Code.Instructions,
            instruction => Equals(instruction.Instruction.Operand,
                typeof(RWStructuredBuffer<uint>).GetProperty("Item")!.SetMethod));
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(raw);
        var body = Function(compiled, "StoreWithPrefix");
        var instructions = Instructions(body).ToArray();
        var prefix = Assert.Single(instructions, instruction =>
            instruction.Operation is LoadOperation &&
            ReferenceEquals(instruction.Operand0, body.Declaration.Parameters[0].Value));
        var index = Assert.Single(instructions, instruction =>
            instruction.Operation is LoadOperation &&
            ReferenceEquals(instruction.Operand0, body.Declaration.Parameters[1].Value));
        var value = Assert.Single(instructions, instruction =>
            instruction.Operation is LoadOperation &&
            ReferenceEquals(instruction.Operand0, body.Declaration.Parameters[2].Value));
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<UIntType<N32>>);
        var indexConversion = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>> &&
            ReferenceEquals(instruction.Result, store.Operand1));
        var valueConversion = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>> &&
            ReferenceEquals(instruction.Result, store[2]));
        var normalizedIndex = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>> &&
            ReferenceEquals(instruction.Operand0, index.Result));
        var normalizedValue = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>> &&
            ReferenceEquals(instruction.Operand0, value.Result));
        var normalizedPrefix = Assert.Single(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>> &&
            ReferenceEquals(instruction.Operand0, prefix.Result));
        Assert.Same(normalizedIndex.Result, indexConversion.Operand0);
        Assert.Same(normalizedValue.Result, valueConversion.Operand0);
        Assert.DoesNotContain(store.Operands, operand =>
            ReferenceEquals(operand, prefix.Result) ||
            ReferenceEquals(operand, normalizedPrefix.Result));
        Assert.Contains(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>> &&
            ReferenceEquals(instruction.Operand0, normalizedPrefix.Result));
        Assert.Null(store.Result);
        Assert.Equal(ShaderType.U32, body.Declaration.Return.Type);
    }

    [Fact]
    public void UnsupportedWritableGenericInstancesFailWithoutPoisoningHolders()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Throws<NotSupportedException>(() =>
                ReadWriteStructuredBufferType<FloatType<N64>>.Instance);
            Assert.Throws<NotSupportedException>(() =>
                ReadWriteStructuredBufferLengthOperation<FloatType<N64>>.Instance);
            Assert.Throws<NotSupportedException>(() =>
                ReadWriteStructuredBufferLoadOperation<FloatType<N64>>.Instance);
            Assert.Throws<NotSupportedException>(() =>
                ReadWriteStructuredBufferStoreOperation<FloatType<N64>>.Instance);
        }
        Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseType(typeof(RWStructuredBuffer<double>)));
        Assert.NotNull(ReadWriteStructuredBufferStoreOperation<UIntType<N32>>.Instance);
        Assert.NotNull(StructuredBufferLoadOperation<FloatType<N32>>.Instance);
    }

    [Theory]
    [InlineData(typeof(RWStructuredBuffer<int>))]
    [InlineData(typeof(RWStructuredBuffer<uint>))]
    public void RawDuplicateGroupMetadataStillRejectsIntegerWritableResources(Type buffer)
    {
        var (shaderType, field) = CreateRawDuplicateGroupWritableShader(buffer);
        Assert.Equal(2, field.GetCustomAttributes<GroupAttribute>(inherit: false).Count());
        var shader = (ISharpShader)Activator.CreateInstance(shaderType)!;
        var error = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(shader));
        Assert.Contains("found 0, 2, and 1", error.Message);
    }

    [Fact]
    public void IntegerWritableResourcesKeepStaticOnlyPlacementAndComputeVisibility()
    {
        Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(Method(nameof(UnsignedWritableParameter))));
        Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new IntegerWritableCopyShader()));
        var stageError = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new IntegerWritableFragmentShader()));
        Assert.Contains("compute entry points only", stageError.Message);
        var hintError = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new IntegerWritableVertexHintShader()));
        Assert.Contains("read-write storage buffer attribute(s) [Vertex]", hintError.Message);
    }

    [Fact]
    public void IntegerWritableStoresHaveCanonicalDirectIrAndRejectMalformedSignatures()
    {
        foreach (var operation in new IReadWriteStructuredBufferStoreOperation[]
                 {
                     ReadWriteStructuredBufferStoreOperation<IntType<N32>>.Instance,
                     ReadWriteStructuredBufferStoreOperation<UIntType<N32>>.Instance
                 })
        {
            var bufferType = operation.BufferPointerType.BaseType;
            var output = new VariableDeclaration(StorageAddressSpace.Instance,
                "Output", bufferType, [new GroupAttribute(0), new BindingAttribute(0)]);
            var index = ShaderValue.Intermediate(ShaderType.U32);
            var value = ShaderValue.Intermediate(operation.ElementType);
            var payload = new object();
            var valid = Instruction<IShaderValue, IShaderValue>.Create(
                operation, null, [output.Value, index, value], payload);
            var target = new SlangTargetLowering().Lower(OperationModule(valid, [output]));
            var body = Assert.Single(target.FunctionDefinitions).Value;
            var origin = Assert.Single(body.Origins.Instructions);
            var assign = Assert.IsType<SlangAssign>(origin.Target);
            var place = Assert.IsType<SlangIndexedPlace>(assign.Target);
            Assert.Equal(valid, origin.Source);
            Assert.Same(payload, origin.Source.Payload);
            Assert.Same(output, Assert.IsType<SlangVariablePlace>(place.Target).Variable);
            Assert.Same(index, Assert.IsType<SlangValueOperand>(place.Index).Value);
            Assert.Same(value, Assert.IsType<SlangValueOperand>(assign.Value).Value);
            Assert.Same(operation.ElementType, place.ElementType);
            Assert.Single(TargetStatements(body.Body).OfType<SlangAssign>());

            IShaderType wrongElementBuffer = operation.ElementType == ShaderType.I32
                ? ReadWriteStructuredBufferType<UIntType<N32>>.Instance
                : ReadWriteStructuredBufferType<IntType<N32>>.Instance;
            var malformed = new[]
            {
                valid with { Result = ShaderValue.Intermediate(UnitType.Instance) },
                valid with { Operand1 = ShaderValue.Intermediate(ShaderType.I32) },
                valid with { RestOperands = [index, ShaderValue.Intermediate(
                    operation.ElementType == ShaderType.I32 ? ShaderType.U32 : ShaderType.I32)] },
                valid with { RestOperands = [] },
                valid with { RestOperands = [index, value, value] },
                valid with { RestOperands = default },
                valid with { Operand0 = ShaderValue.Intermediate(
                    bufferType.GetPtrType(GenericAddressSpace.Instance)) },
                valid with { Operand0 = ShaderValue.Intermediate(
                    bufferType.GetPtrType(FunctionAddressSpace.Instance)) },
                valid with { Operand0 = ShaderValue.Intermediate(
                    wrongElementBuffer.GetPtrType(StorageAddressSpace.Instance)) },
                valid with { Operand0 = ShaderValue.Intermediate(
                    ReadOnlyStructuredBufferType<IntType<N32>>.Instance
                        .GetPtrType(StorageAddressSpace.Instance)) }
            };
            Assert.All(malformed, instruction =>
            {
                var error = Assert.Throws<NotSupportedException>(() =>
                    new SlangTargetLowering().Lower(OperationModule(instruction, [output])));
                Assert.Contains("invalid read-write storage-buffer store signature", error.Message);
            });

            var call = Instruction<IShaderValue, IShaderValue>.Create(
                new CallOperation((FunctionType)operation.Function.Type), null,
                [operation.Function, output.Value, index, value], payload);
            var normalized = OperationModule(call, [output]).RunPass(new FunctionToOperationPass());
            var actual = Assert.Single(Instructions(Function(normalized, "Entry")));
            Assert.Same(operation, actual.Operation);
            Assert.Same(payload, actual.Payload);
            Assert.Null(actual.Result);

            AssertOperationMismatch(call with { Result = ShaderValue.Intermediate(UnitType.Instance) });
            AssertOperationMismatch(call with
            {
                RestOperands = [index, ShaderValue.Intermediate(
                    operation.ElementType == ShaderType.I32 ? ShaderType.U32 : ShaderType.I32)]
            });
            var proxy = new FunctionDeclaration("Proxy", operation.Function.Parameters,
                operation.Function.Return, [operation.GetOperationMethodAttribute()]);
            AssertOperationMismatch(call with { Operand0 = proxy });

            var forged = new ForgedWritableStore(operation);
            var fake = valid with { Operation = forged };
            var fakeError = Assert.Throws<NotSupportedException>(() =>
                new SlangTargetLowering().Lower(OperationModule(fake, [output])));
            Assert.Contains("invalid read-write storage-buffer store signature", fakeError.Message);
        }
    }

    [Fact]
    public void ForgedWritableLoadAndLengthInterfacesAreNotAdmissionAuthority()
    {
        var resource = new VariableDeclaration(StorageAddressSpace.Instance,
            "Output", ReadWriteStructuredBufferType<UIntType<N32>>.Instance,
            [new GroupAttribute(0), new BindingAttribute(0)]);
        IReadWriteStructuredBufferLengthOperation length =
            new ForgedWritableLength(ReadWriteStructuredBufferLengthOperation<UIntType<N32>>.Instance);
        IReadWriteStructuredBufferLoadOperation load =
            new ForgedWritableLoad(ReadWriteStructuredBufferLoadOperation<UIntType<N32>>.Instance);
        var badLength = Instruction<IShaderValue, IShaderValue>.Create(
            length, ShaderValue.Intermediate(ShaderType.U32), [resource.Value]);
        var badLoad = Instruction<IShaderValue, IShaderValue>.Create(
            load, ShaderValue.Intermediate(ShaderType.U32),
            [resource.Value, ShaderValue.Intermediate(ShaderType.U32)]);
        Assert.All(new[] { badLength, badLoad }, instruction =>
            Assert.Throws<NotSupportedException>(() =>
                new SlangTargetLowering().Lower(OperationModule(instruction, [resource]))));
    }

    [Fact]
    public void ExactClosedTypeAndAllAccessorsAreRegistered()
    {
        var parser = new RuntimeReflectionParser();
        var type = Assert.IsType<ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>>(
            parser.ParseType(typeof(RWStructuredBuffer<float>)));
        Assert.Equal(ShaderType.F32, type.ElementType);
        Assert.Equal(4u, type.ElementStride);

        var raw = parser.ParseShaderModule(new DoubleValuesShader());
        var outputResource = Assert.Single(
            raw.Declarations.OfType<VariableDeclaration>(),
            declaration => declaration.Name == "Output");
        Assert.Same(ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance, outputResource.Type);
        Assert.Same(StorageAddressSpace.Instance, outputResource.AddressSpace);

        var methods = CompilerTestPipeline.RawBody(
                raw,
                typeof(DoubleValuesShader).GetMethod(nameof(DoubleValuesShader.Run))!)
            .Code.Instructions
            .Where(instruction => instruction.Instruction.Operand is MethodInfo)
            .Select(instruction => (MethodInfo)instruction.Instruction.Operand)
            .ToArray();
        var wrapper = typeof(RWStructuredBuffer<float>);
        var indexer = wrapper.GetProperty("Item")!;
        Assert.Contains(wrapper.GetProperty(nameof(RWStructuredBuffer<float>.Length))!.GetMethod!, methods);
        Assert.Contains(indexer.SetMethod!, methods);

        var inPlaceRaw = new RuntimeReflectionParser().ParseShaderModule(new InPlaceIncrementShader());
        var inPlaceMethods = CompilerTestPipeline.RawBody(
                inPlaceRaw,
                typeof(InPlaceIncrementShader).GetMethod(nameof(InPlaceIncrementShader.Run))!)
            .Code.Instructions
            .Where(instruction => instruction.Instruction.Operand is MethodInfo)
            .Select(instruction => (MethodInfo)instruction.Instruction.Operand);
        Assert.Contains(indexer.GetMethod!, inPlaceMethods);
    }

    [Fact]
    public void ActualCilUsesDirectAddressedAccessorsAndBothBoundsGuards()
    {
        var method = typeof(DoubleValuesShader).GetMethod(nameof(DoubleValuesShader.Run))
            ?? throw new InvalidOperationException("Compute entry was not found.");
        var instructions = CilMethodDecoder.Decode(method).Instructions;

        Assert.Equal(4, instructions.Count(instruction => instruction.Instruction.OpCode == OpCodes.Ldsflda));
        Assert.Equal(2, instructions.Count(instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: "get_Length" }));
        Assert.Single(instructions, instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: "get_Item" });
        Assert.Single(instructions, instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: "set_Item" });
        Assert.Equal(2, instructions.Count(instruction =>
            instruction.Instruction.OpCode is var opcode &&
            (opcode == OpCodes.Bge_Un || opcode == OpCodes.Bge_Un_S || opcode == OpCodes.Clt_Un)));
        Assert.DoesNotContain(instructions, instruction => instruction.Instruction.OpCode == OpCodes.Callvirt);
        Assert.DoesNotContain(instructions, instruction =>
            instruction.Instruction.OpCode.Name?.Contains("elem", StringComparison.Ordinal) is true);
    }

    [Fact]
    public void DoubleValuesCompilesThroughTypedIrSlangAndWgsl()
    {
        var shader = new DoubleValuesShader();
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(shader);
        var instructions = Instructions(Function(compiled, nameof(DoubleValuesShader.Run))).ToArray();

        Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Null(store.Result);
        Assert.Equal(
            [
                ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance.BufferPointerType,
                ShaderType.U32,
                ShaderType.F32
            ],
            store.Operands.Select(operand => operand.Type));

        var ir = new CLSLCompiler(new(CLSLCompileTarget.IR)).Emit(shader);
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        output.WriteLine(slang);
        output.WriteLine(wgsl);

        Assert.Contains("rw-structured-buffer-length-f32", ir);
        Assert.Contains("rw-structured-buffer-store-f32", ir);
        Assert.Contains("RWStructuredBuffer<f32>", slang);
        Assert.Equal(2, Count(slang, ".GetDimensions("));
        Assert.Matches(@"v_\d+_Output\[v_\d+\] = v_\d+;", slang);
        Assert.Contains("var<storage, read>", wgsl);
        Assert.Contains("var<storage, read_write>", wgsl);
        Assert.Equal(2, Count(wgsl, "arrayLength("));
        Assert.Contains("v_1_Output_0[", wgsl);
        Assert.Contains("] = ", wgsl);
    }

    [Fact]
    public void InPlaceIncrementExercisesRwLoadAndStore()
    {
        var shader = new InPlaceIncrementShader();
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(shader);
        var instructions = Instructions(Function(compiled, nameof(InPlaceIncrementShader.Run))).ToArray();

        Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>);

        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        Assert.Matches(@"v_\d+_Values\[v_\d+\]", slang);
        Assert.Contains("var<storage, read_write>", wgsl);
    }

    [Fact]
    public void ResourceEffectsRetainEachWritableSiteAndOrigin()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(new InPlaceIncrementShader())
            .RunPass(new FunctionToOperationPass());
        var function = Function(module, nameof(InPlaceIncrementShader.Run));
        var instructions = Instructions(function).ToArray();
        var resourceOperations = instructions
            .Where(instruction => instruction.Operation is
                ReadWriteStructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>> or
                ReadWriteStructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>> or
                ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>)
            .ToArray();

        var summary = FunctionEffectAnalysis.Analyze(module)[function.Declaration];

        Assert.True(summary.IsComplete);
        Assert.Equal(3, resourceOperations.Length);
        Assert.DoesNotContain(summary.RequirementSites, site =>
            site.Operation is ReadWriteStructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var load = Assert.Single(summary.RequirementSites, site =>
            site.Operation is ReadWriteStructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var store = Assert.Single(summary.RequirementSites, site =>
            site.Operation is ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Equal(OperationRequirement.MemoryRead, load.Requirements);
        Assert.Equal(OperationRequirement.MemoryWrite, store.Requirements);
        Assert.NotNull(load.Payload);
        Assert.NotNull(store.Payload);
        Assert.Same(
            resourceOperations.Single(instruction =>
                instruction.Operation is ReadWriteStructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>).Payload,
            load.Payload);
        Assert.Same(
            resourceOperations.Single(instruction =>
                instruction.Operation is ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>).Payload,
            store.Payload);
    }

    [Fact]
    public async Task ReflectionDistinguishesUniformReadOnlyAndWritableStorage()
    {
        var shader = new MixedBindingsShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var module = compiler.Parse(shader);
        var reflection = new ShaderModuleReflection();
        var storage = reflection.GetStorageBufferBindings(module);

        Assert.Collection(
            storage,
            writable =>
            {
                Assert.Equal("Output", writable.Name);
                Assert.Equal(0, writable.Binding);
                Assert.Equal(GPUBufferBindingType.Storage, writable.Kind);
                Assert.Equal(GPUShaderStage.Compute, writable.Visibility);
                Assert.Equal(4u, writable.ElementStride);
                Assert.Equal(4ul, writable.MinimumBindingSize);
            },
            readOnly =>
            {
                Assert.Equal("Input", readOnly.Name);
                Assert.Equal(2, readOnly.Binding);
                Assert.Equal(GPUBufferBindingType.ReadOnlyStorage, readOnly.Kind);
                Assert.Equal(
                    GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute,
                    readOnly.Visibility);
            });

        var entries = reflection.GetBindGroupLayoutDescriptor(module, 0).Entries.ToArray();
        Assert.Equal([0, 1, 2], entries.Select(entry => entry.Binding));
        Assert.Equal(
            [GPUBufferBindingType.Storage, GPUBufferBindingType.Uniform, GPUBufferBindingType.ReadOnlyStorage],
            entries.Select(entry => entry.Buffer.Type));
        Assert.Equal(
            entries.Select(entry => (entry.Binding, entry.Visibility, entry.Buffer)),
            reflection.GetBindGroupLayoutDescriptorBuffer(module, 0).Entries.ToArray()
                .Select(entry => (entry.Binding, entry.Visibility, entry.Buffer)));

        var targetText = await new SlangService().ReflectAsync(compiler.Emit(shader));
        output.WriteLine(targetText);
        using var target = JsonDocument.Parse(targetText);
        var targetBindings = target.RootElement.GetProperty("parameters")
            .EnumerateArray()
            .ToDictionary(
                parameter =>
                {
                    var binding = parameter.GetProperty("binding");
                    return (
                        Space: binding.TryGetProperty("space", out var space) ? space.GetInt32() : 0,
                        Index: binding.GetProperty("index").GetInt32());
                });
        var writableTargetType = targetBindings[(0, 0)].GetProperty("type");
        Assert.Equal("structuredBuffer", writableTargetType.GetProperty("baseShape").GetString());
        Assert.Equal("readWrite", writableTargetType.GetProperty("access").GetString());
        Assert.Equal("structuredBuffer", targetBindings[(0, 2)].GetProperty("type")
            .GetProperty("baseShape").GetString());
    }

    [Theory]
    [InlineData(typeof(WritableFragmentEntryShader), "compute entry points only")]
    [InlineData(typeof(WritableVertexEntryShader), "compute entry points only")]
    [InlineData(typeof(WritableFragmentHintShader), "read-write storage buffer attribute(s) [Fragment]")]
    [InlineData(typeof(WritableVertexHintShader), "read-write storage buffer attribute(s) [Vertex]")]
    public void WritableProfileRejectsGraphicsVisibilityAndEntries(Type shaderType, string expected)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));
        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(shader));

        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void WritableBindingMetadataAndDescriptorProjectionAreStrict()
    {
        var duplicate = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new DuplicateWritableBindingShader()));
        Assert.Contains("resource binding (0, 0) is duplicated", duplicate.Message);

        var crossKind = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new WritableUniformCollisionShader()));
        Assert.Contains("resource binding (0, 0) is duplicated", crossKind.Message);

        var dynamicModule = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Parse(new ExplicitComputeWritableShader());
        var reflection = new ShaderModuleReflection();
        var binding = Assert.Single(reflection.GetStorageBufferBindings(dynamicModule));
        Assert.Equal(GPUShaderStage.Compute, binding.Visibility);
        Assert.True(binding.HasDynamicOffset);
        Assert.Empty(reflection.GetBindGroupLayoutDescriptor(dynamicModule, 1).Entries.ToArray());
    }

    [Fact]
    public void RawDuplicateGroupMetadataOnWritableBufferIsRejected()
    {
        var (shaderType, field) = CreateRawDuplicateGroupWritableShader(typeof(RWStructuredBuffer<float>));
        Assert.Equal(2, field.GetCustomAttributes<GroupAttribute>(inherit: false).Count());
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(shader));

        Assert.Equal(
            $"Shader module metadata validation rejected field '{shaderType.FullName}.Output': " +
            "a read-write storage buffer requires no address-space attribute and exactly one [Group] and " +
            "[Binding] attribute (found 0, 2, and 1).",
            exception.Message);
    }

    [Fact]
    public void HelperOnlyWritableDirectIrIsAllowed()
    {
        var resource = new VariableDeclaration(
            StorageAddressSpace.Instance,
            "Output",
            ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance,
            [new GroupAttribute(0), new BindingAttribute(0)]);
        var function = new FunctionDeclaration(
            "Helper",
            [],
            new FunctionReturn(UnitType.Instance, []),
            []);
        var module = VoidModule(function, [], [resource]);

        Assert.Single(new ShaderModuleReflection().GetStorageBufferBindings(module));
        Assert.NotNull(new SlangTargetLowering().Lower(module));
    }

    [Theory]
    [InlineData(typeof(MissingWritableMetadataShader), "exactly one [Group] and [Binding]")]
    [InlineData(typeof(WrongWritableElementShader), "only RWStructuredBuffer<float>, RWStructuredBuffer<int>, and RWStructuredBuffer<uint>")]
    [InlineData(typeof(ConflictingWritableUniformShader), "requires no address-space attribute")]
    [InlineData(typeof(ConflictingWritableReadWriteShader), "attribute(s) [ReadWrite]")]
    [InlineData(typeof(WritableLocalShader), "local variable")]
    [InlineData(typeof(WritablePropertyShader), "static shader-module fields")]
    public void InvalidWritableDeclarationsAreRejected(Type shaderType, string expected)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));
        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Emit(shader));

        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void WritableUserParameterIsRejected()
    {
        var method = Method(nameof(WritableParameter));
        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains("structured buffers are valid only as static shader-module fields", exception.Message);
    }

    [Fact]
    public void HighBitStoreIndexIsConvertedAndRhsIsEvaluatedOnce()
    {
        var method = typeof(HighBitStoreShader).GetMethod(nameof(HighBitStoreShader.Store))
            ?? throw new InvalidOperationException("High-bit store helper was not found.");
        var raw = new RuntimeReflectionParser().ParseMethod(method);
        var rawInstructions = CompilerTestPipeline.RawBody(raw, method).Code.Instructions;
        Assert.Contains(rawInstructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldc_I4 &&
            instruction.Instruction.Operand is int value &&
            value == int.MinValue);
        Assert.Single(rawInstructions, instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: nameof(HighBitStoreShader.Value) });

        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(raw);
        var instructions = Instructions(Function(compiled, nameof(HighBitStoreShader.Store))).ToArray();
        Assert.Contains(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<
                IntType<DualDrill.Common.Nat.N32>,
                UIntType<DualDrill.Common.Nat.N32>>);
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Null(store.Result);
        Assert.Equal(ShaderType.U32, store.Operand1?.Type);
        Assert.Single(instructions, instruction =>
            instruction.Operation is CallOperation &&
            instruction.Operand0 is FunctionDeclaration { Name: nameof(HighBitStoreShader.Value) });
    }

    [Fact]
    public void StoreConversionPreservesAnExistingStackPrefix()
    {
        var method = CreatePrefixStoreMethod<float>();
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Compile(new RuntimeReflectionParser().ParseMethod(method));
        var function = Function(compiled, "StoreWithPrefix");
        var store = Assert.Single(Instructions(function), instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>);

        Assert.Null(store.Result);
        Assert.Equal(ShaderType.F32, function.Declaration.Return.Type);
    }

    [Fact]
    public void StoreCallNormalizationRequiresNullResultAndExactSignatures()
    {
        var operation = ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var receiver = ShaderValue.Intermediate(operation.BufferPointerType);
        var index = ShaderValue.Intermediate(ShaderType.U32);
        var value = ShaderValue.Intermediate(ShaderType.F32);
        var payload = new object();
        var callType = new FunctionType(
            [operation.BufferPointerType, ShaderType.U32, ShaderType.F32],
            UnitType.Instance);
        var valid = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation(callType),
            null,
            [operation.Function, receiver, index, value],
            payload);

        var normalized = OperationModule(valid).RunPass(new FunctionToOperationPass());
        var store = Assert.Single(Instructions(Function(normalized, "Entry")));
        Assert.IsType<ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>>(store.Operation);
        Assert.Null(store.Result);
        Assert.Same(payload, store.Payload);

        var unitResult = valid with { Result = ShaderValue.Intermediate(UnitType.Instance) };
        AssertOperationMismatch(unitResult);
        AssertOperationMismatch(valid with
        {
            Operand1 = ShaderValue.Intermediate(
                ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(FunctionAddressSpace.Instance))
        });
        AssertOperationMismatch(valid with
        {
            Operand1 = ShaderValue.Intermediate(
                ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(GenericAddressSpace.Instance))
        });
        AssertOperationMismatch(valid with
        {
            RestOperands = [ShaderValue.Intermediate(ShaderType.I32), value]
        });
        AssertOperationMismatch(valid with
        {
            RestOperands = [index, ShaderValue.Intermediate(ShaderType.U32)]
        });
        AssertOperationMismatch(valid with
        {
            Operation = new CallOperation(new FunctionType([], UnitType.Instance))
        });

        var proxy = new FunctionDeclaration(
            "BadStore",
            operation.Function.Parameters,
            operation.Function.Return,
            [operation.GetOperationMethodAttribute()]);
        AssertOperationMismatch(valid with { Operand0 = proxy });

        var ordinary = new FunctionDeclaration(
            "Ordinary",
            [],
            new FunctionReturn(UnitType.Instance, []),
            []);
        var ordinaryCall = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation((FunctionType)ordinary.Type),
            ShaderValue.Intermediate(UnitType.Instance),
            [ordinary]);
        var unchanged = Assert.Single(Instructions(
            Function(OperationModule(ordinaryCall).RunPass(new FunctionToOperationPass()), "Entry")));
        Assert.IsType<CallOperation>(unchanged.Operation);
        Assert.IsType<UnitType>(unchanged.Result?.Type);
    }

    [Fact]
    public void ResourceExpressionNormalizationRejectsMissingResultsWithoutNullReference()
    {
        var roLength = StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var roLoad = StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var rwLength = ReadWriteStructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var rwLoad = ReadWriteStructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var u32 = ShaderValue.Intermediate(ShaderType.U32);

        var malformed = new[]
        {
            Instruction<IShaderValue, IShaderValue>.Create(
                new CallOperation((FunctionType)roLength.Function.Type),
                null,
                [roLength.Function, ShaderValue.Intermediate(roLength.BufferPointerType)]),
            Instruction<IShaderValue, IShaderValue>.Create(
                new CallOperation((FunctionType)roLoad.Function.Type),
                null,
                [roLoad.Function, ShaderValue.Intermediate(roLoad.BufferPointerType), u32]),
            Instruction<IShaderValue, IShaderValue>.Create(
                new CallOperation((FunctionType)rwLength.Function.Type),
                null,
                [rwLength.Function, ShaderValue.Intermediate(rwLength.BufferPointerType)]),
            Instruction<IShaderValue, IShaderValue>.Create(
                new CallOperation((FunctionType)rwLoad.Function.Type),
                null,
                [rwLoad.Function, ShaderValue.Intermediate(rwLoad.BufferPointerType), u32])
        };

        Assert.All(malformed, AssertOperationMismatch);
    }

    [Fact]
    public void TargetStoreUsesIndexedPlaceAndRejectsMalformedShapes()
    {
        var operation = ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var outputResource = new VariableDeclaration(
            StorageAddressSpace.Instance,
            "Output",
            ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance,
            [new GroupAttribute(0), new BindingAttribute(0)]);
        var receiver = outputResource.Value;
        var index = ShaderValue.Intermediate(ShaderType.U32);
        var value = ShaderValue.Intermediate(ShaderType.F32);
        var valid = Instruction<IShaderValue, IShaderValue>.Create(
            operation,
            null,
            [receiver, index, value]);

        var target = new SlangTargetLowering().Lower(OperationModule(valid, [outputResource]));
        var body = Assert.Single(target.FunctionDefinitions).Value;
        var assignment = Assert.Single(TargetStatements(body.Body).OfType<SlangAssign>());
        var indexed = Assert.IsType<SlangIndexedPlace>(assignment.Target);
        var origin = Assert.Single(body.Origins.Instructions);
        Assert.Equal(ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance, indexed.Target.Type);
        Assert.Equal(ShaderType.U32, indexed.Index.Type);
        Assert.Equal(ShaderType.F32, indexed.Type);
        Assert.Same(outputResource, Assert.IsType<SlangVariablePlace>(indexed.Target).Variable);
        Assert.Same(index, Assert.IsType<SlangValueOperand>(indexed.Index).Value);
        Assert.Same(value, Assert.IsType<SlangValueOperand>(assignment.Value).Value);
        Assert.Null(origin.Source.Result);
        Assert.Equal(valid, origin.Source);
        Assert.Same(assignment, origin.Target);
        Assert.Matches(
            @"v_\d+_Output\[v_\d+\] = v_\d+;",
            new SlangEmitter(target).Emit());

        var malformed = new[]
        {
            valid with { Result = ShaderValue.Intermediate(UnitType.Instance) },
            valid with { Operand1 = ShaderValue.Intermediate(ShaderType.I32) },
            valid with { RestOperands = [index, ShaderValue.Intermediate(ShaderType.U32)] },
            valid with
            {
                Operand0 = ShaderValue.Intermediate(
                    ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(FunctionAddressSpace.Instance))
            },
            valid with
            {
                Operand0 = ShaderValue.Intermediate(
                    ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(GenericAddressSpace.Instance))
            },
            valid with
            {
                Operand0 = ShaderValue.Intermediate(
                    ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(StorageAddressSpace.Instance))
            }
        };
        Assert.All(malformed, instruction =>
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                new SlangTargetLowering().Lower(OperationModule(instruction)));
            Assert.Contains("invalid read-write storage-buffer store signature", exception.Message);
        });
    }

    [Fact]
    public void WritableDimensionsOriginPreservesImmediateCrossBlockCapture()
    {
        var module = WritableDimensionsCaptureModule();
        var target = new SlangTargetLowering().Lower(module);
        var body = Assert.Single(target.FunctionDefinitions).Value;
        var dimensions = Assert.Single(body.Origins.Dimensions);
        var capture = Assert.IsType<SlangAssign>(dimensions.Capture);
        var captured = Assert.IsType<SlangValueOperand>(capture.Value);
        var carrier = Assert.IsType<SlangVariablePlace>(capture.Target).Variable;
        var store = Assert.Single(body.Origins.Instructions, origin =>
            origin.Source.Operation is ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var assignment = Assert.IsType<SlangAssign>(store.Target);
        var indexed = Assert.IsType<SlangIndexedPlace>(assignment.Target);
        var index = Assert.IsType<SlangVariablePlace>(
            Assert.IsType<SlangPlaceOperand>(indexed.Index).Place);

        Assert.IsType<ReadWriteStructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>>(dimensions.Source.Operation);
        Assert.Same(dimensions.Source.Result, dimensions.Dimensions.Count);
        Assert.Same(dimensions.Dimensions.Count, captured.Value);
        Assert.Same(body.Origins.Captures[dimensions.Dimensions.Count], carrier);
        Assert.True(AreAdjacent(body.Body, dimensions.Dimensions, capture));
        Assert.Same(carrier, index.Variable);
        Assert.Equal(ShaderType.F32, indexed.ElementType);
    }

    [Fact]
    public void TargetStoreRejectsMissingExtraAndUninitializedRestOperands()
    {
        var operation = ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var receiver = ShaderValue.Intermediate(operation.BufferPointerType);
        var index = ShaderValue.Intermediate(ShaderType.U32);
        var value = ShaderValue.Intermediate(ShaderType.F32);
        var valid = Instruction<IShaderValue, IShaderValue>.Create(
            operation,
            null,
            [receiver, index, value]);
        var malformed = new[]
        {
            valid with { RestOperands = [] },
            valid with { RestOperands = [index, value, value] },
            valid with { RestOperands = default }
        };

        Assert.All(malformed, instruction =>
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                new SlangTargetLowering().Lower(OperationModule(instruction)));
            Assert.Contains("invalid read-write storage-buffer store signature", exception.Message);
        });
    }

    [Fact]
    public void ResourceCallNormalizationRejectsMalformedPhysicalOperandStorage()
    {
        var operation = ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var receiver = ShaderValue.Intermediate(operation.BufferPointerType);
        var index = ShaderValue.Intermediate(ShaderType.U32);
        var value = ShaderValue.Intermediate(ShaderType.F32);
        var valid = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation((FunctionType)operation.Function.Type),
            null,
            [operation.Function, receiver, index, value]);
        var malformed = new[]
        {
            valid with { RestOperands = [index] },
            valid with { RestOperands = [index, value, value] },
            valid with { RestOperands = [index, null!] },
            valid with { RestOperands = default }
        };

        Assert.All(malformed, AssertOperationMismatch);
    }

    [Fact]
    public void SourceOraclesRemainExplicitlyNonGpuExpectations()
    {
        float[] input = [1.0f, 3.0f, 5.0f];

        Assert.Equal([2.0f, 6.0f, 10.0f], input.Select(value => value * 2.0f));
        Assert.Equal([2.0f, 6.0f], input.Take(2).Select(value => value * 2.0f));
    }

    private sealed class ForgedWritableLength(IReadWriteStructuredBufferLengthOperation canonical)
        : IReadWriteStructuredBufferLengthOperation
    {
        public FunctionDeclaration Function => canonical.Function;
        public string Name => canonical.Name;
        public IPtrType BufferPointerType => canonical.BufferPointerType;
        public IOperationMethodAttribute GetOperationMethodAttribute() =>
            canonical.GetOperationMethodAttribute();
        public TO EvaluateInstruction<TV, TR, TS, TO>(
            Instruction<TV, TR> instruction, TS semantic)
            where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
            throw new NotSupportedException("Forged buffer operations have no evaluation semantics.");
    }

    private sealed class ForgedWritableLoad(IReadWriteStructuredBufferLoadOperation canonical)
        : IReadWriteStructuredBufferLoadOperation
    {
        public FunctionDeclaration Function => canonical.Function;
        public string Name => canonical.Name;
        public IPtrType BufferPointerType => canonical.BufferPointerType;
        public IShaderType ElementType => canonical.ElementType;
        public IOperationMethodAttribute GetOperationMethodAttribute() =>
            canonical.GetOperationMethodAttribute();
        public TO EvaluateInstruction<TV, TR, TS, TO>(
            Instruction<TV, TR> instruction, TS semantic)
            where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
            throw new NotSupportedException("Forged buffer operations have no evaluation semantics.");
    }

    private sealed class ForgedWritableStore(IReadWriteStructuredBufferStoreOperation canonical)
        : IReadWriteStructuredBufferStoreOperation
    {
        public FunctionDeclaration Function => canonical.Function;
        public string Name => canonical.Name;
        public IPtrType BufferPointerType => canonical.BufferPointerType;
        public IShaderType ElementType => canonical.ElementType;

        public IOperationMethodAttribute GetOperationMethodAttribute() =>
            canonical.GetOperationMethodAttribute();

        public TO EvaluateInstruction<TV, TR, TS, TO>(
            Instruction<TV, TR> instruction, TS semantic)
            where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
            throw new NotSupportedException("Forged buffer operations have no evaluation semantics.");
    }

    private static MethodInfo Method(string name) =>
        typeof(WritableStructuredBufferTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method {name} was not found.");

    private static RegionFunctionBody Function(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        string name) =>
        Assert.Single(module.FunctionDefinitions, pair => pair.Key.Name == name).Value;

    private static IEnumerable<Instruction<IShaderValue, IShaderValue>> Instructions(RegionFunctionBody body)
    {
        var instructions = new List<Instruction<IShaderValue, IShaderValue>>();
        body.Body.Traverse((_, _, block) =>
        {
            instructions.AddRange(block.Body.Elements);
            return false;
        });
        return instructions;
    }

    private static IEnumerable<SlangStatement> TargetStatements(SlangBlock block)
    {
        foreach (var statement in block.Statements)
        {
            yield return statement;
            IEnumerable<SlangBlock> nested = statement switch
            {
                SlangScope scope => [scope.Body],
                SlangIf conditional => [conditional.WhenTrue, conditional.WhenFalse],
                SlangDoOnce once => [once.Body],
                SlangLoop loop => [loop.Body],
                _ => []
            };
            foreach (var child in nested)
                foreach (var descendant in TargetStatements(child))
                    yield return descendant;
        }
    }

    private static bool AreAdjacent(
        SlangBlock block,
        SlangStatement first,
        SlangStatement second)
    {
        var statements = block.Statements;
        for (var index = 0; index + 1 < statements.Length; index++)
            if (ReferenceEquals(statements[index], first) &&
                ReferenceEquals(statements[index + 1], second))
                return true;
        return statements.Any(statement => statement switch
        {
            SlangScope scope => AreAdjacent(scope.Body, first, second),
            SlangIf conditional =>
                AreAdjacent(conditional.WhenTrue, first, second) ||
                AreAdjacent(conditional.WhenFalse, first, second),
            SlangDoOnce once => AreAdjacent(once.Body, first, second),
            SlangLoop loop => AreAdjacent(loop.Body, first, second),
            _ => false
        });
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static ShaderModuleDeclaration<RegionFunctionBody> OperationModule(
        Instruction<IShaderValue, IShaderValue> instruction,
        ImmutableArray<IDeclaration> declarations = default)
    {
        if (declarations.IsDefault)
            declarations = [];
        var function = new FunctionDeclaration(
            "Entry",
            [],
            new FunctionReturn(UnitType.Instance, []),
            []);
        return VoidModule(function, [instruction], declarations);
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> VoidModule(
        FunctionDeclaration function,
        ImmutableArray<Instruction<IShaderValue, IShaderValue>> instructions,
        ImmutableArray<IDeclaration> declarations = default)
    {
        if (declarations.IsDefault)
            declarations = [];
        var label = DualDrill.CLSL.Language.Symbol.Label.Create("entry");
        var body = RegionFixture.CreateFunctionBody(
            function,
            RegionTree.Block(
                label,
                [],
                RegionFixture.Body(
                    label,
                    [],
                    instructions,
                    Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>()),
                null));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [.. declarations, function],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(function, body));
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> WritableDimensionsCaptureModule()
    {
        var output = new VariableDeclaration(
            StorageAddressSpace.Instance,
            "Output",
            ReadWriteStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance,
            [new GroupAttribute(0), new BindingAttribute(0)]);
        var function = new FunctionDeclaration(
            "Run",
            [],
            new FunctionReturn(UnitType.Instance, []),
            [new ComputeAttribute(), new WorkgroupSizeAttribute(1, 1, 1)]);
        var entry = DualDrill.CLSL.Language.Symbol.Label.Create("entry");
        var store = DualDrill.CLSL.Language.Symbol.Label.Create("store");
        var count = ShaderValue.Intermediate(ShaderType.U32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    ReadWriteStructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance,
                    count,
                    [output.Value],
                    "rw-captured-length")
            ],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(store, [])));
        var storeBody = RegionFixture.Body(
            store,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    ReadWriteStructuredBufferStoreOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance,
                    null,
                    [output.Value, count, ShaderValue.Literal(new F32Literal(1.0f))],
                    "rw-captured-store")
            ],
            Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>());
        var body = RegionFixture.CreateFunctionBody(
            function,
            RegionTree.Block(
                entry,
                [RegionTree.Block(store, [], storeBody, null)],
                entryBody,
                store));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [output, function],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(function, body));
    }

    private static void AssertOperationMismatch(Instruction<IShaderValue, IShaderValue> call)
    {
        var exception = Record.Exception(() =>
            OperationModule(call).RunPass(new FunctionToOperationPass()));
        Assert.NotNull(exception);
        Assert.Equal("OperationFunctionNotMatchException", exception.GetType().Name);
        Assert.Contains("does not match operation", exception.Message);
    }

    private static MethodInfo CreatePrefixStoreMethod<TElement>() where TElement : unmanaged
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"WritableBufferPrefix_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("WritableBufferPrefix");
        var type = module.DefineType(
            "WritableBufferPrefixShader",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        var buffer = typeof(RWStructuredBuffer<>).MakeGenericType(typeof(TElement));
        var field = type.DefineField(
            "Output",
            buffer,
            FieldAttributes.Private | FieldAttributes.Static);
        field.SetCustomAttribute(IntAttribute<GroupAttribute>(0));
        field.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BindingAttribute).GetConstructor([typeof(int), typeof(bool)])!,
            [0, false]));
        var method = type.DefineMethod(
            "StoreWithPrefix",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(TElement),
            [typeof(TElement), typeof(uint), typeof(TElement)]);
        method.DefineParameter(1, ParameterAttributes.None, "prefix");
        method.DefineParameter(2, ParameterAttributes.None, "index");
        method.DefineParameter(3, ParameterAttributes.None, "value");
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldsflda, field);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, buffer.GetProperty("Item")!.SetMethod!);
        il.Emit(OpCodes.Ret);
        var created = type.CreateType()
            ?? throw new InvalidOperationException("Prefix store type was not created.");
        return created.GetMethod("StoreWithPrefix")
            ?? throw new InvalidOperationException("Prefix store method was not created.");
    }

    private static CustomAttributeBuilder IntAttribute<TAttribute>(int value) where TAttribute : Attribute =>
        new(
            typeof(TAttribute).GetConstructor([typeof(int)])
            ?? throw new InvalidOperationException($"{typeof(TAttribute).Name} constructor was not found."),
            [value]);

    private static (Type ShaderType, FieldInfo Field) CreateRawDuplicateGroupWritableShader(Type buffer)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"WritableStorageRawMetadata_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("WritableStorageRawMetadata");
        var type = module.DefineType(
            "RawWritableStorageShader",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        type.AddInterfaceImplementation(typeof(ISharpShader));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var field = type.DefineField(
            "Output",
            buffer,
            FieldAttributes.Private | FieldAttributes.Static);
        field.SetCustomAttribute(IntAttribute<GroupAttribute>(0));
        field.SetCustomAttribute(IntAttribute<GroupAttribute>(0));
        field.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BindingAttribute).GetConstructor([typeof(int), typeof(bool)])!,
            [0, false]));

        var shaderType = type.CreateType()
            ?? throw new InvalidOperationException("Dynamic writable storage shader type was not created.");
        return (
            shaderType,
            shaderType.GetField("Output", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Dynamic writable storage field was not created."));
    }

    private static void WritableParameter(RWStructuredBuffer<float> value)
    {
    }

    private sealed class SignedCopyShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<int> Input;

        [Group(0), Binding(1)]
        private static RWStructuredBuffer<int> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var index = id.x;
            if (index < Input.Length && index < Output.Length)
                Output[index] = Input[index] - 7;
        }
    }

    private sealed class UnsignedCopyShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<uint> Input;

        [Group(0), Binding(1)]
        private static RWStructuredBuffer<uint> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var index = id.x;
            if (index < Input.Length && index < Output.Length)
                Output[index] = Input[index];
        }
    }

    private sealed class HighBitUnsignedStoreShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<uint> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run() => Output[0x80000000u] = 0xffffffffu;
    }

    private sealed class UnsignedInPlaceShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<uint> Values;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var index = id.x;
            if (index < Values.Length)
                Values[index] = Values[index];
        }
    }

    private sealed class SignedInPlaceShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<int> Values;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var index = id.x;
            if (index < Values.Length)
                Values[index] = Values[index] - 7;
        }
    }

    private sealed class SignedConstantShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<int> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run() => Output[0u] = -7;
    }

    private static void UnsignedWritableParameter(RWStructuredBuffer<uint> output)
    {
    }

    private sealed class IntegerWritableCopyShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<int> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
            var copy = Output;
            _ = copy.Length;
        }
    }

    private sealed class IntegerWritableFragmentShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<uint> Output;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class IntegerWritableVertexHintShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0), Vertex]
        private static RWStructuredBuffer<int> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class DoubleValuesShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;

        [Group(0), Binding(1)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(64, 1, 1)]
        public static void Run(
            [Builtin(BuiltinBinding.global_invocation_id)]
            vec3u32 id)
        {
            var i = id.x;
            if (i < Input.Length && i < Output.Length)
                Output[i] = Input[i] * 2.0f;
        }
    }

    private sealed class InPlaceIncrementShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Values;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(64, 1, 1)]
        public static void Run(
            [Builtin(BuiltinBinding.global_invocation_id)]
            vec3u32 id)
        {
            var i = id.x;
            if (i < Values.Length)
                Values[i] = Values[i] + 1.0f;
        }
    }

    private sealed class MixedBindingsShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(2)]
        private static StructuredBuffer<float> Input;

        [Uniform, Group(0), Binding(1)]
        private static float Scale;

        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class WritableFragmentEntryShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class WritableVertexEntryShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Vertex]
        public static int Vertex() => 0;
    }

    private sealed class WritableFragmentHintShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0), Fragment]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class WritableVertexHintShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0), Vertex]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class MissingWritableMetadataShader : ISharpShader
    {
#pragma warning disable CS0169
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class WrongWritableElementShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<double> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class ConflictingWritableUniformShader : ISharpShader
    {
#pragma warning disable CS0169
        [Uniform, Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class ConflictingWritableReadWriteShader : ISharpShader
    {
#pragma warning disable CS0169
        [ReadWrite, Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class WritableLocalShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
            var copy = Output;
            _ = copy.Length;
        }
    }

    private sealed class DuplicateWritableBindingShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> First;

        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Second;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class WritableUniformCollisionShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;

        [Uniform, Group(0), Binding(0)]
        private static float Scale;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class ExplicitComputeWritableShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(2), Binding(3, true), Compute]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0169

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class WritablePropertyShader : ISharpShader
    {
        private static RWStructuredBuffer<float> Output => default;

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class HighBitStoreShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0649

        public static void Store() => Output[0x80000000u] = Value();

        public static float Value() => 7.0f;
    }
}
