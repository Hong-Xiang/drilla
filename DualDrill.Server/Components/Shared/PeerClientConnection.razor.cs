using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Media;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Threading.Channels;


namespace DualDrill.Server.Components.Shared;

public partial class PeerClientConnection : IAsyncDisposable
{
    [Inject] ILogger<PeerClientConnection> Logger { get; set; }

    [Parameter]
    public IClient PeerClient { get; set; }

    [Parameter]
    public IClient SelfClient { get; set; }

    public JSMediaStreamProxy? SelfMediaStream => SelfClient is BrowserClient bc ? bc.MediaStream : null;

    [Parameter]
    public Action<JSMediaStreamProxy>? SetPeerStream { get; set; }

    JSMediaStreamProxy? PeerMediaStream { get; set; }

    RTCPeerConnectionPair? BrowserRTCPeerConnectionPair { get; set; } = null;

    Channel<object>? PeerChannel;

    Uri? PreviousPeerUri = null;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
    }

    bool IsConnected => BrowserRTCPeerConnectionPair is not null;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (PeerClient.Uri != PreviousPeerUri)
        {
            _ = Task.Run(async () =>
                   {
                       var peerUri = new Uri(SelfClient.Uri, $"peer/{PeerClient.Uri}");
                       Logger.LogInformation("Init listen peer {PeerUri}", peerUri);
                       await foreach (var e in SelfClient.GetOrAddEventChannel(peerUri)
                                                         .Reader.ReadAllAsync().ConfigureAwait(false))
                       {
                           if (e is RTCPeerConnectionPair pair)
                           {
                               await InvokeAsync(() =>
                                {
                                    BrowserRTCPeerConnectionPair = pair;
                                    StateHasChanged();
                                });
                           }
                           if (e is JSMediaStreamProxy video)
                           {
                               await InvokeAsync(() =>
                               {
                                   PeerMediaStream = video;
                                   StateHasChanged();
                               });
                           }
                       };
                   });
            PreviousPeerUri = PeerClient.Uri;
        }
    }

    bool Sending = false;
    async Task SendStream()
    {
        throw new NotImplementedException();
        Sending = true;
        var sendClient = SelfMediaStream.Client;
        var recvClient = PeerClient;
        var sendPeer = BrowserRTCPeerConnectionPair.GetSelf(sendClient);
        var recvPeer = BrowserRTCPeerConnectionPair.GetSelf(recvClient);

        using var tcs = new TaskCompletionSourceDotnetObjectReference<IJSObjectReference>();
        await using var sub = await recvPeer.WaitVideoStream(SelfMediaStream.Id, tcs);
        Logger.LogInformation("Wait called");

        await sendPeer.AddVideoStream(SelfMediaStream.Reference).ConfigureAwait(false);
        Logger.LogInformation("JS Add video stream called");
        var targetVideoJS = await tcs.Task.ConfigureAwait(false);
        //var targetModule = await (recvClient as BrowserClient).Module;
        //var targetVideoId = await targetModule.GetProperty<string>(targetVideoJS, "id");
        //var targetVideo = new JSMediaStreamProxy(recvClient, targetModule, targetVideoJS, targetVideoId);
        Logger.LogInformation("Target Video Received");
        if (PeerChannel is null)
        {
            var peerUri = new Uri(PeerClient.Uri, $"peer/{SelfClient.Id}");
            PeerChannel = PeerClient.GetOrAddEventChannel(peerUri);
        }
        //await PeerChannel.Writer.WriteAsync(targetVideo);
        await InvokeAsync(() =>
        {
            Sending = false;
            StateHasChanged();
        }).ConfigureAwait(false);
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
        if (BrowserRTCPeerConnectionPair is not null)
        {
            return;
        }
        var peerUri = new Uri(PeerClient.Uri, $"peer/{SelfClient.Id}");
        Logger.LogInformation("Connecting peer {PeerUri}", peerUri);
        BrowserRTCPeerConnectionPair = await RTCPeerConnectionPair.CreateAsync(SelfClient, PeerClient);
        PeerChannel = PeerClient.GetOrAddEventChannel(peerUri);
        await PeerChannel.Writer.WriteAsync(BrowserRTCPeerConnectionPair);
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

    public async ValueTask DisposeAsync()
    {
        if (BrowserRTCPeerConnectionPair is not null)
        {
            await BrowserRTCPeerConnectionPair.DisposeAsync();
        }
    }

    public ValueTask SetPeerClient(IClient client, RTCPeerConnectionPair pair)
    {
        throw new NotImplementedException();
    }
}