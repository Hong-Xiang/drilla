using DualDrill.Engine.Connection;
using DualDrill.Server.Application;
using DualDrill.Server.Components.Pages;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;

namespace DualDrill.Server.BrowserClient;

sealed class BrowserUIClient(BrowserClient HeadlessClient, MediaDevices MediaDevices, IDesktopBrowserUI UI)
    : IBrowserClient, IUIClient
{
    public string Id => HeadlessClient.Id;
    public Circuit Circuit => HeadlessClient.Circuit;
    public JSDrillClientModule Module => HeadlessClient.Module;

    public static async Task<BrowserUIClient> CreateAsync(
        IServiceProvider services,
        Circuit circuit,
        DesktopBrowserClient ui
    )
    {
        var client = await BrowserClient.CreateAsync(
            services,
            circuit,
            services.GetRequiredService<IJSRuntime>()
        );
        return new(client, services.GetRequiredService<MediaDevices>(), ui);
    }

    public IObservable<IP2PClientPair> PairedAsTarget => throw new NotImplementedException();

    public BrowserClient HeadlessClient { get; } = HeadlessClient;
    public MediaDevices MediaDevices { get; } = MediaDevices;
    public IDesktopBrowserUI UI { get; } = UI;

    public Task<IP2PClientPair> CreatePairAsync(IClient target)
    {
        throw new NotImplementedException();
    }

    public bool Equals(IClient? other) => other?.Id == Id;

    public async ValueTask<JSMediaStreamProxy> GetCameraStreamAsync()
    {
        return await MediaDevices.GetUserMedia(this, audio: false, video: true);
    }
}
