
using DualDrill.Graphics.Headless;

namespace DualDrill.Server.Services;

public sealed class VideoPushHostedService(
    HeadlessSurface Surface,
    RTCDemoVideoSource VideoSource,
    ILogger<VideoPushHostedService> Logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var datas = Surface.GetAllPresentedDataAsync(stoppingToken);
        await foreach (var data in datas)
        {
            VideoSource.EncodeVideo(
                Surface.Width,
                Surface.Height,
                data.ToArray());
        }
    }
}
