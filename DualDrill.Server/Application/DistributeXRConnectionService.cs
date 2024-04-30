using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Command;
using Microsoft.JSInterop;
using System.Threading.Channels;

namespace DualDrill.Server.Application;

sealed class DistributeXRConnectionService(ILogger<DistributeXRConnectionService> Logger)
{
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
        if (source is Browser.BrowserClient sui)
        {
            //var sourceCameraMediaStream = await sui.GetCameraStreamAsync().ConfigureAwait(false);
            var canvas = await sui.ExecuteCommandAsync(new GetRenderCanvas());
            var stream = await sui.Module.CaptureStream(sui, canvas);
            await SendVideo(stream, target);
        }
        if (target is Browser.BrowserClient tui)
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
        Logger.LogTrace("Wait called");

        await sendPeer.AddVideoStream(video.Reference).ConfigureAwait(false);
        Logger.LogTrace("JS Add video stream called");
        var targetVideoJS = await tcs.Task.ConfigureAwait(false);
        var targetModule = receiveClient.Services.GetRequiredService<JSClientModule>();
        var targetVideoId = await targetModule.GetProperty<string>(targetVideoJS, "id");
        var targetVideo = new JSMediaStreamProxy(receiveClient, targetModule, targetVideoJS, targetVideoId);
        Logger.LogTrace("Target Video Received");
        await sendClient.ExecuteCommandAsync(new ShowSelfVideoCommand(video));
        await receiveClient.ExecuteCommandAsync(new ShowPeerVideoElementCommand(targetVideo));
    }
}
