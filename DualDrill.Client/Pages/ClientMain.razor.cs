using DualDrill.Engine.Connection;
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
    [Inject] ClientIdentity ClientIdentity { get; set; }
    [Inject] HttpClient HttpClient { get; set; }


    ElementReference SimpleRTCRef { get; set; }
    ElementReference? AttachedElement { get; set; }
    JSObject? VideoRef { get; set; }

    Guid ViewerId { get; } = Guid.NewGuid();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await ClientJSInterop.StartSignalRHubConnection(ClientIdentity.Id.ToString());
        await HttpClient.PostAsync(NavigationManager.BaseUri + $"api/peer-connection/server/{ClientIdentity.Id}", null);
        VideoRef = await ClientJSInterop.CreateSimpleRTCClientAsync(ClientIdentity.Id.ToString());
        StateHasChanged();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        Console.WriteLine("After render");
        if (VideoRef is not null && !AttachedElement.Equals(SimpleRTCRef))
        {
            Console.WriteLine("entered logic");
            using var divParent = ClientJSInterop.GetElementById(ViewerId.ToString());
            ClientJSInterop.AppendChild(divParent, VideoRef);
            AttachedElement = SimpleRTCRef;
        }
    }
}