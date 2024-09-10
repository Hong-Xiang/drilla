using DualDrill.Graphics;

namespace DualDrill.Engine.Services;

public sealed class DevicePollHostedService(IGPUDevice Device) : IHostableBackgroundService
{
    // TODO: find proper way to pool device event (used for async operation like map buffer async)
    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            Device.Poll();
            //await Task.Yield();
            await Task.Delay(1, stoppingToken);
        }
    }
}
