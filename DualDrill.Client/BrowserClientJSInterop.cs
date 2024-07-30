using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Client;

[SupportedOSPlatform("browser")]
partial class BrowserClientJSInterop
{
    [JSImport("GetInteractiveServerHandle", "client-interop")]
    internal static partial Task<string> GetInteractiveServerHandle();
}
