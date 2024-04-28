using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Components;
using SIPSorcery.Net;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DualDrill.Server.Components.Pages;

public partial class DesktopBrowserClient : IAsyncDisposable, IDesktopBrowserUI
{
    [Inject] CircuitService BrowserClientService { get; set; } = default!;
    [Inject] ClientStore ClientHub { get; set; } = default!;
    [Inject] ILogger<DesktopBrowserClient> Logger { get; set; } = default!;

    [Inject] DistributeXRConnectionService ConnectionService { get; set; } = default!;
    [Inject] DistributeXRApplication XRApplication { get; set; } = default!;
    [Inject] BrowserClient Client { get; set; }
    [Inject] JSClientModule Module { get; set; } = default!;

    private ImmutableArray<Uri> PeerUris { get; set; } = [];

    private readonly CompositeDisposable Subscription = [];

    public string Id => Client.Uri.AbsolutePath;
    public IClient? PeerClient { get; set; } = null;


    bool Connected => !(PeerClient is null);

    ElementReference PeerVideoElement { get; set; }
    ElementReference SelfVideoElement { get; set; }

    ElementReference RenderRootElement { get; set; }

    JSRenderService RenderService { get; set; }

    public async Task Render()
    {
        XRApplication.RenderService = RenderService;
    }

    public void StopRender()
    {
        XRApplication.RenderService = null;
    }

    async Task CreateRenderContext()
    {
        await using var canvasElement = await Client.Module.CreateObjectReferenceAsync(RenderRootElement);
        RenderService = new(await Client.Module.CreateWebGPURenderServiceAsync(canvasElement));
    }

    protected override async Task OnInitializedAsync()
    {
        var circuit = await BrowserClientService.GetCircuitAsync().ConfigureAwait(false);
        ClientHub.AddClient(Client);
        Subscription.Add(
            ClientHub.Clients.Subscribe(async (clients) =>
            {
                Logger.LogInformation("[uri = {ClientUri}] clients update, clients = {Clients}", Client.Uri, string.Join(',', clients.Select(c => c.Uri.ToString())));
                RefreshPeerIds();
                await InvokeAsync(StateHasChanged).ConfigureAwait(false);
            })
        );
        if (Client is Browser.BrowserClient bc)
        {
            bc.UserInterface = this;
        }
        await base.OnInitializedAsync().ConfigureAwait(false);
    }

    int FrameCount { get; set; } = -1;


    void UpdateFrameCount()
    {
        FrameCount = XRApplication.FrameCount;
    }

    void RefreshPeerIds()
    {
        PeerUris = [.. ClientHub.ClientUris.Where(c => c != Client.Uri)];
    }

    async Task Connect(Uri targetUri)
    {
        var targetClient = ClientHub.GetClient(targetUri);
        if (targetClient is null || Client is null)
        {
            Logger.LogError("Failed to get client for connection");
            return;
        }
        await ConnectionService.SetClients(Client, targetClient).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        Subscription.Dispose();
    }

    private async Task SendVideo()
    {
        if (PeerClient is null)
        {
            Logger.LogError("Can not send video, not connected yet");
            return;
        }
        var cameraStream = await Client.GetCameraStreamAsync().ConfigureAwait(false);
        await ConnectionService.SendVideo(cameraStream, PeerClient).ConfigureAwait(false);
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
    }

    public async ValueTask SetPeerClient(IClient client)
    {
        PeerClient = client;
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);

    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
    }

    public async ValueTask ShowPeerVideo(IMediaStream stream)
    {
        Console.WriteLine("Show Peer Video");
        await using var videoElementRef = await Module.CreateObjectReferenceAsync(PeerVideoElement).ConfigureAwait(false);
        await Module.SetVideoElementStreamAsync(videoElementRef, ((JSMediaStreamProxy)stream).Reference);
    }

    public async ValueTask ShowSelfVideo(IMediaStream stream)
    {
        Console.WriteLine("Show Self Video");
        await using var videoElementRef = await Module.CreateObjectReferenceAsync(SelfVideoElement).ConfigureAwait(false);
        await Module.SetVideoElementStreamAsync(videoElementRef, ((JSMediaStreamProxy)stream).Reference);
    }
}

