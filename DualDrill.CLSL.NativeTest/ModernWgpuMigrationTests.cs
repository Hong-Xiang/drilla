using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using DualDrill.ApiGen;
using DualDrill.ApiGen.CodeGen;
using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using WebGPU;
using Xunit.Abstractions;
using static WebGPU.WebGPU;

namespace DualDrill.CLSL.NativeTest;

public sealed class ModernWgpuMigrationTests(ITestOutputHelper output)
{
    private const string NativeSha256 = "ae8cfdc91d436978762d75c56452b287368aff569daad68693de399731487f0b";
    private const uint Width = 65;
    private const uint Height = 64;
    private const uint BytesPerPixel = 4;
    private const uint BytesPerRow = 512;
    private const ulong BufferSize = BytesPerRow * Height;
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compute_pipeline_dispatches_integer_buffers_on_hardware(bool explicitLayout)
    {
        using var context = await NativeContext.CreateAsync();
        var info = await context.Adapter.RequestAdapterInfoAsync(CancellationToken.None);
        Assert.Equal(GPUBackendType.Vulkan, info.BackendType);
        Assert.Equal(GPUAdapterType.DiscreteGPU, info.AdapterType);
        Assert.Contains("NVIDIA", info.Vendor, StringComparison.OrdinalIgnoreCase);
        output.WriteLine($"Compute adapter: {info.BackendType}, {info.AdapterType}, {info.Vendor}, {info.Device}");

        using var shader = context.Device.CreateShaderModule(new()
        {
            Code =
                """
                @group(0) @binding(0) var<storage, read> input: array<i32>;
                @group(0) @binding(1) var<storage, read_write> output: array<i32>;

                @compute @workgroup_size(4)
                fn main(@builtin(global_invocation_id) id: vec3<u32>) {
                    output[id.x] = input[id.x] * 2 + 1;
                }
                """,
        });
        using var input = context.Device.CreateBuffer(new()
        {
            Size = 16,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.Storage,
        });
        using var result = context.Device.CreateBuffer(new()
        {
            Size = 16,
            Usage = GPUBufferUsage.Storage | GPUBufferUsage.CopySrc,
        });
        using var readback = context.Device.CreateBuffer(new()
        {
            Size = 16,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead,
        });
        int[] values = [-7, 0, 13, 99];
        context.Device.Queue.WriteBuffer(input, 0, MemoryMarshal.AsBytes(values.AsSpan()));

        IGPUBindGroupLayout? suppliedLayout = null;
        IGPUPipelineLayout? suppliedPipelineLayout = null;
        try
        {
            if (explicitLayout)
            {
                suppliedLayout = context.Device.CreateBindGroupLayout(new()
                {
                    Entries = new GPUBindGroupLayoutEntry[]
                    {
                        new()
                        {
                            Binding = 0,
                            Visibility = GPUShaderStage.Compute,
                            Buffer = new() { Type = GPUBufferBindingType.ReadOnlyStorage, MinBindingSize = 16 },
                        },
                        new()
                        {
                            Binding = 1,
                            Visibility = GPUShaderStage.Compute,
                            Buffer = new() { Type = GPUBufferBindingType.Storage, MinBindingSize = 16 },
                        },
                    },
                });
                suppliedPipelineLayout = context.Device.CreatePipelineLayout(new()
                {
                    BindGroupLayouts = [suppliedLayout],
                });
            }

            using var pipeline = context.Device.CreateComputePipeline(new()
            {
                Layout = suppliedPipelineLayout,
                Compute = new() { Module = shader, EntryPoint = "main" },
            });
            using var pipelineLayout = pipeline.GetBindGroupLayout(0);
            using var bindGroup = context.Device.CreateBindGroup(new()
            {
                Layout = explicitLayout ? suppliedLayout! : pipelineLayout,
                Entries = new GPUBindGroupEntry[]
                {
                    new() { Binding = 0, Buffer = input, Size = 16 },
                    new() { Binding = 1, Buffer = result, Size = 16 },
                },
            });
            using (var encoder = context.Device.CreateCommandEncoder(new()))
            {
                using (var pass = encoder.BeginComputePass(new()))
                {
                    pass.SetPipeline(pipeline);
                    pass.SetBindGroup(0, bindGroup);
                    pass.DispatchWorkgroups(1);
                    pass.End();
                }
                encoder.CopyBufferToBuffer(result, 0, readback, 0, 16);
                using var commands = encoder.Finish(new());
                context.Device.Queue.Submit([commands]);
            }

            await WaitWithPollingAsync(
                context.Device,
                readback.MapAsync(GPUMapMode.Read, 0, 16, CancellationToken.None).AsTask());
            try
            {
                Assert.Equal(new[] { -13, 1, 27, 199 },
                    MemoryMarshal.Cast<byte, int>(readback.GetMappedRange(0, 16)).ToArray());
            }
            finally
            {
                readback.Unmap();
            }
        }
        finally
        {
            suppliedPipelineLayout?.Dispose();
            suppliedLayout?.Dispose();
        }
    }

