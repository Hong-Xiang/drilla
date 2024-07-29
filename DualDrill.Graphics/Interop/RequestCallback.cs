using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics.Interop;

interface IStatusIsSuccessApi<TStatus>
{
    abstract static bool IsSuccess(TStatus status);
}

sealed class RequestCallback<TApi, TResource, TStatus>
    where TApi : IStatusIsSuccessApi<TStatus>
    where TResource : unmanaged
    where TStatus : unmanaged
{
    public unsafe static void Callback(TStatus status, TResource* resource, sbyte* message, void* data)
    {
        var result = (RequestCallbackResult<TResource, TStatus>*)data;
        if (TApi.IsSuccess(status))
        {
            result->Handle = resource;
        }
        else
        {
            result->Message = message;
        }
    }
}

unsafe struct RequestCallbackResult<TResource, TStatus>
    where TResource : unmanaged
    where TStatus : unmanaged
{
    public TResource* Handle { get; set; }
    public TStatus Status { get; set; }
    public sbyte* Message { get; set; }
}
