using System.Runtime.ExceptionServices;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;

internal sealed class GpuFrames : IDisposable
{
    private static readonly TimeSpan MapTimeout = TimeSpan.FromSeconds(10);
    private const string Shader = """
        @group(0) @binding(0) var<uniform> animation: vec4<f32>;

        struct Vertex {
            @builtin(position) position: vec4<f32>,
            @location(0) color: vec3<f32>,
        }

        @vertex fn vs(@builtin(vertex_index) index: u32) -> Vertex {
            var positions = array<vec2<f32>, 3>(
                vec2<f32>(-0.70, -0.60),
                vec2<f32>(0.75, -0.42),
                vec2<f32>(-0.05, 0.78));
            var colors = array<vec3<f32>, 3>(
                vec3<f32>(1.0, 0.0, 0.0),
                vec3<f32>(0.0, 1.0, 0.0),
                vec3<f32>(0.0, 0.0, 1.0));
            let c = cos(animation.x);
            let s = sin(animation.x);
            let p = positions[index];
            var result: Vertex;
            result.position = vec4<f32>(
                (c * p.x - s * p.y) * animation.y + animation.z,
                s * p.x + c * p.y + animation.w, 0.0, 1.0);
            result.color = colors[index];
            return result;
        }

        @fragment fn fs(input: Vertex) -> @location(0) vec4<f32> {
            return vec4<f32>(input.color, 1.0);
        }
        """;

    private readonly Stack<IDisposable> _resources;
    private readonly IGPUDevice _device;
    private readonly IGPURenderPipeline _pipeline;
    private readonly IGPUBindGroup _binding;
    private readonly IGPUBuffer _uniform;
    private readonly IGPUTexture _texture;
    private readonly IGPUTextureView _view;
    private readonly IGPUBuffer _readback;
    private readonly uint _width;
    private readonly uint _height;
    private readonly uint _rowBytes;
    private readonly uint _paddedRowBytes;
    private readonly ulong _bufferBytes;
    private readonly int _frameBytes;

    internal GPUAdapterInfo AdapterInfo { get; }

    private GpuFrames(
        Stack<IDisposable> resources,
        GPUAdapterInfo adapterInfo,
        IGPUDevice device,
        IGPURenderPipeline pipeline,
        IGPUBindGroup binding,
        IGPUBuffer uniform,
        IGPUTexture texture,
        IGPUTextureView view,
        IGPUBuffer readback,
        uint width,
        uint height,
        uint paddedRowBytes)
    {
        _resources = resources;
        AdapterInfo = adapterInfo;
        _device = device;
        _pipeline = pipeline;
        _binding = binding;
        _uniform = uniform;
        _texture = texture;
        _view = view;
        _readback = readback;
        _width = width;
        _height = height;
        _rowBytes = checked(width * VideoSettings.BytesPerPixel);
        _paddedRowBytes = paddedRowBytes;
        _bufferBytes = checked((ulong)paddedRowBytes * height);
        _frameBytes = checked((int)(_rowBytes * height));
    }

    internal static Task<GpuFrames> CreateAsync(VideoSettings video, CancellationToken cancellation) =>
        CreateAsync(checked((uint)video.Width), checked((uint)video.Height), cancellation);

