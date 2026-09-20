using DualDrill.Graphics;
using WebGpuBackend = DualDrill.Graphics.Backend.WebGPUNETBackend;

namespace DualDrill.CLSL.NativeTest;

public sealed class GPUDeviceDisposalTests
{
    private static readonly TimeSpan SynchronizationTimeout = TimeSpan.FromSeconds(10);

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

    [Fact]
    public async Task Concurrent_device_alias_does_not_overtake_queue_cleanup()
    {
        using var instance = WebGpuBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new()
            {
                PowerPreference = GPUPowerPreference.HighPerformance,
                ForceFallbackAdapter = false,
            },
            CancellationToken.None);
        using var nativeDevice = Assert.IsType<GPUDevice<WebGpuBackend>>(
            await adapter.RequestDeviceAsync(new(), CancellationToken.None));
        using var queueEntered = new ManualResetEventSlim();
        using var contenderCompleted = new ManualResetEventSlim();
        var queue = new BlockingQueue(queueEntered, contenderCompleted);
        var device = nativeDevice with { Queue = queue };
        var alias = device with { };
        var contender = Task.Run(() =>
        {
            Assert.True(queueEntered.Wait(SynchronizationTimeout));
            try
            {
                alias.Dispose();
            }
            finally
            {
                contenderCompleted.Set();
            }
        });

        device.Dispose();
        await contender.WaitAsync(SynchronizationTimeout);

        Assert.Equal(1, queue.DisposeCalls);
    }

    private sealed class BlockingQueue(
        ManualResetEventSlim entered,
        ManualResetEventSlim contenderCompleted) : IGPUQueue
    {
        private int disposeCalls;

        public int DisposeCalls => Volatile.Read(ref disposeCalls);

        public void Dispose()
        {
            if (Interlocked.Increment(ref disposeCalls) == 1)
            {
                entered.Set();
                Assert.True(contenderCompleted.Wait(SynchronizationTimeout));
            }
        }

        public ValueTask OnSubmittedWorkDoneAsync(CancellationToken cancellation) =>
            throw new NotSupportedException();

        public void Submit(IReadOnlyList<IGPUCommandBuffer> commandBuffers) =>
            throw new NotSupportedException();

        public void WriteBuffer(IGPUBuffer buffer, ulong bufferOffset, ReadOnlySpan<byte> data) =>
            throw new NotSupportedException();

        public void WriteTexture(
            GPUImageCopyTexture destination,
            ReadOnlySpan<byte> data,
            GPUImageDataLayout dataLayout,
            GPUExtent3D size) => throw new NotSupportedException();
    }
}
