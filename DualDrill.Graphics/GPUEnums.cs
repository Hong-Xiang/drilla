﻿namespace DualDrill.Graphics;

public enum GPUAdapterType : uint
{
    DiscreteGPU = 0x00000000,
    IntegratedGPU = 0x00000001,
    CPU = 0x00000002,
    Unknown = 0x00000003,
}

public enum GPUAddressMode : uint
{
    Repeat = 0x00000000,
    MirrorRepeat = 0x00000001,
    ClampToEdge = 0x00000002,
}

public enum GPUBlendFactor : uint
{
    Zero = 0x00000000,
    One = 0x00000001,
    Src = 0x00000002,
    OneMinusSrc = 0x00000003,
    SrcAlpha = 0x00000004,
    OneMinusSrcAlpha = 0x00000005,
    Dst = 0x00000006,
    OneMinusDst = 0x00000007,
    DstAlpha = 0x00000008,
    OneMinusDstAlpha = 0x00000009,
    SrcAlphaSaturated = 0x0000000A,
    Constant = 0x0000000B,
    OneMinusConstant = 0x0000000C,
}

public enum GPUBlendOperation : uint
{
    Add = 0x00000000,
    Subtract = 0x00000001,
    ReverseSubtract = 0x00000002,
    Min = 0x00000003,
    Max = 0x00000004,
}

public enum GPUBufferBindingType : uint
{
    Undefined = 0x00000000,
    Uniform = 0x00000001,
    Storage = 0x00000002,
    ReadOnlyStorage = 0x00000003,
}

public enum GPUBufferMapAsyncStatus : uint
{
    Success = 0x00000000,
    ValidationError = 0x00000001,
    Unknown = 0x00000002,
    DeviceLost = 0x00000003,
    DestroyedBeforeCallback = 0x00000004,
    UnmappedBeforeCallback = 0x00000005,
    MappingAlreadyPending = 0x00000006,
    OffsetOutOfRange = 0x00000007,
    SizeOutOfRange = 0x00000008,
}

public enum GPUBufferMapState : uint
{
    Unmapped = 0x00000000,
    Pending = 0x00000001,
    Mapped = 0x00000002,
}

public enum GPUCompareFunction : uint
{
    Undefined = 0x00000000,
    Never = 0x00000001,
    Less = 0x00000002,
    LessEqual = 0x00000003,
    Greater = 0x00000004,
    GreaterEqual = 0x00000005,
    Equal = 0x00000006,
    NotEqual = 0x00000007,
    Always = 0x00000008,
}

public enum GPUCompilationInfoRequestStatus : uint
{
    Success = 0x00000000,
    Error = 0x00000001,
    DeviceLost = 0x00000002,
    Unknown = 0x00000003,
}

public enum GPUCompilationMessageType : uint
{
    Error = 0x00000000,
    Warning = 0x00000001,
    Info = 0x00000002,
}

public enum GPUCompositeAlphaMode : uint
{
    Auto = 0x00000000,
    Opaque = 0x00000001,
    Premultiplied = 0x00000002,
    Unpremultiplied = 0x00000003,
    Inherit = 0x00000004,
}

public enum GPUCreatePipelineAsyncStatus : uint
{
    Success = 0x00000000,
    ValidationError = 0x00000001,
    InternalError = 0x00000002,
    DeviceLost = 0x00000003,
    DeviceDestroyed = 0x00000004,
    Unknown = 0x00000005,
}

public enum GPUCullMode : uint
{
    None = 0x00000000,
    Front = 0x00000001,
    Back = 0x00000002,
}

public enum GPUDeviceLostReason : uint
{
    Undefined = 0x00000000,
    Destroyed = 0x00000001,
}

public enum GPUErrorFilter : uint
{
    Validation = 0x00000000,
    OutOfMemory = 0x00000001,
    Internal = 0x00000002,
}

public enum GPUErrorType : uint
{
    NoError = 0x00000000,
    Validation = 0x00000001,
    OutOfMemory = 0x00000002,
    Internal = 0x00000003,
    Unknown = 0x00000004,
    DeviceLost = 0x00000005,
}

