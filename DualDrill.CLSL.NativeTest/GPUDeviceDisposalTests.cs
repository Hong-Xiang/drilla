using DualDrill.Graphics;
using WebGpuBackend = DualDrill.Graphics.Backend.WebGPUNETBackend;

namespace DualDrill.CLSL.NativeTest;

public sealed class GPUDeviceDisposalTests
{
    [Fact]
    public async Task Queue_then_device_disposal_is_idempotent_through_record_aliases()
    {
        using var instance = WebGpuBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new()
            {
                PowerPreference = GPUPowerPreference.HighPerformance,
                ForceFallbackAdapter = false,
            },
            CancellationToken.None);
        using var device = Assert.IsType<GPUDevice<WebGpuBackend>>(
            await adapter.RequestDeviceAsync(new(), CancellationToken.None));
        var alias = device with { };

        device.Queue.Dispose();
        device.Queue.Dispose();
        alias.Queue.Dispose();
        device.Dispose();
        device.Dispose();
        alias.Dispose();
    }
}