    [Fact]
    public async Task Compute_pass_rejects_unsupported_inputs_and_invalid_lifetimes()
    {
        using var context = await NativeContext.CreateAsync();
        using var other = await NativeContext.CreateAsync();
        using var shader = context.Device.CreateShaderModule(new()
        {
            Code = "@compute @workgroup_size(1) fn main() {}",
        });
        using var foreignShader = other.Device.CreateShaderModule(new()
        {
            Code = "@compute @workgroup_size(1) fn main() {}",
        });
        using var pipeline = context.Device.CreateComputePipeline(new()
        {
            Compute = new() { Module = shader, EntryPoint = "main" },
        });
        using var foreignPipeline = other.Device.CreateComputePipeline(new()
        {
            Compute = new() { Module = foreignShader, EntryPoint = "main" },
        });
        using var layout = context.Device.CreateBindGroupLayout(new());
        using var foreignLayout = other.Device.CreateBindGroupLayout(new());
        using var foreignPipelineLayout = other.Device.CreatePipelineLayout(new()
        {
            BindGroupLayouts = [foreignLayout],
        });
        using var foreignBuffer = other.Device.CreateBuffer(new()
        {
            Size = 16,
            Usage = GPUBufferUsage.Storage,
        });
        using var buffer = context.Device.CreateBuffer(new()
        {
            Size = 16,
            Usage = GPUBufferUsage.Storage,
        });
        using var foreignBindGroup = other.Device.CreateBindGroup(new()
        {
            Layout = foreignLayout,
        });
        using var bindGroup = context.Device.CreateBindGroup(new()
        {
            Layout = layout,
        });
        var disposedBuffer = context.Device.CreateBuffer(new()
        {
            Size = 16,
            Usage = GPUBufferUsage.Storage,
        });
        disposedBuffer.Dispose();

        Assert.Throws<ArgumentException>(() => context.Device.CreateComputePipeline(new()
        {
            Compute = new() { EntryPoint = "main" },
        }));
        Assert.Throws<ArgumentException>(() => context.Device.CreateComputePipeline(new()
        {
            Compute = new() { Module = shader },
        }));
        Assert.Throws<ArgumentException>(() => context.Device.CreateComputePipeline(new()
        {
            Compute = new() { Module = Foreign<IGPUShaderModule>(), EntryPoint = "main" },
        }));
        var disposedShader = context.Device.CreateShaderModule(new()
        {
            Code = "@compute @workgroup_size(1) fn main() {}",
        });
        disposedShader.Dispose();
        Assert.Throws<ObjectDisposedException>(() => context.Device.CreateComputePipeline(new()
        {
            Compute = new() { Module = disposedShader, EntryPoint = "main" },
        }));
        Assert.Throws<NotSupportedException>(() => context.Device.CreateComputePipeline(new()
        {
            Compute = new()
            {
                Module = shader,
                EntryPoint = "main",
                Constants = new() { ["unused"] = "1" },
            },
        }));
        Assert.Throws<ArgumentException>(() => context.Device.CreateComputePipeline(new()
        {
            Compute = new() { Module = shader, EntryPoint = "main" },
            Layout = foreignPipelineLayout,
        }));
        Assert.Throws<ArgumentException>(() => context.Device.CreatePipelineLayout(new()
        {
            BindGroupLayouts = [foreignLayout],
        }));
        var disposedLayout = context.Device.CreateBindGroupLayout(new());
        disposedLayout.Dispose();
        Assert.Throws<ObjectDisposedException>(() => context.Device.CreatePipelineLayout(new()
        {
            BindGroupLayouts = [disposedLayout],
        }));
        Assert.Throws<ArgumentException>(() => context.Device.CreateBindGroup(new()
        {
            Layout = foreignLayout,
        }));
        Assert.Throws<ArgumentException>(() => context.Device.CreateBindGroup(new()
        {
            Layout = layout,
            Entries = new GPUBindGroupEntry[] { new() { Buffer = foreignBuffer } },
        }));
        Assert.Throws<ObjectDisposedException>(() => context.Device.CreateBindGroup(new()
        {
            Layout = layout,
            Entries = new GPUBindGroupEntry[] { new() { Buffer = disposedBuffer } },
        }));
        Assert.Throws<ArgumentException>(() => context.Device.CreateBindGroup(new()
        {
            Layout = Foreign<IGPUBindGroupLayout>(),
        }));
        Assert.ThrowsAny<GraphicsApiException>(() => context.Device.CreateBindGroup(new()
        {
            Layout = layout,
            Entries = new GPUBindGroupEntry[] { new() { Binding = 1, Buffer = buffer, Size = 16 } },
        }));
        Assert.ThrowsAny<GraphicsApiException>(() => context.Device.CreateBindGroupLayout(new()
        {
            Entries = new GPUBindGroupLayoutEntry[]
            {
                new() { Binding = 0, Visibility = GPUShaderStage.Compute, Buffer = new() { Type = GPUBufferBindingType.Storage } },
                new() { Binding = 0, Visibility = GPUShaderStage.Compute, Buffer = new() { Type = GPUBufferBindingType.Storage } },
            },
        }));
        Assert.Throws<OverflowException>(() => pipeline.GetBindGroupLayout((ulong)uint.MaxValue + 1));
        Assert.ThrowsAny<GraphicsApiException>(() => context.Device.CreateComputePipeline(new()
        {
            Compute = new() { Module = shader, EntryPoint = "missing" },
        }));

        using var encoder = context.Device.CreateCommandEncoder(new());
        Assert.Throws<NotSupportedException>(() => encoder.BeginComputePass(new()
        {
            TimestampWrites = new() { BeginningOfPassWriteIndex = 1 },
        }));
        using (var pass = encoder.BeginComputePass(new()))
        {
            Assert.Throws<InvalidOperationException>(() => encoder.Finish(new()));
            Assert.Throws<ArgumentException>(() => pass.SetPipeline(foreignPipeline));
            Assert.Throws<ArgumentException>(() => pass.SetPipeline(Foreign<IGPUComputePipeline>()));
            Assert.Throws<ArgumentException>(() => pass.SetBindGroup(0, Foreign<IGPUBindGroup>()));
            Assert.Throws<ArgumentException>(() => pass.SetBindGroup(0, foreignBindGroup));
            Assert.Throws<ArgumentNullException>(() => pass.SetPipeline(null!));
            Assert.Throws<NotSupportedException>(() => pass.SetBindGroup(0, null, [1]));
            var typed = Assert.IsType<GPUComputePassEncoder<WebGPUNETBackend>>(pass);
            Assert.Throws<ArgumentException>(() => typed.SetPipeline(
                Assert.IsType<GPUComputePipeline<WebGPUNETBackend>>(foreignPipeline)));
            Assert.Throws<ArgumentException>(() => typed.SetBindGroup(
                0, Assert.IsType<GPUBindGroup<WebGPUNETBackend>>(foreignBindGroup), default));
            typed.SetBindGroup(0, Assert.IsType<GPUBindGroup<WebGPUNETBackend>>(bindGroup), default);
            pass.SetPipeline(pipeline);
            pass.End();
            Assert.Throws<InvalidOperationException>(() => typed.DispatchWorkgroups(1, 1, 1));
            Assert.Throws<InvalidOperationException>(() => pass.End());
        }
        using var commands = encoder.Finish(new());

        using var abandoned = context.Device.CreateCommandEncoder(new());
        var unended = abandoned.BeginComputePass(new());
        unended.Dispose();
        Assert.Throws<InvalidOperationException>(() => abandoned.Finish(new()));
        Assert.Throws<InvalidOperationException>(() => abandoned.BeginComputePass(new()));
        Assert.Throws<InvalidOperationException>(() => abandoned.BeginRenderPass(new()));
        Assert.Throws<ObjectDisposedException>(() => unended.DispatchWorkgroups(1));

        using var disposedEncoder = context.Device.CreateCommandEncoder(new());
        using var parentPass = disposedEncoder.BeginComputePass(new());
        disposedEncoder.Dispose();
        Assert.Throws<ObjectDisposedException>(() => parentPass.DispatchWorkgroups(1));
        Assert.Throws<ObjectDisposedException>(() => parentPass.End());
        parentPass.Dispose();

        var disposedPipeline = context.Device.CreateComputePipeline(new()
        {
            Compute = new() { Module = shader, EntryPoint = "main" },
        });
        disposedPipeline.Dispose();
        Assert.Throws<ObjectDisposedException>(() => disposedPipeline.GetBindGroupLayout(0));
        using var liveEncoder = context.Device.CreateCommandEncoder(new());
        using var livePass = liveEncoder.BeginComputePass(new());
        Assert.Throws<ObjectDisposedException>(() => livePass.SetPipeline(disposedPipeline));
        livePass.End();
    }

