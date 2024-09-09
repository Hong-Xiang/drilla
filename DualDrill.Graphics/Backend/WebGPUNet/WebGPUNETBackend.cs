using DualDrill.Interop;
using Evergine.Bindings.WebGPU;
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
        PopulateNative(ref desc.size, descriptor.Size);
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

    GPUBindGroupLayout<Backend> IBackend<Backend>.CreateBindGroupLayout(GPUDevice<Backend> handle, GPUBindGroupLayoutDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUPipelineLayout<Backend> IBackend<Backend>.CreatePipelineLayout(GPUDevice<Backend> handle, GPUPipelineLayoutDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUBindGroup<Backend> IBackend<Backend>.CreateBindGroup(GPUDevice<Backend> handle, GPUBindGroupDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUShaderModule<Backend> IBackend<Backend>.CreateShaderModule(GPUDevice<Backend> handle, GPUShaderModuleDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUComputePipeline<Backend> IBackend<Backend>.CreateComputePipeline(GPUDevice<Backend> handle, GPUComputePipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPURenderPipeline<Backend> IBackend<Backend>.CreateRenderPipeline(GPUDevice<Backend> handle, GPURenderPipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUComputePipeline<Backend>> IBackend<Backend>.CreateComputePipelineAsync(GPUDevice<Backend> handle, GPUComputePipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPURenderPipeline<Backend>> IBackend<Backend>.CreateRenderPipelineAsync(GPUDevice<Backend> handle, GPURenderPipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    GPUCommandEncoder<Backend> IBackend<Backend>.CreateCommandEncoder(GPUDevice<Backend> handle, GPUCommandEncoderDescriptor descriptor)
    {
        throw new NotImplementedException();
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


    GPURenderPassEncoder<Backend> IBackend<Backend>.BeginRenderPass(GPUCommandEncoder<Backend> handle, GPURenderPassDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUComputePassEncoder<Backend> IBackend<Backend>.BeginComputePass(GPUCommandEncoder<Backend> handle, GPUComputePassDescriptor descriptor)
    {
        throw new NotImplementedException();
    }


    void IBackend<Backend>.CopyBufferToTexture(GPUCommandEncoder<Backend> handle, GPUImageCopyBuffer source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.CopyTextureToBuffer(GPUCommandEncoder<Backend> handle, GPUImageCopyTexture source, GPUImageCopyBuffer destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
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

    void IBackend<Backend>.WriteTexture(GPUQueue<Backend> handle, GPUImageCopyTexture destination, nint data, GPUTextureDataLayout dataLayout, GPUExtent3D size)
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

    GPUBindGroupLayout<Backend> IBackend<Backend>.GetBindGroupLayout(GPURenderPipeline<Backend> handle, ulong index)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUCompilationInfo> IBackend<Backend>.GetCompilationInfoAsync(GPUShaderModule<Backend> handle, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    unsafe void IBackend<Backend>.Configure(GPUSurface<Backend> handle, GPUSurfaceConfiguration configuration)
    {
        fixed (GPUTextureFormat* viewFormats = configuration.ViewFormats)
        {
            var nativeConfig = new WGPUSurfaceConfiguration
            {
                device = ToNative(configuration.Device.Handle),
                format = ToNative(configuration.Format),
                usage = ToNative(configuration.Usage),
                viewFormatCount = (nuint)configuration.ViewFormats.Length,
                viewFormats = viewFormats,
                alphaMode = ToNative(configuration.AlphaMode),
                width = (uint)configuration.Width,
                height = (uint)configuration.Height,
                presentMode = ToNative(configuration.PresentMode)
            };
            wgpuSurfaceConfigure(ToNative(handle.Handle), &nativeConfig);
        }

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
