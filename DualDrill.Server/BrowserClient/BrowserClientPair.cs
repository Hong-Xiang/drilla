using Microsoft.JSInterop;
using DualDrill.Engine.Connection;
using System.Reactive.Disposables;
using DualDrill.Common.ResourceManagement;
using System.Reactive.Subjects;
using DualDrill.Server.Components;

namespace DualDrill.Server.BrowserClient;

sealed class BrowserRTCPeerConnectionPair(
   RTCPeerConnectionProxy Source,
   RTCPeerConnectionProxy Target,
   IAsyncDisposable Done) : IConnectedPair<RTCPeerConnectionProxy>
{
    public RTCPeerConnectionProxy Source { get; } = Source;
    public RTCPeerConnectionProxy Target { get; } = Target;
    public IClient SourceClient => Source.Client;
    public IClient TargetClient => Target.Client;

    static async IAsyncEnumerable<Func<IAsyncDisposable, BrowserRTCPeerConnectionPair>> CreateAsyncInternal(IBrowserClient source, IBrowserClient target)
    {
        await using var sourcePeer = await RTCPeerConnectionProxy.CreateAsync(source).ConfigureAwait(false);
        await using var targetPeer = await RTCPeerConnectionProxy.CreateAsync(target).ConfigureAwait(false);
        using var sub = new CompositeDisposable(
            targetPeer.IceCandidate.Subscribe(async (candidate) => await sourcePeer.AddIceCandidate(candidate)),
            sourcePeer.IceCandidate.Subscribe(async (candidate) => await targetPeer.AddIceCandidate(candidate)),
            sourcePeer.NegotiationNeeded.Subscribe(async (_) => await Negotiation(sourcePeer, targetPeer)),
            targetPeer.NegotiationNeeded.Subscribe(async (_) => await Negotiation(targetPeer, sourcePeer))
        );
        await Negotiation(sourcePeer, targetPeer).ConfigureAwait(false);
        yield return (dispose) => new BrowserRTCPeerConnectionPair(sourcePeer, targetPeer, dispose); ;
    }

    static async Task Negotiation(RTCPeerConnectionProxy source, RTCPeerConnectionProxy target)
    {
        var offer = await source.CreateOffer().ConfigureAwait(false);
        var answer = await target.SetOffer(offer).ConfigureAwait(false);
        await source.SetAnswer(answer).ConfigureAwait(false);
    }
    public static async Task<BrowserRTCPeerConnectionPair> CreateAsync(IBrowserClient source, IBrowserClient target)
    {
        return await AsyncResource.CreateAsync(CreateAsyncInternal(source, target)).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Done.DisposeAsync().ConfigureAwait(false);
    }
}

sealed class BrowserClientPair(BrowserRTCPeerConnectionPair Peers) : IP2PClientPair, IAsyncDisposable
{
    public IClient Source => SourceClient;
    public IClient Target => TargetClient;
    public IClient SourceClient { get; } = Peers.SourceClient;
    public IClient TargetClient { get; } = Peers.TargetClient;
    public BrowserRTCPeerConnectionPair Peers { get; } = Peers;

    readonly Subject<IClientVideoReference> VideoReceivedSubject = new();
    public IObservable<IClientVideoReference> VideoReceived => VideoReceivedSubject;
    public static async Task<BrowserClientPair> CreateAsync(BrowserClient source, BrowserClient target)
    {
        var peerPair = await BrowserRTCPeerConnectionPair.CreateAsync(source, target).ConfigureAwait(false);
        return new BrowserClientPair(peerPair);
    }

    private TaskCompletionSource SourceUISetSource = new();
    public Task SourceUISet => SourceUISetSource.Task;
    private TaskCompletionSource TargetUISetSource = new();
    public Task TargetUISet => TargetUISetSource.Task;
    public PairViewer? SourceViewer { get; private set; } = default;
    public PairViewer? TargetViewer { get; private set; } = default;
    public void SetSourceUI(PairViewer viewer)
    {
        SourceViewer = viewer;
        SourceUISetSource.SetResult();
    }
    public void SetTargetUI(PairViewer viewer)
    {
        TargetViewer = viewer;
        TargetUISetSource.SetResult();
    }

    public Task UISet()
        => Task.WhenAll(SourceUISet, TargetUISet);

    public async Task<IDataChannelReferencePair> CreateDataChannel(string label)
    {
        return await RTCDataChannelPair.CreateAsync(Peers, label).ConfigureAwait(false);
    }

    public async Task<IClientVideoReference> SendVideo(IClientVideoReference video)
    {
        var sendClient = video.Client;
        var receiveClient = this.GetPeer(sendClient) as BrowserClient ?? throw new Exception("Not support client type");
        var sendPeer = Peers.GetSelf(sendClient);
        var receivePeer = Peers.GetSelf(receiveClient);

        var waitTCS = new TaskCompletionSource<JSMediaStreamProxy>();
        var jsPromise = new JSPromiseLikeBuilder<IJSObjectReference>(async (stream) =>
        {
            var id = await receiveClient.Module.GetProperty<string>(stream, "id");
            waitTCS.SetResult(new JSMediaStreamProxy(receiveClient, stream, id));
        }, async (msg) =>
        {
            Console.WriteLine(msg);
        });

        using var waitTCSReference = DotNetObjectReference.Create(waitTCS);
        using var pref = jsPromise.CreateReference();
        await using var sub = await receivePeer.WaitVideoStream(video.Id, pref);
        Console.WriteLine("Wait called");
        await sendPeer.AddVideoStream(((JSMediaStreamProxy)video).MediaStream).ConfigureAwait(false);
        Console.WriteLine("JS Add video stream called");
        return await waitTCS.Task.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Peers.DisposeAsync().ConfigureAwait(false);
    }
}

