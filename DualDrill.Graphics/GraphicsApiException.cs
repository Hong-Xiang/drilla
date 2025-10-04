using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

//public interface IGraphicsHandle<TApi>
//    where TApi : IGraphicsApi<TApi>
//{
//}

//public interface IGraphicsApi<TApi>
//    where TApi : IGraphicsApi<TApi>
//{
//    internal bool IsValidHandle<T>(T handle) where T : IGraphicsHandle<TApi>;
//    abstract static internal IGPUInstance<TApi> CreateInstance(TApi api);
//    abstract static internal void DestroyInstance(TApi api, IGPUInstance<TApi> instance);
//    internal THandle InstanceRequestAdapter(THandle instance);
//    internal void DestroyAdapter(THandle instance);
//    internal THandle AdapterRequestDevice(THandle adapter);
//    internal THandle DeviceRequestQueue(THandle device);
//}

public class GraphicsApiException(string Message) : Exception(Message)
{
}

public class GraphicsApiException<TBackend>(string message)
    : GraphicsApiException(message)
    where TBackend : IBackend<TBackend>
{
}

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
