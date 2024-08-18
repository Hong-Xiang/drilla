using DualDrill.Engine;
using DualDrill.Engine.Connection;
using Microsoft.Extensions.DependencyInjection;

namespace DualDrill.WebView;

public static class ServicesExtension
{
    public static void AddWebViewConnectionServices(this IServiceCollection services)
    {

        services.AddSingleton<WebViewService>();
        services.AddSingleton<IWebViewService>(sp => sp.GetRequiredService<WebViewService>());
        services.AddSingleton<IWebViewInteropService>(sp => sp.GetRequiredService<WebViewService>());
        services.AddSingleton<IPeerConnectionProviderService, WebViewRTCPeerConnectionProviderService>();
    }
}