    private static async Task<GpuFrames> CreateAsync(
        uint width,
        uint height,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        uint paddedRowBytes = checked((width * VideoSettings.BytesPerPixel + 255) & ~255u);
        var resources = new Stack<IDisposable>();

        T Own<T>(T resource) where T : IDisposable
        {
            resources.Push(resource);
            return resource;
        }

        try
        {
            var instance = Own(WebGPUNETBackend.Instance.CreateGPUInstance());
            var adapter = Own(await instance.RequestAdapterAsync(
                new() { PowerPreference = GPUPowerPreference.HighPerformance, ForceFallbackAdapter = false },
                cancellation));
            GPUAdapterInfo info = await adapter.RequestAdapterInfoAsync(cancellation);
            if (info.AdapterType is not (GPUAdapterType.DiscreteGPU or GPUAdapterType.IntegratedGPU))
            {
                throw new InvalidOperationException(
                    $"A hardware GPU is required; received {info.AdapterType}: {info.Device}.");
            }

            var device = Own(await adapter.RequestDeviceAsync(new(), cancellation));
            var shader = Own(device.CreateShaderModule(new() { Code = Shader }));
            var bindingLayout = Own(device.CreateBindGroupLayout(new()
            {
                Entries = new GPUBindGroupLayoutEntry[]
                {
                    new()
                    {
                        Binding = 0,
                        Visibility = GPUShaderStage.Vertex,
                        Buffer = new() { Type = GPUBufferBindingType.Uniform, MinBindingSize = 16 },
                    },
                },
            }));
            var layout = Own(device.CreatePipelineLayout(new() { BindGroupLayouts = new[] { bindingLayout } }));
            var pipeline = Own(device.CreateRenderPipeline(new()
            {
                Layout = layout,
                Vertex = new() { Module = shader, EntryPoint = "vs" },
                Fragment = new()
                {
                    Module = shader,
                    EntryPoint = "fs",
                    Targets = new GPUColorTargetState[]
                    {
                        new() { Format = GPUTextureFormat.BGRA8Unorm, WriteMask = GPUColorWriteMask.All },
                    },
                },
                Primitive = new() { Topology = GPUPrimitiveTopology.TriangleList },
            }));
            var uniform = Own(device.CreateBuffer(new()
            {
                Size = 16,
                Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
            }));
            var binding = Own(device.CreateBindGroup(new()
            {
                Layout = bindingLayout,
                Entries = new GPUBindGroupEntry[] { new() { Binding = 0, Buffer = uniform, Size = 16 } },
            }));
            var texture = Own(device.CreateTexture(new()
            {
                Size = new() { Width = width, Height = height, DepthOrArrayLayers = 1 },
                Format = GPUTextureFormat.BGRA8Unorm,
                Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
            }));
            var view = Own(texture.CreateView());
            var readback = Own(device.CreateBuffer(new()
            {
                Size = checked((ulong)paddedRowBytes * height),
                Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead,
            }));
            cancellation.ThrowIfCancellationRequested();
            return new(resources, info, device, pipeline, binding, uniform, texture, view, readback,
                width, height, paddedRowBytes);
        }
        catch (Exception failure)
        {
            List<Exception> cleanup = Release(resources);
            if (cleanup.Count != 0)
            {
                throw new AggregateException("GPU initialization and cleanup failed.", [failure, .. cleanup]);
            }
            throw;
        }
    }

    internal async Task RenderAsync(
        Memory<byte> destination,
        TimeSpan animationTime,
        PointerPosition pointer,
        CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(_resources.Count == 0, this);
        cancellation.ThrowIfCancellationRequested();
        if (destination.Length != _frameBytes)
        {
            throw new ArgumentException($"Expected exactly {_frameBytes} BGRA bytes.", nameof(destination));
        }

        float angle = (float)(animationTime.TotalSeconds % (2 * Math.PI));
        _device.Queue.WriteBuffer<float>(
            _uniform,
            0,
            stackalloc float[] { angle, (float)_height / _width, pointer.ClipX, pointer.ClipY });
        using (var encoder = _device.CreateCommandEncoder(new()))
        {
            using (var pass = encoder.BeginRenderPass(new()
            {
                ColorAttachments = new GPURenderPassColorAttachment[]
                {
                    new()
                    {
                        View = _view,
                        ClearValue = new() { R = 0, G = 0, B = 0, A = 1 },
                        LoadOp = GPULoadOp.Clear,
                        StoreOp = GPUStoreOp.Store,
                    },
                },
            }))
            {
                pass.SetPipeline(_pipeline);
                pass.SetBindGroup(0, _binding, []);
                pass.Draw(3);
                pass.End();
            }

            encoder.CopyTextureToBuffer(
                new() { Texture = _texture },
                new()
                {
                    Buffer = _readback,
                    Layout = new() { BytesPerRow = _paddedRowBytes, RowsPerImage = _height },
                },
                new() { Width = _width, Height = _height, DepthOrArrayLayers = 1 });
            using var commands = encoder.Finish(new());
            _device.Queue.Submit([commands]);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(MapTimeout);
        Task map = _readback.MapAsync(GPUMapMode.Read, 0, _bufferBytes, deadline.Token).AsTask();
        Exception? pollingFailure = null;
        do
        {
            try
            {
                _device.Poll();
            }
            catch (Exception failure)
            {
                pollingFailure ??= failure;
                deadline.Cancel();
            }
            if (!map.IsCompleted)
            {
                // MapAsync completes cancellation only after its native callback is terminal.
                await Task.Delay(1, CancellationToken.None);
            }
        } while (!map.IsCompleted);

        bool mapped = false;
        try
        {
            await map;
            mapped = true;
            if (pollingFailure is not null)
            {
                ExceptionDispatchInfo.Throw(pollingFailure);
            }
            deadline.Token.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> source = _readback.GetMappedRange(0, _bufferBytes);
            for (int y = 0; y < _height; y++)
            {
                source.Slice(checked(y * (int)_paddedRowBytes), (int)_rowBytes)
                    .CopyTo(destination.Span.Slice(checked(y * (int)_rowBytes), (int)_rowBytes));
            }
        }
        catch (OperationCanceledException) when (pollingFailure is not null)
        {
            ExceptionDispatchInfo.Throw(pollingFailure);
            throw;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"GPU readback exceeded {MapTimeout}.");
        }
        finally
        {
            if (mapped)
            {
                _readback.Unmap();
            }
        }
    }

