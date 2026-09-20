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
        using var allowQueueExit = new ManualResetEventSlim();
        using var aliasStarted = new ManualResetEventSlim();
        using var aliasCompleted = new ManualResetEventSlim();
        var queue = new BlockingQueue(queueEntered, allowQueueExit);
        var device = nativeDevice with { Queue = queue };
        var alias = device with { };
        var aliasDisposal = Task.Run(() =>
        {
            Assert.True(queueEntered.Wait(SynchronizationTimeout));
            aliasStarted.Set();
            try
            {
                alias.Dispose();
            }
            finally
            {
                aliasCompleted.Set();
            }
        });
        var coordinator = Task.Run(() =>
        {
            Assert.True(aliasStarted.Wait(SynchronizationTimeout));
            try
            {
                Assert.False(aliasCompleted.Wait(TimeSpan.FromMilliseconds(100)));
            }
            finally
            {
                allowQueueExit.Set();
            }
        });

        device.Dispose();
        await coordinator.WaitAsync(SynchronizationTimeout);
        await aliasDisposal.WaitAsync(SynchronizationTimeout);

        Assert.Equal(1, queue.DisposeCalls);
        Assert.True(aliasCompleted.IsSet);
    }

    private sealed class BlockingQueue(
        ManualResetEventSlim entered,
        ManualResetEventSlim allowExit) : IGPUQueue
    {
        private int disposeCalls;

        public int DisposeCalls => Volatile.Read(ref disposeCalls);

        public void Dispose()
        {
            if (Interlocked.Increment(ref disposeCalls) == 1)
            {
                entered.Set();
                Assert.True(allowExit.Wait(SynchronizationTimeout));
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
