using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;
public partial struct GPUBindGroupDescriptor()
{
    public string Label { get; set; }
    public ReadOnlyMemory<GPUBindGroupEntry> Entries { get; set; }
    public IGPUBindGroupLayout Layout { get; set; }
}

public partial struct GPUBindGroupLayoutDescriptor()
{
    public string Label { get; set; }
    public ReadOnlyMemory<GPUBindGroupLayoutEntry> Entries { get; set; }
}

public partial struct GPUBlendComponent()
{
    public GPUBlendFactor DstFactor { get; set; }
    public GPUBlendOperation Operation { get; set; }
    public GPUBlendFactor SrcFactor { get; set; }
}

public partial struct GPUBlendState()
{
    public GPUBlendComponent Alpha { get; set; }
    public GPUBlendComponent Color { get; set; }
}

public partial struct GPUBufferBinding()
{
    public IGPUBuffer Buffer { get; set; }
    public ulong Offset { get; set; }
    public ulong Size { get; set; }
}

public partial struct GPUBufferBindingLayout()
{
    public bool HasDynamicOffset { get; set; }
    public ulong MinBindingSize { get; set; }
    public GPUBufferBindingType Type { get; set; }
}

public partial struct GPUBufferDescriptor()
{
    public string Label { get; set; }
    public bool MappedAtCreation { get; set; } = false;
    public ulong Size { get; set; }
    public GPUBufferUsage Usage { get; set; }
}

public partial struct GPUColor()
{
    public double A { get; set; }
    public double B { get; set; }
    public double G { get; set; }
    public double R { get; set; }
}

public partial struct GPUColorTargetState()
{
    public GPUBlendState? Blend { get; set; }
    public GPUTextureFormat Format { get; set; }
    public GPUColorWriteMask WriteMask { get; set; }
}

public partial struct GPUCommandBufferDescriptor()
{
    public string Label { get; set; }
}

public partial struct GPUCommandEncoderDescriptor()
{
    public string Label { get; set; }
}

public partial struct GPUComputePassDescriptor()
{
    public string Label { get; set; }
    public GPUComputePassTimestampWrites TimestampWrites { get; set; }
}

public partial struct GPUComputePassTimestampWrites()
{
    public uint BeginningOfPassWriteIndex { get; set; }
    public uint EndOfPassWriteIndex { get; set; }
    public IGPUQuerySet QuerySet { get; set; }
}

public partial struct GPUComputePipelineDescriptor()
{
    public string Label { get; set; }
    public GPUProgrammableStage Compute { get; set; }
}

public partial struct GPUDepthStencilState()
{
    public int DepthBias { get; set; }
    public float DepthBiasClamp { get; set; }
    public float DepthBiasSlopeScale { get; set; }
    public GPUCompareFunction DepthCompare { get; set; }
    public bool DepthWriteEnabled { get; set; }
    public GPUTextureFormat Format { get; set; }
    public GPUStencilFaceState StencilBack { get; set; }
    public GPUStencilFaceState StencilFront { get; set; }
    public uint StencilReadMask { get; set; }
    public uint StencilWriteMask { get; set; }
}

public partial struct GPUDeviceDescriptor()
{
    public string Label { get; set; }
    public GPUQueueDescriptor DefaultQueue { get; set; }
    public ReadOnlyMemory<GPUFeatureName> RequiredFeatures { get; set; }
    public Dictionary<string, string> RequiredLimits { get; set; }
}

//public partial struct GPUFragmentState()
//{
//    public ReadOnlyMemory<GPUColorTargetState?> Targets { get; set; }
//}

public partial record struct GPUImageCopyBuffer()
{
    public GPUImageDataLayout Layout { get; set; }
    public IGPUBuffer Buffer { get; set; }
}

public partial struct GPUImageCopyTexture()
{
    public GPUTextureAspect Aspect { get; set; }
    public uint MipLevel { get; set; }
    public GPUOrigin3D Origin { get; set; }
    public IGPUTexture Texture { get; set; }
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
    public bool AlphaToCoverageEnabled { get; set; }
    public uint Count { get; set; }
    public uint Mask { get; set; }
}

public partial struct GPUOrigin2D()
{
    public uint X { get; set; }
    public uint Y { get; set; }
}

public partial struct GPUOrigin3D()
{
    public uint X { get; set; }
    public uint Y { get; set; }
    public uint Z { get; set; }
}

public partial struct GPUPipelineLayoutDescriptor()
{
    public string Label { get; set; }
    public IReadOnlyList<IGPUBindGroupLayout> BindGroupLayouts { get; set; }
}

public partial struct GPUPrimitiveState()
{
    public GPUCullMode CullMode { get; set; }
    public GPUFrontFace FrontFace { get; set; }
    public GPUIndexFormat StripIndexFormat { get; set; }
    public GPUPrimitiveTopology Topology { get; set; }
    public bool UnclippedDepth { get; set; }
}

public partial struct GPUProgrammableStage()
{
    public Dictionary<string, string> Constants { get; set; }
    public string EntryPoint { get; set; }
    public IGPUShaderModule Module { get; set; }
}

public partial struct GPUQuerySetDescriptor()
{
    public string Label { get; set; }
    public uint Count { get; set; }
    public GPUQueryType Type { get; set; }
}

public partial struct GPUQueueDescriptor()
{
    public string Label { get; set; }
}

public partial struct GPURenderBundleDescriptor()
{
    public string Label { get; set; }
}

public partial struct GPURenderBundleEncoderDescriptor()
{
    public string Label { get; set; }
    public bool DepthReadOnly { get; set; }
    public bool StencilReadOnly { get; set; }
}

