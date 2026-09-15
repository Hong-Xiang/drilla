using System.Diagnostics;
using DualDrill.CLSL;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Compiler.Server;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using Xunit.Abstractions;

namespace DualDrill.CLSL.NativeTest;

public sealed class NativeWgpuSmokeTests(ITestOutputHelper output)
{
    private const uint TextureSize = 64;
    private const uint BytesPerRow = 256;
    private const ulong BufferSize = BytesPerRow * TextureSize;
    private static readonly TimeSpan MapTimeout = TimeSpan.FromSeconds(10);
    private static readonly float[] FullScreenVertices =
    [
        -1, -1,
        1, -1,
        -1, 1,
        -1, 1,
        1, -1,
        1, 1,
    ];

    [Theory]
    [InlineData(ShaderProfile.Triangle)]
    [InlineData(ShaderProfile.Uniform)]
    [InlineData(ShaderProfile.Mandelbrot)]
    [InlineData(ShaderProfile.Raymarching)]
    public async Task Compiled_shader_renders_to_offscreen_texture(ShaderProfile profile)
    {
        output.WriteLine($"native wgpu: {profile}");

        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL))
            .Emit(CreateShader(profile));
        Assert.False(string.IsNullOrWhiteSpace(wgsl));
        AssertGeneratedInterface(profile, wgsl);

        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new()
            {
                PowerPreference = GPUPowerPreference.HighPerformance,
                ForceFallbackAdapter = false,
            },
            CancellationToken.None);
        using var device = await adapter.RequestDeviceAsync(new(), CancellationToken.None);
        using var shader = device.CreateShaderModule(new() { Code = wgsl });
        using var pipeline = device.CreateRenderPipeline(new()
        {
            Vertex = new()
            {
                Module = shader,
                EntryPoint = "vs",
                Buffers = VertexBuffers(profile),
            },
            Fragment = new()
            {
                Module = shader,
                EntryPoint = "fs",
                Targets = new GPUColorTargetState[]
                {
                    new()
                    {
                        Format = GPUTextureFormat.RGBA8Unorm,
                        WriteMask = GPUColorWriteMask.All,
                    },
                },
            },
            Primitive = new()
            {
                Topology = GPUPrimitiveTopology.TriangleList,
            },
        });
        using var texture = device.CreateTexture(new()
        {
            Size = new()
            {
                Width = TextureSize,
                Height = TextureSize,
                DepthOrArrayLayers = 1,
            },
            Format = GPUTextureFormat.RGBA8Unorm,
            Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
        });
        using var view = texture.CreateView();
        using var readback = device.CreateBuffer(new()
        {
            Size = BufferSize,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead,
        });

        var resources = CreateDrawResources(device, pipeline, profile);
        try
        {
            using (var encoder = device.CreateCommandEncoder(new()))
            {
                using (var pass = encoder.BeginRenderPass(new()
                {
                    ColorAttachments = new GPURenderPassColorAttachment[]
                    {
                        new()
                        {
                            View = view,
                            ClearValue = new() { R = 0, G = 0, B = 0, A = 0 },
                            LoadOp = GPULoadOp.Clear,
                            StoreOp = GPUStoreOp.Store,
                        },
                    },
                }))
                {
                    pass.SetPipeline(pipeline);
                    if (resources.BindGroup is not null)
                    {
                        pass.SetBindGroup(0, resources.BindGroup);
                    }
                    if (resources.VertexBuffer is not null)
                    {
                        pass.SetVertexBuffer(0, resources.VertexBuffer);
                    }
                    pass.Draw(IsFullScreen(profile) ? 6u : 3u);
                    pass.End();
                }

                encoder.CopyTextureToBuffer(
                    new() { Texture = texture },
                    new()
                    {
                        Buffer = readback,
                        Layout = new()
                        {
                            BytesPerRow = BytesPerRow,
                            RowsPerImage = TextureSize,
                        },
                    },
                    new()
                    {
                        Width = TextureSize,
                        Height = TextureSize,
                        DepthOrArrayLayers = 1,
                    });

                using var commands = encoder.Finish(new());
                device.Queue.Submit([commands]);
            }

            await MapWithPollingAsync(device, readback);
            try
            {
                AssertPixels(profile, readback.GetMappedRange(0, BufferSize));
            }
            finally
            {
                readback.Unmap();
            }
        }
        finally
        {
            foreach (var buffer in resources.Buffers)
            {
                buffer.Dispose();
            }
        }
    }

    private static ISharpShader CreateShader(ShaderProfile profile) =>
        profile switch
        {
            ShaderProfile.Triangle => new MinimumTriangleShader(),
            ShaderProfile.Uniform => new SimpleStructUniformShaderModule(),
            ShaderProfile.Mandelbrot => new MandelbrotDistanceShaderModule(),
            ShaderProfile.Raymarching => new RaymarchingPrimitiveShader(),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
        };

    private static void AssertGeneratedInterface(ShaderProfile profile, string wgsl)
    {
        Assert.Contains("fn vs", wgsl);
        Assert.Contains("fn fs", wgsl);
        switch (profile)
        {
            case ShaderProfile.Triangle:
                Assert.Contains("@builtin(vertex_index)", wgsl);
                break;
            case ShaderProfile.Uniform:
                Assert.Contains("@binding(0) @group(0) var<uniform>", wgsl);
                Assert.Contains("color", wgsl);
                Assert.Contains("scale", wgsl);
                Assert.Contains("offset", wgsl);
                break;
            case ShaderProfile.Mandelbrot:
                Assert.Contains("@binding(0) @group(0) var<uniform>", wgsl);
                Assert.Contains("@location(0) position", wgsl);
                break;
            case ShaderProfile.Raymarching:
                Assert.Contains("@binding(0) @group(0) var<uniform>", wgsl);
                Assert.Contains("@binding(1) @group(0) var<uniform>", wgsl);
                Assert.Contains("@location(0) position", wgsl);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, null);
        }
    }

    private static ReadOnlyMemory<GPUVertexBufferLayout> VertexBuffers(
        ShaderProfile profile) =>
        IsFullScreen(profile)
            ? new GPUVertexBufferLayout[]
            {
                new()
                {
                    ArrayStride = 8,
                    StepMode = GPUVertexStepMode.Vertex,
                    Attributes = new GPUVertexAttribute[]
                    {
                        new()
                        {
                            ShaderLocation = 0,
                            Offset = 0,
                            Format = GPUVertexFormat.Float32x2,
                        },
                    },
                },
            }
            : ReadOnlyMemory<GPUVertexBufferLayout>.Empty;

    private static DrawResources CreateDrawResources(
        IGPUDevice device,
        IGPURenderPipeline pipeline,
        ShaderProfile profile)
    {
        var buffers = new List<IGPUBuffer>();
        IGPUBindGroup? bindGroup = null;
        IGPUBuffer? vertexBuffer = null;
        try
        {
            if (IsFullScreen(profile))
            {
                vertexBuffer = CreateBuffer(
                    device,
                    (ulong)(FullScreenVertices.Length * sizeof(float)),
                    GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst,
                    FullScreenVertices);
                buffers.Add(vertexBuffer);
            }

            GPUBindGroupEntry[] entries = profile switch
            {
                ShaderProfile.Triangle => [],
                ShaderProfile.Uniform =>
                [
                    UniformEntry(
                        buffers,
                        device,
                        binding: 0,
                        size: 32,
                        [0.1f, 0.65f, 1, 1, 0.7f, 0.7f, 0.1f, 0]),
                ],
                ShaderProfile.Mandelbrot =>
                [
                    UniformEntry(buffers, device, binding: 0, size: 16, [0]),
                ],
                ShaderProfile.Raymarching =>
                [
                    UniformEntry(buffers, device, binding: 0, size: 16, [800, 600]),
                    UniformEntry(buffers, device, binding: 1, size: 16, [0]),
                ],
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
            };

            if (entries.Length > 0)
            {
                var layout = pipeline.GetBindGroupLayout(0);
                bindGroup = device.CreateBindGroup(new()
                {
                    Layout = layout,
                    Entries = entries,
                });
            }
            return new(bindGroup, vertexBuffer, buffers);
        }
        catch
        {
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
            throw;
        }
    }

    private static GPUBindGroupEntry UniformEntry(
        List<IGPUBuffer> buffers,
        IGPUDevice device,
        int binding,
        ulong size,
        float[] data)
    {
        var buffer = CreateBuffer(
            device,
            size,
            GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
            data);
        buffers.Add(buffer);
        return new()
        {
            Binding = binding,
            Buffer = buffer,
            Offset = 0,
            Size = size,
        };
    }

    private static IGPUBuffer CreateBuffer(
        IGPUDevice device,
        ulong size,
        GPUBufferUsage usage,
        float[] data)
    {
        var buffer = device.CreateBuffer(new() { Size = size, Usage = usage });
        try
        {
            device.Queue.WriteBuffer<float>(buffer, 0, data);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static bool IsFullScreen(ShaderProfile profile) =>
        profile is ShaderProfile.Mandelbrot or ShaderProfile.Raymarching;

    private static void AssertPixels(ShaderProfile profile, ReadOnlySpan<byte> pixels)
    {
        switch (profile)
        {
            case ShaderProfile.Triangle:
                AssertPixelClose([255, 82, 31, 255], PixelAt(pixels, 32, 32));
                Assert.Equal([0, 0, 0, 0], PixelAt(pixels, 0, 0));
                break;
            case ShaderProfile.Uniform:
                AssertPixelClose([26, 166, 255, 255], PixelAt(pixels, 32, 32));
                Assert.Equal([0, 0, 0, 0], PixelAt(pixels, 0, 0));
                break;
            case ShaderProfile.Mandelbrot:
            case ShaderProfile.Raymarching:
                Assert.Equal(255, PixelAt(pixels, 16, 24)[3]);
                Assert.Equal(255, PixelAt(pixels, 48, 40)[3]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, null);
        }
    }

    private static void AssertPixelClose(byte[] expected, byte[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var channel = 0; channel < expected.Length; channel++)
        {
            Assert.InRange(actual[channel], expected[channel] - 1, expected[channel] + 1);
        }
    }

    private static async Task MapWithPollingAsync(IGPUDevice device, IGPUBuffer buffer)
    {
        var map = buffer.MapAsync(GPUMapMode.Read, 0, BufferSize, CancellationToken.None).AsTask();
        var elapsed = Stopwatch.StartNew();

        while (!map.IsCompleted)
        {
            if (elapsed.Elapsed >= MapTimeout)
            {
                throw new TimeoutException($"Native wgpu buffer mapping exceeded {MapTimeout}.");
            }

            device.Poll();
            await Task.Delay(1);
        }

        await map;
    }

    private static byte[] PixelAt(ReadOnlySpan<byte> pixels, int x, int y)
    {
        var offset = checked((y * (int)BytesPerRow) + (x * 4));
        return pixels.Slice(offset, 4).ToArray();
    }

    public enum ShaderProfile
    {
        Triangle,
        Uniform,
        Mandelbrot,
        Raymarching,
    }

    private readonly record struct DrawResources(
        IGPUBindGroup? BindGroup,
        IGPUBuffer? VertexBuffer,
        IReadOnlyList<IGPUBuffer> Buffers);
}
