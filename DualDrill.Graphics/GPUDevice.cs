using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;

public partial interface IGPUDevice : IDisposable
{
    public IGPUQueue Queue { get; }

    public IGPUBindGroup CreateBindGroup(GPUBindGroupDescriptor descriptor);

    public IGPUBindGroupLayout CreateBindGroupLayout(
    GPUBindGroupLayoutDescriptor descriptor
    );

    public IGPUBuffer CreateBuffer(GPUBufferDescriptor descriptor);


    public IGPUCommandEncoder CreateCommandEncoder(GPUCommandEncoderDescriptor descriptor);


    public IGPUComputePipeline CreateComputePipeline(GPUComputePipelineDescriptor descriptor);


    public ValueTask<IGPUComputePipeline> CreateComputePipelineAsync(
    GPUComputePipelineDescriptor descriptor
    , CancellationToken cancellation
    );

    public IGPUPipelineLayout CreatePipelineLayout(GPUPipelineLayoutDescriptor descriptor);

    public IGPUQuerySet CreateQuerySet(GPUQuerySetDescriptor descriptor);
    public IGPURenderBundleEncoder CreateRenderBundleEncoder(GPURenderBundleEncoderDescriptor descriptor);
    public IGPURenderPipeline CreateRenderPipeline(GPURenderPipelineDescriptor descriptor);

    public ValueTask<IGPURenderPipeline> CreateRenderPipelineAsync(GPURenderPipelineDescriptor descriptor, CancellationToken cancellation);


    public IGPUSampler CreateSampler(GPUSamplerDescriptor descriptor);
    public IGPUShaderModule CreateShaderModule(GPUShaderModuleDescriptor descriptor);


    public IGPUTexture CreateTexture(GPUTextureDescriptor descriptor);

    public void Poll();

    public ValueTask PollAsync(CancellationToken cancellation);
}

public sealed partial record class GPUDevice<TBackend>(GPUHandle<TBackend, GPUDevice<TBackend>> Handle)
    : IDisposable, IGPUDevice
    where TBackend : IBackend<TBackend>
{
    public required IGPUQueue Queue { get; init; }

    public IGPUBindGroup CreateBindGroup(GPUBindGroupDescriptor descriptor)
    {
        return TBackend.Instance.CreateBindGroup(this, descriptor);
    }

    public IGPUBindGroupLayout CreateBindGroupLayout(GPUBindGroupLayoutDescriptor descriptor)
    {
        return TBackend.Instance.CreateBindGroupLayout(this, descriptor);
    }

    public IGPUBuffer CreateBuffer(GPUBufferDescriptor descriptor)
    {
        return TBackend.Instance.CreateBuffer(this, descriptor);
    }

    public IGPUCommandEncoder CreateCommandEncoder(GPUCommandEncoderDescriptor descriptor)
    {
        return TBackend.Instance.CreateCommandEncoder(this, descriptor);
    }

    public IGPUComputePipeline CreateComputePipeline(GPUComputePipelineDescriptor descriptor)
    {
        return TBackend.Instance.CreateComputePipeline(this, descriptor);
    }

    public async ValueTask<IGPUComputePipeline> CreateComputePipelineAsync(GPUComputePipelineDescriptor descriptor, CancellationToken cancellation)
    {
        return await TBackend.Instance.CreateComputePipelineAsync(this, descriptor, cancellation);
    }

    public IGPUPipelineLayout CreatePipelineLayout(GPUPipelineLayoutDescriptor descriptor)
    {
        return TBackend.Instance.CreatePipelineLayout(this, descriptor);
    }

    public IGPUQuerySet CreateQuerySet(GPUQuerySetDescriptor descriptor)
    {
        return TBackend.Instance.CreateQuerySet(this, descriptor);
    }

    public IGPURenderBundleEncoder CreateRenderBundleEncoder(GPURenderBundleEncoderDescriptor descriptor)
    {
        return TBackend.Instance.CreateRenderBundleEncoder(this, descriptor);
    }

    public IGPURenderPipeline CreateRenderPipeline(GPURenderPipelineDescriptor descriptor)
    {
        return TBackend.Instance.CreateRenderPipeline(this, descriptor);
    }

    public async ValueTask<IGPURenderPipeline> CreateRenderPipelineAsync(GPURenderPipelineDescriptor descriptor, CancellationToken cancellation)
    {
        return await TBackend.Instance.CreateRenderPipelineAsync(this, descriptor, cancellation);
    }

    public IGPUSampler CreateSampler(GPUSamplerDescriptor descriptor)
    {
        return TBackend.Instance.CreateSampler(this, descriptor);
    }

    public IGPUShaderModule CreateShaderModule(GPUShaderModuleDescriptor descriptor)
    {
        return TBackend.Instance.CreateShaderModule(this, descriptor);
    }

    public IGPUTexture CreateTexture(GPUTextureDescriptor descriptor)
    {
        return TBackend.Instance.CreateTexture(this, descriptor);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(((GPUQueue<TBackend>)Queue).Handle);
        TBackend.Instance.DisposeHandle(Handle);
    }

    public void Poll()
    {
        TBackend.Instance.Poll(this);
    }

    public ValueTask PollAsync(CancellationToken cancellation)
    {
        return TBackend.Instance.PollAsync(this, cancellation);
    }
}




