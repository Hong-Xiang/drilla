using DualDrill.Graphics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPUSurface : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUSurfaceImpl> Handle { get; }

    internal unsafe GPUSurface(WGPUSurfaceImpl* handle)
    {
        Handle = new(handle);
    }

    public void Dispose()
    {
        Handle.Dispose();
    }

    public unsafe GPUTextureFormat PreferredFormat(GPUAdapter adapter)
    {
        var result = WGPU.SurfaceGetPreferredFormat(Handle, adapter.Handle);
        return result;
    }

    public unsafe void Present()
    {
        WGPU.SurfacePresent(Handle);
    }

    public static unsafe GPUSurface Create(Silk.NET.Windowing.IView view, GPUInstance instance)
    {
        WGPUSurfaceDescriptor descriptor = new();
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND = new();
        surfaceDescriptorFromWindowsHWND.chain = new Interop.WGPUChainedStruct
        {
            next = null,
            sType = WGPUSType.SurfaceDescriptorFromWindowsHWND
        };
        surfaceDescriptorFromWindowsHWND.hwnd = (void*)(((IntPtr, IntPtr, IntPtr)?)view.Native.Win32).Value.Item1;
        surfaceDescriptorFromWindowsHWND.hinstance = (void*)(((IntPtr, IntPtr, IntPtr)?)view.Native.Win32).Value.Item3;
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND2 = surfaceDescriptorFromWindowsHWND;
        descriptor.nextInChain = (Interop.WGPUChainedStruct*)(&surfaceDescriptorFromWindowsHWND);
        WGPUSurfaceImpl* result = WGPU.InstanceCreateSurface(instance.Handle, &descriptor);
        if (result is null)
        {
            throw new GraphicsApiException("Failed to create surface");
        }
        return new(result);
    }

    public unsafe GPUSurfaceTexture GetCurrentTexture()
    {
        WGPUSurfaceTexture texture;
        WGPU.SurfaceGetCurrentTexture(Handle, &texture);
        var result = new GPUSurfaceTexture
        {
            Texture = new GPUTexture(texture.texture),
            Status = texture.status,
            Suboptimal = texture.suboptimal != 0
        };
        if (result.Status != GPUSurfaceGetCurrentTextureStatus.Success)
        {
            throw new GraphicsApiException($"Failed to get current texture, status {result.Status}");
        }
        return result;
    }

    public unsafe void Configure(GPUSurfaceConfiguration configuration)
    {
        fixed (GPUTextureFormat* viewFormats = configuration.ViewFormats)
        {
            var nativeConfig = new WGPUSurfaceConfiguration
            {
                device = configuration.Device.Handle,
                format = configuration.Format,
                usage = (uint)configuration.Usage,
                viewFormatCount = (nuint)configuration.ViewFormats.Length,
                viewFormats = viewFormats,
                alphaMode = configuration.AlphaMode,
                width = (uint)configuration.Width,
                height = (uint)configuration.Height,
                presentMode = configuration.PresentMode
            };
            WGPU.SurfaceConfigure(Handle, &nativeConfig);
        }
    }

    public unsafe void Unconfigure()
    {
        WGPU.SurfaceUnconfigure(Handle);
    }
}
