using DualDrill.Engine.Connection;
using DualDrill.Server.BrowserClient;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DualDrill.Server.Components;

partial class PairViewer
{
    [Parameter]
    public IP2PClientPair Pair { get; set; } = default!;

    [Inject] CircuitService BrowserClientService { get; set; } = default!;
    [Inject] MediaDevices MediaDevices { get; set; } = default!;

    [Parameter]
    public BrowserClient.BrowserClient Client { get; set; } = default!;

    private string SourcePeerState = "unknown";
    private string TargetPeerState = "unknown";
    ElementReference SelfVideoElement { get; set; }
    ElementReference PeerVideoElement { get; set; }

    private bool IsLogDataChannelMessage => LogSubscription is not null;

    private bool IsSource => Pair?.GetRole(Client) == PairRole.Source;

    private JSMediaStreamProxy? CameraStream { get; set; } = null;

    private IDisposable? VideoReceiveSubscription = null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender).ConfigureAwait(false);
        if (firstRender)
        {
            CameraStream ??= await MediaDevices.GetUserMedia(Client, audio: false, video: true);
            await Client.Module.SetVideoElementStream(SelfVideoElement, CameraStream.MediaStream);
            if (Pair.GetRole(Client) == PairRole.Source)
            {
                (Pair as BrowserClientPair)?.SetSourceUI(this);
            }
            else
            {
                (Pair as BrowserClientPair)?.SetTargetUI(this);
            }
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        VideoReceiveSubscription = Pair.VideoReceived.Subscribe(async (vp) =>
        {
            if (!vp.Client.Equals(Client))
            {
                await Client.Module.SetVideoElementStream(PeerVideoElement, ((JSMediaStreamProxy)vp).MediaStream);
            }
        });
    }

    private async Task SetPeerVideo(JSMediaStreamProxy mediaStream)
    {
        await Client.Module.SetVideoElementStream(PeerVideoElement, mediaStream.MediaStream).ConfigureAwait(false);
    }

    IAsyncDisposable? LogSubscription = null;

    public async Task ToggleSystemDataChannel()
    {
        //if (!IsLogDataChannelMessage)
        //{
        //    if (Pair is BrowserClientPair bp && bp.SystemDataChannel is RTCDataChannelPair rp)
        //    {
        //        if (IsSource)
        //        {
        //            LogSubscription = await Client.Module.AddDataChannelLogMessageListener(((RTCDataChannelProxy)rp.Source).Value).ConfigureAwait(false);
        //        }
        //        else
        //        {
        //            LogSubscription = await Client.Module.AddDataChannelLogMessageListener(((RTCDataChannelProxy)rp.Target).Value).ConfigureAwait(false);
        //        }
        //    }
        //}
        //else
        //{
        //    await LogSubscription!.DisposeAsync().ConfigureAwait(false);
        //    LogSubscription = null;
        //}
    }

    public async Task RefreshPairState()
    {
        if (Pair is BrowserClientPair bp)
        {
            SourcePeerState = await bp.Peers.Source.GetConnectionState();
            TargetPeerState = await bp.Peers.Target.GetConnectionState();
        }
        else
        {
            SourcePeerState = "unknown";
            TargetPeerState = "unknown";
        }
        StateHasChanged();
    }
    public async Task SendPing()
    {
        //if (Pair is BrowserClientPair bp)
        //{
        //    if (IsSource)
        //    {
        //        await bp.SystemDataChannel.Source.SendAsync("ping");
        //    }
        //    else
        //    {
        //        await bp.SystemDataChannel.Target.SendAsync("ping");
        //    }
        //}
    }

    private async Task SendVideo()
    {
        if (CameraStream is null)
        {
            Console.WriteLine("Can not send video, camera video is not set yet");
            return;
        }
        if (VideoReceiveSubscription is not null)
        {
            VideoReceiveSubscription.Dispose();
        }
        var peerVideo = await Pair.SendVideo(CameraStream).ConfigureAwait(false);
        var bp = (BrowserClientPair)Pair;
        await bp.UISet().ConfigureAwait(false);
        await bp.TargetViewer!.SetPeerVideo((JSMediaStreamProxy)peerVideo);
    }
}