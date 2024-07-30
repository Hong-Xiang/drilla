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
        builder.Services.AddSingleton<DualDrillBrowserSignalRClientService>();
        var app = builder.Build();
        using var clientModule = await JSHost.ImportAsync("client", "/client.js");
        await app.RunAsync();
    }
}
