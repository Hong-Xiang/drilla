using Microsoft.AspNetCore.Components;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Client;

[SupportedOSPlatform("browser")]
partial class SimpleJSInterop
{
    [JSImport("globalThis.console.log")]
    internal static partial void Log(JSObject obj);

    [JSImport("globalThis.console.log")]
    internal static partial void LogObject([JSMarshalAs<JSType.Any>] object obj);

    [JSImport("globalThis.console.log")]
    internal static partial void LogMemoryViewData([JSMarshalAs<JSType.MemoryView>] Span<int> data);


    [JSImport("callFunction", "client")]
    internal static partial JSObject CallFunction(JSObject f, JSObject cls);

    [JSImport("newOperator", "client")]
    internal static partial JSObject NewOperator(JSObject f);

    [JSImport("CreateSimpleRTCClient", "client")]
    internal static partial Task<JSObject> CreateSimpleRTCClientAsync();

    [JSImport("getDotnetWasmExports", "client")]
    internal static partial Task<JSObject> GetDotnetWasmExports(string assemblyName);
    [JSImport("testDotnetExport", "client")]
    internal static partial Task TestDotnetExport();
  
    [JSImport("StartSignalR", "client")]
    internal static partial Task StartSignalRAsync();

    [JSImport("appendToVideoTarget", "client")]
    internal static partial void AppendToVideoTarget(JSObject el);

    [JSExport]
    internal static string TestJSExport()
    {
        Console.WriteLine($"Calling {nameof(TestJSExport)}");
        return "Hello from DotNet Blazor WASM";
    }

    [JSExport]
    internal static void SetInteractiveServerHandle(string handle)
    {
        InteractiveServerHandle.SetHandle(handle);
    }

    [JSExport]
    internal static nint JSObjectAllocHandle(JSObject target)
    {
        var handle = GCHandle.Alloc(target, GCHandleType.Normal);
        return GCHandle.ToIntPtr(handle);
    }

    [JSExport]
    internal static JSObject GetJSObjectFromHandle(nint ptr)
    {
        var handle = GCHandle.FromIntPtr(ptr);
        return (JSObject)handle.Target!;
    }
}
