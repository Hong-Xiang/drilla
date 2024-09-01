namespace DualDrill.Graphics;

//public static class GraphicsApiExtension
//{
//    public static GPUInstance<TApi, THandle> CreateInstance<TApi, THandle>(this TApi api)
//        where TApi : IGraphicsApi<TApi, THandle>
//    {
//        var handle = api.CreateInstance();
//        if (!api.IsValidHandle(handle))
//        {
//            throw new GraphicsApiException("Failed to create instance");
//        }
//        return new GPUInstance<TApi, THandle>(api, handle);
//    }
//}

public readonly record struct GPUHandle<TBackend, TResource>(nint Pointer, object? Data = null)
    where TBackend : IBackend<TBackend>
{
}

