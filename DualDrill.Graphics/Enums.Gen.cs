using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DualDrill.Graphics.WebGPU.Native;

namespace DualDrill.Graphics;
[Flags]
public enum GPUBufferUsage : uint
{
    None = WGPUBufferUsage.WGPUBufferUsage_None,
    MapRead = WGPUBufferUsage.WGPUBufferUsage_MapRead,
    MapWrite = WGPUBufferUsage.WGPUBufferUsage_MapWrite,
    CopySrc = WGPUBufferUsage.WGPUBufferUsage_CopySrc,
    CopyDst = WGPUBufferUsage.WGPUBufferUsage_CopyDst,
    Index = WGPUBufferUsage.WGPUBufferUsage_Index,
    Vertex = WGPUBufferUsage.WGPUBufferUsage_Vertex,
    Uniform = WGPUBufferUsage.WGPUBufferUsage_Uniform,
    Storage = WGPUBufferUsage.WGPUBufferUsage_Storage,
    Indirect = WGPUBufferUsage.WGPUBufferUsage_Indirect,
    QueryResolve = WGPUBufferUsage.WGPUBufferUsage_QueryResolve
}

public enum GPUAdapterType : uint
{
    DiscreteGPU = WGPUAdapterType.WGPUAdapterType_DiscreteGPU,
    IntegratedGPU = WGPUAdapterType.WGPUAdapterType_IntegratedGPU,
    CPU = WGPUAdapterType.WGPUAdapterType_CPU,
    Unknown = WGPUAdapterType.WGPUAdapterType_Unknown
}

[Flags]
public enum GPUMapMode : uint
{
    None = WGPUMapMode.WGPUMapMode_None,
    Read = WGPUMapMode.WGPUMapMode_Read,
    Write = WGPUMapMode.WGPUMapMode_Write
}

public enum GPUAddressMode : uint
{
    Repeat = WGPUAddressMode.WGPUAddressMode_Repeat,
    MirrorRepeat = WGPUAddressMode.WGPUAddressMode_MirrorRepeat,
    ClampToEdge = WGPUAddressMode.WGPUAddressMode_ClampToEdge
}


public enum GPUBackendType : uint
{
    Undefined = WGPUBackendType.WGPUBackendType_Undefined,
    Null = WGPUBackendType.WGPUBackendType_Null,
    WebGPU = WGPUBackendType.WGPUBackendType_WebGPU,
    D3D11 = WGPUBackendType.WGPUBackendType_D3D11,
    D3D12 = WGPUBackendType.WGPUBackendType_D3D12,
    Metal = WGPUBackendType.WGPUBackendType_Metal,
    Vulkan = WGPUBackendType.WGPUBackendType_Vulkan,
    OpenGL = WGPUBackendType.WGPUBackendType_OpenGL,
    OpenGLES = WGPUBackendType.WGPUBackendType_OpenGLES
}


public enum GPUBlendFactor : uint
{
    Zero = WGPUBlendFactor.WGPUBlendFactor_Zero,
    One = WGPUBlendFactor.WGPUBlendFactor_One,
    Src = WGPUBlendFactor.WGPUBlendFactor_Src,
    OneMinusSrc = WGPUBlendFactor.WGPUBlendFactor_OneMinusSrc,
    SrcAlpha = WGPUBlendFactor.WGPUBlendFactor_SrcAlpha,
    OneMinusSrcAlpha = WGPUBlendFactor.WGPUBlendFactor_OneMinusSrcAlpha,
    Dst = WGPUBlendFactor.WGPUBlendFactor_Dst,
    OneMinusDst = WGPUBlendFactor.WGPUBlendFactor_OneMinusDst,
    DstAlpha = WGPUBlendFactor.WGPUBlendFactor_DstAlpha,
    OneMinusDstAlpha = WGPUBlendFactor.WGPUBlendFactor_OneMinusDstAlpha,
    SrcAlphaSaturated = WGPUBlendFactor.WGPUBlendFactor_SrcAlphaSaturated,
    Constant = WGPUBlendFactor.WGPUBlendFactor_Constant,
    OneMinusConstant = WGPUBlendFactor.WGPUBlendFactor_OneMinusConstant
}


public enum GPUBlendOperation : uint
{
    Add = WGPUBlendOperation.WGPUBlendOperation_Add,
    Subtract = WGPUBlendOperation.WGPUBlendOperation_Subtract,
    ReverseSubtract = WGPUBlendOperation.WGPUBlendOperation_ReverseSubtract,
    Min = WGPUBlendOperation.WGPUBlendOperation_Min,
    Max = WGPUBlendOperation.WGPUBlendOperation_Max
}


public enum GPUBufferBindingType : uint
{
    Undefined = WGPUBufferBindingType.WGPUBufferBindingType_Undefined,
    Uniform = WGPUBufferBindingType.WGPUBufferBindingType_Uniform,
    Storage = WGPUBufferBindingType.WGPUBufferBindingType_Storage,
    ReadOnlyStorage = WGPUBufferBindingType.WGPUBufferBindingType_ReadOnlyStorage
}


