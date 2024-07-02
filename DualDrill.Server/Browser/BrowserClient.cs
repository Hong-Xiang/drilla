using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Graphics;
using DualDrill.Server.Application;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Runtime.InteropServices;

namespace DualDrill.Server.Browser;

class BrowserClient(IJSRuntime JSRuntime,
                    JSClientModule ModuleValue,
                    IHubContext<DrillHub, IDrillHubClient> SignalRHub,
                    string SignalRConnectionId)
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


    public async ValueTask HubInvokeAsync(Func<object, ValueTask> func)
    {
        using var handle = new DisposableGCHandle(GCHandle.Alloc(func));
        await SignalRHub.Clients.Client(SignalRConnectionId).HubInvoke(GCHandle.ToIntPtr(handle.Handle).ToString());
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
