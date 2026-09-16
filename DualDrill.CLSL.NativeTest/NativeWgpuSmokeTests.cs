using System.Diagnostics;
using DualDrill.CLSL.Test.ShaderModule;
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

    [Fact]
    public async Task Invalid_shader_reports_managed_diagnostic()
    {
        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(
            new()
            {
                PowerPreference = GPUPowerPreference.HighPerformance,
                ForceFallbackAdapter = false,
            },
            CancellationToken.None);
        using var device = await adapter.RequestDeviceAsync(new(), CancellationToken.None);

        var error = Assert.ThrowsAny<GraphicsApiException>(
            () => device.CreateShaderModule(new() { Code = "not valid WGSL" }));

        Assert.Contains("parsing error", error.Message);
        Assert.Contains("wgsl:", error.Message);
        device.Poll();
    }

    [Fact]
    public async Task Compiled_triangle_renders_to_offscreen_texture()
    {
        output.WriteLine("native wgpu");

        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL))
            .Emit(new MinimumHelloTriangleShaderModule());
        Assert.False(string.IsNullOrWhiteSpace(wgsl));

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
                pass.Draw(3);
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
            var pixels = readback.GetMappedRange(0, BufferSize);
            Assert.Equal([255, 255, 255, 255], PixelAt(pixels, 32, 32));
            Assert.Equal([0, 0, 0, 255], PixelAt(pixels, 0, 0));
        }
        finally
        {
            readback.Unmap();
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
}