public enum GPUBufferMapAsyncStatus : uint
{
    Success = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_Success,
    ValidationError = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_ValidationError,
    Unknown = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_Unknown,
    DeviceLost = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_DeviceLost,
    DestroyedBeforeCallback = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_DestroyedBeforeCallback,
    UnmappedBeforeCallback = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_UnmappedBeforeCallback,
    MappingAlreadyPending = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_MappingAlreadyPending,
    OffsetOutOfRange = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_OffsetOutOfRange,
    SizeOutOfRange = WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_SizeOutOfRange
}


public enum GPUBufferMapState : uint
{
    Unmapped = WGPUBufferMapState.WGPUBufferMapState_Unmapped,
    Pending = WGPUBufferMapState.WGPUBufferMapState_Pending,
    Mapped = WGPUBufferMapState.WGPUBufferMapState_Mapped
}


public enum GPUCompareFunction : uint
{
    Undefined = WGPUCompareFunction.WGPUCompareFunction_Undefined,
    Never = WGPUCompareFunction.WGPUCompareFunction_Never,
    Less = WGPUCompareFunction.WGPUCompareFunction_Less,
    LessEqual = WGPUCompareFunction.WGPUCompareFunction_LessEqual,
    Greater = WGPUCompareFunction.WGPUCompareFunction_Greater,
    GreaterEqual = WGPUCompareFunction.WGPUCompareFunction_GreaterEqual,
    Equal = WGPUCompareFunction.WGPUCompareFunction_Equal,
    NotEqual = WGPUCompareFunction.WGPUCompareFunction_NotEqual,
    Always = WGPUCompareFunction.WGPUCompareFunction_Always
}


public enum GPUCompilationInfoRequestStatus : uint
{
    Success = WGPUCompilationInfoRequestStatus.WGPUCompilationInfoRequestStatus_Success,
    Error = WGPUCompilationInfoRequestStatus.WGPUCompilationInfoRequestStatus_Error,
    DeviceLost = WGPUCompilationInfoRequestStatus.WGPUCompilationInfoRequestStatus_DeviceLost,
    Unknown = WGPUCompilationInfoRequestStatus.WGPUCompilationInfoRequestStatus_Unknown
}


public enum GPUCompilationMessageType : uint
{
    Error = WGPUCompilationMessageType.WGPUCompilationMessageType_Error,
    Warning = WGPUCompilationMessageType.WGPUCompilationMessageType_Warning,
    Info = WGPUCompilationMessageType.WGPUCompilationMessageType_Info
}


public enum GPUCompositeAlphaMode : uint
{
    Auto = WGPUCompositeAlphaMode.WGPUCompositeAlphaMode_Auto,
    Opaque = WGPUCompositeAlphaMode.WGPUCompositeAlphaMode_Opaque,
    Premultiplied = WGPUCompositeAlphaMode.WGPUCompositeAlphaMode_Premultiplied,
    Unpremultiplied = WGPUCompositeAlphaMode.WGPUCompositeAlphaMode_Unpremultiplied,
    Inherit = WGPUCompositeAlphaMode.WGPUCompositeAlphaMode_Inherit
}


public enum GPUCreatePipelineAsyncStatus : uint
{
    Success = WGPUCreatePipelineAsyncStatus.WGPUCreatePipelineAsyncStatus_Success,
    ValidationError = WGPUCreatePipelineAsyncStatus.WGPUCreatePipelineAsyncStatus_ValidationError,
    InternalError = WGPUCreatePipelineAsyncStatus.WGPUCreatePipelineAsyncStatus_InternalError,
    DeviceLost = WGPUCreatePipelineAsyncStatus.WGPUCreatePipelineAsyncStatus_DeviceLost,
    DeviceDestroyed = WGPUCreatePipelineAsyncStatus.WGPUCreatePipelineAsyncStatus_DeviceDestroyed,
    Unknown = WGPUCreatePipelineAsyncStatus.WGPUCreatePipelineAsyncStatus_Unknown
}


public enum GPUCullMode : uint
{
    None = WGPUCullMode.WGPUCullMode_None,
    Front = WGPUCullMode.WGPUCullMode_Front,
    Back = WGPUCullMode.WGPUCullMode_Back
}


public enum GPUDeviceLostReason : uint
{
    Undefined = WGPUDeviceLostReason.WGPUDeviceLostReason_Undefined,
    Destroyed = WGPUDeviceLostReason.WGPUDeviceLostReason_Destroyed
}


public enum GPUErrorFilter : uint
{
    Validation = WGPUErrorFilter.WGPUErrorFilter_Validation,
    OutOfMemory = WGPUErrorFilter.WGPUErrorFilter_OutOfMemory,
    Internal = WGPUErrorFilter.WGPUErrorFilter_Internal
}


public enum GPUErrorType : uint
{
    NoError = WGPUErrorType.WGPUErrorType_NoError,
    Validation = WGPUErrorType.WGPUErrorType_Validation,
    OutOfMemory = WGPUErrorType.WGPUErrorType_OutOfMemory,
    Internal = WGPUErrorType.WGPUErrorType_Internal,
    Unknown = WGPUErrorType.WGPUErrorType_Unknown,
    DeviceLost = WGPUErrorType.WGPUErrorType_DeviceLost
}


