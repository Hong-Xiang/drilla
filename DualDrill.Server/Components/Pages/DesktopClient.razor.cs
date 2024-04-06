using DualDrill.Engine.Connection;
using DualDrill.Server.BrowserClient;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DualDrill.Server.Components.Pages;

public partial class DesktopClient : IAsyncDisposable
{
    [Inject] MediaDevices MediaDevices { get; set; } = default!;
    [Inject] IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] BrowserClient.BrowserClient BrowserClient { get; set; } = default!;
    [Inject] ClientHub ClientHub { get; set; } = default!;
    [Inject] ILogger<DesktopClient> Logger { get; set; } = default!;
    ClientModule ClientModule { get; set; } = default!;
    ElementReference SelfVideoElement { get; set; }
    ElementReference PeerVideoElement { get; set; }
    ElementReference JSCallbackTestButton { get; set; }
    private string CircuitId { get; set; } = default!;
    private ImmutableArray<string> PeerIds { get; set; } = [];

    private IClientPeerToPeerPair? Pair = null;

    private CompositeDisposable Subscription = new();

    private string SourcePeerState = "unknown";
    private string TargetPeerState = "unknown";


    async Task PlayCameraStream()
    {
        var stream = await MediaDevices.GetUserMedia();
        await ClientModule.ShowCameraStream(SelfVideoElement, stream);
    }
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync().ConfigureAwait(false);
        await BrowserClient.Initialized.ConfigureAwait(false);
        Subscription.Add(
            ClientHub.Clients.Subscribe((clients) =>
            {
                Logger.LogInformation("[id = {0}] clients update, clients = {1}", BrowserClient.Id, string.Join(',', clients.Select(c => c.Id)));
                PeerIds = [.. clients.Select(static c => c.Id).Where(id => id != BrowserClient.Id)];
                InvokeAsync(StateHasChanged);
            })
        );

        Subscription.Add(BrowserClient.ConnectAsTargetPair.Subscribe((pair) =>
            {
                Logger.LogInformation("[id = {0}] pair update", BrowserClient.Id);
                if (Pair is null)
                {
                    Pair = pair;
                    InvokeAsync(StateHasChanged);
                }
                else
                {
                    Logger.LogWarning("multiple pair constructed");
                }
            }));

        ClientModule = BrowserClient.Module;
        CircuitId = BrowserClient.Id;
    }

    void RefreshPeerIds()
    {
        PeerIds = [.. ClientHub.ClientIds.Where(id => id != CircuitId)];
    }

    async Task Connect(string targetId)
    {
        var targetClient = ClientHub.GetClient(targetId);
        if (targetClient is null)
        {
            Console.WriteLine("Failed to get client");
            return;
        }
        Pair = await ClientHub.CreatePeerPairAsync(BrowserClient, targetClient);
    }

    public async Task RefreshPairState()
    {
        if (Pair is BrowserClientPair bp)
        {
            SourcePeerState = await bp.SourcePeer.GetConnectionState();
            TargetPeerState = await bp.SourcePeer.GetConnectionState();
        }
        else
        {
            SourcePeerState = "unknown";
            TargetPeerState = "unknown";
        }
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        Subscription.Dispose();
        if (Pair is BrowserClientPair bp)
        {
            await bp.DisposeAsync().ConfigureAwait(false);
        }
    }
}