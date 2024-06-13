using DualDrill.Engine;
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
    WGPUHeadlessService wGPUHeadlessService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public async IAsyncEnumerable<int> PingPongStream(ChannelReader<int> events)
    {
        await foreach (var e in events.ReadAllAsync().ConfigureAwait(false))
        {
            Console.WriteLine(e);
            yield return e;
        }
    }

    public async IAsyncEnumerable<RenderState> SwapchainPush(ChannelReader<int> swapChainIndex)
    {
        var frame = 0;
        await foreach (var s in swapChainIndex.ReadAllAsync().WithCancellation(Context.ConnectionAborted).ConfigureAwait(false))
        {
            // TODO: read until next frame
            var image = await wGPUHeadlessService.Render(frame);
            var handle = GCHandle.Alloc(image);
            var state = new RenderState(
                s,
                GCHandle.ToIntPtr(handle)
            );
            yield return state;
        }
    }

    public sealed record class RenderState(int SwapchainIndex, nint ImageHandle)
    {
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
