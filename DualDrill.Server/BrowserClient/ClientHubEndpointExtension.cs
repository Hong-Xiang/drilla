using DualDrill.Engine.Connection;
using DualDrill.Engine.UI;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace DualDrill.Server.BrowserClient;

public static class ClientHubEndpointExtension
{
    static Ok<string[]> GetConnectedClients([FromServices] ClientStore clients)
    {
        return TypedResults.Ok(clients.ClientIds);
    }

    public static void AddClients(this IServiceCollection services)
    {
        services.AddSingleton<ClientStore>();
        services.AddSingleton<ClientUIStore>();

        services.AddScoped<MediaDevices>();
        services.AddScoped<CircuitService>();
        services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<CircuitService>());
    }

    public static void MapClients(this WebApplication app)
    {
        app.MapGet("/api/clients", GetConnectedClients);
    }
}
