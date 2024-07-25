
using DualDrill.Graphics.Headless;

namespace DualDrill.Server.Services;

public sealed class VideoPushHostedService(
    HeadlessSurface Surface,
    RTCDemoVideoSource VideoSource,
    ILogger<VideoPushHostedService> Logger
) : BackgroundService
{
    private int count = 0;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var datas = Surface.GetAllPresentedDataAsync(stoppingToken);
        await foreach (var data in datas)
        {
            if (count % 3 == 0)
            {
                VideoSource.EncodeVideo(
                    Surface.Width,
                    Surface.Height,
                    data.Span);
            }
            count++;

        }
    }
}
