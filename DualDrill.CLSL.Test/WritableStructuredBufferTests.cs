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

    [Fact]
    public void ExactClosedTypeAndAllAccessorsAreRegistered()
    {
        var parser = new RuntimeReflectionParser();
        var type = Assert.IsType<ReadWriteStructuredBufferType>(
            parser.ParseType(typeof(RWStructuredBuffer<float>)));
        Assert.Equal(ShaderType.F32, type.ElementType);
        Assert.Equal(4u, type.ElementStride);

        var raw = parser.ParseShaderModule(new DoubleValuesShader());
        var outputResource = Assert.Single(
            raw.Declarations.OfType<VariableDeclaration>(),
            declaration => declaration.Name == "Output");
        Assert.Same(ReadWriteStructuredBufferType.Instance, outputResource.Type);
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
            instruction.Operation is StructuredBufferLengthOperation);
        Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLengthOperation);
        Assert.Single(instructions, instruction =>
            instruction.Operation is StructuredBufferLoadOperation);
        var store = Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation);
        Assert.Null(store.Result);
        Assert.Equal(
            [
                ReadWriteStructuredBufferStoreOperation.Instance.BufferPointerType,
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
            instruction.Operation is ReadWriteStructuredBufferLengthOperation);
        Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLoadOperation);
        Assert.Single(instructions, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation);

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
                ReadWriteStructuredBufferLengthOperation or
                ReadWriteStructuredBufferLoadOperation or
                ReadWriteStructuredBufferStoreOperation)
            .ToArray();

        var summary = FunctionEffectAnalysis.Analyze(module)[function.Declaration];

        Assert.True(summary.IsComplete);
        Assert.Equal(3, resourceOperations.Length);
        Assert.DoesNotContain(summary.RequirementSites, site =>
            site.Operation is ReadWriteStructuredBufferLengthOperation);
        var load = Assert.Single(summary.RequirementSites, site =>
            site.Operation is ReadWriteStructuredBufferLoadOperation);
        var store = Assert.Single(summary.RequirementSites, site =>
            site.Operation is ReadWriteStructuredBufferStoreOperation);
        Assert.Equal(OperationRequirement.MemoryRead, load.Requirements);
        Assert.Equal(OperationRequirement.MemoryWrite, store.Requirements);
        Assert.NotNull(load.Payload);
        Assert.NotNull(store.Payload);
        Assert.Same(
            resourceOperations.Single(instruction =>
                instruction.Operation is ReadWriteStructuredBufferLoadOperation).Payload,
            load.Payload);
        Assert.Same(
            resourceOperations.Single(instruction =>
                instruction.Operation is ReadWriteStructuredBufferStoreOperation).Payload,
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
        var (shaderType, field) = CreateRawDuplicateGroupWritableShader();
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
            ReadWriteStructuredBufferType.Instance,
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
    [InlineData(typeof(WrongWritableElementShader), "only RWStructuredBuffer<float> is supported")]
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
            instruction.Operation is ReadWriteStructuredBufferStoreOperation);
        Assert.Null(store.Result);
        Assert.Equal(ShaderType.U32, store.Operand1?.Type);
        Assert.Single(instructions, instruction =>
            instruction.Operation is CallOperation &&
            instruction.Operand0 is FunctionDeclaration { Name: nameof(HighBitStoreShader.Value) });
    }

    [Fact]
    public void StoreConversionPreservesAnExistingStackPrefix()
    {
        var method = CreatePrefixStoreMethod();
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Compile(new RuntimeReflectionParser().ParseMethod(method));
        var function = Function(compiled, "StoreWithPrefix");
        var store = Assert.Single(Instructions(function), instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation);

        Assert.Null(store.Result);
        Assert.Equal(ShaderType.F32, function.Declaration.Return.Type);
    }

    [Fact]
    public void StoreCallNormalizationRequiresNullResultAndExactSignatures()
    {
        var operation = ReadWriteStructuredBufferStoreOperation.Instance;
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
        Assert.IsType<ReadWriteStructuredBufferStoreOperation>(store.Operation);
        Assert.Null(store.Result);
        Assert.Same(payload, store.Payload);

        var unitResult = valid with { Result = ShaderValue.Intermediate(UnitType.Instance) };
        AssertOperationMismatch(unitResult);
        AssertOperationMismatch(valid with
        {
            Operand1 = ShaderValue.Intermediate(
                ReadWriteStructuredBufferType.Instance.GetPtrType(FunctionAddressSpace.Instance))
        });
        AssertOperationMismatch(valid with
        {
            Operand1 = ShaderValue.Intermediate(
                ReadWriteStructuredBufferType.Instance.GetPtrType(GenericAddressSpace.Instance))
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
        var roLength = StructuredBufferLengthOperation.Instance;
        var roLoad = StructuredBufferLoadOperation.Instance;
        var rwLength = ReadWriteStructuredBufferLengthOperation.Instance;
        var rwLoad = ReadWriteStructuredBufferLoadOperation.Instance;
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
        var operation = ReadWriteStructuredBufferStoreOperation.Instance;
        var outputResource = new VariableDeclaration(
            StorageAddressSpace.Instance,
            "Output",
            ReadWriteStructuredBufferType.Instance,
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
        Assert.Equal(ReadWriteStructuredBufferType.Instance, indexed.Target.Type);
        Assert.Equal(ShaderType.U32, indexed.Index.Type);
        Assert.Equal(ShaderType.F32, indexed.Type);
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
                    ReadWriteStructuredBufferType.Instance.GetPtrType(FunctionAddressSpace.Instance))
            },
            valid with
            {
                Operand0 = ShaderValue.Intermediate(
                    ReadWriteStructuredBufferType.Instance.GetPtrType(GenericAddressSpace.Instance))
            },
            valid with
            {
                Operand0 = ShaderValue.Intermediate(
                    ReadOnlyStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance))
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
            origin.Source.Operation is ReadWriteStructuredBufferStoreOperation);
        var assignment = Assert.IsType<SlangAssign>(store.Target);
        var indexed = Assert.IsType<SlangIndexedPlace>(assignment.Target);
        var index = Assert.IsType<SlangVariablePlace>(
            Assert.IsType<SlangPlaceOperand>(indexed.Index).Place);

        Assert.IsType<ReadWriteStructuredBufferLengthOperation>(dimensions.Source.Operation);
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
        var operation = ReadWriteStructuredBufferStoreOperation.Instance;
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
        var operation = ReadWriteStructuredBufferStoreOperation.Instance;
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
            ReadWriteStructuredBufferType.Instance,
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
                    ReadWriteStructuredBufferLengthOperation.Instance,
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
                    ReadWriteStructuredBufferStoreOperation.Instance,
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

    private static MethodInfo CreatePrefixStoreMethod()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"WritableBufferPrefix_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("WritableBufferPrefix");
        var type = module.DefineType(
            "WritableBufferPrefixShader",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        var field = type.DefineField(
            "Output",
            typeof(RWStructuredBuffer<float>),
            FieldAttributes.Private | FieldAttributes.Static);
        field.SetCustomAttribute(IntAttribute<GroupAttribute>(0));
        field.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BindingAttribute).GetConstructor([typeof(int), typeof(bool)])!,
            [0, false]));
        var method = type.DefineMethod(
            "StoreWithPrefix",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(float),
            [typeof(float), typeof(uint), typeof(float)]);
        method.DefineParameter(1, ParameterAttributes.None, "prefix");
        method.DefineParameter(2, ParameterAttributes.None, "index");
        method.DefineParameter(3, ParameterAttributes.None, "value");
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldsflda, field);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, typeof(RWStructuredBuffer<float>).GetProperty("Item")!.SetMethod!);
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

    private static (Type ShaderType, FieldInfo Field) CreateRawDuplicateGroupWritableShader()
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
            typeof(RWStructuredBuffer<float>),
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
        private static RWStructuredBuffer<int> Output;
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
