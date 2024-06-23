namespace DualDrill.Server;

public sealed class DevicePollHostedService(WGPUProviderService DeviceProviderService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            DeviceProviderService.Device.Poll();
            //await Task.Delay(1, stoppingToken).ConfigureAwait(false);
            await Task.Yield();
        }
    }
}
