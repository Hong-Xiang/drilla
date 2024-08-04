using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Channels;

namespace DualDrill.Client;

public class SignalRService(NavigationManager NavigationManager) : IAsyncDisposable
{
    readonly HubConnection Connection = new HubConnectionBuilder()
        .WithUrl($"{NavigationManager.BaseUri}hub/browser-client")
        .Build();

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync().ConfigureAwait(false);
    }

    public async Task Initialization()
    {
        Console.WriteLine(NavigationManager.BaseUri);
        await Connection.StartAsync();
        Console.WriteLine(await Connection.InvokeAsync<string>("Echo", "test data"));
    }

    public async Task PingPongTest()
    {
        var channel = Channel.CreateUnbounded<int>();
        await foreach (var res in Connection.StreamAsync<int>("PingPongStream", channel))
        {
        }
    }
}
