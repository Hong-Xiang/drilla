using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;

internal sealed class GpuFrames : IDisposable
{
    private static readonly TimeSpan MapTimeout = TimeSpan.FromSeconds(10);
    private const string TriangleShader = """
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

    private static readonly float[] FullscreenVertices =
    [
        -1.0f, 1.0f,
        -1.0f, -1.0f,
        1.0f, -1.0f,
        -1.0f, 1.0f,
        1.0f, -1.0f,
        1.0f, 1.0f,
    ];

    private readonly Stack<IDisposable> _resources;
    private readonly IGPUDevice _device;
    private readonly DrawPacket _draw;
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
        DrawPacket draw,
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
        _draw = draw;
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

    internal static Task<GpuFrames> CreateTriangleAsync(
        VideoSettings video,
        CancellationToken cancellation) =>
        CreateAsync(
            checked((uint)video.Width),
            checked((uint)video.Height),
            CreateTrianglePacket,
            cancellation);

    internal static Task<GpuFrames> CreateRaymarchAsync(
        VideoSettings video,
        RaymarchProgram program,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(program);
        return CreateAsync(
            checked((uint)video.Width),
            checked((uint)video.Height),
            (device, resources, width, height) =>
                CreateRaymarchPacket(device, resources, width, height, program),
            cancellation);
    }

    private static async Task<GpuFrames> CreateAsync(
        uint width,
        uint height,
        DrawPacketFactory createDrawPacket,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        uint paddedRowBytes = checked((width * VideoSettings.BytesPerPixel + 255) & ~255u);
        var resources = new Stack<IDisposable>();

        try
        {
            var instance = Own(resources, WebGPUNETBackend.Instance.CreateGPUInstance());
            var adapter = Own(
                resources,
                await instance.RequestAdapterAsync(
                    new()
                    {
                        PowerPreference = GPUPowerPreference.HighPerformance,
                        ForceFallbackAdapter = false,
                    },
                    cancellation));
            GPUAdapterInfo info = await adapter.RequestAdapterInfoAsync(cancellation);
            if (info.AdapterType is not (GPUAdapterType.DiscreteGPU or GPUAdapterType.IntegratedGPU))
            {
                throw new InvalidOperationException(
                    $"A hardware GPU is required; received {info.AdapterType}: {info.Device}.");
            }

            var device = Own(resources, await adapter.RequestDeviceAsync(new(), cancellation));
            DrawPacket draw = createDrawPacket(device, resources, width, height);
            var texture = Own(resources, device.CreateTexture(new()
            {
                Size = new() { Width = width, Height = height, DepthOrArrayLayers = 1 },
                Format = GPUTextureFormat.BGRA8Unorm,
                Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
            }));
            var view = Own(resources, texture.CreateView());
            var readback = Own(resources, device.CreateBuffer(new()
            {
                Size = checked((ulong)paddedRowBytes * height),
                Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead,
            }));
            cancellation.ThrowIfCancellationRequested();
            return new(
                resources,
                info,
                device,
                draw,
                texture,
                view,
                readback,
                width,
                height,
                paddedRowBytes);
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

    private static DrawPacket CreateTrianglePacket(
        IGPUDevice device,
        Stack<IDisposable> resources,
        uint width,
        uint height)
    {
        var shader = Own(resources, device.CreateShaderModule(new() { Code = TriangleShader }));
        var bindingLayout = Own(resources, device.CreateBindGroupLayout(new()
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
        var layout = Own(
            resources,
            device.CreatePipelineLayout(new() { BindGroupLayouts = [bindingLayout] }));
        var pipeline = Own(resources, device.CreateRenderPipeline(new()
        {
            Layout = layout,
            Vertex = new() { Module = shader, EntryPoint = "vs" },
            Fragment = new()
            {
                Module = shader,
                EntryPoint = "fs",
                Targets = new GPUColorTargetState[]
                {
                    new()
                    {
                        Format = GPUTextureFormat.BGRA8Unorm,
                        WriteMask = GPUColorWriteMask.All,
                    },
                },
            },
            Primitive = new() { Topology = GPUPrimitiveTopology.TriangleList },
        }));
        var uniform = Own(resources, CreateUniformBuffer(device, 16));
        var binding = Own(resources, device.CreateBindGroup(new()
        {
            Layout = bindingLayout,
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Buffer = uniform, Size = 16 },
            },
        }));

        return new(
            (animationTime, pointer) =>
            {
                float angle = (float)(animationTime.TotalSeconds % (2 * Math.PI));
                device.Queue.WriteBuffer<float>(
                    uniform,
                    0,
                    stackalloc float[] { angle, (float)height / width, pointer.ClipX, pointer.ClipY });
            },
            pass =>
            {
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(0, binding, []);
                pass.Draw(3);
            });
    }

    private static DrawPacket CreateRaymarchPacket(
        IGPUDevice device,
        Stack<IDisposable> resources,
        uint width,
        uint height,
        RaymarchProgram program)
    {
        var shader = Own(resources, device.CreateShaderModule(new() { Code = program.Wgsl }));
        var vertexBuffer = CreateVertexBuffer(device, resources);
        var resolution = Own(resources, CreateUniformBuffer(device, 8));
        var time = Own(resources, CreateUniformBuffer(device, 4));
        var mouse = Own(resources, CreateUniformBuffer(device, 16));
        var antiAliasing = Own(resources, CreateUniformBuffer(device, 4));
        var bindingLayout = Own(resources, device.CreateBindGroupLayout(new()
        {
            Entries = new GPUBindGroupLayoutEntry[]
            {
                UniformLayoutEntry(0, 8),
                UniformLayoutEntry(1, 4),
                UniformLayoutEntry(2, 16),
                UniformLayoutEntry(3, 4),
            },
        }));
        var layout = Own(
            resources,
            device.CreatePipelineLayout(new() { BindGroupLayouts = [bindingLayout] }));
        var pipeline = Own(resources, device.CreateRenderPipeline(new()
        {
            Layout = layout,
            Vertex = new()
            {
                Module = shader,
                EntryPoint = "vs",
                Buffers = new GPUVertexBufferLayout[]
                {
                    new()
                    {
                        ArrayStride = 2 * sizeof(float),
                        StepMode = GPUVertexStepMode.Vertex,
                        Attributes = new GPUVertexAttribute[]
                        {
                            new()
                            {
                                ShaderLocation = 0,
                                Format = GPUVertexFormat.Float32x2,
                                Offset = 0,
                            },
                        },
                    },
                },
            },
            Fragment = new()
            {
                Module = shader,
                EntryPoint = "fs",
                Targets = new GPUColorTargetState[]
                {
                    new()
                    {
                        Format = GPUTextureFormat.BGRA8Unorm,
                        WriteMask = GPUColorWriteMask.All,
                    },
                },
            },
            Primitive = new() { Topology = GPUPrimitiveTopology.TriangleList },
        }));
        var binding = Own(resources, device.CreateBindGroup(new()
        {
            Layout = bindingLayout,
            Entries = new GPUBindGroupEntry[]
            {
                UniformEntry(0, resolution, 8),
                UniformEntry(1, time, 4),
                UniformEntry(2, mouse, 16),
                UniformEntry(3, antiAliasing, 4),
            },
        }));

        device.Queue.WriteBuffer<float>(
            resolution,
            0,
            stackalloc float[] { width, height });
        device.Queue.WriteBuffer<int>(antiAliasing, 0, stackalloc int[] { 1 });

        return new(
            (animationTime, pointer) =>
            {
                RaymarchFrameUniforms uniforms =
                    RaymarchFrameUniforms.Pack(width, height, animationTime, pointer);
                device.Queue.WriteBuffer<float>(
                    time,
                    0,
                    stackalloc float[] { uniforms.TimeSeconds });
                device.Queue.WriteBuffer<float>(
                    mouse,
                    0,
                    stackalloc float[]
                    {
                        uniforms.MouseX,
                        uniforms.MouseY,
                        0,
                        0,
                    });
            },
            pass =>
            {
                pass.SetPipeline(pipeline);
                pass.SetVertexBuffer(0, vertexBuffer);
                pass.SetBindGroup(0, binding, []);
                pass.Draw(6);
            });
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

        _draw.Prepare(animationTime, pointer);
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
                _draw.Draw(pass);
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

    internal static int RunRaymarchUniformSelfTest()
    {
        RaymarchFrameUniforms packed = RaymarchFrameUniforms.Pack(
            320,
            180,
            TimeSpan.FromSeconds(10.25),
            PointerPosition.Create(0.25, 0.75));
        RaymarchFrameUniforms top = RaymarchFrameUniforms.Pack(
            320,
            180,
            TimeSpan.Zero,
            PointerPosition.Create(0.5, 0));
        RaymarchFrameUniforms bottom = RaymarchFrameUniforms.Pack(
            320,
            180,
            TimeSpan.Zero,
            PointerPosition.Create(0.5, 1));

        if (packed != new RaymarchFrameUniforms(10.25f, 80, 45) ||
            top.MouseY != 180 ||
            bottom.MouseY != 0)
        {
            throw new InvalidOperationException(
                "Raymarch time or top-origin pointer uniform packing was incorrect.");
        }

        Console.WriteLine("Raymarch time and top-origin pointer uniform packing passed.");
        return 0;
    }

    internal static async Task<int> RunTriangleSelfTestAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using GpuFrames frames = await CreateAsync(65, 49, CreateTrianglePacket, deadline.Token);
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
            if (!first.AsSpan(i, VideoSettings.BytesPerPixel).SequenceEqual(
                    second.AsSpan(i, VideoSettings.BytesPerPixel)))
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

    internal static async Task<int> RunRaymarchSelfTestAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var video = VideoSettings.Load(new ConfigurationManager
        {
            ["Video:Width"] = "64",
            ["Video:Height"] = "48",
            ["Video:FramesPerSecond"] = "30",
        });
        RaymarchProgram program = RaymarchProgram.Compile();
        using GpuFrames firstInstance =
            await CreateRaymarchAsync(video, program, deadline.Token);
        using GpuFrames secondInstance =
            await CreateRaymarchAsync(video, program, deadline.Token);

        byte[] baseline = new byte[video.FrameBytes];
        byte[] repeated = new byte[video.FrameBytes];
        byte[] later = new byte[video.FrameBytes];
        byte[] moved = new byte[video.FrameBytes];
        byte[] reset = new byte[video.FrameBytes];
        byte[] isolatedBefore = new byte[video.FrameBytes];
        byte[] isolatedAfter = new byte[video.FrameBytes];
        byte[] mutated = new byte[video.FrameBytes];

        await firstInstance.RenderAsync(
            baseline,
            TimeSpan.Zero,
            PointerPosition.Center,
            deadline.Token);
        await firstInstance.RenderAsync(
            repeated,
            TimeSpan.Zero,
            PointerPosition.Center,
            deadline.Token);
        await firstInstance.RenderAsync(
            later,
            TimeSpan.FromSeconds(1),
            PointerPosition.Center,
            deadline.Token);
        await firstInstance.RenderAsync(
            moved,
            TimeSpan.Zero,
            PointerPosition.Create(0.7, 0.5),
            deadline.Token);
        await firstInstance.RenderAsync(
            reset,
            TimeSpan.Zero,
            PointerPosition.Center,
            deadline.Token);
        await secondInstance.RenderAsync(
            isolatedBefore,
            TimeSpan.FromSeconds(0.5),
            PointerPosition.Center,
            deadline.Token);
        await firstInstance.RenderAsync(
            mutated,
            TimeSpan.FromSeconds(2),
            PointerPosition.Create(0.9, 0.5),
            deadline.Token);
        await secondInstance.RenderAsync(
            isolatedAfter,
            TimeSpan.FromSeconds(0.5),
            PointerPosition.Center,
            deadline.Token);

        int timeChanged = CountChangedPixels(baseline, later);
        int inputChanged = CountChangedPixels(baseline, moved);
        FrameRange range = AnalyzeFrame(baseline);
        if (!baseline.AsSpan().SequenceEqual(repeated) ||
            !baseline.AsSpan().SequenceEqual(reset) ||
            !isolatedBefore.AsSpan().SequenceEqual(isolatedAfter) ||
            timeChanged < video.Width * video.Height / 20 ||
            inputChanged < video.Width * video.Height / 20 ||
            range.VaryingPixels < video.Width * video.Height / 4 ||
            range.ChannelRange < 32)
        {
            throw new InvalidOperationException(
                "Raymarch determinism, sensitivity, reset, scene variation, or instance isolation failed.");
        }

        Console.WriteLine(
            $"RAYMARCH_SELF_TEST PASS backend={firstInstance.AdapterInfo.BackendType} " +
            $"type={firstInstance.AdapterInfo.AdapterType} " +
            $"device=\"{firstInstance.AdapterInfo.Device}\" " +
            $"timeChangedPixels={timeChanged} inputChangedPixels={inputChanged} " +
            $"varyingPixels={range.VaryingPixels} channelRange={range.ChannelRange}");
        return 0;
    }

    private static T Own<T>(Stack<IDisposable> resources, T resource)
        where T : IDisposable
    {
        resources.Push(resource);
        return resource;
    }

    private static IGPUBuffer CreateUniformBuffer(IGPUDevice device, ulong size) =>
        device.CreateBuffer(new()
        {
            Size = size,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });

    private static IGPUBuffer CreateVertexBuffer(
        IGPUDevice device,
        Stack<IDisposable> resources)
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(FullscreenVertices.AsSpan());
        var buffer = Own(resources, device.CreateBuffer(new()
        {
            MappedAtCreation = true,
            Size = (ulong)bytes.Length,
            Usage = GPUBufferUsage.Vertex,
        }));
        bytes.CopyTo(buffer.GetMappedRange(0, (ulong)bytes.Length));
        buffer.Unmap();
        return buffer;
    }

    private static GPUBindGroupLayoutEntry UniformLayoutEntry(int binding, ulong size) =>
        new()
        {
            Binding = binding,
            Visibility = GPUShaderStage.Fragment,
            Buffer = new()
            {
                Type = GPUBufferBindingType.Uniform,
                MinBindingSize = size,
            },
        };

    private static GPUBindGroupEntry UniformEntry(
        int binding,
        IGPUBuffer buffer,
        ulong size) =>
        new()
        {
            Binding = binding,
            Buffer = buffer,
            Size = size,
        };

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

    private static int CountChangedPixels(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Pixel buffers must have the same length.");
        }

        int changed = 0;
        for (int offset = 0; offset < left.Length; offset += VideoSettings.BytesPerPixel)
        {
            if (left[offset + 3] != 255 || right[offset + 3] != 255)
            {
                throw new InvalidOperationException("Raymarch output did not preserve opaque BGRA alpha.");
            }
            if (!left.Slice(offset, VideoSettings.BytesPerPixel).SequenceEqual(
                    right.Slice(offset, VideoSettings.BytesPerPixel)))
            {
                changed++;
            }
        }
        return changed;
    }

    private static FrameRange AnalyzeFrame(ReadOnlySpan<byte> pixels)
    {
        byte firstBlue = pixels[0];
        byte firstGreen = pixels[1];
        byte firstRed = pixels[2];
        byte minimum = 255;
        byte maximum = 0;
        int varying = 0;

        for (int offset = 0; offset < pixels.Length; offset += VideoSettings.BytesPerPixel)
        {
            if (pixels[offset + 3] != 255)
            {
                throw new InvalidOperationException("Raymarch output did not preserve opaque BGRA alpha.");
            }

            byte blue = pixels[offset];
            byte green = pixels[offset + 1];
            byte red = pixels[offset + 2];
            minimum = Math.Min(minimum, Math.Min(blue, Math.Min(green, red)));
            maximum = Math.Max(maximum, Math.Max(blue, Math.Max(green, red)));
            if (blue != firstBlue || green != firstGreen || red != firstRed)
            {
                varying++;
            }
        }

        return new(varying, maximum - minimum);
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

    private delegate DrawPacket DrawPacketFactory(
        IGPUDevice device,
        Stack<IDisposable> resources,
        uint width,
        uint height);

    private sealed record DrawPacket(
        Action<TimeSpan, PointerPosition> Prepare,
        Action<IGPURenderPassEncoder> Draw);

    private readonly record struct RaymarchFrameUniforms(
        float TimeSeconds,
        float MouseX,
        float MouseY)
    {
        internal static RaymarchFrameUniforms Pack(
            uint width,
            uint height,
            TimeSpan animationTime,
            PointerPosition pointer) =>
            new(
                (float)animationTime.TotalSeconds,
                pointer.X * width,
                (1 - pointer.Y) * height);
    }

    private readonly record struct FrameRange(int VaryingPixels, int ChannelRange);
}
