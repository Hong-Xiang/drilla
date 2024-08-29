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
