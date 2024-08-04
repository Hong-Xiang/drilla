using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Client;

[SupportedOSPlatform("browser")]
internal class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        var app = builder.Build();
        using var clientModule = await JSHost.ImportAsync("client", "/js/dist/client.js");
        using var _interop = await JSHost.ImportAsync("client-interop", "/js/client-interop.js");
        await app.RunAsync();
    }
}