public enum GPUFeatureName : uint
{
    Undefined = WGPUFeatureName.WGPUFeatureName_Undefined,
    DepthClipControl = WGPUFeatureName.WGPUFeatureName_DepthClipControl,
    Depth32FloatStencil8 = WGPUFeatureName.WGPUFeatureName_Depth32FloatStencil8,
    TimestampQuery = WGPUFeatureName.WGPUFeatureName_TimestampQuery,
    TextureCompressionBC = WGPUFeatureName.WGPUFeatureName_TextureCompressionBC,
    TextureCompressionETC2 = WGPUFeatureName.WGPUFeatureName_TextureCompressionETC2,
    TextureCompressionASTC = WGPUFeatureName.WGPUFeatureName_TextureCompressionASTC,
    IndirectFirstInstance = WGPUFeatureName.WGPUFeatureName_IndirectFirstInstance,
    ShaderF16 = WGPUFeatureName.WGPUFeatureName_ShaderF16,
    RG11B10UfloatRenderable = WGPUFeatureName.WGPUFeatureName_RG11B10UfloatRenderable,
    BGRA8UnormStorage = WGPUFeatureName.WGPUFeatureName_BGRA8UnormStorage,
    Float32Filterable = WGPUFeatureName.WGPUFeatureName_Float32Filterable
}


public enum GPUFilterMode : uint
{
    Nearest = WGPUFilterMode.WGPUFilterMode_Nearest,
    Linear = WGPUFilterMode.WGPUFilterMode_Linear
}


public enum GPUFrontFace : uint
{
    CCW = WGPUFrontFace.WGPUFrontFace_CCW,
    CW = WGPUFrontFace.WGPUFrontFace_CW
}


public enum GPUIndexFormat : uint
{
    Undefined = WGPUIndexFormat.WGPUIndexFormat_Undefined,
    Uint16 = WGPUIndexFormat.WGPUIndexFormat_Uint16,
    Uint32 = WGPUIndexFormat.WGPUIndexFormat_Uint32
}


public enum GPULoadOp : uint
{
    Undefined = WGPULoadOp.WGPULoadOp_Undefined,
    Clear = WGPULoadOp.WGPULoadOp_Clear,
    Load = WGPULoadOp.WGPULoadOp_Load
}


public enum GPUMipmapFilterMode : uint
{
    Nearest = WGPUMipmapFilterMode.WGPUMipmapFilterMode_Nearest,
    Linear = WGPUMipmapFilterMode.WGPUMipmapFilterMode_Linear
}


public enum GPUPowerPreference : uint
{
    Undefined = WGPUPowerPreference.WGPUPowerPreference_Undefined,
    LowPower = WGPUPowerPreference.WGPUPowerPreference_LowPower,
    HighPerformance = WGPUPowerPreference.WGPUPowerPreference_HighPerformance
}


public enum GPUPresentMode : uint
{
    Fifo = WGPUPresentMode.WGPUPresentMode_Fifo,
    FifoRelaxed = WGPUPresentMode.WGPUPresentMode_FifoRelaxed,
    Immediate = WGPUPresentMode.WGPUPresentMode_Immediate,
    Mailbox = WGPUPresentMode.WGPUPresentMode_Mailbox
}


public enum GPUPrimitiveTopology : uint
{
    PointList = WGPUPrimitiveTopology.WGPUPrimitiveTopology_PointList,
    LineList = WGPUPrimitiveTopology.WGPUPrimitiveTopology_LineList,
    LineStrip = WGPUPrimitiveTopology.WGPUPrimitiveTopology_LineStrip,
    TriangleList = WGPUPrimitiveTopology.WGPUPrimitiveTopology_TriangleList,
    TriangleStrip = WGPUPrimitiveTopology.WGPUPrimitiveTopology_TriangleStrip
}


public enum GPUQueryType : uint
{
    Occlusion = WGPUQueryType.WGPUQueryType_Occlusion,
    Timestamp = WGPUQueryType.WGPUQueryType_Timestamp
}


public enum GPUQueueWorkDoneStatus : uint
{
    Success = WGPUQueueWorkDoneStatus.WGPUQueueWorkDoneStatus_Success,
    Error = WGPUQueueWorkDoneStatus.WGPUQueueWorkDoneStatus_Error,
    Unknown = WGPUQueueWorkDoneStatus.WGPUQueueWorkDoneStatus_Unknown,
    DeviceLost = WGPUQueueWorkDoneStatus.WGPUQueueWorkDoneStatus_DeviceLost
}


public enum GPURequestAdapterStatus : uint
{
    Success = WGPURequestAdapterStatus.WGPURequestAdapterStatus_Success,
    Unavailable = WGPURequestAdapterStatus.WGPURequestAdapterStatus_Unavailable,
    Error = WGPURequestAdapterStatus.WGPURequestAdapterStatus_Error,
    Unknown = WGPURequestAdapterStatus.WGPURequestAdapterStatus_Unknown
}


