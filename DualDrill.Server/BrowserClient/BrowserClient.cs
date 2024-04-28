using DualDrill.Common.ResourceManagement;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;
using System.Reactive.Subjects;

namespace DualDrill.Server.BrowserClient;

class BrowserClient(
   IServiceProvider Services,
   IJSRuntime JSRuntime,
   JSClientModule Module)
   : IClient
{
    public Uri Uri { get; } = new Uri($"uuid:{Guid.NewGuid()}");
    public IJSRuntime JSRuntime { get; } = JSRuntime;
    public JSClientModule Module { get; } = Module;
    public IServiceProvider Services { get; } = Services;

    public IDesktopBrowserUI? UserInterface { get; set; } = default;

    public async ValueTask<IRTCPeerConnection> CreatePeerConnection()
    {
        return await RTCPeerConnectionProxy.CreateAsync(this, Module);
    }
    public async ValueTask<JSMediaStreamProxy> GetCameraStreamAsync()
    {
        return await Services.GetRequiredService<MediaDevices>().GetUserMedia(audio: false, video: true);
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
