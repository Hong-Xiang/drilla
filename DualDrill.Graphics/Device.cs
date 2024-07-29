using DualDrill.Graphics.Interop;
using Silk.NET.WebGPU;
using System.Diagnostics;

namespace DualDrill.Graphics;

public sealed partial class GPUDevice
{
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

public unsafe sealed class Device(
    Silk.NET.WebGPU.WebGPU Api,
    Silk.NET.WebGPU.Device* Handle
)
{
    public Silk.NET.WebGPU.Device* Handle { get; } = Handle;

    public ShaderModule CreateShaderModule(string code)
    {
        using var codePtr = NativeStringRef.Create(code);
        var wgslDescriptor = new ShaderModuleWGSLDescriptor
        {
            Chain = new ChainedStruct
            {
                SType = SType.ShaderModuleWgslDescriptor
            },
            Code = codePtr.Handle,
        };
        var shaderDescriptor = new ShaderModuleDescriptor
        {
            NextInChain = &wgslDescriptor.Chain,
        };
        return new ShaderModule(Api, Api.DeviceCreateShaderModule(Handle, &shaderDescriptor));
    }

    public RenderPipeline* CreateRenderPipeline(in RenderPipelineDescriptor descriptor)
    {
        return Api.DeviceCreateRenderPipeline(Handle, in descriptor);
    }

    public Buffer CreateBuffer(in BufferDescriptor descriptor)
    {
        return new(Api, Api.DeviceCreateBuffer(Handle, in descriptor));
    }
}
