using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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


    bool Connected => PeerClient is not null;

    ElementReference PeerVideoElement { get; set; }
    ElementReference SelfVideoElement { get; set; }

    ElementReference RenderRootElement { get; set; }

    JSRenderService? RenderService { get; set; } = null;

    public void StartRender()
    {
        if (RenderService is not null && XRApplication is not null)
        {

            XRApplication.RenderService = RenderService;
        }
    }

    public void StopRender()
    {
        if (XRApplication is not null)
        {
            XRApplication.RenderService = null;
        }
    }

    async Task CreateRenderContext()
    {
        await using var canvasElement = await Client.Module.CreateObjectReferenceAsync(RenderRootElement);
        RenderService = new(await Client.Module.CreateWebGPURenderServiceAsync(canvasElement));
    }

    Task? ScaleSubscribe { get; set; } = null;

    protected override async Task OnInitializedAsync()
    {
        var circuit = await BrowserClientService.GetCircuitAsync().ConfigureAwait(false);
        Subscription.Add(ClientHub.Clients.Subscribe(async (clients) =>
        {
            Logger.LogInformation("[uri = {ClientUri}] clients update, clients = {Clients}", Client.Uri, string.Join(',', clients.Select(c => c.Uri.ToString())));
            RefreshPeerIds();
            await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        }));
        if (Client is BrowserClient bc)
        {
            bc.UserInterface = this;
        }
        await base.OnInitializedAsync().ConfigureAwait(false);

        ScaleSubscribe = SubscribeScale();
    }

    async Task SubscribeScale()
    {
        await foreach (var s in XRApplication.ScaleChanges(CancellationToken.None))
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    int FrameCount { get; set; } = -1;

    public float Scale
    {
        get => XRApplication.Scale;
        set
        {
            if (XRApplication.Scale != value)
            {
                XRApplication.Scale = value;
            }
        }
    }

    void UpdateFrameCount()
    {
        FrameCount = XRApplication.FrameCount;
    }

    void RefreshPeerIds()
    {
        PeerUris = [.. ClientHub.ClientUris.Where(c => c != Client.Uri)];
    }

    async Task Connect(Uri headsetUri)
    {
        var headsetClient = ClientHub.GetClient(headsetUri);
        if (headsetClient is null || Client is null)
        {
            Logger.LogError("Failed to get client for connection");
            return;
        }
        await ConnectionService.SetClients(headsetClient, Client).ConfigureAwait(false);
    }

    async Task Disconnect()
    {
        if (Client is null)
        {
            Logger.LogError("Failed to get client for connection");
            return;
        }
        await ConnectionService.ResetClients().ConfigureAwait(false);
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

    public async ValueTask RemovePeerClient()
    {
        PeerClient = null;
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

    public async ValueTask ClosePeerVideo()
    {
        Console.WriteLine("Close Peer Video");
        await using var videoElementRef = await Module.CreateObjectReferenceAsync(PeerVideoElement).ConfigureAwait(false);
        var videoProxy = new JsVideoElementProxy(Client, Module, videoElementRef);
        var mediaStream = await videoProxy.GetStream();
        var camera = await mediaStream.GetVideoTrack(0);
        await camera.Stop();

        await Module.RemoveVideoElementStreamAsync(videoElementRef);
    }
    public async ValueTask CloseSelfVideo()
    {
        Console.WriteLine("Close Self Video");
        await using var videoElementRef = await Module.CreateObjectReferenceAsync(SelfVideoElement).ConfigureAwait(false);
        var videoProxy = new JsVideoElementProxy(Client, Module, videoElementRef);
        var mediaStream = await videoProxy.GetStream();
        var camera = await mediaStream.GetVideoTrack(0);
        await camera.Stop();

        await Module.RemoveVideoElementStreamAsync(videoElementRef) ;
    }

    public async ValueTask<IJSObjectReference> GetCanvasElement()
    {
        return await Module.CreateObjectReferenceAsync(RenderRootElement);
    }
}