public enum GPURequestDeviceStatus : uint
{
    Success = WGPURequestDeviceStatus.WGPURequestDeviceStatus_Success,
    Error = WGPURequestDeviceStatus.WGPURequestDeviceStatus_Error,
    Unknown = WGPURequestDeviceStatus.WGPURequestDeviceStatus_Unknown
}


public enum GPUSType : uint
{
    Invalid = WGPUSType.WGPUSType_Invalid,
    SurfaceDescriptorFromMetalLayer = WGPUSType.WGPUSType_SurfaceDescriptorFromMetalLayer,
    SurfaceDescriptorFromWindowsHWND = WGPUSType.WGPUSType_SurfaceDescriptorFromWindowsHWND,
    SurfaceDescriptorFromXlibWindow = WGPUSType.WGPUSType_SurfaceDescriptorFromXlibWindow,
    SurfaceDescriptorFromCanvasHTMLSelector = WGPUSType.WGPUSType_SurfaceDescriptorFromCanvasHTMLSelector,
    ShaderModuleSPIRVDescriptor = WGPUSType.WGPUSType_ShaderModuleSPIRVDescriptor,
    ShaderModuleWGSLDescriptor = WGPUSType.WGPUSType_ShaderModuleWGSLDescriptor,
    PrimitiveDepthClipControl = WGPUSType.WGPUSType_PrimitiveDepthClipControl,
    SurfaceDescriptorFromWaylandSurface = WGPUSType.WGPUSType_SurfaceDescriptorFromWaylandSurface,
    SurfaceDescriptorFromAndroidNativeWindow = WGPUSType.WGPUSType_SurfaceDescriptorFromAndroidNativeWindow,
    SurfaceDescriptorFromXcbWindow = WGPUSType.WGPUSType_SurfaceDescriptorFromXcbWindow,
    RenderPassDescriptorMaxDrawCount = WGPUSType.WGPUSType_RenderPassDescriptorMaxDrawCount
}


public enum GPUSamplerBindingType : uint
{
    Undefined = WGPUSamplerBindingType.WGPUSamplerBindingType_Undefined,
    Filtering = WGPUSamplerBindingType.WGPUSamplerBindingType_Filtering,
    NonFiltering = WGPUSamplerBindingType.WGPUSamplerBindingType_NonFiltering,
    Comparison = WGPUSamplerBindingType.WGPUSamplerBindingType_Comparison
}


public enum GPUStencilOperation : uint
{
    Keep = WGPUStencilOperation.WGPUStencilOperation_Keep,
    Zero = WGPUStencilOperation.WGPUStencilOperation_Zero,
    Replace = WGPUStencilOperation.WGPUStencilOperation_Replace,
    Invert = WGPUStencilOperation.WGPUStencilOperation_Invert,
    IncrementClamp = WGPUStencilOperation.WGPUStencilOperation_IncrementClamp,
    DecrementClamp = WGPUStencilOperation.WGPUStencilOperation_DecrementClamp,
    IncrementWrap = WGPUStencilOperation.WGPUStencilOperation_IncrementWrap,
    DecrementWrap = WGPUStencilOperation.WGPUStencilOperation_DecrementWrap
}


public enum GPUStorageTextureAccess : uint
{
    Undefined = WGPUStorageTextureAccess.WGPUStorageTextureAccess_Undefined,
    WriteOnly = WGPUStorageTextureAccess.WGPUStorageTextureAccess_WriteOnly,
    ReadOnly = WGPUStorageTextureAccess.WGPUStorageTextureAccess_ReadOnly,
    ReadWrite = WGPUStorageTextureAccess.WGPUStorageTextureAccess_ReadWrite
}


public enum GPUStoreOp : uint
{
    Undefined = WGPUStoreOp.WGPUStoreOp_Undefined,
    Store = WGPUStoreOp.WGPUStoreOp_Store,
    Discard = WGPUStoreOp.WGPUStoreOp_Discard
}


public enum GPUSurfaceGetCurrentTextureStatus : uint
{
    Success = WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Success,
    Timeout = WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Timeout,
    Outdated = WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Outdated,
    Lost = WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Lost,
    OutOfMemory = WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_OutOfMemory,
    DeviceLost = WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_DeviceLost
}


public enum GPUTextureAspect : uint
{
    All = WGPUTextureAspect.WGPUTextureAspect_All,
    StencilOnly = WGPUTextureAspect.WGPUTextureAspect_StencilOnly,
    DepthOnly = WGPUTextureAspect.WGPUTextureAspect_DepthOnly
}


public enum GPUTextureDimension : uint
{
    Dimension1D = WGPUTextureDimension.WGPUTextureDimension_1D,
    Dimension2D = WGPUTextureDimension.WGPUTextureDimension_2D,
    Dimension3D = WGPUTextureDimension.WGPUTextureDimension_3D
}


