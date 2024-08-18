using DualDrill.Engine.Connection;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ServerConnectionController(
    ClientsManager ClientsManager,
    IServiceProvider ServiceProvider,
    IPeerConnectionProviderService PeerConnectionProviderService,
    ILogger<ServerConnectionController> Logger
) : ControllerBase
{
    [HttpGet]
    [Route("client")]
    public Guid[] GetConnectedClients()
    {
        var ids = ClientsManager.Clients.Select(static s => s.Id).ToArray();
        return ids;
    }

    [HttpPost]
    [Route("client/{clientId}")]
    public async Task CreateClientToServerConnection(Guid clientId, CancellationToken cancellation)
    {
        var scope = ServiceProvider.CreateAsyncScope();
        //var client = scope.ServiceProvider.GetRequiredService<IClient>()
        //                     ?? throw new NullReferenceException("Failed to create client identity");
        //if (connectionId is not null)
        //{
        //    ClientsManager.UpdateConnectionId(client.Id, connectionId);
        //}
        //else
        //{
        //    Logger.LogWarning("SignalR ConnectionId not provided for client {ClientId}", client.Id);
        //}
        await PeerConnectionProviderService.CreatePeerConnectionAsync(clientId, cancellation);
        //return client;
    }
}
