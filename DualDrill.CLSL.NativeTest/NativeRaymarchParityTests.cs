using System.Diagnostics;
using System.Runtime.InteropServices;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using DualDrill.Shaders;
using Xunit.Abstractions;

namespace DualDrill.CLSL.NativeTest;

public sealed class NativeRaymarchParityTests(ITestOutputHelper output)
{
    private const string ReferenceVertexWgsl =
        """
        struct VertexOutput {
            @builtin(position) position: vec4<f32>,
        }

        @vertex
        fn vs(@location(0) position: vec2<f32>) -> VertexOutput {
            var output: VertexOutput;
            output.position = vec4<f32>(position, 0.0, 1.0);
            return output;
        }
        """;

    private static readonly float[] FullscreenVertices =
    [
        -1.0f,  1.0f,
        -1.0f, -1.0f,
         1.0f, -1.0f,
        -1.0f,  1.0f,
         1.0f, -1.0f,
         1.0f,  1.0f,
    ];

    private static readonly RaymarchProfile[] Profiles =
    [
        new("center-aa1", 320, 180, 1.0f, 160.0f, 90.0f, AntiAliasing.One),
        new("center-aa2", 320, 180, 1.0f, 160.0f, 90.0f, AntiAliasing.Two),
        new("center-aa3", 320, 180, 1.0f, 160.0f, 90.0f, AntiAliasing.Three),
        new("off-center-aa1", 320, 180, 3.0f, 80.0f, 45.0f, AntiAliasing.One),
    ];

