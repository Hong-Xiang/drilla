using DualDrill.Engine.Connection;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ServerConnectionController(
    ClientConnectionManagerService ClientStore,
    IServiceProvider ServiceProvider,
    IPeerConnectionProviderService PeerConnectionService,
    ILogger<ServerConnectionController> Logger
) : ControllerBase
{
    [HttpPost]
    [Route("client")]
    public IClient CreateClientToServerConnection([FromQuery] string? connectionId)
    {
        var scope = ServiceProvider.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IClient>()
                             ?? throw new NullReferenceException("Failed to create client identity");
        if (connectionId is not null)
        {
            ClientStore.UpdateConnectionId(client.Id, connectionId);
        }
        else
        {
            Logger.LogWarning("SignalR ConnectionId not provided for client {ClientId}", client.Id);
        }
        PeerConnectionService.CreatePeerConnection(client.Id);
        return client;
    }

    [HttpGet]
    [Route("client")]
    public Guid[] GetConnectedClients()
    {
        var ids = ClientStore.Clients.Select(static s => s.Id).ToArray();
        return ids;
    }
}
