using DualDrill.Graphics.Headless;
using DualDrill.Server.WevView2;
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
        WebViewService.Start(uri);
        await WebViewService.WebViewInitialized.ConfigureAwait(false);

        await WebViewService.CreateSharedBufferAsync(stoppingToken);

        //await foreach (var slot in WebViewService.GetAllWriteableSlotsAsync(stoppingToken))
        //{
        //    Logger.LogInformation($"Writeable shared buffer slot {slot}");
        //}

        //await foreach (var data in Surface.ReadAllPresentedDataAsync(stoppingToken))
        //{
        //    var min = byte.MaxValue;
        //    var max = byte.MinValue;
        //    for (var i = 0; i < data.Length; i++)
        //    {
        //        min = Math.Min(min, data.Span[i]);
        //        max = Math.Max(max, data.Span[i]);
        //    }
        //    Logger.LogInformation($"Rendered data size {data.Length}, min: {min}, max: {max}");
        //}

        var datas = Surface.ReadAllPresentedDataAsync(stoppingToken).GetAsyncEnumerator(stoppingToken);
        var slots = WebViewService.GetAllWriteableSlotsAsync(stoppingToken).GetAsyncEnumerator(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await datas.MoveNextAsync().ConfigureAwait(false)
                && await slots.MoveNextAsync().ConfigureAwait(false))
            {
                var slot = slots.Current;
                datas.Current.Span.CopyTo(slot.Span);
                //Logger.LogInformation($"ColorBuffer {string.Join(", ", slot.Span[..8].ToArray())}");
                //slot.Span.Fill(128);
                //void TestDumpValues(Span<byte> span)
                //{
                //    for (var i = 3; i < span.Length; i += 4)
                //    {
                //        span[i] = byte.MaxValue;
                //    }
                //}
                //TestDumpValues(slot.Span);
                WebViewService.SetReadyToRead(slot);
            }
        }
        await WebViewService.GetApplicationResultAsync().ConfigureAwait(false);
    }

    async ValueTask<Uri> GetHostedSourceUriAsync(CancellationToken cancellation)
    {
        var tcs = new TaskCompletionSource();
        HostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            tcs.SetResult();
        });
        await tcs.Task;
        var address = WebServer.Features.Get<IServerAddressesFeature>();
        var uri = (address?.Addresses.FirstOrDefault(x => new Uri(x).Scheme == "https")) ?? throw new ArgumentNullException("WebView2 Source Uri");
        return new Uri(uri);
    }
}
