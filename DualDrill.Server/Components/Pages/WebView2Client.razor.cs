using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using DualDrill.Server.CustomEvents;
using DualDrill.Server.Services;
using DualDrill.Server.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using SIPSorcery.Net;
using System.Collections.Immutable;
using System.Numerics;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DualDrill.Server.Components.Pages;

public partial class WebView2Client : IAsyncDisposable
{
    [Inject] ClientStore ClientHub { get; set; } = default!;
    [Inject] ILogger<WebView2Client> Logger { get; set; } = default!;
    [Inject] IHubContext<DrillHub, IDrillHubClient> HubContext { get; set; } = default!;
    [Inject] IJSRuntime jsRuntime { get; set; }
    BrowserClient? Client { get; set; }
    JSClientModule Module { get; set; } = default;
    [Inject] WebViewService WebViewService { get; set; } = default!;
    JSRenderService? RenderService { get; set; } = null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            Module = await JSClientModule.CreateAsync(jsRuntime);
            var connectionId = await Module.GetSignalRConnectionIdAsync();
            Client = new BrowserClient(jsRuntime, Module, HubContext, connectionId);
            ClientHub.AddClient(Client);
            RenderService = new(await Module.CreateHeadlessSharedBufferServerRenderService());
            //RenderService = new(await Client.Module.CreateHeadlessServerRenderService());
            //RenderService = new(await (await Client.Module).CreateHeadlessSharedBufferServerRenderService());
            //RenderService = new(await Client.Module.CreateServerRenderPresentService());
            Logger.LogInformation("Blazor render first render called");
            StateHasChanged();
        }
    }
    public async Task TriggerPushSharedBuffer()
    {
        await WebViewService.PostSharedBufferAsync(default);
    }

    public async ValueTask DisposeAsync()
    {
    }
}
