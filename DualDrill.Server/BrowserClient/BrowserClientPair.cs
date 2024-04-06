using Microsoft.JSInterop;
using DualDrill.Engine.Connection;
using System.Reactive.Disposables;
using DualDrill.Common.ResourceManagement;

namespace DualDrill.Server.BrowserClient;


sealed class BrowserClientPair(
    BrowserClient SourceClient,
    RTCPeerConnectionProxy SourcePeer,
    BrowserClient TargetClient,
    RTCPeerConnectionProxy TargetPeer,
    RTCDataChannelPair SystemDataChannel,
    IAsyncDisposable Resource) : IClientPeerToPeerPair, IAsyncDisposable
{
    public IClient SourceClient { get; } = SourceClient;
    public RTCPeerConnectionProxy SourcePeer { get; } = SourcePeer;
    public IClient TargetClient { get; } = TargetClient;
    public RTCPeerConnectionProxy TargetPeer { get; } = TargetPeer;
    public IDataChannelReferncePair SystemDataChannel { get; } = SystemDataChannel;

    static async IAsyncEnumerable<Func<IAsyncDisposable, BrowserClientPair>> CreateAsyncInternal(BrowserClient source, BrowserClient target)
    {
        var sourcePeer = await RTCPeerConnectionProxy.CreateAsync(source.Module).ConfigureAwait(false);
        var targetPeer = await RTCPeerConnectionProxy.CreateAsync(target.Module).ConfigureAwait(false);
        await source.JSRuntime.InvokeVoidAsync("console.log", "source log!");
        await target.JSRuntime.InvokeVoidAsync("console.log", "target log!");
        using var sub = new CompositeDisposable(
       sourcePeer.IceCandidate.Subscribe(async (candidate) =>
        {
            Console.WriteLine("source candidate");
            await targetPeer.AddIceCandidate(candidate);
        }),
        targetPeer.IceCandidate.Subscribe(async (candidate) =>
        {
            Console.WriteLine("target candidate");
            await sourcePeer.AddIceCandidate(candidate);
        }),
        sourcePeer.NegotiationNeeded.Subscribe(async (_) =>
        {
            await Negotiation(sourcePeer, targetPeer);
        }));

        await Negotiation(sourcePeer, targetPeer).ConfigureAwait(false);
        Console.WriteLine("Before Create Data Channel");
        var (sd, td) = await CreateDataChannelInternal(sourcePeer, targetPeer);
        await sd.Send("ping");
        yield return (dispose) => new BrowserClientPair(source, sourcePeer, target, targetPeer, new(sd, td), dispose); ;
    }

    static async Task Negotiation(RTCPeerConnectionProxy source, RTCPeerConnectionProxy target)
    {
        var offer = await source.CreateOffer().ConfigureAwait(false);
        var answer = await target.SetOffer(offer).ConfigureAwait(false);
        await source.SetAnswer(answer).ConfigureAwait(false);
        Console.WriteLine(offer);
    }


    static async Task<(RTCDataChannelProxy, RTCDataChannelProxy)> CreateDataChannelInternal(
        RTCPeerConnectionProxy source,
        RTCPeerConnectionProxy target
    )
    {
        var id = Guid.NewGuid().ToString();
        var waitTCS = new TaskCompletionSourceJSWrapper<IJSObjectReference>(new TaskCompletionSource<IJSObjectReference>());
        using var waitTCSReference = DotNetObjectReference.Create(waitTCS);
        await using var sub = await target.WaitDataChannelAsync(id, waitTCSReference);
        Console.WriteLine("after call wait data channel");
        var sourceChannel = await source.CreateDataChannelAsync(id).ConfigureAwait(false);
        Console.WriteLine("after call source create data channel");
        var targetChannel = new RTCDataChannelProxy(await waitTCS.Task);
        return (sourceChannel, targetChannel);
    }


    public static async Task<BrowserClientPair> CreateAsync(BrowserClient source, BrowserClient target)
    {
        return await AsyncResource.CreateAsync(CreateAsyncInternal(source, target)).ConfigureAwait(false);
    }

    public Task<IDataChannelReferncePair> CreateDataChannel()
    {
        throw new NotImplementedException();
    }

    public Task<IVideoChannelReferencePair> CreateVideoChannel(IVideoChannelReference video)
    {
        throw new NotImplementedException();
    }

    public async ValueTask DisposeAsync()
    {
        await Resource.DisposeAsync().ConfigureAwait(false);
    }
}

