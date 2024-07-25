using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Disposable;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Collections.Concurrent;

namespace DualDrill.Server.Browser;

public static class ClientHubEndpointExtension
{
    static Ok<string[]> GetConnectedClients([FromServices] ClientStore clients)
    {
        return TypedResults.Ok(clients.ClientUris.Select(static s => s.ToString()).ToArray());
    }

    public static void AddClients(this IServiceCollection services)
    {
        services.AddSingleton<ClientStore>();

        services.AddScoped<ContextDisposableCollection>();
        services.AddScoped<ContextAsyncDisposableCollection>();
        //services.AddScoped<InitializedClientContext>();

        //services.AddScoped(async sp =>
        //{
        //    var disposables = sp.GetRequiredService<ContextAsyncDisposableCollection>();
        //    var client = await JSClientModule.CreateAsync(sp.GetRequiredService<IJSRuntime>());
        //    disposables.Add(client);
        //    return client;
        //});

        //services.AddKeyedScoped(typeof(InitializedClientContext), (sp, level) =>
        //{
        //    var context = sp.GetRequiredService<InitializedClientContext>();
        //    return context.ClientModule ?? throw new NullReferenceException(nameof(context.ClientModule));
        //});

        //services.AddScoped<MediaDevices>();
        //services.AddScoped<JSClientModule>();
        //services.AddScoped<BrowserClient>();
        //services.AddScoped<IClient>(sp => sp.GetRequiredService<BrowserClient>());
        //services.AddScoped<CircuitService>();
        //services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<CircuitService>());
    }

    public static void MapClients(this WebApplication app)
    {
        app.MapGet("/api/clients", GetConnectedClients);
        app.MapGet("/api/injecthub/", ([FromServices] IHubContext<DrillHub, IDrillHubClient> x) =>
        {
            return "ok";
        });
    }
}
