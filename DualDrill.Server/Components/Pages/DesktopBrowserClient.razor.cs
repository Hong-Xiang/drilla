using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using DualDrill.Server.CustomEvents;
using DualDrill.Server.Services;
using DualDrill.Server.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Implementation;
using SIPSorcery.Net;
using System.Collections.Immutable;
using System.Numerics;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DualDrill.Server.Components.Pages;

public partial class DesktopBrowserClient : IAsyncDisposable
{
    [Inject] ClientStore ClientHub { get; set; } = default!;
    [Inject] ILogger<DesktopBrowserClient> Logger { get; set; } = default!;
    [Inject] PeerClientConnectionService ConnectionService { get; set; } = default!;
    [Inject] FrameSimulationService UpdateService { get; set; } = default!;
    [Inject] IHubContext<DrillHub, IDrillHubClient> HubContext { get; set; } = default!;
    BrowserClient? Client { get; set; }
    JSClientModule Module { get; set; } = default!;
    [Inject] IJSRuntime jsRuntime { get; set; }

    private ImmutableArray<Uri> PeerUris { get; set; } = [];

    private readonly CompositeDisposable Subscription = [];

    public IClient? PeerClient { get; set; } = null;


    bool Connected => PeerClient is not null;

    ElementReference PeerVideoElement { get; set; }
    ElementReference SelfVideoElement { get; set; }

    ElementReference RenderRootElement { get; set; }

    JSRenderService? RenderService { get; set; } = null;

    public void StartRender()
    {
    }

    public void StopRender()
    {
    }


    async Task CreateRenderContext()
    {
    }

    Task? ScaleSubscribe { get; set; } = null;

    ElementSize? RootElementSize { get; set; } = null;

    protected override async Task OnInitializedAsync()
    {
        //Subscription.Add(ClientHub.Clients.Subscribe(async (clients) =>
        //{
        //    Logger.LogInformation("[uri = {ClientUri}] clients update, clients = {Clients}", Client.Uri, string.Join(',', clients.Select(c => c.Uri.ToString())));
        //    RefreshPeerIds();
        //    await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        //}));
        //if (Client is BrowserClient bc)
        //{
        //    bc.UserInterface = this;
        //}
        await base.OnInitializedAsync().ConfigureAwait(false);

        ScaleSubscribe = SubscribeScale();
    }

    async Task SubscribeScale()
    {
        await foreach (var s in UpdateService.CubeScaleChanges(1.0f, CancellationToken.None))
        {
            Console.WriteLine("Scale Changed");
            await InvokeAsync(StateHasChanged);
        }
    }

    public float Scale
    {
        get => UpdateService.Scale;
        set
        {
            if (UpdateService.Scale != value)
            {
                UpdateService.Scale = value;
            }
        }
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
        if (Client is not null)
        {
            ClientHub.RemoveClient(Client);
        }
        await Module.DisposeAsync();
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
        if (firstRender)
        {
            Module = await JSClientModule.CreateAsync(jsRuntime);
            var connectionId = await Module.GetSignalRConnectionIdAsync();
            Client = new BrowserClient(jsRuntime, Module, HubContext, connectionId);
            await Client.HubInvokeAsync(async (hub) =>
            {
                if (hub is DrillHub dhub)
                {
                    Console.WriteLine($"HubInvoke get : {dhub.Context.ConnectionId}");
                }
            });
            ClientHub.AddClient(Client);
            RenderService = new(await (await Client.Module).CreateWebGPURenderServiceAsync());
            //RenderService = new(await Client.Module.CreateHeadlessServerRenderService());
            //RenderService = new(await (await Client.Module).CreateHeadlessSharedBufferServerRenderService());
            //RenderService = new(await Client.Module.CreateServerRenderPresentService());
            Logger.LogInformation("Blazor render first render called");
            StateHasChanged();
        }
    }

    public async ValueTask ShowPeerVideo(IMediaStream stream)
    {
        Console.WriteLine("Show Peer Video");
        await using var videoElementRef = await Module.CreateJSObjectReferenceAsync(PeerVideoElement).ConfigureAwait(false);
        await Module.SetVideoElementStreamAsync(videoElementRef, ((JSMediaStreamProxy)stream).Reference);
    }

    public async ValueTask ShowSelfVideo(IMediaStream stream)
    {
        Console.WriteLine("Show Self Video");
        await using var videoElementRef = await Module.CreateJSObjectReferenceAsync(SelfVideoElement).ConfigureAwait(false);
        await Module.SetVideoElementStreamAsync(videoElementRef, ((JSMediaStreamProxy)stream).Reference);
    }

    public async ValueTask ClosePeerVideo()
    {
        Console.WriteLine("Close Peer Video");
        await using var videoElementRef = await Module.CreateJSObjectReferenceAsync(PeerVideoElement).ConfigureAwait(false);
        var videoProxy = new JsVideoElementProxy(Client, Module, videoElementRef);
        var mediaStream = await videoProxy.GetStream();
        var camera = await mediaStream.GetVideoTrack(0);
        await camera.Stop();

        await Module.RemoveVideoElementStreamAsync(videoElementRef);
    }
    public async ValueTask CloseSelfVideo()
    {
        Console.WriteLine("Close Self Video");
        await using var videoElementRef = await Module.CreateJSObjectReferenceAsync(SelfVideoElement).ConfigureAwait(false);
        var videoProxy = new JsVideoElementProxy(Client, Module, videoElementRef);
        var mediaStream = await videoProxy.GetStream();
        var camera = await mediaStream.GetVideoTrack(0);
        await camera.Stop();

        await Module.RemoveVideoElementStreamAsync(videoElementRef);
    }

    public async ValueTask<IJSObjectReference> GetCanvasElement()
    {
        return await Module.CreateJSObjectReferenceAsync(RenderRootElement);
    }

    Vector2? DragStart { get; set; } = null;

    public void PointerDown(NormalizedPointEventArgs e)
    {
        DragStart = new Vector2
        {
            X = e.OffsetX / e.OffsetWidth,
            Y = e.OffsetY / e.OffsetHeight,
        };
    }
    public void PointerMove(NormalizedPointEventArgs e)
    {
        if (DragStart is null)
        {
            return;
        }
        var x = e.OffsetX / e.OffsetWidth;
        var y = e.OffsetY / e.OffsetHeight;
        var v = new Vector2 { X = x, Y = y };

    }
    public void PointerUp(NormalizedPointEventArgs e)
    {
        DragStart = null;
    }
}
