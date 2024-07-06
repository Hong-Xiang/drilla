using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace DualDrill.Server.WebApi;

sealed class JSObjectReferenceHandle : IDisposable
{
    static readonly string Scheme = "ddh";
    GCHandle Handle;
    private bool disposed;
    public bool Disposed => disposed;
    public Uri Uri => new Uri($"https://localhost:7117//jsobjectreference/{GCHandle.ToIntPtr(Handle)}");
    public static JSObjectReferenceHandle FromUri(Uri uri)
    {
        if (uri.Scheme != "https")
        {
        }
        throw new NotImplementedException();
    }

    public JSObjectReferenceHandle()
    {
        Handle = GCHandle.Alloc(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
            }
            Handle.Free();
            disposed = true;
        }
    }

    ~JSObjectReferenceHandle()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

[Route("api/[controller]")]
[ApiController]
public class ObjectReferenceHandleController : ControllerBase
{
    [HttpPost]
    async Task<string> Alloc()
    {
        return "ok";
    }
}