public enum GPUTextureFormat : uint
{
    Undefined = WGPUTextureFormat.WGPUTextureFormat_Undefined,
    R8Unorm = WGPUTextureFormat.WGPUTextureFormat_R8Unorm,
    R8Snorm = WGPUTextureFormat.WGPUTextureFormat_R8Snorm,
    R8Uint = WGPUTextureFormat.WGPUTextureFormat_R8Uint,
    R8Sint = WGPUTextureFormat.WGPUTextureFormat_R8Sint,
    R16Uint = WGPUTextureFormat.WGPUTextureFormat_R16Uint,
    R16Sint = WGPUTextureFormat.WGPUTextureFormat_R16Sint,
    R16Float = WGPUTextureFormat.WGPUTextureFormat_R16Float,
    RG8Unorm = WGPUTextureFormat.WGPUTextureFormat_RG8Unorm,
    RG8Snorm = WGPUTextureFormat.WGPUTextureFormat_RG8Snorm,
    RG8Uint = WGPUTextureFormat.WGPUTextureFormat_RG8Uint,
    RG8Sint = WGPUTextureFormat.WGPUTextureFormat_RG8Sint,
    R32Float = WGPUTextureFormat.WGPUTextureFormat_R32Float,
    R32Uint = WGPUTextureFormat.WGPUTextureFormat_R32Uint,
    R32Sint = WGPUTextureFormat.WGPUTextureFormat_R32Sint,
    RG16Uint = WGPUTextureFormat.WGPUTextureFormat_RG16Uint,
    RG16Sint = WGPUTextureFormat.WGPUTextureFormat_RG16Sint,
    RG16Float = WGPUTextureFormat.WGPUTextureFormat_RG16Float,
    RGBA8Unorm = WGPUTextureFormat.WGPUTextureFormat_RGBA8Unorm,
    RGBA8UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_RGBA8UnormSrgb,
    RGBA8Snorm = WGPUTextureFormat.WGPUTextureFormat_RGBA8Snorm,
    RGBA8Uint = WGPUTextureFormat.WGPUTextureFormat_RGBA8Uint,
    RGBA8Sint = WGPUTextureFormat.WGPUTextureFormat_RGBA8Sint,
    BGRA8Unorm = WGPUTextureFormat.WGPUTextureFormat_BGRA8Unorm,
    BGRA8UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_BGRA8UnormSrgb,
    RGB10A2Uint = WGPUTextureFormat.WGPUTextureFormat_RGB10A2Uint,
    RGB10A2Unorm = WGPUTextureFormat.WGPUTextureFormat_RGB10A2Unorm,
    RG11B10Ufloat = WGPUTextureFormat.WGPUTextureFormat_RG11B10Ufloat,
    RGB9E5Ufloat = WGPUTextureFormat.WGPUTextureFormat_RGB9E5Ufloat,
    RG32Float = WGPUTextureFormat.WGPUTextureFormat_RG32Float,
    RG32Uint = WGPUTextureFormat.WGPUTextureFormat_RG32Uint,
    RG32Sint = WGPUTextureFormat.WGPUTextureFormat_RG32Sint,
    RGBA16Uint = WGPUTextureFormat.WGPUTextureFormat_RGBA16Uint,
    RGBA16Sint = WGPUTextureFormat.WGPUTextureFormat_RGBA16Sint,
    RGBA16Float = WGPUTextureFormat.WGPUTextureFormat_RGBA16Float,
    RGBA32Float = WGPUTextureFormat.WGPUTextureFormat_RGBA32Float,
    RGBA32Uint = WGPUTextureFormat.WGPUTextureFormat_RGBA32Uint,
    RGBA32Sint = WGPUTextureFormat.WGPUTextureFormat_RGBA32Sint,
    Stencil8 = WGPUTextureFormat.WGPUTextureFormat_Stencil8,
    Depth16Unorm = WGPUTextureFormat.WGPUTextureFormat_Depth16Unorm,
    Depth24Plus = WGPUTextureFormat.WGPUTextureFormat_Depth24Plus,
    Depth24PlusStencil8 = WGPUTextureFormat.WGPUTextureFormat_Depth24PlusStencil8,
    Depth32Float = WGPUTextureFormat.WGPUTextureFormat_Depth32Float,
    Depth32FloatStencil8 = WGPUTextureFormat.WGPUTextureFormat_Depth32FloatStencil8,
    BC1RGBAUnorm = WGPUTextureFormat.WGPUTextureFormat_BC1RGBAUnorm,
    BC1RGBAUnormSrgb = WGPUTextureFormat.WGPUTextureFormat_BC1RGBAUnormSrgb,
    BC2RGBAUnorm = WGPUTextureFormat.WGPUTextureFormat_BC2RGBAUnorm,
    BC2RGBAUnormSrgb = WGPUTextureFormat.WGPUTextureFormat_BC2RGBAUnormSrgb,
    BC3RGBAUnorm = WGPUTextureFormat.WGPUTextureFormat_BC3RGBAUnorm,
    BC3RGBAUnormSrgb = WGPUTextureFormat.WGPUTextureFormat_BC3RGBAUnormSrgb,
    BC4RUnorm = WGPUTextureFormat.WGPUTextureFormat_BC4RUnorm,
    BC4RSnorm = WGPUTextureFormat.WGPUTextureFormat_BC4RSnorm,
    BC5RGUnorm = WGPUTextureFormat.WGPUTextureFormat_BC5RGUnorm,
    BC5RGSnorm = WGPUTextureFormat.WGPUTextureFormat_BC5RGSnorm,
    BC6HRGBUfloat = WGPUTextureFormat.WGPUTextureFormat_BC6HRGBUfloat,
    BC6HRGBFloat = WGPUTextureFormat.WGPUTextureFormat_BC6HRGBFloat,
    BC7RGBAUnorm = WGPUTextureFormat.WGPUTextureFormat_BC7RGBAUnorm,
    BC7RGBAUnormSrgb = WGPUTextureFormat.WGPUTextureFormat_BC7RGBAUnormSrgb,
    ETC2RGB8Unorm = WGPUTextureFormat.WGPUTextureFormat_ETC2RGB8Unorm,
    ETC2RGB8UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ETC2RGB8UnormSrgb,
    ETC2RGB8A1Unorm = WGPUTextureFormat.WGPUTextureFormat_ETC2RGB8A1Unorm,
    ETC2RGB8A1UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ETC2RGB8A1UnormSrgb,
    ETC2RGBA8Unorm = WGPUTextureFormat.WGPUTextureFormat_ETC2RGBA8Unorm,
    ETC2RGBA8UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ETC2RGBA8UnormSrgb,
    EACR11Unorm = WGPUTextureFormat.WGPUTextureFormat_EACR11Unorm,
    EACR11Snorm = WGPUTextureFormat.WGPUTextureFormat_EACR11Snorm,
    EACRG11Unorm = WGPUTextureFormat.WGPUTextureFormat_EACRG11Unorm,
    EACRG11Snorm = WGPUTextureFormat.WGPUTextureFormat_EACRG11Snorm,
    ASTC4x4Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC4x4Unorm,
    ASTC4x4UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC4x4UnormSrgb,
    ASTC5x4Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC5x4Unorm,
    ASTC5x4UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC5x4UnormSrgb,
    ASTC5x5Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC5x5Unorm,
    ASTC5x5UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC5x5UnormSrgb,
    ASTC6x5Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC6x5Unorm,
    ASTC6x5UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC6x5UnormSrgb,
    ASTC6x6Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC6x6Unorm,
    ASTC6x6UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC6x6UnormSrgb,
    ASTC8x5Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC8x5Unorm,
    ASTC8x5UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC8x5UnormSrgb,
    ASTC8x6Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC8x6Unorm,
    ASTC8x6UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC8x6UnormSrgb,
    ASTC8x8Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC8x8Unorm,
    ASTC8x8UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC8x8UnormSrgb,
    ASTC10x5Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC10x5Unorm,
    ASTC10x5UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC10x5UnormSrgb,
    ASTC10x6Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC10x6Unorm,
    ASTC10x6UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC10x6UnormSrgb,
    ASTC10x8Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC10x8Unorm,
    ASTC10x8UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC10x8UnormSrgb,
    ASTC10x10Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC10x10Unorm,
    ASTC10x10UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC10x10UnormSrgb,
    ASTC12x10Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC12x10Unorm,
    ASTC12x10UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC12x10UnormSrgb,
    ASTC12x12Unorm = WGPUTextureFormat.WGPUTextureFormat_ASTC12x12Unorm,
    ASTC12x12UnormSrgb = WGPUTextureFormat.WGPUTextureFormat_ASTC12x12UnormSrgb
}


