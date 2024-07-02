using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SIPSorcery.Net;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DualDrill.Server.Components.Pages;

public partial class HeadsetClient : IAsyncDisposable, IDesktopBrowserUI
{
    [Inject] ClientStore ClientHub { get; set; } = default!;
    [Inject] ILogger<DesktopBrowserClient> Logger { get; set; } = default!;

    [Inject] PeerClientConnectionService ConnectionService { get; set; } = default!;
    [Inject] FrameSimulationService XRApplication { get; set; } = default!;
    [Inject] BrowserClient Client { get; set; }
    [Inject] JSClientModule Module { get; set; } = default!;


    private ImmutableArray<Uri> PeerUris { get; set; } = [];

    private readonly CompositeDisposable Subscription = [];

    public string Id => Client.Uri.AbsolutePath;
    public IClient? PeerClient { get; set; } = null;


    bool Connected => !(PeerClient is null);

    ElementReference PeerVideoElement { get; set; }
    ElementReference SelfVideoElement { get; set; }

    ElementReference RenderCanvasElement { get; set; }

    protected override async Task OnInitializedAsync()
    {
        //Subscription.Add(
        //    ClientHub.Clients.Subscribe(async (clients) =>
        //    {
        //        Logger.LogInformation("[uri = {ClientUri}] clients update, clients = {Clients}", Client.Uri, string.Join(',', clients.Select(c => c.Uri.ToString())));
        //        RefreshPeerIds();
        //        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        //    })
        //);
        if (Client is BrowserClient bc)
        {
            bc.UserInterface = this;
        }
        await base.OnInitializedAsync().ConfigureAwait(false);

        _ = SubscribeScale();
    }

    async Task SubscribeScale()
    {
        await foreach (var s in XRApplication.CubeScaleChanges(1.0f, CancellationToken.None))
        {
            await InvokeAsync(StateHasChanged);
        }
    }

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
        await using var videoElementRef = await Module.CreateJSObjectReferenceAsync(PeerVideoElement).ConfigureAwait(false);
        await Module.SetVideoElementStreamAsync(videoElementRef, ((JSMediaStreamProxy)stream).Reference);
    }

    public async ValueTask ShowSelfVideo(IMediaStream stream)
    {
        Console.WriteLine("Show Self Video");
        await using var videoElementRef = await Module.CreateJSObjectReferenceAsync(SelfVideoElement).ConfigureAwait(false);
        await Module.SetVideoElementStreamAsync(videoElementRef, ((JSMediaStreamProxy)stream).Reference);
    }

    public async ValueTask<IJSObjectReference> GetCanvasElement()
    {
        return await Module.CreateJSObjectReferenceAsync(RenderCanvasElement);
    }

    public async ValueTask RemovePeerClient()
    {
        PeerClient = null;
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
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
}

