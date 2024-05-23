using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public interface IGraphicsApi<TApi, THandle>
    where TApi : IGraphicsApi<TApi, THandle>
{
    internal bool IsValidHandle(THandle handle);
    internal ValueTask<THandle> CreateInstance();
    internal ValueTask DestroyInstance(THandle instance);
}

public sealed class GraphicsApiException(string Message) : Exception(Message)
{
}

public static class GraphicsApiExtension
{
    public static async ValueTask<GPUInstance<TApi, THandle>> CreateInstance<TApi, THandle>(this TApi api)
        where TApi : IGraphicsApi<TApi, THandle>
    {
        var handle = await api.CreateInstance().ConfigureAwait(false);
        if (!api.IsValidHandle(handle))
        {
            throw new GraphicsApiException("Failed to create instance");
        }
        return new GPUInstance<TApi, THandle>(api, handle);
    }
}
