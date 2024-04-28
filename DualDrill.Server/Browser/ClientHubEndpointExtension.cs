using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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

        services.AddScoped<MediaDevices>();
        services.AddScoped<JSClientModule>();
        services.AddScoped<BrowserClient>();
        services.AddScoped<IClient>(sp => sp.GetRequiredService<BrowserClient>());
        services.AddScoped<CircuitService>();
        services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<CircuitService>());
    }

    public static void MapClients(this WebApplication app)
    {
        app.MapGet("/api/clients", GetConnectedClients);
    }
}