public sealed partial class GPUDevice
{
    public GPURenderPipeline CreateRenderPipeline(GPURenderPipelineDescriptor descriptor)
    {
        return GPURenderPipeline.Create(this, descriptor);
    }

    public unsafe GPUCommandEncoder CreateCommandEncoder(GPUCommandEncoderDescriptor descriptor)
    {
        if (descriptor.Label is not null)
        {
            throw new NotImplementedException();
        }
        WGPUCommandEncoderDescriptor nativeDescriptor = new()
        {
        };
        return new(WGPU.DeviceCreateCommandEncoder(Handle, &nativeDescriptor));
    }

    public unsafe GPUSampler CreateSampler(GPUSamplerDescriptor descriptor)
    {
        WGPUSamplerDescriptor nativeDescriptor = new()
        {
            addressModeU = descriptor.AddressModeU,
            addressModeV = descriptor.AddressModeV,
            addressModeW = descriptor.AddressModeW,
            magFilter = descriptor.MagFilter,
            minFilter = descriptor.MinFilter,
            mipmapFilter = descriptor.MipmapFilter,
            lodMinClamp = descriptor.LodMinClamp,
            lodMaxClamp = descriptor.LodMaxClamp,
            compare = descriptor.Compare,
            maxAnisotropy = descriptor.MaxAnisotropy
        };
        return new(WGPU.DeviceCreateSampler(Handle, &nativeDescriptor));
    }

    public unsafe void Poll()
    {
        _ = WGPU.DevicePoll(Handle, 0, null);
    }

    internal async Task PollAndWaitOnTaskAsync(Task target)
    {
        unsafe void DoPoll()
        {
            _ = WGPU.DevicePoll(Handle, 0, null);
        }
        while (!target.IsCompleted)
        {
            DoPoll();
            await Task.Yield();
        }
    }


    public unsafe GPUQueue GetQueue()
    {
        return new(WGPU.DeviceGetQueue(Handle));
    }

    public unsafe GPUTexture CreateTexture(GPUTextureDescriptor descriptor)
    {

        using var label = NativeStringRef.Create(descriptor.Label);
        var viewFormats = stackalloc GPUTextureFormat[descriptor.ViewFormats.Length];
        for (var i = 0; i < descriptor.ViewFormats.Length; i++)
        {
            viewFormats[i] = descriptor.ViewFormats.Span[i];
        }
        WGPUTextureDescriptor native = new()
        {
            label = (sbyte*)label.Handle,
            usage = (uint)descriptor.Usage,
            dimension = descriptor.Dimension,
            size = descriptor.Size,
            format = descriptor.Format,
            mipLevelCount = (uint)descriptor.MipLevelCount,
            sampleCount = (uint)descriptor.SampleCount,
            viewFormatCount = (nuint)descriptor.ViewFormats.Length,
            viewFormats = descriptor.ViewFormats.Length > 0 ? viewFormats : null,
        };
        return new GPUTexture(WGPU.DeviceCreateTexture(Handle, &native));
    }

