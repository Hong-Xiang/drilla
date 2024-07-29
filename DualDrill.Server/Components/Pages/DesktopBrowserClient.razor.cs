using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Graphics.Headless;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Text.Json;

namespace DualDrill.Server.Components.Pages;

public partial class DesktopBrowserClient : IAsyncDisposable
{
    [Inject] ClientStore ClientHub { get; set; } = default!;
    [Inject] ILogger<DesktopBrowserClient> Logger { get; set; } = default!;
    [Inject] IHubContext<DrillHub, IDrillHubClient> HubContext { get; set; } = default!;
    BrowserClient? Client { get; set; }
    JSClientModule Module { get; set; } = default!;
    [Inject] IJSRuntime jsRuntime { get; set; }
    [Inject] ISubscriber<IClient> OnPeerConnected { get; set; }
    [Inject] FrameSimulationService SimulationService { get; set; }
    [Inject] HeadlessSurface Surface { get; set; }
    JSRenderService? RenderService { get; set; } = null;

    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            ClientHub.RemoveClient(Client);
        }
        try
        {

            await Module.DisposeAsync();
        }
        catch (JSDisconnectedException e)
        {
            Logger.LogWarning("disposing while disconnected");
        }
    }

    float Scale
    {
        get
        {
            return SimulationService.Scale;
        }
        set
        {
            SimulationService.Scale = value;
        }
    }

    IJSObjectReference? VideoElement { get; set; } = null;
    ElementReference SimpleRTCRef { get; set; }

    bool Connecting { get; set; } = true;

    IJSObjectReference? VideoRef = null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            Module = await JSClientModule.CreateAsync(jsRuntime);
            var connectionId = await Module.GetSignalRConnectionIdAsync();
            Client = new BrowserClient(jsRuntime, Module, HubContext, connectionId, OnPeerConnected);
            ClientHub.AddClient(Client);
            //RenderService = new(await (await Client.Module).CreateWebGPURenderServiceAsync());
            //RenderService = new(await Client.Module.CreateHeadlessServerRenderService());
            //RenderService = new(await (await Client.Module).CreateHeadlessSharedBufferServerRenderService());
            //RenderService = new(await Client.Module.CreateServerRenderPresentService());
            Logger.LogInformation("Blazor render first render called");
            StateHasChanged();
            //await Task.Delay(1000);
            Connecting = false;
            StateHasChanged();
        }
        if (VideoRef is not null)
        {
            await Module.AppendChildAsync(SimpleRTCRef, VideoRef);
        }
    }

    async Task StartRTC()
    {
        VideoRef = await Module.CreateSimpleRTCClient();
        StateHasChanged();
    }

    async Task OnMouseDown(MouseEventArgs e)
    {
        Console.WriteLine(JsonSerializer.Serialize(e));
    }
    async Task OnMouseUp(MouseEventArgs e)
    {
        Console.WriteLine(JsonSerializer.Serialize(e));
    }
    async Task OnMouseMove(MouseEventArgs e)
    {
        Console.WriteLine(JsonSerializer.Serialize(e));
    }
}
