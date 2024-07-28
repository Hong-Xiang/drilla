using System.Runtime.InteropServices;

namespace DualDrill.Graphics.Interop
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

        public GPUAdapterType adapterType;

        public GPUBackendType backendType;
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
        public GPUBlendOperation operation;

        public GPUBlendFactor srcFactor;

        public GPUBlendFactor dstFactor;
    }

    public unsafe partial struct WGPUBufferBindingLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public GPUBufferBindingType type;

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

        public GPUCompilationMessageType type;

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

        public GPUPrimitiveTopology topology;

        public GPUIndexFormat stripIndexFormat;

        public GPUFrontFace frontFace;

        public GPUCullMode cullMode;
    }

    public unsafe partial struct WGPUQuerySetDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        public GPUQueryType type;

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
        public GPUTextureFormat* colorFormats;

        public GPUTextureFormat depthStencilFormat;

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

        public GPULoadOp depthLoadOp;

        public GPUStoreOp depthStoreOp;

        public float depthClearValue;

        [NativeTypeName("WGPUBool")]
        public uint depthReadOnly;

        public GPULoadOp stencilLoadOp;

        public GPUStoreOp stencilStoreOp;

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

        public GPUPowerPreference powerPreference;

        public GPUBackendType backendType;

        [NativeTypeName("WGPUBool")]
        public uint forceFallbackAdapter;
    }

    public unsafe partial struct WGPUSamplerBindingLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public GPUSamplerBindingType type;
    }

    public unsafe partial struct WGPUSamplerDescriptor
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("const char *")]
        public sbyte* label;

        public GPUAddressMode addressModeU;

        public GPUAddressMode addressModeV;

        public GPUAddressMode addressModeW;

        public GPUFilterMode magFilter;

        public GPUFilterMode minFilter;

        public GPUMipmapFilterMode mipmapFilter;

        public float lodMinClamp;

        public float lodMaxClamp;

        public GPUCompareFunction compare;

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
        public GPUCompareFunction compare;

        public GPUStencilOperation failOp;

        public GPUStencilOperation depthFailOp;

        public GPUStencilOperation passOp;
    }

    public unsafe partial struct WGPUStorageTextureBindingLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public GPUStorageTextureAccess access;

        public GPUTextureFormat format;

        public GPUTextureViewDimension viewDimension;
    }

    public unsafe partial struct WGPUSurfaceCapabilities
    {
        public WGPUChainedStructOut* nextInChain;

        [NativeTypeName("size_t")]
        public nuint formatCount;

        public GPUTextureFormat* formats;

        [NativeTypeName("size_t")]
        public nuint presentModeCount;

        public GPUPresentMode* presentModes;

        [NativeTypeName("size_t")]
        public nuint alphaModeCount;

        public GPUCompositeAlphaMode* alphaModes;
    }

    public unsafe partial struct WGPUSurfaceConfiguration
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        [NativeTypeName("WGPUDevice")]
        public WGPUDeviceImpl* device;

        public GPUTextureFormat format;

        [NativeTypeName("WGPUTextureUsageFlags")]
        public uint usage;

        [NativeTypeName("size_t")]
        public nuint viewFormatCount;

        [NativeTypeName("const WGPUTextureFormat *")]
        public GPUTextureFormat* viewFormats;

        public GPUCompositeAlphaMode alphaMode;

        [NativeTypeName("uint32_t")]
        public uint width;

        [NativeTypeName("uint32_t")]
        public uint height;

        public GPUPresentMode presentMode;
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

        public GPUSurfaceGetCurrentTextureStatus status;
    }

    public unsafe partial struct WGPUTextureBindingLayout
    {
        [NativeTypeName("const WGPUChainedStruct *")]
        public WGPUChainedStruct* nextInChain;

        public GPUTextureSampleType sampleType;

        public GPUTextureViewDimension viewDimension;

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

        public GPUTextureFormat format;

        public GPUTextureViewDimension dimension;

        [NativeTypeName("uint32_t")]
        public uint baseMipLevel;

        [NativeTypeName("uint32_t")]
        public uint mipLevelCount;

        [NativeTypeName("uint32_t")]
        public uint baseArrayLayer;

        [NativeTypeName("uint32_t")]
        public uint arrayLayerCount;

        public GPUTextureAspect aspect;
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

        public GPUTextureFormat format;

        [NativeTypeName("WGPUBool")]
        public uint depthWriteEnabled;

        public GPUCompareFunction depthCompare;

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

        public GPUTextureAspect aspect;
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

        public GPULoadOp loadOp;

        public GPUStoreOp storeOp;

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

        public GPUTextureDimension dimension;

        public WGPUExtent3D size;

        public GPUTextureFormat format;

        [NativeTypeName("uint32_t")]
        public uint mipLevelCount;

        [NativeTypeName("uint32_t")]
        public uint sampleCount;

        [NativeTypeName("size_t")]
        public nuint viewFormatCount;

        [NativeTypeName("const WGPUTextureFormat *")]
        public GPUTextureFormat* viewFormats;
    }

    public unsafe partial struct WGPUVertexBufferLayout
    {
        [NativeTypeName("uint64_t")]
        public ulong arrayStride;

        public GPUVertexStepMode stepMode;

        [NativeTypeName("size_t")]
        public nuint attributeCount;

        [NativeTypeName("const WGPUVertexAttribute *")]
        public GPUVertexAttribute* attributes;
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

        public GPUTextureFormat format;

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
        public GPUFeatureName* requiredFeatures;

        [NativeTypeName("const WGPURequiredLimits *")]
        public WGPURequiredLimits* requiredLimits;

        public WGPUQueueDescriptor defaultQueue;

        [NativeTypeName("WGPUDeviceLostCallback")]
        public delegate* unmanaged[Cdecl]<GPUDeviceLostReason, sbyte*, void*, void> deviceLostCallback;

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
        Force32 = 0x7FFFFFFF,
    }

    public enum WGPUNativeFeature
    {
        PushConstants = 0x00030001,
        TextureAdapterSpecificFormatFeatures = 0x00030002,
        MultiDrawIndirect = 0x00030003,
        MultiDrawIndirectCount = 0x00030004,
        VertexWritableStorage = 0x00030005,
        TextureBindingArray = 0x00030006,
        SampledTextureAndStorageBufferArrayNonUniformIndexing = 0x00030007,
        PipelineStatisticsQuery = 0x00030008,
        StorageResourceBindingArray = 0x00030009,
        PartiallyBoundBindingArray = 0x0003000A,
        Force32 = 0x7FFFFFFF,
    }

    public enum WGPULogLevel
    {
        Off = 0x00000000,
        Error = 0x00000001,
        Warn = 0x00000002,
        Info = 0x00000003,
        Debug = 0x00000004,
        Trace = 0x00000005,
        Force32 = 0x7FFFFFFF,
    }

    public enum WGPUInstanceBackend
    {
        All = 0x00000000,
        Vulkan = 1 << 0,
        GL = 1 << 1,
        Metal = 1 << 2,
        DX12 = 1 << 3,
        DX11 = 1 << 4,
        BrowserWebGPU = 1 << 5,
        Primary = Vulkan | Metal | DX12 | BrowserWebGPU,
        Secondary = GL | DX11,
        Force32 = 0x7FFFFFFF,
    }

    public enum WGPUInstanceFlag
    {
        Default = 0x00000000,
        Debug = 1 << 0,
        Validation = 1 << 1,
        DiscardHalLabels = 1 << 2,
        Force32 = 0x7FFFFFFF,
    }

    public enum WGPUDx12Compiler
    {
        Undefined = 0x00000000,
        Fxc = 0x00000001,
        Dxc = 0x00000002,
        Force32 = 0x7FFFFFFF,
    }

    public enum WGPUGles3MinorVersion
    {
        Automatic = 0x00000000,
        Version0 = 0x00000001,
        Version1 = 0x00000002,
        Version2 = 0x00000003,
        Force32 = 0x7FFFFFFF,
    }

    public enum WGPUPipelineStatisticName
    {
        VertexShaderInvocations = 0x00000000,
        ClipperInvocations = 0x00000001,
        ClipperPrimitivesOut = 0x00000002,
        FragmentShaderInvocations = 0x00000003,
        ComputeShaderInvocations = 0x00000004,
        Force32 = 0x7FFFFFFF,
    }

    public enum WGPUNativeQueryType
    {
        PipelineStatistics = 0x00030000,
        Force32 = 0x7FFFFFFF,
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

        public GPUShaderStage stage;

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

        public GPUBackendType backendType;

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
        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCreateInstance", ExactSpelling = true)]
        [return: NativeTypeName("WGPUInstance")]
        public static extern WGPUInstanceImpl* CreateInstance([NativeTypeName("const WGPUInstanceDescriptor *")] WGPUInstanceDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuGetProcAddress", ExactSpelling = true)]
        [return: NativeTypeName("WGPUProc")]
        public static extern delegate* unmanaged[Cdecl]<void> GetProcAddress([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const char *")] sbyte* procName);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuAdapterEnumerateFeatures", ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint AdapterEnumerateFeatures([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, GPUFeatureName* features);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuAdapterGetLimits", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint AdapterGetLimits([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, WGPUSupportedLimits* limits);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuAdapterGetProperties", ExactSpelling = true)]
        public static extern void AdapterGetProperties([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, WGPUAdapterProperties* properties);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuAdapterHasFeature", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint AdapterHasFeature([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, GPUFeatureName feature);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuAdapterRequestDevice", ExactSpelling = true)]
        public static extern void AdapterRequestDevice([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, [NativeTypeName("const WGPUDeviceDescriptor *")] WGPUDeviceDescriptor* descriptor, [NativeTypeName("WGPURequestDeviceCallback")] delegate* unmanaged[Cdecl]<GPURequestDeviceStatus, WGPUDeviceImpl*, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuAdapterReference", ExactSpelling = true)]
        public static extern void AdapterReference([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuAdapterRelease", ExactSpelling = true)]
        public static extern void AdapterRelease([NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBindGroupSetLabel", ExactSpelling = true)]
        public static extern void BindGroupSetLabel([NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* bindGroup, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBindGroupReference", ExactSpelling = true)]
        public static extern void BindGroupReference([NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* bindGroup);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBindGroupRelease", ExactSpelling = true)]
        public static extern void BindGroupRelease([NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* bindGroup);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBindGroupLayoutSetLabel", ExactSpelling = true)]
        public static extern void BindGroupLayoutSetLabel([NativeTypeName("WGPUBindGroupLayout")] WGPUBindGroupLayoutImpl* bindGroupLayout, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBindGroupLayoutReference", ExactSpelling = true)]
        public static extern void BindGroupLayoutReference([NativeTypeName("WGPUBindGroupLayout")] WGPUBindGroupLayoutImpl* bindGroupLayout);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBindGroupLayoutRelease", ExactSpelling = true)]
        public static extern void BindGroupLayoutRelease([NativeTypeName("WGPUBindGroupLayout")] WGPUBindGroupLayoutImpl* bindGroupLayout);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferDestroy", ExactSpelling = true)]
        public static extern void BufferDestroy([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferGetConstMappedRange", ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* BufferGetConstMappedRange([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("size_t")] nuint offset, [NativeTypeName("size_t")] nuint size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferGetMapState", ExactSpelling = true)]
        public static extern GPUBufferMapState BufferGetMapState([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferGetMappedRange", ExactSpelling = true)]
        public static extern void* BufferGetMappedRange([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("size_t")] nuint offset, [NativeTypeName("size_t")] nuint size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferGetSize", ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong BufferGetSize([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferGetUsage", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBufferUsageFlags")]
        public static extern uint BufferGetUsage([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferMapAsync", ExactSpelling = true)]
        public static extern void BufferMapAsync([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("WGPUMapModeFlags")] uint mode, [NativeTypeName("size_t")] nuint offset, [NativeTypeName("size_t")] nuint size, [NativeTypeName("WGPUBufferMapCallback")] delegate* unmanaged[Cdecl]<GPUBufferMapAsyncStatus, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferSetLabel", ExactSpelling = true)]
        public static extern void BufferSetLabel([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferUnmap", ExactSpelling = true)]
        public static extern void BufferUnmap([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferReference", ExactSpelling = true)]
        public static extern void BufferReference([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuBufferRelease", ExactSpelling = true)]
        public static extern void BufferRelease([NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandBufferSetLabel", ExactSpelling = true)]
        public static extern void CommandBufferSetLabel([NativeTypeName("WGPUCommandBuffer")] WGPUCommandBufferImpl* commandBuffer, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandBufferReference", ExactSpelling = true)]
        public static extern void CommandBufferReference([NativeTypeName("WGPUCommandBuffer")] WGPUCommandBufferImpl* commandBuffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandBufferRelease", ExactSpelling = true)]
        public static extern void CommandBufferRelease([NativeTypeName("WGPUCommandBuffer")] WGPUCommandBufferImpl* commandBuffer);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderBeginComputePass", ExactSpelling = true)]
        [return: NativeTypeName("WGPUComputePassEncoder")]
        public static extern WGPUComputePassEncoderImpl* CommandEncoderBeginComputePass([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUComputePassDescriptor *")] WGPUComputePassDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderBeginRenderPass", ExactSpelling = true)]
        [return: NativeTypeName("WGPURenderPassEncoder")]
        public static extern WGPURenderPassEncoderImpl* CommandEncoderBeginRenderPass([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPURenderPassDescriptor *")] WGPURenderPassDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderClearBuffer", ExactSpelling = true)]
        public static extern void CommandEncoderClearBuffer([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderCopyBufferToBuffer", ExactSpelling = true)]
        public static extern void CommandEncoderCopyBufferToBuffer([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* source, [NativeTypeName("uint64_t")] ulong sourceOffset, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* destination, [NativeTypeName("uint64_t")] ulong destinationOffset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderCopyBufferToTexture", ExactSpelling = true)]
        public static extern void CommandEncoderCopyBufferToTexture([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUImageCopyBuffer *")] WGPUImageCopyBuffer* source, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* destination, [NativeTypeName("const WGPUExtent3D *")] WGPUExtent3D* copySize);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderCopyTextureToBuffer", ExactSpelling = true)]
        public static extern void CommandEncoderCopyTextureToBuffer([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* source, [NativeTypeName("const WGPUImageCopyBuffer *")] WGPUImageCopyBuffer* destination, [NativeTypeName("const WGPUExtent3D *")] WGPUExtent3D* copySize);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderCopyTextureToTexture", ExactSpelling = true)]
        public static extern void CommandEncoderCopyTextureToTexture([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* source, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* destination, [NativeTypeName("const WGPUExtent3D *")] WGPUExtent3D* copySize);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderFinish", ExactSpelling = true)]
        [return: NativeTypeName("WGPUCommandBuffer")]
        public static extern WGPUCommandBufferImpl* CommandEncoderFinish([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const WGPUCommandBufferDescriptor *")] WGPUCommandBufferDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderInsertDebugMarker", ExactSpelling = true)]
        public static extern void CommandEncoderInsertDebugMarker([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const char *")] sbyte* markerLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderPopDebugGroup", ExactSpelling = true)]
        public static extern void CommandEncoderPopDebugGroup([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderPushDebugGroup", ExactSpelling = true)]
        public static extern void CommandEncoderPushDebugGroup([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const char *")] sbyte* groupLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderResolveQuerySet", ExactSpelling = true)]
        public static extern void CommandEncoderResolveQuerySet([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("uint32_t")] uint firstQuery, [NativeTypeName("uint32_t")] uint queryCount, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* destination, [NativeTypeName("uint64_t")] ulong destinationOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderSetLabel", ExactSpelling = true)]
        public static extern void CommandEncoderSetLabel([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderWriteTimestamp", ExactSpelling = true)]
        public static extern void CommandEncoderWriteTimestamp([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder, [NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("uint32_t")] uint queryIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderReference", ExactSpelling = true)]
        public static extern void CommandEncoderReference([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuCommandEncoderRelease", ExactSpelling = true)]
        public static extern void CommandEncoderRelease([NativeTypeName("WGPUCommandEncoder")] WGPUCommandEncoderImpl* commandEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderDispatchWorkgroups", ExactSpelling = true)]
        public static extern void ComputePassEncoderDispatchWorkgroups([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("uint32_t")] uint workgroupCountX, [NativeTypeName("uint32_t")] uint workgroupCountY, [NativeTypeName("uint32_t")] uint workgroupCountZ);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderDispatchWorkgroupsIndirect", ExactSpelling = true)]
        public static extern void ComputePassEncoderDispatchWorkgroupsIndirect([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderEnd", ExactSpelling = true)]
        public static extern void ComputePassEncoderEnd([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderInsertDebugMarker", ExactSpelling = true)]
        public static extern void ComputePassEncoderInsertDebugMarker([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("const char *")] sbyte* markerLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderPopDebugGroup", ExactSpelling = true)]
        public static extern void ComputePassEncoderPopDebugGroup([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderPushDebugGroup", ExactSpelling = true)]
        public static extern void ComputePassEncoderPushDebugGroup([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("const char *")] sbyte* groupLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderSetBindGroup", ExactSpelling = true)]
        public static extern void ComputePassEncoderSetBindGroup([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("uint32_t")] uint groupIndex, [NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* group, [NativeTypeName("size_t")] nuint dynamicOffsetCount, [NativeTypeName("const uint32_t *")] uint* dynamicOffsets);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderSetLabel", ExactSpelling = true)]
        public static extern void ComputePassEncoderSetLabel([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderSetPipeline", ExactSpelling = true)]
        public static extern void ComputePassEncoderSetPipeline([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* pipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderReference", ExactSpelling = true)]
        public static extern void ComputePassEncoderReference([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderRelease", ExactSpelling = true)]
        public static extern void ComputePassEncoderRelease([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePipelineGetBindGroupLayout", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBindGroupLayout")]
        public static extern WGPUBindGroupLayoutImpl* ComputePipelineGetBindGroupLayout([NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* computePipeline, [NativeTypeName("uint32_t")] uint groupIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePipelineSetLabel", ExactSpelling = true)]
        public static extern void ComputePipelineSetLabel([NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* computePipeline, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePipelineReference", ExactSpelling = true)]
        public static extern void ComputePipelineReference([NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* computePipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePipelineRelease", ExactSpelling = true)]
        public static extern void ComputePipelineRelease([NativeTypeName("WGPUComputePipeline")] WGPUComputePipelineImpl* computePipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateBindGroup", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBindGroup")]
        public static extern WGPUBindGroupImpl* DeviceCreateBindGroup([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUBindGroupDescriptor *")] WGPUBindGroupDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateBindGroupLayout", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBindGroupLayout")]
        public static extern WGPUBindGroupLayoutImpl* DeviceCreateBindGroupLayout([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUBindGroupLayoutDescriptor *")] WGPUBindGroupLayoutDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateBuffer", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBuffer")]
        public static extern WGPUBufferImpl* DeviceCreateBuffer([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUBufferDescriptor *")] WGPUBufferDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateCommandEncoder", ExactSpelling = true)]
        [return: NativeTypeName("WGPUCommandEncoder")]
        public static extern WGPUCommandEncoderImpl* DeviceCreateCommandEncoder([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUCommandEncoderDescriptor *")] WGPUCommandEncoderDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateComputePipeline", ExactSpelling = true)]
        [return: NativeTypeName("WGPUComputePipeline")]
        public static extern WGPUComputePipelineImpl* DeviceCreateComputePipeline([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUComputePipelineDescriptor *")] WGPUComputePipelineDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateComputePipelineAsync", ExactSpelling = true)]
        public static extern void DeviceCreateComputePipelineAsync([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUComputePipelineDescriptor *")] WGPUComputePipelineDescriptor* descriptor, [NativeTypeName("WGPUCreateComputePipelineAsyncCallback")] delegate* unmanaged[Cdecl]<GPUCreatePipelineAsyncStatus, WGPUComputePipelineImpl*, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreatePipelineLayout", ExactSpelling = true)]
        [return: NativeTypeName("WGPUPipelineLayout")]
        public static extern WGPUPipelineLayoutImpl* DeviceCreatePipelineLayout([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUPipelineLayoutDescriptor *")] WGPUPipelineLayoutDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateQuerySet", ExactSpelling = true)]
        [return: NativeTypeName("WGPUQuerySet")]
        public static extern WGPUQuerySetImpl* DeviceCreateQuerySet([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUQuerySetDescriptor *")] WGPUQuerySetDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateRenderBundleEncoder", ExactSpelling = true)]
        [return: NativeTypeName("WGPURenderBundleEncoder")]
        public static extern WGPURenderBundleEncoderImpl* DeviceCreateRenderBundleEncoder([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPURenderBundleEncoderDescriptor *")] WGPURenderBundleEncoderDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateRenderPipeline", ExactSpelling = true)]
        [return: NativeTypeName("WGPURenderPipeline")]
        public static extern WGPURenderPipelineImpl* DeviceCreateRenderPipeline([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPURenderPipelineDescriptor *")] WGPURenderPipelineDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateRenderPipelineAsync", ExactSpelling = true)]
        public static extern void DeviceCreateRenderPipelineAsync([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPURenderPipelineDescriptor *")] WGPURenderPipelineDescriptor* descriptor, [NativeTypeName("WGPUCreateRenderPipelineAsyncCallback")] delegate* unmanaged[Cdecl]<GPUCreatePipelineAsyncStatus, WGPURenderPipelineImpl*, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateSampler", ExactSpelling = true)]
        [return: NativeTypeName("WGPUSampler")]
        public static extern WGPUSamplerImpl* DeviceCreateSampler([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUSamplerDescriptor *")] WGPUSamplerDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateShaderModule", ExactSpelling = true)]
        [return: NativeTypeName("WGPUShaderModule")]
        public static extern WGPUShaderModuleImpl* DeviceCreateShaderModule([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUShaderModuleDescriptor *")] WGPUShaderModuleDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceCreateTexture", ExactSpelling = true)]
        [return: NativeTypeName("WGPUTexture")]
        public static extern WGPUTextureImpl* DeviceCreateTexture([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const WGPUTextureDescriptor *")] WGPUTextureDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceDestroy", ExactSpelling = true)]
        public static extern void DeviceDestroy([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceEnumerateFeatures", ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint DeviceEnumerateFeatures([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, GPUFeatureName* features);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceGetLimits", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint DeviceGetLimits([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, WGPUSupportedLimits* limits);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceGetQueue", ExactSpelling = true)]
        [return: NativeTypeName("WGPUQueue")]
        public static extern WGPUQueueImpl* DeviceGetQueue([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceHasFeature", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint DeviceHasFeature([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, GPUFeatureName feature);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDevicePopErrorScope", ExactSpelling = true)]
        public static extern void DevicePopErrorScope([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("WGPUErrorCallback")] delegate* unmanaged[Cdecl]<GPUErrorType, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDevicePushErrorScope", ExactSpelling = true)]
        public static extern void DevicePushErrorScope([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, GPUErrorFilter filter);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceSetLabel", ExactSpelling = true)]
        public static extern void DeviceSetLabel([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceSetUncapturedErrorCallback", ExactSpelling = true)]
        public static extern void DeviceSetUncapturedErrorCallback([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("WGPUErrorCallback")] delegate* unmanaged[Cdecl]<GPUErrorType, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceReference", ExactSpelling = true)]
        public static extern void DeviceReference([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDeviceRelease", ExactSpelling = true)]
        public static extern void DeviceRelease([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuInstanceCreateSurface", ExactSpelling = true)]
        [return: NativeTypeName("WGPUSurface")]
        public static extern WGPUSurfaceImpl* InstanceCreateSurface([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance, [NativeTypeName("const WGPUSurfaceDescriptor *")] WGPUSurfaceDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuInstanceProcessEvents", ExactSpelling = true)]
        public static extern void InstanceProcessEvents([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuInstanceRequestAdapter", ExactSpelling = true)]
        public static extern void InstanceRequestAdapter([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance, [NativeTypeName("const WGPURequestAdapterOptions *")] WGPURequestAdapterOptions* options, [NativeTypeName("WGPURequestAdapterCallback")] delegate* unmanaged[Cdecl]<GPURequestAdapterStatus, WGPUAdapterImpl*, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuInstanceReference", ExactSpelling = true)]
        public static extern void InstanceReference([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuInstanceRelease", ExactSpelling = true)]
        public static extern void InstanceRelease([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuPipelineLayoutSetLabel", ExactSpelling = true)]
        public static extern void PipelineLayoutSetLabel([NativeTypeName("WGPUPipelineLayout")] WGPUPipelineLayoutImpl* pipelineLayout, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuPipelineLayoutReference", ExactSpelling = true)]
        public static extern void PipelineLayoutReference([NativeTypeName("WGPUPipelineLayout")] WGPUPipelineLayoutImpl* pipelineLayout);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuPipelineLayoutRelease", ExactSpelling = true)]
        public static extern void PipelineLayoutRelease([NativeTypeName("WGPUPipelineLayout")] WGPUPipelineLayoutImpl* pipelineLayout);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQuerySetDestroy", ExactSpelling = true)]
        public static extern void QuerySetDestroy([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQuerySetGetCount", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint QuerySetGetCount([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQuerySetGetType", ExactSpelling = true)]
        public static extern GPUQueryType QuerySetGetType([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQuerySetSetLabel", ExactSpelling = true)]
        public static extern void QuerySetSetLabel([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQuerySetReference", ExactSpelling = true)]
        public static extern void QuerySetReference([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQuerySetRelease", ExactSpelling = true)]
        public static extern void QuerySetRelease([NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQueueOnSubmittedWorkDone", ExactSpelling = true)]
        public static extern void QueueOnSubmittedWorkDone([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("WGPUQueueWorkDoneCallback")] delegate* unmanaged[Cdecl]<GPUQueueWorkDoneStatus, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQueueSetLabel", ExactSpelling = true)]
        public static extern void QueueSetLabel([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQueueSubmit", ExactSpelling = true)]
        public static extern void QueueSubmit([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("size_t")] nuint commandCount, [NativeTypeName("const WGPUCommandBuffer *")] WGPUCommandBufferImpl** commands);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQueueWriteBuffer", ExactSpelling = true)]
        public static extern void QueueWriteBuffer([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong bufferOffset, [NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQueueWriteTexture", ExactSpelling = true)]
        public static extern void QueueWriteTexture([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("const WGPUImageCopyTexture *")] WGPUImageCopyTexture* destination, [NativeTypeName("const void *")] void* data, [NativeTypeName("size_t")] nuint dataSize, [NativeTypeName("const WGPUTextureDataLayout *")] WGPUTextureDataLayout* dataLayout, [NativeTypeName("const WGPUExtent3D *")] WGPUExtent3D* writeSize);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQueueReference", ExactSpelling = true)]
        public static extern void QueueReference([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQueueRelease", ExactSpelling = true)]
        public static extern void QueueRelease([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleSetLabel", ExactSpelling = true)]
        public static extern void RenderBundleSetLabel([NativeTypeName("WGPURenderBundle")] WGPURenderBundleImpl* renderBundle, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleReference", ExactSpelling = true)]
        public static extern void RenderBundleReference([NativeTypeName("WGPURenderBundle")] WGPURenderBundleImpl* renderBundle);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleRelease", ExactSpelling = true)]
        public static extern void RenderBundleRelease([NativeTypeName("WGPURenderBundle")] WGPURenderBundleImpl* renderBundle);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderDraw", ExactSpelling = true)]
        public static extern void RenderBundleEncoderDraw([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("uint32_t")] uint vertexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstVertex, [NativeTypeName("uint32_t")] uint firstInstance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderDrawIndexed", ExactSpelling = true)]
        public static extern void RenderBundleEncoderDrawIndexed([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("uint32_t")] uint indexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstIndex, [NativeTypeName("int32_t")] int baseVertex, [NativeTypeName("uint32_t")] uint firstInstance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderDrawIndexedIndirect", ExactSpelling = true)]
        public static extern void RenderBundleEncoderDrawIndexedIndirect([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderDrawIndirect", ExactSpelling = true)]
        public static extern void RenderBundleEncoderDrawIndirect([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderFinish", ExactSpelling = true)]
        [return: NativeTypeName("WGPURenderBundle")]
        public static extern WGPURenderBundleImpl* RenderBundleEncoderFinish([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("const WGPURenderBundleDescriptor *")] WGPURenderBundleDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderInsertDebugMarker", ExactSpelling = true)]
        public static extern void RenderBundleEncoderInsertDebugMarker([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("const char *")] sbyte* markerLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderPopDebugGroup", ExactSpelling = true)]
        public static extern void RenderBundleEncoderPopDebugGroup([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderPushDebugGroup", ExactSpelling = true)]
        public static extern void RenderBundleEncoderPushDebugGroup([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("const char *")] sbyte* groupLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderSetBindGroup", ExactSpelling = true)]
        public static extern void RenderBundleEncoderSetBindGroup([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("uint32_t")] uint groupIndex, [NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* group, [NativeTypeName("size_t")] nuint dynamicOffsetCount, [NativeTypeName("const uint32_t *")] uint* dynamicOffsets);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderSetIndexBuffer", ExactSpelling = true)]
        public static extern void RenderBundleEncoderSetIndexBuffer([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, GPUIndexFormat format, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderSetLabel", ExactSpelling = true)]
        public static extern void RenderBundleEncoderSetLabel([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderSetPipeline", ExactSpelling = true)]
        public static extern void RenderBundleEncoderSetPipeline([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* pipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderSetVertexBuffer", ExactSpelling = true)]
        public static extern void RenderBundleEncoderSetVertexBuffer([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder, [NativeTypeName("uint32_t")] uint slot, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderReference", ExactSpelling = true)]
        public static extern void RenderBundleEncoderReference([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderBundleEncoderRelease", ExactSpelling = true)]
        public static extern void RenderBundleEncoderRelease([NativeTypeName("WGPURenderBundleEncoder")] WGPURenderBundleEncoderImpl* renderBundleEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderBeginOcclusionQuery", ExactSpelling = true)]
        public static extern void RenderPassEncoderBeginOcclusionQuery([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint queryIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderDraw", ExactSpelling = true)]
        public static extern void RenderPassEncoderDraw([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint vertexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstVertex, [NativeTypeName("uint32_t")] uint firstInstance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderDrawIndexed", ExactSpelling = true)]
        public static extern void RenderPassEncoderDrawIndexed([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint indexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstIndex, [NativeTypeName("int32_t")] int baseVertex, [NativeTypeName("uint32_t")] uint firstInstance);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderDrawIndexedIndirect", ExactSpelling = true)]
        public static extern void RenderPassEncoderDrawIndexedIndirect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderDrawIndirect", ExactSpelling = true)]
        public static extern void RenderPassEncoderDrawIndirect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* indirectBuffer, [NativeTypeName("uint64_t")] ulong indirectOffset);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderEnd", ExactSpelling = true)]
        public static extern void RenderPassEncoderEnd([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderEndOcclusionQuery", ExactSpelling = true)]
        public static extern void RenderPassEncoderEndOcclusionQuery([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderExecuteBundles", ExactSpelling = true)]
        public static extern void RenderPassEncoderExecuteBundles([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("size_t")] nuint bundleCount, [NativeTypeName("const WGPURenderBundle *")] WGPURenderBundleImpl** bundles);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderInsertDebugMarker", ExactSpelling = true)]
        public static extern void RenderPassEncoderInsertDebugMarker([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("const char *")] sbyte* markerLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderPopDebugGroup", ExactSpelling = true)]
        public static extern void RenderPassEncoderPopDebugGroup([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderPushDebugGroup", ExactSpelling = true)]
        public static extern void RenderPassEncoderPushDebugGroup([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("const char *")] sbyte* groupLabel);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetBindGroup", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetBindGroup([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint groupIndex, [NativeTypeName("WGPUBindGroup")] WGPUBindGroupImpl* group, [NativeTypeName("size_t")] nuint dynamicOffsetCount, [NativeTypeName("const uint32_t *")] uint* dynamicOffsets);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetBlendConstant", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetBlendConstant([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("const WGPUColor *")] WGPUColor* color);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetIndexBuffer", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetIndexBuffer([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, GPUIndexFormat format, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetLabel", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetLabel([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetPipeline", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetPipeline([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* pipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetScissorRect", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetScissorRect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint x, [NativeTypeName("uint32_t")] uint y, [NativeTypeName("uint32_t")] uint width, [NativeTypeName("uint32_t")] uint height);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetStencilReference", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetStencilReference([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint reference);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetVertexBuffer", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetVertexBuffer([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("uint32_t")] uint slot, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint64_t")] ulong size);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetViewport", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetViewport([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, float x, float y, float width, float height, float minDepth, float maxDepth);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderReference", ExactSpelling = true)]
        public static extern void RenderPassEncoderReference([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderRelease", ExactSpelling = true)]
        public static extern void RenderPassEncoderRelease([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPipelineGetBindGroupLayout", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBindGroupLayout")]
        public static extern WGPUBindGroupLayoutImpl* RenderPipelineGetBindGroupLayout([NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* renderPipeline, [NativeTypeName("uint32_t")] uint groupIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPipelineSetLabel", ExactSpelling = true)]
        public static extern void RenderPipelineSetLabel([NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* renderPipeline, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPipelineReference", ExactSpelling = true)]
        public static extern void RenderPipelineReference([NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* renderPipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPipelineRelease", ExactSpelling = true)]
        public static extern void RenderPipelineRelease([NativeTypeName("WGPURenderPipeline")] WGPURenderPipelineImpl* renderPipeline);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSamplerSetLabel", ExactSpelling = true)]
        public static extern void SamplerSetLabel([NativeTypeName("WGPUSampler")] WGPUSamplerImpl* sampler, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSamplerReference", ExactSpelling = true)]
        public static extern void SamplerReference([NativeTypeName("WGPUSampler")] WGPUSamplerImpl* sampler);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSamplerRelease", ExactSpelling = true)]
        public static extern void SamplerRelease([NativeTypeName("WGPUSampler")] WGPUSamplerImpl* sampler);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuShaderModuleGetCompilationInfo", ExactSpelling = true)]
        public static extern void ShaderModuleGetCompilationInfo([NativeTypeName("WGPUShaderModule")] WGPUShaderModuleImpl* shaderModule, [NativeTypeName("WGPUCompilationInfoCallback")] delegate* unmanaged[Cdecl]<GPUCompilationInfoRequestStatus, WGPUCompilationInfo*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuShaderModuleSetLabel", ExactSpelling = true)]
        public static extern void ShaderModuleSetLabel([NativeTypeName("WGPUShaderModule")] WGPUShaderModuleImpl* shaderModule, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuShaderModuleReference", ExactSpelling = true)]
        public static extern void ShaderModuleReference([NativeTypeName("WGPUShaderModule")] WGPUShaderModuleImpl* shaderModule);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuShaderModuleRelease", ExactSpelling = true)]
        public static extern void ShaderModuleRelease([NativeTypeName("WGPUShaderModule")] WGPUShaderModuleImpl* shaderModule);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfaceConfigure", ExactSpelling = true)]
        public static extern void SurfaceConfigure([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface, [NativeTypeName("const WGPUSurfaceConfiguration *")] WGPUSurfaceConfiguration* config);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfaceGetCapabilities", ExactSpelling = true)]
        public static extern void SurfaceGetCapabilities([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface, [NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter, WGPUSurfaceCapabilities* capabilities);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfaceGetCurrentTexture", ExactSpelling = true)]
        public static extern void SurfaceGetCurrentTexture([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface, WGPUSurfaceTexture* surfaceTexture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfaceGetPreferredFormat", ExactSpelling = true)]
        public static extern GPUTextureFormat SurfaceGetPreferredFormat([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface, [NativeTypeName("WGPUAdapter")] WGPUAdapterImpl* adapter);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfacePresent", ExactSpelling = true)]
        public static extern void SurfacePresent([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfaceUnconfigure", ExactSpelling = true)]
        public static extern void SurfaceUnconfigure([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfaceReference", ExactSpelling = true)]
        public static extern void SurfaceReference([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfaceRelease", ExactSpelling = true)]
        public static extern void SurfaceRelease([NativeTypeName("WGPUSurface")] WGPUSurfaceImpl* surface);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSurfaceCapabilitiesFreeMembers", ExactSpelling = true)]
        public static extern void SurfaceCapabilitiesFreeMembers(WGPUSurfaceCapabilities capabilities);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureCreateView", ExactSpelling = true)]
        [return: NativeTypeName("WGPUTextureView")]
        public static extern WGPUTextureViewImpl* TextureCreateView([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture, [NativeTypeName("const WGPUTextureViewDescriptor *")] WGPUTextureViewDescriptor* descriptor);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureDestroy", ExactSpelling = true)]
        public static extern void TextureDestroy([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureGetDepthOrArrayLayers", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint TextureGetDepthOrArrayLayers([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureGetDimension", ExactSpelling = true)]
        public static extern GPUTextureDimension TextureGetDimension([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureGetFormat", ExactSpelling = true)]
        public static extern GPUTextureFormat TextureGetFormat([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureGetHeight", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint TextureGetHeight([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureGetMipLevelCount", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint TextureGetMipLevelCount([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureGetSampleCount", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint TextureGetSampleCount([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureGetUsage", ExactSpelling = true)]
        [return: NativeTypeName("WGPUTextureUsageFlags")]
        public static extern uint TextureGetUsage([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureGetWidth", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint TextureGetWidth([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureSetLabel", ExactSpelling = true)]
        public static extern void TextureSetLabel([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureReference", ExactSpelling = true)]
        public static extern void TextureReference([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureRelease", ExactSpelling = true)]
        public static extern void TextureRelease([NativeTypeName("WGPUTexture")] WGPUTextureImpl* texture);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureViewSetLabel", ExactSpelling = true)]
        public static extern void TextureViewSetLabel([NativeTypeName("WGPUTextureView")] WGPUTextureViewImpl* textureView, [NativeTypeName("const char *")] sbyte* label);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureViewReference", ExactSpelling = true)]
        public static extern void TextureViewReference([NativeTypeName("WGPUTextureView")] WGPUTextureViewImpl* textureView);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuTextureViewRelease", ExactSpelling = true)]
        public static extern void TextureViewRelease([NativeTypeName("WGPUTextureView")] WGPUTextureViewImpl* textureView);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuGenerateReport", ExactSpelling = true)]
        public static extern void GenerateReport([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance, WGPUGlobalReport* report);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuInstanceEnumerateAdapters", ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint InstanceEnumerateAdapters([NativeTypeName("WGPUInstance")] WGPUInstanceImpl* instance, [NativeTypeName("const WGPUInstanceEnumerateAdapterOptions *")] WGPUInstanceEnumerateAdapterOptions* options, [NativeTypeName("WGPUAdapter *")] WGPUAdapterImpl** adapters);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuQueueSubmitForIndex", ExactSpelling = true)]
        [return: NativeTypeName("WGPUSubmissionIndex")]
        public static extern ulong QueueSubmitForIndex([NativeTypeName("WGPUQueue")] WGPUQueueImpl* queue, [NativeTypeName("size_t")] nuint commandCount, [NativeTypeName("const WGPUCommandBuffer *")] WGPUCommandBufferImpl** commands);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuDevicePoll", ExactSpelling = true)]
        [return: NativeTypeName("WGPUBool")]
        public static extern uint DevicePoll([NativeTypeName("WGPUDevice")] WGPUDeviceImpl* device, [NativeTypeName("WGPUBool")] uint wait, [NativeTypeName("const WGPUWrappedSubmissionIndex *")] WGPUWrappedSubmissionIndex* wrappedSubmissionIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSetLogCallback", ExactSpelling = true)]
        public static extern void SetLogCallback([NativeTypeName("WGPULogCallback")] delegate* unmanaged[Cdecl]<WGPULogLevel, sbyte*, void*, void> callback, void* userdata);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuSetLogLevel", ExactSpelling = true)]
        public static extern void SetLogLevel(WGPULogLevel level);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuGetVersion", ExactSpelling = true)]
        [return: NativeTypeName("uint32_t")]
        public static extern uint GetVersion();

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderSetPushConstants", ExactSpelling = true)]
        public static extern void RenderPassEncoderSetPushConstants([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUShaderStageFlags")] uint stages, [NativeTypeName("uint32_t")] uint offset, [NativeTypeName("uint32_t")] uint sizeBytes, [NativeTypeName("const void *")] void* data);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderMultiDrawIndirect", ExactSpelling = true)]
        public static extern void RenderPassEncoderMultiDrawIndirect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint32_t")] uint count);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderMultiDrawIndexedIndirect", ExactSpelling = true)]
        public static extern void RenderPassEncoderMultiDrawIndexedIndirect([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("uint32_t")] uint count);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderMultiDrawIndirectCount", ExactSpelling = true)]
        public static extern void RenderPassEncoderMultiDrawIndirectCount([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* count_buffer, [NativeTypeName("uint64_t")] ulong count_buffer_offset, [NativeTypeName("uint32_t")] uint max_count);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderMultiDrawIndexedIndirectCount", ExactSpelling = true)]
        public static extern void RenderPassEncoderMultiDrawIndexedIndirectCount([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* encoder, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* buffer, [NativeTypeName("uint64_t")] ulong offset, [NativeTypeName("WGPUBuffer")] WGPUBufferImpl* count_buffer, [NativeTypeName("uint64_t")] ulong count_buffer_offset, [NativeTypeName("uint32_t")] uint max_count);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderBeginPipelineStatisticsQuery", ExactSpelling = true)]
        public static extern void ComputePassEncoderBeginPipelineStatisticsQuery([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder, [NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("uint32_t")] uint queryIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuComputePassEncoderEndPipelineStatisticsQuery", ExactSpelling = true)]
        public static extern void ComputePassEncoderEndPipelineStatisticsQuery([NativeTypeName("WGPUComputePassEncoder")] WGPUComputePassEncoderImpl* computePassEncoder);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderBeginPipelineStatisticsQuery", ExactSpelling = true)]
        public static extern void RenderPassEncoderBeginPipelineStatisticsQuery([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder, [NativeTypeName("WGPUQuerySet")] WGPUQuerySetImpl* querySet, [NativeTypeName("uint32_t")] uint queryIndex);

        [DllImport("wgpu_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wgpuRenderPassEncoderEndPipelineStatisticsQuery", ExactSpelling = true)]
        public static extern void RenderPassEncoderEndPipelineStatisticsQuery([NativeTypeName("WGPURenderPassEncoder")] WGPURenderPassEncoderImpl* renderPassEncoder);
    }
}
