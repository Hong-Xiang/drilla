using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Implementation;

namespace DualDrill.Server.BrowserClient;

public sealed class JSDrillClientModule(IJSRuntime JS, IJSObjectReference Module) : IAsyncDisposable
{
    public IJSRuntime JSRuntime { get; } = JS;
    public static async ValueTask<JSDrillClientModule> ImportModuleAsync(IJSRuntime js)
    {
        return new JSDrillClientModule(js, await js.InvokeAsync<IJSObjectReference>("import", $"/client.js?t={Guid.NewGuid()}").ConfigureAwait(false));
    }
    public async ValueTask DisposeAsync()
    {
        await Module.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask JsConsolelog(string s)
    {
        await JSRuntime.InvokeVoidAsync("console.log", s);
    }
    public async ValueTask SetVideoElementStream(ElementReference videoElement, IJSObjectReference mediaStream)
    {
        await SetProperty(videoElement, "srcObject", mediaStream).ConfigureAwait(false);
    }
    public async Task<JSDisposableReference> CreateRtcPeerConnection()
    {
        return new(await Module.InvokeAsync<IJSObjectReference>("createRTCPeerConnection").ConfigureAwait(false));
    }

    public async ValueTask<JSRenderService> CreateRenderContext(ElementReference canvas)
    {
        return new(await Module.InvokeAsync<IJSObjectReference>("createRenderContext", canvas));
    }

    public async Task<JSDisposableReference> AddDataChannelLogMessageListener(IJSObjectReference dataChannel)
    {
        return new(await Module.InvokeAsync<IJSObjectReference>("addDataChannelLogMessageListener", dataChannel).ConfigureAwait(false));
    }

    public async Task<T> GetProperty<T>(IJSObjectReference target, string propertyName)
    {
        return await Module.InvokeAsync<T>("getProperty", target, propertyName).ConfigureAwait(false);
    }
    public async Task<T> GetProperty<T>(IJSObjectReference target, int propertyName)
    {
        return await Module.InvokeAsync<T>("getProperty", target, propertyName).ConfigureAwait(false);
    }
    public async Task<T> GetProperty<T>(ElementReference target, string propertyName)
    {
        return await Module.InvokeAsync<T>("getProperty", target, propertyName).ConfigureAwait(false);
    }
    public async Task<T> GetProperty<T>(IJSObjectReference target, string key1, int key2)
    {
        return await Module.InvokeAsync<T>("getProperty2", target, key1, key2).ConfigureAwait(false);
    }
    public async Task<T> GetProperty<T>(IJSObjectReference target, string key1, string key2)
    {
        return await Module.InvokeAsync<T>("getProperty2", target, key1, key2).ConfigureAwait(false);
    }
    public async Task SetProperty<T>(ElementReference target, string propertyName, T value)
    {
        await Module.InvokeVoidAsync("setProperty", target, propertyName, value).ConfigureAwait(false);
    }
    public async Task SetProperty<T>(IJSObjectReference target, string propertyName, T value)
    {
        await Module.InvokeVoidAsync("setProperty", target, propertyName, value).ConfigureAwait(false);
    }
}