    public void Dispose()
    {
        List<Exception> errors = Release(_resources);
        if (errors.Count != 0)
        {
            throw new AggregateException("GPU resource cleanup failed.", errors);
        }
    }

    private static List<Exception> Release(Stack<IDisposable> resources)
    {
        var errors = new List<Exception>();
        while (resources.TryPop(out IDisposable? resource))
        {
            try
            {
                resource.Dispose();
            }
            catch (Exception failure)
            {
                errors.Add(failure);
            }
        }
        return errors;
    }

    internal static async Task<int> RunSelfTestAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using GpuFrames frames = await CreateAsync(65, 49, deadline.Token);
        byte[] first = new byte[frames._frameBytes];
        byte[] second = new byte[frames._frameBytes];
        byte[] translated = new byte[frames._frameBytes];
        byte[] repeated = new byte[frames._frameBytes];
        await frames.RenderAsync(first, TimeSpan.Zero, PointerPosition.Center, deadline.Token);
        await frames.RenderAsync(
            second,
            TimeSpan.FromSeconds(1),
            PointerPosition.Center,
            deadline.Token);
        PointerPosition translatedPosition = PointerPosition.Create(0.65, 0.4);
        await frames.RenderAsync(translated, TimeSpan.Zero, translatedPosition, deadline.Token);
        await frames.RenderAsync(repeated, TimeSpan.Zero, PointerPosition.Center, deadline.Token);
        int red = (35 * 65 + 18) * VideoSettings.BytesPerPixel;
        int blue = (10 * 65 + 31) * VideoSettings.BytesPerPixel;
        int changed = 0;
        for (int i = 0; i < first.Length; i += VideoSettings.BytesPerPixel)
        {
            if (first[i + 3] != 255 || second[i + 3] != 255)
            {
                throw new InvalidOperationException("GPU readback lost opaque alpha or included row padding.");
            }
            if (!first.AsSpan(i, VideoSettings.BytesPerPixel).SequenceEqual(second.AsSpan(i, VideoSettings.BytesPerPixel)))
            {
                changed++;
            }
        }
        (double centerX, double centerY) = ForegroundCentroid(first, frames._width);
        (double translatedX, double translatedY) = ForegroundCentroid(translated, frames._width);
        double expectedX = translatedPosition.ClipX * frames._width / 2;
        double expectedY = -translatedPosition.ClipY * frames._height / 2;
        if (first[red + 2] < 160 || first[red] > 80 || first[blue] < 160 || first[blue + 2] > 80 ||
            first[0] != 0 || first[1] != 0 || first[2] != 0 ||
            changed < 200 || !first.AsSpan().SequenceEqual(repeated) ||
            Math.Abs(translatedX - centerX - expectedX) > 2 ||
            Math.Abs(translatedY - centerY - expectedY) > 2)
        {
            throw new InvalidOperationException(
                "GPU BGRA colors, animation, translated position, or reset readback were incorrect.");
        }
        Console.WriteLine(
            $"GPU_SELF_TEST PASS backend={frames.AdapterInfo.BackendType} type={frames.AdapterInfo.AdapterType} " +
            $"device=\"{frames.AdapterInfo.Device}\" rowBytes={frames._rowBytes} " +
            $"paddedRowBytes={frames._paddedRowBytes} changedPixels={changed} " +
            $"translation=({translatedX - centerX:F2},{translatedY - centerY:F2})");
        return 0;
    }

    private static (double X, double Y) ForegroundCentroid(byte[] pixels, uint width)
    {
        long xTotal = 0;
        long yTotal = 0;
        int count = 0;
        for (int i = 0; i < pixels.Length; i += VideoSettings.BytesPerPixel)
        {
            if (pixels[i] == 0 && pixels[i + 1] == 0 && pixels[i + 2] == 0)
            {
                continue;
            }
            int pixel = i / VideoSettings.BytesPerPixel;
            xTotal += pixel % width;
            yTotal += pixel / width;
            count++;
        }
        return count > 0
            ? ((double)xTotal / count, (double)yTotal / count)
            : throw new InvalidOperationException("The GPU frame had no foreground pixels.");
    }
}
