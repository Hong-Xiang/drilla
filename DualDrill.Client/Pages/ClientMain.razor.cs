using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DualDrill.Client.Pages;

[SupportedOSPlatform("browser")]
public partial class ClientMain
{
    [Inject] IJSRuntime JSRuntime { get; set; }
    [Inject] NavigationManager NavigationManager { get; set; }

    private string ServerHandle = "";

    ElementReference SimpleRTCRef { get; set; }
    JSObject? VideoRef = null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            var interactiveServer = await InteractiveServerHandle.GetInteractiveServerHandleAsync();

            await SimpleJSInterop.StartSignalRAsync();

            ServerHandle = await BrowserClientJSInterop.GetInteractiveServerHandle();
            StateHasChanged();
        }
        if (VideoRef is not null)
        {
            //await using var module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "/client.js");
            //await module.InvokeVoidAsync("appendChild", SimpleRTCRef, VideoRef);
            SimpleJSInterop.AppendToVideoTarget(VideoRef);
        }
    }
}