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

    ValueTask<GPUAdapter<VulkanBackend>> IBackend<VulkanBackend>.RequestAdapterAsync(GPUInstance<VulkanBackend> instance, GPURequestAdapterOptions options, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUDevice> IBackend<VulkanBackend>.RequestDeviceAsync(GPUAdapter<VulkanBackend> adapter, GPUDeviceDescriptor descriptor, CancellationToken cancellation)
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
