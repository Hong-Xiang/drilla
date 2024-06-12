
using DualDrill.Graphics;

namespace DualDrill.Server;

public sealed class DevicePollService(WGPUProviderService DeviceProviderService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            if (DeviceProviderService.Disposing is TaskCompletionSource s)
            {
                s.SetResult();
                break;
            }
            DeviceProviderService.Device.Poll();
            await Task.Delay(1, stoppingToken).ConfigureAwait(false);
        }
    }
}


public sealed class WGPUProviderService : IAsyncDisposable
{
    public GPUInstanceW Instance { get; }
    public GPUAdapter Adapter { get; }
    public GPUDevice Device { get; }
    public GPUQueue Queue { get; }

    public TaskCompletionSource? Disposing { get; private set; }

    public WGPUProviderService()
    {
        Instance = new GPUInstanceW();
        Adapter = Instance.RequestAdapter(null);
        Device = Adapter.RequestDevice();
        Queue = Device.GetQueue();
    }

    public async ValueTask DisposeAsync()
    {
        Disposing = new TaskCompletionSource();
        await Disposing.Task;
        Queue.Dispose();
        Device.Dispose();
        Adapter.Dispose();
        Instance.Dispose();
    }
}
