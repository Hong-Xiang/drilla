using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Implementation;

namespace DualDrill.Server.BrowserClient;

sealed class ClientModule(IJSRuntime JS, IJSObjectReference Module) : IAsyncDisposable
{
    public IJSRuntime JSRuntime { get; } = JS;
    public static async Task<ClientModule> ImportAsync(IJSRuntime js)
    {
        return new ClientModule(js, await js.InvokeAsync<IJSObjectReference>("import", $"/client.js?t={Guid.NewGuid()}").ConfigureAwait(false));
    }
    public async ValueTask DisposeAsync()
    {
        await Module.DisposeAsync().ConfigureAwait(false);
    }
    public async ValueTask ShowCameraStream(ElementReference videoElement, IJSObjectReference mediaStream)
    {
        await Module.InvokeVoidAsync("showCameraStream", videoElement, mediaStream).ConfigureAwait(false);
    }
    public async Task<JSUnmanagedResourceReference> CreateRtcPeerConnection()
    {
        return new(await Module.InvokeAsync<IJSObjectReference>("createRTCPeerConnection").ConfigureAwait(false));
    }
}
