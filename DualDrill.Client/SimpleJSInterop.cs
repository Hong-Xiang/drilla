using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Client;

[SupportedOSPlatform("browser")]
partial class SimpleJSInterop
{
    [JSImport("globalThis.console.log")]
    internal static partial void Log(JSObject obj);

    [JSImport("callFunction", "client")]
    internal static partial JSObject CallFunction(JSObject f, JSObject cls);

    [JSImport("newOperator", "client")]
    internal static partial JSObject NewOperator(JSObject f);
}
