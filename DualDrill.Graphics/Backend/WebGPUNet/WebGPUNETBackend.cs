using DotNext;
using DualDrill.Interop;
using Evergine.Bindings.WebGPU;
using System.Globalization;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
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

    GPUShaderModule<Backend> IBackend<Backend>.CreateShaderModule(GPUDevice<Backend> handle, GPUShaderModuleDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUComputePipeline<Backend> IBackend<Backend>.CreateComputePipeline(GPUDevice<Backend> handle, GPUComputePipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }




    unsafe GPURenderPipeline<Backend> IBackend<Backend>.CreateRenderPipeline(GPUDevice<Backend> handle, GPURenderPipelineDescriptor descriptor)
    {
        using var disposables = new CompositeDisposable();

        WGPURenderPipelineDescriptor desc = new();
        using var vertexEntryPoint = InteropUtf8StringValue.Create(descriptor.Vertex.EntryPoint);
        using var fragmentEntryPoint = InteropUtf8StringValue.Create(descriptor.Fragment?.EntryPoint);

        desc.vertex = ToNative(descriptor.Vertex);
        desc.vertex.entryPoint = vertexEntryPoint.CharPointer;
        var vertexConstants = stackalloc WGPUConstantEntry[descriptor.Vertex.Constants.Length];

        WGPUConstantEntry* fragmentConstants = null;
        WGPUFragmentState fragment = new()
        {
            constantCount = (nuint)(descriptor.Fragment.HasValue ? descriptor.Fragment.Value.Constants.Length : 0),
            constants = fragmentConstants
        };

        var colorStateCount = 0;
        if (descriptor.Fragment.HasValue)
        {
            colorStateCount = descriptor.Fragment.Value.Targets.Length;
        }

        WGPUBlendState* fragmentBlendState = stackalloc WGPUBlendState[descriptor.Fragment?.Targets.Length ?? 0];

        if (descriptor.Fragment.HasValue)
        {
            fragment.module = ToNative(descriptor.Fragment.Value.Module);
            fragment.entryPoint = fragmentEntryPoint.CharPointer;
            fragment.targetCount = (ulong)descriptor.Fragment.Value.Targets.Length;
            WGPUColorTargetState* targets = stackalloc WGPUColorTargetState[descriptor.Fragment.Value.Targets.Length];
            for (var i = 0; i < descriptor.Fragment.Value.Targets.Length; i++)
            {
                var c = descriptor.Fragment.Value.Targets.Span[i];
                targets[i] = ToNative(c);
                if (c.Blend.HasValue)
                {
                    fragmentBlendState[i] = ToNative(c.Blend.Value);
                    targets[i].blend = &(fragmentBlendState[i]);
                }
            }
            fragment.targets = targets;
        }

        desc.primitive = ToNative(descriptor.Primitive);
        desc.multisample = ToNative(descriptor.Multisample);
        if (descriptor.Fragment.HasValue)
        {
            desc.fragment = &fragment;
        }

        if (descriptor.Layout is not null)
        {
            desc.layout = ToNative(descriptor.Layout);
        }


        var vertexBuffer = stackalloc WGPUVertexBufferLayout[descriptor.Vertex.Buffers.Length];
        var attributesTotalCount = 0;
        {
            var index = 0;
            foreach (var buffer in descriptor.Vertex.Buffers.Span)
            {
                vertexBuffer[index] = new WGPUVertexBufferLayout
                {
                    arrayStride = buffer.ArrayStride,
                    attributeCount = (nuint)buffer.Attributes.Length,
                };
                attributesTotalCount += buffer.Attributes.Length;
                index++;
            }
        }
        //var attributes = stackalloc GPUVertexAttribute[attributesTotalCount];
        {
            var bufferIndex = 0;
            //var attributeIndex = 0;
            foreach (var buffer in descriptor.Vertex.Buffers.Span)
            {
                var pin = buffer.Attributes.Pin();
                disposables.Add(pin);
                vertexBuffer[bufferIndex].attributes = (GPUVertexAttribute*)pin.Pointer;

                //foreach (var attribute in buffer.Attributes.Span)
                //{
                //    attributes[attributeIndex] = attribute;
                //    Console.WriteLine(attributes[attributeIndex].Format);
                //    Console.WriteLine(attribute.Format);
                //    attributeIndex++;
                //}

                bufferIndex++;
            }
        }
        desc.vertex.buffers = descriptor.Vertex.Buffers.Length > 0 ? vertexBuffer : null;

        var result = wgpuDeviceCreateRenderPipeline(ToNative(handle.Handle), &desc);
        return new(new(result.Handle));


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

    private WGPUColorTargetState ToNative(GPUColorTargetState c)
    {
        return new()
        {
            format = ToNative(c.Format),
            writeMask = ToNative(c.WriteMask)
        };
    }

    private WGPUVertexState ToNative(GPUVertexState vertex)
    {
        if (vertex.Constants.Length > 0)
        {
            throw new NotImplementedException("Constant Entry is not support yet");
        }
        return new WGPUVertexState()
        {
            module = ToNative(vertex.Module),
        };
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

    ValueTask IBackend<Backend>.MapAsyncAsync(GPUBuffer<Backend> handle, GPUMapMode mode, ulong offset, ulong size, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    ReadOnlySpan<byte> IBackend<Backend>.GetMappedRange(GPUBuffer<Backend> handle, ulong offset, ulong size)
    {
        throw new NotImplementedException();
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
        WGPURenderPassDepthStencilAttachment depthStencilAttachment = default;
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


    GPUCommandBuffer<Backend> IBackend<Backend>.Finish(GPUCommandEncoder<Backend> handle, GPUCommandBufferDescriptor descriptor)
    {
        throw new NotImplementedException();
    }


    void IBackend<Backend>.SetBindGroup(GPUComputePassEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        throw new NotImplementedException();
    }

    unsafe void IBackend<Backend>.Submit(GPUQueue<Backend> handle, IReadOnlyList<GPUCommandBuffer<Backend>> commandBuffers)
    {
        var count = commandBuffers.Count;
        var cmds = stackalloc WGPUCommandBuffer[count];
        wgpuQueueSubmit(ToNative(handle.Handle), (uint)count, cmds);
    }

    unsafe void IBackend<Backend>.WriteBuffer(GPUQueue<Backend> handle, GPUBuffer<Backend> buffer, ulong bufferOffset, nint data, ulong dataOffset, ulong size)
    {
        wgpuQueueWriteBuffer(ToNative(handle.Handle), ToNative(buffer.Handle), bufferOffset, (void*)data, size);
    }

    void IBackend<Backend>.WriteTexture(GPUQueue<Backend> handle, GPUImageCopyTexture destination, nint data, GPUImageDataLayout dataLayout, GPUExtent3D size)
    {
        throw new NotImplementedException();
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


    void IBackend<Backend>.SetBindGroup(GPURenderPassEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    void IBackend<Backend>.SetVertexBuffer(GPURenderPassEncoder<Backend> handle, int slot, GPUBuffer<Backend>? buffer, ulong offset, ulong size)
    {
        throw new NotImplementedException();
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