    public unsafe GPUBuffer CreateBuffer(GPUBufferDescriptor descriptor)
    {
        var alignedSize = (descriptor.Size + 3UL) & ~3UL;
        //Debug.Assert(descriptor.Size == alignedSize, "Buffer byte size should be multiple of 4");
        WGPUBufferDescriptor nativeDescriptor = new()
        {
            mappedAtCreation = descriptor.MappedAtCreation.Value,
            size = alignedSize,
            usage = (uint)descriptor.Usage,
        };
        var handle = WGPU.DeviceCreateBuffer(Handle, &nativeDescriptor);
        return new(handle);
    }

    public unsafe GPUBindGroup CreateBindGroup(GPUBindGroupDescriptor descriptor)
    {
        var entries = stackalloc WGPUBindGroupEntry[descriptor.Entries.Length];
        var entryIndex = 0;
        using var label = NativeStringRef.Create(descriptor.Label);
        foreach (var entry in descriptor.Entries.Span)
        {
            entries[entryIndex] = new WGPUBindGroupEntry()
            {
                binding = (uint)entry.Binding,
                buffer = entry.Buffer is GPUBuffer b ? b.NativePointer : null,
                offset = entry.Offset,
                size = entry.Size,
                sampler = entry.Sampler is GPUSampler s ? s.NativePointer : null,
                textureView = entry.TextureView is GPUTextureView v ? v.NativePointer : null
            };
            entryIndex++;
        }


        WGPUBindGroupDescriptor nativeDescriptor = new()
        {
            label = (sbyte*)label.Handle,
            layout = descriptor.Layout.Handle,
            entryCount = (nuint)descriptor.Entries.Length,
            entries = entries
        };
        return new GPUBindGroup(WGPU.DeviceCreateBindGroup(Handle, &nativeDescriptor));
    }

    public unsafe GPUBindGroupLayout CreateBindGroupLayout(GPUBindGroupLayoutDescriptor descriptor)
    {
        var entries = stackalloc WGPUBindGroupLayoutEntry[descriptor.Entries.Length];
        var index = 0;
        foreach (var entry in descriptor.Entries.Span)
        {
            entries[index] = new WGPUBindGroupLayoutEntry
            {
                binding = (uint)entry.Binding,
                visibility = (uint)entry.Visibility,
                buffer = entry.Buffer
            };
            index++;
        }
        var nativeDescriptor = new WGPUBindGroupLayoutDescriptor
        {
            entryCount = (uint)descriptor.Entries.Length,
            entries = entries
        };
        return new(WGPU.DeviceCreateBindGroupLayout(Handle, &nativeDescriptor));
    }

    public unsafe GPUPipelineLayout CreatePipelineLayout(GPUPipelineLayoutDescriptor descriptor)
    {
        var bindGroupLayouts = stackalloc IntPtr[descriptor.BindGroupLayouts.Length];
        var native = new WGPUPipelineLayoutDescriptor
        {
            bindGroupLayoutCount = (nuint)descriptor.BindGroupLayouts.Length,
            bindGroupLayouts = (WGPUBindGroupLayoutImpl**)bindGroupLayouts
        };
        var index = 0;
        foreach (var bindGroupLayout in descriptor.BindGroupLayouts.Span)
        {
            bindGroupLayouts[index] = (nint)(WGPUBindGroupLayoutImpl*)bindGroupLayout.Handle;
            index++;
        }


        return new GPUPipelineLayout(WGPU.DeviceCreatePipelineLayout(Handle, &native));
    }
}
