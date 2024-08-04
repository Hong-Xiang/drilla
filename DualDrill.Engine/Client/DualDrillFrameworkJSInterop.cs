using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Engine.Client;

[SupportedOSPlatform("browser")]
public partial class DualDrillFrameworkJSInterop
{
    [JSExport]
    internal static string JSObjectAllocHandle(JSObject target)
    {
        var handle = GCHandle.Alloc(target, GCHandleType.Normal);
        return GCHandle.ToIntPtr(handle).ToString();
    }

    [JSExport]
    internal static void JSObjectFreeHandle(string objectHandle)
    {
        var handle = GCHandle.FromIntPtr(nint.Parse(objectHandle));
        handle.Free();
    }

    [JSExport]
    internal static JSObject GetJSObjectFromHandle(string objectHandle)
    {
        var handle = GCHandle.FromIntPtr(nint.Parse(objectHandle));
        return (JSObject)handle.Target!;
    }
}
