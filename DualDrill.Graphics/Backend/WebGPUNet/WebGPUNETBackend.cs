using DotNext;
using DualDrill.Interop;
using Evergine.Bindings.WebGPU;
using Silk.NET.SDL;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Varena;
namespace DualDrill.Graphics.Backend;
using static Evergine.Bindings.WebGPU.WebGPUNative;
using Backend = DualDrill.Graphics.Backend.WebGPUNETBackend;
using Native = Evergine.Bindings.WebGPU;

internal readonly record struct WebGPUNETHandle<THandle, TResource>(
    THandle Handle
) : IGPUNativeHandle<Backend, TResource>
{
}

public sealed partial class WebGPUNETBackend : IBackend<Backend>
{
    public static Backend Instance { get; } = new();

    private unsafe T* Alloc<T>(int count = 1) where T : unmanaged
    {
        return (T*)NativeMemory.AllocZeroed((nuint)count, (nuint)(sizeof(T)));
    }

    public unsafe GPUInstance<Backend> CreateGPUInstance()
    {
        Native.WGPUInstanceDescriptor descriptor = new();
        var nativeInstance = wgpuCreateInstance(&descriptor);
        return new GPUInstance<Backend>(new(nativeInstance.Handle));
    }

    unsafe ValueTask<GPUAdapter<Backend>> IBackend<Backend>.RequestAdapterAsync(GPUInstance<Backend> instance, GPURequestAdapterOptions options, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<GPUAdapter<Backend>?>(cancellationToken);
        // TODO: static method implementation (passing tcs using user GCHandle/data pointer) for better performance
        Native.WGPURequestAdapterOptions options_ = new();
        unsafe void OnAdapterRequestEnded(WGPURequestAdapterStatus status, WGPUAdapter candidateAdapter, char* message, void* pUserData)
        {
            if (status == WGPURequestAdapterStatus.Success)
            {
                tcs.SetResult(new(new(candidateAdapter.Handle)));

                // TODO: update AdapterProperties and AdapterLimits

                //WGPUAdapterProperties properties;
                //wgpuAdapterGetProperties(candidateAdapter, &properties);

                //WGPUSupportedLimits limits;
                //wgpuAdapterGetLimits(candidateAdapter, &limits);

                //AdapterProperties = properties;
                //AdapterLimits = limits;
            }
            else
            {
                tcs.SetException(new GraphicsApiException<Backend>($"Could not get WebGPU adapter: {Marshal.PtrToStringUTF8((nint)message)}"));
            }
        }
        wgpuInstanceRequestAdapter(ToNative(instance.Handle),
                                        &options_,
                                        OnAdapterRequestEnded, null);
        return new(tcs.Task);
    }

    sealed class DeviceUncapturedError(
        WGPUErrorType ErrorType,
        string Message
    ) : GraphicsApiException<Backend>(
        $"Device Error {Enum.GetName(ErrorType)}, Message: {Message}"
    )
    {
    }

    unsafe ValueTask<GPUDevice<Backend>> IBackend<Backend>.RequestDeviceAsync(GPUAdapter<Backend> adapter, GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        WGPUDeviceDescriptor descriptor_ = new();
        // TODO: filling descriptor fields

        var tcs = new TaskCompletionSource<GPUDevice<Backend>>();
        void OnDeviceRequestEnded(WGPURequestDeviceStatus status, WGPUDevice device, char* message, void* pUserData)
        {
            if (status == WGPURequestDeviceStatus.Success)
            {
                var queue_ = wgpuDeviceGetQueue(device);
                var queue = new GPUQueue<Backend>(new(queue_.Handle));
                wgpuDeviceSetUncapturedErrorCallback(device, static (errorType, message, data) =>
                {
                    var messageString = Marshal.PtrToStringUTF8((nint)message) ?? "Failed to get message";
                    throw new DeviceUncapturedError(errorType, messageString);
                }, null);
                tcs.SetResult(new(new(device.Handle)) { Queue = queue });
            }
            else
            {
                tcs.SetException(new GraphicsApiException<Backend>($"Could not get WebGPU device: {Marshal.PtrToStringUTF8((nint)message)}"));
            }
        }
        wgpuAdapterRequestDevice(ToNative(adapter.Handle), &descriptor_, OnDeviceRequestEnded, null);
        return new(tcs.Task);
    }

    unsafe GPUBuffer<Backend> IBackend<Backend>.CreateBuffer(GPUDevice<Backend> device, GPUBufferDescriptor descriptor)
    {
        var alignedSize = (descriptor.Size + 3UL) & ~3UL;
        //Debug.Assert(descriptor.Size == alignedSize, "Buffer byte size should be multiple of 4");
        WGPUBufferDescriptor nativeDescriptor = new()
        {
            mappedAtCreation = descriptor.MappedAtCreation,
            size = alignedSize,
            usage = ToNative(descriptor.Usage),
        };
        var handle = wgpuDeviceCreateBuffer(ToNative(device.Handle), &nativeDescriptor);
        return new(new(handle.Handle))
        {
            Length = alignedSize
        };
    }

