using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Reactive.Disposables;

namespace DualDrill.Server.Components.Pages;

public partial class DesktopBrowserClient : IAsyncDisposable
{
    [Inject] ClientStore ClientHub { get; set; } = default!;
    [Inject] ILogger<DesktopBrowserClient> Logger { get; set; } = default!;
    [Inject] IHubContext<DrillHub, IDrillHubClient> HubContext { get; set; } = default!;
    BrowserClient? Client { get; set; }
    JSClientModule Module { get; set; } = default!;
    [Inject] IJSRuntime jsRuntime { get; set; }
    JSRenderService? RenderService { get; set; } = null;

    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            ClientHub.RemoveClient(Client);
        }
        await Module.DisposeAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            Module = await JSClientModule.CreateAsync(jsRuntime);
            var connectionId = await Module.GetSignalRConnectionIdAsync();
            Client = new BrowserClient(jsRuntime, Module, HubContext, connectionId);
            ClientHub.AddClient(Client);
            RenderService = new(await (await Client.Module).CreateWebGPURenderServiceAsync());
            //RenderService = new(await Client.Module.CreateHeadlessServerRenderService());
            //RenderService = new(await (await Client.Module).CreateHeadlessSharedBufferServerRenderService());
            //RenderService = new(await Client.Module.CreateServerRenderPresentService());
            Logger.LogInformation("Blazor render first render called");
            StateHasChanged();
        }
    }
}
