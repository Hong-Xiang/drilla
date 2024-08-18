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
}
