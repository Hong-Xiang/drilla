using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Graphics;
using DualDrill.Server.Application;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DualDrill.Server.Browser;

class BrowserClient(IJSRuntime JSRuntime,
                    JSClientModule ModuleValue,
                    IHubContext<DrillHub, IDrillHubClient> SignalRHub,
                    string SignalRConnectionId)
   : IClient
{
    public Guid Id { get; } = Guid.NewGuid();
    private Uri? uri;
    public Uri Uri
    {
        get
        {
            uri ??= new Uri($"uuid:{Id}");
            return uri;
        }
    }
    public IJSRuntime JSRuntime { get; } = JSRuntime;
    public JSMediaStreamProxy? MediaStream { get; set; }

    public ConcurrentDictionary<Uri, Channel<object>> EventChannels = [];

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

    private string? ConnectionId { get; set; }
    public async ValueTask<string> GetConnectionId()
    {
        if (ConnectionId is null)
        {
            await HubInvokeAsync(async (hub) =>
            {
                if (hub is DrillHub dhub)
                {
                    ConnectionId = dhub.Context.ConnectionId;
                }
            });
        }
        return ConnectionId;
    }

    public async ValueTask HubInvokeAsync(Func<object, ValueTask> func)
    {
        using var handle = new DisposableGCHandle(GCHandle.Alloc(func));
        await SignalRHub.Clients.Client(SignalRConnectionId)
                                .HubInvoke(GCHandle.ToIntPtr(handle.Handle).ToString())
                                .ConfigureAwait(false);
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

    public Channel<object> GetOrAddEventChannel(Uri uri)
    {
        return EventChannels.GetOrAdd(Uri, (uri) =>
                {
                    return Channel.CreateUnbounded<object>();
                });
    }
}
