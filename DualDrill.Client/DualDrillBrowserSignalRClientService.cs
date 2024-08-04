using DualDrill.Client.Abstraction;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace DualDrill.Client;
public class DualDrillBrowserSignalRClientService(NavigationManager NavigationManager)
    : IDualDrillBrowserSignalRClient
{
    readonly HubConnection Connection = new HubConnectionBuilder()
           .WithUrl($"{NavigationManager.BaseUri}hub/browser-client")
           .Build();

    public async ValueTask StartAsync()
    {
    }
}