    GPUTextureView<Backend> IBackend<Backend>.CreateTextureView(GPUTexture<Backend> texture, GPUTextureViewDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUDevice> IBackend<Backend>.RequestDeviceAsyncLegacy(GPUAdapter<Backend> adapter, GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }

    GPUTextureFormat IBackend<Backend>.GetPreferredCanvasFormat(GPUInstance<Backend> handle)
    {
        // TODO: consider how to implement this properly
        return GPUTextureFormat.BGRA8Unorm;
    }

    unsafe GPUTexture<Backend> IBackend<Backend>.CreateTexture(GPUDevice<Backend> handle, GPUTextureDescriptor descriptor)
    {
        using var pLabel = InteropUtf8StringValue.Create(descriptor.Label);
        WGPUTextureDescriptor desc = new();
        desc.usage = ToNative(descriptor.Usage);
        desc.mipLevelCount = (uint)descriptor.MipLevelCount;
        desc.sampleCount = (uint)descriptor.SampleCount;
        desc.label = pLabel.CharPointer;
        desc.dimension = ToNative(descriptor.Dimension);
        desc.size = ToNative(descriptor.Size);
        desc.size.depthOrArrayLayers = (uint)descriptor.Size.DepthOrArrayLayers;
        desc.format = ToNative(descriptor.Format);
        var p = stackalloc WGPUTextureFormat[descriptor.ViewFormats.Length];
        if (descriptor.ViewFormats.Length > 0)
        {
            desc.viewFormats = p;
            for (var i = 0; i < descriptor.ViewFormats.Length; i++)
            {
                p[i] = ToNative(descriptor.ViewFormats.Span[i]);
            }
        }
        var h = wgpuDeviceCreateTexture(ToNative(handle.Handle), &desc);
        return new GPUTexture<Backend>(new GPUHandle<Backend, GPUTexture<Backend>>(h.Handle));
    }

    void PopulateNative(ref WGPUExtent3D native, GPUExtent3D value)
    {
        native.height = (uint)value.Height;
        native.width = (uint)value.Width;
        native.depthOrArrayLayers = (uint)value.DepthOrArrayLayers;
    }


    unsafe GPUSampler<Backend> IBackend<Backend>.CreateSampler(GPUDevice<Backend> handle, GPUSamplerDescriptor descriptor)
    {
        var nativeDescriptor = ToNative(descriptor);
        var result = wgpuDeviceCreateSampler(ToNative(handle.Handle), &nativeDescriptor);
        return new GPUSampler<Backend>(new(result.Handle));
        // TODO: add finally / using to free the label
    }

    private unsafe WGPUSamplerDescriptor ToNative(GPUSamplerDescriptor descriptor)
    {
        WGPUSamplerDescriptor nativeDescriptor = new()
        {
            label = (char*)Marshal.StringToHGlobalAnsi(descriptor.Label),
            addressModeU = ToNative(descriptor.AddressModeU),
            addressModeV = ToNative(descriptor.AddressModeV),
            addressModeW = ToNative(descriptor.AddressModeW),
            magFilter = ToNative(descriptor.MagFilter),
            minFilter = ToNative(descriptor.MinFilter),
            mipmapFilter = ToNative(descriptor.MipmapFilter),
            lodMinClamp = descriptor.LodMinClamp,
            lodMaxClamp = descriptor.LodMaxClamp,
            compare = ToNative(descriptor.Compare),
            maxAnisotropy = descriptor.MaxAnisotropy,
        };
        return nativeDescriptor;
    }

    unsafe GPUBindGroupLayout<Backend> IBackend<Backend>.CreateBindGroupLayout(GPUDevice<Backend> handle, GPUBindGroupLayoutDescriptor descriptor)
    {
        var entries = stackalloc WGPUBindGroupLayoutEntry[descriptor.Entries.Length];
        var index = 0;
        foreach (var entry in descriptor.Entries.Span)
        {
            entries[index] = ToNative(entry);


            index++;
        }
        var nativeDescriptor = new WGPUBindGroupLayoutDescriptor
        {
            entryCount = (uint)descriptor.Entries.Length,
            entries = entries
        };
        return new(new(wgpuDeviceCreateBindGroupLayout(ToNative(handle.Handle), &nativeDescriptor).Handle));
    }

    private WGPUBindGroupLayoutEntry ToNative(GPUBindGroupLayoutEntry entry)
    {
        return new WGPUBindGroupLayoutEntry
        {
            binding = (uint)entry.Binding,
            visibility = ToNative(entry.Visibility),
            buffer = ToNative(entry.Buffer),
            sampler = ToNative(entry.Sampler),
            texture = ToNative(entry.Texture),
            storageTexture = ToNative(entry.StorageTexture)
        };
    }

    private WGPUStorageTextureBindingLayout ToNative(GPUStorageTextureBindingLayout storageTexture)
    {
        return new()
        {
            access = ToNative(storageTexture.Access),
            format = ToNative(storageTexture.Format),
            viewDimension = ToNative(storageTexture.ViewDimension),
        };
    }

    private WGPUTextureBindingLayout ToNative(GPUTextureBindingLayout texture)
    {
        return new()
        {
            multisampled = texture.Multisampled,
            sampleType = ToNative(texture.SampleType),
            viewDimension = ToNative(texture.ViewDimension)
        };
    }

    private WGPUSamplerBindingLayout ToNative(GPUSamplerBindingLayout sampler)
    {
        return new()
        {
            type = ToNative(sampler.Type)
        };
    }

    private WGPUBufferBindingLayout ToNative(GPUBufferBindingLayout buffer)
    {
        return new()
        {
            type = ToNative(buffer.Type),
            minBindingSize = buffer.MinBindingSize,
            hasDynamicOffset = buffer.HasDynamicOffset,
        };
    }

    unsafe GPUPipelineLayout<Backend> IBackend<Backend>.CreatePipelineLayout(GPUDevice<Backend> handle, GPUPipelineLayoutDescriptor descriptor)
    {
        var bindGroupLayouts = stackalloc WGPUBindGroupLayout[descriptor.BindGroupLayouts.Count];
        var native = new WGPUPipelineLayoutDescriptor
        {
            bindGroupLayoutCount = (ulong)descriptor.BindGroupLayouts.Count,
            bindGroupLayouts = bindGroupLayouts
        };
        var index = 0;
        foreach (var bindGroupLayout in descriptor.BindGroupLayouts)
        {
            bindGroupLayouts[index] = ToNative(bindGroupLayout);
            index++;
        }


        return new(new(wgpuDeviceCreatePipelineLayout(ToNative(handle.Handle), &native).Handle));

        throw new NotImplementedException();
    }

    WGPUBindGroupEntry ToNative(GPUBindGroupEntry value)
    {
        var result = new WGPUBindGroupEntry()
        {
            binding = (uint)value.Binding,
            offset = value.Offset,
            size = value.Size,
        };
        if (value.Buffer is not null)
        {
            result.buffer = ToNative(value.Buffer);
        }
        if (value.Sampler is not null)
        {
            result.sampler = ToNative(value.Sampler);
        }
        if (value.TextureView is not null)
        {
            result.textureView = ToNative(value.TextureView);
        }
        return result;
    }

    private WGPUTextureView ToNative(IGPUTextureView textureView)
    {
        return ToNative(((GPUTextureView<Backend>)textureView).Handle);
    }

    private WGPUSampler ToNative(IGPUSampler sampler)
    {
        return ToNative(((GPUSampler<Backend>)sampler).Handle);
    }

    unsafe GPUBindGroup<Backend> IBackend<Backend>.CreateBindGroup(GPUDevice<Backend> handle, GPUBindGroupDescriptor descriptor)
    {
        var entries = stackalloc WGPUBindGroupEntry[descriptor.Entries.Length];
        using var label = InteropUtf8StringValue.Create(descriptor.Label);
        for (var i = 0; i < descriptor.Entries.Length; i++)
        {
            entries[i] = ToNative(descriptor.Entries.Span[i]);
        }

        WGPUBindGroupDescriptor nativeDescriptor = new()
        {
            label = label.CharPointer,
            layout = ToNative(descriptor.Layout),
            entryCount = (nuint)descriptor.Entries.Length,
            entries = entries
        };
        return new(new(wgpuDeviceCreateBindGroup(ToNative(handle.Handle), &nativeDescriptor).Handle));
    }

    private WGPUBindGroupLayout ToNative(IGPUBindGroupLayout layout)
    {
        return ToNative(((GPUBindGroupLayout<Backend>)layout).Handle);
    }

    unsafe GPUShaderModule<Backend> IBackend<Backend>.CreateShaderModule(GPUDevice<Backend> handle, GPUShaderModuleDescriptor descriptor)
    {
        using var codeUtf8 = InteropUtf8StringValue.Create(descriptor.Code);
        var dc = new WGPUShaderModuleWGSLDescriptor
        {
            code = codeUtf8.CharPointer,
            chain = new WGPUChainedStruct
            {
                sType = Native.WGPUSType.ShaderModuleWGSLDescriptor
            }
        };

        var d = new WGPUShaderModuleDescriptor
        {
            nextInChain = &dc.chain,
        };
        var h = wgpuDeviceCreateShaderModule(ToNative(handle.Handle), &d);
        return new(new(h.Handle));
    }

    GPUComputePipeline<Backend> IBackend<Backend>.CreateComputePipeline(GPUDevice<Backend> handle, GPUComputePipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }




    unsafe GPURenderPipeline<Backend> IBackend<Backend>.CreateRenderPipeline(GPUDevice<Backend> handle, GPURenderPipelineDescriptor descriptor)
    {
        // TODO: use arena based allocator for better performance and easier free
        WGPURenderPipelineDescriptor desc = new();
        try
        {
            using var pipelineLabel = InteropUtf8StringValue.Create(descriptor.Label);
            using var vertexEntryPoint = InteropUtf8StringValue.Create(descriptor.Vertex.EntryPoint);
            using var fragmentEntryPoint = InteropUtf8StringValue.Create(descriptor.Fragment?.EntryPoint);

            desc.label = pipelineLabel.CharPointer;
            if (descriptor.Layout is not null)
            {
                desc.layout = ToNative(descriptor.Layout);
            }
            desc.vertex = ToNative(descriptor.Vertex);
            desc.primitive = ToNative(descriptor.Primitive);
            if (descriptor.DepthStencil is not null)
            {
                desc.depthStencil = Alloc<WGPUDepthStencilState>();
                *desc.depthStencil = ToNative(descriptor.DepthStencil.Value);
            }

            desc.multisample = ToNative(descriptor.Multisample);

            if (descriptor.Fragment is not null)
            {
                desc.fragment = Alloc<WGPUFragmentState>();
                ref var fragment = ref *desc.fragment;
                *desc.fragment = ToNative(descriptor.Fragment.Value);
            }

            var result = wgpuDeviceCreateRenderPipeline(ToNative(handle.Handle), &desc);
            return new(new(result.Handle));
        }
        finally
        {
            if (desc.fragment is not null)
            {
                Free(*desc.fragment);
                NativeMemory.Free(desc.fragment);
            }
            Free(desc.vertex);
        }
    }

    unsafe private WGPUFragmentState ToNative(GPUFragmentState fragment)
    {
        var result = new WGPUFragmentState
        {
            module = ToNative(fragment.Module),
            constantCount = (ulong)fragment.Constants.Length,
            targetCount = (ulong)fragment.Targets.Length
        };
        if (fragment.EntryPoint is not null)
        {
            result.entryPoint = MarshalString(fragment.EntryPoint);
        }
        if (fragment.Constants.Length > 0)
        {
            throw new NotSupportedException();
        }
        if (fragment.Targets.Length > 0)
        {
            result.targets = Alloc<WGPUColorTargetState>(fragment.Targets.Length);
            for (var i = 0; i < fragment.Targets.Length; i++)
            {
                result.targets[i] = ToNative(fragment.Targets.Span[i]);
            }
        }
        return result;
    }

    private WGPUDepthStencilState ToNative(GPUDepthStencilState depthStencil)
    {

        return new()
        {
            format = ToNative(depthStencil.Format),
            depthWriteEnabled = depthStencil.DepthWriteEnabled,
            depthCompare = ToNative(depthStencil.DepthCompare),
            stencilFront = ToNative(depthStencil.StencilFront),
            stencilBack = ToNative(depthStencil.StencilBack),
            stencilReadMask = depthStencil.StencilReadMask,
            stencilWriteMask = depthStencil.StencilWriteMask,
            depthBias = depthStencil.DepthBias,
            depthBiasSlopeScale = depthStencil.DepthBiasSlopeScale,
            depthBiasClamp = depthStencil.DepthBiasClamp,
        };
    }

    private WGPUStencilFaceState ToNative(GPUStencilFaceState stencilFront)
    {
        return new WGPUStencilFaceState
        {
            compare = ToNative(stencilFront.Compare),
            depthFailOp = ToNative(stencilFront.DepthFailOp),
            failOp = ToNative(stencilFront.FailOp),
            passOp = ToNative(stencilFront.PassOp)
        };
    }

    unsafe private void Free(WGPUFragmentState value)
    {
        if (value.targets is not null)
        {
            // TODO: implement free
        }
    }

    private WGPUPipelineLayout ToNative(IGPUPipelineLayout layout)
    {
        return ToNative(((GPUPipelineLayout<Backend>)layout).Handle);
    }

    private WGPUMultisampleState ToNative(GPUMultisampleState multisample)
    {
        return new WGPUMultisampleState
        {
            count = multisample.Count,
            mask = multisample.Mask,
            alphaToCoverageEnabled = multisample.AlphaToCoverageEnabled
        };
    }

    private WGPUPrimitiveState ToNative(GPUPrimitiveState primitive)
    {
        return new()
        {
            topology = ToNative(primitive.Topology),
            stripIndexFormat = ToNative(primitive.StripIndexFormat),
            frontFace = ToNative(primitive.FrontFace),
            cullMode = ToNative(primitive.CullMode)
        };
    }

    private WGPUBlendState ToNative(GPUBlendState blend)
    {
        return new()
        {
            alpha = ToNative(blend.Alpha),
            color = ToNative(blend.Color)
        };
    }

    private WGPUBlendComponent ToNative(GPUBlendComponent alpha)
    {
        return new WGPUBlendComponent()
        {
            dstFactor = ToNative(alpha.DstFactor),
            srcFactor = ToNative(alpha.SrcFactor),
            operation = ToNative(alpha.Operation),
        };
    }

    unsafe private WGPUColorTargetState ToNative(GPUColorTargetState c)
    {
        var result = new WGPUColorTargetState()
        {
            format = ToNative(c.Format),
            writeMask = ToNative(c.WriteMask),
        };
        if (c.Blend is not null)
        {
            result.blend = Alloc<WGPUBlendState>();
            *result.blend = ToNative(c.Blend.Value);
        }
        return result;
    }

    unsafe private WGPUVertexState ToNative(GPUVertexState vertex)
    {
        if (vertex.Constants.Length > 0)
        {
            throw new NotImplementedException("Constant Entry is not support yet");
        }
        var result = new WGPUVertexState()
        {
            module = ToNative(vertex.Module),
        };
        if (vertex.EntryPoint is not null)
        {
            result.entryPoint = (char*)MarshalString(vertex.EntryPoint);
        }
        if (vertex.Buffers.Length > 0)
        {
            result.buffers = Alloc<WGPUVertexBufferLayout>(vertex.Buffers.Length);
            result.bufferCount = (ulong)vertex.Buffers.Length;
            for (var i = 0; i < vertex.Buffers.Length; i++)
            {
                result.buffers[i] = ToNative(vertex.Buffers.Span[i]);
            }
        }
        return result;
    }

    unsafe private WGPUVertexBufferLayout ToNative(GPUVertexBufferLayout value)
    {
        var result = new WGPUVertexBufferLayout()
        {
            arrayStride = value.ArrayStride,
            stepMode = ToNative(value.StepMode),
            attributeCount = (ulong)value.Attributes.Length
        };
        if (value.Attributes.Length > 0)
        {
            result.attributes = Alloc<WGPUVertexAttribute>(value.Attributes.Length);
            for (var i = 0; i < value.Attributes.Length; i++)
            {
                result.attributes[i] = ToNative(value.Attributes.Span[i]);
            }
        }
        return result;
    }

    private WGPUVertexAttribute ToNative(GPUVertexAttribute value)
    {
        return new WGPUVertexAttribute()
        {
            format = ToNative(value.Format),
            offset = value.Offset,
            shaderLocation = (uint)value.ShaderLocation
        };
    }

    unsafe private char* MarshalString(string value)
    {
        var size = System.Text.Encoding.UTF8.GetByteCount(value) + 1;
        var buffer = NativeMemory.Alloc((nuint)size);
        System.Text.Encoding.UTF8.GetBytes(value, new Span<byte>(buffer, size));
        ((byte*)buffer)[size - 1] = 0;
        return (char*)buffer;
    }

    unsafe void Free(WGPUVertexState value)
    {
        if (value.entryPoint is not null)
        {
            NativeMemory.Free(value.entryPoint);
        }
        if (value.buffers is not null)
        {
            foreach (var b in value.GetBuffers())
            {
                Free(b);
            }
        }
        NativeMemory.Free(value.buffers);
    }

    unsafe void Free(WGPUVertexBufferLayout value)
    {
    }

    private void PopolateNative(ref WGPUFragmentState target, GPUVertexState value)
    {
    }

    private WGPUShaderModule ToNative(IGPUShaderModule module)
    {
        return ToNative(((GPUShaderModule<Backend>)module).Handle);
    }

    ValueTask<GPUComputePipeline<Backend>> IBackend<Backend>.CreateComputePipelineAsync(GPUDevice<Backend> handle, GPUComputePipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPURenderPipeline<Backend>> IBackend<Backend>.CreateRenderPipelineAsync(GPUDevice<Backend> handle, GPURenderPipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    unsafe GPUCommandEncoder<Backend> IBackend<Backend>.CreateCommandEncoder(GPUDevice<Backend> handle, GPUCommandEncoderDescriptor descriptor)
    {
        var label = InteropUtf8StringValue.Create(descriptor.Label);

        WGPUCommandEncoderDescriptor nativeDescriptor = new()
        {
            label = label.CharPointer
        };
        var h = wgpuDeviceCreateCommandEncoder(ToNative(handle.Handle), &nativeDescriptor);
        return new(new(h.Handle));
    }

    GPURenderBundleEncoder<Backend> IBackend<Backend>.CreateRenderBundleEncoder(GPUDevice<Backend> handle, GPURenderBundleEncoderDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUQuerySet<Backend> IBackend<Backend>.CreateQuerySet(GPUDevice<Backend> handle, GPUQuerySetDescriptor descriptor)
    {
        throw new NotImplementedException();
    }


    unsafe ValueTask IBackend<Backend>.MapAsync(GPUBuffer<Backend> handle, GPUMapMode mode, ulong offset, ulong size, CancellationToken cancellation)
    {
        var t = new TaskCompletionSource();
        unsafe void BufferMapped(WGPUBufferMapAsyncStatus status, void* userData)
        {
            if (status == WGPUBufferMapAsyncStatus.Success)
            {
                t.SetResult();
            }
            else
            {
                t.SetException(new GraphicsApiException<Backend>($"Map buffer failed {Enum.GetName(status)}"));
            }
        }
        wgpuBufferMapAsync(ToNative(handle.Handle), ToNative(mode), offset, size, BufferMapped, null);
        return new ValueTask(t.Task);
    }

    unsafe Span<byte> IBackend<Backend>.GetMappedRange(GPUBuffer<Backend> handle, ulong offset, ulong size)
    {
        var ptr = wgpuBufferGetMappedRange(ToNative(handle.Handle), offset, size);
        return new(ptr, (int)size);
    }

    WGPUColor ToNative(GPUColor c)
    {
        return new()
        {
            r = c.R,
            g = c.G,
            b = c.B,
            a = c.A,
        };
    }

    WGPURenderPassColorAttachment ToNative(GPURenderPassColorAttachment c)
    {
        var result = new WGPURenderPassColorAttachment
        {
            view = ToNative(((GPUTextureView<Backend>)c.View).Handle),
            loadOp = ToNative(c.LoadOp),
            storeOp = ToNative(c.StoreOp),
            clearValue = ToNative(c.ClearValue)
        };
        if (c.ResolveTarget is not null)
        {
            result.resolveTarget = ToNative(((GPUTextureView<Backend>)c.ResolveTarget).Handle);
        }
        return result;
    }

    unsafe GPURenderPassEncoder<Backend> IBackend<Backend>.BeginRenderPass(GPUCommandEncoder<Backend> handle, GPURenderPassDescriptor descriptor)
    {
        WGPURenderPassDepthStencilAttachment depthStencilAttachment = new();
        if (descriptor.DepthStencilAttachment.HasValue)
        {
            throw new NotImplementedException();
        }
        var colorAttachments = stackalloc WGPURenderPassColorAttachment[descriptor.ColorAttachments.Length];
        for (var i = 0; i < descriptor.ColorAttachments.Length; i++)
        {
            colorAttachments[i] = ToNative(descriptor.ColorAttachments.Span[i]);
        }
        WGPURenderPassDescriptor nativeDescriptor = new WGPURenderPassDescriptor
        {
            colorAttachmentCount = (uint)descriptor.ColorAttachments.Length,
            colorAttachments = colorAttachments,
            depthStencilAttachment = descriptor.DepthStencilAttachment.HasValue ? &depthStencilAttachment : null
        };
        var result = wgpuCommandEncoderBeginRenderPass(ToNative(handle.Handle), &nativeDescriptor);
        return new(new(result.Handle));
    }

    GPUComputePassEncoder<Backend> IBackend<Backend>.BeginComputePass(GPUCommandEncoder<Backend> handle, GPUComputePassDescriptor descriptor)
    {
        throw new NotImplementedException();
    }


    void IBackend<Backend>.CopyBufferToTexture(GPUCommandEncoder<Backend> handle, GPUImageCopyBuffer source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }

    WGPUTexture ToNative(IGPUTexture value)
    {
        return ToNative(((GPUTexture<Backend>)value).Handle);
    }

    WGPUImageCopyTexture ToNative(GPUImageCopyTexture value)
    {
        return new()
        {
            texture = ToNative(value.Texture),
            aspect = ToNative(value.Aspect),
            mipLevel = value.MipLevel,
            origin = ToNative(value.Origin)
        };
    }

    WGPUOrigin3D ToNative(GPUOrigin3D value)
    {
        return new()
        {
            x = value.X,
            y = value.Y,
            z = value.Z
        };
    }

    WGPUBuffer ToNative(IGPUBuffer value)
    {
        return ToNative(((GPUBuffer<Backend>)value).Handle);
    }

    WGPUTextureDataLayout ToNative(GPUImageDataLayout value)
    {
        return new()
        {
            offset = value.Offset,
            bytesPerRow = value.BytesPerRow,
            rowsPerImage = value.RowsPerImage,
        };
    }

    WGPUImageCopyBuffer ToNative(GPUImageCopyBuffer value)
    {
        return new()
        {
            buffer = ToNative(value.Buffer),
            layout = ToNative(value.Layout)
        };
    }

    WGPUExtent3D ToNative(GPUExtent3D value)
    {
        return new()
        {
            width = value.Width,
            height = value.Height,
            depthOrArrayLayers = value.DepthOrArrayLayers
        };
    }

    unsafe void IBackend<Backend>.CopyTextureToBuffer(GPUCommandEncoder<Backend> handle, GPUImageCopyTexture source, GPUImageCopyBuffer destination, GPUExtent3D copySize)
    {
        WGPUImageCopyTexture nativeSource = ToNative(source);
        WGPUImageCopyBuffer nativeDestination = ToNative(destination);
        WGPUExtent3D nativeCopySize = ToNative(copySize);
        wgpuCommandEncoderCopyTextureToBuffer(ToNative(handle.Handle), &nativeSource, &nativeDestination, &nativeCopySize);
    }

    void IBackend<Backend>.CopyTextureToTexture(GPUCommandEncoder<Backend> handle, GPUImageCopyTexture source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }


    unsafe GPUCommandBuffer<Backend> IBackend<Backend>.Finish(GPUCommandEncoder<Backend> handle, GPUCommandBufferDescriptor descriptor)
    {
        using var label = InteropUtf8StringValue.Create(descriptor.Label);
        WGPUCommandBufferDescriptor d = new()
        {
            label = label.CharPointer
        };
        var h = wgpuCommandEncoderFinish(ToNative(handle.Handle), &d);
        return new(new(h.Handle));
    }


    void IBackend<Backend>.SetBindGroup(GPUComputePassEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        throw new NotImplementedException();
    }

    unsafe void IBackend<Backend>.Submit(GPUQueue<Backend> handle, IReadOnlyList<GPUCommandBuffer<Backend>> commandBuffers)
    {
        var count = commandBuffers.Count;
        var cmds = stackalloc WGPUCommandBuffer[count];
        for (int i = 0; i < count; i++)
        {
            cmds[i] = ToNative(commandBuffers[i].Handle);
        }
        wgpuQueueSubmit(ToNative(handle.Handle), (uint)count, cmds);
    }


    unsafe void IBackend<Backend>.WriteBuffer(GPUQueue<Backend> handle, GPUBuffer<Backend> buffer, ulong bufferOffset, nint data, ulong dataOffset, ulong size)
    {
        wgpuQueueWriteBuffer(ToNative(handle.Handle), ToNative(buffer.Handle), bufferOffset, (void*)data, size);
    }

    unsafe void IBackend<Backend>.WriteTexture(GPUQueue<Backend> handle, GPUImageCopyTexture destination, ReadOnlySpan<byte> data, GPUImageDataLayout dataLayout, GPUExtent3D size)
    {
        var nativeDestination = new WGPUImageCopyTexture
        {
            aspect = ToNative(destination.Aspect),
            mipLevel = destination.MipLevel,
            origin = ToNative(destination.Origin),
            texture = ToNative(destination.Texture),
        };
        var nativeLayout = ToNative(dataLayout);
        var nativeExtend = ToNative(size);
        wgpuQueueWriteTexture(
            ToNative(handle.Handle),
            &nativeDestination,
            Unsafe.AsPointer(ref MemoryMarshal.GetReference(data)),
            (nuint)data.Length,
            &nativeLayout,
            &nativeExtend
        );
    }


    GPURenderBundle<Backend> IBackend<Backend>.Finish(GPURenderBundleEncoder<Backend> handle, GPURenderBundleDescriptor descriptor)
    {
        throw new NotImplementedException();
    }


    void IBackend<Backend>.SetBindGroup(GPURenderBundleEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.SetBindGroup(GPURenderBundleEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.SetIndexBuffer(GPURenderBundleEncoder<Backend> handle, GPUBuffer<Backend> buffer, GPUIndexFormat indexFormat, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }


    void IBackend<Backend>.SetVertexBuffer(GPURenderBundleEncoder<Backend> handle, int slot, GPUBuffer<Backend>? buffer, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.ExecuteBundles(GPURenderPassEncoder<Backend> handle, ReadOnlySpan<GPURenderBundle<Backend>> bundles)
    {
        throw new NotImplementedException();
    }


    unsafe void IBackend<Backend>.SetBindGroup(GPURenderPassEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        uint* ptr = null;
        if (dynamicOffsets.Length > 0)
        {
            ptr = (uint*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(dynamicOffsets));
        }
        wgpuRenderPassEncoderSetBindGroup(ToNative(handle.Handle), (uint)index, bindGroup is not null ? ToNative(bindGroup.Handle) : WGPUBindGroup.Null, (ulong)dynamicOffsets.Length, ptr);
    }

    void IBackend<Backend>.SetBindGroup(GPURenderPassEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.SetBlendConstant(GPURenderPassEncoder<Backend> handle, GPUColor color)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.SetIndexBuffer(GPURenderPassEncoder<Backend> handle, GPUBuffer<Backend> buffer, GPUIndexFormat indexFormat, ulong offset, ulong size)
    {
        wgpuRenderPassEncoderSetIndexBuffer(ToNative(handle.Handle), ToNative(buffer.Handle), ToNative(indexFormat), offset, size);
    }

    void IBackend<Backend>.SetVertexBuffer(GPURenderPassEncoder<Backend> handle, int slot, GPUBuffer<Backend>? buffer, ulong offset, ulong size)
    {
        wgpuRenderPassEncoderSetVertexBuffer(ToNative(handle.Handle), (uint)slot, buffer is not null ? ToNative(buffer.Handle) : WGPUBuffer.Null, offset, size);
    }

    void IBackend<Backend>.SetViewport(GPURenderPassEncoder<Backend> handle, float x, float y, float width, float height, float minDepth, float maxDepth)
    {
        throw new NotImplementedException();
    }

    unsafe GPUBindGroupLayout<Backend> IBackend<Backend>.GetBindGroupLayout(GPURenderPipeline<Backend> handle, ulong index)
    {
        return new(new(wgpuRenderPipelineGetBindGroupLayout(ToNative(handle.Handle), (uint)index).Handle));
    }

    ValueTask<GPUCompilationInfo> IBackend<Backend>.GetCompilationInfoAsync(GPUShaderModule<Backend> handle, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    WGPUDevice ToNative(IGPUDevice value)
    {
        return ToNative(((GPUDevice<Backend>)value).Handle);
    }

    unsafe void IBackend<Backend>.Configure(GPUSurface<Backend> handle, GPUSurfaceConfiguration configuration)
    {
        var nativeConfig = new WGPUSurfaceConfiguration
        {
            device = ToNative(configuration.Device),
            format = ToNative(configuration.Format),
            usage = ToNative(configuration.Usage),
            viewFormatCount = (nuint)configuration.ViewFormats.Count,
            viewFormats = null,
            alphaMode = ToNative(configuration.AlphaMode),
            width = (uint)configuration.Width,
            height = (uint)configuration.Height,
            presentMode = ToNative(configuration.PresentMode)
        };
        var p = stackalloc WGPUTextureFormat[configuration.ViewFormats.Count];
        for (var i = 0; i < configuration.ViewFormats.Count; i++)
        {
            p[i] = ToNative(configuration.ViewFormats[i]);
        }
        if (configuration.ViewFormats.Count > 0)
        {
            nativeConfig.viewFormats = p;
        }
        wgpuSurfaceConfigure(ToNative(handle.Handle), &nativeConfig);
    }

    WGPUPresentMode ToNative(GPUPresentMode mode)
    {
        return (WGPUPresentMode)mode;
    }

    WGPUCompositeAlphaMode ToNative(GPUCompositeAlphaMode alphaMode)
    {
        return (WGPUCompositeAlphaMode)alphaMode;
    }

    unsafe GPUTexture<Backend> IBackend<Backend>.GetCurrentTexture(GPUSurface<Backend> handle)
    {
        WGPUSurfaceTexture result = new();
        wgpuSurfaceGetCurrentTexture(ToNative(handle.Handle), &result);
        if (result.status != WGPUSurfaceGetCurrentTextureStatus.Success)
        {
            throw new GraphicsApiException<Backend>($"Failed to get current texture, status {Enum.GetName(result.status)}");
        }
        return new GPUTexture<Backend>(new(result.texture.Handle));
    }

    unsafe WGPUTextureViewDescriptor ToNative(GPUTextureViewDescriptor descriptor)
    {
        var result = new WGPUTextureViewDescriptor();
        if (!string.IsNullOrEmpty(descriptor.Label))
        {
            result.label = (char*)Marshal.StringToHGlobalAnsi(descriptor.Label);
        }
        result.format = ToNative(descriptor.Format);
        result.dimension = ToNative(descriptor.Dimension);
        result.baseMipLevel = (uint)descriptor.BaseMipLevel;
        result.mipLevelCount = (uint)descriptor.MipLevelCount;
        result.baseArrayLayer = (uint)descriptor.BaseArrayLayer;
        result.arrayLayerCount = (uint)descriptor.ArrayLayerCount;
        result.aspect = ToNative(descriptor.Aspect);
        return result;
    }

    unsafe GPUTextureView<Backend> IBackend<Backend>.CreateView(GPUTexture<Backend> handle, GPUTextureViewDescriptor? descriptor)
    {
        WGPUTextureView resultHandle;
        if (descriptor is null)
        {
            resultHandle = wgpuTextureCreateView(ToNative(handle.Handle), null);
        }
        else
        {
            var d_ = ToNative(descriptor.Value);
            try
            {
                resultHandle = wgpuTextureCreateView(ToNative(handle.Handle), &d_);
            }
            finally
            {
                if (d_.label is not null)
                {
                    Marshal.FreeHGlobal((nint)d_.label);
                }
            }
        }
        return new GPUTextureView<Backend>(new GPUHandle<Backend, GPUTextureView<Backend>>(resultHandle.Handle));
    }

    void IBackend<Backend>.SetBindGroup(GPUComputePassEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        throw new NotImplementedException();
    }

    GPUBindGroupLayout<Backend> IBackend<Backend>.GetBindGroupLayout(GPUComputePipeline<Backend> handle, ulong index)
    {
        throw new NotImplementedException();
    }

    ValueTask IBackend<Backend>.OnSubmittedWorkDoneAsync(GPUQueue<Backend> handle, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    unsafe void IBackend<Backend>.Poll(GPUDevice<Backend> device)
    {
        wgpuDevicePoll(ToNative(device.Handle), false, null);
    }

    async ValueTask IBackend<Backend>.PollAsync(GPUDevice<Backend> device, CancellationToken cancellation)
    {
        await Task.Run(() =>
        {
            unsafe static void PollWait(WGPUDevice device)
            {
                wgpuDevicePoll(device, true, null);
            }
            PollWait(ToNative(device.Handle));
        }, cancellation).ConfigureAwait(true);
    }

    ValueTask<GPUAdapterInfo> IBackend<Backend>.RequestAdapterInfoAsync(GPUAdapter<Backend> adapter, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.Present(GPUSurface<Backend> surface)
    {
        wgpuSurfacePresent(ToNative(surface.Handle));
    }
}
static class WebGPUNETExtension
{
    public unsafe static Span<WGPUVertexBufferLayout> GetBuffers(this WGPUVertexState value)
             => new(value.buffers, (int)value.bufferCount);
}

