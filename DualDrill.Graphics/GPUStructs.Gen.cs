namespace DualDrill.Graphics;

public partial struct GPUBindGroupDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public required ReadOnlyMemory<GPUBindGroupEntry> Entries { get; set; }
    public required IGPUBindGroupLayout Layout { get; set; }
}

public partial struct GPUBindGroupLayoutDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public required ReadOnlyMemory<GPUBindGroupLayoutEntry> Entries { get; set; }
}

public partial struct GPUBindGroupLayoutDescriptorBuffer()
{
    public string Label { get; set; } = string.Empty;
    public required ReadOnlyMemory<GPUBindGroupLayoutEntryBuffer> Entries { get; set; }
}

public partial struct GPUBlendComponent()
{
    public GPUBlendFactor DstFactor { get; set; } = GPUBlendFactor.Zero;
    public GPUBlendOperation Operation { get; set; } = GPUBlendOperation.Add;
    public GPUBlendFactor SrcFactor { get; set; } = GPUBlendFactor.One;
}

public partial struct GPUBlendState()
{
    public required GPUBlendComponent Alpha { get; set; }
    public required GPUBlendComponent Color { get; set; }
}

public partial struct GPUBufferBinding()
{
    public required IGPUBuffer Buffer { get; set; }
    public ulong Offset { get; set; } = 0;
    public ulong? Size { get; set; }
}

public partial struct GPUBufferBindingLayout()
{
    public bool HasDynamicOffset { get; set; } = false;
    public ulong MinBindingSize { get; set; } = 0;
    public GPUBufferBindingType Type { get; set; } = GPUBufferBindingType.Uniform;
}

public partial struct GPUBufferDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public bool MappedAtCreation { get; set; } = false;
    public required ulong Size { get; set; }
    public required GPUBufferUsage Usage { get; set; }
}

public partial struct GPUColor()
{
    public required double A { get; set; }
    public required double B { get; set; }
    public required double G { get; set; }
    public required double R { get; set; }
}

public partial struct GPUColorTargetState()
{
    public GPUBlendState? Blend { get; set; }
    public required GPUTextureFormat Format { get; set; }
    public GPUColorWriteMask WriteMask { get; set; } = (GPUColorWriteMask)0xF;
}

public partial struct GPUCommandBufferDescriptor()
{
    public string Label { get; set; } = string.Empty;
}

public partial struct GPUCommandEncoderDescriptor()
{
    public string Label { get; set; } = string.Empty;
}

public partial struct GPUComputePassDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public GPUComputePassTimestampWrites? TimestampWrites { get; set; }
}

public partial struct GPUComputePassTimestampWrites()
{
    public uint? BeginningOfPassWriteIndex { get; set; }
    public uint? EndOfPassWriteIndex { get; set; }
    public required IGPUQuerySet QuerySet { get; set; }
}

public partial struct GPUComputePipelineDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public required GPUProgrammableStage Compute { get; set; }
    public IGPUPipelineLayout? Layout { get; set; }
}

public partial struct GPUDepthStencilState()
{
    public int DepthBias { get; set; } = 0;
    public float DepthBiasClamp { get; set; } = (float)0;
    public float DepthBiasSlopeScale { get; set; } = (float)0;
    public GPUCompareFunction? DepthCompare { get; set; }
    public bool? DepthWriteEnabled { get; set; }
    public required GPUTextureFormat Format { get; set; }
    public GPUStencilFaceState StencilBack { get; set; } = new();
    public GPUStencilFaceState StencilFront { get; set; } = new();
    public uint StencilReadMask { get; set; } = 0xFFFFFFFF;
    public uint StencilWriteMask { get; set; } = 0xFFFFFFFF;
}

public partial struct GPUDeviceDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public GPUQueueDescriptor DefaultQueue { get; set; } = new();
    public ReadOnlyMemory<GPUFeatureName> RequiredFeatures { get; set; }
    public Dictionary<string, ulong> RequiredLimits { get; set; } = new();
}

public partial record struct GPUImageCopyBuffer()
{
    public GPUImageDataLayout Layout { get; set; }
    public required IGPUBuffer Buffer { get; set; }
}

public partial struct GPUImageCopyTexture()
{
    public GPUTextureAspect Aspect { get; set; } = GPUTextureAspect.All;
    public uint MipLevel { get; set; } = 0;
    public GPUOrigin3D Origin { get; set; } = new();
    public required IGPUTexture Texture { get; set; }
}

public partial record struct GPUImageDataLayout()
{
    /// <summary>
    /// Bytes Per Row must be mulitlier of 256
    /// </summary>
    public uint BytesPerRow { get; set; }
    public ulong Offset { get; set; }
    public uint RowsPerImage { get; set; }
}

