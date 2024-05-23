
namespace DualDrill.Graphics;

public unsafe sealed class WebGPUGraphicsApi : IGraphicsApi<WebGPUGraphicsApi, nint>
{
    internal readonly static Silk.NET.WebGPU.WebGPU Api = Silk.NET.WebGPU.WebGPU.GetApi();

    public readonly static WebGPUGraphicsApi Instance = new();
    internal WebGPUGraphicsApi() { }

    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.CreateInstance()
    {
        var desc = new Silk.NET.WebGPU.InstanceDescriptor();
        var result = Api.CreateInstance(&desc);
        return ValueTask.FromResult((nint)result);
    }
    ValueTask IGraphicsApi<WebGPUGraphicsApi, nint>.DestroyInstance(nint instance)
    {
        Api.InstanceRelease((Silk.NET.WebGPU.Instance*)instance);
        return ValueTask.CompletedTask;
    }
    bool IGraphicsApi<WebGPUGraphicsApi, nint>.IsValidHandle(nint handle) => (void*)handle is not null;
}

public unsafe static class WebGPUGraphicsApiExtension
{
    public static Silk.NET.WebGPU.Instance* NativeHandle(this GPUInstance<WebGPUGraphicsApi, nint> instance) => (Silk.NET.WebGPU.Instance*)instance.Handle;
}
