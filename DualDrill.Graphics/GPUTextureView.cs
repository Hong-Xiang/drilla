using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;


public partial interface IGPUTextureView : IDisposable
{

}

public sealed partial record class GPUTextureView<TBackend>(GPUHandle<TBackend, GPUTextureView<TBackend>> Handle)
    : IDisposable, IGPUTextureView
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}



public sealed partial class GPUTextureView : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUTextureViewImpl> Handle { get; }
    internal unsafe GPUTextureView(WGPUTextureViewImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUTextureViewImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
