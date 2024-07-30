using DualDrill.Client.Abstraction;
using Microsoft.AspNetCore.SignalR;

namespace DualDrill.Server;

public sealed class DualDrillBrowserClientHub : Hub<IDualDrillBrowserSignalRClient>
{
}
