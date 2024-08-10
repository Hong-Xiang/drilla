using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Client;

[SupportedOSPlatform("browser")]
partial class BrowserClientJSInterop
{
    public const string InteropJSModuleName = "browserclient-interop";
    public const string JSUri = "/js/browserclient-interop.js";

    [JSImport("GetClientId", InteropJSModuleName)]
    internal static partial Task<string> GetClientIdAsync();

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