public enum GPUFeatureName : uint
{
    Undefined = 0x00000000,
    DepthClipControl = 0x00000001,
    Depth32FloatStencil8 = 0x00000002,
    TimestampQuery = 0x00000003,
    TextureCompressionBC = 0x00000004,
    TextureCompressionETC2 = 0x00000005,
    TextureCompressionASTC = 0x00000006,
    IndirectFirstInstance = 0x00000007,
    ShaderF16 = 0x00000008,
    RG11B10UfloatRenderable = 0x00000009,
    BGRA8UnormStorage = 0x0000000A,
    Float32Filterable = 0x0000000B,
}

public enum GPUFilterMode : uint
{
    Nearest = 0x00000000,
    Linear = 0x00000001,
}

public enum GPUFrontFace : uint
{
    CCW = 0x00000000,
    CW = 0x00000001,
}

public enum GPUIndexFormat : uint
{
    Undefined = 0x00000000,
    Uint16 = 0x00000001,
    Uint32 = 0x00000002,
}

public enum GPULoadOp : uint
{
    Undefined = 0x00000000,
    Clear = 0x00000001,
    Load = 0x00000002,
}

public enum GPUMipmapFilterMode : uint
{
    Nearest = 0x00000000,
    Linear = 0x00000001,
}

public enum GPUPresentMode : uint
{
    Fifo = 0x00000000,
    FifoRelaxed = 0x00000001,
    Immediate = 0x00000002,
    Mailbox = 0x00000003,
}

public enum GPUPrimitiveTopology : uint
{
    PointList = 0x00000000,
    LineList = 0x00000001,
    LineStrip = 0x00000002,
    TriangleList = 0x00000003,
    TriangleStrip = 0x00000004,
}

public enum GPUQueryType : uint
{
    Occlusion = 0x00000000,
    Timestamp = 0x00000001,
}

public enum GPUQueueWorkDoneStatus : uint
{
    Success = 0x00000000,
    Error = 0x00000001,
    Unknown = 0x00000002,
    DeviceLost = 0x00000003,
}

public enum GPURequestAdapterStatus : uint
{
    Success = 0x00000000,
    Unavailable = 0x00000001,
    Error = 0x00000002,
    Unknown = 0x00000003,
}

public enum GPURequestDeviceStatus : uint
{
    Success = 0x00000000,
    Error = 0x00000001,
    Unknown = 0x00000002,
}

public enum WGPUSType : uint
{
    Invalid = 0x00000000,
    SurfaceDescriptorFromMetalLayer = 0x00000001,
    SurfaceDescriptorFromWindowsHWND = 0x00000002,
    SurfaceDescriptorFromXlibWindow = 0x00000003,
    SurfaceDescriptorFromCanvasHTMLSelector = 0x00000004,
    ShaderModuleSPIRVDescriptor = 0x00000005,
    ShaderModuleWGSLDescriptor = 0x00000006,
    PrimitiveDepthClipControl = 0x00000007,
    SurfaceDescriptorFromWaylandSurface = 0x00000008,
    SurfaceDescriptorFromAndroidNativeWindow = 0x00000009,
    SurfaceDescriptorFromXcbWindow = 0x0000000A,
    RenderPassDescriptorMaxDrawCount = 0x0000000F,
}

public enum GPUSamplerBindingType : uint
{
    Undefined = 0x00000000,
    Filtering = 0x00000001,
    NonFiltering = 0x00000002,
    Comparison = 0x00000003,
}

public enum GPUStencilOperation : uint
{
    Keep = 0x00000000,
    Zero = 0x00000001,
    Replace = 0x00000002,
    Invert = 0x00000003,
    IncrementClamp = 0x00000004,
    DecrementClamp = 0x00000005,
    IncrementWrap = 0x00000006,
    DecrementWrap = 0x00000007,
}

public enum GPUStorageTextureAccess : uint
{
    Undefined = 0x00000000,
    WriteOnly = 0x00000001,
    ReadOnly = 0x00000002,
    ReadWrite = 0x00000003,
}

public enum GPUStoreOp : uint
{
    Undefined = 0x00000000,
    Store = 0x00000001,
    Discard = 0x00000002,
}

