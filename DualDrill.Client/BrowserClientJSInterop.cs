using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Client;

[SupportedOSPlatform("browser")]
partial class BrowserClientJSInterop
{
    [JSImport("GetInteractiveServerHandle", "client-interop")]
    internal static partial Task<string> GetInteractiveServerHandle();

    [JSExport]
    internal static nint CreateJSObjectReference(JSObject target)
    {
        var handle = GCHandle.Alloc(target);
        return GCHandle.ToIntPtr(handle);
    }

    [JSExport]
    internal static void FreeJSObjectReference(nint pointer)
    {
        var handle = GCHandle.FromIntPtr(pointer);
        if (handle.IsAllocated)
        {
            handle.Free();
        }
    }
}
