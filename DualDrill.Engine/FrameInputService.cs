using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Engine;

public sealed class FrameInputService : IDisposable
{
    readonly ConcurrentDictionary<string, ChannelReader<MouseEvent>> MouseEvents = [];

    public void AddUserEventSource(string clientId, ChannelReader<MouseEvent> reader)
    {
        if (!MouseEvents.TryAdd(clientId, reader))
        {
            throw new Exception("Failed to add user input listener service");
        }
    }

    public async ValueTask<List<MouseEvent>> ReadUserInputsAsync(CancellationToken cancellation)
    {
        var result = new List<MouseEvent>();
        foreach (var channel in MouseEvents)
        {
            while (channel.Value.TryRead(out var e))
            {
                result.Add(e);
            }
        }
        return result;
    }

    public void Dispose()
    {
    }
}