    [Fact]
    public void Compute_pass_generator_excludes_native_bypasses()
    {
        var output = new StringBuilder();
        new WebGPUNativeBackendCodeGen(AlimerWebGPUApi.Create()).EmitAll(output);
        Assert.DoesNotContain("wgpuComputePassEncoder", output.ToString());

        using var descriptorOutput = new StringWriter();
        new GPUStructCodeGen(AlimerWebGPUApi.Create()).EmitStruct(
            descriptorOutput,
            new StructDeclaration("GPUComputePipelineDescriptor", []));
        Assert.Contains("public IGPUPipelineLayout? Layout { get; set; }", descriptorOutput.ToString());
    }

    [Fact]
    public async Task Compute_pass_rejects_a_disposed_device_before_native_use()
    {
        using var context = await NativeContext.CreateAsync();
        using var encoder = context.Device.CreateCommandEncoder(new());
        context.Device.Dispose();

        Assert.Throws<ObjectDisposedException>(() => context.Device.CreateComputePipeline(new()));
        Assert.Throws<ObjectDisposedException>(() => encoder.BeginComputePass(new()));
    }

    [Fact]
    public async Task Failed_native_finish_consumes_compute_command_encoder()
    {
        using var context = await NativeContext.CreateAsync();
        using var encoder = context.Device.CreateCommandEncoder(new());
        using (var pass = encoder.BeginComputePass(new()))
        {
            pass.End();
        }

        Assert.IsType<GPUCommandEncoder<WebGPUNETBackend>>(encoder).PushDebugGroup("unbalanced");
        Assert.ThrowsAny<GraphicsApiException>(() => encoder.Finish(new()));
        Assert.Throws<InvalidOperationException>(() => encoder.Finish(new()));
        Assert.Throws<InvalidOperationException>(() => encoder.BeginComputePass(new()));
    }

