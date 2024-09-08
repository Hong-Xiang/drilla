namespace DualDrill.Graphics;
[Flags]
public enum GPUBufferUsage : int
{
    None = 0x0,
    MapRead = 0x1,
    MapWrite = 0x2,
    CopySrc = 0x4,
    CopyDst = 0x8,
    Index = 0x10,
    Vertex = 0x20,
    Uniform = 0x40,
    Storage = 0x80,
    Indirect = 0x100,
    QueryResolve = 0x200,
}
[Flags]
public enum GPUColorWriteMask : int
{
    None = 0x0,
    Red = 0x1,
    Green = 0x2,
    Blue = 0x4,
    Alpha = 0x8,
    All = 0xF,
}
public enum GPUTextureAspect : int
{
    All = 0,
    StencilOnly = 1,
    DepthOnly = 2,
}
public enum GPUDeviceLostReason : int
{
    Undefined = 0,
    Destroyed = 1,
}
public enum GPUBlendOperation : int
{
    Add = 0,
    Subtract = 1,
    ReverseSubtract = 2,
    Min = 3,
    Max = 4,
}
public enum GPUCullMode : int
{
    None = 0,
    Front = 1,
    Back = 2,
}
public enum GPUStencilOperation : int
{
    Keep = 0,
    Zero = 1,
    Replace = 2,
    Invert = 3,
    IncrementClamp = 4,
    DecrementClamp = 5,
    IncrementWrap = 6,
    DecrementWrap = 7,
}
public enum GPUTextureDimension : int
{
    _1D = 0,
    _2D = 1,
    _3D = 2,
}
public enum GPUQueryType : int
{
    Occlusion = 0,
    Timestamp = 1,
}
public enum GPUFeatureName : int
{
    DepthClipControl = 1,
    Depth32FloatStencil8 = 2,
    TimestampQuery = 3,
    TextureCompressionBC = 4,
    TextureCompressionETC2 = 5,
    TextureCompressionASTC = 6,
    IndirectFirstInstance = 7,
    ShaderF16 = 8,
    RG11B10UfloatRenderable = 9,
    BGRA8UnormStorage = 10,
    Float32Filterable = 11,
}
public enum GPUBufferMapState : int
{
    Unmapped = 0,
    Pending = 1,
    Mapped = 2,
}
public enum GPUErrorFilter : int
{
    Validation = 0,
    OutOfMemory = 1,
    Internal = 2,
}
public enum GPUVertexFormat : int
{
    Uint8x2 = 1,
    Uint8x4 = 2,
    Sint8x2 = 3,
    Sint8x4 = 4,
    Unorm8x2 = 5,
    Unorm8x4 = 6,
    Snorm8x2 = 7,
    Snorm8x4 = 8,
    Uint16x2 = 9,
    Uint16x4 = 10,
    Sint16x2 = 11,
    Sint16x4 = 12,
    Unorm16x2 = 13,
    Unorm16x4 = 14,
    Snorm16x2 = 15,
    Snorm16x4 = 16,
    Float16x2 = 17,
    Float16x4 = 18,
    Float32 = 19,
    Float32x2 = 20,
    Float32x3 = 21,
    Float32x4 = 22,
    Uint32 = 23,
    Uint32x2 = 24,
    Uint32x3 = 25,
    Uint32x4 = 26,
    Sint32 = 27,
    Sint32x2 = 28,
    Sint32x3 = 29,
    Sint32x4 = 30,
}
public enum GPUPrimitiveTopology : int
{
    PointList = 0,
    LineList = 1,
    LineStrip = 2,
    TriangleList = 3,
    TriangleStrip = 4,
}
public enum GPUIndexFormat : int
{
    Uint16 = 1,
    Uint32 = 2,
}
public enum GPUSamplerBindingType : int
{
    Filtering = 1,
    NonFiltering = 2,
    Comparison = 3,
}
public enum GPUVertexStepMode : int
{
    Vertex = 0,
    Instance = 1,
    VertexBufferNotUsed = 2,
}
public enum GPUBufferBindingType : int
{
    Uniform = 1,
    Storage = 2,
    ReadOnlyStorage = 3,
}
public enum GPUStoreOp : int
{
    Store = 1,
    Discard = 2,
}
[Flags]
public enum GPUMapMode : int
{
    None = 0x0,
    Read = 0x1,
    Write = 0x2,
}
public enum GPUFrontFace : int
{
    CCW = 0,
    CW = 1,
}
public enum GPUAddressMode : int
{
    Repeat = 0,
    MirrorRepeat = 1,
    ClampToEdge = 2,
}
[Flags]
public enum GPUShaderStage : int
{
    None = 0x0,
    Vertex = 0x1,
    Fragment = 0x2,
    Compute = 0x4,
}
[Flags]
public enum GPUTextureUsage : int
{
    None = 0x0,
    CopySrc = 0x1,
    CopyDst = 0x2,
    TextureBinding = 0x4,
    StorageBinding = 0x8,
    RenderAttachment = 0x10,
}
public enum GPUCompilationMessageType : int
{
    Error = 0,
    Warning = 1,
    Info = 2,
}
public enum GPUPowerPreference : int
{
    LowPower = 1,
    HighPerformance = 2,
}
public enum GPUTextureSampleType : int
{
    Float = 1,
    UnfilterableFloat = 2,
    Depth = 3,
    Sint = 4,
    Uint = 5,
}
public enum GPUCompareFunction : int
{
    Never = 1,
    Less = 2,
    LessEqual = 3,
    Greater = 4,
    GreaterEqual = 5,
    Equal = 6,
    NotEqual = 7,
    Always = 8,
}
public enum GPUStorageTextureAccess : int
{
    WriteOnly = 1,
    ReadOnly = 2,
    ReadWrite = 3,
}
public enum GPUTextureFormat : int
{
    R8Unorm = 1,
    R8Snorm = 2,
    R8Uint = 3,
    R8Sint = 4,
    R16Uint = 5,
    R16Sint = 6,
    R16Float = 7,
    RG8Unorm = 8,
    RG8Snorm = 9,
    RG8Uint = 10,
    RG8Sint = 11,
    R32Float = 12,
    R32Uint = 13,
    R32Sint = 14,
    RG16Uint = 15,
    RG16Sint = 16,
    RG16Float = 17,
    RGBA8Unorm = 18,
    RGBA8UnormSrgb = 19,
    RGBA8Snorm = 20,
    RGBA8Uint = 21,
    RGBA8Sint = 22,
    BGRA8Unorm = 23,
    BGRA8UnormSrgb = 24,
    RGB10A2Uint = 25,
    RGB10A2Unorm = 26,
    RG11B10Ufloat = 27,
    RGB9E5Ufloat = 28,
    RG32Float = 29,
    RG32Uint = 30,
    RG32Sint = 31,
    RGBA16Uint = 32,
    RGBA16Sint = 33,
    RGBA16Float = 34,
    RGBA32Float = 35,
    RGBA32Uint = 36,
    RGBA32Sint = 37,
    Stencil8 = 38,
    Depth16Unorm = 39,
    Depth24Plus = 40,
    Depth24PlusStencil8 = 41,
    Depth32Float = 42,
    Depth32FloatStencil8 = 43,
    BC1RGBAUnorm = 44,
    BC1RGBAUnormSrgb = 45,
    BC2RGBAUnorm = 46,
    BC2RGBAUnormSrgb = 47,
    BC3RGBAUnorm = 48,
    BC3RGBAUnormSrgb = 49,
    BC4RUnorm = 50,
    BC4RSnorm = 51,
    BC5RGUnorm = 52,
    BC5RGSnorm = 53,
    BC6HRGBUfloat = 54,
    BC6HRGBFloat = 55,
    BC7RGBAUnorm = 56,
    BC7RGBAUnormSrgb = 57,
    ETC2RGB8Unorm = 58,
    ETC2RGB8UnormSrgb = 59,
    ETC2RGB8A1Unorm = 60,
    ETC2RGB8A1UnormSrgb = 61,
    ETC2RGBA8Unorm = 62,
    ETC2RGBA8UnormSrgb = 63,
    EACR11Unorm = 64,
    EACR11Snorm = 65,
    EACRG11Unorm = 66,
    EACRG11Snorm = 67,
    ASTC4x4Unorm = 68,
    ASTC4x4UnormSrgb = 69,
    ASTC5x4Unorm = 70,
    ASTC5x4UnormSrgb = 71,
    ASTC5x5Unorm = 72,
    ASTC5x5UnormSrgb = 73,
    ASTC6x5Unorm = 74,
    ASTC6x5UnormSrgb = 75,
    ASTC6x6Unorm = 76,
    ASTC6x6UnormSrgb = 77,
    ASTC8x5Unorm = 78,
    ASTC8x5UnormSrgb = 79,
    ASTC8x6Unorm = 80,
    ASTC8x6UnormSrgb = 81,
    ASTC8x8Unorm = 82,
    ASTC8x8UnormSrgb = 83,
    ASTC10x5Unorm = 84,
    ASTC10x5UnormSrgb = 85,
    ASTC10x6Unorm = 86,
    ASTC10x6UnormSrgb = 87,
    ASTC10x8Unorm = 88,
    ASTC10x8UnormSrgb = 89,
    ASTC10x10Unorm = 90,
    ASTC10x10UnormSrgb = 91,
    ASTC12x10Unorm = 92,
    ASTC12x10UnormSrgb = 93,
    ASTC12x12Unorm = 94,
    ASTC12x12UnormSrgb = 95,
}
public enum GPUFilterMode : int
{
    Nearest = 0,
    Linear = 1,
}
public enum GPUTextureViewDimension : int
{
    _1D = 1,
    _2D = 2,
    _2DArray = 3,
    Cube = 4,
    CubeArray = 5,
    _3D = 6,
}
public enum GPUBlendFactor : int
{
    Zero = 0,
    One = 1,
    Src = 2,
    OneMinusSrc = 3,
    SrcAlpha = 4,
    OneMinusSrcAlpha = 5,
    Dst = 6,
    OneMinusDst = 7,
    DstAlpha = 8,
    OneMinusDstAlpha = 9,
    SrcAlphaSaturated = 10,
    Constant = 11,
    OneMinusConstant = 12,
}
public enum GPULoadOp : int
{
    Clear = 1,
    Load = 2,
}
public enum GPUMipmapFilterMode : int
{
    Nearest = 0,
    Linear = 1,
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

public enum GPUCompositeAlphaMode : uint
{
    Auto = 0x00000000,
    Opaque = 0x00000001,
    Premultiplied = 0x00000002,
    Unpremultiplied = 0x00000003,
    Inherit = 0x00000004,
}

public enum GPUPresentMode : uint
{
    Fifo = 0x00000000,
    FifoRelaxed = 0x00000001,
    Immediate = 0x00000002,
    Mailbox = 0x00000003,
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


public enum GPUErrorType : uint
{
    NoError = 0x00000000,
    Validation = 0x00000001,
    OutOfMemory = 0x00000002,
    Internal = 0x00000003,
    Unknown = 0x00000004,
    DeviceLost = 0x00000005,
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
public enum GPUAdapterType : uint
{
    DiscreteGPU = 0x00000000,
    IntegratedGPU = 0x00000001,
    CPU = 0x00000002,
    Unknown = 0x00000003,
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

public enum GPUCreatePipelineAsyncStatus : uint
{
    Success = 0x00000000,
    ValidationError = 0x00000001,
    InternalError = 0x00000002,
    DeviceLost = 0x00000003,
    DeviceDestroyed = 0x00000004,
    Unknown = 0x00000005,
}

public enum GPUCompilationInfoRequestStatus : uint
{
    Success = 0x00000000,
    Error = 0x00000001,
    DeviceLost = 0x00000002,
    Unknown = 0x00000003,
}
