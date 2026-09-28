using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Reflection;
using DualDrill.ApiGen;
using DualDrill.ApiGen.CodeGen;
using DualDrill.ApiGen.DrillGpu;
using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.ApiGen.DrillLang.Value;
using DualDrill.ApiGen.WebIDL;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;

namespace DualDrill.CLSL.NativeTest;

public sealed class GraphicsDescriptorGenerationTests
{
    [Fact]
    public void Webidl_dictionary_inheritance_rejects_missing_bases_and_cycles()
    {
        using var unknown = JsonDocument.Parse(
            """[{"type":"dictionary","name":"Derived","members":[],"inheritance":"Missing"}]""");
        Assert.Throws<InvalidOperationException>(() => WebIDLSpec.Parse(unknown).ToModuleDeclaration());

        using var cycle = JsonDocument.Parse(
            """
            [
              {"type":"dictionary","name":"First","members":[],"inheritance":"Second"},
              {"type":"dictionary","name":"Second","members":[],"inheritance":"First"}
            ]
            """);
        Assert.Throws<InvalidOperationException>(() => WebIDLSpec.Parse(cycle).ToModuleDeclaration());
    }

    [Fact]
    public void Webidl_defaults_and_required_members_survive_parsing_transforms_and_codegen()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(
            root.FullName, "DualDrill.Server", "wwwroot", "spec", "webgpu-webidl.json")))
            root = root.Parent;
        var specPath = Path.Combine(
            root?.FullName ?? throw new FileNotFoundException("WebGPU WebIDL spec was not found."),
            "DualDrill.Server", "wwwroot", "spec", "webgpu-webidl.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(specPath));
        var idl = WebIDLSpec.Parse(doc);
        var deviceFields = idl.Declarations.OfType<DictionaryDeclaration>()
            .Single(d => d.Name == "GPUDeviceDescriptor").Members.OfType<FieldDecl>().ToArray();
        Assert.False(deviceFields.Single(f => f.Name == "defaultQueue").Required);
        Assert.Equal("dictionary", deviceFields.Single(f => f.Name == "defaultQueue").Default?.Type);
        Assert.Equal("sequence", deviceFields.Single(f => f.Name == "requiredFeatures").Default?.Type);
        var deviceDictionary = idl.Declarations.OfType<DictionaryDeclaration>()
            .Single(d => d.Name == "GPUDeviceDescriptor");
        Assert.Equal("GPUObjectDescriptorBase", deviceDictionary.Inheritance);
        var inheritedLabel = idl.GetAllMembers(deviceDictionary).OfType<FieldDecl>().Single(f => f.Name == "label");
        Assert.False(inheritedLabel.Required);
        Assert.Equal(string.Empty, inheritedLabel.Default?.Value?.GetString());

        using var roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(
            idl.Declarations, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        Assert.Equal(
            deviceFields.Select(f => (f.Name, f.Required, f.Default?.Type)),
            WebIDLSpec.Parse(roundTrip).Declarations.OfType<DictionaryDeclaration>()
                .Single(d => d.Name == "GPUDeviceDescriptor").Members.OfType<FieldDecl>()
                .Select(f => (f.Name, f.Required, f.Default?.Type)));

        var native = AlimerWebGPUApi.Create();
        var module = GPUApi.ParseWebGPUWebIDLSpecToModuleDeclaration(
            idl.WebGPUSpecAdHocFix(), native).CodeGenAdHocTransform(native);
        foreach (var name in new[] { "GPUVertexState", "GPUFragmentState" })
        {
            var dictionary = idl.Declarations.OfType<DictionaryDeclaration>().Single(d => d.Name == name);
            Assert.Equal("GPUProgrammableStage", dictionary.Inheritance);
            var inherited = idl.GetAllMembers(dictionary).OfType<FieldDecl>().ToArray();
            Assert.True(inherited.Single(f => f.Name == "module").Required);
            Assert.False(inherited.Single(f => f.Name == "entryPoint").Required);
            var stage = module.Structs.Single(s => s.Name == name);
            Assert.True(stage.Properties.Single(p => p.Name == "Module").IsRequired);
            Assert.False(stage.Properties.Single(p => p.Name == "EntryPoint").IsRequired);
            Assert.Null(stage.Properties.Single(p => p.Name == "Constants").DefaultValue);
            using var stageWriter = new StringWriter();
            new GPUStructCodeGen(module).EmitStruct(stageWriter, stage);
            Assert.Contains("required IGPUShaderModule Module { get; set; }", stageWriter.ToString());
            Assert.Contains("string? EntryPoint { get; set; }", stageWriter.ToString());
            Assert.Contains("ReadOnlyMemory<GPUConstantEntry>? Constants { get; set; }", stageWriter.ToString());
        }
        Assert.True(module.Structs.Single(s => s.Name == "GPUFragmentState")
            .Properties.Single(p => p.Name == "Targets").IsRequired);
        foreach (var name in new[] { "GPUComputePipelineDescriptor", "GPURenderPipelineDescriptor" })
            Assert.False(module.Structs.Single(s => s.Name == name)
                .Properties.Single(p => p.Name == "Layout").IsRequired);
        var device = module.Structs.Single(s => s.Name == "GPUDeviceDescriptor");
        Assert.IsType<EmptyDictionaryValue>(
            device.Properties.Single(p => p.Name == "DefaultQueue").DefaultValue);
        Assert.IsType<EmptySequenceValue>(
            device.Properties.Single(p => p.Name == "RequiredFeatures").DefaultValue);
        var limits = Assert.IsType<RecordTypeReference>(
            device.Properties.Single(p => p.Name == "RequiredLimits").Type);
        Assert.IsType<StringTypeReference>(limits.KeyType);
        Assert.Equal(new IntegerTypeReference(BitWidth._64, false), limits.ValueType);

        var writer = new StringWriter();
        var generator = new GPUStructCodeGen(module);
        foreach (var name in new[]
        {
            "GPUDeviceDescriptor", "GPUBindGroupDescriptor", "GPUBufferDescriptor", "GPUSamplerDescriptor",
            "GPURenderPassDescriptor", "GPUProgrammableStage"
        })
            generator.EmitStruct(writer, module.Structs.Single(s => s.Name == name));
        var output = writer.ToString();
        Assert.Contains("Dictionary<string, ulong> RequiredLimits { get; set; } = new();", output);
        Assert.Contains("GPUQueueDescriptor DefaultQueue { get; set; } = new();", output);
        Assert.Contains("ReadOnlyMemory<GPUFeatureName> RequiredFeatures { get; set; }", output);
        Assert.Contains("required IGPUBindGroupLayout Layout { get; set; }", output);
        Assert.Contains("required ulong Size { get; set; }", output);
        Assert.Contains("bool MappedAtCreation { get; set; } = false;", output);
        Assert.Contains("GPUAddressMode AddressModeU { get; set; } = GPUAddressMode.ClampToEdge;", output);
        Assert.Contains("float LodMaxClamp { get; set; } = (float)32;", output);
        Assert.Contains("GPUCompareFunction? Compare { get; set; }", output);
        Assert.Contains("IGPUQuerySet? OcclusionQuerySet { get; set; }", output);
        Assert.Contains("string? EntryPoint { get; set; }", output);
        Assert.Contains("required IGPUShaderModule Module { get; set; }", output);
    }

    [Fact]
    public void Checked_in_descriptor_defaults_are_usable_without_fake_handles()
    {
        var device = new GPUDeviceDescriptor();
        Assert.Equal(string.Empty, device.Label);
        Assert.Equal(string.Empty, device.DefaultQueue.Label);
        Assert.Empty(device.RequiredLimits);
        Assert.True(device.RequiredFeatures.IsEmpty);
        Assert.Equal(GPUAddressMode.ClampToEdge, new GPUSamplerDescriptor().AddressModeU);
        Assert.Equal(32, new GPUSamplerDescriptor().LodMaxClamp);
        Assert.Null(new GPURenderPassDescriptor
        {
            ColorAttachments = Array.Empty<GPURenderPassColorAttachment>()
        }.OcclusionQuerySet);
        var layout = typeof(GPUBindGroupDescriptor).GetProperty(nameof(GPUBindGroupDescriptor.Layout))
            ?? throw new InvalidOperationException("Bind group layout property is missing.");
        Assert.Contains(layout.GetCustomAttributesData(),
            attribute => attribute.AttributeType == typeof(RequiredMemberAttribute));
        foreach (var stage in new[] { typeof(GPUVertexState), typeof(GPUFragmentState) })
        {
            var moduleProperty = stage.GetProperty("Module")
                ?? throw new InvalidOperationException($"{stage.Name}.Module is missing.");
            Assert.Contains(moduleProperty.GetCustomAttributesData(),
                attribute => attribute.AttributeType == typeof(RequiredMemberAttribute));
            var entryPoint = stage.GetProperty("EntryPoint")
                ?? throw new InvalidOperationException($"{stage.Name}.EntryPoint is missing.");
            Assert.Equal(NullabilityState.Nullable, new NullabilityInfoContext().Create(entryPoint).ReadState);
            Assert.Equal(typeof(ReadOnlyMemory<GPUConstantEntry>?),
                stage.GetProperty("Constants")?.PropertyType);
        }
        Assert.Equal(string.Empty, new GPUTextureDescriptor
        {
            Size = new() { Width = 1, Height = 1 },
            Format = GPUTextureFormat.RGBA8Unorm,
            Usage = GPUTextureUsage.RenderAttachment
        }.Label);
    }

    [Fact]
    public async Task Native_backend_accepts_default_device_and_omitted_optional_view_fields()
    {
        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new GPURequestAdapterOptions { PowerPreference = GPUPowerPreference.HighPerformance },
            CancellationToken.None);
        using var device = await adapter.RequestDeviceAsync(new GPUDeviceDescriptor(), CancellationToken.None);
        using var sampler = device.CreateSampler(new GPUSamplerDescriptor());
        using var texture = device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new GPUExtent3D { Width = 1, Height = 1 },
            Format = GPUTextureFormat.RGBA8Unorm,
            Usage = GPUTextureUsage.RenderAttachment
        });
        using var view = texture.CreateView(new GPUTextureViewDescriptor());
        Assert.NotNull(view);
    }

    [Fact]
    public async Task Native_compute_resolves_a_sole_omitted_entrypoint_but_rejects_ambiguous_or_missing_entries()
    {
        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new GPURequestAdapterOptions { PowerPreference = GPUPowerPreference.HighPerformance },
            CancellationToken.None);
        using var device = await adapter.RequestDeviceAsync(new GPUDeviceDescriptor(), CancellationToken.None);
        using var sole = device.CreateShaderModule(new GPUShaderModuleDescriptor
        {
            Code = "@compute @workgroup_size(1) fn only() {}",
        });
        using var pipeline = device.CreateComputePipeline(new GPUComputePipelineDescriptor
        {
            Compute = new GPUProgrammableStage { Module = sole },
        });
        Assert.NotNull(pipeline);

        using var ambiguous = device.CreateShaderModule(new GPUShaderModuleDescriptor
        {
            Code = "@compute @workgroup_size(1) fn first() {} @compute @workgroup_size(1) fn second() {}",
        });
        Assert.ThrowsAny<GraphicsApiException>(() => device.CreateComputePipeline(new GPUComputePipelineDescriptor
        {
            Compute = new GPUProgrammableStage { Module = ambiguous },
        }));
        using var noCompute = device.CreateShaderModule(new GPUShaderModuleDescriptor
        {
            Code = "@vertex fn vertex() -> @builtin(position) vec4f { return vec4f(0.0); }",
        });
        Assert.ThrowsAny<GraphicsApiException>(() => device.CreateComputePipeline(new GPUComputePipelineDescriptor
        {
            Compute = new GPUProgrammableStage { Module = noCompute },
        }));
        Assert.Throws<ArgumentException>(() => device.CreateComputePipeline(new GPUComputePipelineDescriptor
        {
            Compute = new GPUProgrammableStage { Module = sole, EntryPoint = " " },
        }));
    }

    [Fact]
    public async Task Native_render_pass_rejects_unimplemented_queries_without_dropping_default_options()
    {
        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new GPURequestAdapterOptions { PowerPreference = GPUPowerPreference.HighPerformance },
            CancellationToken.None);
        using var device = await adapter.RequestDeviceAsync(new GPUDeviceDescriptor(), CancellationToken.None);
        using var texture = device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new GPUExtent3D { Width = 1, Height = 1 },
            Format = GPUTextureFormat.RGBA8Unorm,
            Usage = GPUTextureUsage.RenderAttachment,
        });
        using var view = texture.CreateView();
        using var encoder = device.CreateCommandEncoder(new GPUCommandEncoderDescriptor());
        GPURenderPassColorAttachment[] attachments =
        [
            new() { View = view, LoadOp = GPULoadOp.Clear, StoreOp = GPUStoreOp.Store }
        ];
        var foreignQuerySet = DispatchProxy.Create<IGPUQuerySet, ModernWgpuMigrationTests.ForeignProxy>();

        Assert.Throws<NotSupportedException>(() => encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = attachments,
            OcclusionQuerySet = foreignQuerySet,
        }));
        Assert.Throws<NotSupportedException>(() => encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = attachments,
            TimestampWrites = new GPURenderPassTimestampWrites { QuerySet = foreignQuerySet },
        }));

        using (var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = attachments,
        }))
            pass.End();
        using (var limited = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = attachments,
            MaxDrawCount = 1,
        }))
            limited.End();
        using var commands = encoder.Finish(new GPUCommandBufferDescriptor());
        device.Queue.Submit([commands]);
        device.Poll();
    }

    [Fact]
    public async Task Native_render_pipeline_resolves_omitted_vertex_and_fragment_entrypoints()
    {
        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new GPURequestAdapterOptions { PowerPreference = GPUPowerPreference.HighPerformance },
            CancellationToken.None);
        using var device = await adapter.RequestDeviceAsync(new GPUDeviceDescriptor(), CancellationToken.None);
        using var shader = device.CreateShaderModule(new GPUShaderModuleDescriptor
        {
            Code =
                """
                @vertex fn onlyVertex(@builtin(vertex_index) index: u32) -> @builtin(position) vec4f {
                    return vec4f(f32(index), 0.0, 0.0, 1.0);
                }
                @fragment fn onlyFragment() -> @location(0) vec4f {
                    return vec4f(1.0);
                }
                """,
        });
        using var pipeline = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Vertex = new GPUVertexState { Module = shader },
            Fragment = new GPUFragmentState
            {
                Module = shader,
                Targets = new GPUColorTargetState[]
                {
                    new() { Format = GPUTextureFormat.RGBA8Unorm },
                },
            },
        });
        Assert.NotNull(pipeline);
    }

    [Fact]
    public async Task Native_render_pass_uses_explicit_three_dimensional_depth_slice()
    {
        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new GPURequestAdapterOptions { PowerPreference = GPUPowerPreference.HighPerformance },
            CancellationToken.None);
        using var device = await adapter.RequestDeviceAsync(new GPUDeviceDescriptor(), CancellationToken.None);
        using var texture = device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new GPUExtent3D { Width = 1, Height = 1, DepthOrArrayLayers = 2 },
            Dimension = GPUTextureDimension._3D,
            Format = GPUTextureFormat.RGBA8Unorm,
            Usage = GPUTextureUsage.RenderAttachment,
        });
        using var view = texture.CreateView(new GPUTextureViewDescriptor
        {
            Dimension = GPUTextureViewDimension._3D,
        });
        using var encoder = device.CreateCommandEncoder(new GPUCommandEncoderDescriptor());
        using (var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = new GPURenderPassColorAttachment[]
            {
                new() { View = view, DepthSlice = 1, LoadOp = GPULoadOp.Clear, StoreOp = GPUStoreOp.Store }
            },
        }))
            pass.End();
        using var commands = encoder.Finish(new GPUCommandBufferDescriptor());
        device.Queue.Submit([commands]);
        device.Poll();
    }
}
