namespace DualDrill.Graphics;

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
