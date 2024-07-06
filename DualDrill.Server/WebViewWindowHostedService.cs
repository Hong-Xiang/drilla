using DualDrill.Graphics.Headless;
using DualDrill.Server.WebView;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DualDrill.Server;

public sealed class WebViewWindowHostedService(
    IServer WebServer,
    IHostApplicationLifetime HostApplicationLifetime,
    WebViewService WebViewService,
    HeadlessSurface Surface,
    ILogger<WebViewWindowHostedService> Logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uri = await GetHostedSourceUriAsync(stoppingToken).ConfigureAwait(false);
        Logger.LogInformation("Starting WebView2 window to url {SourceUrl}", uri);
        await WebViewService.StartAsync(uri).ConfigureAwait(false);

        await WebViewService.CreateSharedBufferAsync(stoppingToken).ConfigureAwait(false);

        var datas = Surface.GetAllPresentedDataAsync(stoppingToken).GetAsyncEnumerator(stoppingToken);
        var slots = WebViewService.GetAllWriteableSlotsAsync(stoppingToken).GetAsyncEnumerator(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await datas.MoveNextAsync().ConfigureAwait(false)
                && await slots.MoveNextAsync().ConfigureAwait(false))
            {
                var slot = slots.Current;
                datas.Current.Span.CopyTo(slot.Span);
                WebViewService.SetReadyToRead(slot);
            }
        }
        await WebViewService.GetApplicationResultAsync().ConfigureAwait(false);
    }

    async ValueTask<Uri> GetHostedSourceUriAsync(CancellationToken cancellation)
    {
        var tcs = new TaskCompletionSource(cancellation);
        HostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            tcs.SetResult();
        });
        await tcs.Task.ConfigureAwait(false);
        var address = WebServer.Features.Get<IServerAddressesFeature>();
        var uri = (address?.Addresses.FirstOrDefault(x => new Uri(x).Scheme == "https")) ?? throw new ArgumentNullException("WebView2 Source Uri");
        return new Uri(uri + "/webview2");
    }
}
