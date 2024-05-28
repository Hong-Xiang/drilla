using System.Runtime.InteropServices;

namespace DualDrill.Graphics.WebGPU.Native
{
    public partial struct WGPUAdapterImpl
    {
    }

    public partial struct WGPUBindGroupImpl
    {
    }

    public partial struct WGPUBindGroupLayoutImpl
    {
    }

    public partial struct WGPUBufferImpl
    {
    }

    public partial struct WGPUCommandBufferImpl
    {
    }

    public partial struct WGPUCommandEncoderImpl
    {
    }

    public partial struct WGPUComputePassEncoderImpl
    {
    }

    public partial struct WGPUComputePipelineImpl
    {
    }

    public partial struct WGPUDeviceImpl
    {
    }

    public partial struct WGPUInstanceImpl
    {
    }

    public partial struct WGPUPipelineLayoutImpl
    {
    }

    public partial struct WGPUQuerySetImpl
    {
    }

    public partial struct WGPUQueueImpl
    {
    }

    public partial struct WGPURenderBundleImpl
    {
    }

    public partial struct WGPURenderBundleEncoderImpl
    {
    }

    public partial struct WGPURenderPassEncoderImpl
    {
    }

    public partial struct WGPURenderPipelineImpl
    {
    }

    public partial struct WGPUSamplerImpl
    {
    }

    public partial struct WGPUShaderModuleImpl
    {
    }

    public partial struct WGPUSurfaceImpl
    {
    }

    public partial struct WGPUTextureImpl
    {
    }

    public partial struct WGPUTextureViewImpl
    {
    }

