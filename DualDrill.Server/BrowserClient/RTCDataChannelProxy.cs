using DualDrill.Engine.Connection;
using Microsoft.JSInterop;

namespace DualDrill.Server.BrowserClient;

public sealed class RTCDataChannelProxy(IJSObjectReference Value) : IAsyncDisposable, IDataChannelReference
{
    public IJSObjectReference Value { get; } = Value;

    public async ValueTask DisposeAsync()
    {
        await Value.DisposeAsync().ConfigureAwait(false);
    }

    public async Task Send<T>(T message)
    {
        await Value.InvokeVoidAsync("send", message);
    }
}

public sealed class RTCDataChannelPair(RTCDataChannelProxy Source, RTCDataChannelProxy Target) : IDataChannelReferncePair
{
    public IDataChannelReference Source { get; } = Source;

    public IDataChannelReference Target { get; } = Target;

    public async ValueTask DisposeAsync()
    {
        if (Source is RTCDataChannelProxy sp)
        {
            await sp.DisposeAsync().ConfigureAwait(false);
        }
        if (Target is RTCDataChannelProxy tp)
        {
            await tp.DisposeAsync().ConfigureAwait(false);
        }
    }
}