public enum GPUTextureSampleType : uint
{
    Undefined = WGPUTextureSampleType.WGPUTextureSampleType_Undefined,
    Float = WGPUTextureSampleType.WGPUTextureSampleType_Float,
    UnfilterableFloat = WGPUTextureSampleType.WGPUTextureSampleType_UnfilterableFloat,
    Depth = WGPUTextureSampleType.WGPUTextureSampleType_Depth,
    Sint = WGPUTextureSampleType.WGPUTextureSampleType_Sint,
    Uint = WGPUTextureSampleType.WGPUTextureSampleType_Uint
}


public enum GPUTextureViewDimension : uint
{
    Undefined = WGPUTextureViewDimension.WGPUTextureViewDimension_Undefined,
    Dimension1D = WGPUTextureViewDimension.WGPUTextureViewDimension_1D,
    Dimension2D = WGPUTextureViewDimension.WGPUTextureViewDimension_2D,
    Dimension2DArray = WGPUTextureViewDimension.WGPUTextureViewDimension_2DArray,
    Cube = WGPUTextureViewDimension.WGPUTextureViewDimension_Cube,
    CubeArray = WGPUTextureViewDimension.WGPUTextureViewDimension_CubeArray,
    Dimension3D = WGPUTextureViewDimension.WGPUTextureViewDimension_3D
}


