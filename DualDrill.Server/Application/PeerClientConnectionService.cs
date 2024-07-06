﻿using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Browser;
using DualDrill.Server.Command;
using Microsoft.JSInterop;
using System.Reactive.Linq;
using System.Security.Claims;
using System.Threading.Channels;

namespace DualDrill.Server.Application;

sealed class PeerClientConnectionService(ILogger<PeerClientConnectionService> Logger)
{
    IClient? SourceClient { get; set; } = null;
    IClient? TargetClient { get; set; } = null;

    RTCPeerConnectionPair? BrowserRTCPeerConnectionPair { get; set; } = null;

    public async ValueTask SetClients(IClient displayClient, IClient renderClient)
    {
        SourceClient = displayClient;
        TargetClient = renderClient;
        BrowserRTCPeerConnectionPair = await RTCPeerConnectionPair.CreateAsync(displayClient, renderClient);
        await displayClient.ExecuteCommandAsync(new ShowPeerClientCommand(renderClient, null));
        await renderClient.ExecuteCommandAsync(new ShowPeerClientCommand(displayClient, null));
        if (displayClient is Browser.BrowserClient sui)
        {
            var stream = await sui.GetCameraStreamAsync().ConfigureAwait(false);
            //var canvas = await sui.ExecuteCommandAsync(new GetRenderCanvas());
            //var stream = await sui.Module.CaptureStream(sui, canvas);
            await SendVideo(stream, renderClient);
        }
        if (renderClient is Browser.BrowserClient tui)
        {
            var stream = await tui.GetCameraStreamAsync().ConfigureAwait(false);
            //var canvas = await tui.ExecuteCommandAsync(new GetRenderCanvas());
            //var stream = await (await tui.Module).CaptureCanvasToStream(tui, canvas);

            await SendVideo(stream, displayClient);
        }
        AutoResetClientsWhenFailed(BrowserRTCPeerConnectionPair);
    }

    public void AutoResetClientsWhenFailed(RTCPeerConnectionPair connectionPair)
    {
        connectionPair.Source.ConnectionStateChange
            .Merge(connectionPair.Target.ConnectionStateChange)
            .Where(state => state == "failed")
            .Take(1)
            .SelectMany(Observable.FromAsync(async c =>
            {
                await ResetClients();

            }))
            .Subscribe(_ => { });
    }

    public async ValueTask ResetClients()
    {
        if (SourceClient is Browser.BrowserClient sui)
        {
            Console.WriteLine("Reset SourceClient");
            try
            {
                var sourceCameraMediaStream = await sui.GetCameraStreamAsync().ConfigureAwait(false);
                var mediaTrack = await sourceCameraMediaStream.GetVideoTrack(0);
                await mediaTrack.Stop();
                await CloseVideo(SourceClient);
                await SourceClient.ExecuteCommandAsync(new RemovePeerClientCommand());
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
        }
        Console.WriteLine("Reset TargetClient");

        if (TargetClient is Browser.BrowserClient tui)
        {
            try
            {
                var targetCameraMediaStream = await tui.GetCameraStreamAsync().ConfigureAwait(false);
                var mediaTrack = await targetCameraMediaStream.GetVideoTrack(0);
                await mediaTrack.Stop();
                await CloseVideo(TargetClient);
                await TargetClient.ExecuteCommandAsync(new RemovePeerClientCommand());
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
        }
        Console.WriteLine("Reset BrowserRTCPeerConnectionPair");
        if (BrowserRTCPeerConnectionPair is not null)
        {
            try
            {
                await BrowserRTCPeerConnectionPair.DisposeAsync();
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
        }

        SourceClient = null;
        TargetClient = null;
        BrowserRTCPeerConnectionPair = null;
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
        var recvClient = target;
        var sendPeer = BrowserRTCPeerConnectionPair.GetSelf(sendClient);
        var recvPeer = BrowserRTCPeerConnectionPair.GetSelf(recvClient);

        using var tcs = new TaskCompletionSourceDotnetObjectReference<IJSObjectReference>();
        await using var sub = await recvPeer.WaitVideoStream(video.Id, tcs);
        Logger.LogTrace("Wait called");

        await sendPeer.AddVideoStream(video.Reference).ConfigureAwait(false);
        Logger.LogTrace("JS Add video stream called");
        var targetVideoJS = await tcs.Task.ConfigureAwait(false);
        var targetModule = await (recvClient as BrowserClient).Module;
        var targetVideoId = await targetModule.GetProperty<string>(targetVideoJS, "id");
        var targetVideo = new JSMediaStreamProxy(recvClient, targetModule, targetVideoJS, targetVideoId);
        Logger.LogTrace("Target Video Received");
        await sendClient.ExecuteCommandAsync(new ShowSelfVideoCommand(video));
        await recvClient.ExecuteCommandAsync(new ShowPeerVideoElementCommand(targetVideo));
    }

    public async ValueTask CloseVideo<TClient>(TClient client)
        where TClient : IClient
    {
        Logger.LogTrace("close video");
        await client.ExecuteCommandAsync(new CloseSelfVideoCommand());
        await client.ExecuteCommandAsync(new ClosePeerVideoElementCommand());
    }
}
