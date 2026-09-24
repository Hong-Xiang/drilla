using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Reflection;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using DualDrill.Mathematics;
using DualDrill.Shaders;
using Xunit.Abstractions;

namespace DualDrill.CLSL.NativeTest;

public sealed class NativeComputeDifferentialTests(ITestOutputHelper output)
{
    private static readonly int[] Inputs = [-2, 0, 1, 2, 3, 4];
    private static readonly int[] Goldens = [-9, 12, 16, 28, 1011, 56];
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Clr_scalar_reference_matches_fixed_lane_goldens()
    {
        var reference = Inputs.Select(ScoreShader.Score).ToArray();
        AssertLanes(Goldens, reference);
    }

    [Fact]
    public void Corrupted_lane_fails_the_same_exact_comparison()
    {
        var corrupted = (int[])Goldens.Clone();
        corrupted[4] = 1010;

        var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertLanes(Goldens, corrupted));
        Assert.Contains("lane 4: expected 1011, actual 1010", error.Message);
    }

    [Fact]
    public void Actual_scalar_cil_has_a_switch_and_entry_uses_unsigned_buffer_guards()
    {
        var scalar = CilMethodDecoder.Decode(((Func<int, int>)ScoreShader.Score).Method).Instructions;
        Assert.Contains(scalar, instruction => instruction.Instruction.OpCode == OpCodes.Switch);

        var entry = CilMethodDecoder.Decode(
            typeof(ScoreShader).GetMethod(nameof(ScoreShader.Run))!).Instructions;
        Assert.Equal(2, entry.Count(instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: "get_Length" }));
        Assert.Single(entry, instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: "get_Item" });
        Assert.Single(entry, instruction =>
            instruction.Instruction.Operand is MethodInfo { Name: "set_Item" });
        Assert.Equal(2, entry.Count(instruction =>
            instruction.Instruction.OpCode is var opcode &&
            (opcode == OpCodes.Bge_Un || opcode == OpCodes.Bge_Un_S || opcode == OpCodes.Clt_Un)));
        Assert.DoesNotContain(entry, instruction => instruction.Instruction.OpCode == OpCodes.Conv_I4);
    }

    [Fact]
    [Trait("Category", "GPU")]
    public async Task Public_CSharp_compute_matches_clr_on_hardware()
    {
        var artifactDirectory = Directory.CreateDirectory(
            Path.Combine(AppContext.BaseDirectory, "compute-differential", Guid.NewGuid().ToString("N"))).FullName;
        output.WriteLine($"Artifacts: {artifactDirectory}");
        var method = ((Func<int, int>)ScoreShader.Score).Method;
        File.WriteAllText(
            Path.Combine(artifactDirectory, "source.txt"),
            $"{method.DeclaringType?.Assembly.FullName}{Environment.NewLine}" +
            $"MVID={method.Module.ModuleVersionId}, token={method.MetadataToken}{Environment.NewLine}" +
            $"Input=[{string.Join(", ", Inputs)}], golden=[{string.Join(", ", Goldens)}]");
        output.WriteLine($"C# source: {method.DeclaringType?.Assembly.FullName}, MVID={method.Module.ModuleVersionId}");

        var source = new ScoreShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.WGSL));
        var parsed = compiler.Parse(source);
        AssertEntryGuardsDominateAccesses(parsed);
        var reflection = new ShaderModuleReflection();
        var storage = reflection.GetStorageBufferBindings(parsed);
        Assert.Collection(storage,
            input =>
            {
                Assert.Equal((0, 0, GPUBufferBindingType.ReadOnlyStorage, 4u),
                    (input.Group, input.Binding, input.Kind, input.ElementStride));
            },
            result =>
            {
                Assert.Equal((0, 1, GPUBufferBindingType.Storage, 4u),
                    (result.Group, result.Binding, result.Kind, result.ElementStride));
            });

        var wgsl = compiler.Emit(source);
        File.WriteAllText(Path.Combine(artifactDirectory, "candidate.wgsl"), wgsl);
        Assert.Contains("@compute", wgsl);
        Assert.Contains("var<storage, read>", wgsl);
        Assert.Contains("var<storage, read_write>", wgsl);

        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(new()
        {
            PowerPreference = GPUPowerPreference.HighPerformance,
            ForceFallbackAdapter = false,
            BackendType = GPUBackendType.Vulkan,
        }, CancellationToken.None);
        var info = await adapter.RequestAdapterInfoAsync(CancellationToken.None);
        output.WriteLine($"Adapter: {info.BackendType}, {info.AdapterType}, {info.Vendor}, {info.Device}");
        File.WriteAllText(Path.Combine(artifactDirectory, "adapter.txt"),
            $"{info.BackendType}, {info.AdapterType}, {info.Vendor}, {info.Device}");
        Assert.Equal(GPUBackendType.Vulkan, info.BackendType);
        Assert.True(info.AdapterType is GPUAdapterType.DiscreteGPU or GPUAdapterType.IntegratedGPU,
            $"Expected hardware adapter, got {info.AdapterType}.");
        using var device = await adapter.RequestDeviceAsync(new(), CancellationToken.None);
        using var shader = device.CreateShaderModule(new() { Code = wgsl });
        using var bindGroupLayout = device.CreateBindGroupLayout(
            reflection.GetBindGroupLayoutDescriptor(parsed, 0));
        using var pipelineLayout = device.CreatePipelineLayout(new()
        {
            BindGroupLayouts = [bindGroupLayout],
        });
        using var pipeline = device.CreateComputePipeline(new()
        {
            Layout = pipelineLayout,
            Compute = new() { Module = shader, EntryPoint = nameof(ScoreShader.Run) },
        });

        var extendedInput = new[] { -2, 0, 1, 2, 3, 4, 0, 1 };
        var scenarios = new (string Name, int[] Input, int OutputCount)[]
        {
            ("input-6-output-8", Inputs, 8),
            ("input-8-output-6", extendedInput, 6),
        };
        foreach (var (name, input, outputCount) in scenarios)
        {
            var expected = new int[outputCount];
            Array.Fill(expected, int.MinValue);
            Goldens.CopyTo(expected, 0);
            File.WriteAllText(Path.Combine(artifactDirectory, $"{name}-reference.txt"),
                $"input=[{string.Join(", ", input)}]{Environment.NewLine}" +
                $"expected=[{string.Join(", ", expected)}]");

            var actual = await DispatchAsync(device, pipeline, bindGroupLayout, input, outputCount);
            File.WriteAllText(Path.Combine(artifactDirectory, $"{name}-candidate.txt"),
                $"actual=[{string.Join(", ", actual)}]");
            AssertLanes(expected, actual);
        }
    }

    private static void AssertEntryGuardsDominateAccesses(
        ShaderModuleDeclaration<RawCilFunctionBody> parsed)
    {
        var labelled = CilBlockPartitionPass.Run(CilPreStackPass.Run(parsed));
        var body = Assert.Single(labelled.FunctionDefinitions.Values,
            candidate => candidate.Declaration.Name == nameof(ScoreShader.Run));
        var graph = ControlFlowGraph.Create(body.Blocks, static block => block.Terminator.ToSuccessor());
        var dominators = graph.ControlFlowAnalysis().DominatorTree;
        var guards = body.Blocks.Blocks.Where(block => block.Instructions.Any(instruction =>
            instruction.Node.Instruction.OpCode is var opcode &&
            (opcode == OpCodes.Bge_Un || opcode == OpCodes.Bge_Un_S || opcode == OpCodes.Clt_Un))).ToArray();
        Assert.Equal(2, guards.Length);
        var accesses = body.Blocks.Blocks.Where(block => block.Instructions.Any(instruction =>
            instruction.Node.Instruction.Operand is MethodInfo { Name: "get_Item" or "set_Item" })).ToArray();
        Assert.NotEmpty(accesses);
        foreach (var access in accesses)
            foreach (var guard in guards)
            {
                Assert.NotEqual(guard.Label, access.Label);
                Assert.Contains(guard.Label, dominators.Dominators(access.Label));
            }
    }

    private static async Task<int[]> DispatchAsync(
        IGPUDevice device, IGPUComputePipeline pipeline, IGPUBindGroupLayout layout,
        int[] values, int outputCount)
    {
        var inputBytes = checked((ulong)values.Length * sizeof(int));
        var outputBytes = checked((ulong)outputCount * sizeof(int));
        using var input = device.CreateBuffer(new()
        {
            Size = inputBytes,
            Usage = GPUBufferUsage.Storage | GPUBufferUsage.CopyDst,
        });
        using var result = device.CreateBuffer(new()
        {
            Size = outputBytes,
            Usage = GPUBufferUsage.Storage | GPUBufferUsage.CopySrc | GPUBufferUsage.CopyDst,
        });
        using var readback = device.CreateBuffer(new()
        {
            Size = outputBytes,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead,
        });
        device.Queue.WriteBuffer(input, 0, MemoryMarshal.AsBytes(values.AsSpan()));
        var sentinel = new int[outputCount];
        Array.Fill(sentinel, int.MinValue);
        device.Queue.WriteBuffer(result, 0, MemoryMarshal.AsBytes(sentinel.AsSpan()));

        using var bindGroup = device.CreateBindGroup(new()
        {
            Layout = layout,
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Buffer = input, Size = inputBytes },
                new() { Binding = 1, Buffer = result, Size = outputBytes },
            },
        });
        using (var encoder = device.CreateCommandEncoder(new()))
        {
            using (var pass = encoder.BeginComputePass(new()))
            {
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(0, bindGroup);
                pass.DispatchWorkgroups(1);
                pass.End();
            }
            encoder.CopyBufferToBuffer(result, 0, readback, 0, outputBytes);
            using var commands = encoder.Finish(new());
            device.Queue.Submit([commands]);
        }

        await WaitWithPollingAsync(device, device.Queue.OnSubmittedWorkDoneAsync(CancellationToken.None).AsTask());
        await WaitWithPollingAsync(device,
            readback.MapAsync(GPUMapMode.Read, 0, outputBytes, CancellationToken.None).AsTask());
        try
        {
            return MemoryMarshal.Cast<byte, int>(readback.GetMappedRange(0, outputBytes)).ToArray();
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
                throw new TimeoutException($"Native callback exceeded {CallbackTimeout}.");
            device.Poll();
            await Task.Delay(1);
        }
        await operation;
    }

    private static void AssertLanes(ReadOnlySpan<int> expected, ReadOnlySpan<int> actual)
    {
        Assert.True(expected.Length == actual.Length,
            $"Expected {expected.Length} lanes, got {actual.Length}.");
        for (var lane = 0; lane < expected.Length; lane++)
            Assert.True(expected[lane] == actual[lane],
                $"lane {lane}: expected {expected[lane]}, actual {actual[lane]}");
    }

    private sealed class ScoreShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<int> Input;

        [Group(0), Binding(1)]
        private static RWStructuredBuffer<int> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(64, 1, 1)]
        public static void Run([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Input.Length && i < Output.Length)
                Output[i] = Score(Input[i]);
        }

        [ShaderMethod]
        public static int Score(int x)
        {
            var acc = 1;
            if (x < 0)
                return x - 7;

            for (var i = 0; i < x; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    if (j == 1)
                        continue;
                    if (i == 2 && j == 2)
                        break;
                    acc += i * 3 + j;
                    if (x == 3 && i == 1 && j == 2)
                        return acc + 1000;
                }
            }

            switch (x)
            {
                case 0: acc += 11; break;
                case 1: acc += 13; break;
                case 2: acc += 17; break;
                case 3: acc += 19; break;
                default: acc += 19; break;
            }
            return acc;
        }
    }
}
