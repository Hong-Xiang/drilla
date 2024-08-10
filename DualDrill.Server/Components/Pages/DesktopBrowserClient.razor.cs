using DualDrill.Engine;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Server.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using SIPSorcery.Net;
using System.Runtime.InteropServices;

namespace DualDrill.Server.Components.Pages;

public partial class DesktopBrowserClient : IAsyncDisposable
{
    [Inject] IJSRuntime JSRuntime { get; set; }
    [Inject] ClientStore ClientHub { get; set; } = default!;
    [Inject] ILogger<DesktopBrowserClient> Logger { get; set; } = default!;
    [Inject] IHubContext<DrillHub, IDrillHubClient> HubContext { get; set; } = default!;
    BrowserClient? Client { get; set; }
    JSClientModule Module { get; set; } = default!;
    [Inject] FrameSimulationService SimulationService { get; set; }

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

    IJSObjectReference? OfferEmitter { get; set; }
    IJSObjectReference? AnswerEmitter { get; set; }
    IJSObjectReference? CandidateEmitter { get; set; }

    async Task Emit()
    {
        await HubContext.Clients.Client(ClientHub.GetConnectionId(ClientId.Value)).Emit(RenderTargetId.ToString());
    }

    RTCPeerConnection? PeerConnection { get; set; }
    Guid? ClientId { get; set; }

    Guid RenderTargetId { get; set; } = Guid.NewGuid();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            Module = await JSClientModule.CreateAsync(JSRuntime);
            //var connectionId = await Module.GetSignalRConnectionIdAsync();
            Client = new BrowserClient(JSRuntime, HubContext);
            ClientHub.AddClient(Client);
            ClientId = Client.Id;
            StateHasChanged();
            await SetClientId(Client.Id);

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

    private async ValueTask SetClientId(Guid clientId)
    {
        await JSRuntime.InvokeVoidAsync("DualDrillSetClientId", clientId);
        StateHasChanged();
    }
}
