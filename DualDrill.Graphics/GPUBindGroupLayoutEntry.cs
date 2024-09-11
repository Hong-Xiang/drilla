namespace DualDrill.Graphics;

public partial struct GPUBindGroupLayoutEntry
{
    public GPUShaderStage Visibility { get; set; }
    public int Binding { get; set; }
    public GPUBufferBindingLayout Buffer { get; set; }
    public GPUSamplerBindingLayout Sampler { get; set; }
    public GPUTextureBindingLayout Texture { get; set; }
    public GPUStorageTextureBindingLayout StorageTexture { get; set; }
}
//public partial struct GPUVertexBufferLayout
//{
//    public ulong ArrayStride { get; set; }
//    public GPUVertexStepMode StepMode { get; set; }
//    public ReadOnlyMemory<GPUVertexAttribute> Attributes { get; set; }
//}
//public partial struct GPUImageCopyBuffer
//{
//    public GPUTextureDataLayout Layout { get; set; }
//    public GPUBuffer Buffer { get; set; }
//}
//public partial struct GPUImageCopyTexture
//{
//    public int MipLevel { get; set; }
//    public GPUOrigin3D Origin { get; set; }
//    public GPUTextureAspect Aspect { get; set; }
//    public GPUTexture Texture { get; set; }
//}
//public partial struct GPUCommandBufferDescriptor { public string? Label { get; set; } }
//public partial struct GPUCommandEncoderDescriptor { public string? Label { get; set; } }
//public partial struct GPUComputePassTimestampWrites
//{
//    public GPUQuerySet QuerySet { get; set; }
//    public int BeginningOfPassWriteIndex { get; set; }
//    public int EndOfPassWriteIndex { get; set; }
//}
//public partial struct GPUComputePassDescriptor
//{
//    public ReadOnlyMemory<GPUComputePassTimestampWrites> TimestampWrites { get; set; }
//    public string? Label { get; set; }
//}
//public partial struct GPURenderPassTimestampWrites
//{
//    public int BeginningOfPassWriteIndex { get; set; }
//    public int EndOfPassWriteIndex { get; set; }
//    public GPUQuerySet QuerySet { get; set; }
//}
//public partial struct GPURenderPassDescriptor
//{
//    public ReadOnlyMemory<GPURenderPassTimestampWrites> TimestampWrites { get; set; }
//    public ReadOnlyMemory<GPURenderPassColorAttachment> ColorAttachments { get; set; }
//    public string? Label { get; set; }
//    public GPURenderPassDepthStencilAttachment? DepthStencilAttachment { get; set; }
//    public GPUQuerySet OcclusionQuerySet { get; set; }
//}
//public partial struct GPURenderPassColorAttachment
//{
//    public GPULoadOp LoadOp { get; set; }
//    public GPUStoreOp StoreOp { get; set; }
//    public GPUColor ClearValue { get; set; }
//    public GPUTextureView? ResolveTarget { get; set; }
//    public GPUTextureView View { get; set; }
//}
//public partial struct GPURenderPassDepthStencilAttachment
//{
//    public int StencilClearValue { get; set; }
//    public bool DepthReadOnly { get; set; }
//    public bool StencilReadOnly { get; set; }
//    public float DepthClearValue { get; set; }
//    public GPULoadOp DepthLoadOp { get; set; }
//    public GPUStoreOp DepthStoreOp { get; set; }
//    public GPULoadOp StencilLoadOp { get; set; }
//    public GPUStoreOp StencilStoreOp { get; set; }
//    public GPUTextureView View { get; set; }
//}
//public partial struct GPURenderBundleDescriptor { public string? Label { get; set; } }
//public partial struct GPURenderBundleEncoderDescriptor
//{
//    public ReadOnlyMemory<GPUTextureFormat> ColorFormats { get; set; }
//    public int SampleCount { get; set; }
//    public bool DepthReadOnly { get; set; }
//    public bool StencilReadOnly { get; set; }
//    public string? Label { get; set; }
//    public GPUTextureFormat DepthStencilFormat { get; set; }
//}
//public partial struct GPUQueueDescriptor { public string? Label { get; set; } }
//public partial struct GPUQuerySetDescriptor
//{
//    public int Count { get; set; }
//    public string? Label { get; set; }
//    public GPUQueryType Type { get; set; }
//}