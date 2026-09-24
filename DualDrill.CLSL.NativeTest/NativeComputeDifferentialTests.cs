using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
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
    private static readonly int[] ResetGoldens = [-21, 1, 12, 23, 34, 45];
    private static readonly uint[] Words =
        [0u, 1u, 0x7fffffffu, 0x80000000u, 0x80000001u, uint.MaxValue];
    private static readonly uint[] GreaterGoldens = [0u, 0u, 0u, 0u, 1u, 1u];
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
    public void Unsigned_comparison_and_reset_match_fixed_clr_goldens()
    {
        AssertLanes(GreaterGoldens, Words.Select(value => value > 0x80000000u ? 1u : 0u).ToArray());
        AssertLanes(ResetGoldens, Inputs.Select(ResetShader.Reset).ToArray());
    }

    [Fact]
    public void Corrupted_unsigned_high_bit_fails_exact_lane_comparison()
    {
        var corrupted = (uint[])Words.Clone();
        corrupted[3] = 0u;

        var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertLanes(Words, corrupted));
        Assert.Contains("lane 3: expected 2147483648, actual 0", error.Message);
    }

    [Fact]
    public void Actual_unsigned_comparison_cil_uses_unsigned_relational_op()
    {
        var cil = CilMethodDecoder.Decode(typeof(UnsignedShader).GetMethod(nameof(UnsignedShader.Compare))!)
            .Instructions;
        Assert.Contains(cil, instruction => instruction.Instruction.OpCode is var opcode &&
            (opcode == OpCodes.Cgt_Un || opcode == OpCodes.Bgt_Un || opcode == OpCodes.Bgt_Un_S));
    }

    [Fact]
    public void Reset_cil_and_ir_preserve_observable_initobj_and_exact_field_identity()
    {
        var method = ((Func<int, int>)ResetShader.Reset).Method;
        var parser = new RuntimeReflectionParser();
        var raw = parser.ParseMethod(method);
        var cil = Assert.Single(raw.FunctionDefinitions.Values).Code.Instructions;
        var countField = typeof(ResetShader.Acc).GetField(nameof(ResetShader.Acc.Count))!;
        var sumField = typeof(ResetShader.Acc).GetField(nameof(ResetShader.Acc.Sum))!;
        var count = parser.ParseField(countField);
        var sum = parser.ParseField(sumField);
        var fields = new[] { (Field: countField, Member: count), (Field: sumField, Member: sum) };
        var reset = Assert.Single(cil.Where(instruction =>
            instruction.Instruction.OpCode == OpCodes.Initobj &&
            Equals(instruction.Instruction.Operand, typeof(ResetShader.Acc)) &&
            fields.All(field => cil.Any(write =>
                write.Index < instruction.Index &&
                write.Instruction.OpCode == OpCodes.Stfld &&
                Equals(write.Instruction.Operand, field.Field)))));

        foreach (var (field, _) in fields)
        {
            Assert.Contains(cil, instruction => instruction.Index < reset.Index &&
                instruction.Instruction.OpCode == OpCodes.Ldfld &&
                Equals(instruction.Instruction.Operand, field));
            Assert.Contains(cil, instruction => instruction.Index > reset.Index &&
                instruction.Instruction.OpCode == OpCodes.Ldfld &&
                Equals(instruction.Instruction.Operand, field));
        }

        var value = ShaderStackToValuePass.Run(ShaderStackControlFlowPass.Run(
            CilToShaderStackPass.Run(CilBlockPartitionPass.Run(CilPreStackPass.Run(raw)))));
        var instructions = Assert.Single(value.FunctionDefinitions.Values).Graph;
        var operations = instructions.Labels().SelectMany(label => instructions[label].Body.Elements).ToArray();
        var zeroStore = Assert.Single(operations, operation =>
            operation.Operation is StoreOperation &&
            operation.Payload is ShaderStackProvenance provenance &&
            provenance.OriginalIndex == reset.Index);
        Assert.Same(Assert.Single(raw.FunctionDefinitions.Values).DeclarationContext.LocalVariables[0].Value,
            zeroStore.Operand0);
        Assert.Contains(operations, operation =>
            operation.Operation is StructureCompositeConstructionOperation &&
            operation.Payload is ShaderStackProvenance provenance &&
            provenance.OriginalIndex == reset.Index);

        foreach (var (field, member) in fields)
            foreach (var access in cil.Where(instruction =>
                instruction.Instruction.OpCode is var opcode &&
                (opcode == OpCodes.Stfld || opcode == OpCodes.Ldfld) &&
                Equals(instruction.Instruction.Operand, field)))
                Assert.Contains(operations, operation =>
                    operation.Payload is ShaderStackProvenance provenance &&
                    provenance.OriginalIndex == access.Index &&
                    (operation.Operation is AddressOfMemberOperation address &&
                     ReferenceEquals(address.Member, member) ||
                     operation.Operation is StructureMemberGetOperation get &&
                     ReferenceEquals(get.Member, member)));
        Assert.NotEmpty(CilModuleCompiler.Compile(raw).FunctionDefinitions);
    }

    [Fact]
    [Trait("Category", "GPU")]
    public async Task Public_CSharp_unsigned_copy_and_comparison_match_exact_bits_on_hardware()
    {
        await RunCompiledAsync(new UnsignedShader(), nameof(UnsignedShader.Copy),
            Words, Words, 0xD15EA5E0u);
        await RunCompiledAsync(new UnsignedShader(), nameof(UnsignedShader.Compare),
            Words, GreaterGoldens, 0xD15EA5E0u);
    }

    [Fact]
    [Trait("Category", "GPU")]
    public async Task Public_CSharp_reset_accumulator_matches_exact_signed_lanes_on_hardware()
    {
        await RunCompiledAsync(new ResetShader(), nameof(ResetShader.Run),
            Inputs, ResetGoldens, int.MinValue);
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
        CaptureCompilerStages(parsed, artifactDirectory);
        AssertEntryBoundsGuardAccesses(parsed);
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

            var actual = await DispatchAsync(device, pipeline, bindGroupLayout, input, outputCount, int.MinValue);
            File.WriteAllText(Path.Combine(artifactDirectory, $"{name}-candidate.txt"),
                $"actual=[{string.Join(", ", actual)}]");
            AssertLanes(expected, actual);
        }
    }

    private static void CaptureCompilerStages(
        ShaderModuleDeclaration<RawCilFunctionBody> parsed, string artifactDirectory)
    {
        File.WriteAllText(Path.Combine(artifactDirectory, "source-cil.txt"),
            string.Join(Environment.NewLine, parsed.FunctionDefinitions.Values
                .OrderBy(body => body.Declaration.Name)
                .Select(body => body.Code.PrettyPrint())));
        var labelled = CilBlockPartitionPass.Run(CilPreStackPass.Run(parsed));
        var values = ShaderStackToValuePass.Run(
            ShaderStackControlFlowPass.Run(CilToShaderStackPass.Run(labelled)));
        File.WriteAllText(Path.Combine(artifactDirectory, "source-value.ir"),
            string.Join(Environment.NewLine, values.FunctionDefinitions.Values
                .OrderBy(body => body.Declaration.Name)
                .Select(body => body.PrettyPrint())));
    }

    private static void AssertEntryBoundsGuardAccesses(
        ShaderModuleDeclaration<RawCilFunctionBody> parsed)
    {
        var labelled = CilBlockPartitionPass.Run(CilPreStackPass.Run(parsed));
        var body = Assert.Single(labelled.FunctionDefinitions.Values,
            candidate => candidate.Declaration.Name == nameof(ScoreShader.Run));

        var graph = ControlFlowGraph.Create(body.Blocks, static block => block.Terminator.ToSuccessor());
        var dominators = graph.ControlFlowAnalysis().DominatorTree;
        var guards = body.Blocks.Blocks.Where(block => block.Instructions.Any(instruction =>
            instruction.Node.Instruction.OpCode is var opcode &&
            (opcode == OpCodes.Bge_Un || opcode == OpCodes.Bge_Un_S || opcode == OpCodes.Clt_Un)))
            .OrderBy(block => block.InstructionIndex).ToArray();
        Assert.Equal(2, guards.Length);
        Assert.Contains(guards[0].Instructions, instruction =>
            instruction.Node.Instruction.Operand is MethodInfo { Name: "get_Length" } method &&
            method.DeclaringType == typeof(StructuredBuffer<int>));
        Assert.Contains(guards[1].Instructions, instruction =>
            instruction.Node.Instruction.Operand is MethodInfo { Name: "get_Length" } method &&
            method.DeclaringType == typeof(RWStructuredBuffer<int>));
        var accesses = body.Blocks.Blocks.Where(block => block.Instructions.Any(instruction =>
            instruction.Node.Instruction.Operand is MethodInfo { Name: "get_Item" or "set_Item" })).ToArray();
        Assert.NotEmpty(accesses);
        foreach (var access in accesses)
        {
            Assert.NotEqual(guards[0].Label, access.Label);
            Assert.Contains(guards[0].Label, dominators.Dominators(access.Label));
        }
        if (accesses.All(access => dominators.Dominators(access.Label).Contains(guards[1].Label)))
            return;

        // Debug CIL merges false from the first guard with the second unsigned comparison.
        var first = Assert.IsType<CilControlFlow.ConditionalBranch>(guards[0].Terminator);
        Assert.True(first.Instruction.Instruction.OpCode is var opcode &&
            (opcode == OpCodes.Bge_Un || opcode == OpCodes.Bge_Un_S));
        Assert.Same(guards[1].Label, first.FallThroughTarget);
        var bypass = body[first.BranchTarget];
        Assert.Equal(OpCodes.Ldc_I4_0, Assert.Single(bypass.Instructions).Node.Instruction.OpCode);
        Assert.Equal(OpCodes.Clt_Un, guards[1].Instructions[^2].Node.Instruction.OpCode);
        var join = Assert.IsType<CilControlFlow.Branch>(guards[1].Terminator).Target;
        Assert.Same(join, Assert.Single(bypass.Terminator.ToSuccessor().AllTargets()));
        var merged = body[join];
        Assert.Equal([OpCodes.Stloc_1, OpCodes.Ldloc_1, OpCodes.Brfalse_S],
            merged.Instructions.Select(instruction => instruction.Node.Instruction.OpCode));
        var gate = Assert.IsType<CilControlFlow.ConditionalBranch>(merged.Terminator);
        Assert.IsType<CilControlFlow.Return>(body[gate.BranchTarget].Terminator);
        foreach (var access in accesses)
            Assert.Contains(gate.FallThroughTarget, dominators.Dominators(access.Label));
    }

    private async Task RunCompiledAsync<T>(
        ISharpShader source, string entryPoint, T[] values, T[] expected, T sentinel)
        where T : unmanaged, IEquatable<T>
    {
        var artifactDirectory = Directory.CreateDirectory(
            Path.Combine(AppContext.BaseDirectory, "compute-differential", Guid.NewGuid().ToString("N"))).FullName;
        output.WriteLine($"Artifacts: {artifactDirectory}");
        var entry = source.GetType().GetMethod(entryPoint)
            ?? throw new InvalidOperationException($"Missing C# compute entry {entryPoint}.");
        File.WriteAllText(Path.Combine(artifactDirectory, "source.txt"),
            $"{entry.Module.Assembly.FullName}{Environment.NewLine}" +
            $"MVID={entry.Module.ModuleVersionId}, token={entry.MetadataToken}, entry={entryPoint}{Environment.NewLine}" +
            $"input=[{string.Join(", ", values)}], expected=[{string.Join(", ", expected)}]");

        var compiler = new CLSLCompiler(new(CLSLCompileTarget.WGSL));
        var parsed = compiler.Parse(source);
        CaptureCompilerStages(parsed, artifactDirectory);
        var reflection = new ShaderModuleReflection();
        var bindings = reflection.GetStorageBufferBindings(parsed);
        Assert.Equal(
            [(0, GPUBufferBindingType.ReadOnlyStorage, 4u), (1, GPUBufferBindingType.Storage, 4u)],
            bindings.Select(binding => (binding.Binding, binding.Kind, binding.ElementStride)));
        var wgsl = compiler.Emit(source);
        File.WriteAllText(Path.Combine(artifactDirectory, "candidate.wgsl"), wgsl);
        Assert.Contains("@compute", wgsl);
        var scalar = typeof(T) == typeof(uint) ? "u32"
            : typeof(T) == typeof(int) ? "i32"
            : throw new NotSupportedException($"Unsupported compute buffer element {typeof(T)}.");
        Assert.Contains($"array<{scalar}>", wgsl);

        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(new()
        {
            PowerPreference = GPUPowerPreference.HighPerformance,
            ForceFallbackAdapter = false,
            BackendType = GPUBackendType.Vulkan,
        }, CancellationToken.None);
        var info = await adapter.RequestAdapterInfoAsync(CancellationToken.None);
        File.WriteAllText(Path.Combine(artifactDirectory, "adapter.txt"),
            $"{info.BackendType}, {info.AdapterType}, {info.Vendor}, {info.Device}");
        Assert.Equal(GPUBackendType.Vulkan, info.BackendType);
        Assert.True(info.AdapterType is GPUAdapterType.DiscreteGPU or GPUAdapterType.IntegratedGPU,
            $"Expected hardware adapter, got {info.AdapterType}.");
        using var device = await adapter.RequestDeviceAsync(new(), CancellationToken.None);
        using var shader = device.CreateShaderModule(new() { Code = wgsl });
        using var bindGroupLayout = device.CreateBindGroupLayout(
            reflection.GetBindGroupLayoutDescriptor(parsed, 0));
        using var pipelineLayout = device.CreatePipelineLayout(new() { BindGroupLayouts = [bindGroupLayout] });
        using var pipeline = device.CreateComputePipeline(new()
        {
            Layout = pipelineLayout,
            Compute = new() { Module = shader, EntryPoint = entryPoint },
        });
        var actual = await DispatchAsync(device, pipeline, bindGroupLayout, values, expected.Length, sentinel);
        File.WriteAllText(Path.Combine(artifactDirectory, "candidate.txt"),
            $"actual=[{string.Join(", ", actual)}]");
        AssertLanes(expected, actual);
    }

    private static async Task<T[]> DispatchAsync<T>(
        IGPUDevice device, IGPUComputePipeline pipeline, IGPUBindGroupLayout layout,
        T[] values, int outputCount, T sentinelValue) where T : unmanaged
    {
        var sentinel = new T[outputCount];
        Array.Fill(sentinel, sentinelValue);
        var inputBytes = (ulong)MemoryMarshal.AsBytes(values.AsSpan()).Length;
        var outputBytes = (ulong)MemoryMarshal.AsBytes(sentinel.AsSpan()).Length;
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
            return MemoryMarshal.Cast<byte, T>(readback.GetMappedRange(0, outputBytes)).ToArray();
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

    private static void AssertLanes<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual)
        where T : IEquatable<T>
    {
        Assert.True(expected.Length == actual.Length,
            $"Expected {expected.Length} lanes, got {actual.Length}.");
        for (var lane = 0; lane < expected.Length; lane++)
            Assert.True(expected[lane].Equals(actual[lane]),
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

    private sealed class UnsignedShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<uint> Input;

        [Group(0), Binding(1)]
        private static RWStructuredBuffer<uint> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(64, 1, 1)]
        public static void Copy([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Input.Length && i < Output.Length)
                Output[i] = Input[i];
        }

        [Compute, WorkgroupSize(64, 1, 1)]
        public static void Compare([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Input.Length && i < Output.Length)
                Output[i] = Input[i] > 0x80000000u ? 1u : 0u;
        }
    }

    private sealed class ResetShader : ISharpShader
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Acc
        {
            public int Count;
            public int Sum;
        }

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
                Output[i] = Reset(Input[i]);
        }

        [ShaderMethod]
        public static int Reset(int x)
        {
            Acc acc = default;
            acc.Count = x;
            acc.Sum = x + 1;
            var before = 10 * acc.Count + acc.Sum;
            acc = default;
            return before + 100 * acc.Count + acc.Sum;
        }
    }
}
