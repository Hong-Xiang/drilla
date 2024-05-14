using DualDrill.Engine;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Server;

sealed class UserInputHub(UpdateService UpdateService, ILogger<UserInputHub> Logger) : Hub
{
    public async Task MouseEvent(ChannelReader<MouseEvent> events)
    {
        UpdateService.MouseEventChannels.Add(events);
        var tcs = new TaskCompletionSource();
        if (Context.ConnectionAborted.IsCancellationRequested)
        {
            return;
        }
        await using var r = Context.ConnectionAborted.Register(() => tcs.SetResult());
        await tcs.Task;
    }

    public async IAsyncEnumerable<RenderState> RenderStates([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var renderService = UpdateService.RenderService;
        if (renderService is null)
        {
            Logger.LogWarning("Render Service not connected");
            yield break;
        }
        await foreach (var s in renderService.States)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return s;
        }
    }

    public async Task<string> Echo(string data)
    {
        Console.WriteLine(data);
        return data;
    }
}
