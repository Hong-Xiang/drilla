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
public sealed class ReadonlyStructuredBufferTests(ITestOutputHelper output)
{
    [Fact]
    public void PublicWrapperIsAReadonlyShaderOnlyHandle()
    {
        var type = typeof(StructuredBuffer<float>);
        var indexer = type.GetProperty("Item") ??
                      throw new InvalidOperationException("StructuredBuffer indexer was not found.");

        Assert.True(type.IsValueType);
        Assert.True(type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false));
        Assert.Empty(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal(typeof(uint), type.GetProperty(nameof(StructuredBuffer<float>.Length))?.PropertyType);
        Assert.Equal(typeof(float), indexer.PropertyType);
        Assert.Equal(typeof(uint), Assert.Single(indexer.GetIndexParameters()).ParameterType);
        Assert.NotNull(indexer.GetMethod);
        Assert.Null(indexer.SetMethod);
    }

    [Theory]
    [InlineData(typeof(int), typeof(SignedBufferShader), "int32", "i32")]
    [InlineData(typeof(uint), typeof(UnsignedBufferShader), "uint32", "u32")]
    public async Task IntegerBufferCompilesAndReflectsExactScalar(
        Type element, Type shaderType, string scalar, string spelling)
    {
        var wrapper = typeof(StructuredBuffer<>).MakeGenericType(element);
        var indexer = wrapper.GetProperty("Item")!;
        Assert.Equal(element, indexer.PropertyType);
        Assert.Equal(typeof(uint), Assert.Single(indexer.GetIndexParameters()).ParameterType);
        Assert.Null(indexer.SetMethod);
        Assert.Throws<TargetInvocationException>(() =>
            indexer.GetMethod!.Invoke(Activator.CreateInstance(wrapper), [0u]));

        var shader = (ISharpShader)Activator.CreateInstance(shaderType)!;
        var raw = new RuntimeReflectionParser().ParseShaderModule(shader);
        var field = shaderType.GetField("Input", BindingFlags.NonPublic | BindingFlags.Static)!;
        var declaration = Assert.Single(raw.Declarations.OfType<VariableDeclaration>(),
            variable => variable.Name == "Input");
        IShaderType expectedType = element == typeof(int)
            ? ReadOnlyStructuredBufferType<IntType<N32>>.Instance
            : ReadOnlyStructuredBufferType<UIntType<N32>>.Instance;
        Assert.Same(expectedType, declaration.Type);
        Assert.Equal(StorageAddressSpace.Instance, declaration.AddressSpace);
        Assert.Equal($"StructuredBuffer<{spelling}>", declaration.Type.Name);
        var body = Assert.Single(raw.FunctionDefinitions.Values);
        Assert.Contains(body.Code.Instructions,
            instruction => Equals(instruction.Instruction.Operand, indexer.GetMethod));
        Assert.Contains(body.Code.Instructions,
            instruction => Equals(instruction.Instruction.Operand,
                wrapper.GetProperty(nameof(StructuredBuffer<int>.Length))!.GetMethod));
        Assert.Contains(body.Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Ldsflda &&
                           Equals(instruction.Instruction.Operand, field));

        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(shader);
        var operations = Instructions(Function(compiled, "Shade")).ToArray();
        var length = Assert.Single(operations, instruction =>
            instruction.Operation is IReadOnlyStructuredBufferLengthOperation);
        var loads = operations.Where(instruction =>
            instruction.Operation is IReadOnlyStructuredBufferLoadOperation).ToArray();
        Assert.NotEmpty(loads);
        Assert.All(loads, load =>
        {
            var resource = Assert.IsAssignableFrom<IReadOnlyStructuredBufferLoadOperation>(load.Operation);
            Assert.Equal(resource.ElementType, load.Result?.Type);
            Assert.Equal(resource.BufferPointerType, load.Operand0?.Type);
            Assert.Equal(ShaderType.U32, load.Operand1?.Type);
            Assert.NotNull(load.Payload);
            Assert.True(ReadOnlyStructuredBufferFamily.IsCanonicalLoad(load.Operation));
        });
        Assert.Equal(ShaderType.U32, length.Result?.Type);
        Assert.NotNull(length.Payload);
        Assert.True(ReadOnlyStructuredBufferFamily.IsCanonicalLength(length.Operation));
        Assert.True(ReadOnlyStructuredBufferFamily.IsCanonicalType(declaration.Type));

        var binding = Assert.Single(new ShaderModuleReflection().GetStorageBufferBindings(raw));
        Assert.Equal(GPUBufferBindingType.ReadOnlyStorage, binding.Kind);
        Assert.Equal(4u, binding.ElementStride);
        Assert.Equal(4ul, binding.MinimumBindingSize);
        Assert.Equal(GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute,
            binding.Visibility);
        var entry = Assert.Single(new ShaderModuleReflection()
            .GetBindGroupLayoutDescriptor(raw, 0).Entries.ToArray());
        Assert.Equal(4ul, entry.Buffer.MinBindingSize);

        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        var reflectionJson = await new SlangService().ReflectAsync(slang);
        output.WriteLine(slang);
        output.WriteLine(wgsl);
        output.WriteLine(reflectionJson);
        Assert.Contains($"StructuredBuffer<{spelling}>", slang);
        Assert.Contains($"array<{spelling}>", wgsl);
        Assert.Contains("var<storage, read>", wgsl);
        Assert.Contains("arrayLength(", wgsl);
        if (element == typeof(uint))
            Assert.Matches(
                @"u32\(i32\([^\n]*Input_0\[u32\(0\)\]\)\)\s*>\s*u32\(2147483648\)",
                wgsl);
        else
            Assert.Matches(@"Input_0\[u32\(0\)\]\s*<\s*i32\(-3\)", wgsl);
        using var reflection = JsonDocument.Parse(reflectionJson);
        var parameter = Assert.Single(reflection.RootElement.GetProperty("parameters").EnumerateArray());
        var targetType = parameter.GetProperty("type");
        Assert.Equal("structuredBuffer", targetType.GetProperty("baseShape").GetString());
        Assert.Equal(scalar, targetType.GetProperty("resultType").GetProperty("scalarType").GetString());
    }

