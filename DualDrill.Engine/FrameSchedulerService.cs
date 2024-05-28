using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Engine;

public sealed class FrameSchedulerService : IDisposable
{
    readonly TimeSpan SampleRate = TimeSpan.FromSeconds(1.0 / 60.0);
    readonly ITimer FrameTimer;

    static int NextFrameChannelId = 0;
    readonly ConcurrentDictionary<int, Channel<int>> FrameChannels = [];
    readonly ConcurrentDictionary<string, TaskCompletionSource<int>> WaitingRequests = [];

    public int Frame { get; private set; } = 0;
    public ILogger<FrameSchedulerService> Logger { get; }

    readonly Channel<RenderScene> RenderSceneChannel = Channel.CreateUnbounded<RenderScene>();

    public async ValueTask AddFrameRenderSceneAsync(RenderScene scene, CancellationToken cancellation)
    {
        await RenderSceneChannel.Writer.WriteAsync(scene, cancellation).ConfigureAwait(false);
    }

    public FrameSchedulerService(ILogger<FrameSchedulerService> logger)
    {
        var timeProvider = TimeProvider.System;
        FrameTimer = timeProvider.CreateTimer(
            FrameTimerCallback,
            this,
            TimeSpan.Zero,
            SampleRate
        );
        Logger = logger;
    }

    public async ValueTask<int> RequestNextFrame(string id, CancellationToken cancellation)
    {
        var tcs = new TaskCompletionSource<int>(cancellation);
        if (!WaitingRequests.TryAdd(id, tcs))
        {
            throw new Exception("Failed add request next frame listener");
        };
        var frame = await tcs.Task.ConfigureAwait(false);
        return frame;
    }

    void FrameTimerCallback(object? state)
    {
        Frame++;
        foreach (var cs in FrameChannels)
        {
            cs.Value.Writer.TryWrite(Frame);
        }
    }

    public IAsyncEnumerable<int> Frames([EnumeratorCancellation] CancellationToken cancellation)
    {
        var id = Interlocked.Increment(ref NextFrameChannelId);
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        if (!FrameChannels.TryAdd(id, channel))
        {
            throw new Exception("Failed to create frame channel");
        }
        return channel.Reader.ReadAllAsync(cancellation);
    }

    public void Dispose()
    {
        FrameTimer.Dispose();
    }
}
