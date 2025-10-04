using DualDrill.Engine.Connection;
using System.Reactive.Disposables;
using DualDrill.Common.ResourceManagement;

namespace DualDrill.Engine.WebRTC;

public sealed class RTCPeerConnectionPair(
   IRTCPeerConnection Source,
   IRTCPeerConnection Target,
   IAsyncDisposable Done) : IConnectedPair<IClient, IClient, IRTCPeerConnection>
{
    public IRTCPeerConnection Source { get; } = Source;
    public IRTCPeerConnection Target { get; } = Target;
    public IClient SourceClient => Source.Client;
    public IClient TargetClient => Target.Client;

    static async IAsyncEnumerable<Func<IAsyncDisposable, RTCPeerConnectionPair>> CreateAsyncInternal(IClient source, IClient target)
    {
        await using var sourcePeer = await source.CreatePeerConnection().ConfigureAwait(false);
        await using var targetPeer = await target.CreatePeerConnection().ConfigureAwait(false);
        using var sub = new CompositeDisposable(
            sourcePeer.IceCandidate.Subscribe(async (candidate) => await targetPeer.AddIceCandidate(candidate)),
            targetPeer.IceCandidate.Subscribe(async (candidate) => await sourcePeer.AddIceCandidate(candidate)),
            sourcePeer.NegotiationNeeded.Subscribe(async (_) => await Negotiation(sourcePeer, targetPeer)),
            targetPeer.NegotiationNeeded.Subscribe(async (_) => await Negotiation(sourcePeer, targetPeer))
        );
        await Negotiation(sourcePeer, targetPeer).ConfigureAwait(false);
        yield return (dispose) => new RTCPeerConnectionPair(sourcePeer, targetPeer, dispose); ;
    }

    static async Task Negotiation(IRTCPeerConnection source, IRTCPeerConnection target)
    {
        var offer = await source.CreateOffer().ConfigureAwait(false);
        await source.SetLocalDescription(RTCSessionDescription.Offer, offer);
        await target.SetRemoteDescription(RTCSessionDescription.Offer, offer);
        var answer = await target.CreateAnswer().ConfigureAwait(false);
        await target.SetLocalDescription(RTCSessionDescription.Answer, answer);
        await source.SetRemoteDescription(RTCSessionDescription.Answer, answer);
    }
    public static async Task<RTCPeerConnectionPair> CreateAsync(IClient source, IClient target)
    {
        return await AsyncResource.CreateAsync(CreateAsyncInternal(source, target)).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Done.DisposeAsync().ConfigureAwait(false);
    }
}

//sealed class BrowserClientPair(BrowserRTCPeerConnectionPair Peers) : IPeerToPeerClientPair, IAsyncDisposable
//{
//    public IClient Source => SourceClient;
//    public IClient Target => TargetClient;
//    public IClient SourceClient { get; } = Peers.SourceClient;
//    public IClient TargetClient { get; } = Peers.TargetClient;
//    public BrowserRTCPeerConnectionPair Peers { get; } = Peers;

//    public static async Task<BrowserClientPair> CreateAsync(BrowserClient source, BrowserClient target)
//    {
//        var peerPair = await BrowserRTCPeerConnectionPair.CreateAsync(source, target).ConfigureAwait(false);
//        return new BrowserClientPair(peerPair);
//    }

//    private TaskCompletionSource SourceUISetSource = new();
//    public Task SourceUISet => SourceUISetSource.Task;
//    private TaskCompletionSource TargetUISetSource = new();
//    public Task TargetUISet => TargetUISetSource.Task;
//    public PairViewer? SourceViewer { get; private set; } = default;
//    public PairViewer? TargetViewer { get; private set; } = default;
//    public void SetSourceUI(PairViewer viewer)
//    {
//        SourceViewer = viewer;
//        SourceUISetSource.SetResult();
//    }
//    public void SetTargetUI(PairViewer viewer)
//    {
//        TargetViewer = viewer;
//        TargetUISetSource.SetResult();
//    }

//    public Task UISet()
//        => Task.WhenAll(SourceUISet, TargetUISet);

//    public async Task<IDataChannelReferencePair> CreateDataChannel(string label)
//    {
//        return await RTCDataChannelPair.CreateAsync(Peers, label).ConfigureAwait(false);
//    }

//    //public async Task<IClientVideoReference> SendVideo(IClientVideoReference video)
//    //{
//    //    var sendClient = video.Client;
//    //    var receiveClient = this.GetPeer(sendClient) as BrowserClient ?? throw new Exception("Not support client type");
//    //    var sendPeer = Peers.GetSelf(sendClient);
//    //    var receivePeer = Peers.GetSelf(receiveClient);

//    //    var waitTCS = new TaskCompletionSource<JSMediaStreamProxy>();
//    //    var jsPromise = new PrimitiveJSPromiseBuilder<IJSObjectReference>(async (stream) =>
//    //    {
//    //        var id = await receiveClient.Module.GetProperty<string>(stream, "id");
//    //        waitTCS.SetResult(new JSMediaStreamProxy(receiveClient, stream, id));
//    //    }, async (msg) =>
//    //    {
//    //        Console.WriteLine(msg);
//    //    });

//    //    using var waitTCSReference = DotNetObjectReference.Create(waitTCS);
//    //    using var pref = jsPromise.CreateReference();
//    //    await using var sub = await receivePeer.WaitVideoStream(video.Id, pref);
//    //    Console.WriteLine("Wait called");
//    //    await sendPeer.AddVideoStream(video.js).ConfigureAwait(false);
//    //    Console.WriteLine("JS Add video stream called");
//    //    return await waitTCS.Task.ConfigureAwait(false);
//    //}

//    public async ValueTask DisposeAsync()
//    {
//        await Peers.DisposeAsync().ConfigureAwait(false);
//    }
//}

