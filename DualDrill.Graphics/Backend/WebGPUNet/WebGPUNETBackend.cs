using WebGPU;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace DualDrill.Graphics.Backend;

using static WebGPU.WebGPU;
using Backend = DualDrill.Graphics.Backend.WebGPUNETBackend;
using Native = WebGPU;

internal readonly record struct WebGPUNETHandle<THandle, TResource>(
    THandle Handle
) : IGPUNativeHandle<Backend, TResource>
{
}

public sealed partial class WebGPUNETBackend : IBackend<Backend>
{
    private const uint ExpectedNativeVersion = 0x1B000400;
    private static int s_nativeVersionChecked;

    public static Backend Instance { get; } = new();

    private sealed unsafe class NativeUtf8String : IDisposable
    {
        private byte* _data;

        private NativeUtf8String(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                View = WGPUStringView.Empty;
                return;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            _data = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
            bytes.CopyTo(new Span<byte>(_data, bytes.Length));
            View = new WGPUStringView(_data, bytes.Length);
        }

        public WGPUStringView View { get; }

        public static NativeUtf8String Create(string? value) => new(value);

        public void Dispose()
        {
            NativeMemory.Free(_data);
            _data = null;
        }
    }

    private unsafe T* Alloc<T>(int count = 1) where T : unmanaged
    {
        return (T*)NativeMemory.AllocZeroed((nuint)count, (nuint)(sizeof(T)));
    }

    private sealed class InstanceState(WGPUInstance instance)
    {
        public WGPUInstance Instance { get; } = instance;
    }

    private sealed class AdapterState(InstanceState instance)
    {
        public InstanceState Instance { get; } = instance;
    }

    private sealed class DeviceState(InstanceState instance) : IDisposable
    {
        private readonly GraphicsApiException<Backend> _callbackFailure =
            new("Native WebGPU error callback failed before its diagnostic could be decoded.");
        private GCHandle _callbackHandle;
        private int _callbackFailed;

        public InstanceState Instance { get; } = instance;
        public WGPUDevice Device { get; set; }
        public ConcurrentQueue<GraphicsApiException<Backend>> Errors { get; } = new();
        public object ValidationGate { get; } = new();

        public unsafe void* RegisterCallbacks()
        {
            _callbackHandle = GCHandle.Alloc(this);
            return (void*)GCHandle.ToIntPtr(_callbackHandle);
        }

        public void Enqueue(WGPUErrorType type, WGPUStringView message)
        {
            try
            {
                Errors.Enqueue(new GraphicsApiException<Backend>(
                    $"Device Error {type}, Message: {message}"));
            }
            catch
            {
                Interlocked.Exchange(ref _callbackFailed, 1);
            }
        }

        public void EnqueueDeviceLost(WGPUDeviceLostReason reason, WGPUStringView message)
        {
            try
            {
                Errors.Enqueue(new GraphicsApiException<Backend>(
                    $"Device lost {reason}, Message: {message}"));
            }
            catch
            {
                Interlocked.Exchange(ref _callbackFailed, 1);
            }
        }

        public GraphicsApiException<Backend>? TakeError()
        {
            if (Errors.TryDequeue(out var error))
            {
                return error;
            }

            return Interlocked.Exchange(ref _callbackFailed, 0) == 0 ? null : _callbackFailure;
        }

