using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Drill.Connection;

public static class ConnectionServiceModule
{
    public static void AddConnectionServices(this IServiceCollection services)
    {
        services.AddSingleton<ConnectionStoreService>();
    }

    static async Task<Ok<string[]>> GetConnectedClients([FromServices] ConnectionStoreService connections)
    {
        return TypedResults.Ok(connections.ClientIds.ToArray());
    }

    public static void UseDrillClients(this WebApplication app)
    {
        app.MapHub<ConnectionHub>("/hub/rtc");
        app.MapGet("/api/clients/", GetConnectedClients);
    }
}

