using WebGPU;

namespace DualDrill.Graphics.Backend;

using static WebGPU.WebGPU;
using Backend = WebGPUNETBackend;

public sealed partial class WebGPUNETBackend
{
    unsafe GPUComputePipeline<Backend> IBackend<Backend>.CreateComputePipeline(
        GPUDevice<Backend> device, GPUComputePipelineDescriptor descriptor)
    {
        RequireLive(device.Handle, nameof(device));
        var owner = StateOf(device);
        if (descriptor.Compute.Module is not GPUShaderModule<Backend> module)
            throw new ArgumentException("Compute shader module belongs to another backend or is missing.", nameof(descriptor));
        RequireDevice(module.Handle, owner, nameof(descriptor.Compute.Module));
        if (string.IsNullOrWhiteSpace(descriptor.Compute.EntryPoint))
            throw new ArgumentException("A compute entry point is required.", nameof(descriptor));
        if (descriptor.Compute.Constants is { Count: > 0 })
            throw new NotSupportedException("Compute pipeline constants are not supported.");
        WGPUPipelineLayout layout = default;
        if (descriptor.Layout is not null)
        {
            if (descriptor.Layout is not GPUPipelineLayout<Backend> typed)
                throw new ArgumentException("Pipeline layout belongs to another backend.", nameof(descriptor));
            RequireDevice(typed.Handle, owner, nameof(descriptor.Layout));
            layout = ToNative(typed.Handle);
        }

        using var label = NativeUtf8String.Create(descriptor.Label);
        using var entryPoint = NativeUtf8String.Create(descriptor.Compute.EntryPoint);
        var native = new WGPUComputePipelineDescriptor
        {
            label = label.View,
            layout = layout,
            compute = new()
            {
                module = ToNative(module.Handle),
                entryPoint = entryPoint.View,
            },
        };
        lock (owner.ValidationGate)
        {
            ThrowPendingDeviceError(owner);
            wgpuDevicePushErrorScope(owner.Device, WGPUErrorFilter.Validation);
            var result = wgpuDeviceCreateComputePipeline(owner.Device, &native);
            try
            {
                var error = PopErrorScope(owner);
                if (error.ErrorType != WGPUErrorType.NoError || result.IsNull)
                    throw new GraphicsApiException<Backend>(
                        $"WebGPU compute pipeline creation failed: {error.ErrorType}: {error.Message}");
                ThrowPendingDeviceError(owner);
                return new(new(result.Handle, owner));
            }
            catch
            {
                if (result.IsNotNull)
                    wgpuComputePipelineRelease(result);
                throw;
            }
        }
    }

    unsafe GPUComputePassEncoder<Backend> IBackend<Backend>.BeginComputePass(
        GPUCommandEncoder<Backend> encoder, GPUComputePassDescriptor descriptor)
    {
        var state = EncoderOf(encoder);
        if (state.ActiveComputePass || state.Abandoned || state.Finished)
            throw new InvalidOperationException("Command encoder has an active or abandoned compute pass, or has finished.");
        if (descriptor.TimestampWrites.QuerySet is not null
            || descriptor.TimestampWrites.BeginningOfPassWriteIndex != 0
            || descriptor.TimestampWrites.EndOfPassWriteIndex != 0)
            throw new NotSupportedException("Compute pass timestamp writes are not supported.");

        using var label = NativeUtf8String.Create(descriptor.Label);
        var native = new WGPUComputePassDescriptor { label = label.View };
        lock (state.Device.ValidationGate)
        {
            ThrowPendingDeviceError(state.Device);
            wgpuDevicePushErrorScope(state.Device.Device, WGPUErrorFilter.Validation);
            var result = wgpuCommandEncoderBeginComputePass(ToNative(encoder.Handle), &native);
            try
            {
                var error = PopErrorScope(state.Device);
                if (error.ErrorType != WGPUErrorType.NoError || result.IsNull)
                    throw new GraphicsApiException<Backend>(
                        $"WebGPU compute pass creation failed: {error.ErrorType}: {error.Message}");
                ThrowPendingDeviceError(state.Device);
                state.ActiveComputePass = true;
                state.UsedCompute = true;
                return new(new(result.Handle, new ComputePassState(encoder, state)));
            }
            catch
            {
                if (result.IsNotNull)
                    wgpuComputePassEncoderRelease(result);
                state.Abandoned = true;
                throw;
            }
        }
    }

    void IGPUHandleDisposer<Backend, GPUComputePassEncoder<Backend>>.DisposeHandle(
        GPUHandle<Backend, GPUComputePassEncoder<Backend>> handle)
    {
        if (handle.Data is ComputePassState state && !state.Ended)
        {
            state.Parent.ActiveComputePass = false;
            state.Parent.Abandoned = true;
        }
        wgpuComputePassEncoderRelease(ToNative(handle));
    }

