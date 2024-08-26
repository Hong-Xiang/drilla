using DualDrill.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DualDrill.Graphics.Interop;
using System.Collections.Immutable;
using DualDrill.Interop;


namespace DualDrill.Graphics;

public partial struct GPUAdapterProperties
{
    public int VendorID { get; set; }
    public int DeviceID { get; set; }
    public string VendorName { get; set; }
    public string Architecture { get; set; }
    public string Name { get; set; }
    public string DriverDescription { get; set; }
    public GPUAdapterType AdapterType { get; set; }
    public GPUBackendType BackendType { get; set; }
}
public partial struct GPUColor
{
    public double R { get; set; }
    public double G { get; set; }
    public double B { get; set; }
    public double A { get; set; }
}
public partial struct GPUCompilationMessage
{
    public string Message { get; set; }
    public ulong LineNum { get; set; }
    public ulong LinePos { get; set; }
    public ulong Offset { get; set; }
    public ulong Length { get; set; }
    public ulong Utf16LinePos { get; set; }
    public ulong Utf16Offset { get; set; }
    public ulong Utf16Length { get; set; }
    public GPUCompilationMessageType Type { get; set; }
}
public partial struct GPUConstantEntry
{
    public double Value { get; set; }
    public string Key { get; set; }
}
public partial struct GPUExtent3D
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int DepthOrArrayLayers { get; set; }
}
public partial struct GPULimits
{
    public int MinStorageBufferOffsetAlignment { get; set; }
    public int MaxVertexBufferArrayStride { get; set; }
    public int MaxInterStageShaderComponents { get; set; }
    public int MaxInterStageShaderVariables { get; set; }
    public int MaxColorAttachmentBytesPerSample { get; set; }
    public int MaxComputeWorkgroupStorageSize { get; set; }
    public int MaxComputeInvocationsPerWorkgroup { get; set; }
    public int MaxComputeWorkgroupSizeX { get; set; }
    public int MaxComputeWorkgroupSizeY { get; set; }
    public int MaxComputeWorkgroupSizeZ { get; set; }
    public int MaxComputeWorkgroupsPerDimension { get; set; }
    public int MaxTextureDimension1D { get; set; }
    public int MaxTextureDimension2D { get; set; }
    public int MaxTextureDimension3D { get; set; }
    public int MaxTextureArrayLayers { get; set; }
    public int MaxBindGroups { get; set; }
    public int MaxBindGroupsPlusVertexBuffers { get; set; }
    public int MaxBindingsPerBindGroup { get; set; }
    public int MaxDynamicUniformBuffersPerPipelineLayout { get; set; }
    public int MaxDynamicStorageBuffersPerPipelineLayout { get; set; }
    public int MaxSampledTexturesPerShaderStage { get; set; }
    public ulong MaxUniformBufferBindingSize { get; set; }
    public ulong MaxStorageBufferBindingSize { get; set; }
    public ulong MaxBufferSize { get; set; }
    public int MaxSamplersPerShaderStage { get; set; }
    public int MaxStorageBuffersPerShaderStage { get; set; }
    public int MaxStorageTexturesPerShaderStage { get; set; }
    public int MaxUniformBuffersPerShaderStage { get; set; }
    public int MinUniformBufferOffsetAlignment { get; set; }
    public int MaxVertexBuffers { get; set; }
    public int MaxVertexAttributes { get; set; }
    public int MaxColorAttachments { get; set; }
}
public partial struct GPUOrigin3D
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}
public partial struct GPUPrimitiveDepthClipControl
{
    public bool UnclippedDepth { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPURenderPassDescriptorMaxDrawCount
{
    public ulong MaxDrawCount { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUShaderModuleWGSLDescriptor
{
    public string Code { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceCapabilities
{
    public ReadOnlyMemory<GPUTextureFormat> Formats { get; set; }
    public ReadOnlyMemory<GPUPresentMode> PresentModes { get; set; }
    public ReadOnlyMemory<GPUCompositeAlphaMode> AlphaModes { get; set; }
}
public ref struct GPUSurfaceConfiguration
{
    public GPUDevice Device { get; set; }
    public GPUTextureUsage Usage { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public GPUTextureFormat Format { get; set; }
    public GPUCompositeAlphaMode AlphaMode { get; set; }
    public GPUPresentMode PresentMode { get; set; }
    public ReadOnlySpan<GPUTextureFormat> ViewFormats { get; set; }
}
public partial struct GPUSurfaceDescriptor { public string? Label { get; set; } }
public partial struct GPUSurfaceDescriptorFromAndroidNativeWindow
{
    public nint Window { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceDescriptorFromCanvasHTMLSelector
{
    public string Selector { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceDescriptorFromMetalLayer
{
    public nint Layer { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceDescriptorFromWaylandSurface
{
    public nint Display { get; set; }
    public nint Surface { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceDescriptorFromWindowsHWND
{
    public nint Hinstance { get; set; }
    public nint Hwnd { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceDescriptorFromXcbWindow
{
    public int Window { get; set; }
    public nint Connection { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceDescriptorFromXlibWindow
{
    public ulong Window { get; set; }
    public nint Display { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceTexture
{
    public bool Suboptimal { get; set; }
    public GPUSurfaceGetCurrentTextureStatus Status { get; set; }
    public GPUTexture Texture { get; set; }
}
public partial struct GPUTextureDataLayout
{
    public int BytesPerRow { get; set; }
    public int RowsPerImage { get; set; }
    public ulong Offset { get; set; }
}
public partial struct GPUCompilationInfo { public ReadOnlyMemory<GPUCompilationMessage> Messages { get; set; } }
public partial struct GPUProgrammableStageDescriptor
{
    public GPUShaderModule Module { get; set; }
    public string EntryPoint { get; set; }
    public ReadOnlyMemory<GPUConstantEntry> Constants { get; set; }
}
public partial struct GPURequiredLimits { public GPULimits Limits { get; set; } }
public partial struct GPUSupportedLimits { public GPULimits Limits { get; set; } }
public partial struct GPUInstanceExtras
{
    public uint Backends { get; set; }
    public uint Flags { get; set; }
    public string DxilPath { get; set; }
    public string DxcPath { get; set; }
    public GPUChainedStruct Chain { get; set; }
    public WGPUDx12Compiler Dx12ShaderCompiler { get; set; }
    public WGPUGles3MinorVersion Gles3MinorVersion { get; set; }
}
public partial struct GPUDeviceExtras
{
    public string TracePath { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUNativeLimits
{
    public int MaxPushConstantSize { get; set; }
    public int MaxNonSamplerBindings { get; set; }
}
public partial struct GPURequiredLimitsExtras
{
    public GPUChainedStruct Chain { get; set; }
    public GPUNativeLimits Limits { get; set; }
}
public partial struct GPUSupportedLimitsExtras
{
    public GPUChainedStructOut Chain { get; set; }
    public GPUNativeLimits Limits { get; set; }
}
public partial struct GPUPushConstantRange
{
    public uint Stages { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
}
public partial struct GPUPipelineLayoutExtras
{
    public ReadOnlyMemory<GPUPushConstantRange> PushConstantRanges { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUWrappedSubmissionIndex
{
    public ulong SubmissionIndex { get; set; }
    public GPUQueue Queue { get; set; }
}
public partial struct GPUShaderDefine
{
    public string Name { get; set; }
    public string Value { get; set; }
}
public partial struct GPUShaderModuleGLSLDescriptor
{
    public ReadOnlyMemory<GPUShaderDefine> Defines { get; set; }
    public int DefineCount { get; set; }
    public string Code { get; set; }
    public GPUChainedStruct Chain { get; set; }
    public GPUShaderStage Stage { get; set; }
}
public partial struct GPURegistryReport
{
    public nuint NumAllocated { get; set; }
    public nuint NumKeptFromUser { get; set; }
    public nuint NumReleasedFromUser { get; set; }
    public nuint NumError { get; set; }
    public nuint ElementSize { get; set; }
}
public partial struct GPUHubReport
{
    public GPURegistryReport QuerySets { get; set; }
    public GPURegistryReport Buffers { get; set; }
    public GPURegistryReport Textures { get; set; }
    public GPURegistryReport Adapters { get; set; }
    public GPURegistryReport Devices { get; set; }
    public GPURegistryReport Queues { get; set; }
    public GPURegistryReport PipelineLayouts { get; set; }
    public GPURegistryReport ShaderModules { get; set; }
    public GPURegistryReport BindGroupLayouts { get; set; }
    public GPURegistryReport BindGroups { get; set; }
    public GPURegistryReport CommandBuffers { get; set; }
    public GPURegistryReport RenderBundles { get; set; }
    public GPURegistryReport RenderPipelines { get; set; }
    public GPURegistryReport ComputePipelines { get; set; }
    public GPURegistryReport TextureViews { get; set; }
    public GPURegistryReport Samplers { get; set; }
}
public partial struct GPUGlobalReport
{
    public GPURegistryReport Surfaces { get; set; }
    public GPUBackendType BackendType { get; set; }
    public GPUHubReport Vulkan { get; set; }
    public GPUHubReport Metal { get; set; }
    public GPUHubReport Dx12 { get; set; }
    public GPUHubReport Gl { get; set; }
}
public partial struct GPUInstanceEnumerateAdapterOptions { public uint Backends { get; set; } }
public partial struct GPUBindGroupEntryExtras
{
    public ReadOnlyMemory<GPUBuffer> Buffers { get; set; }
    public ReadOnlyMemory<GPUSampler> Samplers { get; set; }
    public ReadOnlyMemory<GPUTextureView> TextureViews { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUBindGroupLayoutEntryExtras
{
    public int Count { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUQuerySetDescriptorExtras
{
    public ReadOnlyMemory<WGPUPipelineStatisticName> PipelineStatistics { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPUSurfaceConfigurationExtras
{
    public bool DesiredMaximumFrameLatency { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public partial struct GPURequestAdapterOptions
{
    public bool ForceFallbackAdapter { get; set; }
    public GPUPowerPreference PowerPreference { get; set; }
    public GPUBackendType BackendType { get; set; }
    public GPUSurface CompatibleSurface { get; set; }
}
public partial struct GPUBufferDescriptor
{
    public GPUBufferUsage Usage { get; set; }
    public ulong Size { get; set; }
    public GPUBool MappedAtCreation { get; set; }
    public string? Label { get; set; }
}
public partial struct GPUTextureDescriptor()
{
    public GPUTextureUsage Usage { get; set; }
    public int MipLevelCount { get; set; } = 1;
    public int SampleCount { get; set; } = 1;
    public string? Label { get; set; }
    public GPUTextureDimension Dimension { get; set; } = GPUTextureDimension._2D;
    public GPUExtent3D Size { get; set; }
    public GPUTextureFormat Format { get; set; }
    public ReadOnlyMemory<GPUTextureFormat> ViewFormats { get; set; }
}
public partial struct GPUTextureViewDescriptor()
{
    public int BaseMipLevel { get; set; } = 0;
    public required int MipLevelCount { get; set; }
    public required int BaseArrayLayer { get; set; }
    public int ArrayLayerCount { get; set; } = 0;
    public string? Label { get; set; }
    public required GPUTextureFormat Format { get; set; }
    public required GPUTextureViewDimension Dimension { get; set; }
    public GPUTextureAspect Aspect { get; set; } = GPUTextureAspect.All;
}
public partial struct GPUSamplerDescriptor
{
    public float LodMinClamp { get; set; }
    public float LodMaxClamp { get; set; }
    public ushort MaxAnisotropy { get; set; }
    public string? Label { get; set; }
    public GPUAddressMode AddressModeU { get; set; }
    public GPUAddressMode AddressModeV { get; set; }
    public GPUAddressMode AddressModeW { get; set; }
    public GPUFilterMode MagFilter { get; set; }
    public GPUFilterMode MinFilter { get; set; }
    public GPUMipmapFilterMode MipmapFilter { get; set; }
    public GPUCompareFunction Compare { get; set; }
}
public partial struct GPUBindGroupLayoutDescriptor
{
    public ReadOnlyMemory<GPUBindGroupLayoutEntry> Entries { get; set; }
    public string? Label { get; set; }
}
public unsafe partial struct GPUDeviceDescriptor
{
    public ReadOnlyMemory<GPUFeatureName> RequiredFeatures { get; set; }
    public delegate* unmanaged[Cdecl]<GPUDeviceLostReason, sbyte*, void*, void> DeviceLostCallback { get; set; }
    public nint DeviceLostUserdata { get; set; }
    public ReadOnlyMemory<GPURequiredLimits> RequiredLimits { get; set; }
    public string? Label { get; set; }
    public GPUQueueDescriptor DefaultQueue { get; set; }
}
public unsafe partial struct GPUShaderModuleSPIRVDescriptor
{
    public int CodeSize { get; set; }
    public uint* Code { get; set; }
    public GPUChainedStruct Chain { get; set; }
}
public unsafe partial struct GPUChainedStruct
{
    public GPUChainedStruct* Next { get; set; }
    public WGPUSType SType { get; set; }
}
public unsafe partial struct GPUChainedStructOut
{
    public GPUChainedStructOut* Next { get; set; }
    public WGPUSType SType { get; set; }
}
public partial struct GPUBindGroupLayoutEntry
{
    public GPUShaderStage Visibility { get; set; }
    public int Binding { get; set; }
    public GPUBufferBindingLayout Buffer { get; set; }
    public GPUSamplerBindingLayout Sampler { get; set; }
    public GPUTextureBindingLayout Texture { get; set; }
    public GPUStorageTextureBindingLayout StorageTexture { get; set; }
}
public partial struct GPUBufferBindingLayout
{
    public ulong MinBindingSize { get; set; }
    public bool HasDynamicOffset { get; set; }
    public GPUBufferBindingType Type { get; set; }

    public static implicit operator WGPUBufferBindingLayout(GPUBufferBindingLayout layout)
    {
        return new WGPUBufferBindingLayout
        {
            type = layout.Type,
            minBindingSize = layout.MinBindingSize,
            hasDynamicOffset = layout.HasDynamicOffset ? 1u : 0
        };
    }
}
public partial struct GPUSamplerBindingLayout { public GPUSamplerBindingType Type { get; set; } }
public partial struct GPUTextureBindingLayout
{
    public bool Multisampled { get; set; }
    public GPUTextureSampleType SampleType { get; set; }
    public GPUTextureViewDimension ViewDimension { get; set; }
}
public partial struct GPUStorageTextureBindingLayout
{
    public GPUStorageTextureAccess Access { get; set; }
    public GPUTextureFormat Format { get; set; }
    public GPUTextureViewDimension ViewDimension { get; set; }
}
public partial struct GPUBindGroupDescriptor
{
    public string? Label { get; set; }
    public GPUBindGroupLayout Layout { get; set; }
    public ReadOnlyMemory<GPUBindGroupEntry> Entries { get; set; }
}
public partial struct GPUBindGroupEntry
{
    public ulong Offset { get; set; }
    public ulong Size { get; set; }
    public int Binding { get; set; }
    public GPUTextureView? TextureView { get; set; }
    public GPUBuffer? Buffer { get; set; }
    public GPUSampler? Sampler { get; set; }
}
public partial struct GPUPipelineLayoutDescriptor
{
    public ReadOnlyMemory<GPUBindGroupLayout> BindGroupLayouts { get; set; }
    public string? Label { get; set; }
}
public partial struct GPUShaderModuleDescriptor
{
    public ReadOnlyMemory<GPUShaderModuleCompilationHint> Hints { get; set; }
    public string? Label { get; set; }
}
public partial struct GPUShaderModuleCompilationHint
{
    public GPUPipelineLayout Layout { get; set; }
    public string EntryPoint { get; set; }
}
public partial struct GPUComputePipelineDescriptor
{
    public GPUPipelineLayout Layout { get; set; }
    public GPUProgrammableStageDescriptor Compute { get; set; }
    public string? Label { get; set; }
}
public partial struct GPURenderPipelineDescriptor
{
    public GPUPipelineLayout? Layout { get; set; }
    public GPUVertexState Vertex { get; set; }
    public GPUPrimitiveState Primitive { get; set; }
    public string? Label { get; set; }
    public GPUMultisampleState Multisample { get; set; }
    public GPUDepthStencilState? DepthStencil { get; set; }
    public GPUFragmentState? Fragment { get; set; }
}
public partial struct GPUPrimitiveState
{
    public GPUPrimitiveTopology Topology { get; set; }
    public GPUIndexFormat StripIndexFormat { get; set; }
    public GPUFrontFace FrontFace { get; set; }
    public GPUCullMode CullMode { get; set; }
}
public partial struct GPUMultisampleState
{
    public int Count { get; set; }
    public uint Mask { get; set; }
    public bool AlphaToCoverageEnabled { get; set; }
}
public partial struct GPUFragmentState
{
    public string EntryPoint { get; set; }
    public ReadOnlyMemory<GPUConstantEntry> Constants { get; set; }
    public ReadOnlyMemory<GPUColorTargetState> Targets { get; set; }
    public GPUShaderModule Module { get; set; }
}
public partial struct GPUColorTargetState
{
    public uint WriteMask { get; set; }
    public GPUTextureFormat Format { get; set; }
    public GPUBlendState? Blend { get; set; }
}
public partial struct GPUBlendState
{
    public GPUBlendComponent Color { get; set; }
    public GPUBlendComponent Alpha { get; set; }
}
public partial struct GPUBlendComponent
{
    public GPUBlendOperation Operation { get; set; }
    public GPUBlendFactor SrcFactor { get; set; }
    public GPUBlendFactor DstFactor { get; set; }
}
public partial struct GPUDepthStencilState
{
    public uint StencilReadMask { get; set; }
    public uint StencilWriteMask { get; set; }
    public int DepthBias { get; set; }
    public float DepthBiasSlopeScale { get; set; }
    public float DepthBiasClamp { get; set; }
    public bool DepthWriteEnabled { get; set; }
    public GPUTextureFormat Format { get; set; }
    public GPUCompareFunction DepthCompare { get; set; }
    public GPUStencilFaceState StencilFront { get; set; }
    public GPUStencilFaceState StencilBack { get; set; }
}
public partial struct GPUStencilFaceState
{
    public GPUCompareFunction Compare { get; set; }
    public GPUStencilOperation FailOp { get; set; }
    public GPUStencilOperation DepthFailOp { get; set; }
    public GPUStencilOperation PassOp { get; set; }
}
public partial struct GPUVertexState
{
    public Utf8String EntryPoint { get; set; }
    public GPUShaderModule Module { get; set; }
    public ReadOnlyMemory<GPUConstantEntry> Constants { get; set; }
    public ReadOnlyMemory<GPUVertexBufferLayout> Buffers { get; set; }
}
public partial struct GPUVertexBufferLayout
{
    public ulong ArrayStride { get; set; }
    public GPUVertexStepMode StepMode { get; set; }
    public ReadOnlyMemory<GPUVertexAttribute> Attributes { get; set; }
}
public partial struct GPUImageCopyBuffer
{
    public GPUTextureDataLayout Layout { get; set; }
    public GPUBuffer Buffer { get; set; }
}
public partial struct GPUImageCopyTexture
{
    public int MipLevel { get; set; }
    public GPUOrigin3D Origin { get; set; }
    public GPUTextureAspect Aspect { get; set; }
    public GPUTexture Texture { get; set; }
}
public partial struct GPUCommandBufferDescriptor { public string? Label { get; set; } }
public partial struct GPUCommandEncoderDescriptor { public string? Label { get; set; } }
public partial struct GPUComputePassTimestampWrites
{
    public GPUQuerySet QuerySet { get; set; }
    public int BeginningOfPassWriteIndex { get; set; }
    public int EndOfPassWriteIndex { get; set; }
}
public partial struct GPUComputePassDescriptor
{
    public ReadOnlyMemory<GPUComputePassTimestampWrites> TimestampWrites { get; set; }
    public string? Label { get; set; }
}
public partial struct GPURenderPassTimestampWrites
{
    public int BeginningOfPassWriteIndex { get; set; }
    public int EndOfPassWriteIndex { get; set; }
    public GPUQuerySet QuerySet { get; set; }
}
public partial struct GPURenderPassDescriptor
{
    public ReadOnlyMemory<GPURenderPassTimestampWrites> TimestampWrites { get; set; }
    public ReadOnlyMemory<GPURenderPassColorAttachment> ColorAttachments { get; set; }
    public string? Label { get; set; }
    public GPURenderPassDepthStencilAttachment? DepthStencilAttachment { get; set; }
    public GPUQuerySet OcclusionQuerySet { get; set; }
}
public partial struct GPURenderPassColorAttachment
{
    public GPULoadOp LoadOp { get; set; }
    public GPUStoreOp StoreOp { get; set; }
    public GPUColor ClearValue { get; set; }
    public GPUTextureView? ResolveTarget { get; set; }
    public GPUTextureView View { get; set; }
}
public partial struct GPURenderPassDepthStencilAttachment
{
    public int StencilClearValue { get; set; }
    public bool DepthReadOnly { get; set; }
    public bool StencilReadOnly { get; set; }
    public float DepthClearValue { get; set; }
    public GPULoadOp DepthLoadOp { get; set; }
    public GPUStoreOp DepthStoreOp { get; set; }
    public GPULoadOp StencilLoadOp { get; set; }
    public GPUStoreOp StencilStoreOp { get; set; }
    public GPUTextureView View { get; set; }
}
public partial struct GPURenderBundleDescriptor { public string? Label { get; set; } }
public partial struct GPURenderBundleEncoderDescriptor
{
    public ReadOnlyMemory<GPUTextureFormat> ColorFormats { get; set; }
    public int SampleCount { get; set; }
    public bool DepthReadOnly { get; set; }
    public bool StencilReadOnly { get; set; }
    public string? Label { get; set; }
    public GPUTextureFormat DepthStencilFormat { get; set; }
}
public partial struct GPUQueueDescriptor { public string? Label { get; set; } }
public partial struct GPUQuerySetDescriptor
{
    public int Count { get; set; }
    public string? Label { get; set; }
    public GPUQueryType Type { get; set; }
}