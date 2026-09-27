using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using Xunit.Abstractions;

namespace DualDrill.CLSL.NativeTest;

public sealed class CilIndirectNegativeZeroNativeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PublicIndirectFloatStoreLoadPreservesNegativeZeroOnGpu()
    {
        var shader = Shader();
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        using var instance = WebGPUNETBackend.Instance.CreateGPUInstance();
        using var adapter = await instance.RequestAdapterAsync(new()
        {
            PowerPreference = GPUPowerPreference.HighPerformance,
            ForceFallbackAdapter = false
        }, CancellationToken.None);
        var info = await adapter.RequestAdapterInfoAsync(CancellationToken.None);
        output.WriteLine($"Indirect float adapter: {info.BackendType}, {info.AdapterType}, {info.Vendor}, {info.Device}");
        using var device = await adapter.RequestDeviceAsync(new(), CancellationToken.None);
        using var module = device.CreateShaderModule(new() { Code = wgsl });
        using var pipeline = device.CreateComputePipeline(new()
        {
            Compute = new() { Module = module, EntryPoint = "Run" }
        });
        using var layout = pipeline.GetBindGroupLayout(0);
        using var result = device.CreateBuffer(new()
        {
            Size = 4,
            Usage = GPUBufferUsage.Storage | GPUBufferUsage.CopySrc
        });
        using var readback = device.CreateBuffer(new()
        {
            Size = 4,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.MapRead
        });
        using var binding = device.CreateBindGroup(new()
        {
            Layout = layout,
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Buffer = result, Size = 4 }
            }
        });
        using (var encoder = device.CreateCommandEncoder(new()))
        {
            using (var pass = encoder.BeginComputePass(new()))
            {
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(0, binding);
                pass.DispatchWorkgroups(1);
                pass.End();
            }
            encoder.CopyBufferToBuffer(result, 0, readback, 0, 4);
            using var commands = encoder.Finish(new());
            device.Queue.Submit([commands]);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var mapped = readback.MapAsync(GPUMapMode.Read, 0, 4, timeout.Token).AsTask();
        while (!mapped.IsCompleted)
        {
            device.Poll();
            await Task.Delay(1, timeout.Token);
        }
        await mapped;
        try
        {
            var bytes = readback.GetMappedRange(0, 4);
            var bits = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            output.WriteLine($"Indirect float output bits: 0x{bits:X8}");
            Assert.Equal(unchecked((int)0x80000000), bits);
        }
        finally
        {
            readback.Unmap();
        }
    }

    private static ISharpShader Shader()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("IndirectSignedZeroNative"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("IndirectSignedZeroNative").DefineType(
            "IndirectSignedZeroNative", TypeAttributes.Public, typeof(object), [typeof(ISharpShader)]);
        _ = type.DefineDefaultConstructor(MethodAttributes.Public);
        var output = type.DefineField("Output", typeof(RWStructuredBuffer<float>),
            FieldAttributes.Private | FieldAttributes.Static);
        output.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(GroupAttribute).GetConstructor([typeof(int)])!, [0]));
        output.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BindingAttribute).GetConstructor([typeof(int), typeof(bool)])!, [0, false]));
        var method = type.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), Type.EmptyTypes);
        method.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(ComputeAttribute).GetConstructor(Type.EmptyTypes)!, []));
        method.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(WorkgroupSizeAttribute).GetConstructor([typeof(int), typeof(int), typeof(int)])!,
            [1, 1, 1]));
        var il = method.GetILGenerator();
        var local = il.DeclareLocal(typeof(float));
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Ldc_R4, -0.0f);
        il.Emit(OpCodes.Stind_R4);
        il.Emit(OpCodes.Ldsflda, output);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Ldind_R4);
        il.Emit(OpCodes.Call, typeof(RWStructuredBuffer<float>).GetProperty("Item")!.SetMethod!);
        il.Emit(OpCodes.Ret);
        return (ISharpShader)Activator.CreateInstance(type.CreateType()!)!;
    }
}
