using DualDrill.Engine.Connection;
using DualDrill.Engine.Disposable;
using DualDrill.Engine.UI;
using DualDrill.Engine.WebRTC;
using Microsoft.JSInterop;
using System.Runtime.InteropServices;

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

sealed class QuickOnBuffer(IJSRuntime JSRuntime)
{
    [JSInvokable]
    public async Task OnBuffer(IJSObjectReference jsObjectReference)
    {
        await JSRuntime.InvokeVoidAsync("console.log", jsObjectReference);
        await jsObjectReference.DisposeAsync();
    }
}

public sealed class JSClientModule(IJSRuntime jsRuntime, IJSObjectReference Module) : IAsyncDisposable
{
    public IJSRuntime JSRuntime { get; } = jsRuntime;
    public static async ValueTask<JSClientModule> CreateAsync(IJSRuntime runtime)
    {
        var module = await runtime.InvokeAsync<IJSObjectReference>("import", $"/client.js?t={Guid.NewGuid()}").ConfigureAwait(false);
        await module.InvokeVoidAsync("Initialization", DotNetObjectReference.Create(new QuickOnBuffer(runtime))).ConfigureAwait(false);
        return new JSClientModule(runtime, module);
    }

    public async ValueTask<ElementSize> GetElementSize(IJSObjectReference element)
    {
        return await JSRuntime.InvokeAsync<ElementSize>("getElementSize", element);
    }

    public async ValueTask<JSMediaStreamProxy> CaptureCanvasToStream(IClient client, IJSObjectReference canvasElement)
    {
        var mediaStream = await Module.InvokeAsync<IJSObjectReference>("captureStream", canvasElement);
        var id = await GetProperty<string>(mediaStream, "id").ConfigureAwait(false);
        return new(client, this, mediaStream, id);

    }

    public async ValueTask<IJSObjectReference> CreateRtcPeerConnectionAsync()
    {
        return await Module.InvokeAsync<IJSObjectReference>("createRTCPeerConnection").ConfigureAwait(false);
    }

    //public async ValueTask<IJSObjectReference> CreateCanvasElementAsync(DotNetObjectReference<ICanvasElementObserver> observer)
    //{
    //    var module = await Module.ConfigureAwait(false);
    //    var result = await module.InvokeAsync<IJSObjectReference>("createCanvasElement", observer);
    //}

    public async ValueTask<IJSObjectReference> CreateJSObjectReferenceAsync(object value)
    {
        return await Module.InvokeAsync<IJSObjectReference>("asObjectReference", value).ConfigureAwait(false);
    }


    public ValueTask<IJSObjectReference> CreateVideoElementAsync()
    {
        throw new NotImplementedException();
    }
    public async ValueTask<IJSObjectReference> CreateWebGPURenderServiceAsync()
    {
        return await Module.InvokeAsync<IJSObjectReference>("createWebGPURenderService");
    }

    public async ValueTask<IJSObjectReference> CreateHeadlessServerRenderService()
    {
        return await Module.InvokeAsync<IJSObjectReference>("createHeadlessServerRenderService");
    }
    public async ValueTask<IJSObjectReference> CreateHeadlessSharedBufferServerRenderService()
    {
        return await Module.InvokeAsync<IJSObjectReference>("createHeadlessSharedBufferServerRenderService");
    }


    public async ValueTask<IJSObjectReference> CreateServerRenderPresentService()
    {
        return await Module.InvokeAsync<IJSObjectReference>("createServerRenderPresentService");
    }
    public async ValueTask<IJSObjectReference> CreateWebGLRenderServiceAsync(IJSObjectReference canvasElement)
    {
        return await Module.InvokeAsync<IJSObjectReference>("createWebGPURenderService", canvasElement);
    }
    public async ValueTask<T> GetProperty<T>(IJSObjectReference target, params PropertyKey[] path)
    {
        return await Module.InvokeAsync<T>("getProperty", target, (object[])[.. path.Select(static x => x.Value)]);
    }

    public async ValueTask SetProperty<T>(IJSObjectReference target, T? value, params PropertyKey[] path)
    {
        await Module.InvokeVoidAsync("setProperty", target, value, (object[])[.. path.Select(static x => x.Value)]);
    }

    public async ValueTask DisposeAsync()
    {
        await Module.DisposeAsync().ConfigureAwait(false);
    }
}

public static class JSClientModuleExtension
{
    public static async ValueTask SetVideoElementStreamAsync(this JSClientModule client, IJSObjectReference videoElement, IJSObjectReference? mediaStream)
    {
        await client.SetProperty(videoElement, mediaStream, "srcObject");
    }

    public static async ValueTask RemoveVideoElementStreamAsync(this JSClientModule client, IJSObjectReference videoElement)
    {
        await client.SetProperty<IJSObjectReference>(videoElement, null, "srcObject");
    }
}

