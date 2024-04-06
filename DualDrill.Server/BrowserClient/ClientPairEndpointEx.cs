using DualDrill.Engine.Connection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace DualDrill.Server.BrowserClient;

public static class ClientPairEndpointEx
{
    static Ok<string[]> GetConnectedClients([FromServices] ClientHub clients)
    {
        return TypedResults.Ok(clients.ClientIds);
    }

    public static void AddBrowserClients(this IServiceCollection services)
    {
        services.AddScoped<MediaDevices>();
    }

    public static void MapBrowserClients(this WebApplication app)
    {
        app.MapGet("/api/clients", GetConnectedClients);
    }
}