    private static T Foreign<T>() where T : class =>
        DispatchProxy.Create<T, ForeignProxy>();

    public sealed class ForeignProxy : DispatchProxy
    {
        protected override object? Invoke(
            System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("Foreign resource was used.");
    }

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
    public void Classifies_only_discrete_and_integrated_adapters_as_hardware()
    {
        Assert.True(WebGPUNETBackend.IsHardwareAdapter(WGPUAdapterType.DiscreteGPU));
        Assert.True(WebGPUNETBackend.IsHardwareAdapter(WGPUAdapterType.IntegratedGPU));
        Assert.False(WebGPUNETBackend.IsHardwareAdapter(WGPUAdapterType.CPU));
        Assert.False(WebGPUNETBackend.IsHardwareAdapter(WGPUAdapterType.Unknown));
    }

    [Fact]
    public void Native_status_errors_are_not_silently_accepted()
    {
        WebGPUNETBackend.ThrowIfNativeFailed(WGPUStatus.Success, "success");
        var error = Assert.ThrowsAny<GraphicsApiException>(
            () => WebGPUNETBackend.ThrowIfNativeFailed(WGPUStatus.Error, "present"));
        Assert.Contains("present failed", error.Message);
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

        using var samplerOutput = new StringWriter();
        new GPUStructCodeGen(nativeModule).EmitStruct(
            samplerOutput,
            new StructDeclaration(
                "GPUSamplerDescriptor",
                [
                    new PropertyDeclaration(
                        "AddressModeU",
                        new OpaqueTypeReference("GPUAddressMode")),
                    new PropertyDeclaration(
                        "LodMaxClamp",
                        new FloatTypeReference(BitWidth._32)),
                    new PropertyDeclaration(
                        "MaxAnisotropy",
                        new IntegerTypeReference(BitWidth._16, false)),
                ]));
        Assert.Contains(
            "AddressModeU { get; set; } = GPUAddressMode.ClampToEdge;",
            samplerOutput.ToString());
        Assert.Contains("LodMaxClamp { get; set; } = 32;", samplerOutput.ToString());
        Assert.Contains("MaxAnisotropy { get; set; } = 1;", samplerOutput.ToString());
    }

