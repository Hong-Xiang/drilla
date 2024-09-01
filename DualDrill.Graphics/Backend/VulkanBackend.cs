using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics.Backend;

public sealed class VulkanBackend
    : IBackend<VulkanBackend>
{
    public static VulkanBackend Instance => new();
    internal readonly static Vk Api = Vk.GetApi();

    public record struct GPUInstanceDescriptor(
        string ApplicationName,
        string EngineName,
        Version32 EngineVersion,
        bool EnableValidationLayers,
        IReadOnlyList<string> RequiredExtensionNames
    )
    {
        public readonly static GPUInstanceDescriptor Default = new(
            "Unknown Application",
            "Drill Engine",
            new Version32(0, 0, 1),
            true,
            []
        );
    }

    internal static readonly string[] ValidationLayerNames = ["VK_LAYER_KHRONOS_validation"];

    sealed record class GPUInstanceData(
        ExtDebugUtils? ExtDebugUtils,
        DebugUtilsMessengerEXT? DebugUtilsMessengerEXT)
    {
    }

    unsafe public GPUInstance<VulkanBackend> CreateGPUInstance(GPUInstanceDescriptor descriptor)
    {
        using var applicationName = NativeStringRef.Create(descriptor.ApplicationName);
        using var engineName = NativeStringRef.Create(descriptor.EngineName);
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = applicationName,
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = engineName,
            EngineVersion = descriptor.EngineVersion,
            ApiVersion = Vk.Version13
        };
        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo
        };
        using var extensionNames = NativeStringArrayRef.Create(descriptor.RequiredExtensionNames);
        createInfo.EnabledExtensionCount = (uint)descriptor.RequiredExtensionNames.Count;
        createInfo.PpEnabledExtensionNames = extensionNames;
        using var layerNames = NativeStringArrayRef.Create(ValidationLayerNames);
        var debugInfoExt = new DebugUtilsMessengerCreateInfoEXT();
        PopulateDebugMessengerCreateInfo(ref debugInfoExt);
        if (descriptor.EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayerNames.Length;
            createInfo.PpEnabledLayerNames = layerNames;
            createInfo.PNext = &debugInfoExt;
        }
        if (Api.CreateInstance(in createInfo, null, out var instance) != Result.Success)
        {
            throw new GraphicsApiException<VulkanBackend>("Failed to create GPUInstance");
        }
        if (descriptor.EnableValidationLayers)
        {
            if (Api.TryGetInstanceExtension<ExtDebugUtils>(instance, out var extDebugUtils))
            {
                if (extDebugUtils?.CreateDebugUtilsMessenger(instance, in debugInfoExt, null, out var debugMessengerExt) != Result.Success)
                {
                    throw new GraphicsApiException("failed to set up debug messenger!");
                }
                var handle = new GPUHandle<VulkanBackend, GPUInstance<VulkanBackend>>(
                    instance.Handle,
                    new GPUInstanceData(extDebugUtils, debugMessengerExt)
                );
                return new(handle);
            }
        }
        return new(new GPUHandle<VulkanBackend, GPUInstance<VulkanBackend>>(
            instance.Handle,
            null
        ));
    }

    static unsafe void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
        createInfo.PfnUserCallback = new(DebugCallback);
    }
    private static unsafe uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        Console.WriteLine($"validation layer:" + Marshal.PtrToStringUTF8((nint)pCallbackData->PMessage));
        return Vk.False;
    }

    ValueTask<GPUAdapter<VulkanBackend>?> IBackend<VulkanBackend>.RequestAdapterAsync(GPUInstance<VulkanBackend> instance, GPURequestAdapterOptions options, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }


    unsafe void IGPUHandleDisposer<VulkanBackend, GPUInstance<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUInstance<VulkanBackend>> handle)
    {
        var instance = new Silk.NET.Vulkan.Instance(handle.Pointer);
        if (handle.Data is not null)
        {
            var data = (GPUInstanceData)handle.Data;
            if (data.DebugUtilsMessengerEXT.HasValue)
            {
                data.ExtDebugUtils?.DestroyDebugUtilsMessenger(instance, data.DebugUtilsMessengerEXT.Value, null);
            }
        }
        Api.DestroyInstance(instance, null);
    }

    static unsafe Silk.NET.Vulkan.Instance ToNative(GPUHandle<VulkanBackend, GPUInstance<VulkanBackend>> handle)
    {
        return new(handle.Pointer);
    }

    void IGPUHandleDisposer<VulkanBackend, GPUAdapter<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUAdapter<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUDevice<VulkanBackend>> IBackend<VulkanBackend>.RequestDeviceAsync(GPUAdapter<VulkanBackend> adapter, GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    GPUBuffer<VulkanBackend> IBackend<VulkanBackend>.CreateBuffer(GPUDevice<VulkanBackend> device, GPUBufferDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUTextureView<VulkanBackend> IBackend<VulkanBackend>.CreateTextureView(GPUTexture<VulkanBackend> texture, GPUTextureViewDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUDevice<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUDevice<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUTexture<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUTexture<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUBuffer<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUBuffer<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUTextureView<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUTextureView<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUDevice> IBackend<VulkanBackend>.RequestDeviceAsyncLegacy(GPUAdapter<VulkanBackend> adapter, GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUBindGroup<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUBindGroup<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUBindGroupLayout<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUBindGroupLayout<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUCommandBuffer<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUCommandBuffer<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUCommandEncoder<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUCommandEncoder<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUComputePassEncoder<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUComputePassEncoder<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUComputePipeline<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUComputePipeline<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUPipelineLayout<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUPipelineLayout<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUQuerySet<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUQuerySet<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUQueue<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUQueue<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPURenderBundle<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPURenderBundle<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPURenderBundleEncoder<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPURenderBundleEncoder<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPURenderPassEncoder<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPURenderPassEncoder<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPURenderPipeline<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPURenderPipeline<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUSampler<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUSampler<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUShaderModule<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUShaderModule<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<VulkanBackend, GPUSurface<VulkanBackend>>.DisposeHandle(GPUHandle<VulkanBackend, GPUSurface<VulkanBackend>> handle)
    {
        throw new NotImplementedException();
    }

    GPUTextureFormat IBackend<VulkanBackend>.GetPreferredCanvasFormat(GPUInstance<VulkanBackend> handle)
    {
        throw new NotImplementedException();
    }

    GPUTexture<VulkanBackend> IBackend<VulkanBackend>.CreateTexture(GPUDevice<VulkanBackend> handle, GPUTextureDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUSampler<VulkanBackend> IBackend<VulkanBackend>.CreateSampler(GPUDevice<VulkanBackend> handle, GPUSamplerDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUBindGroupLayout<VulkanBackend> IBackend<VulkanBackend>.CreateBindGroupLayout(GPUDevice<VulkanBackend> handle, GPUBindGroupLayoutDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUPipelineLayout<VulkanBackend> IBackend<VulkanBackend>.CreatePipelineLayout(GPUDevice<VulkanBackend> handle, GPUPipelineLayoutDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUBindGroup<VulkanBackend> IBackend<VulkanBackend>.CreateBindGroup(GPUDevice<VulkanBackend> handle, GPUBindGroupDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUShaderModule<VulkanBackend> IBackend<VulkanBackend>.CreateShaderModule(GPUDevice<VulkanBackend> handle, GPUShaderModuleDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUComputePipeline<VulkanBackend> IBackend<VulkanBackend>.CreateComputePipeline(GPUDevice<VulkanBackend> handle, GPUComputePipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPURenderPipeline<VulkanBackend> IBackend<VulkanBackend>.CreateRenderPipeline(GPUDevice<VulkanBackend> handle, GPURenderPipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUComputePipeline<VulkanBackend>> IBackend<VulkanBackend>.CreateComputePipelineAsyncAsync(GPUDevice<VulkanBackend> handle, GPUComputePipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPURenderPipeline<VulkanBackend>> IBackend<VulkanBackend>.CreateRenderPipelineAsyncAsync(GPUDevice<VulkanBackend> handle, GPURenderPipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    GPUCommandEncoder<VulkanBackend> IBackend<VulkanBackend>.CreateCommandEncoder(GPUDevice<VulkanBackend> handle, GPUCommandEncoderDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPURenderBundleEncoder<VulkanBackend> IBackend<VulkanBackend>.CreateRenderBundleEncoder(GPUDevice<VulkanBackend> handle, GPURenderBundleEncoderDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUQuerySet<VulkanBackend> IBackend<VulkanBackend>.CreateQuerySet(GPUDevice<VulkanBackend> handle, GPUQuerySetDescriptor descriptor)
    {
        throw new NotImplementedException();
    }
}

//interface IGPUHandleDisposer<THandle>
//{
//    abstract static void DisposeGraphicsHandle(THandle handle);
//}

//unsafe sealed class GPUHandle<TManaged, THandle>(THandle Handle) : SafeHandleZeroOrMinusOneIsInvalid(true)
//    where TManaged : IGPUHandleDisposer<THandle>
//{
//    public THandle Handle { get; } = Handle;

//    public static implicit operator THandle(GPUHandle<TManaged, THandle> safeHandle) => safeHandle.Handle;
//    public static implicit operator GPUHandle<TManaged, THandle>(THandle nativeHandle) => new(nativeHandle);

//    protected override bool ReleaseHandle()
//    {
//        TManaged.DisposeGraphicsHandle(Handle);
//        return true;
//    }
//}
