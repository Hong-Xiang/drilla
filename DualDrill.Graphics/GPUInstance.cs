using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DualDrill.Graphics.Native;
using DualDrill.Graphics.WebGPU.Native;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using static DualDrill.Graphics.VulkanApi;

namespace DualDrill.Graphics;

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


public sealed partial class GPUInstanceW : IDisposable
{
    public unsafe GPUInstanceW()
    {
        WGPUInstanceDescriptor descriptor = new();
        var pointer = WGPU.CreateInstance(&descriptor);
        Handle = new(pointer);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void RequestAdaptorCallback(GPURequestAdapterStatus status, WGPUAdapterImpl* adapter, sbyte* message, void* data)
    {
        RequestCallback<WGPUApiWrapper, WGPUAdapterImpl, GPURequestAdapterStatus>.Callback(status, adapter, message, data);
    }

    public unsafe GPUAdapter RequestAdapter(GPUSurface? surface)
    {
        var options = new WGPURequestAdapterOptions
        {
            powerPreference = GPUPowerPreference.HighPerformance
        };
        if (surface is not null)
        {
            options.compatibleSurface = surface.Handle;
        }
        var result = new RequestCallbackResult<WGPUAdapterImpl, GPURequestAdapterStatus>();
        WGPU.InstanceRequestAdapter(
            Handle,
            &options,
            &RequestAdaptorCallback,
            &result
        );
        if (result.Handle is null)
        {
            throw new GraphicsApiException($"Request {nameof(GPUAdapter)} failed, status {result.Status}, message {Marshal.PtrToStringUTF8((nint)result.Message)}");
        }
        return new GPUAdapter(result.Handle);
    }

}

public sealed partial class GPUInstance
    : IDisposable
{
    static readonly string[] ValidationLayerNames = ["VK_LAYER_KHRONOS_validation"];
    public unsafe GPUInstance(GPUInstanceDescriptor descriptor)
    {
        using var applicationName = NativeStringRef.Create(descriptor.ApplicationName);
        using var engineName = NativeStringRef.Create(descriptor.EngineName);
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = applicationName,
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = engineName,
            EngineVersion = new Version32(1, 0, 0),
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
            throw new GraphicsApiException("Failed to create GPUInstance using Vulkan API");
        }
        InstanceHandle = instance;
        if (descriptor.EnableValidationLayers)
        {
            if (VulkanApi.Api.TryGetInstanceExtension<ExtDebugUtils>(instance, out ExtDebugUtils))
            {
                if (ExtDebugUtils?.CreateDebugUtilsMessenger(instance, in debugInfoExt, null, out var debugMessengerExt) != Result.Success)
                {
                    throw new Exception("failed to set up debug messenger!");
                }
                DebugUtilsMessengerEXT = debugMessengerExt;
            }
        }
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
        createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
    }

    public Silk.NET.Vulkan.Instance NativeHandle => InstanceHandle;
    internal GPUHandle<VulkanApi, Silk.NET.Vulkan.Instance> InstanceHandle;
    internal ExtDebugUtils? ExtDebugUtils;
    public DebugUtilsMessengerEXT? DebugUtilsMessengerEXT { get; }

    private static unsafe uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        Console.WriteLine($"validation layer:" + Marshal.PtrToStringUTF8((nint)pCallbackData->PMessage));
        return Vk.False;
    }

    public unsafe void Dispose()
    {
        InstanceHandle.Dispose();
        if (DebugUtilsMessengerEXT.HasValue)
        {
            ExtDebugUtils?.DestroyDebugUtilsMessenger(InstanceHandle, DebugUtilsMessengerEXT.Value, null);
        }
    }
}