public enum GPUSurfaceGetCurrentTextureStatus : uint
{
    Success = 0x00000000,
    Timeout = 0x00000001,
    Outdated = 0x00000002,
    Lost = 0x00000003,
    OutOfMemory = 0x00000004,
    DeviceLost = 0x00000005,
}

public enum GPUTextureAspect : uint
{
    All = 0x00000000,
    StencilOnly = 0x00000001,
    DepthOnly = 0x00000002,
}

public enum GPUTextureDimension : uint
{
    _1D = 0x00000000,
    _2D = 0x00000001,
    _3D = 0x00000002,
}

public enum GPUTextureSampleType : uint
{
    Undefined = 0x00000000,
    Float = 0x00000001,
    UnfilterableFloat = 0x00000002,
    Depth = 0x00000003,
    Sint = 0x00000004,
    Uint = 0x00000005,
}

public enum GPUTextureViewDimension : uint
{
    Undefined = 0x00000000,
    _1D = 0x00000001,
    _2D = 0x00000002,
    _2DArray = 0x00000003,
    Cube = 0x00000004,
    CubeArray = 0x00000005,
    _3D = 0x00000006,
}

public enum GPUVertexFormat : uint
{
    Undefined = 0x00000000,
    Uint8x2 = 0x00000001,
    Uint8x4 = 0x00000002,
    Sint8x2 = 0x00000003,
    Sint8x4 = 0x00000004,
    Unorm8x2 = 0x00000005,
    Unorm8x4 = 0x00000006,
    Snorm8x2 = 0x00000007,
    Snorm8x4 = 0x00000008,
    Uint16x2 = 0x00000009,
    Uint16x4 = 0x0000000A,
    Sint16x2 = 0x0000000B,
    Sint16x4 = 0x0000000C,
    Unorm16x2 = 0x0000000D,
    Unorm16x4 = 0x0000000E,
    Snorm16x2 = 0x0000000F,
    Snorm16x4 = 0x00000010,
    Float16x2 = 0x00000011,
    Float16x4 = 0x00000012,
    Float32 = 0x00000013,
    Float32x2 = 0x00000014,
    Float32x3 = 0x00000015,
    Float32x4 = 0x00000016,
    Uint32 = 0x00000017,
    Uint32x2 = 0x00000018,
    Uint32x3 = 0x00000019,
    Uint32x4 = 0x0000001A,
    Sint32 = 0x0000001B,
    Sint32x2 = 0x0000001C,
    Sint32x3 = 0x0000001D,
    Sint32x4 = 0x0000001E,
}

public enum GPUVertexStepMode : uint
{
    Vertex = 0x00000000,
    Instance = 0x00000001,
    VertexBufferNotUsed = 0x00000002,
}

[Flags]
public enum GPUBufferUsage : uint
{
    None = 0x00000000,
    MapRead = 0x00000001,
    MapWrite = 0x00000002,
    CopySrc = 0x00000004,
    CopyDst = 0x00000008,
    Index = 0x00000010,
    Vertex = 0x00000020,
    Uniform = 0x00000040,
    Storage = 0x00000080,
    Indirect = 0x00000100,
    QueryResolve = 0x00000200,
}

public enum GPUColorWriteMask : uint
{
    None = 0x00000000,
    Red = 0x00000001,
    Green = 0x00000002,
    Blue = 0x00000004,
    Alpha = 0x00000008,
    All = 0x0000000F,
}

[Flags]
public enum GPUMapMode : uint
{
    None = 0x00000000,
    Read = 0x00000001,
    Write = 0x00000002,
}

[Flags]
public enum GPUShaderStage : uint
{
    None = 0x00000000,
    Vertex = 0x00000001,
    Fragment = 0x00000002,
    Compute = 0x00000004,
}

[Flags]
public enum GPUTextureUsage : uint
{
    None = 0x00000000,
    CopySrc = 0x00000001,
    CopyDst = 0x00000002,
    TextureBinding = 0x00000004,
    StorageBinding = 0x00000008,
    RenderAttachment = 0x00000010,
}