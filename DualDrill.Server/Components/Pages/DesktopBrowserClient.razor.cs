using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Headless;
using DualDrill.Server.Browser;
using MessagePipe;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Runtime.InteropServices;

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

    ElementReference SimpleRTCRef { get; set; }

    bool Connecting { get; set; } = true;

    IJSObjectReference? VideoRef = null;

    GCHandle? SelfHandle = default;

    ElementReference? AttachedElement { get; set; } = null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {

            Module = await JSClientModule.CreateAsync(jsRuntime);
            var connectionId = await Module.GetSignalRConnectionIdAsync();
            Client = new BrowserClient(jsRuntime, Module, HubContext, connectionId);
            await SetInteractiveServerHandle();
            VideoRef = await Module.CreateSimpleRTCClient();

            ClientHub.AddClient(Client);
            Connecting = false;
            StateHasChanged();
        }
        if (VideoRef is not null)
        {
            if (!AttachedElement.Equals(SimpleRTCRef))
            {
                await Module.AppendChildAsync(SimpleRTCRef, VideoRef);
            }
            AttachedElement = SimpleRTCRef;
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
