using DualDrill.Graphics;
using WebGpuBackend = DualDrill.Graphics.Backend.WebGPUNETBackend;

namespace DualDrill.CLSL.NativeTest;

public sealed class GPUHandleOwnershipTests
{
    [Fact]
    public void Pointer_and_data_are_constructor_only_and_deconstructable()
    {
        var data = new object();
        var handle = new GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>(42, data);
        var (pointer, deconstructedData) = handle;

        Assert.False(typeof(GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>)
            .GetProperty(nameof(handle.Pointer))!.CanWrite);
        Assert.False(typeof(GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>)
            .GetProperty(nameof(handle.Data))!.CanWrite);
        Assert.Equal(42, pointer);
        Assert.Same(data, deconstructedData);
    }

    [Fact]
    public void Record_aliases_release_one_ownership_token_once()
    {
        var resource = new GPUBuffer<WebGpuBackend>(new(1)) { Length = 1 };
        var alias = resource with { };
        var releases = 0;

        Parallel.For(0, 64, index =>
        {
            var handle = index % 2 == 0 ? resource.Handle : alias.Handle;
            handle.Release(_ => Interlocked.Increment(ref releases));
        });

        Assert.Equal(1, releases);
    }

    [Fact]
    public void Reentrant_release_returns_without_running_cleanup_twice()
    {
        var handle = new GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>(1);
        var releases = 0;

        handle.Release(alias =>
        {
            releases++;
            alias.Release(_ => releases++);
        });

        Assert.Equal(1, releases);
    }

    [Fact]
    public async Task Device_release_waits_for_independent_queue_release_completion()
    {
        var queue = new GPUHandle<WebGpuBackend, GPUQueue<WebGpuBackend>>(1);
        var device = new GPUHandle<WebGpuBackend, GPUDevice<WebGpuBackend>>(2);
        using var queueReleaseStarted = new ManualResetEventSlim();
        using var allowQueueRelease = new ManualResetEventSlim();
        using var deviceQueueReleaseStarted = new ManualResetEventSlim();
        using var deviceNativeRelease = new ManualResetEventSlim();

        var queueOwner = Task.Run(() => queue.Release(_ =>
        {
            queueReleaseStarted.Set();
            Assert.True(allowQueueRelease.Wait(TimeSpan.FromSeconds(10)));
        }));
        Assert.True(queueReleaseStarted.Wait(TimeSpan.FromSeconds(10)));

        var deviceOwner = Task.Run(() => device.Release(_ =>
        {
            try
            {
                deviceQueueReleaseStarted.Set();
                queue.Release(_ => throw new InvalidOperationException());
            }
            finally
            {
                deviceNativeRelease.Set();
            }
        }));
        Assert.True(deviceQueueReleaseStarted.Wait(TimeSpan.FromSeconds(10)));
        Assert.False(deviceNativeRelease.Wait(TimeSpan.FromMilliseconds(100)));

        allowQueueRelease.Set();
        await queueOwner.WaitAsync(TimeSpan.FromSeconds(10));
        await deviceOwner.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(deviceNativeRelease.IsSet);
    }

    [Fact]
    public void Separate_acquisitions_of_the_same_pointer_release_independently()
    {
        var first = new GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>(1);
        var second = new GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>(1);
        var releases = 0;

        first.Release(_ => releases++);
        second.Release(_ => releases++);

        Assert.Equal(2, releases);
    }

    [Fact]
    public void Null_handles_do_not_release()
    {
        var defaultHandle = default(GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>);
        var releases = 0;

        defaultHandle.Release(_ => releases++);
        new GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>(0).Release(_ => releases++);

        Assert.Equal(0, defaultHandle.Pointer);
        Assert.Null(defaultHandle.Data);
        Assert.Equal(0, releases);
    }

    [Fact]
    public async Task Failed_cleanup_is_not_retried_and_unblocks_waiters()
    {
        var handle = new GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>(1);
        using var cleanupStarted = new ManualResetEventSlim();
        using var allowCleanupFailure = new ManualResetEventSlim();
        using var waiterStarted = new ManualResetEventSlim();
        using var waiterCompleted = new ManualResetEventSlim();
        var releases = 0;

        var owner = Task.Run(() =>
            Assert.Throws<InvalidOperationException>(() =>
                handle.Release(_ =>
                {
                    cleanupStarted.Set();
                    Assert.True(allowCleanupFailure.Wait(TimeSpan.FromSeconds(10)));
                    releases++;
                    throw new InvalidOperationException();
                })));
        Assert.True(cleanupStarted.Wait(TimeSpan.FromSeconds(10)));

        var waiter = Task.Run(() =>
        {
            waiterStarted.Set();
            try
            {
                handle.Release(_ => releases++);
            }
            finally
            {
                waiterCompleted.Set();
            }
        });
        Assert.True(waiterStarted.Wait(TimeSpan.FromSeconds(10)));
        try
        {
            Assert.False(waiterCompleted.Wait(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            allowCleanupFailure.Set();
        }

        await owner.WaitAsync(TimeSpan.FromSeconds(10));
        await waiter.WaitAsync(TimeSpan.FromSeconds(10));
        handle.Release(_ => releases++);

        Assert.Equal(1, releases);
    }
}
