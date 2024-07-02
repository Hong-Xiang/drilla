using Autofac;
using Autofac.Extensions.DependencyInjection;
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
    public static readonly string ScopeName = nameof(InitializedClientContext);
    public JSClientModule? ClientModule { get; private set; }

    public static async ValueTask<ILifetimeScope> CreateAsync(IServiceProvider serviceProvider)
    {
        var root = serviceProvider.GetAutofacRoot();
        var scope = root.BeginLifetimeScope(ScopeName);
        var context = scope.Resolve<InitializedClientContext>();
        context.ClientModule = await serviceProvider.GetRequiredService<Task<JSClientModule>>();
        //await context.ClientModule.Initialization();
        return scope;
    }
}



sealed class CircuitService(
    //Browser.BrowserClient Client,
    ClientStore ClientStore,
    IServiceProvider ServiceProvider) : CircuitHandler
{
    private readonly TaskCompletionSource<Circuit> CircuitSource = new();
    public Task<Circuit> GetCircuitAsync() => CircuitSource.Task;

    ILifetimeScope? InitializedClientScope;


    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await base.OnCircuitOpenedAsync(circuit, cancellationToken).ConfigureAwait(false);

        //InitializedClientScope = await InitializedClientContext.CreateAsync(ServiceProvider);
        CircuitSource.SetResult(circuit);
        //ClientStore.AddClient(Client);
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        //if (InitializedClientScope is not null)
        //{
        //    await InitializedClientScope.DisposeAsync();
        //}

        //ClientStore.RemoveClient(Client);
        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}
