using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Command;
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

    RTCPeerConnectionPair? BrowserRTCPeerConnectionPair { get; set; } = null;

    public async ValueTask SetClients(IClient source, IClient target)
    {
        SourceClient = source;
        TargetClient = target;
        BrowserRTCPeerConnectionPair = await RTCPeerConnectionPair.CreateAsync(source, target);
        await source.ExecuteCommandAsync(new ShowPeerClientCommand(target));
        await target.ExecuteCommandAsync(new ShowPeerClientCommand(source));
        if (source is BrowserClient.BrowserClient sui)
        {
            var sourceCameraMediaStream = await sui.GetCameraStreamAsync().ConfigureAwait(false);
            await SendVideo(sourceCameraMediaStream, target);
        }
        if (target is BrowserClient.BrowserClient tui)
        {
            var targetCameraMediaStream = await tui.GetCameraStreamAsync().ConfigureAwait(false);
            await SendVideo(targetCameraMediaStream, source);
        }
    }

    public async ValueTask SendVideo<TVideo, TClient>(TVideo video, TClient target)
        where TClient : IClient
        where TVideo : IClientObjectReferenceProxy<IClient, IJSObjectReference>, IMediaStream
    {
        if (BrowserRTCPeerConnectionPair is null)
        {
            throw new NotPartitionClientException(target);
        }
        var sendClient = video.Client;
        var receiveClient = target;
        var sendPeer = BrowserRTCPeerConnectionPair.GetSelf(sendClient);
        var receivePeer = BrowserRTCPeerConnectionPair.GetSelf(receiveClient);

        using var tcs = new TaskCompletionSourceReferenceWrapper<IJSObjectReference>();
        await using var sub = await receivePeer.WaitVideoStream(video.Id, tcs);
        Console.WriteLine("Wait called");

        await sendPeer.AddVideoStream(video.Reference).ConfigureAwait(false);
        Console.WriteLine("JS Add video stream called");
        var targetVideoJS = await tcs.Task.ConfigureAwait(false);
        var targetModule = receiveClient.Services.GetRequiredService<JSClientModule>();
        var targetVideoId = await targetModule.GetProperty<string>(targetVideoJS, "id");
        var targetVideo = new JSMediaStreamProxy(receiveClient, targetModule, targetVideoJS, targetVideoId);
        Console.WriteLine("Target Video Received");
        await sendClient.ExecuteCommandAsync(new ShowSelfVideoCommand(video));
        await receiveClient.ExecuteCommandAsync(new ShowPeerVideoElementCommand(targetVideo));
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
