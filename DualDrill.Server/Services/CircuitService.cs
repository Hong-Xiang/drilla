using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Server.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DualDrill.Server.Services;

sealed class InitializedClientContext
{
    public JSClientModule? ClientModule { get; set; }
}


sealed class CircuitService(
    Browser.BrowserClient Client,
    ClientStore ClientStore,
    Task<JSClientModule> ClientModule,
    IServiceProvider ServiceProvider) : CircuitHandler
{
    private readonly TaskCompletionSource<Circuit> CircuitSource = new();
    public Task<Circuit> GetCircuitAsync() => CircuitSource.Task;

    AsyncServiceScope? ServiceScope = null;


    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await base.OnCircuitOpenedAsync(circuit, cancellationToken).ConfigureAwait(false);

        var serviceScope = ServiceProvider.CreateAsyncScope();
        ServiceScope = serviceScope;
        var context = serviceScope.ServiceProvider.GetRequiredService<InitializedClientContext>();
        context.ClientModule = await ClientModule;

        CircuitSource.SetResult(circuit);
        ClientStore.AddClient(Client);
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (ServiceScope.HasValue)
        {
            await ServiceScope.Value.DisposeAsync();
        }

        ClientStore.RemoveClient(Client);
        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}
