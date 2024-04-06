using DualDrill.Engine.Connection;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.Reactive.Subjects;

namespace DualDrill.Server.BrowserClient;

sealed class BrowserClient(IServiceProvider ServiceProvider, ClientHub ClientHub, IJSRuntime JSRuntime)
    : CircuitHandler, IClient, IAsyncDisposable
{
    private readonly TaskCompletionSource InitializedSource = new();

    private Circuit? _circuit = null;
    public Circuit Circuit => _circuit ?? throw new Exception("Not initialized yet");
    public IServiceProvider ServiceProvider { get; } = ServiceProvider;
    public IJSRuntime JSRuntime { get; } = JSRuntime;

    public Task Initialized => InitializedSource.Task;

    public string Id => Circuit?.Id ?? throw new Exception("Not initialized yet");

    private ClientModule? _module = null;
    public ClientModule Module => _module ?? throw new Exception("Not initialized yet");

    private Subject<IClientPeerToPeerPair> SourcePeer = new();
    private Subject<IClientPeerToPeerPair> TargetPeer = new();

    public IObservable<IClientPeerToPeerPair> ConnectAsTargetPair => TargetPeer;

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await base.OnCircuitOpenedAsync(circuit, cancellationToken).ConfigureAwait(false);
        _circuit = circuit;
        _module = await ClientModule.ImportAsync(JSRuntime).ConfigureAwait(false);
        InitializedSource.SetResult();
        ClientHub.AddClient(circuit.Id, this);
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        ClientHub.RemoveClient(this);
        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    public async Task<IClientPeerToPeerPair> CreatePairAsync(IClient target)
    {
        var browserTarget = (BrowserClient)target;
        var pair = await BrowserClientPair.CreateAsync(this, browserTarget);
        SourcePeer.OnNext(pair);
        browserTarget.TargetPeer.OnNext(pair);
        return pair;
    }

    public async ValueTask DisposeAsync()
    {
        SourcePeer.Dispose();
        TargetPeer.Dispose();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
