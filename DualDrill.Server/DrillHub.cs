using DualDrill.Engine;
using DualDrill.Server.WebView;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DualDrill.Server;

sealed class DrillHub(
    FrameSimulationService UpdateService,
    ILogger<DrillHub> Logger,
    WebViewService WebView) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public async IAsyncEnumerable<int> PingPongStream(ChannelReader<int> events)
    {
        await foreach (var e in events.ReadAllAsync().ConfigureAwait(false))
        {
            Logger.LogInformation("Ping-Pong Stream {Data}", e);
            yield return e;
        }
    }
    public async IAsyncEnumerable<SharedBufferMessage> SharedBufferServerRenderingReadable(CancellationToken cancellation)
    {
        await foreach (var b in WebView.GetAllReadableSlotsAsync(cancellation).ConfigureAwait(false))
        {
            yield return b.Message;
        }
    }


    public async Task SharedBufferServerRenderingWriteable(ChannelReader<SharedBufferMessage> writeable)
    {
        await foreach (var m in writeable.ReadAllAsync(Context.ConnectionAborted).ConfigureAwait(false))
        {
            await WebView.SetReadyToWriteAsync(m, Context.ConnectionAborted);
        }
    }

    public async Task MouseEvent(ChannelReader<MouseEvent> events, [FromServices] FrameInputService inputService)
    {
        //var writer = UpdateService.MouseEvents.Writer;
        //await foreach (var e in events.ReadAllAsync().ConfigureAwait(false))
        //{
        //    await writer.WriteAsync(e).ConfigureAwait(false);
        //}
        inputService.AddUserEventSource(Context.ConnectionId, events);
        var tcs = new TaskCompletionSource();
        if (Context.ConnectionAborted.IsCancellationRequested)
        {
            return;
        }
        await using var r = Context.ConnectionAborted.Register(() => tcs.SetResult());
        await tcs.Task;
    }

    public async IAsyncEnumerable<RenderScene> RenderStates([EnumeratorCancellation] CancellationToken cancellationToken)
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