    [Fact]
    public void UnsignedLoadsUseCanonicalI32ThenUnsignedComparisonsAndReturns()
    {
        var raw = new RuntimeReflectionParser().ParseShaderModule(new UnsignedBufferShader());
        var method = typeof(UnsignedBufferShader).GetMethod("Shade")!;
        Assert.Contains(CompilerTestPipeline.RawBody(raw, method).Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Ldc_I4 &&
                           instruction.Instruction.Operand is int value &&
                           value == int.MinValue);

        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(raw);
        var instructions = Instructions(Function(compiled, "Shade")).ToArray();
        Assert.Contains(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>>);
        Assert.Contains(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>>);
        Assert.Contains(instructions, instruction =>
            instruction.Operation is NumericBinaryRelationalOperation<UIntType<N32>, BinaryRelational.Gt> &&
            instruction.Operand0?.Type == ShaderType.U32 &&
            instruction.Operand1?.Type == ShaderType.U32);
        var returnModule = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Compile(new UnsignedReturnShader());
        var returned = Instructions(Function(returnModule, "Shade")).ToArray();
        Assert.Contains(returned, instruction =>
            instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>>);
        Assert.Contains(returned, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>>);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, false)]
    [InlineData(0x7fffffffu, false)]
    [InlineData(0x80000000u, false)]
    [InlineData(0x80000001u, true)]
    [InlineData(uint.MaxValue, true)]
    public void UnsignedBoundaryOracleIncludesBothSidesOfSignBit(uint value, bool expected)
    {
        Assert.Equal(expected, value > 0x80000000u);
        Assert.Equal(value, unchecked((uint)(int)value));
    }

    [Fact]
    public void UnsupportedGenericInstancesFailBeforeStaticHolderInitialization()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Throws<NotSupportedException>(() =>
                ReadOnlyStructuredBufferType<FloatType<N64>>.Instance);
            Assert.Throws<NotSupportedException>(() =>
                StructuredBufferLengthOperation<FloatType<N64>>.Instance);
            Assert.Throws<NotSupportedException>(() =>
                StructuredBufferLoadOperation<FloatType<N64>>.Instance);
        }
        Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseType(typeof(StructuredBuffer<double>)));
        Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseType(typeof(RWStructuredBuffer<double>)));
        Assert.NotNull(StructuredBufferLoadOperation<FloatType<N32>>.Instance);
    }

    [Fact]
    public void ForgedReadOnlyOperationCannotClaimCanonicalInterface()
    {
        var canonical = StructuredBufferLoadOperation<UIntType<N32>>.Instance;
        var fake = new ForgedBufferLoad(canonical);
        var call = Instruction<IShaderValue, IShaderValue>.Create(
            fake,
            ShaderValue.Intermediate(ShaderType.U32),
            [ShaderValue.Intermediate(fake.BufferPointerType), ShaderValue.Intermediate(ShaderType.U32)]);
        var error = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(OperationModule(call)));
        Assert.Contains("invalid read-only storage-buffer load", error.Message);
    }

    [Fact]
    public void IntegerOperationsRejectMalformedDirectIrAndProxyCalls()
    {
        foreach (var operation in new IReadOnlyStructuredBufferLoadOperation[]
                 {
                     StructuredBufferLoadOperation<IntType<N32>>.Instance,
                     StructuredBufferLoadOperation<UIntType<N32>>.Instance
                 })
        {
            var receiver = ShaderValue.Intermediate(operation.BufferPointerType);
            var index = ShaderValue.Intermediate(ShaderType.U32);
            var result = ShaderValue.Intermediate(operation.ElementType);
            var valid = Instruction<IShaderValue, IShaderValue>.Create(
                operation, result, [receiver, index]);
            var wrongAddress = ShaderValue.Intermediate(
                operation.BufferPointerType.BaseType.GetPtrType(GenericAddressSpace.Instance));
            var malformed = new[]
            {
                valid with { Operand0 = wrongAddress },
                valid with { Operand1 = ShaderValue.Intermediate(ShaderType.I32) },
                valid with { Result = ShaderValue.Intermediate(ShaderType.F32) },
                Instruction<IShaderValue, IShaderValue>.Create(operation, result, [receiver])
            };
            Assert.All(malformed, instruction =>
                Assert.Throws<NotSupportedException>(() =>
                    new SlangTargetLowering().Lower(OperationModule(instruction))));

            var proxy = TaggedProxy(
                operation,
                [new ParameterDeclaration("buffer", operation.BufferPointerType, [])],
                operation.ElementType);
            var call = ResourceCall(
                proxy,
                new FunctionType([operation.BufferPointerType], operation.ElementType),
                result,
                [receiver, index]);
            var error = Record.Exception(() =>
                OperationModule(call).RunPass(new FunctionToOperationPass()));
            Assert.NotNull(error);
            Assert.Equal("OperationFunctionNotMatchException", error.GetType().Name);
        }

        foreach (var operation in new IReadOnlyStructuredBufferLengthOperation[]
                 {
                     StructuredBufferLengthOperation<IntType<N32>>.Instance,
                     StructuredBufferLengthOperation<UIntType<N32>>.Instance
                 })
        {
            var receiver = ShaderValue.Intermediate(operation.BufferPointerType);
            var count = ShaderValue.Intermediate(ShaderType.U32);
            var valid = Instruction<IShaderValue, IShaderValue>.Create(
                operation, count, [receiver]);
            var malformed = new[]
            {
                valid with { Result = ShaderValue.Intermediate(ShaderType.I32) },
                valid with
                {
                    Operand0 = ShaderValue.Intermediate(
                        operation.BufferPointerType.BaseType.GetPtrType(GenericAddressSpace.Instance))
                },
                Instruction<IShaderValue, IShaderValue>.Create(
                    operation, count, [receiver, ShaderValue.Intermediate(ShaderType.U32)])
            };
            Assert.All(malformed, instruction =>
                Assert.Throws<NotSupportedException>(() =>
                    new SlangTargetLowering().Lower(OperationModule(instruction))));
        }
    }

    [Theory]
    [InlineData(typeof(StructuredBuffer<int>))]
    [InlineData(typeof(StructuredBuffer<uint>))]
    public void DuplicateRawMetadataRejectsIntegerBuffers(Type bufferType)
    {
        var (shaderType, _) = CreateRawDuplicateGroupBufferShader(bufferType);
        var shader = (ISharpShader)Activator.CreateInstance(shaderType)!;
        var error = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(shader));
        Assert.Contains("found 0, 2, and 1", error.Message);
    }

    [Fact]
    public void IntegerResourcePlacementAndWholeValueCopiesReject()
    {
        Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(Method(nameof(IntegerResourceParameter))));
        Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new IntegerBufferCopyShader()));
        var pointer = ReadOnlyStructuredBufferType<UIntType<N32>>.Instance
            .GetPtrType(StorageAddressSpace.Instance);
        var resource = ShaderValue.Intermediate(pointer.BaseType);
        var load = Instruction<IShaderValue, IShaderValue>.Create(
            new LoadOperation(), resource, [ShaderValue.Intermediate(pointer)]);
        Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(OperationModule(load)));
    }

    [Fact]
    public void IntegerLengthAndLoadCannotProveCooperationThroughArithmeticOrHelper()
    {
        foreach (var shader in new ISharpShader[]
                 {
                     new VaryingIntegerLoadShader(),
                     new VaryingIntegerLengthShader()
                 })
        {
            var error = Assert.Throws<NotSupportedException>(() =>
                new CLSLCompiler(new(
                    CLSLCompileTarget.WGSL,
                    CLSLCooperationProfile.PortableWgsl)).Emit(shader));
            Assert.Contains("varying", error.Message);
        }
    }

    [Fact]
    public void ExactClosedTypeAndAccessorsAreRegistered()
    {
        var parser = new RuntimeReflectionParser();
        var type = Assert.IsType<ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>>(
            parser.ParseType(typeof(StructuredBuffer<float>)));
        Assert.Equal(ShaderType.F32, type.ElementType);
        Assert.Equal(4u, type.ElementStride);

        var raw = parser.ParseShaderModule(new ReadOnlyBufferShader());
        var input = Assert.Single(
            raw.Declarations.OfType<VariableDeclaration>(),
            declaration => declaration.Name == "Input");
        Assert.Same(ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance, input.Type);
        Assert.Same(StorageAddressSpace.Instance, input.AddressSpace);

        var methods = CompilerTestPipeline.RawBody(
                raw,
                typeof(ReadOnlyBufferShader).GetMethod(nameof(ReadOnlyBufferShader.Shade))!)
            .Code.Instructions
            .Where(instruction => instruction.Instruction.Operand is MethodInfo)
            .Select(instruction => (MethodInfo)instruction.Instruction.Operand)
            .ToArray();
        Assert.Contains(
            typeof(StructuredBuffer<float>).GetProperty(nameof(StructuredBuffer<float>.Length))!.GetMethod!,
            methods);
        Assert.Contains(typeof(StructuredBuffer<float>).GetProperty("Item")!.GetMethod!, methods);
    }

    [Fact]
    public void ActualCilUsesAddressLoadsAndDirectAccessorCalls()
    {
        var method = typeof(ReadOnlyBufferShader).GetMethod(nameof(ReadOnlyBufferShader.Shade))
            ?? throw new InvalidOperationException("Shader entry was not found.");
        var instructions = CilMethodDecoder.Decode(method).Instructions;

        Assert.Contains(instructions, instruction => instruction.Instruction.OpCode == OpCodes.Ldsflda);
        Assert.DoesNotContain(instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldsfld &&
            instruction.Instruction.Operand is FieldInfo field &&
            field.FieldType == typeof(StructuredBuffer<float>));
        Assert.DoesNotContain(instructions, instruction => instruction.Instruction.OpCode == OpCodes.Callvirt);
        Assert.DoesNotContain(instructions, instruction =>
            instruction.Instruction.OpCode.Name?.StartsWith("ldelem", StringComparison.Ordinal) is true);
        Assert.Contains(instructions, instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: "get_Length" });
        Assert.Contains(instructions, instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: "get_Item" });
    }

    [Fact]
    public void PublicCompilerEmitsTypedReadOnlyStoragePath()
    {
        var shader = new ReadOnlyBufferShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.IR));
        var compiled = compiler.Compile(shader);
        var instructions = Instructions(Function(compiled, nameof(ReadOnlyBufferShader.Shade))).ToArray();

        var length = Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Equal(ShaderType.U32, length.Result?.Type);
        Assert.Equal(
            StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance.BufferPointerType,
            length.Operand0?.Type);

        var load = Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Equal(ShaderType.F32, load.Result?.Type);
        Assert.Equal(ShaderType.U32, load.Operand1?.Type);

        var ir = new CLSLCompiler(new(CLSLCompileTarget.IR)).Emit(shader);
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        output.WriteLine(slang);
        output.WriteLine(wgsl);

        Assert.Contains("structured-buffer-length-f32", ir);
        Assert.Contains("structured-buffer-load-f32", ir);
        Assert.Contains("StructuredBuffer<f32>", slang);
        Assert.Contains(".GetDimensions(", slang);
        Assert.Matches(@"v_\d+_Input\[v_\d+\]", slang);
        Assert.Contains("@group(0)", wgsl);
        Assert.Contains("@binding(0)", wgsl);
        Assert.Contains("var<storage, read>", wgsl);
        Assert.Contains("arrayLength(", wgsl);
        Assert.Contains("Input_0[", wgsl);
    }

    [Fact]
    public async Task TargetAndTypedReflectionDescribeReadOnlyStorage()
    {
        var shader = new ReadOnlyBufferShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var module = compiler.Parse(shader);
        var reflection = new ShaderModuleReflection();
        var storage = Assert.Single(reflection.GetStorageBufferBindings(module));

        Assert.Equal("Input", storage.Name);
        Assert.Equal(0, storage.Group);
        Assert.Equal(0, storage.Binding);
        Assert.Equal(
            GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute,
            storage.Visibility);
        Assert.False(storage.HasDynamicOffset);
        Assert.Equal(GPUBufferBindingType.ReadOnlyStorage, storage.Kind);
        Assert.Equal(4u, storage.ElementStride);
        Assert.Equal(4ul, storage.MinimumBindingSize);

        var entries = reflection.GetBindGroupLayoutDescriptor(module, 0).Entries.ToArray();
        Assert.Collection(
            entries,
            input =>
            {
                Assert.Equal(0, input.Binding);
                Assert.Equal(GPUBufferBindingType.ReadOnlyStorage, input.Buffer.Type);
                Assert.Equal(4ul, input.Buffer.MinBindingSize);
            },
            scale =>
            {
                Assert.Equal(1, scale.Binding);
                Assert.Equal(GPUBufferBindingType.Uniform, scale.Buffer.Type);
                Assert.Equal(4ul, scale.Buffer.MinBindingSize);
            });
        Assert.Equal(
            entries.Select(entry => (entry.Binding, entry.Visibility, entry.Buffer)),
            reflection.GetBindGroupLayoutDescriptorBuffer(module, 0).Entries.ToArray()
                .Select(entry => (entry.Binding, entry.Visibility, entry.Buffer)));

        var targetText = await new SlangService().ReflectAsync(compiler.Emit(shader));
        output.WriteLine(targetText);
        using var target = JsonDocument.Parse(targetText);
        var parameters = target.RootElement.GetProperty("parameters").EnumerateArray().ToArray();
        var targetInput = Assert.Single(parameters, parameter =>
            parameter.GetProperty("binding").GetProperty("index").GetInt32() == 0);
        Assert.EndsWith("Input", targetInput.GetProperty("name").GetString());
        var targetType = targetInput.GetProperty("type");
        Assert.Equal("resource", targetType.GetProperty("kind").GetString());
        Assert.Equal("structuredBuffer", targetType.GetProperty("baseShape").GetString());
        Assert.Equal(
            "float32",
            targetType.GetProperty("resultType").GetProperty("scalarType").GetString());
    }

    [Fact]
    public void BufferOperationsHavePreciseEffectsAndOrigins()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(new ReadOnlyBufferShader())
            .RunPass(new FunctionToOperationPass());
        var function = Function(module, nameof(ReadOnlyBufferShader.Shade));
        var instructions = Instructions(function).ToArray();
        var length = Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var load = Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);

        var summary = FunctionEffectAnalysis.Analyze(module)[function.Declaration];

        Assert.True(summary.IsComplete);
        Assert.DoesNotContain(summary.UnknownSites, site =>
            site.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>> or StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.DoesNotContain(summary.RequirementSites, site =>
            site.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var site = Assert.Single(summary.RequirementSites, site =>
            site.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Equal(OperationRequirement.MemoryRead, site.Requirements);
        Assert.Same(load.Payload, site.Payload);
        Assert.NotNull(site.Payload);
        Assert.NotNull(length.Payload);
    }

    [Fact]
    public void IsolatedLengthIsCompleteAndRequirementFree()
    {
        var method = typeof(LengthOnlyShader).GetMethod(nameof(LengthOnlyShader.GetLength))
            ?? throw new InvalidOperationException("Length helper was not found.");
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Compile(new RuntimeReflectionParser().ParseMethod(method))
            .RunPass(new FunctionToOperationPass());
        var function = Function(module, nameof(LengthOnlyShader.GetLength));

        var summary = FunctionEffectAnalysis.Analyze(module)[function.Declaration];

        Assert.True(summary.IsComplete);
        Assert.Equal(OperationRequirement.None, summary.Requirements);
        Assert.Empty(summary.RequirementSites);
        Assert.Empty(summary.UnknownSites);
        Assert.Single(Instructions(function), instruction =>
            instruction.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
    }

    [Fact]
    public void HighBitIndexIsExplicitlyConvertedToU32()
    {
        var method = typeof(HighBitIndexShader).GetMethod(nameof(HighBitIndexShader.Read))
            ?? throw new InvalidOperationException("High-bit helper was not found.");
        var raw = new RuntimeReflectionParser().ParseMethod(method);
        Assert.Contains(
            CompilerTestPipeline.RawBody(raw, method).Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Ldc_I4 &&
                           instruction.Instruction.Operand is int value &&
                           value == int.MinValue);

        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(raw);
        var instructions = Instructions(Function(compiled, nameof(HighBitIndexShader.Read))).ToArray();
        Assert.Contains(instructions, instruction =>
            instruction.Operation is ScalarConversionOperation<IntType<DualDrill.Common.Nat.N32>,
                UIntType<DualDrill.Common.Nat.N32>>);
        var load = Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Equal(ShaderType.U32, load.Operand1?.Type);
    }

    [Fact]
    public void RepeatedAndCrossBlockLengthQueriesLower()
    {
        var repeated = new CLSLCompiler(new(CLSLCompileTarget.SLang))
            .Emit(new RepeatedLengthShader());
        var crossBlock = new CLSLCompiler(new(CLSLCompileTarget.SLang))
            .Emit(new CrossBlockLengthShader());

        Assert.Equal(2, Count(repeated, ".GetDimensions("));
        Assert.Equal(1, Count(crossBlock, ".GetDimensions("));
        Assert.Contains("if(", crossBlock);
    }

    [Fact]
    public void LengthTargetStatementDefinesOriginalCountAndWritableStride()
    {
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Compile(new ReadOnlyBufferShader())
            .RunPass(new FunctionToOperationPass());
        var source = Function(compiled, nameof(ReadOnlyBufferShader.Shade));
        var length = Assert.Single(Instructions(source), instruction =>
            instruction.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var target = new SlangTargetLowering().Lower(compiled);
        var targetBody = Assert.Single(target.FunctionDefinitions, pair =>
            pair.Key.Name == nameof(ReadOnlyBufferShader.Shade)).Value;
        var dimensions = Assert.Single(TargetStatements(targetBody.Body).OfType<SlangGetDimensions>());
        var origin = Assert.Single(targetBody.Origins.Dimensions);

        Assert.Same(length.Result, dimensions.Count);
        Assert.Equal(length, origin.Source);
        Assert.Same(dimensions, origin.Dimensions);
        Assert.Equal(ShaderType.U32, dimensions.Count.Type);
        Assert.Equal(ShaderType.U32, dimensions.Stride.Type);
        Assert.IsType<IntermediateValue>(dimensions.Stride);
        Assert.NotSame(dimensions.Count, dimensions.Stride);
        Assert.Equal(ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance, dimensions.Buffer.Type);
    }

    [Fact]
    public void ResourceDiscoveryDoesNotRunStaticInitializer()
    {
        BufferInitializerProbe.Executions = 0;

        var module = new RuntimeReflectionParser().ParseShaderModule(default(InitializedBufferShader));

        Assert.Equal(0, BufferInitializerProbe.Executions);
        Assert.Contains(
            module.Declarations.OfType<VariableDeclaration>(),
            declaration => declaration.Name == "Input");
    }

    [Fact]
    public void ArithmeticOracleIsSourceLevelOnly()
    {
        float[] input = [1.0f, 3.0f, 5.0f];
        const float scale = 2.0f;

        Assert.Equal(6.0f, input[1] * scale);
    }

    [Theory]
    [InlineData(typeof(MissingBufferMetadataShader), "exactly one [Group] and [Binding]")]
    [InlineData(typeof(WrongElementShader), "only StructuredBuffer<float>, StructuredBuffer<int>, and StructuredBuffer<uint>")]
    [InlineData(typeof(UnannotatedWrongElementShader), "only StructuredBuffer<float>, StructuredBuffer<int>, and StructuredBuffer<uint>")]
    [InlineData(typeof(ConflictingUniformShader), "requires no address-space attribute")]
    [InlineData(typeof(ConflictingReadShader), "attribute(s) [Read] are not supported")]
    [InlineData(typeof(InstanceBufferFieldShader), "must be static shader-module fields")]
    [InlineData(typeof(BufferPropertyShader), "must be static shader-module fields")]
    [InlineData(typeof(BufferAutoPropertyShader), "compiler-generated backing field")]
    [InlineData(typeof(ReferencedBufferPropertyShader), "ResourcePropertyHolder.Input")]
    [InlineData(typeof(NestedBufferShader), "structure member field")]
    [InlineData(typeof(BufferLocalShader), "local variable")]
    [InlineData(typeof(BufferArrayShader), "must be declared directly")]
    public void InvalidResourceDeclarationsAreRejected(Type shaderType, string expected)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader));

        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void ResourceUserSignaturesAreRejected()
    {
        foreach (var method in new[]
                 {
                     Method(nameof(ResourceParameter)),
                     Method(nameof(ResourceRefParameter)),
                     Method(nameof(ResourceInParameter)),
                     Method(nameof(ResourceOutParameter)),
                     Method(nameof(ResourceReturn))
                 })
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                new RuntimeReflectionParser().ParseMethod(method));
            Assert.Contains("structured buffers are valid only as static shader-module fields", exception.Message);
        }
    }

    [Fact]
    public void ReadonlySurfaceHasNoStoreAndRuntimeStubsThrow()
    {
        var type = typeof(StructuredBuffer<float>);
        Assert.Null(type.GetProperty("Item")?.SetMethod);
        Assert.Throws<NotSupportedException>(() => default(StructuredBuffer<float>).Length);
        Assert.Throws<NotSupportedException>(() => default(StructuredBuffer<float>)[0]);
    }

    [Fact]
    public void VisibilityAndDynamicOffsetArePreserved()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new VisibleDynamicBufferShader());
        var binding = Assert.Single(new ShaderModuleReflection().GetStorageBufferBindings(module));

        Assert.Equal(GPUShaderStage.Vertex | GPUShaderStage.Fragment, binding.Visibility);
        Assert.True(binding.HasDynamicOffset);
        Assert.Empty(new ShaderModuleReflection().GetBindGroupLayoutDescriptor(module, 1).Entries.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShaderModuleReflection().GetBindGroupLayoutDescriptor(module, -1));
    }

    [Fact]
    public void DuplicateStorageAndCrossKindBindingsAreRejected()
    {
        var duplicate = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new DuplicateBufferBindingShader()));
        Assert.Contains("resource binding (0, 0) is duplicated", duplicate.Message);

        var crossKind = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new CrossKindBindingShader()));
        Assert.Contains("resource binding (0, 0) is duplicated", crossKind.Message);
    }

    [Fact]
    public void RawDuplicateGroupMetadataOnStorageBufferIsRejectedBeforeSetConversion()
    {
        var (shaderType, field) = CreateRawDuplicateGroupBufferShader();
        Assert.Equal(2, field.GetCustomAttributes<GroupAttribute>(inherit: false).Count());
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(shader));

        Assert.Equal(
            $"Shader module metadata validation rejected field '{shaderType.FullName}.Input': " +
            "a read-only storage buffer requires no address-space attribute and exactly one [Group] and " +
            "[Binding] attribute (found 0, 2, and 1).",
            exception.Message);
    }

    [Fact]
    public void ExactMethodRegistrationDoesNotMatchImpostors()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(new AccessorImpostorShader());
        var instructions = Instructions(Function(module, nameof(AccessorImpostorShader.Shade))).ToArray();

        Assert.DoesNotContain(instructions, instruction =>
            instruction.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>> or StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Contains(instructions, instruction => instruction.Operation is CallOperation);

        var intrinsicImpostor = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(Method(nameof(ResourceIntrinsicImpostor))));
        Assert.Contains("parameter of mapped intrinsic", intrinsicImpostor.Message);
        Assert.Contains("structured buffers are valid only as static shader-module fields", intrinsicImpostor.Message);
    }

    [Fact]
    public void FunctionCallNormalizationPreservesPayloadAndRequiresExactStoragePointer()
    {
        var operation = StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var payload = new object();
        var receiver = ShaderValue.Intermediate(operation.BufferPointerType);
        var index = ShaderValue.Intermediate(ShaderType.U32);
        var result = ShaderValue.Intermediate(ShaderType.F32);
        var call = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation(new FunctionType([operation.BufferPointerType, ShaderType.U32], ShaderType.F32)),
            result,
            [operation.Function, receiver, index],
            payload);

        var normalized = OperationModule(call).RunPass(new FunctionToOperationPass());
        var replacement = Assert.Single(Instructions(Function(normalized, "Entry")));
        Assert.IsType<StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>>(replacement.Operation);
        Assert.Same(payload, replacement.Payload);

        var wrongReceiver = ShaderValue.Intermediate(
            ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(GenericAddressSpace.Instance));
        var malformed = call with { Operand1 = wrongReceiver };
        var exception = Record.Exception(() =>
            OperationModule(malformed).RunPass(new FunctionToOperationPass()));
        Assert.NotNull(exception);
        Assert.Equal("OperationFunctionNotMatchException", exception.GetType().Name);
        Assert.Contains("does not match operation", exception.Message);
    }

    [Fact]
    public void TargetLoweringRejectsMalformedAndWholeHandleOperations()
    {
        var operation = StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var storageReceiver = ShaderValue.Intermediate(operation.BufferPointerType);
        var genericReceiver = ShaderValue.Intermediate(
            ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(GenericAddressSpace.Instance));
        var f32 = ShaderValue.Intermediate(ShaderType.F32);
        var u32 = ShaderValue.Intermediate(ShaderType.U32);
        var i32 = ShaderValue.Intermediate(ShaderType.I32);
        var resource = ShaderValue.Intermediate(ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance);
        var indirectResource = ShaderValue.Intermediate(
            operation.BufferPointerType.GetPtrType(FunctionAddressSpace.Instance));

        var malformed = new[]
        {
            Instruction<IShaderValue, IShaderValue>.Create(
                StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance,
                u32,
                [genericReceiver]),
            Instruction<IShaderValue, IShaderValue>.Create(
                StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance,
                f32,
                [storageReceiver]),
            Instruction<IShaderValue, IShaderValue>.Create(operation, f32, [genericReceiver, u32]),
            Instruction<IShaderValue, IShaderValue>.Create(operation, f32, [storageReceiver, i32]),
            Instruction<IShaderValue, IShaderValue>.Create(operation, u32, [storageReceiver, u32]),
            Instruction<IShaderValue, IShaderValue>.Create(
                new LoadOperation(),
                resource,
                [storageReceiver]),
            Instruction<IShaderValue, IShaderValue>.Create(
                new StoreOperation(),
                null,
                [storageReceiver, resource]),
            Instruction<IShaderValue, IShaderValue>.Create(
                new LoadOperation(),
                storageReceiver,
                [indirectResource])
        };

        Assert.All(malformed, instruction =>
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                new SlangTargetLowering().Lower(OperationModule(instruction)));
            Assert.Contains("structured-buffer", exception.Message);
        });
    }

    [Fact]
    public void DirectIrStorageDeclarationsRequireBindingMetadataAndStorageAddress()
    {
        var missing = new VariableDeclaration(
            StorageAddressSpace.Instance,
            "Input",
            ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance,
            []);
        var missingModule = new ShaderModuleDeclaration<RegionFunctionBody>(
            [missing],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);
        var missingException = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(missingModule));
        Assert.Contains("exactly one [Group] and [Binding]", missingException.Message);
        Assert.Throws<NotSupportedException>(() =>
            new ShaderModuleReflection().GetBindGroupLayoutDescriptor(missingModule, 9));

        var wrongAddress = new VariableDeclaration(
            GenericAddressSpace.Instance,
            "Input",
            ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance,
            [new GroupAttribute(0), new BindingAttribute(0)]);
        var wrongAddressModule = new ShaderModuleDeclaration<RegionFunctionBody>(
            [wrongAddress],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);
        var addressException = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(wrongAddressModule));
        Assert.Contains("does not match attribute address space Storage", addressException.Message);
    }

    [Fact]
    public void DirectIrRejectsIndirectResourceSignatureAndLocalTypes()
    {
        var resourcePointer = ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(StorageAddressSpace.Instance);
        var indirect = resourcePointer.GetPtrType(FunctionAddressSpace.Instance);
        var doublyIndirect = indirect.GetPtrType(FunctionAddressSpace.Instance);

        var parameterModule = SignatureModule(
            [new ParameterDeclaration("value", indirect, [])],
            UnitType.Instance);
        Assert.Throws<NotSupportedException>(() =>
            new ShaderModuleReflection().GetStorageBufferBindings(parameterModule));
        Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(parameterModule));

        var returnModule = SignatureModule([], doublyIndirect);
        Assert.Throws<NotSupportedException>(() =>
            new ShaderModuleReflection().GetStorageBufferBindings(returnModule));
        Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(returnModule));

        var localModule = SignatureModule(
            [],
            UnitType.Instance,
            [new VariableDeclaration(FunctionAddressSpace.Instance, "copy", doublyIndirect, [])]);
        Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(localModule));

        var structure = new StructureDeclaration
        {
            Name = "Payload",
            Attributes = [],
            Members = [new MemberDeclaration("Buffer", doublyIndirect, [])]
        };
        var memberModule = new ShaderModuleDeclaration<RegionFunctionBody>(
            [structure],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);
        Assert.Throws<NotSupportedException>(() =>
            new ShaderModuleReflection().GetStorageBufferBindings(memberModule));
    }

    [Fact]
    public void DirectIrPreservesOrdinaryIndirectPointerTypes()
    {
        var ordinary = ShaderType.F32.GetPtrType(StorageAddressSpace.Instance)
            .GetPtrType(FunctionAddressSpace.Instance);
        var module = SignatureModule(
            [new ParameterDeclaration("value", ordinary, [])],
            UnitType.Instance);

        Assert.Empty(new ShaderModuleReflection().GetStorageBufferBindings(module));
        Assert.NotNull(new SlangTargetLowering().Lower(module));
    }

    [Fact]
    public unsafe void ClrFunctionPointersContainingResourcesAreRejected()
    {
        foreach (var method in new[]
                 {
                     Method(nameof(ResourceFunctionPointerParameter)),
                     Method(nameof(ResourceFunctionPointerReturn))
                 })
        {
            Assert.True(method.GetParameters().Single().ParameterType.IsFunctionPointer);
            var exception = Assert.Throws<NotSupportedException>(() =>
                new RuntimeReflectionParser().ParseMethod(method));
            Assert.Contains("structured buffers are valid only as static shader-module fields", exception.Message);
        }

        var fieldException = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(new HiddenFunctionPointerShader()));
        Assert.Contains("Hidden", fieldException.Message);
        Assert.Contains("structured buffers must be declared directly", fieldException.Message);
    }

    [Fact]
    public unsafe void NonResourceFunctionPointerBehaviorIsPreserved()
    {
        var method = Method(nameof(OrdinaryFunctionPointerParameter));
        Assert.True(method.GetParameters().Single().ParameterType.IsFunctionPointer);

        Assert.NotNull(new RuntimeReflectionParser().ParseMethod(method));
    }

    [Fact]
    public void ResourceNormalizerRejectsProxyDeclarationsAndWrongCallTypes()
    {
        var length = StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var load = StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance;
        var receiver = ShaderValue.Intermediate(length.BufferPointerType);
        var index = ShaderValue.Intermediate(ShaderType.U32);
        var u32 = ShaderValue.Intermediate(ShaderType.U32);
        var f32 = ShaderValue.Intermediate(ShaderType.F32);
        var malformed = new[]
        {
            ResourceCall(
                TaggedProxy(length, [], ShaderType.U32),
                new FunctionType([], ShaderType.U32),
                u32,
                [receiver]),
            ResourceCall(
                TaggedProxy(
                    length,
                    [new ParameterDeclaration("buffer", length.BufferPointerType, [])],
                    ShaderType.F32),
                new FunctionType([length.BufferPointerType], ShaderType.F32),
                u32,
                [receiver]),
            ResourceCall(
                length.Function,
                new FunctionType([], ShaderType.U32),
                u32,
                [receiver]),
            ResourceCall(
                TaggedProxy(
                    load,
                    [new ParameterDeclaration("buffer", load.BufferPointerType, [])],
                    ShaderType.F32),
                new FunctionType([load.BufferPointerType], ShaderType.F32),
                f32,
                [receiver, index]),
            ResourceCall(
                TaggedProxy(
                    load,
                    [
                        new ParameterDeclaration("buffer", load.BufferPointerType, []),
                        new ParameterDeclaration("index", ShaderType.U32, [])
                    ],
                    ShaderType.U32),
                new FunctionType([load.BufferPointerType, ShaderType.U32], ShaderType.U32),
                f32,
                [receiver, index]),
            ResourceCall(
                load.Function,
                new FunctionType([load.BufferPointerType], ShaderType.F32),
                f32,
                [receiver, index])
        };

        Assert.All(malformed, call =>
        {
            var exception = Record.Exception(() =>
                OperationModule(call).RunPass(new FunctionToOperationPass()));
            Assert.NotNull(exception);
            Assert.Equal("OperationFunctionNotMatchException", exception.GetType().Name);
        });
    }

    private sealed class ForgedBufferLoad(IReadOnlyStructuredBufferLoadOperation canonical)
        : IReadOnlyStructuredBufferLoadOperation
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

    private sealed class SignedBufferShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<int> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static int Shade() => Input.Length > 0u && Input[0u] < -3 ? Input[0u] : -1;
    }

    private sealed class UnsignedBufferShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<uint> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static uint Shade() =>
            Input.Length > 0u && Input[0u] > 0x80000000u ? Input[0u] : 0u;
    }

    private sealed class UnsignedReturnShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<uint> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static uint Shade() => Input[0u];
    }

    private static uint IntegerResourceParameter(StructuredBuffer<uint> buffer) =>
        buffer.Length;

    private sealed class IntegerBufferCopyShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<uint> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static uint Shade()
        {
            var copy = Input;
            return copy.Length;
        }
    }

    private sealed class VaryingIntegerLoadShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<int> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Shade() =>
            Input[0u] + 1 < 0 ? DMath.dpdx(1.0f) : 0.0f;
    }

    private sealed class VaryingIntegerLengthShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<uint> Input;