    [Fact]
    [Trait("Category", "GPU")]
    public async Task Public_CLSL_raymarch_matches_independent_GLSL_reference()
    {
        var candidateWgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL))
            .Emit(new RaymarchingPrimitiveShader());
        Assert.False(string.IsNullOrWhiteSpace(candidateWgsl));

        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new()
            {
                PowerPreference = GPUPowerPreference.HighPerformance,
                ForceFallbackAdapter = false,
            },
            CancellationToken.None);
        using var device = await adapter.RequestDeviceAsync(new(), CancellationToken.None);
        using var vertexBuffer = CreateVertexBuffer(device);

        var references = new Dictionary<string, RgbaImage>(StringComparer.Ordinal);
        foreach (var profile in Profiles)
        {
            var referenceWgsl = await RaymarchReference.CompileFragmentAsync(profile);
            var reference = await RenderReferenceAsync(
                device,
                vertexBuffer,
                referenceWgsl,
                profile);
            var candidate = await RenderCandidateAsync(
                device,
                vertexBuffer,
                candidateWgsl,
                profile);
            references.Add(profile.Name, reference);

            var metrics = RgbaImageComparer.Compare(reference, candidate);
            output.WriteLine($"{profile.Name}: {metrics}");
            if (!metrics.IsWithin(ImageThresholds.Raymarch))
                WriteFailureArtifacts(profile.Name, reference, candidate);

            Assert.True(
                metrics.IsWithin(ImageThresholds.Raymarch),
                $"{profile.Name} exceeded {ImageThresholds.Raymarch}: {metrics}");
        }

        AssertNegativeControl(
            "x-flipped",
            references["center-aa1"],
            FlipX(references["center-aa1"]));
        AssertNegativeControl(
            "mouse-time",
            references["center-aa1"],
            references["off-center-aa1"]);

        var aaMetrics = RgbaImageComparer.Compare(
            references["center-aa1"],
            references["center-aa2"]);
        Assert.False(
            aaMetrics.IsWithin(ImageThresholds.Raymarch),
            "AA1 and AA2 were not distinguishable by the whole-frame predicate. "
            + $"Observed {aaMetrics}; add a targeted AA regression rather than claiming coverage.");
    }

    private static IGPUBuffer CreateVertexBuffer(IGPUDevice device)
    {
        var bytes = MemoryMarshal.AsBytes(FullscreenVertices.AsSpan());
        var buffer = device.CreateBuffer(new()
        {
            MappedAtCreation = true,
            Size = (ulong)bytes.Length,
            Usage = GPUBufferUsage.Vertex,
        });
        bytes.CopyTo(buffer.GetMappedRange(0, (ulong)bytes.Length));
        buffer.Unmap();
        return buffer;
    }

    private static async Task<RgbaImage> RenderReferenceAsync(
        IGPUDevice device,
        IGPUBuffer vertexBuffer,
        string fragmentWgsl,
        RaymarchProfile profile)
    {
        using var vertexShader = device.CreateShaderModule(new() { Code = ReferenceVertexWgsl });
        using var fragmentShader = device.CreateShaderModule(new() { Code = fragmentWgsl });
        using var layout = device.CreatePipelineLayout(new() { BindGroupLayouts = [] });
        using var pipeline = CreatePipeline(
            device,
            layout,
            vertexShader,
            "vs",
            fragmentShader,
            "main");
        return await RenderAsync(device, vertexBuffer, pipeline, null, profile);
    }

    private static async Task<RgbaImage> RenderCandidateAsync(
        IGPUDevice device,
        IGPUBuffer vertexBuffer,
        string candidateWgsl,
        RaymarchProfile profile)
    {
        using var shader = device.CreateShaderModule(new() { Code = candidateWgsl });
        using var resolution = CreateUniformBuffer(
            device,
            [(float)profile.Width, (float)profile.Height]);
        using var time = CreateUniformBuffer(device, [profile.Time]);
        using var mouse = CreateUniformBuffer(
            device,
            [profile.MouseX, profile.MouseY, 0.0f, 0.0f]);
        using var aa = CreateUniformBuffer(device, [profile.AntiAliasing.Value]);
        var bindGroupLayout = device.CreateBindGroupLayout(new()
        {
            Entries = new GPUBindGroupLayoutEntry[]
            {
                UniformLayoutEntry(0, 8),
                UniformLayoutEntry(1, 4),
                UniformLayoutEntry(2, 16),
                UniformLayoutEntry(3, 4),
            },
        });
        using var bindGroupLayoutScope = (IDisposable)bindGroupLayout;
        using var layout = device.CreatePipelineLayout(new()
        {
            BindGroupLayouts = [bindGroupLayout],
        });
        var bindGroup = device.CreateBindGroup(new()
        {
            Layout = bindGroupLayout,
            Entries = new GPUBindGroupEntry[]
            {
                UniformEntry(0, resolution, 8),
                UniformEntry(1, time, 4),
                UniformEntry(2, mouse, 16),
                UniformEntry(3, aa, 4),
            },
        });
        using var bindGroupScope = (IDisposable)bindGroup;
        using var pipeline = CreatePipeline(device, layout, shader, "vs", shader, "fs");
        return await RenderAsync(device, vertexBuffer, pipeline, bindGroup, profile);
    }

    private static IGPURenderPipeline CreatePipeline(
        IGPUDevice device,
        IGPUPipelineLayout layout,
        IGPUShaderModule vertexShader,
        string vertexEntry,
        IGPUShaderModule fragmentShader,
        string fragmentEntry) =>
        device.CreateRenderPipeline(new()
        {
            Layout = layout,
            Vertex = new()
            {
                Module = vertexShader,
                EntryPoint = vertexEntry,
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
                Module = fragmentShader,
                EntryPoint = fragmentEntry,
                Targets = new GPUColorTargetState[]
                {
                    new()
                    {
                        Format = GPUTextureFormat.RGBA8Unorm,
                        WriteMask = GPUColorWriteMask.All,
                    },
                },
            },
            Primitive = new() { Topology = GPUPrimitiveTopology.TriangleList },
        });

    private static async Task<RgbaImage> RenderAsync(
        IGPUDevice device,
        IGPUBuffer vertexBuffer,
        IGPURenderPipeline pipeline,
        IGPUBindGroup? bindGroup,
        RaymarchProfile profile)
    {
        var rowBytes = checked((uint)profile.Width * 4);
        var paddedRowBytes = checked((rowBytes + 255u) & ~255u);
        var readbackSize = checked((ulong)paddedRowBytes * (uint)profile.Height);
        using var texture = device.CreateTexture(new()
        {
            Size = new()
            {
                Width = (uint)profile.Width,
                Height = (uint)profile.Height,
                DepthOrArrayLayers = 1,
            },
            Format = GPUTextureFormat.RGBA8Unorm,
            Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
        });
        using var view = texture.CreateView();
        using var readback = device.CreateBuffer(new()
        {
            Size = readbackSize,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead,
        });

        using (var encoder = device.CreateCommandEncoder(new()))
        {
            using (var pass = encoder.BeginRenderPass(new()
            {
                ColorAttachments = new GPURenderPassColorAttachment[]
                {
                    new()
                    {
                        View = view,
                        ClearValue = new() { R = 0, G = 0, B = 0, A = 1 },
                        LoadOp = GPULoadOp.Clear,
                        StoreOp = GPUStoreOp.Store,
                    },
                },
            }))
            {
                pass.SetPipeline(pipeline);
                pass.SetVertexBuffer(0, vertexBuffer);
                if (bindGroup is not null)
                    pass.SetBindGroup(0, bindGroup);
                pass.Draw(6);
                pass.End();
            }

            encoder.CopyTextureToBuffer(
                new() { Texture = texture },
                new()
                {
                    Buffer = readback,
                    Layout = new()
                    {
                        BytesPerRow = paddedRowBytes,
                        RowsPerImage = (uint)profile.Height,
                    },
                },
                new()
                {
                    Width = (uint)profile.Width,
                    Height = (uint)profile.Height,
                    DepthOrArrayLayers = 1,
                });
            using var commands = encoder.Finish(new());
            device.Queue.Submit([commands]);
        }

        await MapWithPollingAsync(device, readback, readbackSize);
        try
        {
            var mapped = readback.GetMappedRange(0, readbackSize);
            var pixels = new byte[checked(profile.Width * profile.Height * 4)];
            for (var row = 0; row < profile.Height; row++)
            {
                mapped.Slice(checked(row * (int)paddedRowBytes), (int)rowBytes)
                    .CopyTo(pixels.AsSpan(checked(row * (int)rowBytes), (int)rowBytes));
            }

            return new(profile.Width, profile.Height, pixels);
        }
        finally
        {
            readback.Unmap();
        }
    }

    private static async Task MapWithPollingAsync(
        IGPUDevice device,
        IGPUBuffer buffer,
        ulong size)
    {
        var mapping = buffer.MapAsync(
            GPUMapMode.Read,
            0,
            size,
            CancellationToken.None).AsTask();
        var elapsed = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(30);

        while (!mapping.IsCompleted)
        {
            if (elapsed.Elapsed >= timeout)
                throw new TimeoutException($"Native wgpu buffer mapping exceeded {timeout}.");

            device.Poll();
            await Task.Delay(1);
        }

        await mapping;
    }

    private static IGPUBuffer CreateUniformBuffer<T>(
        IGPUDevice device,
        ReadOnlySpan<T> values)
        where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(values);
        var buffer = device.CreateBuffer(new()
        {
            Size = (ulong)bytes.Length,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });
        device.Queue.WriteBuffer(buffer, 0, bytes);
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

    private void AssertNegativeControl(string name, RgbaImage reference, RgbaImage changed)
    {
        var metrics = RgbaImageComparer.Compare(reference, changed);
        output.WriteLine($"negative-control/{name}: {metrics}");
        Assert.False(
            metrics.IsWithin(ImageThresholds.Raymarch),
            $"Negative control '{name}' unexpectedly passed: {metrics}");
    }

    private static RgbaImage FlipX(RgbaImage image)
    {
        var pixels = new byte[image.Pixels.Length];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var source = checked(((y * image.Width) + x) * 4);
                var destination = checked(((y * image.Width) + (image.Width - x - 1)) * 4);
                image.Pixels.AsSpan(source, 4).CopyTo(pixels.AsSpan(destination, 4));
            }
        }

        return new(image.Width, image.Height, pixels);
    }

    private static void WriteFailureArtifacts(
        string profile,
        RgbaImage reference,
        RgbaImage candidate)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "oracle-failures", profile);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "reference.rgba8"), reference.Pixels);
        File.WriteAllBytes(Path.Combine(directory, "candidate.rgba8"), candidate.Pixels);

        var diff = new byte[reference.Pixels.Length];
        for (var offset = 0; offset < diff.Length; offset += 4)
        {
            diff[offset] = (byte)Math.Abs(reference.Pixels[offset] - candidate.Pixels[offset]);
            diff[offset + 1] = (byte)Math.Abs(reference.Pixels[offset + 1] - candidate.Pixels[offset + 1]);
            diff[offset + 2] = (byte)Math.Abs(reference.Pixels[offset + 2] - candidate.Pixels[offset + 2]);
            diff[offset + 3] = 255;
        }

        File.WriteAllBytes(Path.Combine(directory, "diff.rgba8"), diff);
        WritePpm(Path.Combine(directory, "reference.ppm"), reference);
        WritePpm(Path.Combine(directory, "candidate.ppm"), candidate);
        WritePpm(Path.Combine(directory, "diff.ppm"), new(reference.Width, reference.Height, diff));
    }

    private static void WritePpm(string path, RgbaImage image)
    {
        using var stream = File.Create(path);
        var header = System.Text.Encoding.ASCII.GetBytes(
            $"P6\n{image.Width} {image.Height}\n255\n");
        stream.Write(header);
        for (var offset = 0; offset < image.Pixels.Length; offset += 4)
            stream.Write(image.Pixels, offset, 3);
    }
}
