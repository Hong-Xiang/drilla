using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using DualDrill.ApiGen;
using DualDrill.ApiGen.CodeGen;
using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using WebGPU;
using static WebGPU.WebGPU;

namespace DualDrill.CLSL.NativeTest;

public sealed class ModernWgpuMigrationTests
{
    private const string NativeSha256 = "ae8cfdc91d436978762d75c56452b287368aff569daad68693de399731487f0b";
    private const uint Width = 65;
    private const uint Height = 64;
    private const uint BytesPerPixel = 4;
    private const uint BytesPerRow = 512;
    private const ulong BufferSize = BytesPerRow * Height;
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void Loads_the_pinned_Alimer_wgpu_native_pair()
    {
        Assert.Equal(new Version(1, 6, 0, 0), typeof(WGPUInstance).Assembly.GetName().Version);
        Assert.Equal(0x1B000400u, wgpuGetVersion());

        var depsPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{typeof(ModernWgpuMigrationTests).Assembly.GetName().Name}.deps.json");
        var deps = File.ReadAllText(depsPath);
        Assert.Contains("Alimer.Bindings.WebGPU/1.6.0", deps);
        Assert.Contains("Alimer.WebGPU.Native/1.0.4", deps);
        Assert.DoesNotContain("Evergine.Bindings.WebGPU", deps);

        var nativePath = Assert.Single(
            File.ReadLines("/proc/self/maps")
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last())
                .Where(path => path.EndsWith("/libwgpu_native.so", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal));
        Assert.Equal(
            NativeSha256,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativePath))).ToLowerInvariant());
    }

    [Fact]
    public void Maps_enums_by_semantic_name_and_rejects_unknown_flags()
    {
        Assert.NotEqual(
            (int)GPUBufferMapState.Mapped,
            (int)WGPUBufferMapState.Mapped);
        Assert.Equal(
            WGPUBufferMapState.Mapped,
            WebGPUNETBackend.MapEnumByName<GPUBufferMapState, WGPUBufferMapState>(
                GPUBufferMapState.Mapped));
        Assert.Equal(
            WGPUBufferUsage.MapRead | WGPUBufferUsage.CopyDst,
            WebGPUNETBackend.MapEnumByName<GPUBufferUsage, WGPUBufferUsage>(
                GPUBufferUsage.MapRead | GPUBufferUsage.CopyDst));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WebGPUNETBackend.MapEnumByName<GPUBufferUsage, WGPUBufferUsage>(
                (GPUBufferUsage)(1 << 30)));
    }

    [Fact]
    public void Generator_discovers_Alimer_handles_and_emits_semantic_enum_mapping()
    {
        var nativeModule = AlimerWebGPUApi.Create();
        Assert.Contains(nativeModule.Handles, handle => handle.Name == "WGPUAdapter");
        Assert.Equal(
            "BGRA8Unorm",
            AlimerWebGPUApi.GetNativeEnumMemberName(
                "GPUTextureFormat",
                "bgra8unorm",
                nativeModule));
        Assert.Equal(
            "Unknown",
            AlimerWebGPUApi.GetNativeEnumMemberName(
                "GPUDeviceLostReason",
                "unknown",
                nativeModule));
        Assert.Equal(
            "Undefined",
            AlimerWebGPUApi.GetManagedEnumMemberName(
                "GPUDeviceLostReason",
                "unknown"));

        var output = new StringBuilder();
        new WebGPUNativeBackendCodeGen(nativeModule).EmitEnumToNative(
            output,
            new EnumDeclaration("GPUBufferMapState", [], false));

        Assert.Contains(
            "MapEnumByName<GPUBufferMapState, WGPUBufferMapState>(value)",
            output.ToString());
        Assert.DoesNotContain("=> (WGPUBufferMapState)(value)", output.ToString());

        output.Clear();
        new WebGPUNativeBackendCodeGen(nativeModule).EmitEnumToNative(
            output,
            new EnumDeclaration("GPUIndexFormat", [], false));
        Assert.Contains("WGPUIndexFormat.Undefined", output.ToString());
    }

    [Fact]
    public async Task Selects_a_hardware_Vulkan_adapter_and_copies_info()
    {
        using var context = await NativeContext.CreateAsync();
        var info = await context.Adapter.RequestAdapterInfoAsync(CancellationToken.None);

        Assert.Contains("NVIDIA", info.Vendor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A800", info.Device, StringComparison.OrdinalIgnoreCase);

        var concrete = Assert.IsType<GPUAdapter<WebGPUNETBackend>>(context.Adapter);
        var status = wgpuAdapterGetInfo(new WGPUAdapter(concrete.Handle.Pointer), out var nativeInfo);
        Assert.Equal(WGPUStatus.Success, status);
        try
        {
            Assert.Equal(WGPUBackendType.Vulkan, nativeInfo.backendType);
            Assert.Equal(WGPUAdapterType.DiscreteGPU, nativeInfo.adapterType);
        }
        finally
        {
            wgpuAdapterInfoFreeMembers(nativeInfo);
        }
    }

    [Fact]
    public async Task Invalid_utf8_shader_reports_a_managed_diagnostic()
    {
        using var context = await NativeContext.CreateAsync();

        var error = Assert.ThrowsAny<GraphicsApiException>(
            () => context.Device.CreateShaderModule(new()
            {
                Label = "无效着色器",
                Code = "/* 非 ASCII 注释 */ not valid WGSL",
            }));

        Assert.Contains("parsing", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Map_cancellation_keeps_callback_state_alive_until_native_completion()
    {
        using var context = await NativeContext.CreateAsync();
        using var buffer = context.Device.CreateBuffer(new()
        {
            Size = 4,
            Usage = GPUBufferUsage.MapRead | GPUBufferUsage.CopyDst,
        });
        using var cancellation = new CancellationTokenSource();

        var map = buffer.MapAsync(
            GPUMapMode.Read,
            0,
            4,
            cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => WaitWithPollingAsync(context.Device, map));
    }

    [Fact]
    public async Task Renders_multiple_uniform_frames_to_padded_BGRA_readback()
    {
        using var context = await NativeContext.CreateAsync();
        using var shader = context.Device.CreateShaderModule(new()
        {
            Code =
                """
                struct Tint {
                    color: vec4<f32>,
                }

                @group(0) @binding(0)
                var<uniform> tint: Tint;

                @vertex
                fn vs(@builtin(vertex_index) vertex_index: u32) -> @builtin(position) vec4<f32> {
                    let positions = array(
                        vec2<f32>(-0.8, -0.8),
                        vec2<f32>( 0.8, -0.8),
                        vec2<f32>( 0.0,  0.8),
                    );
                    return vec4<f32>(positions[vertex_index], 0.0, 1.0);
                }

                @fragment
                fn fs() -> @location(0) vec4<f32> {
                    return tint.color;
                }
                """,
        });
        var bindGroupLayout = context.Device.CreateBindGroupLayout(new()
        {
            Entries = new GPUBindGroupLayoutEntry[]
            {
                new()
                {
                    Binding = 0,
                    Visibility = GPUShaderStage.Fragment,
                    Buffer = new()
                    {
                        Type = GPUBufferBindingType.Uniform,
                        MinBindingSize = 16,
                    },
                },
            },
        });
        using var bindGroupLayoutLifetime = (IDisposable)bindGroupLayout;
        using var pipelineLayout = context.Device.CreatePipelineLayout(new()
        {
            BindGroupLayouts = [bindGroupLayout],
        });
        using var pipeline = context.Device.CreateRenderPipeline(new()
        {
            Layout = pipelineLayout,
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
                        Format = GPUTextureFormat.BGRA8Unorm,
                        WriteMask = GPUColorWriteMask.All,
                    },
                },
            },
            Primitive = new()
            {
                Topology = GPUPrimitiveTopology.TriangleList,
            },
        });
        using var uniform = context.Device.CreateBuffer(new()
        {
            Size = 16,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });
        var bindGroup = context.Device.CreateBindGroup(new()
        {
            Layout = bindGroupLayout,
            Entries = new GPUBindGroupEntry[]
            {
                new()
                {
                    Binding = 0,
                    Buffer = uniform,
                    Size = 16,
                },
            },
        });
        using var bindGroupLifetime = (IDisposable)bindGroup;
        using var texture = context.Device.CreateTexture(new()
        {
            Size = new()
            {
                Width = Width,
                Height = Height,
                DepthOrArrayLayers = 1,
            },
            Format = GPUTextureFormat.BGRA8Unorm,
            Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
        });
        using var readback = context.Device.CreateBuffer(new()
        {
            Size = BufferSize,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead,
        });

        var red = await RenderFrameAsync(
            context.Device,
            pipeline,
            bindGroup,
            uniform,
            texture,
            readback,
            new Vector4(1, 0, 0, 1));
        var green = await RenderFrameAsync(
            context.Device,
            pipeline,
            bindGroup,
            uniform,
            texture,
            readback,
            new Vector4(0, 1, 0, 1));

        Assert.Equal([0, 0, 255, 255], red);
        Assert.Equal([0, 255, 0, 255], green);
        Assert.NotEqual(red, green);
    }

    private static async Task<byte[]> RenderFrameAsync(
        IGPUDevice device,
        IGPURenderPipeline pipeline,
        IGPUBindGroup bindGroup,
        IGPUBuffer uniform,
        IGPUTexture texture,
        IGPUBuffer readback,
        Vector4 color)
    {
        device.Queue.WriteBuffer(uniform, 0, new[] { color.X, color.Y, color.Z, color.W });
        using (var view = texture.CreateView())
        using (var encoder = device.CreateCommandEncoder(new()))
        {
            using (var pass = encoder.BeginRenderPass(new()
            {
                ColorAttachments = new GPURenderPassColorAttachment[]
                {
                    new()
                    {
                        View = view,
                        ClearValue = new() { A = 1 },
                        LoadOp = GPULoadOp.Clear,
                        StoreOp = GPUStoreOp.Store,
                    },
                },
            }))
            {
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(0, bindGroup);
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
                        RowsPerImage = Height,
                    },
                },
                new()
                {
                    Width = Width,
                    Height = Height,
                    DepthOrArrayLayers = 1,
                });

            using var commands = encoder.Finish(new());
            device.Queue.Submit([commands]);
        }

        await WaitWithPollingAsync(
            device,
            device.Queue.OnSubmittedWorkDoneAsync(CancellationToken.None).AsTask());
        await WaitWithPollingAsync(
            device,
            readback.MapAsync(
                GPUMapMode.Read,
                0,
                BufferSize,
                CancellationToken.None).AsTask());
        try
        {
            var pixels = readback.GetMappedRange(0, BufferSize);
            var centerOffset = checked(((int)Height / 2 * (int)BytesPerRow) + ((int)Width / 2 * 4));
            Assert.Equal(0, pixels[(int)(Width * BytesPerPixel)]);
            return pixels.Slice(centerOffset, 4).ToArray();
        }
        finally
        {
            readback.Unmap();
        }
    }

    private static async Task WaitWithPollingAsync(IGPUDevice device, Task operation)
    {
        var elapsed = Stopwatch.StartNew();
        while (!operation.IsCompleted)
        {
            if (elapsed.Elapsed >= CallbackTimeout)
            {
                throw new TimeoutException($"Native callback exceeded {CallbackTimeout}.");
            }

            device.Poll();
            await Task.Delay(1);
        }

        await operation;
    }

    private sealed class NativeContext(
        IGPUInstance instance,
        IGPUAdapter adapter,
        IGPUDevice device) : IDisposable
    {
        public IGPUAdapter Adapter { get; } = adapter;
        public IGPUDevice Device { get; } = device;

        public static async Task<NativeContext> CreateAsync()
        {
            var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
            try
            {
                var adapter = await instance.RequestAdapterAsync(
                    new()
                    {
                        PowerPreference = GPUPowerPreference.HighPerformance,
                        ForceFallbackAdapter = false,
                        BackendType = GPUBackendType.Vulkan,
                    },
                    CancellationToken.None);
                try
                {
                    var device = await adapter.RequestDeviceAsync(new(), CancellationToken.None);
                    return new NativeContext(instance, adapter, device);
                }
                catch
                {
                    adapter.Dispose();
                    throw;
                }
            }
            catch
            {
                instance.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Device.Dispose();
            Adapter.Dispose();
            instance.Dispose();
        }
    }
}
