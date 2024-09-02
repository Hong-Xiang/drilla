using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;


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
