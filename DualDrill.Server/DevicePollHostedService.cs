using DualDrill.Graphics;

namespace DualDrill.Server;

public sealed class DevicePollHostedService(GPUDevice Device) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            Device.Poll();
            await Task.Yield();
        }
    }
}