#pragma warning restore CS0649

        private static bool IsLong(uint length) => length + 1u > 2u;

        [Fragment]
        [return: Location(0)]
        public static float Shade() =>
            IsLong(Input.Length) ? DMath.dpdx(1.0f) : 0.0f;
    }

    private static MethodInfo Method(string name) =>
        typeof(ReadonlyStructuredBufferTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
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
                SlangIf conditional => new[] { conditional.WhenTrue, conditional.WhenFalse },
                SlangDoOnce once => [once.Body],
                SlangLoop loop => [loop.Body],
                _ => []
            };
            foreach (var child in nested)
                foreach (var descendant in TargetStatements(child))
                    yield return descendant;
        }
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static (Type ShaderType, FieldInfo Field) CreateRawDuplicateGroupBufferShader(
        Type? bufferType = null)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ReadOnlyStorageRawMetadata_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("ReadOnlyStorageRawMetadata");
        var type = module.DefineType(
            "RawReadOnlyStorageShader",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        type.AddInterfaceImplementation(typeof(ISharpShader));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var field = type.DefineField(
            "Input",
            bufferType ?? typeof(StructuredBuffer<float>),
            FieldAttributes.Private | FieldAttributes.Static);
        var groupConstructor = typeof(GroupAttribute).GetConstructor([typeof(int)])
            ?? throw new InvalidOperationException("GroupAttribute constructor was not found.");
        var bindingConstructor = typeof(BindingAttribute).GetConstructor([typeof(int), typeof(bool)])
            ?? throw new InvalidOperationException("BindingAttribute constructor was not found.");
        field.SetCustomAttribute(new CustomAttributeBuilder(groupConstructor, [0]));
        field.SetCustomAttribute(new CustomAttributeBuilder(groupConstructor, [0]));
        field.SetCustomAttribute(new CustomAttributeBuilder(bindingConstructor, [0, false]));

        var shaderType = type.CreateType()
            ?? throw new InvalidOperationException("Dynamic storage shader type was not created.");
        return (
            shaderType,
            shaderType.GetField("Input", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Dynamic storage field was not created."));
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> OperationModule(
        Instruction<IShaderValue, IShaderValue> instruction)
    {
        var function = new FunctionDeclaration(
            "Entry",
            [],
            new FunctionReturn(UnitType.Instance, []),
            []);
        var label = DualDrill.CLSL.Language.Symbol.Label.Create("entry");
        var body = RegionFixture.CreateFunctionBody(
            function,
            RegionTree.Block(
                label,
                [],
                RegionFixture.Body(
                    label,
                    [],
                    [instruction],
                    Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>()),
                null));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [function],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(function, body));
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> SignatureModule(
        ImmutableArray<ParameterDeclaration> parameters,
        IShaderType returnType,
        ImmutableArray<VariableDeclaration> locals = default)
    {
        if (locals.IsDefault)
            locals = [];
        var function = new FunctionDeclaration(
            "Entry",
            parameters,
            new FunctionReturn(returnType, []),
            []);
        var label = DualDrill.CLSL.Language.Symbol.Label.Create("entry");
        var terminator = returnType is UnitType
            ? Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>()
            : Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(
                ShaderValue.Intermediate(returnType));
        var instructions = locals.Select(local =>
            Instruction<IShaderValue, IShaderValue>.Create(
                new LoadOperation(),
                ShaderValue.Intermediate(local.Type),
                [local.Value]));
        var body = RegionFixture.CreateFunctionBody(
            function,
            RegionTree.Block(
                label,
                [],
                RegionFixture.Body(label, [], instructions, terminator),
                null));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [function],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(function, body));
    }

    private static FunctionDeclaration TaggedProxy(
        IOperation operation,
        ImmutableArray<ParameterDeclaration> parameters,
        IShaderType returnType) =>
        new(
            "BadProxy",
            parameters,
            new FunctionReturn(returnType, []),
            [operation.GetOperationMethodAttribute()]);

    private static Instruction<IShaderValue, IShaderValue> ResourceCall(
        FunctionDeclaration function,
        FunctionType callType,
        IShaderValue result,
        ImmutableArray<IShaderValue> arguments) =>
        Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation(callType),
            result,
            [function, .. arguments],
            "resource-call-origin");

    private static uint ResourceParameter(StructuredBuffer<float> buffer) => buffer.Length;
    private static uint ResourceRefParameter(ref StructuredBuffer<float> buffer) => buffer.Length;
    private static uint ResourceInParameter(in StructuredBuffer<float> buffer) => buffer.Length;

    private static void ResourceOutParameter(out StructuredBuffer<float> buffer) =>
        buffer = default;

    private static StructuredBuffer<float> ResourceReturn() => default;

    private static float get_Length() => 1.0f;

    [OperationMethod<StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>>]
    private static float ResourceIntrinsicImpostor(StructuredBuffer<float> buffer, uint index) => 1.0f;

    private static unsafe void ResourceFunctionPointerParameter(
        delegate*<StructuredBuffer<float>, void> value)
    {
    }

    private static unsafe void ResourceFunctionPointerReturn(
        delegate*<StructuredBuffer<float>> value)
    {
    }

    private static unsafe void OrdinaryFunctionPointerParameter(delegate*<int, void> value)
    {
    }

    private sealed class ReadOnlyBufferShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;

        [Uniform, Group(0), Binding(1)]
        private static float Scale;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade()
        {
            var value = 0.0f;
            if (Input.Length > 1u)
                value = Input[1u] * Scale;
            return DMath.vec4(value, 0.0f, 0.0f, 1.0f);
        }
    }

    private sealed class LengthOnlyShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        public static uint GetLength() => Input.Length;
    }

    private sealed class HighBitIndexShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        public static float Read() => Input[0x80000000u];
    }

    private sealed class RepeatedLengthShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Shade() => Input.Length > 1u && Input.Length > 2u ? Input[1u] : 0.0f;
    }

    private sealed class CrossBlockLengthShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Shade()
        {
            var length = Input.Length;
            return length > 1u ? Input[1u] : 0.0f;
        }
    }

    private sealed class MissingBufferMetadataShader : ISharpShader
    {
#pragma warning disable CS0169
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class WrongElementShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static StructuredBuffer<double> Input;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class UnannotatedWrongElementShader : ISharpShader
    {
#pragma warning disable CS0169
        private static StructuredBuffer<double> Input;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class ConflictingUniformShader : ISharpShader
    {
#pragma warning disable CS0169
        [Uniform, Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class ConflictingReadShader : ISharpShader
    {
#pragma warning disable CS0169
        [Read, Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class InstanceBufferFieldShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private StructuredBuffer<float> Input;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class BufferPropertyShader : ISharpShader
    {
        private static StructuredBuffer<float> Input => default;

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class BufferAutoPropertyShader : ISharpShader
    {
        private static StructuredBuffer<float> Input { get; }

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private static class ResourcePropertyHolder
    {
        public static StructuredBuffer<float> Input => default;
    }

    private sealed class ReferencedBufferPropertyShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Shade() => ResourcePropertyHolder.Input[0u];
    }

    private struct BufferContainer
    {
        public StructuredBuffer<float> Input;
    }

    private sealed class NestedBufferShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Shade(BufferContainer value) => 0.0f;
    }

    private sealed class BufferLocalShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Shade()
        {
            var copy = Input;
            return copy.Length;
        }
    }

    private sealed class BufferArrayShader : ISharpShader
    {
#pragma warning disable CS0169
        private static StructuredBuffer<float>[] Input;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class VisibleDynamicBufferShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(2), Binding(3, true), Vertex, Fragment]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class DuplicateBufferBindingShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> First;

        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Second;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class CrossKindBindingShader : ISharpShader
    {
#pragma warning disable CS0169
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;

        [Uniform, Group(0), Binding(0)]
        private static float Scale;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed class AccessorImpostorShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Shade() => get_Length();
    }

    private static class BufferInitializerProbe
    {
        public static int Executions;

        public static StructuredBuffer<float> Create()
        {
            Executions++;
            return default;
        }
    }

    private readonly struct InitializedBufferShader : ISharpShader
    {
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input = BufferInitializerProbe.Create();

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }

    private sealed unsafe class HiddenFunctionPointerShader : ISharpShader
    {
#pragma warning disable CS0169
        private static delegate*<StructuredBuffer<float>, void> Hidden;
#pragma warning restore CS0169

        [Fragment]
        [return: Location(0)]
        public static float Shade() => 0.0f;
    }
}
