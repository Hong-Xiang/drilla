using DualDrill.Engine.Connection;
using DualDrill.Engine.UI;
using DualDrill.Engine.WebRTC;
using Microsoft.JSInterop;

namespace DualDrill.Engine.BrowserProxy;


public struct PropertyKey
{
    public object Value { get; private set; }

    public static implicit operator PropertyKey(int value)
    {
        return new PropertyKey
        {
            Value = value
        };
    }

    public static implicit operator PropertyKey(string value)
    {
        return new PropertyKey
        {
            Value = value
        };
    }
}

public sealed class JSClientModule(IJSRuntime jsRuntime) : IAsyncDisposable
{
    public IJSRuntime JSRuntime { get; } = jsRuntime;
    Task<IJSObjectReference> Module { get; } = jsRuntime.InvokeAsync<IJSObjectReference>("import", $"/client.js?t={Guid.NewGuid()}").AsTask();

    public async ValueTask<IJSObjectReference> CreateRtcPeerConnectionAsync()
    {
        var module = await Module.ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>("createRTCPeerConnection").ConfigureAwait(false);
    }
    public ValueTask<IJSObjectReference> CreateCanvasElementAsync()
    {
        throw new NotImplementedException();
    }
    public async ValueTask<IJSObjectReference> CreateObjectReferenceAsync(object value)
    {
        var module = await Module.ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>("asObjectReference", value).ConfigureAwait(false);
    }


    public ValueTask<IJSObjectReference> CreateVideoElementAsync()
    {
        throw new NotImplementedException();
    }
    public async ValueTask<IJSObjectReference> CreateWebGPURenderServiceAsync()
    {
        var module = await Module.ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>("createWebGPURenderService");
    }
    public async ValueTask<IJSObjectReference> CreateWebGLRenderServiceAsync(IJSObjectReference canvasElement)
    {
        var module = await Module.ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>("createWebGPURenderService", canvasElement);
    }
    public async ValueTask<T> GetProperty<T>(IJSObjectReference target, params PropertyKey[] path)
    {
        var module = await Module.ConfigureAwait(false);
        return await module.InvokeAsync<T>("getProperty", target, (object[])[.. path.Select(static x => x.Value)]);
    }

    public async ValueTask SetProperty<T>(IJSObjectReference target, T value, params PropertyKey[] path)
    {
        var module = await Module.ConfigureAwait(false);
        await module.InvokeVoidAsync("setProperty", target, (object[])[.. path.Select(static x => x.Value)], value);
    }

    public async ValueTask DisposeAsync()
    {
        var module = await Module.ConfigureAwait(false);
        await module.DisposeAsync().ConfigureAwait(false);
    }
}

public static class JSClientModuleExtension
{
    public static async ValueTask SetVideoElementStreamAsync(this JSClientModule client, IJSObjectReference videoElement, IJSObjectReference mediaStream)
    {
        await client.SetProperty(videoElement, mediaStream, "srcObject");
    }
}

