using DualDrill.Engine.Connection;
using DualDrill.Engine.UI;
using DualDrill.Server.Application;
using DualDrill.Server.BrowserClient;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;
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
    [Inject] IServiceProvider ServiceProvider { get; set; }

    private ImmutableArray<string> PeerIds { get; set; } = [];

    private IP2PClientPair? Pair = null;

    private readonly CompositeDisposable Subscription = [];

    BrowserUIClient Client { get; set; } = default!;
    public string Id => Client.Id;
    public IClient? PeerClient { get; set; } = null;


    bool Connected => !(PeerClient is null);

    JSMediaStreamProxy? PeerVideoStream { get; set; }
    ElementReference PeerVideoElement { get; set; }
    ElementReference SelfVideoElement { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var circuit = await BrowserClientService.GetCircuitAsync().ConfigureAwait(false);
        Client = await BrowserUIClient.CreateAsync(ServiceProvider, circuit, this);
        ClientHub.AddClient(Id, Client);

        Subscription.Add(
            ClientHub.Clients.Subscribe(async (clients) =>
            {
                Logger.LogInformation("[id = {ClientId}] clients update, clients = {Clients}", Client.Id, string.Join(',', clients.Select(c => c.Id)));
                RefreshPeerIds();
                await InvokeAsync(StateHasChanged).ConfigureAwait(false);
            })
        );
        await base.OnInitializedAsync().ConfigureAwait(false);
        Console.WriteLine(Client);
        Console.WriteLine("intialized end");

        //Subscription.Add(client.PairedAsTarget.Subscribe(async (pair) =>
        //    {
        //        Logger.LogInformation("[id = {0}] pair update", client.Id);
        //        if (Pair is null)
        //        {
        //            Pair = pair;
        //            await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        //        }
        //        else
        //        {
        //            Logger.LogWarning("multiple pair constructed");
        //        }
        //    }) ?? Disposable.Empty);

    }

    int FrameCount { get; set; } = -1;

    public IObservable<IP2PClientPair> PairedAsTarget => throw new NotImplementedException();

    void UpdateFrameCount()
    {
        FrameCount = UpdateLoop.FrameCount;
    }

    void RefreshPeerIds()
    {
        PeerIds = [.. ClientHub.ClientIds.Where(c => c != Client.Id)];
    }

    async Task Connect(string targetId)
    {
        var targetClient = ClientHub.GetClient(targetId);
        if (targetClient is null || Client is null)
        {
            Console.WriteLine("Failed to get client");
            return;
        }
        if (targetClient is IBrowserClient bc)
        {
            await Application.SetClients(Client, bc).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Subscription.Dispose();
        if (Pair is BrowserClientPair bp)
        {
            await bp.DisposeAsync().ConfigureAwait(false);
        }
    }

    async Task IPeerVideoUI.ShowPeerVideo(JSMediaStreamProxy mediaStream)
    {
        await Client.Module.SetVideoElementStream(PeerVideoElement, mediaStream.MediaStream);
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
    }
    async Task IPeerVideoUI.ShowSelfVideo(JSMediaStreamProxy mediaStream)
    {
        await Client.Module.SetVideoElementStream(SelfVideoElement, mediaStream.MediaStream);
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
    }

    public Task<IP2PClientPair> CreatePairAsync(IClient target)
    {
        throw new NotImplementedException();
    }

    public bool Equals(IClient? other) => Client.Id == other?.Id;

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

    public async ValueTask RenderUpdatedState()
    {
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
    }

}