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
    public void Failed_cleanup_is_not_retried()
    {
        var handle = new GPUHandle<WebGpuBackend, GPUBuffer<WebGpuBackend>>(1);
        var releases = 0;

        Assert.Throws<InvalidOperationException>(() =>
            handle.Release(_ =>
            {
                releases++;
                throw new InvalidOperationException();
            }));
        handle.Release(_ => releases++);

        Assert.Equal(1, releases);
    }
}
