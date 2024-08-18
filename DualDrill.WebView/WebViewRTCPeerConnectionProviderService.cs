using DualDrill.Engine;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Event;
using DualDrill.WebView.Event;
using MessagePipe;

namespace DualDrill.WebView;


public sealed record class PeerConnectionCreatedEvent(Guid ClientId) { }

internal sealed class WebViewRTCPeerConnectionProviderService(
    IWebViewService WebViewService,
    ISubscriber<Guid, PeerConnectionCreatedEvent> RTCPeerConnectionCreatedSub)
    : IPeerConnectionProviderService
{
    public async ValueTask<IPeerConnection> CreatePeerConnectionAsync(Guid clientId, CancellationToken cancellation)
    {
        var created = RTCPeerConnectionCreatedSub.FirstAsync(clientId, cancellation).AsTask();
        await WebViewService.SendMessageAsync(
            new ConnectionEvent<RequestPeerConnectionEvent>(clientId, ClientsManager.ServerId, new()),
            cancellation);
        //await created;
        return new WebViewPeerConnectionProxy(clientId);
    }
}
