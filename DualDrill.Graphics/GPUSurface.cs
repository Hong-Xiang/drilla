using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;

public partial interface IGPUSurface : IDisposable
{
    //GPUTexture? GetCurrentTextureLegacy();
    IGPUTexture? GetCurrentTexture();
    void Configure(GPUSurfaceConfiguration configuration);
    void Unconfigure();
    void Present();
}

public sealed partial record class GPUSurface<TBackend>(GPUHandle<TBackend, GPUSurface<TBackend>> Handle)
    : IGPUSurface, IDisposable
    where TBackend : IBackend<TBackend>
{

    public void Configure(
     GPUSurfaceConfiguration configuration
    )
    {
        TBackend.Instance.Configure(this, configuration);
    }

    public void Unconfigure(
    )
    {
        TBackend.Instance.Unconfigure(this);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }

    public IGPUTexture? GetCurrentTexture()
    {
        return TBackend.Instance.GetCurrentTexture(this);
    }

    public void Present()
    {
        TBackend.Instance.Present(this);
    }
}



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

    //public unsafe void Configure(GPUSurfaceConfiguration configuration)
    //{
    //    fixed (GPUTextureFormat* viewFormats = configuration.ViewFormats)
    //    {
    //        var nativeConfig = new WGPUSurfaceConfiguration
    //        {
    //            device = configuration.Device.Handle,
    //            format = configuration.Format,
    //            usage = (uint)configuration.Usage,
    //            viewFormatCount = (nuint)configuration.ViewFormats.Length,
    //            viewFormats = viewFormats,
    //            alphaMode = configuration.AlphaMode,
    //            width = (uint)configuration.Width,
    //            height = (uint)configuration.Height,
    //            presentMode = configuration.PresentMode
    //        };
    //        WGPU.SurfaceConfigure(Handle, &nativeConfig);
    //    }
    //}

    public unsafe void Unconfigure()
    {
        WGPU.SurfaceUnconfigure(Handle);
    }
}