    public enum WGPUAdapterType
    {
        WGPUAdapterType_DiscreteGPU = 0x00000000,
        WGPUAdapterType_IntegratedGPU = 0x00000001,
        WGPUAdapterType_CPU = 0x00000002,
        WGPUAdapterType_Unknown = 0x00000003,
        WGPUAdapterType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUAddressMode
    {
        WGPUAddressMode_Repeat = 0x00000000,
        WGPUAddressMode_MirrorRepeat = 0x00000001,
        WGPUAddressMode_ClampToEdge = 0x00000002,
        WGPUAddressMode_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUBackendType
    {
        WGPUBackendType_Undefined = 0x00000000,
        WGPUBackendType_Null = 0x00000001,
        WGPUBackendType_WebGPU = 0x00000002,
        WGPUBackendType_D3D11 = 0x00000003,
        WGPUBackendType_D3D12 = 0x00000004,
        WGPUBackendType_Metal = 0x00000005,
        WGPUBackendType_Vulkan = 0x00000006,
        WGPUBackendType_OpenGL = 0x00000007,
        WGPUBackendType_OpenGLES = 0x00000008,
        WGPUBackendType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUBlendFactor
    {
        WGPUBlendFactor_Zero = 0x00000000,
        WGPUBlendFactor_One = 0x00000001,
        WGPUBlendFactor_Src = 0x00000002,
        WGPUBlendFactor_OneMinusSrc = 0x00000003,
        WGPUBlendFactor_SrcAlpha = 0x00000004,
        WGPUBlendFactor_OneMinusSrcAlpha = 0x00000005,
        WGPUBlendFactor_Dst = 0x00000006,
        WGPUBlendFactor_OneMinusDst = 0x00000007,
        WGPUBlendFactor_DstAlpha = 0x00000008,
        WGPUBlendFactor_OneMinusDstAlpha = 0x00000009,
        WGPUBlendFactor_SrcAlphaSaturated = 0x0000000A,
        WGPUBlendFactor_Constant = 0x0000000B,
        WGPUBlendFactor_OneMinusConstant = 0x0000000C,
        WGPUBlendFactor_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUBlendOperation
    {
        WGPUBlendOperation_Add = 0x00000000,
        WGPUBlendOperation_Subtract = 0x00000001,
        WGPUBlendOperation_ReverseSubtract = 0x00000002,
        WGPUBlendOperation_Min = 0x00000003,
        WGPUBlendOperation_Max = 0x00000004,
        WGPUBlendOperation_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUBufferBindingType
    {
        WGPUBufferBindingType_Undefined = 0x00000000,
        WGPUBufferBindingType_Uniform = 0x00000001,
        WGPUBufferBindingType_Storage = 0x00000002,
        WGPUBufferBindingType_ReadOnlyStorage = 0x00000003,
        WGPUBufferBindingType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUBufferMapAsyncStatus
    {
        WGPUBufferMapAsyncStatus_Success = 0x00000000,
        WGPUBufferMapAsyncStatus_ValidationError = 0x00000001,
        WGPUBufferMapAsyncStatus_Unknown = 0x00000002,
        WGPUBufferMapAsyncStatus_DeviceLost = 0x00000003,
        WGPUBufferMapAsyncStatus_DestroyedBeforeCallback = 0x00000004,
        WGPUBufferMapAsyncStatus_UnmappedBeforeCallback = 0x00000005,
        WGPUBufferMapAsyncStatus_MappingAlreadyPending = 0x00000006,
        WGPUBufferMapAsyncStatus_OffsetOutOfRange = 0x00000007,
        WGPUBufferMapAsyncStatus_SizeOutOfRange = 0x00000008,
        WGPUBufferMapAsyncStatus_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUBufferMapState
    {
        WGPUBufferMapState_Unmapped = 0x00000000,
        WGPUBufferMapState_Pending = 0x00000001,
        WGPUBufferMapState_Mapped = 0x00000002,
        WGPUBufferMapState_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUCompareFunction
    {
        WGPUCompareFunction_Undefined = 0x00000000,
        WGPUCompareFunction_Never = 0x00000001,
        WGPUCompareFunction_Less = 0x00000002,
        WGPUCompareFunction_LessEqual = 0x00000003,
        WGPUCompareFunction_Greater = 0x00000004,
        WGPUCompareFunction_GreaterEqual = 0x00000005,
        WGPUCompareFunction_Equal = 0x00000006,
        WGPUCompareFunction_NotEqual = 0x00000007,
        WGPUCompareFunction_Always = 0x00000008,
        WGPUCompareFunction_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUCompilationInfoRequestStatus
    {
        WGPUCompilationInfoRequestStatus_Success = 0x00000000,
        WGPUCompilationInfoRequestStatus_Error = 0x00000001,
        WGPUCompilationInfoRequestStatus_DeviceLost = 0x00000002,
        WGPUCompilationInfoRequestStatus_Unknown = 0x00000003,
        WGPUCompilationInfoRequestStatus_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUCompilationMessageType
    {
        WGPUCompilationMessageType_Error = 0x00000000,
        WGPUCompilationMessageType_Warning = 0x00000001,
        WGPUCompilationMessageType_Info = 0x00000002,
        WGPUCompilationMessageType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUCompositeAlphaMode
    {
        WGPUCompositeAlphaMode_Auto = 0x00000000,
        WGPUCompositeAlphaMode_Opaque = 0x00000001,
        WGPUCompositeAlphaMode_Premultiplied = 0x00000002,
        WGPUCompositeAlphaMode_Unpremultiplied = 0x00000003,
        WGPUCompositeAlphaMode_Inherit = 0x00000004,
        WGPUCompositeAlphaMode_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUCreatePipelineAsyncStatus
    {
        WGPUCreatePipelineAsyncStatus_Success = 0x00000000,
        WGPUCreatePipelineAsyncStatus_ValidationError = 0x00000001,
        WGPUCreatePipelineAsyncStatus_InternalError = 0x00000002,
        WGPUCreatePipelineAsyncStatus_DeviceLost = 0x00000003,
        WGPUCreatePipelineAsyncStatus_DeviceDestroyed = 0x00000004,
        WGPUCreatePipelineAsyncStatus_Unknown = 0x00000005,
        WGPUCreatePipelineAsyncStatus_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUCullMode
    {
        WGPUCullMode_None = 0x00000000,
        WGPUCullMode_Front = 0x00000001,
        WGPUCullMode_Back = 0x00000002,
        WGPUCullMode_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUDeviceLostReason
    {
        WGPUDeviceLostReason_Undefined = 0x00000000,
        WGPUDeviceLostReason_Destroyed = 0x00000001,
        WGPUDeviceLostReason_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUErrorFilter
    {
        WGPUErrorFilter_Validation = 0x00000000,
        WGPUErrorFilter_OutOfMemory = 0x00000001,
        WGPUErrorFilter_Internal = 0x00000002,
        WGPUErrorFilter_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUErrorType
    {
        WGPUErrorType_NoError = 0x00000000,
        WGPUErrorType_Validation = 0x00000001,
        WGPUErrorType_OutOfMemory = 0x00000002,
        WGPUErrorType_Internal = 0x00000003,
        WGPUErrorType_Unknown = 0x00000004,
        WGPUErrorType_DeviceLost = 0x00000005,
        WGPUErrorType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUFeatureName
    {
        WGPUFeatureName_Undefined = 0x00000000,
        WGPUFeatureName_DepthClipControl = 0x00000001,
        WGPUFeatureName_Depth32FloatStencil8 = 0x00000002,
        WGPUFeatureName_TimestampQuery = 0x00000003,
        WGPUFeatureName_TextureCompressionBC = 0x00000004,
        WGPUFeatureName_TextureCompressionETC2 = 0x00000005,
        WGPUFeatureName_TextureCompressionASTC = 0x00000006,
        WGPUFeatureName_IndirectFirstInstance = 0x00000007,
        WGPUFeatureName_ShaderF16 = 0x00000008,
        WGPUFeatureName_RG11B10UfloatRenderable = 0x00000009,
        WGPUFeatureName_BGRA8UnormStorage = 0x0000000A,
        WGPUFeatureName_Float32Filterable = 0x0000000B,
        WGPUFeatureName_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUFilterMode
    {
        WGPUFilterMode_Nearest = 0x00000000,
        WGPUFilterMode_Linear = 0x00000001,
        WGPUFilterMode_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUFrontFace
    {
        WGPUFrontFace_CCW = 0x00000000,
        WGPUFrontFace_CW = 0x00000001,
        WGPUFrontFace_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUIndexFormat
    {
        WGPUIndexFormat_Undefined = 0x00000000,
        WGPUIndexFormat_Uint16 = 0x00000001,
        WGPUIndexFormat_Uint32 = 0x00000002,
        WGPUIndexFormat_Force32 = 0x7FFFFFFF,
    }

    public enum WGPULoadOp
    {
        WGPULoadOp_Undefined = 0x00000000,
        WGPULoadOp_Clear = 0x00000001,
        WGPULoadOp_Load = 0x00000002,
        WGPULoadOp_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUMipmapFilterMode
    {
        WGPUMipmapFilterMode_Nearest = 0x00000000,
        WGPUMipmapFilterMode_Linear = 0x00000001,
        WGPUMipmapFilterMode_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUPowerPreference
    {
        WGPUPowerPreference_Undefined = 0x00000000,
        WGPUPowerPreference_LowPower = 0x00000001,
        WGPUPowerPreference_HighPerformance = 0x00000002,
        WGPUPowerPreference_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUPresentMode
    {
        WGPUPresentMode_Fifo = 0x00000000,
        WGPUPresentMode_FifoRelaxed = 0x00000001,
        WGPUPresentMode_Immediate = 0x00000002,
        WGPUPresentMode_Mailbox = 0x00000003,
        WGPUPresentMode_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUPrimitiveTopology
    {
        WGPUPrimitiveTopology_PointList = 0x00000000,
        WGPUPrimitiveTopology_LineList = 0x00000001,
        WGPUPrimitiveTopology_LineStrip = 0x00000002,
        WGPUPrimitiveTopology_TriangleList = 0x00000003,
        WGPUPrimitiveTopology_TriangleStrip = 0x00000004,
        WGPUPrimitiveTopology_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUQueryType
    {
        WGPUQueryType_Occlusion = 0x00000000,
        WGPUQueryType_Timestamp = 0x00000001,
        WGPUQueryType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUQueueWorkDoneStatus
    {
        WGPUQueueWorkDoneStatus_Success = 0x00000000,
        WGPUQueueWorkDoneStatus_Error = 0x00000001,
        WGPUQueueWorkDoneStatus_Unknown = 0x00000002,
        WGPUQueueWorkDoneStatus_DeviceLost = 0x00000003,
        WGPUQueueWorkDoneStatus_Force32 = 0x7FFFFFFF,
    }

    public enum WGPURequestAdapterStatus
    {
        WGPURequestAdapterStatus_Success = 0x00000000,
        WGPURequestAdapterStatus_Unavailable = 0x00000001,
        WGPURequestAdapterStatus_Error = 0x00000002,
        WGPURequestAdapterStatus_Unknown = 0x00000003,
        WGPURequestAdapterStatus_Force32 = 0x7FFFFFFF,
    }

    public enum WGPURequestDeviceStatus
    {
        WGPURequestDeviceStatus_Success = 0x00000000,
        WGPURequestDeviceStatus_Error = 0x00000001,
        WGPURequestDeviceStatus_Unknown = 0x00000002,
        WGPURequestDeviceStatus_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUSType
    {
        WGPUSType_Invalid = 0x00000000,
        WGPUSType_SurfaceDescriptorFromMetalLayer = 0x00000001,
        WGPUSType_SurfaceDescriptorFromWindowsHWND = 0x00000002,
        WGPUSType_SurfaceDescriptorFromXlibWindow = 0x00000003,
        WGPUSType_SurfaceDescriptorFromCanvasHTMLSelector = 0x00000004,
        WGPUSType_ShaderModuleSPIRVDescriptor = 0x00000005,
        WGPUSType_ShaderModuleWGSLDescriptor = 0x00000006,
        WGPUSType_PrimitiveDepthClipControl = 0x00000007,
        WGPUSType_SurfaceDescriptorFromWaylandSurface = 0x00000008,
        WGPUSType_SurfaceDescriptorFromAndroidNativeWindow = 0x00000009,
        WGPUSType_SurfaceDescriptorFromXcbWindow = 0x0000000A,
        WGPUSType_RenderPassDescriptorMaxDrawCount = 0x0000000F,
        WGPUSType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUSamplerBindingType
    {
        WGPUSamplerBindingType_Undefined = 0x00000000,
        WGPUSamplerBindingType_Filtering = 0x00000001,
        WGPUSamplerBindingType_NonFiltering = 0x00000002,
        WGPUSamplerBindingType_Comparison = 0x00000003,
        WGPUSamplerBindingType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUStencilOperation
    {
        WGPUStencilOperation_Keep = 0x00000000,
        WGPUStencilOperation_Zero = 0x00000001,
        WGPUStencilOperation_Replace = 0x00000002,
        WGPUStencilOperation_Invert = 0x00000003,
        WGPUStencilOperation_IncrementClamp = 0x00000004,
        WGPUStencilOperation_DecrementClamp = 0x00000005,
        WGPUStencilOperation_IncrementWrap = 0x00000006,
        WGPUStencilOperation_DecrementWrap = 0x00000007,
        WGPUStencilOperation_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUStorageTextureAccess
    {
        WGPUStorageTextureAccess_Undefined = 0x00000000,
        WGPUStorageTextureAccess_WriteOnly = 0x00000001,
        WGPUStorageTextureAccess_ReadOnly = 0x00000002,
        WGPUStorageTextureAccess_ReadWrite = 0x00000003,
        WGPUStorageTextureAccess_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUStoreOp
    {
        WGPUStoreOp_Undefined = 0x00000000,
        WGPUStoreOp_Store = 0x00000001,
        WGPUStoreOp_Discard = 0x00000002,
        WGPUStoreOp_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUSurfaceGetCurrentTextureStatus
    {
        WGPUSurfaceGetCurrentTextureStatus_Success = 0x00000000,
        WGPUSurfaceGetCurrentTextureStatus_Timeout = 0x00000001,
        WGPUSurfaceGetCurrentTextureStatus_Outdated = 0x00000002,
        WGPUSurfaceGetCurrentTextureStatus_Lost = 0x00000003,
        WGPUSurfaceGetCurrentTextureStatus_OutOfMemory = 0x00000004,
        WGPUSurfaceGetCurrentTextureStatus_DeviceLost = 0x00000005,
        WGPUSurfaceGetCurrentTextureStatus_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUTextureAspect
    {
        WGPUTextureAspect_All = 0x00000000,
        WGPUTextureAspect_StencilOnly = 0x00000001,
        WGPUTextureAspect_DepthOnly = 0x00000002,
        WGPUTextureAspect_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUTextureDimension
    {
        WGPUTextureDimension_1D = 0x00000000,
        WGPUTextureDimension_2D = 0x00000001,
        WGPUTextureDimension_3D = 0x00000002,
        WGPUTextureDimension_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUTextureFormat
    {
        WGPUTextureFormat_Undefined = 0x00000000,
        WGPUTextureFormat_R8Unorm = 0x00000001,
        WGPUTextureFormat_R8Snorm = 0x00000002,
        WGPUTextureFormat_R8Uint = 0x00000003,
        WGPUTextureFormat_R8Sint = 0x00000004,
        WGPUTextureFormat_R16Uint = 0x00000005,
        WGPUTextureFormat_R16Sint = 0x00000006,
        WGPUTextureFormat_R16Float = 0x00000007,
        WGPUTextureFormat_RG8Unorm = 0x00000008,
        WGPUTextureFormat_RG8Snorm = 0x00000009,
        WGPUTextureFormat_RG8Uint = 0x0000000A,
        WGPUTextureFormat_RG8Sint = 0x0000000B,
        WGPUTextureFormat_R32Float = 0x0000000C,
        WGPUTextureFormat_R32Uint = 0x0000000D,
        WGPUTextureFormat_R32Sint = 0x0000000E,
        WGPUTextureFormat_RG16Uint = 0x0000000F,
        WGPUTextureFormat_RG16Sint = 0x00000010,
        WGPUTextureFormat_RG16Float = 0x00000011,
        WGPUTextureFormat_RGBA8Unorm = 0x00000012,
        WGPUTextureFormat_RGBA8UnormSrgb = 0x00000013,
        WGPUTextureFormat_RGBA8Snorm = 0x00000014,
        WGPUTextureFormat_RGBA8Uint = 0x00000015,
        WGPUTextureFormat_RGBA8Sint = 0x00000016,
        WGPUTextureFormat_BGRA8Unorm = 0x00000017,
        WGPUTextureFormat_BGRA8UnormSrgb = 0x00000018,
        WGPUTextureFormat_RGB10A2Uint = 0x00000019,
        WGPUTextureFormat_RGB10A2Unorm = 0x0000001A,
        WGPUTextureFormat_RG11B10Ufloat = 0x0000001B,
        WGPUTextureFormat_RGB9E5Ufloat = 0x0000001C,
        WGPUTextureFormat_RG32Float = 0x0000001D,
        WGPUTextureFormat_RG32Uint = 0x0000001E,
        WGPUTextureFormat_RG32Sint = 0x0000001F,
        WGPUTextureFormat_RGBA16Uint = 0x00000020,
        WGPUTextureFormat_RGBA16Sint = 0x00000021,
        WGPUTextureFormat_RGBA16Float = 0x00000022,
        WGPUTextureFormat_RGBA32Float = 0x00000023,
        WGPUTextureFormat_RGBA32Uint = 0x00000024,
        WGPUTextureFormat_RGBA32Sint = 0x00000025,
        WGPUTextureFormat_Stencil8 = 0x00000026,
        WGPUTextureFormat_Depth16Unorm = 0x00000027,
        WGPUTextureFormat_Depth24Plus = 0x00000028,
        WGPUTextureFormat_Depth24PlusStencil8 = 0x00000029,
        WGPUTextureFormat_Depth32Float = 0x0000002A,
        WGPUTextureFormat_Depth32FloatStencil8 = 0x0000002B,
        WGPUTextureFormat_BC1RGBAUnorm = 0x0000002C,
        WGPUTextureFormat_BC1RGBAUnormSrgb = 0x0000002D,
        WGPUTextureFormat_BC2RGBAUnorm = 0x0000002E,
        WGPUTextureFormat_BC2RGBAUnormSrgb = 0x0000002F,
        WGPUTextureFormat_BC3RGBAUnorm = 0x00000030,
        WGPUTextureFormat_BC3RGBAUnormSrgb = 0x00000031,
        WGPUTextureFormat_BC4RUnorm = 0x00000032,
        WGPUTextureFormat_BC4RSnorm = 0x00000033,
        WGPUTextureFormat_BC5RGUnorm = 0x00000034,
        WGPUTextureFormat_BC5RGSnorm = 0x00000035,
        WGPUTextureFormat_BC6HRGBUfloat = 0x00000036,
        WGPUTextureFormat_BC6HRGBFloat = 0x00000037,
        WGPUTextureFormat_BC7RGBAUnorm = 0x00000038,
        WGPUTextureFormat_BC7RGBAUnormSrgb = 0x00000039,
        WGPUTextureFormat_ETC2RGB8Unorm = 0x0000003A,
        WGPUTextureFormat_ETC2RGB8UnormSrgb = 0x0000003B,
        WGPUTextureFormat_ETC2RGB8A1Unorm = 0x0000003C,
        WGPUTextureFormat_ETC2RGB8A1UnormSrgb = 0x0000003D,
        WGPUTextureFormat_ETC2RGBA8Unorm = 0x0000003E,
        WGPUTextureFormat_ETC2RGBA8UnormSrgb = 0x0000003F,
        WGPUTextureFormat_EACR11Unorm = 0x00000040,
        WGPUTextureFormat_EACR11Snorm = 0x00000041,
        WGPUTextureFormat_EACRG11Unorm = 0x00000042,
        WGPUTextureFormat_EACRG11Snorm = 0x00000043,
        WGPUTextureFormat_ASTC4x4Unorm = 0x00000044,
        WGPUTextureFormat_ASTC4x4UnormSrgb = 0x00000045,
        WGPUTextureFormat_ASTC5x4Unorm = 0x00000046,
        WGPUTextureFormat_ASTC5x4UnormSrgb = 0x00000047,
        WGPUTextureFormat_ASTC5x5Unorm = 0x00000048,
        WGPUTextureFormat_ASTC5x5UnormSrgb = 0x00000049,
        WGPUTextureFormat_ASTC6x5Unorm = 0x0000004A,
        WGPUTextureFormat_ASTC6x5UnormSrgb = 0x0000004B,
        WGPUTextureFormat_ASTC6x6Unorm = 0x0000004C,
        WGPUTextureFormat_ASTC6x6UnormSrgb = 0x0000004D,
        WGPUTextureFormat_ASTC8x5Unorm = 0x0000004E,
        WGPUTextureFormat_ASTC8x5UnormSrgb = 0x0000004F,
        WGPUTextureFormat_ASTC8x6Unorm = 0x00000050,
        WGPUTextureFormat_ASTC8x6UnormSrgb = 0x00000051,
        WGPUTextureFormat_ASTC8x8Unorm = 0x00000052,
        WGPUTextureFormat_ASTC8x8UnormSrgb = 0x00000053,
        WGPUTextureFormat_ASTC10x5Unorm = 0x00000054,
        WGPUTextureFormat_ASTC10x5UnormSrgb = 0x00000055,
        WGPUTextureFormat_ASTC10x6Unorm = 0x00000056,
        WGPUTextureFormat_ASTC10x6UnormSrgb = 0x00000057,
        WGPUTextureFormat_ASTC10x8Unorm = 0x00000058,
        WGPUTextureFormat_ASTC10x8UnormSrgb = 0x00000059,
        WGPUTextureFormat_ASTC10x10Unorm = 0x0000005A,
        WGPUTextureFormat_ASTC10x10UnormSrgb = 0x0000005B,
        WGPUTextureFormat_ASTC12x10Unorm = 0x0000005C,
        WGPUTextureFormat_ASTC12x10UnormSrgb = 0x0000005D,
        WGPUTextureFormat_ASTC12x12Unorm = 0x0000005E,
        WGPUTextureFormat_ASTC12x12UnormSrgb = 0x0000005F,
        WGPUTextureFormat_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUTextureSampleType
    {
        WGPUTextureSampleType_Undefined = 0x00000000,
        WGPUTextureSampleType_Float = 0x00000001,
        WGPUTextureSampleType_UnfilterableFloat = 0x00000002,
        WGPUTextureSampleType_Depth = 0x00000003,
        WGPUTextureSampleType_Sint = 0x00000004,
        WGPUTextureSampleType_Uint = 0x00000005,
        WGPUTextureSampleType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUTextureViewDimension
    {
        WGPUTextureViewDimension_Undefined = 0x00000000,
        WGPUTextureViewDimension_1D = 0x00000001,
        WGPUTextureViewDimension_2D = 0x00000002,
        WGPUTextureViewDimension_2DArray = 0x00000003,
        WGPUTextureViewDimension_Cube = 0x00000004,
        WGPUTextureViewDimension_CubeArray = 0x00000005,
        WGPUTextureViewDimension_3D = 0x00000006,
        WGPUTextureViewDimension_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUVertexFormat
    {
        WGPUVertexFormat_Undefined = 0x00000000,
        WGPUVertexFormat_Uint8x2 = 0x00000001,
        WGPUVertexFormat_Uint8x4 = 0x00000002,
        WGPUVertexFormat_Sint8x2 = 0x00000003,
        WGPUVertexFormat_Sint8x4 = 0x00000004,
        WGPUVertexFormat_Unorm8x2 = 0x00000005,
        WGPUVertexFormat_Unorm8x4 = 0x00000006,
        WGPUVertexFormat_Snorm8x2 = 0x00000007,
        WGPUVertexFormat_Snorm8x4 = 0x00000008,
        WGPUVertexFormat_Uint16x2 = 0x00000009,
        WGPUVertexFormat_Uint16x4 = 0x0000000A,
        WGPUVertexFormat_Sint16x2 = 0x0000000B,
        WGPUVertexFormat_Sint16x4 = 0x0000000C,
        WGPUVertexFormat_Unorm16x2 = 0x0000000D,
        WGPUVertexFormat_Unorm16x4 = 0x0000000E,
        WGPUVertexFormat_Snorm16x2 = 0x0000000F,
        WGPUVertexFormat_Snorm16x4 = 0x00000010,
        WGPUVertexFormat_Float16x2 = 0x00000011,
        WGPUVertexFormat_Float16x4 = 0x00000012,
        WGPUVertexFormat_Float32 = 0x00000013,
        WGPUVertexFormat_Float32x2 = 0x00000014,
        WGPUVertexFormat_Float32x3 = 0x00000015,
        WGPUVertexFormat_Float32x4 = 0x00000016,
        WGPUVertexFormat_Uint32 = 0x00000017,
        WGPUVertexFormat_Uint32x2 = 0x00000018,
        WGPUVertexFormat_Uint32x3 = 0x00000019,
        WGPUVertexFormat_Uint32x4 = 0x0000001A,
        WGPUVertexFormat_Sint32 = 0x0000001B,
        WGPUVertexFormat_Sint32x2 = 0x0000001C,
        WGPUVertexFormat_Sint32x3 = 0x0000001D,
        WGPUVertexFormat_Sint32x4 = 0x0000001E,
        WGPUVertexFormat_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUVertexStepMode
    {
        WGPUVertexStepMode_Vertex = 0x00000000,
        WGPUVertexStepMode_Instance = 0x00000001,
        WGPUVertexStepMode_VertexBufferNotUsed = 0x00000002,
        WGPUVertexStepMode_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUBufferUsage
    {
        WGPUBufferUsage_None = 0x00000000,
        WGPUBufferUsage_MapRead = 0x00000001,
        WGPUBufferUsage_MapWrite = 0x00000002,
        WGPUBufferUsage_CopySrc = 0x00000004,
        WGPUBufferUsage_CopyDst = 0x00000008,
        WGPUBufferUsage_Index = 0x00000010,
        WGPUBufferUsage_Vertex = 0x00000020,
        WGPUBufferUsage_Uniform = 0x00000040,
        WGPUBufferUsage_Storage = 0x00000080,
        WGPUBufferUsage_Indirect = 0x00000100,
        WGPUBufferUsage_QueryResolve = 0x00000200,
        WGPUBufferUsage_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUColorWriteMask
    {
        WGPUColorWriteMask_None = 0x00000000,
        WGPUColorWriteMask_Red = 0x00000001,
        WGPUColorWriteMask_Green = 0x00000002,
        WGPUColorWriteMask_Blue = 0x00000004,
        WGPUColorWriteMask_Alpha = 0x00000008,
        WGPUColorWriteMask_All = 0x0000000F,
        WGPUColorWriteMask_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUMapMode
    {
        WGPUMapMode_None = 0x00000000,
        WGPUMapMode_Read = 0x00000001,
        WGPUMapMode_Write = 0x00000002,
        WGPUMapMode_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUShaderStage
    {
        WGPUShaderStage_None = 0x00000000,
        WGPUShaderStage_Vertex = 0x00000001,
        WGPUShaderStage_Fragment = 0x00000002,
        WGPUShaderStage_Compute = 0x00000004,
        WGPUShaderStage_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUTextureUsage
    {
        WGPUTextureUsage_None = 0x00000000,
        WGPUTextureUsage_CopySrc = 0x00000001,
        WGPUTextureUsage_CopyDst = 0x00000002,
        WGPUTextureUsage_TextureBinding = 0x00000004,
        WGPUTextureUsage_StorageBinding = 0x00000008,
        WGPUTextureUsage_RenderAttachment = 0x00000010,
        WGPUTextureUsage_Force32 = 0x7FFFFFFF,
    }

    public unsafe partial struct WGPUChainedStruct
    {
        [NativeTypeName("const struct WGPUChainedStruct *")]
        public WGPUChainedStruct* next;

        public WGPUSType sType;
    }

    public unsafe partial struct WGPUChainedStructOut
    {
        [NativeTypeName("struct WGPUChainedStructOut *")]
        public WGPUChainedStructOut* next;

        public WGPUSType sType;
    }

    public unsafe partial struct WGPUAdapterProperties
    {
        public WGPUChainedStructOut* nextInChain;

        [NativeTypeName("uint32_t")]
        public uint vendorID;

        [NativeTypeName("const char *")]
        public sbyte* vendorName;

        [NativeTypeName("const char *")]
        public sbyte* architecture;

        [NativeTypeName("uint32_t")]
        public uint deviceID;

        [NativeTypeName("const char *")]
        public sbyte* name;

        [NativeTypeName("const char *")]
        public sbyte* driverDescription;

        public WGPUAdapterType adapterType;

        public WGPUBackendType backendType;
    }

    public unsafe partial struct WGPUBindGroupEntry
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("uint32_t")]
        public uint binding;

        [NativeTypeName("WGPUBuffer")]
        public WGPUBufferImpl* buffer;

        [NativeTypeName("uint64_t")]
        public ulong offset;

        [NativeTypeName("uint64_t")]
        public ulong size;

        [NativeTypeName("WGPUSampler")]
        public WGPUSamplerImpl* sampler;

        [NativeTypeName("WGPUTextureView")]
        public WGPUTextureViewImpl* textureView;
    }

    public partial struct WGPUBlendComponent
    {
        public WGPUBlendOperation operation;

        public WGPUBlendFactor srcFactor;

        public WGPUBlendFactor dstFactor;
    }

    public unsafe partial struct WGPUBufferBindingLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPUBufferBindingType type;

        [NativeTypeName("WGPUBool")]
        public uint hasDynamicOffset;

        [NativeTypeName("uint64_t")]
        public ulong minBindingSize;
    }

    public unsafe partial struct WGPUBufferDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("WGPUBufferUsageFlags")]
        public uint usage;

        [NativeTypeName("uint64_t")]
        public ulong size;

        [NativeTypeName("WGPUBool")]
        public uint mappedAtCreation;
    }

    public partial struct WGPUColor
    {
        public double r;

        public double g;

        public double b;

        public double a;
    }

    public unsafe partial struct WGPUCommandBufferDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;
    }

    public unsafe partial struct WGPUCommandEncoderDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;
    }

    public unsafe partial struct WGPUCompilationMessage
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* message;

        public WGPUCompilationMessageType type;

        [NativeTypeName("uint64_t")]
        public ulong lineNum;

        [NativeTypeName("uint64_t")]
        public ulong linePos;

        [NativeTypeName("uint64_t")]
        public ulong offset;

        [NativeTypeName("uint64_t")]
        public ulong length;

        [NativeTypeName("uint64_t")]
        public ulong utf16LinePos;

        [NativeTypeName("uint64_t")]
        public ulong utf16Offset;

        [NativeTypeName("uint64_t")]
        public ulong utf16Length;
    }

    public unsafe partial struct WGPUComputePassTimestampWrites
    {
        [NativeTypeName("WGPUQuerySet")]
        public WGPUQuerySetImpl* querySet;

        [NativeTypeName("uint32_t")]
        public uint beginningOfPassWriteIndex;

        [NativeTypeName("uint32_t")]
        public uint endOfPassWriteIndex;
    }

    public unsafe partial struct WGPUConstantEntry
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* key;

        public double value;
    }

    public partial struct WGPUExtent3D
    {
        [NativeTypeName("uint32_t")]
        public uint width;

        [NativeTypeName("uint32_t")]
        public uint height;

        [NativeTypeName("uint32_t")]
        public uint depthOrArrayLayers;
    }

    public unsafe partial struct WGPUInstanceDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;
    }

    public partial struct WGPULimits
    {
        [NativeTypeName("uint32_t")]
        public uint maxTextureDimension1D;

        [NativeTypeName("uint32_t")]
        public uint maxTextureDimension2D;

        [NativeTypeName("uint32_t")]
        public uint maxTextureDimension3D;

        [NativeTypeName("uint32_t")]
        public uint maxTextureArrayLayers;

        [NativeTypeName("uint32_t")]
        public uint maxBindGroups;

        [NativeTypeName("uint32_t")]
        public uint maxBindGroupsPlusVertexBuffers;

        [NativeTypeName("uint32_t")]
        public uint maxBindingsPerBindGroup;

        [NativeTypeName("uint32_t")]
        public uint maxDynamicUniformBuffersPerPipelineLayout;

        [NativeTypeName("uint32_t")]
        public uint maxDynamicStorageBuffersPerPipelineLayout;

        [NativeTypeName("uint32_t")]
        public uint maxSampledTexturesPerShaderStage;

        [NativeTypeName("uint32_t")]
        public uint maxSamplersPerShaderStage;

        [NativeTypeName("uint32_t")]
        public uint maxStorageBuffersPerShaderStage;

        [NativeTypeName("uint32_t")]
        public uint maxStorageTexturesPerShaderStage;

        [NativeTypeName("uint32_t")]
        public uint maxUniformBuffersPerShaderStage;

        [NativeTypeName("uint64_t")]
        public ulong maxUniformBufferBindingSize;

        [NativeTypeName("uint64_t")]
        public ulong maxStorageBufferBindingSize;

        [NativeTypeName("uint32_t")]
        public uint minUniformBufferOffsetAlignment;

        [NativeTypeName("uint32_t")]
        public uint minStorageBufferOffsetAlignment;

        [NativeTypeName("uint32_t")]
        public uint maxVertexBuffers;

        [NativeTypeName("uint64_t")]
        public ulong maxBufferSize;

        [NativeTypeName("uint32_t")]
        public uint maxVertexAttributes;

        [NativeTypeName("uint32_t")]
        public uint maxVertexBufferArrayStride;

        [NativeTypeName("uint32_t")]
        public uint maxInterStageShaderComponents;

        [NativeTypeName("uint32_t")]
        public uint maxInterStageShaderVariables;

        [NativeTypeName("uint32_t")]
        public uint maxColorAttachments;

        [NativeTypeName("uint32_t")]
        public uint maxColorAttachmentBytesPerSample;

        [NativeTypeName("uint32_t")]
        public uint maxComputeWorkgroupStorageSize;

        [NativeTypeName("uint32_t")]
        public uint maxComputeInvocationsPerWorkgroup;

        [NativeTypeName("uint32_t")]
        public uint maxComputeWorkgroupSizeX;

        [NativeTypeName("uint32_t")]
        public uint maxComputeWorkgroupSizeY;

        [NativeTypeName("uint32_t")]
        public uint maxComputeWorkgroupSizeZ;

        [NativeTypeName("uint32_t")]
        public uint maxComputeWorkgroupsPerDimension;
    }

    public unsafe partial struct WGPUMultisampleState
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("uint32_t")]
        public uint count;

        [NativeTypeName("uint32_t")]
        public uint mask;

        [NativeTypeName("WGPUBool")]
        public uint alphaToCoverageEnabled;
    }

    public partial struct WGPUOrigin3D
    {
        [NativeTypeName("uint32_t")]
        public uint x;

        [NativeTypeName("uint32_t")]
        public uint y;

        [NativeTypeName("uint32_t")]
        public uint z;
    }

    public unsafe partial struct WGPUPipelineLayoutDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("size_t")]
        public nuint bindGroupLayoutCount;

        [NativeTypeName("const WGPUBindGroupLayout *")]
        public WGPUBindGroupLayoutImpl** bindGroupLayouts;
    }

    public partial struct WGPUPrimitiveDepthClipControl
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("WGPUBool")]
        public uint unclippedDepth;
    }

    public unsafe partial struct WGPUPrimitiveState
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPUPrimitiveTopology topology;

        public WGPUIndexFormat stripIndexFormat;

        public WGPUFrontFace frontFace;

        public WGPUCullMode cullMode;
    }

    public unsafe partial struct WGPUQuerySetDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        public WGPUQueryType type;

        [NativeTypeName("uint32_t")]
        public uint count;
    }

    public unsafe partial struct WGPUQueueDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;
    }

    public unsafe partial struct WGPURenderBundleDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;
    }

    public unsafe partial struct WGPURenderBundleEncoderDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("size_t")]
        public nuint colorFormatCount;

        [NativeTypeName("const WGPUTextureFormat *")]
        public WGPUTextureFormat* colorFormats;

        public WGPUTextureFormat depthStencilFormat;

        [NativeTypeName("uint32_t")]
        public uint sampleCount;

        [NativeTypeName("WGPUBool")]
        public uint depthReadOnly;

        [NativeTypeName("WGPUBool")]
        public uint stencilReadOnly;
    }

    public unsafe partial struct WGPURenderPassDepthStencilAttachment
    {
        [NativeTypeName("WGPUTextureView")]
        public WGPUTextureViewImpl* view;

        public WGPULoadOp depthLoadOp;

        public WGPUStoreOp depthStoreOp;

        public float depthClearValue;

        [NativeTypeName("WGPUBool")]
        public uint depthReadOnly;

        public WGPULoadOp stencilLoadOp;

        public WGPUStoreOp stencilStoreOp;

        [NativeTypeName("uint32_t")]
        public uint stencilClearValue;

        [NativeTypeName("WGPUBool")]
        public uint stencilReadOnly;
    }

    public partial struct WGPURenderPassDescriptorMaxDrawCount
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("uint64_t")]
        public ulong maxDrawCount;
    }

    public unsafe partial struct WGPURenderPassTimestampWrites
    {
        [NativeTypeName("WGPUQuerySet")]
        public WGPUQuerySetImpl* querySet;

        [NativeTypeName("uint32_t")]
        public uint beginningOfPassWriteIndex;

        [NativeTypeName("uint32_t")]
        public uint endOfPassWriteIndex;
    }

    public unsafe partial struct WGPURequestAdapterOptions
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUSurface")]
        public WGPUSurfaceImpl* compatibleSurface;

        public WGPUPowerPreference powerPreference;

        public WGPUBackendType backendType;

        [NativeTypeName("WGPUBool")]
        public uint forceFallbackAdapter;
    }

    public unsafe partial struct WGPUSamplerBindingLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPUSamplerBindingType type;
    }

    public unsafe partial struct WGPUSamplerDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        public WGPUAddressMode addressModeU;

        public WGPUAddressMode addressModeV;

        public WGPUAddressMode addressModeW;

        public WGPUFilterMode magFilter;

        public WGPUFilterMode minFilter;

        public WGPUMipmapFilterMode mipmapFilter;

        public float lodMinClamp;

        public float lodMaxClamp;

        public WGPUCompareFunction compare;

        [NativeTypeName("uint16_t")]
        public ushort maxAnisotropy;
    }

    public unsafe partial struct WGPUShaderModuleCompilationHint
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* entryPoint;

        [NativeTypeName("WGPUPipelineLayout")]
        public WGPUPipelineLayoutImpl* layout;
    }

    public unsafe partial struct WGPUShaderModuleSPIRVDescriptor
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("uint32_t")]
        public uint codeSize;

        [NativeTypeName("const uint32_t *")]
        public uint* code;
    }

    public unsafe partial struct WGPUShaderModuleWGSLDescriptor
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("const char *")]
        public sbyte* code;
    }

    public partial struct WGPUStencilFaceState
    {
        public WGPUCompareFunction compare;

        public WGPUStencilOperation failOp;

        public WGPUStencilOperation depthFailOp;

        public WGPUStencilOperation passOp;
    }

    public unsafe partial struct WGPUStorageTextureBindingLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPUStorageTextureAccess access;

        public WGPUTextureFormat format;

        public WGPUTextureViewDimension viewDimension;
    }

    public unsafe partial struct WGPUSurfaceCapabilities
    {
        public WGPUChainedStructOut* nextInChain;

        [NativeTypeName("size_t")]
        public nuint formatCount;

        public WGPUTextureFormat* formats;

        [NativeTypeName("size_t")]
        public nuint presentModeCount;

        public WGPUPresentMode* presentModes;

        [NativeTypeName("size_t")]
        public nuint alphaModeCount;

        public WGPUCompositeAlphaMode* alphaModes;
    }

    public unsafe partial struct WGPUSurfaceConfiguration
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUDevice")]
        public WGPUDeviceImpl* device;

        public WGPUTextureFormat format;

        [NativeTypeName("WGPUTextureUsageFlags")]
        public uint usage;

        [NativeTypeName("size_t")]
        public nuint viewFormatCount;

        [NativeTypeName("const WGPUTextureFormat *")]
        public WGPUTextureFormat* viewFormats;

        public WGPUCompositeAlphaMode alphaMode;

        [NativeTypeName("uint32_t")]
        public uint width;

        [NativeTypeName("uint32_t")]
        public uint height;

        public WGPUPresentMode presentMode;
    }

    public unsafe partial struct WGPUSurfaceDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;
    }

    public unsafe partial struct WGPUSurfaceDescriptorFromAndroidNativeWindow
    {
        public WGPUChainedStruct chain;

        public void* window;
    }

    public unsafe partial struct WGPUSurfaceDescriptorFromCanvasHTMLSelector
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("const char *")]
        public sbyte* selector;
    }

    public unsafe partial struct WGPUSurfaceDescriptorFromMetalLayer
    {
        public WGPUChainedStruct chain;

        public void* layer;
    }

    public unsafe partial struct WGPUSurfaceDescriptorFromWaylandSurface
    {
        public WGPUChainedStruct chain;

        public void* display;

        public void* surface;
    }

    public unsafe partial struct WGPUSurfaceDescriptorFromWindowsHWND
    {
        public WGPUChainedStruct chain;

        public void* hinstance;

        public void* hwnd;
    }

    public unsafe partial struct WGPUSurfaceDescriptorFromXcbWindow
    {
        public WGPUChainedStruct chain;

        public void* connection;

        [NativeTypeName("uint32_t")]
        public uint window;
    }

    public unsafe partial struct WGPUSurfaceDescriptorFromXlibWindow
    {
        public WGPUChainedStruct chain;

        public void* display;

        [NativeTypeName("uint64_t")]
        public ulong window;
    }

    public unsafe partial struct WGPUSurfaceTexture
    {
        [NativeTypeName("WGPUTexture")]
        public WGPUTextureImpl* texture;

        [NativeTypeName("WGPUBool")]
        public uint suboptimal;

        public WGPUSurfaceGetCurrentTextureStatus status;
    }

    public unsafe partial struct WGPUTextureBindingLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPUTextureSampleType sampleType;

        public WGPUTextureViewDimension viewDimension;

        [NativeTypeName("WGPUBool")]
        public uint multisampled;
    }

    public unsafe partial struct WGPUTextureDataLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("uint64_t")]
        public ulong offset;

        [NativeTypeName("uint32_t")]
        public uint bytesPerRow;

        [NativeTypeName("uint32_t")]
        public uint rowsPerImage;
    }

    public unsafe partial struct WGPUTextureViewDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        public WGPUTextureFormat format;

        public WGPUTextureViewDimension dimension;

        [NativeTypeName("uint32_t")]
        public uint baseMipLevel;

        [NativeTypeName("uint32_t")]
        public uint mipLevelCount;

        [NativeTypeName("uint32_t")]
        public uint baseArrayLayer;

        [NativeTypeName("uint32_t")]
        public uint arrayLayerCount;

        public WGPUTextureAspect aspect;
    }

    public partial struct WGPUVertexAttribute
    {
        public WGPUVertexFormat format;

        [NativeTypeName("uint64_t")]
        public ulong offset;

        [NativeTypeName("uint32_t")]
        public uint shaderLocation;
    }

    public unsafe partial struct WGPUBindGroupDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("WGPUBindGroupLayout")]
        public WGPUBindGroupLayoutImpl* layout;

        [NativeTypeName("size_t")]
        public nuint entryCount;

        [NativeTypeName("const WGPUBindGroupEntry *")]
        public WGPUBindGroupEntry* entries;
    }