public partial struct GPUMultisampleState()
{
    public bool AlphaToCoverageEnabled { get; set; } = false;
    public uint Count { get; set; } = 1;
    public uint Mask { get; set; } = 0xFFFFFFFF;
}

public partial struct GPUOrigin2D()
{
    public uint X { get; set; } = 0;
    public uint Y { get; set; } = 0;
}

public partial struct GPUOrigin3D()
{
    public uint X { get; set; } = 0;
    public uint Y { get; set; } = 0;
    public uint Z { get; set; } = 0;
}

public partial struct GPUPipelineLayoutDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public required IReadOnlyList<IGPUBindGroupLayout> BindGroupLayouts { get; set; }
}

public partial struct GPUPrimitiveState()
{
    public GPUCullMode CullMode { get; set; } = GPUCullMode.None;
    public GPUFrontFace FrontFace { get; set; } = GPUFrontFace.CCW;
    public GPUIndexFormat? StripIndexFormat { get; set; }
    public GPUPrimitiveTopology Topology { get; set; } = GPUPrimitiveTopology.TriangleList;
    public bool UnclippedDepth { get; set; } = false;
}

public partial struct GPUProgrammableStage()
{
    public Dictionary<string, string>? Constants { get; set; }
    public string? EntryPoint { get; set; }
    public required IGPUShaderModule Module { get; set; }
}

public partial struct GPUQuerySetDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public required uint Count { get; set; }
    public required GPUQueryType Type { get; set; }
}

public partial struct GPUQueueDescriptor()
{
    public string Label { get; set; } = string.Empty;
}

public partial struct GPURenderBundleDescriptor()
{
    public string Label { get; set; } = string.Empty;
}

public partial struct GPURenderBundleEncoderDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public required ReadOnlyMemory<GPUTextureFormat?> ColorFormats { get; set; }
    public bool DepthReadOnly { get; set; } = false;
    public GPUTextureFormat? DepthStencilFormat { get; set; }
    public uint SampleCount { get; set; } = 1;
    public bool StencilReadOnly { get; set; } = false;
}

public partial struct GPURenderPassColorAttachment()
{
    public GPUColor? ClearValue { get; set; }
    public uint? DepthSlice { get; set; }
    public required GPULoadOp LoadOp { get; set; }
    public IGPUTextureView? ResolveTarget { get; set; }
    public required GPUStoreOp StoreOp { get; set; }
    public required IGPUTextureView View { get; set; }
}

public partial struct GPURenderPassDepthStencilAttachment()
{
    public float? DepthClearValue { get; set; }
    public GPULoadOp? DepthLoadOp { get; set; }
    public bool DepthReadOnly { get; set; } = false;
    public GPUStoreOp? DepthStoreOp { get; set; }
    public uint StencilClearValue { get; set; } = 0;
    public GPULoadOp? StencilLoadOp { get; set; }
    public bool StencilReadOnly { get; set; } = false;
    public GPUStoreOp? StencilStoreOp { get; set; }
    public required IGPUTextureView View { get; set; }
}

public partial struct GPURenderPassDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public required ReadOnlyMemory<GPURenderPassColorAttachment> ColorAttachments { get; set; }
    public GPURenderPassDepthStencilAttachment? DepthStencilAttachment { get; set; }
    public ulong MaxDrawCount { get; set; } = 50000000;
    public IGPUQuerySet? OcclusionQuerySet { get; set; }
    public GPURenderPassTimestampWrites? TimestampWrites { get; set; }
}

public partial struct GPURenderPassLayout()
{
    public required ReadOnlyMemory<GPUTextureFormat?> ColorFormats { get; set; }
    public GPUTextureFormat? DepthStencilFormat { get; set; }
    public string Label { get; set; } = string.Empty;
    public uint SampleCount { get; set; } = 1;
}

public partial struct GPURenderPassTimestampWrites()
{
    public uint? BeginningOfPassWriteIndex { get; set; }
    public uint? EndOfPassWriteIndex { get; set; }
    public required IGPUQuerySet QuerySet { get; set; }
}

public partial struct GPURenderPipelineDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public GPUDepthStencilState? DepthStencil { get; set; }
    public GPUFragmentState? Fragment { get; set; }
    public GPUMultisampleState Multisample { get; set; } = new();
    public GPUPrimitiveState Primitive { get; set; } = new();
    public required GPUVertexState Vertex { get; set; }
    public IGPUPipelineLayout? Layout { get; set; }
}

//public partial struct GPURequestAdapterOptions()
//{
//    public bool ForceFallbackAdapter { get; set; }
//    public GPUPowerPreference PowerPreference { get; set; }
//}

