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

public sealed record class ElementSize(int OffsetWidth, int OffsetHeight)
{
}

public sealed class JSClientModule(IJSRuntime jsRuntime) : IAsyncDisposable
{
    public IJSRuntime JSRuntime { get; } = jsRuntime;
    Task<IJSObjectReference> Module { get; } = jsRuntime.InvokeAsync<IJSObjectReference>("import", $"/client.js?t={Guid.NewGuid()}").AsTask();

    public async ValueTask Initialization()
    {
        var module = await Module.ConfigureAwait(false);
        await module.InvokeVoidAsync("Initialization").ConfigureAwait(false);
    }

    public async ValueTask<ElementSize> GetElementSize(IJSObjectReference element)
    {
        return await JSRuntime.InvokeAsync<ElementSize>("getElementSize", element);
    }

    public async ValueTask<JSMediaStreamProxy> CaptureStream(IClient client, IJSObjectReference canvasElement)
    {
        var module = await Module;
        var mediaStream = await module.InvokeAsync<IJSObjectReference>("captureStream", canvasElement);
        var id = await GetProperty<string>(mediaStream, "id").ConfigureAwait(false);
        return new(client, this, mediaStream, id);

    }

    public async ValueTask<IJSObjectReference> CreateRtcPeerConnectionAsync()
    {
        var module = await Module.ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>("createRTCPeerConnection").ConfigureAwait(false);
    }
    //public async ValueTask<IJSObjectReference> CreateCanvasElementAsync(DotNetObjectReference<ICanvasElementObserver> observer)
    //{
    //    var module = await Module.ConfigureAwait(false);
    //    var result = await module.InvokeAsync<IJSObjectReference>("createCanvasElement", observer);

    //}
    public async ValueTask<IJSObjectReference> CreateJSObjectReferenceAsync(object value)
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
    public async ValueTask<IJSObjectReference> CreateServerRenderPresentService()
    {
        var module = await Module.ConfigureAwait(false);
        return await module.InvokeAsync<IJSObjectReference>("createServerRenderPresentService");
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

    public async ValueTask SetProperty<T>(IJSObjectReference target, T? value, params PropertyKey[] path)
    {
        var module = await Module.ConfigureAwait(false);
        await module.InvokeVoidAsync("setProperty", target, value, (object[])[.. path.Select(static x => x.Value)]);
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

    public static async ValueTask RemoveVideoElementStreamAsync(this JSClientModule client, IJSObjectReference videoElement)
    {
        await client.SetProperty<IJSObjectReference>(videoElement, null, "srcObject");
    }
}

