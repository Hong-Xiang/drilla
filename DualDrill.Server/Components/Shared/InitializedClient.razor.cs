using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Collections.Immutable;
using System.Windows.Input;

namespace DualDrill.Server.Components.Shared;

public partial class InitializedClient : IAsyncDisposable, IDesktopBrowserUI
{
    [Inject]
    IJSRuntime JSRuntime { get; set; }

    [Inject]
    ClientStore ClientHub { get; set; } = default!;

    [Inject]
    ILogger<InitializedClient> Logger { get; set; } = default!;

    [Parameter]
    public IClient Client { get; set; } = default!;

    [CascadingParameter]
    public JSClientModule ClientModule { get; set; } = default!;

    [Parameter]
    public JSRenderService RenderService { get; set; } = default!;

    ElementReference RenderRootElement;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            UpdatePeerClients(ClientHub.Clients);
            ClientHub.OnClientChanges += UpdatePeerClients;
        }
        await base.OnAfterRenderAsync(firstRender);
        await RenderService.AttachToElementAsync(RenderRootElement);
        Logger.LogInformation("Blazor render attach to element called");
    }

    JSMediaStreamProxy? SelfMediaStream { get; set; }
    JSMediaStreamProxy? SelectedPeerMediaStream { get; set; }

    private ImmutableArray<IClient> PeerClientCandidates { get; set; } = [];
    private Dictionary<IClient, RTCPeerConnectionPair> PeerConnectionPair = [];


    void RefreshPeerIds()
    {
        UpdatePeerClients(ClientHub.Clients);
    }

    void UpdatePeerClients(ImmutableArray<IClient> peerClients)
    {
        InvokeAsync(() =>
        {
            PeerClientCandidates = [.. peerClients.Where(c => c.Uri != Client.Uri)];
            StateHasChanged();
        });
    }

    async Task CaptureCamera()
    {

        if (SelfMediaStream is not null)
        {
            await SelfMediaStream.DisposeAsync();
        }
        SelfMediaStream = await new MediaDevices(Client, JSRuntime).GetUserMedia(ClientModule, false, true);
        if (Client is BrowserClient bc)
        {
            bc.MediaStream = SelfMediaStream;
        }
    }

    async Task RemoveStream()
    {
        if (Client is BrowserClient bc)
        {
            bc.MediaStream = null;
        }
        if (SelfMediaStream is not null)
        {
            await SelfMediaStream.DisposeAsync();
        }
        SelfMediaStream = null;
    }

    void SetPeerStream(JSMediaStreamProxy peerStream)
    {
        SelectedPeerMediaStream = peerStream;
        _ = InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        ClientHub.OnClientChanges -= UpdatePeerClients;
    }

    public ValueTask<IJSObjectReference> GetCanvasElement()
    {
        throw new NotImplementedException();
    }

    public ValueTask SetPeerClient(IClient client, RTCPeerConnectionPair pair)
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