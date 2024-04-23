
using DualDrill.Engine.Connection;
using DualDrill.Server.BrowserClient;
using Microsoft.JSInterop;
using System.Threading.Channels;

namespace DualDrill.Server.Application;

sealed class DistributeXRApplicationService(ILogger<DistributeXRApplicationService> Logger) : BackgroundService
{
    readonly Channel<Func<CancellationToken, DistributeXRApplicationService, ValueTask>> ConnectionWorkItems =
        Channel.CreateUnbounded<Func<CancellationToken, DistributeXRApplicationService, ValueTask>>();

    public void QueueConnectionWorkItemAsync(Func<CancellationToken, DistributeXRApplicationService, ValueTask> work)
    {
        if (!ConnectionWorkItems.Writer.TryWrite(work))
        {
            Logger.LogError("Failed to queue connection task");
        }
    }
    IClient? SourceClient { get; set; } = null;
    IClient? TargetClient { get; set; } = null;

    BrowserRTCPeerConnectionPair? BrowserRTCPeerConnectionPair { get; set; } = null;

    public async ValueTask SetClients(IBrowserClient source, IBrowserClient target)
    {
        SourceClient = source;
        TargetClient = target;
        BrowserRTCPeerConnectionPair = await BrowserRTCPeerConnectionPair.CreateAsync(source, target);
        ValueTask sSend = default;
        if (source is BrowserUIClient sui)
        {
            var sourceCamera = await sui.GetCameraStreamAsync().ConfigureAwait(false);
            await SendVideo(sourceCamera, target);
            await sui.UI.SetPeerClient(target);
        }
        if (target is BrowserUIClient tui)
        {
            var targetCamera = await target.GetCameraStreamAsync().ConfigureAwait(false);
            await tui.UI.SetPeerClient(source);
            await SendVideo(targetCamera, source);
        }
        //if (source is BrowserUIClient sui2)
        //{
        //    var sourceCamera = await sui2.GetCameraStreamAsync().ConfigureAwait(false);
        //    await SendVideo(sourceCamera, target);
        //    await sSend;
        //}
    }

    public async ValueTask SendVideo(IClientVideoReference video, IClient target)
    {
        if (BrowserRTCPeerConnectionPair is null)
        {
            throw new NotPartitionClientException(target);
        }
        var sendClient = video.Client as IBrowserClient ?? throw new NotSupportedException();
        var receiveClient = target as IBrowserClient ?? throw new NotSupportedException();
        var sendPeer = BrowserRTCPeerConnectionPair.GetSelf(sendClient);
        var receivePeer = BrowserRTCPeerConnectionPair.GetSelf(receiveClient);

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
        var targetVideo = await waitTCS.Task.ConfigureAwait(false);
        Console.WriteLine("Target Video Received");
        if (sendClient is BrowserUIClient sui)
        {
            await sui.UI.ShowSelfVideo((JSMediaStreamProxy)video);
        }
        if (target is BrowserUIClient tui)
        {
            await tui.UI.ShowPeerVideo(targetVideo);
        }
    }



    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await ConnectionWorkItems.Reader.ReadAsync(stoppingToken);
            await workItem(stoppingToken, this);
        }
    }
}