        public void Dispose()
        {
            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }
        }
    }

    private sealed class AdapterRequestState
    {
        public WGPURequestAdapterStatus Status { get; set; } = WGPURequestAdapterStatus.Unknown;
        public WGPUAdapter Adapter { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool Completed { get; set; }
    }

    private sealed class DeviceRequestState
    {
        public WGPURequestDeviceStatus Status { get; set; } = WGPURequestDeviceStatus.Unknown;
        public WGPUDevice Device { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool Completed { get; set; }
    }

    private sealed class ErrorScopeState
    {
        public WGPUPopErrorScopeStatus Status { get; set; }
        public WGPUErrorType ErrorType { get; set; } = WGPUErrorType.Unknown;
        public string Message { get; set; } = string.Empty;
        public bool Completed { get; set; }
    }

    private sealed class MapState(
        WGPUBuffer buffer,
        CancellationToken cancellation)
    {
        private const int Pending = 0;
        private const int CancellationRequested = 1;
        private const int Completed = 2;
        private readonly object _gate = new();
        private int _phase;

        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RequestCancellation()
        {
            lock (_gate)
            {
                if (_phase != Pending)
                {
                    return;
                }

                _phase = CancellationRequested;
                wgpuBufferUnmap(buffer);
            }
        }

        public void Complete(WGPUMapAsyncStatus status, WGPUStringView message)
        {
            lock (_gate)
            {
                if (_phase == Completed)
                {
                    return;
                }

                if (_phase == CancellationRequested)
                {
                    _phase = Completed;
                    Completion.TrySetCanceled(cancellation);
                }
                else if (status == WGPUMapAsyncStatus.Success)
                {
                    _phase = Completed;
                    Completion.TrySetResult();
                }
                else
                {
                    try
                    {
                        var text = message.ToString();
                        _phase = Completed;
                        Completion.TrySetException(new GraphicsApiException<Backend>(
                            $"Map buffer failed {status}: {text}"));
                    }
                    catch (Exception error)
                    {
                        _phase = Completed;
                        Completion.TrySetException(error);
                    }
                }
            }
        }

        public void CompleteException(Exception error)
        {
            lock (_gate)
            {
                if (_phase == Completed)
                {
                    return;
                }

                var cancellationWon = _phase == CancellationRequested;
                _phase = Completed;
                if (cancellationWon)
                {
                    Completion.TrySetCanceled(cancellation);
                }
                else
                {
                    Completion.TrySetException(error);
                }
            }
        }
    }

    private sealed class QueueWorkState : IDisposable
    {
        private GCHandle _callbackHandle;

        public TaskCompletionSource<WGPUQueueWorkDoneStatus> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public unsafe void* RegisterCallback()
        {
            _callbackHandle = GCHandle.Alloc(this);
            return (void*)GCHandle.ToIntPtr(_callbackHandle);
        }

        public void Complete(WGPUQueueWorkDoneStatus status)
        {
            try
            {
                Completion.TrySetResult(status);
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }
        }
    }

    private static InstanceState StateOf(GPUInstance<Backend> instance)
        => (InstanceState)(instance.Handle.Data
            ?? throw new GraphicsApiException<Backend>("GPU instance has no native state."));

    private static InstanceState StateOf(GPUAdapter<Backend> adapter)
        => ((AdapterState)(adapter.Handle.Data
            ?? throw new GraphicsApiException<Backend>("GPU adapter has no native state."))).Instance;

    private static DeviceState StateOf(GPUDevice<Backend> device)
        => (DeviceState)(device.Handle.Data
            ?? throw new GraphicsApiException<Backend>("GPU device has no native state."));

    private static DeviceState StateOf(GPUBuffer<Backend> buffer)
        => (DeviceState)(buffer.Handle.Data
            ?? throw new GraphicsApiException<Backend>("GPU buffer has no native state."));

    private static DeviceState StateOf(GPUQueue<Backend> queue)
        => (DeviceState)(queue.Handle.Data
            ?? throw new GraphicsApiException<Backend>("GPU queue has no native state."));

    private static void EnsureNativeVersion()
    {
        if (Volatile.Read(ref s_nativeVersionChecked) != 0)
        {
            return;
        }

        var actual = wgpuGetVersion();
        if (actual != ExpectedNativeVersion)
        {
            throw new GraphicsApiException<Backend>(
                $"Expected wgpu-native 27.0.4.0 (0x{ExpectedNativeVersion:X8}), loaded 0x{actual:X8}.");
        }

        Volatile.Write(ref s_nativeVersionChecked, 1);
    }

    internal static TNative MapEnumByName<TManaged, TNative>(TManaged value)
        where TManaged : struct, Enum
        where TNative : struct, Enum
    {
        var managedType = typeof(TManaged);
        if (managedType.GetCustomAttributes(typeof(FlagsAttribute), false).Length == 0)
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown {managedType.Name} value.");
            }
        }
        else
        {
            ulong knownBits = 0;
            foreach (var member in Enum.GetValues<TManaged>())
            {
                knownBits |= Convert.ToUInt64(member);
            }

            var actualBits = Convert.ToUInt64(value);
            if ((actualBits & ~knownBits) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"Unknown {managedType.Name} flag bits 0x{actualBits & ~knownBits:X}.");
            }
        }

        var name = value.ToString();
        if (!Enum.TryParse<TNative>(name, false, out var native))
        {
            throw new NotSupportedException(
                $"{managedType.Name}.{name} has no semantic {typeof(TNative).Name} mapping.");
        }

        return native;
    }

    private static WGPUBackendType ToNative(GPUBackendType value)
        => MapEnumByName<GPUBackendType, WGPUBackendType>(value);

    private static WGPUOptionalBool ToNativeOptional(bool value)
        => value ? WGPUOptionalBool.True : WGPUOptionalBool.False;

    internal static void ThrowIfNativeFailed(WGPUStatus status, string operation)
    {
        if (status != WGPUStatus.Success)
        {
            throw new GraphicsApiException<Backend>($"{operation} failed: {status}.");
        }
    }

    internal static bool IsHardwareAdapter(WGPUAdapterType adapterType)
        => adapterType is WGPUAdapterType.DiscreteGPU or WGPUAdapterType.IntegratedGPU;

    public unsafe GPUInstance<Backend> CreateGPUInstance()
    {
        EnsureNativeVersion();
        WGPUInstanceDescriptor descriptor = new();
        var nativeInstance = wgpuCreateInstance(&descriptor);
        if (nativeInstance.IsNull)
        {
            throw new GraphicsApiException<Backend>("wgpuCreateInstance returned a null instance.");
        }

        var state = new InstanceState(nativeInstance);
        return new GPUInstance<Backend>(new(nativeInstance.Handle, state));
    }

    unsafe ValueTask<GPUAdapter<Backend>> IBackend<Backend>.RequestAdapterAsync(
        GPUInstance<Backend> instance,
        GPURequestAdapterOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var instanceState = StateOf(instance);
        var request = new AdapterRequestState();
        var requestHandle = GCHandle.Alloc(request);
        try
        {
            WGPURequestAdapterOptions nativeOptions = new()
            {
                featureLevel = WGPUFeatureLevel.Core,
                powerPreference = ToNative(options.PowerPreference),
                forceFallbackAdapter = options.ForceFallbackAdapter,
                backendType = ToNative(options.BackendType),
                compatibleSurface = options.CompatibleSurface switch
                {
                    null => WGPUSurface.Null,
                    GPUSurface<Backend> surface => ToNative(surface.Handle),
                    _ => throw new NotSupportedException(
                        "Only native WebGPU surfaces can constrain adapter selection."),
                },
            };
            WGPURequestAdapterCallbackInfo callback = new()
            {
                mode = WGPUCallbackMode.AllowSpontaneous,
                callback = &AdapterRequested,
                userdata1 = (void*)GCHandle.ToIntPtr(requestHandle),
            };
            _ = wgpuInstanceRequestAdapter(instanceState.Instance, &nativeOptions, callback);
            if (!request.Completed)
            {
                throw new GraphicsApiException<Backend>(
                    "wgpu-native did not complete the adapter request synchronously.");
            }

            if (request.Status != WGPURequestAdapterStatus.Success || request.Adapter.IsNull)
            {
                throw new GraphicsApiException<Backend>(
                    $"Could not get WebGPU adapter: {request.Status}: {request.Message}");
            }

            var infoStatus = wgpuAdapterGetInfo(request.Adapter, out var info);
            if (infoStatus != WGPUStatus.Success)
            {
                wgpuAdapterRelease(request.Adapter);
                throw new GraphicsApiException<Backend>(
                    $"Could not inspect WebGPU adapter: {infoStatus}.");
            }

            try
            {
                if (!options.ForceFallbackAdapter && !IsHardwareAdapter(info.adapterType))
                {
                    wgpuAdapterRelease(request.Adapter);
                    throw new GraphicsApiException<Backend>(
                        $"Rejected non-hardware WebGPU adapter '{info.device}' classified as {info.adapterType}.");
                }

                if (options.BackendType != GPUBackendType.Undefined
                    && info.backendType != ToNative(options.BackendType))
                {
                    wgpuAdapterRelease(request.Adapter);
                    throw new GraphicsApiException<Backend>(
                        $"Requested {options.BackendType}, received {info.backendType}.");
                }
            }
            finally
            {
                wgpuAdapterInfoFreeMembers(info);
            }

            return ValueTask.FromResult(
                new GPUAdapter<Backend>(
                    new(request.Adapter.Handle, new AdapterState(instanceState))));
        }
        finally
        {
            requestHandle.Free();
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void AdapterRequested(
        WGPURequestAdapterStatus status,
        WGPUAdapter adapter,
        WGPUStringView message,
        void* userdata1,
        void* userdata2)
    {
        AdapterRequestState? request = null;
        try
        {
            request = (AdapterRequestState?)GCHandle.FromIntPtr((nint)userdata1).Target;
            if (request is null)
            {
                return;
            }

            request.Status = status;
            request.Adapter = adapter;
            request.Message = message.ToString();
            request.Completed = true;
        }
        catch
        {
            if (request is not null)
            {
                request.Message = "Native adapter callback failed.";
                request.Completed = true;
            }
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void DeviceRequested(
        WGPURequestDeviceStatus status,
        WGPUDevice device,
        WGPUStringView message,
        void* userdata1,
        void* userdata2)
    {
        DeviceRequestState? request = null;
        try
        {
            request = (DeviceRequestState?)GCHandle.FromIntPtr((nint)userdata1).Target;
            if (request is null)
            {
                return;
            }

            request.Status = status;
            request.Device = device;
            request.Message = message.ToString();
            request.Completed = true;
        }
        catch
        {
            if (request is not null)
            {
                request.Message = "Native device callback failed.";
                request.Completed = true;
            }
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void DeviceLost(
        WGPUDevice* device,
        WGPUDeviceLostReason reason,
        WGPUStringView message,
        void* userdata1,
        void* userdata2)
    {
        try
        {
            var state = (DeviceState?)GCHandle.FromIntPtr((nint)userdata1).Target;
            state?.EnqueueDeviceLost(reason, message);
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void UncapturedError(
        WGPUDevice* device,
        WGPUErrorType type,
        WGPUStringView message,
        void* userdata1,
        void* userdata2)
    {
        try
        {
            var state = (DeviceState?)GCHandle.FromIntPtr((nint)userdata1).Target;
            state?.Enqueue(type, message);
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void ErrorScopePopped(
        WGPUPopErrorScopeStatus status,
        WGPUErrorType type,
        WGPUStringView message,
        void* userdata1,
        void* userdata2)
    {
        ErrorScopeState? state = null;
        try
        {
            state = (ErrorScopeState?)GCHandle.FromIntPtr((nint)userdata1).Target;
            if (state is null)
            {
                return;
            }

            state.Status = status;
            state.ErrorType = type;
            state.Message = message.ToString();
            state.Completed = true;
        }
        catch
        {
            if (state is not null)
            {
                state.Message = "Native error-scope callback failed.";
                state.Completed = true;
            }
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void BufferMapped(
        WGPUMapAsyncStatus status,
        WGPUStringView message,
        void* userdata1,
        void* userdata2)
    {
        MapState? state = null;
        try
        {
            state = (MapState?)GCHandle.FromIntPtr((nint)userdata1).Target;
            state?.Complete(status, message);
        }
        catch (Exception error)
        {
            state?.CompleteException(error);
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void QueueWorkDone(
        WGPUQueueWorkDoneStatus status,
        void* userdata1,
        void* userdata2)
    {
        try
        {
            var state = (QueueWorkState?)GCHandle.FromIntPtr((nint)userdata1).Target;
            state?.Complete(status);
        }
        catch
        {
        }
    }

    private static void ThrowPendingDeviceError(DeviceState state)
    {
        if (state.TakeError() is { } error)
        {
            throw error;
        }
    }

    private static unsafe void PumpNative(DeviceState state)
    {
        wgpuInstanceProcessEvents(state.Instance.Instance);
        _ = wgpuDevicePoll(state.Device, false, null);
    }

    private static void PollNative(DeviceState state)
    {
        PumpNative(state);
        ThrowPendingDeviceError(state);
    }

    private static ErrorScopeState PopErrorScope(DeviceState deviceState)
    {
        var result = new ErrorScopeState();
        var resultHandle = GCHandle.Alloc(result);
        try
        {
            unsafe
            {
                WGPUPopErrorScopeCallbackInfo callback = new()
                {
                    mode = WGPUCallbackMode.AllowSpontaneous,
                    callback = &ErrorScopePopped,
                    userdata1 = (void*)GCHandle.ToIntPtr(resultHandle),
                };
                _ = wgpuDevicePopErrorScope(deviceState.Device, callback);
            }

            while (!result.Completed)
            {
                PumpNative(deviceState);
                Thread.Sleep(1);
            }

            if (result.Status != WGPUPopErrorScopeStatus.Success)
            {
                throw new GraphicsApiException<Backend>(
                    $"WebGPU error scope failed: {result.Status}: {result.Message}");
            }

            return result;
        }
        finally
        {
            resultHandle.Free();
        }
    }

    unsafe ValueTask<GPUDevice<Backend>> IBackend<Backend>.RequestDeviceAsync(
        GPUAdapter<Backend> adapter,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var deviceState = new DeviceState(StateOf(adapter));
        var persistentUserdata = deviceState.RegisterCallbacks();
        var request = new DeviceRequestState();
        var requestHandle = GCHandle.Alloc(request);
        try
        {
            if (descriptor.RequiredLimits is { Count: > 0 })
            {
                throw new NotSupportedException(
                    "Named GPU required limits are not supported by the native backend.");
            }

            using var label = NativeUtf8String.Create(descriptor.Label);
            using var queueLabel = NativeUtf8String.Create(descriptor.DefaultQueue.Label);
            var requiredFeatures = stackalloc WGPUFeatureName[descriptor.RequiredFeatures.Length];
            for (var index = 0; index < descriptor.RequiredFeatures.Length; index++)
            {
                requiredFeatures[index] = ToNative(descriptor.RequiredFeatures.Span[index]);
            }

            WGPUDeviceDescriptor nativeDescriptor = new()
            {
                label = label.View,
                requiredFeatureCount = (nuint)descriptor.RequiredFeatures.Length,
                requiredFeatures = requiredFeatures,
                defaultQueue = new()
                {
                    label = queueLabel.View,
                },
                deviceLostCallbackInfo = new()
                {
                    mode = WGPUCallbackMode.AllowSpontaneous,
                    callback = &DeviceLost,
                    userdata1 = persistentUserdata,
                },
                uncapturedErrorCallbackInfo = new()
                {
                    callback = &UncapturedError,
                    userdata1 = persistentUserdata,
                },
            };
            WGPURequestDeviceCallbackInfo callback = new()
            {
                mode = WGPUCallbackMode.AllowSpontaneous,
                callback = &DeviceRequested,
                userdata1 = (void*)GCHandle.ToIntPtr(requestHandle),
            };
            _ = wgpuAdapterRequestDevice(ToNative(adapter.Handle), &nativeDescriptor, callback);
            if (!request.Completed)
            {
                throw new GraphicsApiException<Backend>(
                    "wgpu-native did not complete the device request synchronously.");
            }

            if (request.Status != WGPURequestDeviceStatus.Success || request.Device.IsNull)
            {
                if (request.Device.IsNotNull)
                {
                    wgpuDeviceRelease(request.Device);
                }
                throw new GraphicsApiException<Backend>(
                    $"Could not get WebGPU device: {request.Status}: {request.Message}");
            }

            deviceState.Device = request.Device;
            var nativeQueue = wgpuDeviceGetQueue(request.Device);
            if (nativeQueue.IsNull)
            {
                wgpuDeviceRelease(request.Device);
                throw new GraphicsApiException<Backend>("wgpuDeviceGetQueue returned null.");
            }

            var queue = new GPUQueue<Backend>(new(nativeQueue.Handle, deviceState));
            return ValueTask.FromResult(
                new GPUDevice<Backend>(new(request.Device.Handle, deviceState)) { Queue = queue });
        }
        catch
        {
            deviceState.Dispose();
            throw;
        }
        finally
        {
            requestHandle.Free();
        }
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
        if (handle.IsNull)
        {
            throw new GraphicsApiException<Backend>("wgpuDeviceCreateBuffer returned null.");
        }
        return new(new(handle.Handle, StateOf(device)))
        {
            Length = alignedSize
        };
    }

    GPUTextureView<Backend> IBackend<Backend>.CreateTextureView(GPUTexture<Backend> texture, GPUTextureViewDescriptor descriptor)
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
        using var label = NativeUtf8String.Create(descriptor.Label);
        WGPUTextureDescriptor desc = new();
        desc.usage = ToNative(descriptor.Usage);
        desc.mipLevelCount = (uint)descriptor.MipLevelCount;
        desc.sampleCount = (uint)descriptor.SampleCount;
        desc.label = label.View;
        desc.dimension = ToNative(descriptor.Dimension);
        desc.size = ToNative(descriptor.Size);
        desc.size.depthOrArrayLayers = (uint)descriptor.Size.DepthOrArrayLayers;
        desc.format = ToNative(descriptor.Format);
        desc.viewFormatCount = (nuint)descriptor.ViewFormats.Length;
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
        if (h.IsNull)
        {
            throw new GraphicsApiException<Backend>("wgpuDeviceCreateTexture returned null.");
        }
        return new GPUTexture<Backend>(new GPUHandle<Backend, GPUTexture<Backend>>(h.Handle))
        {
            Label = descriptor.Label,
            Width = checked((int)descriptor.Size.Width),
            Height = checked((int)descriptor.Size.Height),
            DepthOrArrayLayers = checked((int)descriptor.Size.DepthOrArrayLayers),
            MipLevelCount = descriptor.MipLevelCount,
            SampleCount = descriptor.SampleCount,
            Dimension = descriptor.Dimension,
            Format = descriptor.Format,
            Usage = descriptor.Usage,
        };
    }

    void PopulateNative(ref WGPUExtent3D native, GPUExtent3D value)
    {
        native.height = (uint)value.Height;
        native.width = (uint)value.Width;
        native.depthOrArrayLayers = (uint)value.DepthOrArrayLayers;
    }


    unsafe GPUSampler<Backend> IBackend<Backend>.CreateSampler(GPUDevice<Backend> handle, GPUSamplerDescriptor descriptor)
    {
        using var label = NativeUtf8String.Create(descriptor.Label);
        var nativeDescriptor = ToNative(descriptor, label.View);
        var result = wgpuDeviceCreateSampler(ToNative(handle.Handle), &nativeDescriptor);
        return new GPUSampler<Backend>(new(result.Handle));
    }

    private WGPUSamplerDescriptor ToNative(
        GPUSamplerDescriptor descriptor,
        WGPUStringView label)
    {
        WGPUSamplerDescriptor nativeDescriptor = new()
        {
            label = label,
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
        using var label = NativeUtf8String.Create(descriptor.Label);
        var entries = stackalloc WGPUBindGroupLayoutEntry[descriptor.Entries.Length];
        var index = 0;
        foreach (var entry in descriptor.Entries.Span)
        {
            entries[index] = ToNative(entry);


            index++;
        }
        var nativeDescriptor = new WGPUBindGroupLayoutDescriptor
        {
            label = label.View,
            entryCount = (nuint)descriptor.Entries.Length,
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
        using var label = NativeUtf8String.Create(descriptor.Label);
        var bindGroupLayouts = stackalloc WGPUBindGroupLayout[descriptor.BindGroupLayouts.Count];
        var native = new WGPUPipelineLayoutDescriptor
        {
            label = label.View,
            bindGroupLayoutCount = (nuint)descriptor.BindGroupLayouts.Count,
            bindGroupLayouts = bindGroupLayouts
        };
        var index = 0;
        foreach (var bindGroupLayout in descriptor.BindGroupLayouts)
        {
            bindGroupLayouts[index] = ToNative(bindGroupLayout);
            index++;
        }

        return new(new(wgpuDeviceCreatePipelineLayout(ToNative(handle.Handle), &native).Handle));
    }

    WGPUBindGroupEntry ToNative(GPUBindGroupEntry value)
    {
        var result = new WGPUBindGroupEntry()
        {
            binding = (uint)value.Binding,
            offset = value.Offset,
            size = value.Size == 0 ? WGPU_WHOLE_SIZE : value.Size,
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
        using var label = NativeUtf8String.Create(descriptor.Label);
        for (var i = 0; i < descriptor.Entries.Length; i++)
        {
            entries[i] = ToNative(descriptor.Entries.Span[i]);
        }

        WGPUBindGroupDescriptor nativeDescriptor = new()
        {
            label = label.View,
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
        var nativeDevice = ToNative(handle.Handle);
        var deviceState = StateOf(handle);
        lock (deviceState.ValidationGate)
        {
            ThrowPendingDeviceError(deviceState);
            using var code = NativeUtf8String.Create(descriptor.Code);
            using var label = NativeUtf8String.Create(descriptor.Label);
            var source = new WGPUShaderSourceWGSL
            {
                code = code.View,
                chain = new WGPUChainedStruct
                {
                    sType = Native.WGPUSType.ShaderSourceWGSL,
                },
            };
            var nativeDescriptor = new WGPUShaderModuleDescriptor
            {
                nextInChain = &source.chain,
                label = label.View,
            };

            wgpuDevicePushErrorScope(nativeDevice, WGPUErrorFilter.Validation);
            var result = wgpuDeviceCreateShaderModule(nativeDevice, &nativeDescriptor);
            try
            {
                var error = PopErrorScope(deviceState);
                if (error.ErrorType != WGPUErrorType.NoError || result.IsNull)
                {
                    throw new GraphicsApiException<Backend>(
                        $"WebGPU shader module creation failed: {error.ErrorType}: {error.Message}");
                }
                ThrowPendingDeviceError(deviceState);
            }
            catch
            {
                if (result.IsNotNull)
                {
                    wgpuShaderModuleRelease(result);
                }
                throw;
            }

            return new(new(result.Handle));
        }
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
            using var pipelineLabel = NativeUtf8String.Create(descriptor.Label);

            desc.label = pipelineLabel.View;
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

            var deviceState = StateOf(handle);
            lock (deviceState.ValidationGate)
            {
                ThrowPendingDeviceError(deviceState);
                wgpuDevicePushErrorScope(deviceState.Device, WGPUErrorFilter.Validation);
                var result = wgpuDeviceCreateRenderPipeline(ToNative(handle.Handle), &desc);
                try
                {
                    var error = PopErrorScope(deviceState);
                    if (error.ErrorType != WGPUErrorType.NoError || result.IsNull)
                    {
                        throw new GraphicsApiException<Backend>(
                            $"WebGPU render pipeline creation failed: {error.ErrorType}: {error.Message}");
                    }
                    ThrowPendingDeviceError(deviceState);
                }
                catch
                {
                    if (result.IsNotNull)
                    {
                        wgpuRenderPipelineRelease(result);
                    }
                    throw;
                }

                return new(new(result.Handle));
            }
        }
        finally
        {
            if (desc.fragment is not null)
            {
                Free(*desc.fragment);
                NativeMemory.Free(desc.fragment);
            }
            NativeMemory.Free(desc.depthStencil);
            Free(desc.vertex);
        }
    }

    unsafe private WGPUFragmentState ToNative(GPUFragmentState fragment)
    {
        if (fragment.Constants.Length > 0)
        {
            throw new NotSupportedException("Pipeline constants are not supported.");
        }

        var result = new WGPUFragmentState
        {
            module = ToNative(fragment.Module),
            constantCount = (nuint)fragment.Constants.Length,
            targetCount = (nuint)fragment.Targets.Length
        };
        if (fragment.EntryPoint is not null)
        {
            result.entryPoint = MarshalString(fragment.EntryPoint);
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
            depthWriteEnabled = ToNativeOptional(depthStencil.DepthWriteEnabled),
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
        NativeMemory.Free(value.entryPoint.data);
        if (value.targets is not null)
        {
            for (nuint index = 0; index < value.targetCount; index++)
            {
                NativeMemory.Free(value.targets[index].blend);
            }
            NativeMemory.Free(value.targets);
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
            result.entryPoint = MarshalString(vertex.EntryPoint);
        }
        if (vertex.Buffers.Length > 0)
        {
            result.buffers = Alloc<WGPUVertexBufferLayout>(vertex.Buffers.Length);
            result.bufferCount = (nuint)vertex.Buffers.Length;
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
            attributeCount = (nuint)value.Attributes.Length
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

    unsafe private WGPUStringView MarshalString(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var buffer = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
        bytes.CopyTo(new Span<byte>(buffer, bytes.Length));
        return new WGPUStringView(buffer, bytes.Length);
    }

    unsafe void Free(WGPUVertexState value)
    {
        NativeMemory.Free(value.entryPoint.data);
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
        NativeMemory.Free(value.attributes);
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
        using var label = NativeUtf8String.Create(descriptor.Label);

        WGPUCommandEncoderDescriptor nativeDescriptor = new()
        {
            label = label.View
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


    async ValueTask IBackend<Backend>.MapAsync(GPUBuffer<Backend> handle, GPUMapMode mode, ulong offset, ulong size, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var state = new MapState(ToNative(handle.Handle), cancellation);
        var stateHandle = GCHandle.Alloc(state);
        try
        {
            unsafe
            {
                WGPUBufferMapCallbackInfo callback = new()
                {
                    mode = WGPUCallbackMode.AllowSpontaneous,
                    callback = &BufferMapped,
                    userdata1 = (void*)GCHandle.ToIntPtr(stateHandle),
                };
                _ = wgpuBufferMapAsync(
                    ToNative(handle.Handle),
                    ToNative(mode),
                    checked((nuint)offset),
                    checked((nuint)size),
                    callback);
            }

            using var cancellationRegistration = cancellation.Register(
                static state => ((MapState)state!).RequestCancellation(),
                state);
            await state.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            stateHandle.Free();
        }
    }

    unsafe Span<byte> IBackend<Backend>.GetMappedRange(GPUBuffer<Backend> handle, ulong offset, ulong size)
    {
        var ptr = wgpuBufferGetMappedRange(
            ToNative(handle.Handle),
            checked((nuint)offset),
            checked((nuint)size));
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
            depthSlice = WGPU_DEPTH_SLICE_UNDEFINED,
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
        unsafe
        {
            var nativeSource = ToNative(source);
            var nativeDestination = ToNative(destination);
            var nativeCopySize = ToNative(copySize);
            wgpuCommandEncoderCopyBufferToTexture(
                ToNative(handle.Handle),
                &nativeSource,
                &nativeDestination,
                &nativeCopySize);
        }
    }

    WGPUTexture ToNative(IGPUTexture value)
    {
        return ToNative(((GPUTexture<Backend>)value).Handle);
    }

    WGPUTexelCopyTextureInfo ToNative(GPUImageCopyTexture value)
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

    WGPUTexelCopyBufferLayout ToNative(GPUImageDataLayout value)
    {
        return new()
        {
            offset = value.Offset,
            bytesPerRow = value.BytesPerRow == 0
                ? WGPU_COPY_STRIDE_UNDEFINED
                : value.BytesPerRow,
            rowsPerImage = value.RowsPerImage == 0
                ? WGPU_COPY_STRIDE_UNDEFINED
                : value.RowsPerImage,
        };
    }

    WGPUTexelCopyBufferInfo ToNative(GPUImageCopyBuffer value)
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
        WGPUTexelCopyTextureInfo nativeSource = ToNative(source);
        WGPUTexelCopyBufferInfo nativeDestination = ToNative(destination);
        WGPUExtent3D nativeCopySize = ToNative(copySize);
        wgpuCommandEncoderCopyTextureToBuffer(ToNative(handle.Handle), &nativeSource, &nativeDestination, &nativeCopySize);
    }

    void IBackend<Backend>.CopyTextureToTexture(GPUCommandEncoder<Backend> handle, GPUImageCopyTexture source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }


    unsafe GPUCommandBuffer<Backend> IBackend<Backend>.Finish(GPUCommandEncoder<Backend> handle, GPUCommandBufferDescriptor descriptor)
    {
        using var label = NativeUtf8String.Create(descriptor.Label);
        WGPUCommandBufferDescriptor d = new()
        {
            label = label.View
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
        wgpuQueueSubmit(ToNative(handle.Handle), (nuint)count, cmds);
    }


    unsafe void IBackend<Backend>.WriteBuffer(GPUQueue<Backend> handle, GPUBuffer<Backend> buffer, ulong bufferOffset, nint data, ulong dataOffset, ulong size)
    {
        wgpuQueueWriteBuffer(
            ToNative(handle.Handle),
            ToNative(buffer.Handle),
            bufferOffset,
            (void*)data,
            checked((nuint)size));
    }

    unsafe void IBackend<Backend>.WriteTexture(GPUQueue<Backend> handle, GPUImageCopyTexture destination, ReadOnlySpan<byte> data, GPUImageDataLayout dataLayout, GPUExtent3D size)
    {
        var nativeDestination = new WGPUTexelCopyTextureInfo
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
        wgpuRenderPassEncoderSetBindGroup(
            ToNative(handle.Handle),
            (uint)index,
            bindGroup is not null ? ToNative(bindGroup.Handle) : WGPUBindGroup.Null,
            (nuint)dynamicOffsets.Length,
            ptr);
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
        return MapEnumByName<GPUPresentMode, WGPUPresentMode>(mode);
    }

    WGPUCompositeAlphaMode ToNative(GPUCompositeAlphaMode alphaMode)
    {
        return MapEnumByName<GPUCompositeAlphaMode, WGPUCompositeAlphaMode>(alphaMode);
    }

    unsafe GPUTexture<Backend> IBackend<Backend>.GetCurrentTexture(GPUSurface<Backend> handle)
    {
        WGPUSurfaceTexture result = new();
        wgpuSurfaceGetCurrentTexture(ToNative(handle.Handle), &result);
        if (result.status is not (
            WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal
            or WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal))
        {
            throw new GraphicsApiException<Backend>($"Failed to get current texture, status {Enum.GetName(result.status)}");
        }
        return new GPUTexture<Backend>(new(result.texture.Handle));
    }

    WGPUTextureViewDescriptor ToNative(
        GPUTextureViewDescriptor descriptor,
        WGPUStringView label)
    {
        var result = new WGPUTextureViewDescriptor
        {
            label = label,
        };
        result.format = ToNative(descriptor.Format);
        result.dimension = ToNative(descriptor.Dimension);
        result.baseMipLevel = (uint)descriptor.BaseMipLevel;
        result.mipLevelCount = descriptor.MipLevelCount == 0
            ? WGPU_MIP_LEVEL_COUNT_UNDEFINED
            : (uint)descriptor.MipLevelCount;
        result.baseArrayLayer = (uint)descriptor.BaseArrayLayer;
        result.arrayLayerCount = descriptor.ArrayLayerCount == 0
            ? WGPU_ARRAY_LAYER_COUNT_UNDEFINED
            : (uint)descriptor.ArrayLayerCount;
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
            using var label = NativeUtf8String.Create(descriptor.Value.Label);
            var nativeDescriptor = ToNative(descriptor.Value, label.View);
            resultHandle = wgpuTextureCreateView(ToNative(handle.Handle), &nativeDescriptor);
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

    async ValueTask IBackend<Backend>.OnSubmittedWorkDoneAsync(
        GPUQueue<Backend> handle,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var state = new QueueWorkState();
        var submitted = false;
        try
        {
            unsafe
            {
                WGPUQueueWorkDoneCallbackInfo callback = new()
                {
                    mode = WGPUCallbackMode.AllowSpontaneous,
                    callback = &QueueWorkDone,
                    userdata1 = state.RegisterCallback(),
                };
                _ = wgpuQueueOnSubmittedWorkDone(ToNative(handle.Handle), callback);
                submitted = true;
            }

            var status = await state.Completion.Task.WaitAsync(cancellation).ConfigureAwait(false);
            if (status != WGPUQueueWorkDoneStatus.Success)
            {
                throw new GraphicsApiException<Backend>(
                    $"WebGPU queue work failed: {status}.");
            }
        }
        catch
        {
            if (!submitted)
            {
                state.Dispose();
            }
            throw;
        }
    }

    unsafe void IBackend<Backend>.Poll(GPUDevice<Backend> device)
    {
        PollNative(StateOf(device));
    }

    async ValueTask IBackend<Backend>.PollAsync(GPUDevice<Backend> device, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        PollNative(StateOf(device));
        await Task.CompletedTask;
    }

    ValueTask<GPUAdapterInfo> IBackend<Backend>.RequestAdapterInfoAsync(GPUAdapter<Backend> adapter, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var status = wgpuAdapterGetInfo(ToNative(adapter.Handle), out var info);
        if (status != WGPUStatus.Success)
        {
            throw new GraphicsApiException<Backend>(
                $"Could not inspect WebGPU adapter: {status}.");
        }

        try
        {
            return ValueTask.FromResult(new GPUAdapterInfo(
                info.vendor.ToString(),
                info.architecture.ToString(),
                info.device.ToString(),
                info.description.ToString())
            {
                BackendType = MapEnumByName<WGPUBackendType, GPUBackendType>(info.backendType),
                AdapterType = MapEnumByName<WGPUAdapterType, GPUAdapterType>(info.adapterType),
            });
        }
        finally
        {
            wgpuAdapterInfoFreeMembers(info);
        }
    }

    void IBackend<Backend>.Present(GPUSurface<Backend> surface)
    {
        ThrowIfNativeFailed(
            wgpuSurfacePresent(ToNative(surface.Handle)),
            nameof(wgpuSurfacePresent));
    }
}
static class WebGPUNETExtension
{
    public unsafe static Span<WGPUVertexBufferLayout> GetBuffers(this WGPUVertexState value)
             => new(value.buffers, (int)value.bufferCount);
}
