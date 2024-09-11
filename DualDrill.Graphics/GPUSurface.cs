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


