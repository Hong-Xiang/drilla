using Microsoft.Win32.SafeHandles;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

internal sealed class VulkanApi
    : IGPUHandleDisposer<Silk.NET.Vulkan.Instance>
{
    internal readonly static Vk Api = Vk.GetApi();

    public unsafe static void DisposeGraphicsHandle(Silk.NET.Vulkan.Instance handle)
    {
        Api.DestroyInstance(handle, null);
    }
}

interface IGPUHandleDisposer<THandle>
    where THandle : unmanaged
{
    abstract static void DisposeGraphicsHandle(THandle handle);
}

unsafe sealed class GPUHandle<TManaged, THandle>(THandle Handle) : SafeHandleZeroOrMinusOneIsInvalid(true)
    where THandle : unmanaged
    where TManaged : IGPUHandleDisposer<THandle>
{
    public THandle Handle { get; } = Handle;

    public static implicit operator THandle(GPUHandle<TManaged, THandle> safeHandle) => safeHandle.Handle;
    public static implicit operator GPUHandle<TManaged, THandle>(THandle nativeHandle) => new(nativeHandle);

    protected override bool ReleaseHandle()
    {
        TManaged.DisposeGraphicsHandle(Handle);
        return true;
    }
}
