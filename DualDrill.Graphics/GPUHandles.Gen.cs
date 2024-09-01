namespace DualDrill.Graphics;
public interface IGPUAdapter
{
}

public sealed partial record class GPUAdapter<TBackend>(GPUHandle<TBackend, GPUAdapter<TBackend>> Handle)
    : IDisposable, IGPUAdapter
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUBindGroup
{
}

public sealed partial record class GPUBindGroup<TBackend>(GPUHandle<TBackend, GPUBindGroup<TBackend>> Handle)
    : IDisposable, IGPUBindGroup
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUBindGroupLayout
{
}

public sealed partial record class GPUBindGroupLayout<TBackend>(GPUHandle<TBackend, GPUBindGroupLayout<TBackend>> Handle)
    : IDisposable, IGPUBindGroupLayout
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUBuffer
{
}

public sealed partial record class GPUBuffer<TBackend>(GPUHandle<TBackend, GPUBuffer<TBackend>> Handle)
    : IDisposable, IGPUBuffer
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUCommandBuffer
{
}

public sealed partial record class GPUCommandBuffer<TBackend>(GPUHandle<TBackend, GPUCommandBuffer<TBackend>> Handle)
    : IDisposable, IGPUCommandBuffer
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUCommandEncoder
{
}

public sealed partial record class GPUCommandEncoder<TBackend>(GPUHandle<TBackend, GPUCommandEncoder<TBackend>> Handle)
    : IDisposable, IGPUCommandEncoder
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUComputePassEncoder
{
}

public sealed partial record class GPUComputePassEncoder<TBackend>(GPUHandle<TBackend, GPUComputePassEncoder<TBackend>> Handle)
    : IDisposable, IGPUComputePassEncoder
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUComputePipeline
{
}

public sealed partial record class GPUComputePipeline<TBackend>(GPUHandle<TBackend, GPUComputePipeline<TBackend>> Handle)
    : IDisposable, IGPUComputePipeline
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUDevice
{
}

public sealed partial record class GPUDevice<TBackend>(GPUHandle<TBackend, GPUDevice<TBackend>> Handle)
    : IDisposable, IGPUDevice
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUInstance
{
}

public sealed partial record class GPUInstance<TBackend>(GPUHandle<TBackend, GPUInstance<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUPipelineLayout
{
}

public sealed partial record class GPUPipelineLayout<TBackend>(GPUHandle<TBackend, GPUPipelineLayout<TBackend>> Handle)
    : IDisposable, IGPUPipelineLayout
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUQuerySet
{
}

public sealed partial record class GPUQuerySet<TBackend>(GPUHandle<TBackend, GPUQuerySet<TBackend>> Handle)
    : IDisposable, IGPUQuerySet
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUQueue
{
}

public sealed partial record class GPUQueue<TBackend>(GPUHandle<TBackend, GPUQueue<TBackend>> Handle)
    : IDisposable, IGPUQueue
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPURenderBundle
{
}

public sealed partial record class GPURenderBundle<TBackend>(GPUHandle<TBackend, GPURenderBundle<TBackend>> Handle)
    : IDisposable, IGPURenderBundle
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPURenderBundleEncoder
{
}

public sealed partial record class GPURenderBundleEncoder<TBackend>(GPUHandle<TBackend, GPURenderBundleEncoder<TBackend>> Handle)
    : IDisposable, IGPURenderBundleEncoder
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPURenderPassEncoder
{
}

public sealed partial record class GPURenderPassEncoder<TBackend>(GPUHandle<TBackend, GPURenderPassEncoder<TBackend>> Handle)
    : IDisposable, IGPURenderPassEncoder
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPURenderPipeline
{
}

public sealed partial record class GPURenderPipeline<TBackend>(GPUHandle<TBackend, GPURenderPipeline<TBackend>> Handle)
    : IDisposable, IGPURenderPipeline
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUSampler
{
}

public sealed partial record class GPUSampler<TBackend>(GPUHandle<TBackend, GPUSampler<TBackend>> Handle)
    : IDisposable, IGPUSampler
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUShaderModule
{
}

public sealed partial record class GPUShaderModule<TBackend>(GPUHandle<TBackend, GPUShaderModule<TBackend>> Handle)
    : IDisposable, IGPUShaderModule
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public sealed partial record class GPUSurface<TBackend>(GPUHandle<TBackend, GPUSurface<TBackend>> Handle)
    : IDisposable
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUTexture
{
}

public sealed partial record class GPUTexture<TBackend>(GPUHandle<TBackend, GPUTexture<TBackend>> Handle)
    : IDisposable, IGPUTexture
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUTextureView
{
}

public sealed partial record class GPUTextureView<TBackend>(GPUHandle<TBackend, GPUTextureView<TBackend>> Handle)
    : IDisposable, IGPUTextureView
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}

