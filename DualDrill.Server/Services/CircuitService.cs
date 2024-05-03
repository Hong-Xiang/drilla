using DualDrill.Engine.Connection;
using DualDrill.Server.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DualDrill.Server.Services;

sealed class CircuitService(Browser.BrowserClient Client, ClientStore ClientStore) : CircuitHandler
{
    private readonly TaskCompletionSource<Circuit> CircuitSource = new();
    public Task<Circuit> GetCircuitAsync() => CircuitSource.Task;

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await base.OnCircuitOpenedAsync(circuit, cancellationToken).ConfigureAwait(false);
        CircuitSource.SetResult(circuit);
        ClientStore.AddClient(Client);
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        ClientStore.RemoveClient(Client);
        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}
