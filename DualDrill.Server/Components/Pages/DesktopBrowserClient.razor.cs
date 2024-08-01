using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Graphics.Headless;
using DualDrill.Server.Browser;
using DualDrill.Server.CustomEvents;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DualDrill.Server.Components.Pages;

public partial class DesktopBrowserClient : IAsyncDisposable
{
    [Inject] IJSRuntime? JSRuntime { get; set; }
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
        if (SelfHandle.HasValue)
        {
            SelfHandle.Value.Free();
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

    string? InteropMessage = null;

    GCHandle? SelfHandle = default;

    ElementReference? Attached { get; set; } = null;

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
            await StartRTC();
            //await Task.Delay(1000);
            Connecting = false;
            StateHasChanged();
        }
        if (VideoRef is not null)
        {
            var changed = !Attached.Equals(SimpleRTCRef);
            if (changed)
            {
                await Module.AppendChildAsync(SimpleRTCRef, VideoRef);
                Logger.LogInformation($"call attach");
            }
            Attached = SimpleRTCRef;
        }
    }

    bool IsPointerDown { get; set; }

    async Task StartRTC()
    {
        VideoRef = await Module.CreateSimpleRTCClient();
        StateHasChanged();
    }

    void OnNormalizedPointerDown(NormalizedPointerEventArgs e)
    {
        IsPointerDown = true;
    }
    void OnNormalizedPointerUp(NormalizedPointerEventArgs e)
    {
        IsPointerDown = false;
    }
    void OnNormalizedPointerMove(NormalizedPointerEventArgs e)
    {
        if (IsPointerDown)
        {
            Logger.LogInformation("Draggin ({X}, {Y})", e.OffsetX / e.BoundingRect.Width, e.OffsetY / e.BoundingRect.Height);
        }
    }

    private async ValueTask SetInteractiveServerHandle()
    {
        if (JSRuntime is not null && !SelfHandle.HasValue)
        {
            SelfHandle = GCHandle.Alloc(this);
            var handle = GCHandle.ToIntPtr(SelfHandle.Value).ToString();
            await JSRuntime.InvokeVoidAsync("DualDrillSetInteractiveServerHandle", "HelloFromServer");
        }
        else
        {
            Logger.LogWarning("Skip SetInteractiveServerHandle, JSRuntime is null or SelfHandle already set");
        }
    }
}
