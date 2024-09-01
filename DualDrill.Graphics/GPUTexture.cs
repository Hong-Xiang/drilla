using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;



public sealed partial record class GPUTexture<TBackend>(GPUHandle<TBackend, GPUTexture<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}


public sealed partial class GPUTexture
{
    public unsafe GPUTextureView CreateView(GPUTextureViewDescriptor? descriptor = null)
    {
        if (!descriptor.HasValue)
        {
            return new(WGPU.TextureCreateView(Handle, null));
        }
        WGPUTextureViewDescriptor native = new()
        {
            format = descriptor.Value.Format,
            dimension = descriptor.Value.Dimension,
            baseMipLevel = (uint)descriptor.Value.BaseMipLevel,
            mipLevelCount = (uint)descriptor.Value.MipLevelCount,
            baseArrayLayer = (uint)descriptor.Value.BaseArrayLayer,
            arrayLayerCount = (uint)descriptor.Value.ArrayLayerCount,
            aspect = descriptor.Value.Aspect
        };
        return new(WGPU.TextureCreateView(Handle, &native));
    }
}