public partial struct GPUSamplerBindingLayout()
{
    public GPUSamplerBindingType Type { get; set; } = GPUSamplerBindingType.Filtering;
}

public partial struct GPUSamplerDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public GPUAddressMode AddressModeU { get; set; } = GPUAddressMode.ClampToEdge;
    public GPUAddressMode AddressModeV { get; set; } = GPUAddressMode.ClampToEdge;
    public GPUAddressMode AddressModeW { get; set; } = GPUAddressMode.ClampToEdge;
    public GPUCompareFunction? Compare { get; set; }
    public float LodMaxClamp { get; set; } = (float)32;
    public float LodMinClamp { get; set; } = (float)0;
    public GPUFilterMode MagFilter { get; set; } = GPUFilterMode.Nearest;
    public ushort MaxAnisotropy { get; set; } = 1;
    public GPUFilterMode MinFilter { get; set; } = GPUFilterMode.Nearest;
    public GPUMipmapFilterMode MipmapFilter { get; set; } = GPUMipmapFilterMode.Nearest;
}

public partial struct GPUShaderModuleCompilationHint()
{
    public required string EntryPoint { get; set; }
    public IGPUPipelineLayout? Layout { get; set; }
}

public partial struct GPUShaderModuleDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public required string Code { get; set; }
    public ReadOnlyMemory<GPUShaderModuleCompilationHint> CompilationHints { get; set; }
}

public partial struct GPUStencilFaceState()
{
    public GPUCompareFunction Compare { get; set; } = GPUCompareFunction.Always;
    public GPUStencilOperation DepthFailOp { get; set; } = GPUStencilOperation.Keep;
    public GPUStencilOperation FailOp { get; set; } = GPUStencilOperation.Keep;
    public GPUStencilOperation PassOp { get; set; } = GPUStencilOperation.Keep;
}

public partial struct GPUStorageTextureBindingLayout()
{
    public GPUStorageTextureAccess Access { get; set; } = GPUStorageTextureAccess.WriteOnly;
    public required GPUTextureFormat Format { get; set; }
    public GPUTextureViewDimension ViewDimension { get; set; } = GPUTextureViewDimension._2D;
}

public partial struct GPUSurfaceConfiguration()
{
    public int Width { get; set; }
    public int Height { get; set; }
    public GPUCompositeAlphaMode AlphaMode { get; set; } = GPUCompositeAlphaMode.Opaque;
    public required IGPUDevice Device { get; set; }
    public required GPUTextureFormat Format { get; set; }
    public GPUTextureUsage Usage { get; set; } = GPUTextureUsage.RenderAttachment;
    public GPUPresentMode PresentMode { get; set; }
    public IReadOnlyList<GPUTextureFormat> ViewFormats { get; set; } = [];
}

public partial struct GPUTextureBindingLayout()
{
    public bool Multisampled { get; set; } = false;
    public GPUTextureSampleType SampleType { get; set; } = GPUTextureSampleType.Float;
    public GPUTextureViewDimension ViewDimension { get; set; } = GPUTextureViewDimension._2D;
}

//public partial struct GPUTextureDescriptor()
//{
//    public string Label { get; set; }
//    public GPUTextureDimension Dimension { get; set; }
//    public GPUTextureFormat Format { get; set; }
//    public uint MipLevelCount { get; set; }
//    public uint SampleCount { get; set; }
//    public GPUExtent3D Size { get; set; }
//    public GPUTextureUsage Usage { get; set; }
//    public ReadOnlyMemory<GPUTextureFormat> ViewFormats { get; set; }
//}

public partial struct GPUTextureViewDescriptor()
{
    public string Label { get; set; } = string.Empty;
    public uint? ArrayLayerCount { get; set; }
    public GPUTextureAspect Aspect { get; set; } = GPUTextureAspect.All;
    public uint BaseArrayLayer { get; set; } = 0;
    public uint BaseMipLevel { get; set; } = 0;
    public GPUTextureViewDimension? Dimension { get; set; }
    public GPUTextureFormat? Format { get; set; }
    public uint? MipLevelCount { get; set; }
}

public partial struct GPUUncapturedErrorEventInit()
{
}

public partial struct GPUVertexAttribute()
{
    public required GPUVertexFormat Format { get; set; }
    public required ulong Offset { get; set; }
    public required int ShaderLocation { get; set; }
}

public partial struct GPUVertexBufferLayout()
{
    public required ulong ArrayStride { get; set; }
    public required ReadOnlyMemory<GPUVertexAttribute> Attributes { get; set; }
    public GPUVertexStepMode StepMode { get; set; } = GPUVertexStepMode.Vertex;
}
