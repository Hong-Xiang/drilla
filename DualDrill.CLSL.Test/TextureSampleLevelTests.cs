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
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Graphics;
using DualDrill.Mathematics;
using Xunit.Abstractions;
using static DualDrill.Mathematics.DMath;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class TextureSampleLevelTests(ITestOutputHelper output)
{
    [Fact]
    public void PublicHandlesAreReadonlyShaderOnlyValues()
    {
        var texture = typeof(Texture2D<float>);
        var sampler = typeof(SamplerState);
        var sampleLevel = texture.GetMethod(nameof(Texture2D<float>.SampleLevel))
            ?? throw new InvalidOperationException("SampleLevel method was not found.");
        var parameters = sampleLevel.GetParameters();

        Assert.True(texture.IsValueType);
        Assert.True(texture.IsDefined(typeof(IsReadOnlyAttribute), inherit: false));
        Assert.Empty(texture.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.True(sampler.IsValueType);
        Assert.True(sampler.IsDefined(typeof(IsReadOnlyAttribute), inherit: false));
        Assert.Empty(sampler.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal(typeof(vec4f32), sampleLevel.ReturnType);
        Assert.Equal(typeof(SamplerState).MakeByRefType(), parameters[0].ParameterType);
        Assert.True(parameters[0].IsIn);
        Assert.False(parameters[0].IsOut);
        Assert.Equal(typeof(vec2f32), parameters[1].ParameterType);
        Assert.Equal(typeof(float), parameters[2].ParameterType);

        var samplerValue = default(SamplerState);
        Assert.Throws<NotSupportedException>(() =>
            default(Texture2D<float>).SampleLevel(in samplerValue, default, 0.0f));
    }

    [Fact]
    public void ExactClosedTypesAndSampleMethodAreRegistered()
    {
        var parser = new RuntimeReflectionParser();
        Assert.Same(
            SampledTexture2DF32Type.Instance,
            parser.ParseType(typeof(Texture2D<float>)));
        Assert.Same(
            SamplerStateType.Instance,
            parser.ParseType(typeof(SamplerState)));
        Assert.Null(parser.Context[typeof(SamplerState).MakeByRefType()]);

        var raw = parser.ParseShaderModule(new TextureSampleLevelShaderModule());
        var resources = raw.Declarations.OfType<VariableDeclaration>()
            .OrderBy(declaration => declaration.Name)
            .ToArray();
        Assert.Collection(
            resources,
            color =>
            {
                Assert.Equal("Color", color.Name);
                Assert.Same(SampledTexture2DF32Type.Instance, color.Type);
                Assert.Same(HandleAddressSpace.Instance, color.AddressSpace);
            },
            linear =>
            {
                Assert.Equal("Linear", linear.Name);
                Assert.Same(SamplerStateType.Instance, linear.Type);
                Assert.Same(HandleAddressSpace.Instance, linear.AddressSpace);
            });

        var method = typeof(TextureSampleLevelShaderModule).GetMethod(nameof(TextureSampleLevelShaderModule.Shade))
            ?? throw new InvalidOperationException("Texture sample entry was not found.");
        var called = CompilerTestPipeline.RawBody(raw, method).Code.Instructions
            .Select(instruction => instruction.Instruction.Operand)
            .OfType<MethodInfo>();
        Assert.Contains(typeof(Texture2D<float>).GetMethod(nameof(Texture2D<float>.SampleLevel)), called);
        Assert.Null(parser.Context[typeof(SamplerState).MakeByRefType()]);
    }

    [Fact]
    public void ActualCilUsesAddressedTextureAndSamplerHandles()
    {
        foreach (var method in new[]
                 {
                     Method<TextureSampleLevelShaderModule>(nameof(TextureSampleLevelShaderModule.Shade)),
                     Method<PlainTextureSampleShader>(nameof(PlainTextureSampleShader.Shade))
                 })
        {
            var instructions = CilMethodDecoder.Decode(method).Instructions;
            Assert.Equal(
                2,
                instructions.Count(instruction => instruction.Instruction.OpCode == OpCodes.Ldsflda));
            Assert.DoesNotContain(instructions, instruction =>
                instruction.Instruction.OpCode == OpCodes.Ldsfld &&
                instruction.Instruction.Operand is FieldInfo field &&
                field.FieldType is var type &&
                (type == typeof(Texture2D<float>) || type == typeof(SamplerState)));
            Assert.Single(instructions, instruction =>
                Equals(
                    instruction.Instruction.Operand,
                    typeof(Texture2D<float>).GetMethod(nameof(Texture2D<float>.SampleLevel))));
            Assert.DoesNotContain(instructions, instruction => instruction.Instruction.OpCode == OpCodes.Callvirt);
            Assert.DoesNotContain(instructions, instruction => instruction.Instruction.OpCode == OpCodes.Ldobj);
        }
    }

    [Fact]
    public void ShaderStackConsumesFourArgumentsAndPreservesPrefix()
    {
        var method = Method<PrefixTextureSampleShader>(nameof(PrefixTextureSampleShader.Shade));
        var stages = CompilerTestPipeline.CompileStages(method);
        var body = Assert.Single(stages.ShaderStack.FunctionDefinitions).Value;
        var annotated = Assert.Single(
            body.Blocks.Blocks.SelectMany(block => block.Body.Elements),
            item => item.Node is ShaderStackInstruction.Operation
            {
                Instruction.Operation: TextureSampleLevelOperation
            });
        var sample = Assert.IsType<ShaderStackInstruction.Operation>(annotated.Node);

        Assert.Equal(4, sample.PopCount);
        Assert.Equal([3, 2, 1, 0], sample.Instruction.Operands
            .Cast<ShaderStackOperand.Depth>()
            .Select(operand => operand.Index));
        Assert.Equal(
            new IShaderType[]
            {
                ShaderType.Vec4F32,
                TextureSampleLevelOperation.Instance.TexturePointerType,
                TextureSampleLevelOperation.Instance.SamplerPointerType,
                ShaderType.Vec2F32,
                ShaderType.F32
            },
            annotated.Annotation.Pre.ToArray());
        Assert.Equal(
            new IShaderType[] { ShaderType.Vec4F32, ShaderType.Vec4F32 },
            annotated.Annotation.Post.ToArray());
    }

    [Fact]
    public async Task ActualCSharpCompilesThroughIrSlangWgslAndReflection()
    {
        var shader = new TextureSampleLevelShaderModule();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.IR));
        var compiled = compiler.Compile(shader);
        var sample = Assert.Single(
            Instructions(Function(compiled, nameof(TextureSampleLevelShaderModule.Shade))),
            instruction => instruction.Operation is TextureSampleLevelOperation);

        Assert.Equal(4, sample.OperandCount);
        Assert.False(sample.RestOperands.IsDefault);
        Assert.Equal(2, sample.RestOperands.Length);
        Assert.Equal(
            [
                TextureSampleLevelOperation.Instance.TexturePointerType,
                TextureSampleLevelOperation.Instance.SamplerPointerType,
                ShaderType.Vec2F32,
                ShaderType.F32
            ],
            sample.Operands.Select(operand => operand.Type));
        Assert.Equal(ShaderType.Vec4F32, sample.Result?.Type);
        Assert.NotNull(sample.Payload);

        var ir = new CLSLCompiler(new(CLSLCompileTarget.IR)).Emit(shader);
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        var targetReflection = await new SlangService().ReflectAsync(slang);
        var operationModule = compiled.RunPass(new FunctionToOperationPass());
        var targetModule = new SlangTargetLowering().Lower(
            operationModule.RunPass(new StablePointerRegionParameterPass()));
        var typedReflection = new ShaderModuleReflection();
        var parsed = new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(shader);
        var textureBindings = typedReflection.GetTextureBindings(parsed);
        var samplerBindings = typedReflection.GetSamplerBindings(parsed);
        var descriptor = typedReflection.GetBindGroupLayoutDescriptor(parsed, 0);
        var effectFunction = Function(
            operationModule,
            nameof(TextureSampleLevelShaderModule.Shade));
        var effects = FunctionEffectAnalysis.Analyze(operationModule)[effectFunction.Declaration];
        output.WriteLine(slang);
        output.WriteLine(wgsl);

        Assert.Contains(TextureSampleLevelOperation.Instance.Name, ir);
        Assert.Contains("Texture2D<vec4<f32>>", slang);
        Assert.Contains("SamplerState", slang);
        Assert.Matches(@"\.SampleLevel\(v_\d+_Linear, v_\d+, v_\d+\)", slang);
        Assert.Contains("texture_2d<f32>", wgsl);
        Assert.Contains("var v_1_Linear_0 : sampler", wgsl);
        Assert.Contains("textureSampleLevel", wgsl);
        Assert.Equal(
            GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute,
            Assert.Single(textureBindings).Visibility);
        Assert.Equal(
            GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute,
            Assert.Single(samplerBindings).Visibility);

        using var reflection = JsonDocument.Parse(targetReflection);
        var bindings = TargetBindings(reflection.RootElement);
        var textureType = bindings[(0, 2)].GetProperty("type");
        Assert.Equal("resource", textureType.GetProperty("kind").GetString());
        Assert.Equal("texture2D", textureType.GetProperty("baseShape").GetString());
        Assert.Equal(
            "float32",
            textureType.GetProperty("resultType")
                .GetProperty("elementType")
                .GetProperty("scalarType")
                .GetString());
        Assert.Equal("samplerState", bindings[(0, 3)].GetProperty("type").GetProperty("kind").GetString());

        var folder = Path.GetDirectoryName(typeof(TextureSampleLevelTests).Assembly.Location)
            ?? throw new InvalidOperationException("Test assembly has no output directory.");
        await File.WriteAllTextAsync(Path.Combine(folder, "r184-texture-sample.ir"), ir);
        await File.WriteAllTextAsync(Path.Combine(folder, "r184-texture-sample.slang"), slang);
        await File.WriteAllTextAsync(Path.Combine(folder, "r184-texture-sample.wgsl"), wgsl);
        await File.WriteAllTextAsync(Path.Combine(folder, "r184-texture-sample.json"), targetReflection);
        await File.WriteAllTextAsync(
            Path.Combine(folder, "r184-texture-sample.cil.txt"),
            DumpCil(Method<TextureSampleLevelShaderModule>(nameof(TextureSampleLevelShaderModule.Shade))));
        await File.WriteAllTextAsync(
            Path.Combine(folder, "r184-texture-sample.target.txt"),
            string.Join(
                Environment.NewLine,
                targetModule.FunctionDefinitions.Values.Select(body => body.PrettyPrint())));
        await File.WriteAllTextAsync(
            Path.Combine(folder, "r184-texture-sample.typed-reflection.json"),
            JsonSerializer.Serialize(
                new
                {
                    Textures = textureBindings,
                    Samplers = samplerBindings,
                    DescriptorEntries = descriptor.Entries.ToArray()
                },
                new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(
            Path.Combine(folder, "r184-texture-sample.effects.txt"),
            string.Join(
                Environment.NewLine,
                [
                    $"requirements: {effects.Requirements}",
                    $"complete: {effects.IsComplete}",
                    $"unknown-sites: {effects.UnknownSites.Length}",
                    .. effects.RequirementSites.Select(site =>
                        $"site: {site.Operation.Name} requirements={site.Requirements} " +
                        $"payload={site.Payload?.GetType().FullName ?? "<null>"}")
                ]));
    }

    [Fact]
    public void SampleLevelHasMemoryReadOnlyAndPreservesEachOrigin()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Compile(new RepeatedTextureSampleShader())
            .RunPass(new FunctionToOperationPass());
        var function = Function(module, nameof(RepeatedTextureSampleShader.Shade));
        var samples = Instructions(function)
            .Where(instruction => instruction.Operation is TextureSampleLevelOperation)
            .ToArray();
        var summary = FunctionEffectAnalysis.Analyze(module)[function.Declaration];

        Assert.Equal(2, samples.Length);
        Assert.True(summary.IsComplete);
        Assert.Equal(OperationRequirement.MemoryRead, summary.Requirements);
        Assert.Empty(summary.UnknownSites);
        var sites = summary.RequirementSites
            .Where(site => site.Operation is TextureSampleLevelOperation)
            .ToArray();
        Assert.Equal(2, sites.Length);
        Assert.All(sites, site =>
        {
            Assert.Equal(OperationRequirement.MemoryRead, site.Requirements);
            Assert.Equal(OperationRequirement.None, site.Requirements & OperationRequirement.DerivativeQuad);
            Assert.NotNull(site.Payload);
        });
        Assert.Equal(
            samples.Select(sample => sample.Payload),
            sites.Select(site => site.Payload));
    }

    [Fact]
    public void ExplicitLodWorksAcrossStagesAndControlFlowShapes()
    {
        ISharpShader[] shaders =
        [
            new PlainTextureSampleShader(),
            new RepeatedTextureSampleShader(),
            new PrefixTextureSampleShader(),
            new CrossBlockTextureSampleShader(),
            new VertexTextureSampleShader(),
            new ComputeTextureSampleShader()
        ];

        foreach (var shader in shaders)
        {
            var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
            var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);

            Assert.Contains(".SampleLevel(", slang);
            Assert.Contains("textureSampleLevel", wgsl);
        }

        var prefixCil = CilMethodDecoder.Decode(
            Method<PrefixTextureSampleShader>(nameof(PrefixTextureSampleShader.Shade))).Instructions;
        var prefixLoad = Assert.Single(prefixCil, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldarg_0);
        var textureAddress = Assert.Single(prefixCil, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldsflda &&
            instruction.Instruction.Operand is FieldInfo { Name: "Color" });
        Assert.True(prefixLoad.ByteOffset < textureAddress.ByteOffset);

        var repeated = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Compile(new RepeatedTextureSampleShader());
        Assert.Equal(
            2,
            Instructions(Function(repeated, nameof(RepeatedTextureSampleShader.Shade)))
                .Count(instruction => instruction.Operation is TextureSampleLevelOperation));

        Assert.Contains(
            "[shader(\"vertex\")]",
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(new VertexTextureSampleShader()));
        Assert.Contains(
            "[shader(\"compute\")]",
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(new ComputeTextureSampleShader()));
    }

    [Fact]
    public async Task TypedReflectionAndDescriptorsDescribeExactHandleKinds()
    {
        var shader = new MixedTextureResourcesShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var module = compiler.Parse(shader);
        var reflection = new ShaderModuleReflection();

        var texture = Assert.Single(reflection.GetTextureBindings(module));
        Assert.Equal("Color", texture.Name);
        Assert.Equal(0, texture.Group);
        Assert.Equal(2, texture.Binding);
        Assert.Equal(
            GPUShaderStage.Vertex | GPUShaderStage.Fragment,
            texture.Visibility);
        Assert.Equal(BindGroupLayoutEntryFlag.Texture, texture.Kind);
        Assert.Equal(GPUTextureViewDimension._2D, texture.Dimension);
        Assert.Equal(GPUTextureSampleType.Float, texture.SampleType);
        Assert.False(texture.Multisampled);

        var sampler = Assert.Single(reflection.GetSamplerBindings(module));
        Assert.Equal("Linear", sampler.Name);
        Assert.Equal(0, sampler.Group);
        Assert.Equal(3, sampler.Binding);
        Assert.Equal(
            GPUShaderStage.Fragment | GPUShaderStage.Compute,
            sampler.Visibility);
        Assert.Equal(BindGroupLayoutEntryFlag.Sampler, sampler.Kind);
        Assert.Equal(GPUSamplerBindingType.Filtering, sampler.SamplerType);
        Assert.False(sampler.Comparison);

        var entries = reflection.GetBindGroupLayoutDescriptor(module, 0).Entries.ToArray();
        Assert.Equal([0, 1, 2, 3], entries.Select(entry => entry.Binding));
        AssertBufferEntry(entries[0], GPUBufferBindingType.Uniform);
        AssertBufferEntry(entries[1], GPUBufferBindingType.ReadOnlyStorage);
        Assert.Equal(default, entries[2].Buffer);
        Assert.Equal(default, entries[2].Sampler);
        Assert.Equal(default, entries[2].StorageTexture);
        Assert.Equal(GPUTextureViewDimension._2D, entries[2].Texture.ViewDimension);
        Assert.Equal(GPUTextureSampleType.Float, entries[2].Texture.SampleType);
        Assert.False(entries[2].Texture.Multisampled);
        Assert.Equal(default, entries[3].Buffer);
        Assert.Equal(default, entries[3].Texture);
        Assert.Equal(default, entries[3].StorageTexture);
        Assert.Equal(GPUSamplerBindingType.Filtering, entries[3].Sampler.Type);

        var bufferOnly = Assert.Throws<NotSupportedException>(
            () => reflection.GetBindGroupLayoutDescriptorBuffer(module, 0));
        Assert.Equal(
            "Bind group 0 contains texture or sampler binding(s) 'Color', 'Linear'; " +
            "use GetBindGroupLayoutDescriptor for mixed resource groups.",
            bufferOnly.Message);
        var group1 = reflection.GetBindGroupLayoutDescriptorBuffer(module, 1).Entries.ToArray();
        Assert.Single(group1);
        Assert.Equal(GPUBufferBindingType.ReadOnlyStorage, group1[0].Buffer.Type);

        var targetText = await new SlangService().ReflectAsync(compiler.Emit(shader));
        using var target = JsonDocument.Parse(targetText);
        var targetBindings = TargetBindings(target.RootElement);
        Assert.Equal("texture2D", targetBindings[(0, 2)].GetProperty("type")
            .GetProperty("baseShape").GetString());
        Assert.Equal("samplerState", targetBindings[(0, 3)].GetProperty("type")
            .GetProperty("kind").GetString());
    }

    public static TheoryData<Type, string> InvalidHandleShaders =>
        new()
        {
            { typeof(MissingTextureMetadataShader), "a sampled texture requires no address-space attribute" },
            { typeof(MissingSamplerMetadataShader), "a sampler requires no address-space attribute" },
            { typeof(NegativeTextureGroupShader), "group must be nonnegative; found -1" },
            { typeof(NegativeSamplerBindingShader), "binding must be nonnegative; found -1" },
            { typeof(WrongTextureElementShader), "only Texture2D<float> is supported" },
            { typeof(ConflictingUniformTextureShader), "a sampled texture requires no address-space attribute" },
            { typeof(ConflictingReadSamplerShader), "sampler attribute(s) [Read] are not supported" },
            { typeof(ConflictingReadWriteTextureShader), "sampled texture attribute(s) [ReadWrite] are not supported" },
            { typeof(DynamicTextureShader), "sampled texture bindings do not support dynamic offsets" },
            { typeof(DynamicSamplerShader), "sampler bindings do not support dynamic offsets" },
            { typeof(DuplicateTextureSamplerBindingShader), "resource binding (0, 2) is duplicated" },
            { typeof(TexturePropertyShader), "must be declared as fields, not properties" },
            { typeof(NestedTextureShader), "structure member field" },
            { typeof(TextureLocalShader), "local variable" },
            { typeof(TextureParameterShader), "parameter" },
            { typeof(TextureReturnShader), "return of method" },
            { typeof(TextureInstanceFieldShader), "must be static shader-module fields" }
        };

    [Theory]
    [MemberData(nameof(InvalidHandleShaders))]
    public void InvalidCSharpHandleDeclarationsAreRejected(Type shaderType, string expected)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));
        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader));

        Assert.Contains(expected, Innermost(exception).Message);
    }

    [Fact]
    public void DuplicateRawHandleMetadataIsRejectedBeforeSetConversion()
    {
        var (type, field) = CreateDuplicateGroupTextureShader();
        Assert.Equal(2, field.GetCustomAttributes<GroupAttribute>(inherit: false).Count());
        var shader = (ISharpShader)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Could not create emitted texture shader."));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(shader));

        Assert.Contains("found 0, 2, and 1", exception.Message);
    }

    [Fact]
    public void DirectIrRejectsDynamicHandleOffsetsAcrossLoweringAndReflection()
    {
        foreach (var resource in new[]
                 {
                     new VariableDeclaration(
                         HandleAddressSpace.Instance,
                         "Color",
                         SampledTexture2DF32Type.Instance,
                         [new GroupAttribute(0), new BindingAttribute(2, true)]),
                     new VariableDeclaration(
                         HandleAddressSpace.Instance,
                         "Linear",
                         SamplerStateType.Instance,
                         [new GroupAttribute(0), new BindingAttribute(3, true)])
                 })
        {
            var module = EmptyModule(resource);
            var lowering = Assert.Throws<NotSupportedException>(
                () => new SlangTargetLowering().Lower(module));
            var reflection = Assert.Throws<NotSupportedException>(
                () => new ShaderModuleReflection().GetBindGroupLayoutDescriptor(module, 0));

            Assert.Contains("bindings do not support dynamic offsets", lowering.Message);
            Assert.Equal(lowering.Message, reflection.Message);
        }
    }

    [Fact]
    public void PointerWrappedResourceModuleDeclarationsRejectAtEverySharedBoundary()
    {
        var resourceTypes = new[]
        {
            SamplerStateType.Instance.GetPtrType(HandleAddressSpace.Instance),
            SamplerStateType.Instance.GetPtrType(HandleAddressSpace.Instance)
                .GetPtrType(GenericAddressSpace.Instance),
            SampledTexture2DF32Type.Instance.GetPtrType(HandleAddressSpace.Instance),
            SampledTexture2DF32Type.Instance.GetPtrType(HandleAddressSpace.Instance)
                .GetPtrType(GenericAddressSpace.Instance),
            ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(StorageAddressSpace.Instance),
            ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(StorageAddressSpace.Instance)
                .GetPtrType(GenericAddressSpace.Instance),
            ReadWriteStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance),
            ReadWriteStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance)
                .GetPtrType(GenericAddressSpace.Instance)
        };

        foreach (var (index, type) in resourceTypes.Index())
        {
            var resource = new VariableDeclaration(
                GenericAddressSpace.Instance,
                $"Wrapped{index}",
                type,
                []);
            AssertWrappedResourceRejected(EmptyModule(resource), resource.Name);
        }
    }

    [Fact]
    public void ResourceMetadataCannotDisguisePointerWrappedDeclaredTypes()
    {
        var disguised = new[]
        {
            new VariableDeclaration(
                UniformAddressSpace.Instance,
                "WrappedTexture",
                SampledTexture2DF32Type.Instance.GetPtrType(HandleAddressSpace.Instance),
                [new UniformAttribute(), new GroupAttribute(0), new BindingAttribute(2)]),
            new VariableDeclaration(
                UniformAddressSpace.Instance,
                "WrappedSampler",
                SamplerStateType.Instance.GetPtrType(HandleAddressSpace.Instance),
                [new UniformAttribute(), new GroupAttribute(0), new BindingAttribute(3)]),
            new VariableDeclaration(
                UniformAddressSpace.Instance,
                "WrappedReadOnly",
                ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance.GetPtrType(StorageAddressSpace.Instance),
                [new UniformAttribute(), new GroupAttribute(1), new BindingAttribute(0)]),
            new VariableDeclaration(
                UniformAddressSpace.Instance,
                "WrappedWritable",
                ReadWriteStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance),
                [new UniformAttribute(), new GroupAttribute(1), new BindingAttribute(1)])
        };

        foreach (var resource in disguised)
            AssertWrappedResourceRejected(EmptyModule(resource), resource.Name);
    }

    [Fact]
    public void DirectResourcesAndOrdinaryScalarPointersRemainValid()
    {
        var resources = new VariableDeclaration[]
        {
            new(
                HandleAddressSpace.Instance,
                "Color",
                SampledTexture2DF32Type.Instance,
                [new GroupAttribute(0), new BindingAttribute(2)]),
            new(
                HandleAddressSpace.Instance,
                "Linear",
                SamplerStateType.Instance,
                [new GroupAttribute(0), new BindingAttribute(3)]),
            new(
                StorageAddressSpace.Instance,
                "Input",
                ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance,
                [new GroupAttribute(1), new BindingAttribute(0)]),
            new(
                StorageAddressSpace.Instance,
                "Output",
                ReadWriteStructuredBufferType.Instance,
                [new GroupAttribute(1), new BindingAttribute(1)])
        };
        var resourceModule = Module(resources);
        var reflection = new ShaderModuleReflection();

        Assert.Single(reflection.GetTextureBindings(resourceModule));
        Assert.Single(reflection.GetSamplerBindings(resourceModule));
        Assert.Equal(2, reflection.GetStorageBufferBindings(resourceModule).Length);
        Assert.Equal(2, reflection.GetBindGroupLayoutDescriptor(resourceModule, 0).Entries.Length);
        Assert.Equal(2, reflection.GetBindGroupLayoutDescriptorBuffer(resourceModule, 1).Entries.Length);
        Assert.Equal(4, new SlangTargetLowering().Lower(resourceModule).Declarations.Length);

        var scalarPointer = new VariableDeclaration(
            GenericAddressSpace.Instance,
            "ScalarPointer",
            ShaderType.F32.GetPtrType(GenericAddressSpace.Instance),
            []);
        var scalarModule = EmptyModule(scalarPointer);
        Assert.Empty(reflection.GetTextureBindings(scalarModule));
        Assert.Empty(reflection.GetSamplerBindings(scalarModule));
        Assert.Empty(reflection.GetStorageBufferBindings(scalarModule));
        Assert.Empty(reflection.GetBindGroupLayoutDescriptor(scalarModule, 0).Entries.ToArray());
        Assert.Empty(reflection.GetBindGroupLayoutDescriptorBuffer(scalarModule, 0).Entries.ToArray());
        Assert.Single(new SlangTargetLowering().Lower(scalarModule).Declarations);
    }

    [Fact]
    public void SampleCallNormalizationRequiresCanonicalCalleeAndPhysicalShape()
    {
        var operation = TextureSampleLevelOperation.Instance;
        var texture = ShaderValue.Intermediate(operation.TexturePointerType);
        var sampler = ShaderValue.Intermediate(operation.SamplerPointerType);
        var uv = ShaderValue.Intermediate(ShaderType.Vec2F32);
        var lod = ShaderValue.Intermediate(ShaderType.F32);
        var result = ShaderValue.Intermediate(ShaderType.Vec4F32);
        var payload = new object();
        var valid = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation((FunctionType)operation.Function.Type),
            result,
            [operation.Function, texture, sampler, uv, lod],
            payload);

        Assert.Equal(5, valid.OperandCount);
        Assert.Equal(new[] { sampler, uv, lod }, valid.RestOperands.ToArray());
        var normalized = OperationModule(valid).RunPass(new FunctionToOperationPass());
        var sample = Assert.Single(Instructions(Function(normalized, "Entry")));
        Assert.IsType<TextureSampleLevelOperation>(sample.Operation);
        Assert.Equal(4, sample.OperandCount);
        Assert.Equal(new[] { uv, lod }, sample.RestOperands.ToArray());
        Assert.Same(payload, sample.Payload);

        var proxy = new FunctionDeclaration(
            "SampleLevelProxy",
            operation.Function.Parameters,
            operation.Function.Return,
            [operation.GetOperationMethodAttribute()]);
        var malformed = new[]
        {
            valid with { OperandCount = 0 },
            valid with { Operand0 = null },
            valid with { RestOperands = [] },
            valid with { RestOperands = [sampler, uv, lod, lod] },
            valid with { RestOperands = [sampler, uv, null!] },
            valid with { RestOperands = default },
            valid with { Operand1 = sampler, RestOperands = [texture, uv, lod] },
            valid with
            {
                Operand1 = ShaderValue.Intermediate(
                    SampledTexture2DF32Type.Instance.GetPtrType(StorageAddressSpace.Instance))
            },
            valid with { RestOperands = [sampler, ShaderValue.Intermediate(ShaderType.I32), lod] },
            valid with { RestOperands = [sampler, uv, ShaderValue.Intermediate(ShaderType.U32)] },
            valid with { Result = ShaderValue.Intermediate(ShaderType.F32) },
            valid with { Result = null },
            valid with { Operand0 = proxy },
            valid with { Operation = new CallOperation(new FunctionType([], ShaderType.Vec4F32)) }
        };
        Assert.All(malformed, AssertOperationMismatch);
    }

    [Fact]
    public void TargetSampleRejectsEveryMalformedPhysicalAndTypedShape()
    {
        var operation = TextureSampleLevelOperation.Instance;
        var texture = ShaderValue.Intermediate(operation.TexturePointerType);
        var sampler = ShaderValue.Intermediate(operation.SamplerPointerType);
        var uv = ShaderValue.Intermediate(ShaderType.Vec2F32);
        var lod = ShaderValue.Intermediate(ShaderType.F32);
        var result = ShaderValue.Intermediate(ShaderType.Vec4F32);
        var valid = Instruction<IShaderValue, IShaderValue>.Create(
            operation,
            result,
            [texture, sampler, uv, lod]);
        var malformed = new[]
        {
            valid with { OperandCount = 0 },
            valid with { Operand0 = null },
            valid with { Operand1 = null },
            valid with { RestOperands = [] },
            valid with { RestOperands = [uv, lod, lod] },
            valid with { RestOperands = [uv, null!] },
            valid with { RestOperands = default },
            valid with { Operand0 = sampler, Operand1 = texture },
            valid with
            {
                Operand0 = ShaderValue.Intermediate(
                    SampledTexture2DF32Type.Instance.GetPtrType(StorageAddressSpace.Instance))
            },
            valid with { Operand0 = ShaderValue.Intermediate(StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance.BufferPointerType) },
            valid with { Operand1 = texture },
            valid with { RestOperands = [ShaderValue.Intermediate(ShaderType.I32), lod] },
            valid with { RestOperands = [uv, ShaderValue.Intermediate(ShaderType.U32)] },
            valid with { Result = ShaderValue.Intermediate(ShaderType.F32) },
            valid with { Result = null }
        };

        Assert.All(malformed, instruction =>
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                new SlangTargetLowering().Lower(OperationModule(instruction)));
            Assert.Contains("invalid texture SampleLevel signature", exception.Message);
        });
    }

    [Fact]
    public void WholeHandleLoadsAndStoresRemainRejected()
    {
        var samplerPointer = ShaderValue.Intermediate(
            SamplerStateType.Instance.GetPtrType(HandleAddressSpace.Instance));
        var samplerValue = ShaderValue.Intermediate(SamplerStateType.Instance);
        var malformed = new[]
        {
            Instruction<IShaderValue, IShaderValue>.Create(
                new LoadOperation(),
                samplerValue,
                [samplerPointer]),
            Instruction<IShaderValue, IShaderValue>.Create(
                new StoreOperation(),
                null,
                [samplerPointer, samplerValue])
        };

        Assert.All(malformed, instruction =>
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                new SlangTargetLowering().Lower(OperationModule(instruction)));
            Assert.Contains("texture/sampler handle", exception.Message);
        });
    }

    [Fact]
    public void OnlyExactRegisteredSampleLevelGetsTheInSamplerSignatureException()
    {
        var impostor = typeof(TextureIntrinsicImpostor).GetMethod(nameof(TextureIntrinsicImpostor.Sample))
            ?? throw new InvalidOperationException("Texture intrinsic impostor was not found.");
        var userMethod = Method<TextureSampleLevelTests>(nameof(UserInSampler));

        var impostorError = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(impostor));
        var userError = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(userMethod));

        Assert.Contains("parameter of mapped intrinsic", impostorError.Message);
        Assert.Contains("texture and sampler handles", impostorError.Message);
        Assert.Contains("parameter", userError.Message);
        Assert.Contains("texture and sampler handles", userError.Message);
    }

    [Fact]
    public void HandleInitializersAreNeverExecuted()
    {
        initializerCalls = 0;

        _ = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(default(InitializerTextureShader));

        Assert.Equal(0, initializerCalls);
    }

    [Fact]
    public void SourceOracleIsExplicitlyNotGpuEvidence()
    {
        var expected = new[] { 0.25f, 0.5f, 0.75f, 1.0f };
        var uv = new[] { 0.5f, 0.5f };

        Assert.Equal([0.25f, 0.5f, 0.75f, 1.0f], expected);
        Assert.Equal([0.5f, 0.5f], uv);
        Assert.Equal(0.0f, 0.0f);
    }

    private static void AssertBufferEntry(
        GPUBindGroupLayoutEntry entry,
        GPUBufferBindingType kind)
    {
        Assert.Equal(kind, entry.Buffer.Type);
        Assert.Equal(default, entry.Sampler);
        Assert.Equal(default, entry.Texture);
        Assert.Equal(default, entry.StorageTexture);
    }

    private static Dictionary<(int Group, int Binding), JsonElement> TargetBindings(JsonElement reflection) =>
        reflection.GetProperty("parameters")
            .EnumerateArray()
            .ToDictionary(parameter =>
            {
                var binding = parameter.GetProperty("binding");
                return (
                    binding.TryGetProperty("space", out var space) ? space.GetInt32() : 0,
                    binding.GetProperty("index").GetInt32());
            });

    private static string DumpCil(MethodInfo method)
    {
        var code = CilMethodDecoder.Decode(method);
        var lines = new List<string>
        {
            $"configuration: {typeof(TextureSampleLevelTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration}",
            $"runtime: {Environment.Version}",
            $"method: {method.DeclaringType?.FullName}.{method.Name}"
        };
        lines.AddRange(code.Instructions.Select(instruction =>
            $"IL_{instruction.ByteOffset:X4}: {instruction.Instruction.OpCode,-12} " +
            $"{FormatOperand(instruction.Instruction.Operand)}".TrimEnd()));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatOperand(object? operand) =>
        operand switch
        {
            null => string.Empty,
            MethodBase method => $"{method.DeclaringType?.FullName}::{method}",
            FieldInfo field => $"{field.DeclaringType?.FullName}::{field.Name} : {field.FieldType}",
            _ => operand.ToString() ?? string.Empty
        };

    private static MethodInfo Method<T>(string name) =>
        typeof(T).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method {typeof(T).FullName}.{name} was not found.");

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

    private static ShaderModuleDeclaration<RegionFunctionBody> EmptyModule(VariableDeclaration resource) =>
        Module([resource]);

    private static ShaderModuleDeclaration<RegionFunctionBody> Module(
        IEnumerable<VariableDeclaration> resources) =>
        new(
            [.. resources],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

    private static void AssertWrappedResourceRejected(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        string name)
    {
        var reflection = new ShaderModuleReflection();
        var attempts = new Func<object?>[]
        {
            () => reflection.GetUniformBindings(module),
            () => reflection.GetStorageBufferBindings(module),
            () => reflection.GetTextureBindings(module),
            () => reflection.GetSamplerBindings(module),
            () => reflection.GetBindGroupLayoutDescriptor(module, 0),
            () => reflection.GetBindGroupLayoutDescriptorBuffer(module, 0),
            () => new SlangTargetLowering().Lower(module)
        };

        Assert.All(attempts, attempt =>
        {
            var exception = Assert.Throws<NotSupportedException>(() => attempt());
            Assert.Equal(
                $"Shader module metadata validation rejected module variable '{name}': " +
                "resource types cannot be pointer-wrapped module declarations; " +
                "declare the resource type directly.",
                exception.Message);
        });
    }

    private static void AssertOperationMismatch(Instruction<IShaderValue, IShaderValue> call)
    {
        var exception = Record.Exception(() =>
            OperationModule(call).RunPass(new FunctionToOperationPass()));
        Assert.NotNull(exception);
        Assert.Equal("OperationFunctionNotMatchException", exception.GetType().Name);
        Assert.Contains("does not match operation", exception.Message);
    }

    private static Exception Innermost(Exception exception)
    {
        while (exception.InnerException is { } inner)
            exception = inner;
        return exception;
    }

    private static (Type Type, FieldInfo Field) CreateDuplicateGroupTextureShader()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"DuplicateTextureMetadata_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("DuplicateTextureMetadata")
            .DefineType(
                "DuplicateTextureMetadataShader",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        type.AddInterfaceImplementation(typeof(ISharpShader));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var field = type.DefineField(
            "Color",
            typeof(Texture2D<float>),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        field.SetCustomAttribute(IntAttribute<GroupAttribute>(0));
        field.SetCustomAttribute(IntAttribute<GroupAttribute>(0));
        field.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BindingAttribute).GetConstructor([typeof(int), typeof(bool)])
            ?? throw new InvalidOperationException("BindingAttribute constructor was not found."),
            [2, false]));
        var created = type.CreateType()
            ?? throw new InvalidOperationException("Duplicate texture metadata type was not created.");
        return (
            created,
            created.GetField("Color", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Duplicate texture field was not found."));
    }

    private static CustomAttributeBuilder IntAttribute<TAttribute>(int value) where TAttribute : Attribute =>
        new(
            typeof(TAttribute).GetConstructor([typeof(int)])
            ?? throw new InvalidOperationException($"{typeof(TAttribute).Name} constructor was not found."),
            [value]);

    private static int initializerCalls;

    private static Texture2D<float> InitializeTexture()
    {
        initializerCalls++;
        return default;
    }

    private static SamplerState InitializeSampler()
    {
        initializerCalls++;
        return default;
    }

    private static void UserInSampler(in SamplerState sampler)
    {
    }

    private sealed class PlainTextureSampleShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(2)]
        private static Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static SamplerState Linear;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade([Location(0)] vec2f32 uv) =>
            Color.SampleLevel(Linear, uv, 0.0f);
    }

    private sealed class RepeatedTextureSampleShader : ISharpShader
    {
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade(
            [Location(0)] vec2f32 uv,
            [Location(1)] float lod) =>
            Color.SampleLevel(Linear, uv, lod)
            + Color.SampleLevel(Linear, uv, lod + 1.0f);
    }

    private sealed class PrefixTextureSampleShader : ISharpShader
    {
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade(
            [Location(1)] vec4f32 prefix,
            [Location(0)] vec2f32 uv,
            [Location(2)] float lod) =>
            prefix + Color.SampleLevel(Linear, uv, lod);
    }

    private sealed class CrossBlockTextureSampleShader : ISharpShader
    {
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade(
            [Location(0)] vec2f32 uv,
            [Location(1)] float lod)
        {
            var sampled = Color.SampleLevel(Linear, uv, lod);
            if (uv.x > 0.0f)
                return sampled;
            return sampled * 0.5f;
        }
    }

    private sealed class VertexTextureSampleShader : ISharpShader
    {
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;

        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 Shade([Location(0)] vec2f32 uv) =>
            Color.SampleLevel(Linear, uv, 0.0f);
    }

    private sealed class ComputeTextureSampleShader : ISharpShader
    {
        [Group(0), Binding(0)]
        private static readonly RWStructuredBuffer<float> Output;

        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;

        [Compute]
        [WorkgroupSize(1, 1, 1)]
        public static void Shade()
        {
            Output[0u] = Color.SampleLevel(Linear, vec2(0.5f), 0.0f).x;
        }
    }

    private sealed class MixedTextureResourcesShader : ISharpShader
    {
        [Uniform, Group(0), Binding(0)]
        private static readonly float Scale;

        [Group(0), Binding(1)]
        private static readonly StructuredBuffer<float> Input;

        [Group(0), Binding(2), Vertex, Fragment]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3), Fragment, Compute]
        private static readonly SamplerState Linear;

        [Group(1), Binding(0)]
        private static readonly StructuredBuffer<float> Other;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade([Location(0)] vec2f32 uv) =>
            Color.SampleLevel(Linear, uv, 0.0f) * Scale
            + vec4(Input[0u] + Other[0u]);
    }

    private sealed class MissingTextureMetadataShader : ISharpShader
    {
        private static readonly Texture2D<float> Color;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class MissingSamplerMetadataShader : ISharpShader
    {
        private static readonly SamplerState Linear;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class NegativeTextureGroupShader : ISharpShader
    {
        [Group(-1), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class WrongTextureElementShader : ISharpShader
    {
        [Group(0), Binding(2)]
        private static readonly Texture2D<int> Color;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class NegativeSamplerBindingShader : ISharpShader
    {
        [Group(0), Binding(-1)]
        private static readonly SamplerState Linear;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class ConflictingUniformTextureShader : ISharpShader
    {
        [Uniform, Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class ConflictingReadSamplerShader : ISharpShader
    {
        [Read, Group(0), Binding(3)]
        private static readonly SamplerState Linear;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class ConflictingReadWriteTextureShader : ISharpShader
    {
        [ReadWrite, Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class DynamicTextureShader : ISharpShader
    {
        [Group(0), Binding(2, true)]
        private static readonly Texture2D<float> Color;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class DynamicSamplerShader : ISharpShader
    {
        [Group(0), Binding(3, true)]
        private static readonly SamplerState Linear;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class DuplicateTextureSamplerBindingShader : ISharpShader
    {
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(2)]
        private static readonly SamplerState Linear;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class TexturePropertyShader : ISharpShader
    {
        private static Texture2D<float> Color { get; }

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class NestedTextureShader : ISharpShader
    {
        private struct Resources
        {
            public Texture2D<float> Color;
        }

        [Group(0), Binding(2)]
        private static readonly Resources Data;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private sealed class TextureLocalShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade()
        {
            Texture2D<float> color = default;
            return color.SampleLevel(default, default, 0.0f);
        }
    }

    private sealed class TextureParameterShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade(Texture2D<float> color) => vec4(0.0f);
    }

    private sealed class TextureReturnShader : ISharpShader
    {
        [Fragment]
        public static Texture2D<float> Shade() => default;
    }

    private sealed class TextureInstanceFieldShader : ISharpShader
    {
        [Group(0), Binding(2)]
        private readonly Texture2D<float> Color;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private struct InitializerTextureShader : ISharpShader
    {
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color = InitializeTexture();

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear = InitializeSampler();

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade([Location(0)] vec2f32 uv) =>
            Color.SampleLevel(Linear, uv, 0.0f);
    }

    private static class TextureIntrinsicImpostor
    {
        [OperationMethod<TextureSampleLevelOperation>]
        public static vec4f32 Sample(
            in SamplerState sampler,
            vec2f32 uv,
            float lod) =>
            default;
    }
}