public enum GPUVertexFormat : uint
{
    Undefined = WGPUVertexFormat.WGPUVertexFormat_Undefined,
    Uint8x2 = WGPUVertexFormat.WGPUVertexFormat_Uint8x2,
    Uint8x4 = WGPUVertexFormat.WGPUVertexFormat_Uint8x4,
    Sint8x2 = WGPUVertexFormat.WGPUVertexFormat_Sint8x2,
    Sint8x4 = WGPUVertexFormat.WGPUVertexFormat_Sint8x4,
    Unorm8x2 = WGPUVertexFormat.WGPUVertexFormat_Unorm8x2,
    Unorm8x4 = WGPUVertexFormat.WGPUVertexFormat_Unorm8x4,
    Snorm8x2 = WGPUVertexFormat.WGPUVertexFormat_Snorm8x2,
    Snorm8x4 = WGPUVertexFormat.WGPUVertexFormat_Snorm8x4,
    Uint16x2 = WGPUVertexFormat.WGPUVertexFormat_Uint16x2,
    Uint16x4 = WGPUVertexFormat.WGPUVertexFormat_Uint16x4,
    Sint16x2 = WGPUVertexFormat.WGPUVertexFormat_Sint16x2,
    Sint16x4 = WGPUVertexFormat.WGPUVertexFormat_Sint16x4,
    Unorm16x2 = WGPUVertexFormat.WGPUVertexFormat_Unorm16x2,
    Unorm16x4 = WGPUVertexFormat.WGPUVertexFormat_Unorm16x4,
    Snorm16x2 = WGPUVertexFormat.WGPUVertexFormat_Snorm16x2,
    Snorm16x4 = WGPUVertexFormat.WGPUVertexFormat_Snorm16x4,
    Float16x2 = WGPUVertexFormat.WGPUVertexFormat_Float16x2,
    Float16x4 = WGPUVertexFormat.WGPUVertexFormat_Float16x4,
    Float32 = WGPUVertexFormat.WGPUVertexFormat_Float32,
    Float32x2 = WGPUVertexFormat.WGPUVertexFormat_Float32x2,
    Float32x3 = WGPUVertexFormat.WGPUVertexFormat_Float32x3,
    Float32x4 = WGPUVertexFormat.WGPUVertexFormat_Float32x4,
    Uint32 = WGPUVertexFormat.WGPUVertexFormat_Uint32,
    Uint32x2 = WGPUVertexFormat.WGPUVertexFormat_Uint32x2,
    Uint32x3 = WGPUVertexFormat.WGPUVertexFormat_Uint32x3,
    Uint32x4 = WGPUVertexFormat.WGPUVertexFormat_Uint32x4,
    Sint32 = WGPUVertexFormat.WGPUVertexFormat_Sint32,
    Sint32x2 = WGPUVertexFormat.WGPUVertexFormat_Sint32x2,
    Sint32x3 = WGPUVertexFormat.WGPUVertexFormat_Sint32x3,
    Sint32x4 = WGPUVertexFormat.WGPUVertexFormat_Sint32x4
}


public enum GPUVertexStepMode : uint
{
    Vertex = WGPUVertexStepMode.WGPUVertexStepMode_Vertex,
    Instance = WGPUVertexStepMode.WGPUVertexStepMode_Instance,
    VertexBufferNotUsed = WGPUVertexStepMode.WGPUVertexStepMode_VertexBufferNotUsed
}


public enum GPUColorWriteMask : uint
{
    None = WGPUColorWriteMask.WGPUColorWriteMask_None,
    Red = WGPUColorWriteMask.WGPUColorWriteMask_Red,
    Green = WGPUColorWriteMask.WGPUColorWriteMask_Green,
    Blue = WGPUColorWriteMask.WGPUColorWriteMask_Blue,
    Alpha = WGPUColorWriteMask.WGPUColorWriteMask_Alpha,
    All = WGPUColorWriteMask.WGPUColorWriteMask_All
}

public enum GPUShaderStage : uint
{
    None = WGPUShaderStage.WGPUShaderStage_None,
    Vertex = WGPUShaderStage.WGPUShaderStage_Vertex,
    Fragment = WGPUShaderStage.WGPUShaderStage_Fragment,
    Compute = WGPUShaderStage.WGPUShaderStage_Compute
}


[Flags]
public enum GPUTextureUsage : uint
{
    None = WGPUTextureUsage.WGPUTextureUsage_None,
    CopySrc = WGPUTextureUsage.WGPUTextureUsage_CopySrc,
    CopyDst = WGPUTextureUsage.WGPUTextureUsage_CopyDst,
    TextureBinding = WGPUTextureUsage.WGPUTextureUsage_TextureBinding,
    StorageBinding = WGPUTextureUsage.WGPUTextureUsage_StorageBinding,
    RenderAttachment = WGPUTextureUsage.WGPUTextureUsage_RenderAttachment
}


public enum GPUNativeSType : uint
{
    Extras = WGPUNativeSType.WGPUSType_DeviceExtras,
    edLimitsExtras = WGPUNativeSType.WGPUSType_RequiredLimitsExtras,
    neLayoutExtras = WGPUNativeSType.WGPUSType_PipelineLayoutExtras,
    ModuleGLSLDescriptor = WGPUNativeSType.WGPUSType_ShaderModuleGLSLDescriptor,
    tedLimitsExtras = WGPUNativeSType.WGPUSType_SupportedLimitsExtras,
    ceExtras = WGPUNativeSType.WGPUSType_InstanceExtras,
    oupEntryExtras = WGPUNativeSType.WGPUSType_BindGroupEntryExtras,
    oupLayoutEntryExtras = WGPUNativeSType.WGPUSType_BindGroupLayoutEntryExtras,
    etDescriptorExtras = WGPUNativeSType.WGPUSType_QuerySetDescriptorExtras,
    eConfigurationExtras = WGPUNativeSType.WGPUSType_SurfaceConfigurationExtras
}


