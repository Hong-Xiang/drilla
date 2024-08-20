using DualDrill.Engine;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Event;
using DualDrill.WebView.Event;

namespace DualDrill.WebView;

internal sealed class WebViewRTCPeerConnectionProviderService(IWebViewService WebViewService)
    : IPeerConnectionProviderService
{
    public async ValueTask<IPeerConnection> CreatePeerConnectionAsync(Guid clientId, CancellationToken cancellation)
    {
        await WebViewService.SendMessageAsync(
            new ConnectionEvent<RequestPeerConnectionEvent>(clientId, ClientsManager.ServerId, new()),
            cancellation);
        return new WebViewPeerConnectionProxy(clientId);
    }
}
