using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.BrowserClient;
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

    [Inject] DistributeXRApplicationService Application { get; set; } = default!;
    [Inject] DistributeXRUpdateLoopService UpdateLoop { get; set; } = default!;
    [Inject] BrowserClient.BrowserClient Client { get; set; }
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
        var FPS = 60;
        var pc = new RTCPeerConnection();
        var dc = await pc.createDataChannel("render");
        dc.send([1]);

        _ = Task.Run(async () =>
           {
               await foreach (var f in UpdateLoop.ReadAllRenderCommands())
               {
                   await RenderService.Render(f);
               }
           });
        UpdateLoop.IsRendering = true;
    }

    public void StopRender()
    {
        UpdateLoop.IsRendering = false;
    }

    async Task CreateRenderContext()
    {
        RenderService = new(await Client.Module.CreateWebGPURenderServiceAsync());
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
        if (Client is BrowserClient.BrowserClient bc)
        {
            bc.UserInterface = this;
        }
        await base.OnInitializedAsync().ConfigureAwait(false);
    }

    int FrameCount { get; set; } = -1;


    void UpdateFrameCount()
    {
        FrameCount = UpdateLoop.FrameCount;
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
        await Application.SetClients(Client, targetClient).ConfigureAwait(false);
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
        await Application.SendVideo(cameraStream, PeerClient).ConfigureAwait(false);
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

