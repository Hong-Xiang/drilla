using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Browser;
using Microsoft.AspNetCore.Components;
using DualDrill.Server.Application;
using Microsoft.JSInterop;


namespace DualDrill.Server.Components.Shared;

public partial class PeerClientConnection : IDesktopBrowserUI
{
    [Inject] DualDrill.Server.Application.PeerClientConnectionService ConnectionService { get; set; }
    [Inject] DualDrill.Engine.Connection.ClientStore ClientHub { get; set; }
    [Inject] ILogger<PeerClientConnection> Logger { get; set; }

    [Parameter]
    public IClient PeerClient { get; set; }

    [Parameter]
    public IClient SelfClient { get; set; }

    public JSMediaStreamProxy? SelfMediaStream => SelfClient is BrowserClient bc ? bc.MediaStream : null;

    JSMediaStreamProxy? SentMediaStream { get; set; }

    [Parameter]
    public Action<JSMediaStreamProxy>? SetPeerStream { get; set; }

    JSMediaStreamProxy? PeerMediaStream { get; set; }

    RTCPeerConnectionPair? BrowserRTCPeerConnectionPair { get; set; } = null;


    bool IsConnected => BrowserRTCPeerConnectionPair is not null;

    protected override async Task OnParametersSetAsync()
    {
        if (SelfClient is BrowserClient bc)
        {
            bc.UserInterface = this;
        }
        await base.OnParametersSetAsync();
    }

    async Task SendStream()
    {
    }

    async Task SelectAsShownPeerStream()
    {
        if (PeerMediaStream is not null && SetPeerStream is not null)
        {
            SetPeerStream(PeerMediaStream);
        }
    }

    async Task Connect()
    {
        if (PeerClient is null || SelfClient is null)
        {
            Logger.LogError("Failed to get client for connection");
            return;
        }
        BrowserRTCPeerConnectionPair = await RTCPeerConnectionPair.CreateAsync(SelfClient, PeerClient);
        // if (displayClient is Browser.BrowserClient sui)
        // {
        //     var stream = await sui.GetCameraStreamAsync().ConfigureAwait(false);
        //     //var canvas = await sui.ExecuteCommandAsync(new GetRenderCanvas());
        //     //var stream = await sui.Module.CaptureStream(sui, canvas);
        //     await SendVideo(stream, renderClient);
        // }
        // if (renderClient is Browser.BrowserClient tui)
        // {
        //     var stream = await tui.GetCameraStreamAsync().ConfigureAwait(false);
        //     //var canvas = await tui.ExecuteCommandAsync(new GetRenderCanvas());
        //     //var stream = await (await tui.Module).CaptureCanvasToStream(tui, canvas);

        //     await SendVideo(stream, displayClient);
        // }
        // AutoResetClientsWhenFailed(BrowserRTCPeerConnectionPair);
    }

    public ValueTask<IJSObjectReference> GetCanvasElement()
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPeerClient(IClient client)
    {
        throw new NotImplementedException();
    }

    public ValueTask RemovePeerClient()
    {
        throw new NotImplementedException();
    }

    public ValueTask ShowPeerVideo(IMediaStream stream)
    {
        throw new NotImplementedException();
    }

    public ValueTask ShowSelfVideo(IMediaStream stream)
    {
        throw new NotImplementedException();
    }

    public ValueTask ClosePeerVideo()
    {
        throw new NotImplementedException();
    }

    public ValueTask CloseSelfVideo()
    {
        throw new NotImplementedException();
    }
}