using DualDrill.Engine.Connection;
using Microsoft.JSInterop;

namespace DualDrill.Server.BrowserClient;

public sealed class RTCDataChannelProxy(IBrowserClient Client, IJSObjectReference Value) : IAsyncDisposable, IDataChannelReference
{
    public IBrowserClient Client { get; } = Client;
    IJSObjectReference Handle { get; } = Value;

    public async ValueTask DisposeAsync()
    {
        await Handle.DisposeAsync().ConfigureAwait(false);
    }

    public async Task SendAsync<T>(T message)
    {
        await Handle.InvokeVoidAsync("send", message);
    }
}

sealed class RTCDataChannelPair(
   RTCDataChannelProxy Source,
   RTCDataChannelProxy Target) : IDataChannelReferencePair, IAsyncDisposable
{
    public IDataChannelReference Source { get; } = Source;

    public IDataChannelReference Target { get; } = Target;

    public IClient SourceClient { get; } = Source.Client;

    public IClient TargetClient { get; } = Target.Client;
    static async Task<RTCDataChannelPair> CreateDataChannelInternal(
           string label,
           RTCPeerConnectionProxy source,
           RTCPeerConnectionProxy target)
    {
        var waitTCS = new TaskCompletionSourceJSWrapper<IJSObjectReference>(new TaskCompletionSource<IJSObjectReference>());

        using var waitTCSReference = DotNetObjectReference.Create(waitTCS);
        await using var sub = await target.WaitDataChannelAsync(label, waitTCSReference);
        var sourceChannel = await source.CreateDataChannelAsync(label).ConfigureAwait(false);
        var targetChannel = new RTCDataChannelProxy(target.Client, await waitTCS.Task);
        return new(sourceChannel, targetChannel);
    }

    public static async Task<IDataChannelReferencePair> CreateAsync(BrowserRTCPeerConnectionPair peers, string label)
    {
        return await CreateDataChannelInternal(label, peers.Source, peers.Target).ConfigureAwait(false);
    }

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