public enum GPUNativeFeature : uint
{
    PushConstants = WGPUNativeFeature.WGPUNativeFeature_PushConstants,
    TextureAdapterSpecificFormatFeatures = WGPUNativeFeature.WGPUNativeFeature_TextureAdapterSpecificFormatFeatures,
    MultiDrawIndirect = WGPUNativeFeature.WGPUNativeFeature_MultiDrawIndirect,
    MultiDrawIndirectCount = WGPUNativeFeature.WGPUNativeFeature_MultiDrawIndirectCount,
    VertexWritableStorage = WGPUNativeFeature.WGPUNativeFeature_VertexWritableStorage,
    TextureBindingArray = WGPUNativeFeature.WGPUNativeFeature_TextureBindingArray,
    SampledTextureAndStorageBufferArrayNonUniformIndexing = WGPUNativeFeature.WGPUNativeFeature_SampledTextureAndStorageBufferArrayNonUniformIndexing,
    PipelineStatisticsQuery = WGPUNativeFeature.WGPUNativeFeature_PipelineStatisticsQuery,
    StorageResourceBindingArray = WGPUNativeFeature.WGPUNativeFeature_StorageResourceBindingArray,
    PartiallyBoundBindingArray = WGPUNativeFeature.WGPUNativeFeature_PartiallyBoundBindingArray
}


public enum GPULogLevel : uint
{
    Off = WGPULogLevel.WGPULogLevel_Off,
    Error = WGPULogLevel.WGPULogLevel_Error,
    Warn = WGPULogLevel.WGPULogLevel_Warn,
    Info = WGPULogLevel.WGPULogLevel_Info,
    Debug = WGPULogLevel.WGPULogLevel_Debug,
    Trace = WGPULogLevel.WGPULogLevel_Trace
}


public enum GPUInstanceBackend : uint
{
    All = WGPUInstanceBackend.WGPUInstanceBackend_All,
    Vulkan = WGPUInstanceBackend.WGPUInstanceBackend_Vulkan,
    GL = WGPUInstanceBackend.WGPUInstanceBackend_GL,
    Metal = WGPUInstanceBackend.WGPUInstanceBackend_Metal,
    DX12 = WGPUInstanceBackend.WGPUInstanceBackend_DX12,
    DX11 = WGPUInstanceBackend.WGPUInstanceBackend_DX11,
    BrowserWebGPU = WGPUInstanceBackend.WGPUInstanceBackend_BrowserWebGPU,
    Primary = WGPUInstanceBackend.WGPUInstanceBackend_Primary,
    Secondary = WGPUInstanceBackend.WGPUInstanceBackend_Secondary
}


public enum GPUInstanceFlag : uint
{
    Default = WGPUInstanceFlag.WGPUInstanceFlag_Default,
    Debug = WGPUInstanceFlag.WGPUInstanceFlag_Debug,
    Validation = WGPUInstanceFlag.WGPUInstanceFlag_Validation,
    DiscardHalLabels = WGPUInstanceFlag.WGPUInstanceFlag_DiscardHalLabels
}


public enum GPUDx12Compiler : uint
{
    Undefined = WGPUDx12Compiler.WGPUDx12Compiler_Undefined,
    Fxc = WGPUDx12Compiler.WGPUDx12Compiler_Fxc,
    Dxc = WGPUDx12Compiler.WGPUDx12Compiler_Dxc
}


public enum GPUGles3MinorVersion : uint
{
    Automatic = WGPUGles3MinorVersion.WGPUGles3MinorVersion_Automatic,
    Version0 = WGPUGles3MinorVersion.WGPUGles3MinorVersion_Version0,
    Version1 = WGPUGles3MinorVersion.WGPUGles3MinorVersion_Version1,
    Version2 = WGPUGles3MinorVersion.WGPUGles3MinorVersion_Version2
}


public enum GPUPipelineStatisticName : uint
{
    VertexShaderInvocations = WGPUPipelineStatisticName.WGPUPipelineStatisticName_VertexShaderInvocations,
    ClipperInvocations = WGPUPipelineStatisticName.WGPUPipelineStatisticName_ClipperInvocations,
    ClipperPrimitivesOut = WGPUPipelineStatisticName.WGPUPipelineStatisticName_ClipperPrimitivesOut,
    FragmentShaderInvocations = WGPUPipelineStatisticName.WGPUPipelineStatisticName_FragmentShaderInvocations,
    ComputeShaderInvocations = WGPUPipelineStatisticName.WGPUPipelineStatisticName_ComputeShaderInvocations
}


public enum GPUNativeQueryType : uint
{
    PipelineStatistics = WGPUNativeQueryType.WGPUNativeQueryType_PipelineStatistics
}