    [Fact]
    public async Task Selects_a_hardware_Vulkan_adapter_and_copies_info()
    {
        using var context = await NativeContext.CreateAsync();
        var info = await context.Adapter.RequestAdapterInfoAsync(CancellationToken.None);

        Assert.Equal(GPUBackendType.Vulkan, info.BackendType);
        Assert.Equal(GPUAdapterType.DiscreteGPU, info.AdapterType);
        Assert.Contains("NVIDIA", info.Vendor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A800", info.Device, StringComparison.OrdinalIgnoreCase);
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
        for (var iteration = 0; iteration < 16; iteration++)
        {
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
    }

    [Fact]
    public async Task Queue_work_cancellation_is_prompt_but_native_callback_still_drains()
    {
        using var context = await NativeContext.CreateAsync();
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();

        var preCancelledTask = context.Device.Queue
            .OnSubmittedWorkDoneAsync(preCancelled.Token)
            .AsTask();
        Assert.True(preCancelledTask.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preCancelledTask);

        using var cancellation = new CancellationTokenSource();
        var cancelledTask = context.Device.Queue
            .OnSubmittedWorkDoneAsync(cancellation.Token)
            .AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancelledTask.WaitAsync(CallbackTimeout));

        context.Device.Poll();
        await WaitWithPollingAsync(
            context.Device,
            context.Device.Queue.OnSubmittedWorkDoneAsync(CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Successful_map_wins_before_late_cancellation()
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
        await WaitWithPollingAsync(context.Device, map);

        cancellation.Cancel();
        try
        {
            Assert.Equal(4, buffer.GetMappedRange(0, 4).Length);
        }
        finally
        {
            buffer.Unmap();
        }
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

        using var autoPipeline = context.Device.CreateRenderPipeline(new()
        {
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
        });
        using var autoLayout = autoPipeline.GetBindGroupLayout(0);
        using var autoBindGroup = context.Device.CreateBindGroup(new()
        {
            Layout = autoLayout,
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Buffer = uniform },
            },
        });
        Assert.Equal([0, 0, 255, 255], await RenderFrameAsync(
            context.Device, autoPipeline, autoBindGroup, uniform, texture, readback,
            new Vector4(1, 0, 0, 1)));

        using var other = await NativeContext.CreateAsync();
        Assert.Throws<ArgumentException>(() => other.Device.CreateBindGroup(new()
        {
            Layout = autoLayout,
        }));
        var disposedAutoLayout = autoPipeline.GetBindGroupLayout(0);
        disposedAutoLayout.Dispose();
        Assert.Throws<ObjectDisposedException>(() => context.Device.CreateBindGroup(new()
        {
            Layout = disposedAutoLayout,
        }));
    }

    [Fact]
    public async Task Default_and_explicit_sampler_descriptors_are_valid()
    {
        var descriptor = new GPUSamplerDescriptor();
        Assert.Equal(GPUAddressMode.ClampToEdge, descriptor.AddressModeU);
        Assert.Equal(GPUAddressMode.ClampToEdge, descriptor.AddressModeV);
        Assert.Equal(GPUAddressMode.ClampToEdge, descriptor.AddressModeW);
        Assert.Equal(GPUFilterMode.Nearest, descriptor.MagFilter);
        Assert.Equal(GPUFilterMode.Nearest, descriptor.MinFilter);
        Assert.Equal(GPUMipmapFilterMode.Nearest, descriptor.MipmapFilter);
        Assert.Equal(0, descriptor.LodMinClamp);
        Assert.Equal(32, descriptor.LodMaxClamp);
        Assert.Equal(1, descriptor.MaxAnisotropy);
        Assert.Equal(0, default(GPUSamplerDescriptor).MaxAnisotropy);

        using var context = await NativeContext.CreateAsync();
        var defaultSampler = context.Device.CreateSampler(descriptor);
        using var defaultSamplerLifetime = (IDisposable)defaultSampler;
        var explicitSampler = context.Device.CreateSampler(new()
        {
            AddressModeU = GPUAddressMode.Repeat,
            AddressModeV = GPUAddressMode.MirrorRepeat,
            AddressModeW = GPUAddressMode.ClampToEdge,
            MagFilter = GPUFilterMode.Linear,
            MinFilter = GPUFilterMode.Linear,
            MipmapFilter = GPUMipmapFilterMode.Linear,
            LodMinClamp = 0,
            LodMaxClamp = 0,
            MaxAnisotropy = 1,
        });
        using var explicitSamplerLifetime = (IDisposable)explicitSampler;
    }

    [Fact]
    public async Task Omitted_single_image_copy_strides_use_native_undefined_sentinels()
    {
        using var context = await NativeContext.CreateAsync();
        using var texture = context.Device.CreateTexture(new()
        {
            Size = new()
            {
                Width = 1,
                Height = 1,
                DepthOrArrayLayers = 1,
            },
            Format = GPUTextureFormat.BGRA8Unorm,
            Usage = GPUTextureUsage.CopyDst | GPUTextureUsage.CopySrc,
        });
        using var readback = context.Device.CreateBuffer(new()
        {
            Size = 4,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead,
        });
        using var upload = context.Device.CreateBuffer(new()
        {
            Size = 4,
            Usage = GPUBufferUsage.CopySrc | GPUBufferUsage.CopyDst,
        });
        var extent = new GPUExtent3D
        {
            Width = 1,
            Height = 1,
            DepthOrArrayLayers = 1,
        };

        byte[] queueWrite = [17, 34, 51, 255];
        context.Device.Queue.WriteTexture(
            new() { Texture = texture },
            queueWrite,
            new(),
            extent);
        Assert.Equal(
            queueWrite,
            await ReadTexturePixelAsync(context.Device, texture, readback, extent));

        byte[] bufferCopy = [68, 85, 102, 255];
        context.Device.Queue.WriteBuffer(upload, 0, bufferCopy);
        using (var encoder = context.Device.CreateCommandEncoder(new()))
        {
            Assert.IsType<GPUCommandEncoder<WebGPUNETBackend>>(encoder).CopyBufferToTexture(
                new()
                {
                    Buffer = upload,
                    Layout = new(),
                },
                new() { Texture = texture },
                extent);
            using var commands = encoder.Finish(new());
            context.Device.Queue.Submit([commands]);
        }

        Assert.Equal(
            bufferCopy,
            await ReadTexturePixelAsync(context.Device, texture, readback, extent));
    }

    private static async Task<byte[]> ReadTexturePixelAsync(
        IGPUDevice device,
        IGPUTexture texture,
        IGPUBuffer readback,
        GPUExtent3D extent)
    {
        using (var encoder = device.CreateCommandEncoder(new()))
        {
            encoder.CopyTextureToBuffer(
                new() { Texture = texture },
                new()
                {
                    Buffer = readback,
                    Layout = new(),
                },
                extent);
            using var commands = encoder.Finish(new());
            device.Queue.Submit([commands]);
        }

        await WaitWithPollingAsync(
            device,
            readback.MapAsync(
                GPUMapMode.Read,
                0,
                4,
                CancellationToken.None).AsTask());
        try
        {
            return readback.GetMappedRange(0, 4).ToArray();
        }
        finally
        {
            readback.Unmap();
        }
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
