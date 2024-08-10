using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Client;

[SupportedOSPlatform("browser")]
partial class ClientJSInterop
{
    public const string InteropJSModuleName = "client";
    public const string JSUri = "/js/dist/client.js";

    [JSImport("callFunction", InteropJSModuleName)]
    internal static partial JSObject CallFunction(JSObject f, JSObject cls);

    [JSImport("newOperator", InteropJSModuleName)]
    internal static partial JSObject NewOperator(JSObject f);

    [JSImport("getDotnetWasmExports", InteropJSModuleName)]
    internal static partial Task<JSObject> GetDotnetWasmExports(string assemblyName);
    [JSImport("testDotnetExport", InteropJSModuleName)]
    internal static partial Task TestDotnetExport();

    [JSImport("appendToVideoTarget", InteropJSModuleName)]
    internal static partial void AppendToVideoTarget(JSObject el);

    [JSImport("globalThis.console.log")]
    internal static partial void JSConsoleLog(JSObject obj);

    [JSImport("globalThis.document.getElementById")]
    internal static partial JSObject GetElementById(string id);


    [JSImport("SignalRHubConnectionSubscribeEmitEvent", InteropJSModuleName)]
    internal static partial JSObject SignalRHubConnectionSubscribeEmitEvent(
        [JSMarshalAs<JSType.Function<JSType.String>>]
        Action<string> handler);

    [JSImport("StartSignalRHubConnection", InteropJSModuleName)]
    internal static partial Task StartSignalRHubConnection(string clientId);

    [JSImport("CreateSimpleRTCClient", InteropJSModuleName)]
    internal static partial Task<JSObject> CreateSimpleRTCClientAsync(string clientId);

    [JSImport("appendChild", InteropJSModuleName)]
    internal static partial void AppendChild(JSObject el, JSObject child);

    [JSExport]
    internal static string TestJSExport()
    {
        Console.WriteLine($"Calling {nameof(TestJSExport)}");
        return "Hello from DotNet Blazor WASM";
    }
}
