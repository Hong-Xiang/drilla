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
        UpdateService.MouseEvent = events;
        //var writer = UpdateService.MouseEvents.Writer;
        //await foreach (var e in events.ReadAllAsync().ConfigureAwait(false))
        //{
        //    await writer.WriteAsync(e).ConfigureAwait(false);
        //}
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
        await foreach (var s in UpdateService.RenderStates.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            yield return s;
        }
    }

    public async Task<string> Echo(string data)
    {
        Console.WriteLine(data);
        return data;
    }
}
