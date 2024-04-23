using DualDrill.Common.ResourceManagement;
using DualDrill.Engine.Connection;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;
using System.Reactive.Subjects;
using DualDrill.Engine.UI;

namespace DualDrill.Server.BrowserClient;

public class BrowserClient(IServiceProvider Services, Circuit Circuit, IJSRuntime JSRuntime, JSDrillClientModule Module)
    : IClient, IBrowserClient, IAsyncDisposable
{
    public Circuit Circuit { get; } = Circuit;
    public IJSRuntime JSRuntime { get; } = JSRuntime;
    public JSDrillClientModule Module { get; } = Module;
    public IServiceProvider Services { get; } = Services;

    public string Id => Circuit.Id;

    private Subject<IP2PClientPair> PairedAsTargetSubject { get; } = new();
    public IObservable<IP2PClientPair> PairedAsTarget => PairedAsTargetSubject;

    public static async Task<BrowserClient> CreateAsync(
        IServiceProvider services,
        Circuit circuit,
        IJSRuntime jsRuntime
    )
    {
        Console.WriteLine("Loading client js module");
        var module = await JSDrillClientModule.ImportModuleAsync(jsRuntime).ConfigureAwait(false);
        return new BrowserClient(services, circuit, jsRuntime, module);
    }

    public async Task<IP2PClientPair> CreatePairAsync(IClient target)
    {
        var browserTarget = (BrowserClient)target;
        var pair = await BrowserClientPair.CreateAsync(this, browserTarget);
        browserTarget.PairedAsTargetSubject.OnNext(pair);
        return pair;
    }

    public async ValueTask DisposeAsync()
    {
        await Module.DisposeAsync().ConfigureAwait(false);
        PairedAsTargetSubject.Dispose();
    }

    public bool Equals(IClient? other) => other?.Id == Id;

    public ValueTask<JSMediaStreamProxy> GetCameraStreamAsync()
    {
        throw new NotImplementedException();
    }
}
