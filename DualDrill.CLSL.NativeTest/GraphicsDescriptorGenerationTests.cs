using System.Text.Json;
using System.Runtime.CompilerServices;
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
}