public partial struct GPURenderPassColorAttachment()
{
    public GPUColor ClearValue { get; set; }
    public uint DepthSlice { get; set; }
    public GPULoadOp LoadOp { get; set; }
    public IGPUTextureView ResolveTarget { get; set; }
    public GPUStoreOp StoreOp { get; set; }
    public IGPUTextureView View { get; set; }
}

public partial struct GPURenderPassDepthStencilAttachment()
{
    public float DepthClearValue { get; set; }
    public GPULoadOp DepthLoadOp { get; set; }
    public bool DepthReadOnly { get; set; }
    public GPUStoreOp DepthStoreOp { get; set; }
    public uint StencilClearValue { get; set; }
    public GPULoadOp StencilLoadOp { get; set; }
    public bool StencilReadOnly { get; set; }
    public GPUStoreOp StencilStoreOp { get; set; }
    public IGPUTextureView View { get; set; }
}

public partial struct GPURenderPassDescriptor()
{
    public string Label { get; set; }
    public ReadOnlyMemory<GPURenderPassColorAttachment> ColorAttachments { get; set; }
    public GPURenderPassDepthStencilAttachment? DepthStencilAttachment { get; set; }
    public ulong MaxDrawCount { get; set; }
    public IGPUQuerySet OcclusionQuerySet { get; set; }
    public GPURenderPassTimestampWrites TimestampWrites { get; set; }
}

public partial struct GPURenderPassLayout()
{
    public ReadOnlyMemory<GPUTextureFormat?> ColorFormats { get; set; }
    public GPUTextureFormat DepthStencilFormat { get; set; }
    public uint SampleCount { get; set; }
}

public partial struct GPURenderPassTimestampWrites()
{
    public uint BeginningOfPassWriteIndex { get; set; }
    public uint EndOfPassWriteIndex { get; set; }
    public IGPUQuerySet QuerySet { get; set; }
}

public partial struct GPURenderPipelineDescriptor()
{
    public string Label { get; set; }
    public IGPUPipelineLayout? Layout { get; set; }
    public GPUDepthStencilState? DepthStencil { get; set; }
    public GPUFragmentState? Fragment { get; set; }
    public GPUMultisampleState Multisample { get; set; }
    public GPUPrimitiveState Primitive { get; set; }
    public GPUVertexState Vertex { get; set; }
}

//public partial struct GPURequestAdapterOptions()
//{
//    public bool ForceFallbackAdapter { get; set; }
//    public GPUPowerPreference PowerPreference { get; set; }
//}

public partial struct GPUSamplerBindingLayout()
{
    public GPUSamplerBindingType Type { get; set; }
}

public partial struct GPUSamplerDescriptor()
{
    public string Label { get; set; }
    public GPUAddressMode AddressModeU { get; set; }
    public GPUAddressMode AddressModeV { get; set; }
    public GPUAddressMode AddressModeW { get; set; }
    public GPUCompareFunction Compare { get; set; }
    public float LodMaxClamp { get; set; }
    public float LodMinClamp { get; set; }
    public GPUFilterMode MagFilter { get; set; }
    public ushort MaxAnisotropy { get; set; }
    public GPUFilterMode MinFilter { get; set; }
    public GPUMipmapFilterMode MipmapFilter { get; set; }
}

public partial struct GPUShaderModuleCompilationHint()
{
    public string EntryPoint { get; set; }
    public IGPUPipelineLayout Layout { get; set; }
}

public partial struct GPUShaderModuleDescriptor()
{
    public string Label { get; set; }
    public string Code { get; set; }
    public ReadOnlyMemory<GPUShaderModuleCompilationHint> CompilationHints { get; set; }
}

public partial struct GPUStencilFaceState()
{
    public GPUCompareFunction Compare { get; set; }
    public GPUStencilOperation DepthFailOp { get; set; }
    public GPUStencilOperation FailOp { get; set; }
    public GPUStencilOperation PassOp { get; set; }
}

public partial struct GPUStorageTextureBindingLayout()
{
    public GPUStorageTextureAccess Access { get; set; }
    public GPUTextureFormat Format { get; set; }
    public GPUTextureViewDimension ViewDimension { get; set; }
}

public partial struct GPUSurfaceConfiguration()
{
    public int Width { get; set; }
    public int Height { get; set; }
    public GPUCompositeAlphaMode AlphaMode { get; set; }
    public IGPUDevice Device { get; set; }
    public GPUTextureFormat Format { get; set; }
    public GPUTextureUsage Usage { get; set; }
    public GPUPresentMode PresentMode { get; set; }
    public IReadOnlyList<GPUTextureFormat> ViewFormats { get; set; }
}

public partial struct GPUTextureBindingLayout()
{
    public bool Multisampled { get; set; }
    public GPUTextureSampleType SampleType { get; set; }
    public GPUTextureViewDimension ViewDimension { get; set; }
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
    public string Label { get; set; }
    public uint ArrayLayerCount { get; set; }
    public GPUTextureAspect Aspect { get; set; }
    public uint BaseArrayLayer { get; set; }
    public uint BaseMipLevel { get; set; }
    public GPUTextureViewDimension Dimension { get; set; }
    public GPUTextureFormat Format { get; set; }
    public uint MipLevelCount { get; set; }
}

public partial struct GPUUncapturedErrorEventInit()
{
}

public partial struct GPUVertexAttribute()
{
    public GPUVertexFormat Format { get; set; }
    public ulong Offset { get; set; }
    public int ShaderLocation { get; set; }
}

public partial struct GPUVertexBufferLayout()
{
    public ulong ArrayStride { get; set; }
    public ReadOnlyMemory<GPUVertexAttribute> Attributes { get; set; }
    public GPUVertexStepMode StepMode { get; set; }
}

//public partial struct GPUVertexState()
//{
//    public ReadOnlyMemory<GPUVertexBufferLayout?> Buffers { get; set; }
//}

