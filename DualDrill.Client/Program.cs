using DualDrill.Engine.Connection;
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
        using var clientModule = await JSHost.ImportAsync(ClientJSInterop.InteropJSModuleName, ClientJSInterop.JSUri);
        using var _interop = await JSHost.ImportAsync(BrowserClientJSInterop.InteropJSModuleName, BrowserClientJSInterop.JSUri);
        var clientId = await BrowserClientJSInterop.GetClientIdAsync();

        builder.Services.AddSingleton(sp => new ClientIdentity(Guid.Parse(clientId)));
        builder.Services.AddHttpClient();

        var app = builder.Build();
        await app.RunAsync();
    }
}
