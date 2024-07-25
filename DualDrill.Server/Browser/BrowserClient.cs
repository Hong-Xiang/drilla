using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using DualDrill.Graphics;
using DualDrill.Server.Application;
using MessagePipe;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DualDrill.Server.Browser;

class BrowserClient
   : IClient
{
    public BrowserClient(IJSRuntime jsRuntime,
                         JSClientModule moduleValue,
                         IHubContext<DrillHub, IDrillHubClient> signalRHub,
                         string signalRConnectionId,
                         ISubscriber<IClient> onPeerConnected)
    {
        JSRuntime = jsRuntime;
        ModuleValue = moduleValue;
        SignalRHub = signalRHub;
        SignalRConnectionId = signalRConnectionId;
        OnPeerConnected = onPeerConnected;
    }

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
    public IJSRuntime JSRuntime { get; }
    public JSClientModule ModuleValue { get; }
    public IHubContext<DrillHub, IDrillHubClient> SignalRHub { get; }
    public string SignalRConnectionId { get; }
    public JSMediaStreamProxy? MediaStream { get; set; }

    public ConcurrentDictionary<Uri, Channel<object>> EventChannels = [];
    public ISubscriber<IClient> OnPeerConnected { get; }
    private IDisposablePublisher<IClient> connected { get; }


    public void Connected(IClient client)
    {
        connected.Publish(client);
    }

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