    void IBackend<Backend>.SetPipeline(
        GPUComputePassEncoder<Backend> pass, GPUComputePipeline<Backend> pipeline)
    {
        var state = PassOf(pass);
        ArgumentNullException.ThrowIfNull(pipeline);
        RequireDevice(pipeline.Handle, state.Parent.Device, nameof(pipeline));
        wgpuComputePassEncoderSetPipeline(ToNative(pass.Handle), ToNative(pipeline.Handle));
    }

    unsafe void IBackend<Backend>.SetBindGroup(
        GPUComputePassEncoder<Backend> pass, int index,
        GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        var state = PassOf(pass);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (!dynamicOffsets.IsEmpty)
            throw new NotSupportedException("Compute bind group dynamic offsets are not supported.");
        if (bindGroup is not null)
            RequireDevice(bindGroup.Handle, state.Parent.Device, nameof(bindGroup));
        wgpuComputePassEncoderSetBindGroup(
            ToNative(pass.Handle), (uint)index,
            bindGroup is null ? WGPUBindGroup.Null : ToNative(bindGroup.Handle), 0, null);
    }

    void IBackend<Backend>.SetBindGroup(
        GPUComputePassEncoder<Backend> pass, int index, GPUBindGroup<Backend>? bindGroup,
        ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        _ = PassOf(pass);
        throw new NotSupportedException("Compute bind group dynamic offset ranges are not supported.");
    }

    void IBackend<Backend>.DispatchWorkgroups(
        GPUComputePassEncoder<Backend> pass, uint x, uint y, uint z)
    {
        _ = PassOf(pass);
        wgpuComputePassEncoderDispatchWorkgroups(ToNative(pass.Handle), x, y, z);
    }

    void IBackend<Backend>.DispatchWorkgroupsIndirect(
        GPUComputePassEncoder<Backend> pass, GPUBuffer<Backend> indirectBuffer, ulong indirectOffset)
    {
        var state = PassOf(pass);
        ArgumentNullException.ThrowIfNull(indirectBuffer);
        RequireDevice(indirectBuffer.Handle, state.Parent.Device, nameof(indirectBuffer));
        wgpuComputePassEncoderDispatchWorkgroupsIndirect(
            ToNative(pass.Handle), ToNative(indirectBuffer.Handle), indirectOffset);
    }

    void IBackend<Backend>.End(GPUComputePassEncoder<Backend> pass)
    {
        var state = PassOf(pass);
        wgpuComputePassEncoderEnd(ToNative(pass.Handle));
        state.Ended = true;
        state.Parent.ActiveComputePass = false;
    }

    void IBackend<Backend>.InsertDebugMarker(GPUComputePassEncoder<Backend> pass, string markerLabel)
    {
        _ = PassOf(pass);
        wgpuComputePassEncoderInsertDebugMarker(ToNative(pass.Handle), markerLabel);
    }

    void IBackend<Backend>.PushDebugGroup(GPUComputePassEncoder<Backend> pass, string groupLabel)
    {
        _ = PassOf(pass);
        wgpuComputePassEncoderPushDebugGroup(ToNative(pass.Handle), groupLabel);
    }

    void IBackend<Backend>.PopDebugGroup(GPUComputePassEncoder<Backend> pass)
    {
        _ = PassOf(pass);
        wgpuComputePassEncoderPopDebugGroup(ToNative(pass.Handle));
    }

    unsafe GPUBindGroupLayout<Backend> IBackend<Backend>.GetBindGroupLayout(
        GPUComputePipeline<Backend> pipeline, ulong index)
    {
        RequireLive(pipeline.Handle, nameof(pipeline));
        var owner = pipeline.Handle.Data as DeviceState
            ?? throw new GraphicsApiException<Backend>("Compute pipeline has no native device.");
        if (owner.IsDisposed)
            throw new ObjectDisposedException("GPU device");
        var nativeIndex = checked((uint)index);
        lock (owner.ValidationGate)
        {
            ThrowPendingDeviceError(owner);
            wgpuDevicePushErrorScope(owner.Device, WGPUErrorFilter.Validation);
            var result = wgpuComputePipelineGetBindGroupLayout(ToNative(pipeline.Handle), nativeIndex);
            try
            {
                var error = PopErrorScope(owner);
                if (error.ErrorType != WGPUErrorType.NoError || result.IsNull)
                    throw new GraphicsApiException<Backend>(
                        $"WebGPU compute bind group layout lookup failed: {error.ErrorType}: {error.Message}");
                ThrowPendingDeviceError(owner);
                return new(new(result.Handle, owner));
            }
            catch
            {
                if (result.IsNotNull)
                    wgpuBindGroupLayoutRelease(result);
                throw;
            }
        }
    }
}
