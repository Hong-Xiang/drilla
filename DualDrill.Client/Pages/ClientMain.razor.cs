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
    [Inject] DualDrillBrowserSignalRClientService SignalRService { get; set; }

    private int currentCount = 0;
    private string ServerHandle = "";

    ElementReference SimpleRTCRef { get; set; }
    JSObject? VideoRef = null;
    private class ASimpleObject
    {
        public bool Audio { get; set; }
        public bool Video { get; set; }
    }


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        Console.WriteLine("After base after render");
        if (firstRender)
        {
            var interactiveServer = await InteractiveServerHandle.GetInteractiveServerHandleAsync();
            //ServerHandle = interactiveServer.Handle;
            Console.WriteLine($"Server Handle {ServerHandle}");
            StateHasChanged();
            using var DotNet = JSHost.GlobalThis.GetPropertyAsJSObject("DotNet");
            using var RTCPeerConnectionCls = JSHost.GlobalThis.GetPropertyAsJSObject("RTCPeerConnection");
            await using var dotnetRuntime = await JSRuntime.InvokeAsync<IJSObjectReference>("getDotnetRuntime", 0);
            await JSRuntime.InvokeVoidAsync("console.log", dotnetRuntime);

            Console.WriteLine(NavigationManager.BaseUri);
            await SimpleJSInterop.StartSignalRAsync();
            //VideoRef = await SimpleJSInterop.CreateSimpleRTCClientAsync();
            StateHasChanged();
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

    private void IncrementCount()
    {
        Console.WriteLine("increment");
        currentCount++;
    }

    async Task StartRTC()
    {
    }

}