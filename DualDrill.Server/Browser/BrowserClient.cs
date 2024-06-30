using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using Microsoft.JSInterop;

namespace DualDrill.Server.Browser;

class BrowserClient(IJSRuntime JSRuntime, JSClientModule ModuleValue)
   : IClient
{
    public Uri Uri { get; } = new Uri($"uuid:{Guid.NewGuid()}");
    public IJSRuntime JSRuntime { get; } = JSRuntime;
    public JSMediaStreamProxy? MediaStream { get; set; }

    public ValueTask<JSClientModule> Module
    {
        get
        {
            return ValueTask.FromResult(ModuleValue);
        }
    }

    public IDesktopBrowserUI? UserInterface { get; set; } = default;

    public async ValueTask<IRTCPeerConnection> CreatePeerConnection()
    {
        return await RTCPeerConnectionProxy.CreateAsync(this, await Module);
    }
    public async ValueTask<JSMediaStreamProxy> GetCameraStreamAsync()
    {
        return await new MediaDevices(this, JSRuntime).GetUserMedia(await Module, audio: false, video: true);
    }

    //public async Task<IPeerToPeerClientPair> CreatePairAsync(IClient target)
    //{
    //    var browserTarget = (BrowserClient)target;
    //    var pair = await BrowserClientPair.CreateAsync(this, browserTarget);
    //    browserTarget.PairedAsTargetSubject.OnNext(pair);
    //    return pair;
    //}

    public ValueTask SendDataStream<T>(Uri uri, IAsyncEnumerable<T> dataStream)
    {
        throw new NotImplementedException();
    }

    public ValueTask<IAsyncEnumerable<T>> SubscribeDataStream<T>(Uri uri)
    {
        throw new NotImplementedException();
    }
}