    public unsafe partial struct WGPUBindGroupLayoutEntry
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("uint32_t")]
        public uint binding;

        [NativeTypeName("WGPUShaderStageFlags")]
        public uint visibility;

        public WGPUBufferBindingLayout buffer;

        public WGPUSamplerBindingLayout sampler;

        public WGPUTextureBindingLayout texture;

        public WGPUStorageTextureBindingLayout storageTexture;
    }

    public partial struct WGPUBlendState
    {
        public WGPUBlendComponent color;

        public WGPUBlendComponent alpha;
    }

    public unsafe partial struct WGPUCompilationInfo
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("size_t")]
        public nuint messageCount;

        [NativeTypeName("const WGPUCompilationMessage *")]
        public WGPUCompilationMessage* messages;
    }

    public unsafe partial struct WGPUComputePassDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("const WGPUComputePassTimestampWrites *")]
        public WGPUComputePassTimestampWrites* timestampWrites;
    }

    public unsafe partial struct WGPUDepthStencilState
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPUTextureFormat format;

        [NativeTypeName("WGPUBool")]
        public uint depthWriteEnabled;

        public WGPUCompareFunction depthCompare;

        public WGPUStencilFaceState stencilFront;

        public WGPUStencilFaceState stencilBack;

        [NativeTypeName("uint32_t")]
        public uint stencilReadMask;

        [NativeTypeName("uint32_t")]
        public uint stencilWriteMask;

        [NativeTypeName("int32_t")]
        public int depthBias;

        public float depthBiasSlopeScale;

        public float depthBiasClamp;
    }

    public unsafe partial struct WGPUImageCopyBuffer
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPUTextureDataLayout layout;

        [NativeTypeName("WGPUBuffer")]
        public WGPUBufferImpl* buffer;
    }

    public unsafe partial struct WGPUImageCopyTexture
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUTexture")]
        public WGPUTextureImpl* texture;

        [NativeTypeName("uint32_t")]
        public uint mipLevel;

        public WGPUOrigin3D origin;

        public WGPUTextureAspect aspect;
    }

    public unsafe partial struct WGPUProgrammableStageDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUShaderModule")]
        public WGPUShaderModuleImpl* module;

        [NativeTypeName("const char *")]
        public sbyte* entryPoint;

        [NativeTypeName("size_t")]
        public nuint constantCount;

        [NativeTypeName("const WGPUConstantEntry *")]
        public WGPUConstantEntry* constants;
    }

    public unsafe partial struct WGPURenderPassColorAttachment
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUTextureView")]
        public WGPUTextureViewImpl* view;

        [NativeTypeName("WGPUTextureView")]
        public WGPUTextureViewImpl* resolveTarget;

        public WGPULoadOp loadOp;

        public WGPUStoreOp storeOp;

        public WGPUColor clearValue;
    }

    public unsafe partial struct WGPURequiredLimits
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPULimits limits;
    }

    public unsafe partial struct WGPUShaderModuleDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("size_t")]
        public nuint hintCount;

        [NativeTypeName("const WGPUShaderModuleCompilationHint *")]
        public WGPUShaderModuleCompilationHint* hints;
    }

    public unsafe partial struct WGPUSupportedLimits
    {
        public WGPUChainedStructOut* nextInChain;

        public WGPULimits limits;
    }

    public unsafe partial struct WGPUTextureDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("WGPUTextureUsageFlags")]
        public uint usage;

        public WGPUTextureDimension dimension;

        public WGPUExtent3D size;

        public WGPUTextureFormat format;

        [NativeTypeName("uint32_t")]
        public uint mipLevelCount;

        [NativeTypeName("uint32_t")]
        public uint sampleCount;

        [NativeTypeName("size_t")]
        public nuint viewFormatCount;

        [NativeTypeName("const WGPUTextureFormat *")]
        public WGPUTextureFormat* viewFormats;
    }

    public unsafe partial struct WGPUVertexBufferLayout
    {
        [NativeTypeName("uint64_t")]
        public ulong arrayStride;

        public WGPUVertexStepMode stepMode;

        [NativeTypeName("size_t")]
        public nuint attributeCount;

        [NativeTypeName("const WGPUVertexAttribute *")]
        public WGPUVertexAttribute* attributes;
    }

    public unsafe partial struct WGPUBindGroupLayoutDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("size_t")]
        public nuint entryCount;

        [NativeTypeName("const WGPUBindGroupLayoutEntry *")]
        public WGPUBindGroupLayoutEntry* entries;
    }

    public unsafe partial struct WGPUColorTargetState
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public WGPUTextureFormat format;

        [NativeTypeName("const WGPUBlendState *")]
        public WGPUBlendState* blend;

        [NativeTypeName("WGPUColorWriteMaskFlags")]
        public uint writeMask;
    }

    public unsafe partial struct WGPUComputePipelineDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("WGPUPipelineLayout")]
        public WGPUPipelineLayoutImpl* layout;

        public WGPUProgrammableStageDescriptor compute;
    }

    public unsafe partial struct WGPUDeviceDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("size_t")]
        public nuint requiredFeatureCount;

        [NativeTypeName("const WGPUFeatureName *")]
        public WGPUFeatureName* requiredFeatures;

        [NativeTypeName("const WGPURequiredLimits *")]
        public WGPURequiredLimits* requiredLimits;

        public WGPUQueueDescriptor defaultQueue;

        [NativeTypeName("WGPUDeviceLostCallback")]
        public delegate* unmanaged[Cdecl]<WGPUDeviceLostReason, sbyte*, void*, void> deviceLostCallback;

        public void* deviceLostUserdata;
    }

    public unsafe partial struct WGPURenderPassDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("size_t")]
        public nuint colorAttachmentCount;

        [NativeTypeName("const WGPURenderPassColorAttachment *")]
        public WGPURenderPassColorAttachment* colorAttachments;

        [NativeTypeName("const WGPURenderPassDepthStencilAttachment *")]
        public WGPURenderPassDepthStencilAttachment* depthStencilAttachment;

        [NativeTypeName("WGPUQuerySet")]
        public WGPUQuerySetImpl* occlusionQuerySet;

        [NativeTypeName("const WGPURenderPassTimestampWrites *")]
        public WGPURenderPassTimestampWrites* timestampWrites;
    }

    public unsafe partial struct WGPUVertexState
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUShaderModule")]
        public WGPUShaderModuleImpl* module;

        [NativeTypeName("const char *")]
        public sbyte* entryPoint;

        [NativeTypeName("size_t")]
        public nuint constantCount;

        [NativeTypeName("const WGPUConstantEntry *")]
        public WGPUConstantEntry* constants;

        [NativeTypeName("size_t")]
        public nuint bufferCount;

        [NativeTypeName("const WGPUVertexBufferLayout *")]
        public WGPUVertexBufferLayout* buffers;
    }

    public unsafe partial struct WGPUFragmentState
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUShaderModule")]
        public WGPUShaderModuleImpl* module;

        [NativeTypeName("const char *")]
        public sbyte* entryPoint;

        [NativeTypeName("size_t")]
        public nuint constantCount;

        [NativeTypeName("const WGPUConstantEntry *")]
        public WGPUConstantEntry* constants;

        [NativeTypeName("size_t")]
        public nuint targetCount;

        [NativeTypeName("const WGPUColorTargetState *")]
        public WGPUColorTargetState* targets;
    }

    public unsafe partial struct WGPURenderPipelineDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        [NativeTypeName("WGPUPipelineLayout")]
        public WGPUPipelineLayoutImpl* layout;

        public WGPUVertexState vertex;

        public WGPUPrimitiveState primitive;

        [NativeTypeName("const WGPUDepthStencilState *")]
        public WGPUDepthStencilState* depthStencil;

        public WGPUMultisampleState multisample;

        [NativeTypeName("const WGPUFragmentState *")]
        public WGPUFragmentState* fragment;
    }

    public enum WGPUNativeSType
    {
        WGPUSType_DeviceExtras = 0x00030001,
        WGPUSType_RequiredLimitsExtras = 0x00030002,
        WGPUSType_PipelineLayoutExtras = 0x00030003,
        WGPUSType_ShaderModuleGLSLDescriptor = 0x00030004,
        WGPUSType_SupportedLimitsExtras = 0x00030005,
        WGPUSType_InstanceExtras = 0x00030006,
        WGPUSType_BindGroupEntryExtras = 0x00030007,
        WGPUSType_BindGroupLayoutEntryExtras = 0x00030008,
        WGPUSType_QuerySetDescriptorExtras = 0x00030009,
        WGPUSType_SurfaceConfigurationExtras = 0x0003000A,
        WGPUNativeSType_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUNativeFeature
    {
        WGPUNativeFeature_PushConstants = 0x00030001,
        WGPUNativeFeature_TextureAdapterSpecificFormatFeatures = 0x00030002,
        WGPUNativeFeature_MultiDrawIndirect = 0x00030003,
        WGPUNativeFeature_MultiDrawIndirectCount = 0x00030004,
        WGPUNativeFeature_VertexWritableStorage = 0x00030005,
        WGPUNativeFeature_TextureBindingArray = 0x00030006,
        WGPUNativeFeature_SampledTextureAndStorageBufferArrayNonUniformIndexing = 0x00030007,
        WGPUNativeFeature_PipelineStatisticsQuery = 0x00030008,
        WGPUNativeFeature_StorageResourceBindingArray = 0x00030009,
        WGPUNativeFeature_PartiallyBoundBindingArray = 0x0003000A,
        WGPUNativeFeature_Force32 = 0x7FFFFFFF,
    }

    public enum WGPULogLevel
    {
        WGPULogLevel_Off = 0x00000000,
        WGPULogLevel_Error = 0x00000001,
        WGPULogLevel_Warn = 0x00000002,
        WGPULogLevel_Info = 0x00000003,
        WGPULogLevel_Debug = 0x00000004,
        WGPULogLevel_Trace = 0x00000005,
        WGPULogLevel_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUInstanceBackend
    {
        WGPUInstanceBackend_All = 0x00000000,
        WGPUInstanceBackend_Vulkan = 1 << 0,
        WGPUInstanceBackend_GL = 1 << 1,
        WGPUInstanceBackend_Metal = 1 << 2,
        WGPUInstanceBackend_DX12 = 1 << 3,
        WGPUInstanceBackend_DX11 = 1 << 4,
        WGPUInstanceBackend_BrowserWebGPU = 1 << 5,
        WGPUInstanceBackend_Primary = WGPUInstanceBackend_Vulkan | WGPUInstanceBackend_Metal | WGPUInstanceBackend_DX12 | WGPUInstanceBackend_BrowserWebGPU,
        WGPUInstanceBackend_Secondary = WGPUInstanceBackend_GL | WGPUInstanceBackend_DX11,
        WGPUInstanceBackend_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUInstanceFlag
    {
        WGPUInstanceFlag_Default = 0x00000000,
        WGPUInstanceFlag_Debug = 1 << 0,
        WGPUInstanceFlag_Validation = 1 << 1,
        WGPUInstanceFlag_DiscardHalLabels = 1 << 2,
        WGPUInstanceFlag_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUDx12Compiler
    {
        WGPUDx12Compiler_Undefined = 0x00000000,
        WGPUDx12Compiler_Fxc = 0x00000001,
        WGPUDx12Compiler_Dxc = 0x00000002,
        WGPUDx12Compiler_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUGles3MinorVersion
    {
        WGPUGles3MinorVersion_Automatic = 0x00000000,
        WGPUGles3MinorVersion_Version0 = 0x00000001,
        WGPUGles3MinorVersion_Version1 = 0x00000002,
        WGPUGles3MinorVersion_Version2 = 0x00000003,
        WGPUGles3MinorVersion_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUPipelineStatisticName
    {
        WGPUPipelineStatisticName_VertexShaderInvocations = 0x00000000,
        WGPUPipelineStatisticName_ClipperInvocations = 0x00000001,
        WGPUPipelineStatisticName_ClipperPrimitivesOut = 0x00000002,
        WGPUPipelineStatisticName_FragmentShaderInvocations = 0x00000003,
        WGPUPipelineStatisticName_ComputeShaderInvocations = 0x00000004,
        WGPUPipelineStatisticName_Force32 = 0x7FFFFFFF,
    }

    public enum WGPUNativeQueryType
    {
        WGPUNativeQueryType_PipelineStatistics = 0x00030000,
        WGPUNativeQueryType_Force32 = 0x7FFFFFFF,
    }

    public unsafe partial struct WGPUInstanceExtras
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("WGPUInstanceBackendFlags")]
        public uint backends;

        [NativeTypeName("WGPUInstanceFlags")]
        public uint flags;

        public WGPUDx12Compiler dx12ShaderCompiler;

        public WGPUGles3MinorVersion gles3MinorVersion;

        [NativeTypeName("const char *")]
        public sbyte* dxilPath;

        [NativeTypeName("const char *")]
        public sbyte* dxcPath;
    }

    public unsafe partial struct WGPUDeviceExtras
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("const char *")]
        public sbyte* tracePath;
    }

    public partial struct WGPUNativeLimits
    {
        [NativeTypeName("uint32_t")]
        public uint maxPushConstantSize;

        [NativeTypeName("uint32_t")]
        public uint maxNonSamplerBindings;
    }

    public partial struct WGPURequiredLimitsExtras
    {
        public WGPUChainedStruct chain;

        public WGPUNativeLimits limits;
    }

    public partial struct WGPUSupportedLimitsExtras
    {
        public WGPUChainedStructOut chain;

        public WGPUNativeLimits limits;
    }

    public partial struct WGPUPushConstantRange
    {
        [NativeTypeName("WGPUShaderStageFlags")]
        public uint stages;

        [NativeTypeName("uint32_t")]
        public uint start;

        [NativeTypeName("uint32_t")]
        public uint end;
    }

    public unsafe partial struct WGPUPipelineLayoutExtras
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("size_t")]
        public nuint pushConstantRangeCount;

        [NativeTypeName("const WGPUPushConstantRange *")]
        public WGPUPushConstantRange* pushConstantRanges;
    }

    public unsafe partial struct WGPUWrappedSubmissionIndex
    {
        [NativeTypeName("WGPUQueue")]
        public WGPUQueueImpl* queue;

        [NativeTypeName("WGPUSubmissionIndex")]
        public ulong submissionIndex;
    }

    public unsafe partial struct WGPUShaderDefine
    {
        [NativeTypeName("const char *")]
        public sbyte* name;

        [NativeTypeName("const char *")]
        public sbyte* value;
    }

    public unsafe partial struct WGPUShaderModuleGLSLDescriptor
    {
        public WGPUChainedStruct chain;

        public WGPUShaderStage stage;

        [NativeTypeName("const char *")]
        public sbyte* code;

        [NativeTypeName("uint32_t")]
        public uint defineCount;

        public WGPUShaderDefine* defines;
    }

    public partial struct WGPURegistryReport
    {
        [NativeTypeName("size_t")]
        public nuint numAllocated;

        [NativeTypeName("size_t")]
        public nuint numKeptFromUser;

        [NativeTypeName("size_t")]
        public nuint numReleasedFromUser;

        [NativeTypeName("size_t")]
        public nuint numError;

        [NativeTypeName("size_t")]
        public nuint elementSize;
    }

    public partial struct WGPUHubReport
    {
        public WGPURegistryReport adapters;

        public WGPURegistryReport devices;

        public WGPURegistryReport queues;

        public WGPURegistryReport pipelineLayouts;

        public WGPURegistryReport shaderModules;

        public WGPURegistryReport bindGroupLayouts;

        public WGPURegistryReport bindGroups;

        public WGPURegistryReport commandBuffers;

        public WGPURegistryReport renderBundles;

        public WGPURegistryReport renderPipelines;

        public WGPURegistryReport computePipelines;

        public WGPURegistryReport querySets;

        public WGPURegistryReport buffers;

        public WGPURegistryReport textures;

        public WGPURegistryReport textureViews;

        public WGPURegistryReport samplers;
    }

    public partial struct WGPUGlobalReport
    {
        public WGPURegistryReport surfaces;

        public WGPUBackendType backendType;

        public WGPUHubReport vulkan;

        public WGPUHubReport metal;

        public WGPUHubReport dx12;

        public WGPUHubReport gl;
    }

    public unsafe partial struct WGPUInstanceEnumerateAdapterOptions
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUInstanceBackendFlags")]
        public uint backends;
    }

    public unsafe partial struct WGPUBindGroupEntryExtras
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("const WGPUBuffer *")]
        public WGPUBufferImpl** buffers;

        [NativeTypeName("size_t")]
        public nuint bufferCount;

        [NativeTypeName("const WGPUSampler *")]
        public WGPUSamplerImpl** samplers;

        [NativeTypeName("size_t")]
        public nuint samplerCount;

        [NativeTypeName("const WGPUTextureView *")]
        public WGPUTextureViewImpl** textureViews;

        [NativeTypeName("size_t")]
        public nuint textureViewCount;
    }

    public partial struct WGPUBindGroupLayoutEntryExtras
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("uint32_t")]
        public uint count;
    }

    public unsafe partial struct WGPUQuerySetDescriptorExtras
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("const WGPUPipelineStatisticName *")]
        public WGPUPipelineStatisticName* pipelineStatistics;

        [NativeTypeName("size_t")]
        public nuint pipelineStatisticCount;
    }

    public partial struct WGPUSurfaceConfigurationExtras
    {
        public WGPUChainedStruct chain;

        [NativeTypeName("WGPUBool")]
        public uint desiredMaximumFrameLatency;
    }

    public static unsafe partial class WGPU
    {
        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUInstance")]
        public static extern WGPUInstanceImpl* wgpuCreateInstance([NativeTypeName("const WGPUInstanceDescriptor *")] WGPUInstanceDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUProc")]
        public static extern delegate* unmanaged[Cdecl]<void> wgpuGetProcAddress([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const char *")] sbyte* procName);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint wgpuAdapterEnumerateFeatures([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, WGPUFeatureName* features);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint wgpuAdapterGetLimits([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, WGPUSupportedLimits* limits);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuAdapterGetProperties([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, WGPUAdapterProperties* properties);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint wgpuAdapterHasFeature([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, WGPUFeatureName feature);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuAdapterRequestDevice([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, [NativeTypeName("const WGPUDeviceDescriptor *")] WGPUDeviceDescriptor* descriptor, [NativeTypeName("WGPURequestDeviceCallback")] delegate* unmanaged[Cdecl]<WGPURequestDeviceStatus, WGPUDeviceImpl*, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuAdapterReference([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuAdapterRelease([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBindGroupSetLabel([NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* bindGroup, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBindGroupReference([NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* bindGroup);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBindGroupRelease([NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* bindGroup);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBindGroupLayoutSetLabel([NativeTypeName("WGPUBindGroupLayout")] WGPUBindGroupLayoutImpl* bindGroupLayout, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBindGroupLayoutReference([NativeTypeName("WGPUBindGroupLayout")] WGPUBindGroupLayoutImpl* bindGroupLayout);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBindGroupLayoutRelease([NativeTypeName("WGPUBindGroupLayout")] WGPUBindGroupLayoutImpl* bindGroupLayout);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBufferDestroy([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* wgpuBufferGetConstMappedRange([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("size_t")] nuint offset, [NativeTypeName("size_t")] nuint size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern WGPUBufferMapState wgpuBufferGetMapState([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* wgpuBufferGetMappedRange([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("size_t")] nuint offset, [NativeTypeName("size_t")] nuint size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong wgpuBufferGetSize([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBufferUsageFlags")]
        public static extern uint wgpuBufferGetUsage([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBufferMapAsync([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("WGPUMapModeFlags")] uint mode, [NativeTypeName("size_t")] nuint offset, [NativeTypeName("size_t")] nuint size, [NativeTypeName("WGPUBufferMapCallback")] delegate* unmanaged[Cdecl]<WGPUBufferMapAsyncStatus, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBufferSetLabel([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBufferUnmap([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBufferReference([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuBufferRelease([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandBufferSetLabel([NativeTypeName("WGPUCommandBuffer")] WGPUCommandBufferImpl* commandBuffer, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandBufferReference([NativeTypeName("WGPUCommandBuffer")] WGPUCommandBufferImpl* commandBuffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandBufferRelease([NativeTypeName("WGPUCommandBuffer")] WGPUCommandBufferImpl* commandBuffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUComputePassEncoder")]
        public static extern WGPUComputePassEncoderImpl* wgpuCommandEncoderBeginComputePass([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUComputePassDescriptor *")] WGPUComputePassDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPURenderPassEncoder")]
        public static extern WGPURenderPassEncoderImpl* wgpuCommandEncoderBeginRenderPass([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPURenderPassDescriptor *")] WGPURenderPassDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderClearBuffer([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderCopyBufferToBuffer([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* source, [NativeTypeName("uint64_t")] ulong sourceOffset, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* destination, [NativeTypeName("uint64_t")] ulong destinationOffset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderCopyBufferToTexture([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUImageCopyBuffer *")] WGPUImageCopyBuffer* source, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* destination, [NativeTypeName("const WGPUExtent3D *")] WGPUExtent3D* copySize);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderCopyTextureToBuffer([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* source, [NativeTypeName("const WGPUImageCopyBuffer *")] WGPUImageCopyBuffer* destination, [NativeTypeName("const WGPUExtent3D *")] WGPUExtent3D* copySize);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderCopyTextureToTexture([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* source, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* destination, [NativeTypeName("const WGPUExtent3D *")] WGPUExtent3D* copySize);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUCommandBuffer")]
        public static extern WGPUCommandBufferImpl* wgpuCommandEncoderFinish([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUCommandBufferDescriptor *")] WGPUCommandBufferDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderInsertDebugMarker([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const char *")] sbyte* markerLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderPopDebugGroup([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderPushDebugGroup([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const char *")] sbyte* groupLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderResolveQuerySet([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("uint32_t")] uint firstQuery, [NativeTypeName("uint32_t")] uint queryCount, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* destination, [NativeTypeName("uint64_t")] ulong destinationOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderSetLabel([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderWriteTimestamp([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("uint32_t")] uint queryIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderReference([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuCommandEncoderRelease([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderDispatchWorkgroups([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("uint32_t")] uint workgroupCountX, [NativeTypeName("uint32_t")] uint workgroupCountY, [NativeTypeName("uint32_t")] uint workgroupCountZ);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderDispatchWorkgroupsIndirect([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderEnd([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderInsertDebugMarker([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("const char *")] sbyte* markerLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderPopDebugGroup([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderPushDebugGroup([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("const char *")] sbyte* groupLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderSetBindGroup([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("uint32_t")] uint groupIndex, [NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* group, [NativeTypeName("size_t")] nuint dynamicOffsetCount, [NativeTypeName("const uint32_t *")] uint* dynamicOffsets);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderSetLabel([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderSetPipeline([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* pipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderReference([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderRelease([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBindGroupLayout")]
        public static extern WGPUBindGroupLayoutImpl* wgpuComputePipelineGetBindGroupLayout([NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* computePipeline, [NativeTypeName("uint32_t")] uint groupIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePipelineSetLabel([NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* computePipeline, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePipelineReference([NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* computePipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePipelineRelease([NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* computePipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBindGroup")]
        public static extern WGPUBindGroupImpl* wgpuDeviceCreateBindGroup([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUBindGroupDescriptor *")] WGPUBindGroupDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBindGroupLayout")]
        public static extern WGPUBindGroupLayoutImpl* wgpuDeviceCreateBindGroupLayout([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUBindGroupLayoutDescriptor *")] WGPUBindGroupLayoutDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBuffer")]
        public static extern WGPUBufferImpl* wgpuDeviceCreateBuffer([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUBufferDescriptor *")] WGPUBufferDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUCommandEncoder")]
        public static extern WGPUCommandEncoderImpl* wgpuDeviceCreateCommandEncoder([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUCommandEncoderDescriptor *")] WGPUCommandEncoderDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUComputePipeline")]
        public static extern WGPUComputePipelineImpl* wgpuDeviceCreateComputePipeline([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUComputePipelineDescriptor *")] WGPUComputePipelineDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDeviceCreateComputePipelineAsync([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUComputePipelineDescriptor *")] WGPUComputePipelineDescriptor* descriptor, [NativeTypeName("WGPUCreateComputePipelineAsyncCallback")] delegate* unmanaged[Cdecl]<WGPUCreatePipelineAsyncStatus, WGPUComputePipelineImpl*, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUPipelineLayout")]
        public static extern WGPUPipelineLayoutImpl* wgpuDeviceCreatePipelineLayout([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUPipelineLayoutDescriptor *")] WGPUPipelineLayoutDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUQuerySet")]
        public static extern WGPUQuerySetImpl* wgpuDeviceCreateQuerySet([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUQuerySetDescriptor *")] WGPUQuerySetDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPURenderBundleEncoder")]
        public static extern WGPURenderBundleEncoderImpl* wgpuDeviceCreateRenderBundleEncoder([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPURenderBundleEncoderDescriptor *")] WGPURenderBundleEncoderDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPURenderPipeline")]
        public static extern WGPURenderPipelineImpl* wgpuDeviceCreateRenderPipeline([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPURenderPipelineDescriptor *")] WGPURenderPipelineDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDeviceCreateRenderPipelineAsync([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPURenderPipelineDescriptor *")] WGPURenderPipelineDescriptor* descriptor, [NativeTypeName("WGPUCreateRenderPipelineAsyncCallback")] delegate* unmanaged[Cdecl]<WGPUCreatePipelineAsyncStatus, WGPURenderPipelineImpl*, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUSampler")]
        public static extern WGPUSamplerImpl* wgpuDeviceCreateSampler([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUSamplerDescriptor *")] WGPUSamplerDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUShaderModule")]
        public static extern WGPUShaderModuleImpl* wgpuDeviceCreateShaderModule([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUShaderModuleDescriptor *")] WGPUShaderModuleDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUTexture")]
        public static extern WGPUTextureImpl* wgpuDeviceCreateTexture([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUTextureDescriptor *")] WGPUTextureDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDeviceDestroy([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint wgpuDeviceEnumerateFeatures([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, WGPUFeatureName* features);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint wgpuDeviceGetLimits([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, WGPUSupportedLimits* limits);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUQueue")]
        public static extern WGPUQueueImpl* wgpuDeviceGetQueue([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint wgpuDeviceHasFeature([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, WGPUFeatureName feature);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDevicePopErrorScope([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("WGPUErrorCallback")] delegate* unmanaged[Cdecl]<WGPUErrorType, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDevicePushErrorScope([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, WGPUErrorFilter filter);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDeviceSetLabel([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDeviceSetUncapturedErrorCallback([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("WGPUErrorCallback")] delegate* unmanaged[Cdecl]<WGPUErrorType, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDeviceReference([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuDeviceRelease([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUSurface")]
        public static extern WGPUSurfaceImpl* wgpuInstanceCreateSurface([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance, [NativeTypeName("const WGPUSurfaceDescriptor *")] WGPUSurfaceDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuInstanceProcessEvents([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuInstanceRequestAdapter([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance, [NativeTypeName("const WGPURequestAdapterOptions *")] WGPURequestAdapterOptions* options, [NativeTypeName("WGPURequestAdapterCallback")] delegate* unmanaged[Cdecl]<WGPURequestAdapterStatus, WGPUAdapterImpl*, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuInstanceReference([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuInstanceRelease([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuPipelineLayoutSetLabel([NativeTypeName("WGPUPipelineLayout")] WGPUPipelineLayoutImpl* pipelineLayout, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuPipelineLayoutReference([NativeTypeName("WGPUPipelineLayout")] WGPUPipelineLayoutImpl* pipelineLayout);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuPipelineLayoutRelease([NativeTypeName("WGPUPipelineLayout")] WGPUPipelineLayoutImpl* pipelineLayout);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQuerySetDestroy([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint wgpuQuerySetGetCount([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern WGPUQueryType wgpuQuerySetGetType([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQuerySetSetLabel([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQuerySetReference([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQuerySetRelease([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQueueOnSubmittedWorkDone([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("WGPUQueueWorkDoneCallback")] delegate* unmanaged[Cdecl]<WGPUQueueWorkDoneStatus, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQueueSetLabel([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQueueSubmit([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("size_t")] nuint commandCount, [NativeTypeName("const WGPUCommandBuffer *")] WGPUCommandBufferImpl** commands);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQueueWriteBuffer([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong bufferOffset, [NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQueueWriteTexture([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* destination, [NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint dataSize, [NativeTypeName("const WGPUTextureDataLayout *")] WGPUTextureDataLayout* dataLayout, [NativeTypeName("const WGPUExtent3D *")] WGPUExtent3D* writeSize);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQueueReference([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuQueueRelease([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleSetLabel([NativeTypeName("WGPURenderBundle")] WGPURenderBundleImpl* renderBundle, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleReference([NativeTypeName("WGPURenderBundle")] WGPURenderBundleImpl* renderBundle);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleRelease([NativeTypeName("WGPURenderBundle")] WGPURenderBundleImpl* renderBundle);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderDraw([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("uint32_t")] uint vertexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstVertex, [NativeTypeName("uint32_t")] uint firstInstance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderDrawIndexed([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("uint32_t")] uint indexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstIndex, [NativeTypeName("int32_t")] int baseVertex, [NativeTypeName("uint32_t")] uint firstInstance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderDrawIndexedIndirect([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderDrawIndirect([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPURenderBundle")]
        public static extern WGPURenderBundleImpl* wgpuRenderBundleEncoderFinish([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("const WGPURenderBundleDescriptor *")] WGPURenderBundleDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderInsertDebugMarker([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("const char *")] sbyte* markerLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderPopDebugGroup([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderPushDebugGroup([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("const char *")] sbyte* groupLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderSetBindGroup([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("uint32_t")] uint groupIndex, [NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* group, [NativeTypeName("size_t")] nuint dynamicOffsetCount, [NativeTypeName("const uint32_t *")] uint* dynamicOffsets);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderSetIndexBuffer([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, WGPUIndexFormat format, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderSetLabel([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderSetPipeline([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* pipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderSetVertexBuffer([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("uint32_t")] uint slot, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderReference([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderBundleEncoderRelease([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderBeginOcclusionQuery([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint queryIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderDraw([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint vertexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstVertex, [NativeTypeName("uint32_t")] uint firstInstance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderDrawIndexed([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint indexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstIndex, [NativeTypeName("int32_t")] int baseVertex, [NativeTypeName("uint32_t")] uint firstInstance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderDrawIndexedIndirect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderDrawIndirect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderEnd([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderEndOcclusionQuery([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderExecuteBundles([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("size_t")] nuint bundleCount, [NativeTypeName("const WGPURenderBundle *")] WGPURenderBundleImpl** bundles);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderInsertDebugMarker([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("const char *")] sbyte* markerLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderPopDebugGroup([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderPushDebugGroup([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("const char *")] sbyte* groupLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetBindGroup([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint groupIndex, [NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* group, [NativeTypeName("size_t")] nuint dynamicOffsetCount, [NativeTypeName("const uint32_t *")] uint* dynamicOffsets);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetBlendConstant([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("const WGPUColor *")] WGPUColor* color);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetIndexBuffer([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, WGPUIndexFormat format, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetLabel([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetPipeline([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* pipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetScissorRect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint x, [NativeTypeName("uint32_t")] uint y, [NativeTypeName("uint32_t")] uint width, [NativeTypeName("uint32_t")] uint height);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetStencilReference([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint reference);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetVertexBuffer([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint slot, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetViewport([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, float x, float y, float width, float height, float minDepth, float maxDepth);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderReference([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderRelease([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBindGroupLayout")]
        public static extern WGPUBindGroupLayoutImpl* wgpuRenderPipelineGetBindGroupLayout([NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* renderPipeline, [NativeTypeName("uint32_t")] uint groupIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPipelineSetLabel([NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* renderPipeline, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPipelineReference([NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* renderPipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPipelineRelease([NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* renderPipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSamplerSetLabel([NativeTypeName("WGPUSampler")] WGPUSamplerImpl* sampler, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSamplerReference([NativeTypeName("WGPUSampler")] WGPUSamplerImpl* sampler);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSamplerRelease([NativeTypeName("WGPUSampler")] WGPUSamplerImpl* sampler);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuShaderModuleGetCompilationInfo([NativeTypeName("WGPUShaderModule")] WGPUShaderModuleImpl* shaderModule, [NativeTypeName("WGPUCompilationInfoCallback")] delegate* unmanaged[Cdecl]<WGPUCompilationInfoRequestStatus, WGPUCompilationInfo*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuShaderModuleSetLabel([NativeTypeName("WGPUShaderModule")] WGPUShaderModuleImpl* shaderModule, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuShaderModuleReference([NativeTypeName("WGPUShaderModule")] WGPUShaderModuleImpl* shaderModule);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuShaderModuleRelease([NativeTypeName("WGPUShaderModule")] WGPUShaderModuleImpl* shaderModule);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSurfaceConfigure([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface, [NativeTypeName("const WGPUSurfaceConfiguration *")] WGPUSurfaceConfiguration* config);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSurfaceGetCapabilities([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface, [NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, WGPUSurfaceCapabilities* capabilities);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSurfaceGetCurrentTexture([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface, WGPUSurfaceTexture* surfaceTexture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern WGPUTextureFormat wgpuSurfaceGetPreferredFormat([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface, [NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSurfacePresent([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSurfaceUnconfigure([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSurfaceReference([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSurfaceRelease([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSurfaceCapabilitiesFreeMembers(WGPUSurfaceCapabilities capabilities);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUTextureView")]
        public static extern WGPUTextureViewImpl* wgpuTextureCreateView([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture, [NativeTypeName("const WGPUTextureViewDescriptor *")] WGPUTextureViewDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuTextureDestroy([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint wgpuTextureGetDepthOrArrayLayers([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern WGPUTextureDimension wgpuTextureGetDimension([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern WGPUTextureFormat wgpuTextureGetFormat([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint wgpuTextureGetHeight([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint wgpuTextureGetMipLevelCount([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint wgpuTextureGetSampleCount([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUTextureUsageFlags")]
        public static extern uint wgpuTextureGetUsage([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint wgpuTextureGetWidth([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuTextureSetLabel([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuTextureReference([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuTextureRelease([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuTextureViewSetLabel([NativeTypeName("WGPUTextureView")] WGPUTextureViewImpl* textureView, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuTextureViewReference([NativeTypeName("WGPUTextureView")] WGPUTextureViewImpl* textureView);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuTextureViewRelease([NativeTypeName("WGPUTextureView")] WGPUTextureViewImpl* textureView);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuGenerateReport([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance, WGPUGlobalReport* report);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint wgpuInstanceEnumerateAdapters([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance, [NativeTypeName("const WGPUInstanceEnumerateAdapterOptions *")] WGPUInstanceEnumerateAdapterOptions* options, [NativeTypeName("WGPUAdapter *")] WGPUAdapterImpl** adapters);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUSubmissionIndex")]
        public static extern ulong wgpuQueueSubmitForIndex([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("size_t")] nuint commandCount, [NativeTypeName("const WGPUCommandBuffer *")] WGPUCommandBufferImpl** commands);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint wgpuDevicePoll([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("WGPUBool")] uint wait, [NativeTypeName("const WGPUWrappedSubmissionIndex *")] WGPUWrappedSubmissionIndex* wrappedSubmissionIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSetLogCallback([NativeTypeName("WGPULogCallback")] delegate* unmanaged[Cdecl]<WGPULogLevel, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuSetLogLevel(WGPULogLevel level);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint wgpuGetVersion();

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderSetPushConstants([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUShaderStageFlags")] uint stages, [NativeTypeName("uint32_t")] uint offset, [NativeTypeName("uint32_t")] uint sizeBytes, [NativeTypeName("const void *")] void* data);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderMultiDrawIndirect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint32_t")] uint count);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderMultiDrawIndexedIndirect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint32_t")] uint count);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderMultiDrawIndirectCount([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* count_buffer, [NativeTypeName("uint64_t")] ulong count_buffer_offset, [NativeTypeName("uint32_t")] uint max_count);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderMultiDrawIndexedIndirectCount([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* count_buffer, [NativeTypeName("uint64_t")] ulong count_buffer_offset, [NativeTypeName("uint32_t")] uint max_count);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderBeginPipelineStatisticsQuery([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("uint32_t")] uint queryIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuComputePassEncoderEndPipelineStatisticsQuery([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderBeginPipelineStatisticsQuery([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("uint32_t")] uint queryIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void wgpuRenderPassEncoderEndPipelineStatisticsQuery([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);
    }
}
