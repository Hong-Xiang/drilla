using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Engine;

public sealed class FrameInputService(FrameSchedulerService FrameScheduler) : IDisposable
{
    readonly ConcurrentDictionary<string, ChannelReader<MouseEvent>> MouseEvents = [];
    readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

    public void AddUserEventSource(string clientId, ChannelReader<MouseEvent> reader)
    {
        if (!MouseEvents.TryAdd(clientId, reader))
        {
            throw new Exception("Failed to add user input listener service");
        }
    }

    public async IAsyncEnumerable<FrameContext> FrameEvents([EnumeratorCancellation] CancellationToken cancellation)
    {
        while (true)
        {
            var frame = await FrameScheduler.RequestNextFrame(nameof(FrameInputService), cancellation).ConfigureAwait(false);
            // TODO: better implementation to reduce allocation, maybe use a ring buffer
            var result = new List<MouseEvent>();
            foreach (var channel in MouseEvents)
            {
                while (channel.Value.TryRead(out var e))
                {
                    if (cancellation.IsCancellationRequested || CancellationTokenSource.Token.IsCancellationRequested)
                    {
                        yield break;
                    }
                    result.Add(e);
                }
            }
            yield return new FrameContext
            {
                FrameIndex = frame,
                MouseEvent = result.ToArray()
            };
        }
    }

    public void Dispose()
    {
        CancellationTokenSource.Cancel();
    }
}
