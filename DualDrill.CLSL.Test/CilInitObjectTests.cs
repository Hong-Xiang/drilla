using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using DualDrill.CLSL;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Mathematics;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Test;

public sealed class CilInitObjectTests
{
    public struct Inner
    {
        public int X;
        public uint Y;
        public float Z;
    }

    public struct Accumulator
    {
        public Inner Inner;
        public int Count;
    }

    public struct ReadonlyField
    {
        public readonly int Value;
    }

    public struct Constructed
    {
        public int Value;
        public Constructed() => Value = 9;
    }

    public struct WithVector
    {
        public vec2f32 Vector;
        public float Scalar;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CustomLayout
    {
        public int Value;
    }

    public struct WithProperty
    {
        public int Value { get; set; }
    }

    [Fact]
    public void ForcedInitObjectClearsWrittenFieldsWithUninitializedLocalsAndPreservesPrefix()
    {
        var method = ForcedReset();
        Assert.Equal(0, (int)method.Invoke(null, null)!);
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        Assert.False(raw.Code.Environment.Body!.InitLocals);
        var init = Assert.Single(raw.Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Initobj);
        var pre = Assert.Single(stages.Pre.FunctionDefinitions.Values);
        var annotated = Assert.Single(pre.Code.Instructions, item => item.Node.Index == init.Index);
        Assert.Equal(2, annotated.Annotation.Types.Count());
        var after = Assert.Single(pre.Code.Instructions, item => item.Node.Index == init.Index + 1);
        Assert.Single(after.Annotation.Types);
        Assert.Same(annotated.Annotation.Types.Last(), after.Annotation.Types.Single());

        var values = Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values);
        var instructions = values.Graph.Labels()
            .SelectMany(label => values.Graph[label].Body.Elements).ToArray();
        var stores = instructions
            .Where(instruction => instruction.Operation is StoreOperation &&
                                  instruction.Payload is ShaderStackProvenance provenance &&
                                  provenance.OriginalIndex == init.Index)
            .ToArray();
        var store = Assert.Single(stores);
        var local = Assert.Single(raw.DeclarationContext.LocalVariables);
        Assert.Same(local.Value, store.Operand0);
        Assert.Equal(local.Type, store.Operand1!.Type);
        var position = Array.IndexOf(instructions, store);
        Assert.Contains(instructions.Take(position), instruction => instruction.Operation is StoreOperation);
        Assert.Contains(instructions.Skip(position + 1), instruction => instruction.Operation is LoadOperation);
        Assert.NotEmpty(CilModuleCompiler.Compile(stages.Raw).FunctionDefinitions);
    }

    [Fact]
    public void OrdinaryDefaultAccumulatorBuildsWithoutInvokingConstructors()
    {
        var method = typeof(CilInitObjectTests).GetMethod(nameof(Ordinary), BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(0, Ordinary());
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        Assert.Contains(raw.Code.Instructions, instruction => instruction.Instruction.OpCode == OpCodes.Initobj);
        Assert.NotEmpty(stages.Compiled.FunctionDefinitions);
    }

    [Fact]
    public void MappedVectorZeroUsesExplicitComponentsAndPositiveFloatZero()
    {
        var method = typeof(CilInitObjectTests).GetMethod(nameof(VectorZero), BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(0, VectorZero());
        var stages = CompilerTestPipeline.CompileStages(method);
        var zeroIndices = CompilerTestPipeline.RawBody(stages.Raw, method).Code.Instructions
            .Where(instruction => instruction.Instruction.OpCode == OpCodes.Initobj)
            .Select(instruction => instruction.Index).ToHashSet();
        var values = Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values);
        var instructions = values.Graph.Labels().SelectMany(label => values.Graph[label].Body.Elements).ToArray();
        Assert.Contains(instructions, instruction =>
            instruction.Operation is VectorCompositeConstructionOperation);
        Assert.Contains(instructions, instruction =>
            instruction.Operation is StructureCompositeConstructionOperation);
        var floatZeros = instructions
            .Where(instruction => instruction.Operation is LiteralOperation &&
                                  instruction.Payload is ShaderStackProvenance provenance &&
                                  zeroIndices.Contains(provenance.OriginalIndex))
            .Select(instruction => instruction.Operand0)
            .OfType<LiteralValue>()
            .Select(value => value.Value)
            .OfType<DualDrill.CLSL.Language.Literal.F32Literal>()
            .ToArray();
        Assert.NotEmpty(floatZeros);
        Assert.All(floatZeros, zero => Assert.Equal(0, BitConverter.SingleToInt32Bits(zero.Value)));
    }

    [Fact]
    public void PublicCompilerEmitsAndTranspilesObservableNestedZero()
    {
        var shader = new InitObjectShader();
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        Assert.Contains("let v_", slang);
        Assert.Contains(": Accumulator = {", slang);
        Assert.Contains(" = v_", slang);
        Assert.Contains("@compute", wgsl);
        Assert.Contains("Inner", wgsl);
    }

    [Theory]
    [InlineData(typeof(ReadonlyField), "readonly")]
    [InlineData(typeof(Constructed), "constructor")]
    [InlineData(typeof(long), "i64")]
    [InlineData(typeof(string), "reference")]
    [InlineData(typeof(CustomLayout), "layout")]
    [InlineData(typeof(WithProperty), "properties")]
    public void UnsupportedInitObjectTypeIsContextual(Type type, string reason)
    {
        var method = InvalidType(type);
        var exception = Assert.ThrowsAny<Exception>(() => CompilerTestPipeline.CompileStages(method));
        Assert.Contains(reason, exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IL_", exception.ToString());
    }

    [Fact]
    public void ResourceLocalIsRejectedBeforeInitObjectLowering()
    {
        var method = InvalidType(typeof(StructuredBuffer<float>));
        var exception = Assert.Throws<NotSupportedException>(() => CompilerTestPipeline.ParseRaw(method));
        Assert.Contains("resource", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("local variable", exception.ToString());
    }

    [Fact]
    public void WrongPointeeAndStackUnderflowFailAtTheirOwnInstruction()
    {
        foreach (var (name, pushAddress, expected) in new[]
                 {
                     ("Mismatch", true, "matching"),
                     ("Underflow", false, "underflow")
                 })
        {
            var method = NewMethod(name, typeof(void));
            var il = method.GetILGenerator();
            if (pushAddress)
            {
                var local = il.DeclareLocal(typeof(int));
                il.Emit(OpCodes.Ldloca, local);
            }
            il.Emit(OpCodes.Initobj, typeof(Accumulator));
            il.Emit(OpCodes.Ret);
            var complete = ((TypeBuilder)method.DeclaringType!).CreateType()!.GetMethod(name)!;
            var exception = Assert.Throws<ValidationException>(() => CompilerTestPipeline.CompileStages(complete));
            Assert.Contains(expected, exception.Message);
            Assert.Contains("IL_", exception.Message);
        }
    }

    [Fact]
    public void ParameterAddressIsNotAnOriginalLocalRoot()
    {
        var method = NewMethod("ParameterRoot", typeof(void), typeof(Accumulator));
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarga_S, (byte)0);
        il.Emit(OpCodes.Initobj, typeof(Accumulator));
        il.Emit(OpCodes.Ret);
        var complete = ((TypeBuilder)method.DeclaringType!).CreateType()!.GetMethod("ParameterRoot")!;
        var raw = CompilerTestPipeline.ParseRaw(complete);
        Assert.NotEmpty(CilPreStackPass.Run(raw).FunctionDefinitions);
        var exception = Assert.Throws<ValidationException>(() => CilModuleCompiler.Compile(raw));
        Assert.Contains("original writable function-local address", exception.Message);
        Assert.Contains("IL_", exception.Message);
    }

    [Fact]
    public void ProjectedFieldAddressCannotMasqueradeAsLocalRoot()
    {
        var method = NewMethod("Projected", typeof(void));
        var il = method.GetILGenerator();
        var local = il.DeclareLocal(typeof(Accumulator));
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Ldflda, typeof(Accumulator).GetField(nameof(Accumulator.Inner))!);
        il.Emit(OpCodes.Initobj, typeof(Inner));
        il.Emit(OpCodes.Ret);
        var complete = ((TypeBuilder)method.DeclaringType!).CreateType()!.GetMethod("Projected")!;
        var exception = Assert.Throws<ValidationException>(() =>
            CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(complete)));
        Assert.Contains("original writable function-local address", exception.Message);
    }

    private static int Ordinary()
    {
        Accumulator accumulator = default;
        accumulator.Count = 7;
        accumulator.Inner.X = 4;
        accumulator = default;
        return accumulator.Count + accumulator.Inner.X + (int)accumulator.Inner.Y + (int)accumulator.Inner.Z;
    }

    private static int VectorZero()
    {
        WithVector value = default;
        value.Vector.x = 8;
        value.Vector.y = 9;
        value.Scalar = 7;
        value = default;
        return (int)(value.Vector.x + value.Vector.y + value.Scalar);
    }

    private sealed class InitObjectShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static RWStructuredBuffer<float> Output;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
            Accumulator accumulator = default;
            accumulator.Count = 7;
            accumulator.Inner.X = 4;
            accumulator = default;
            Output[0u] = accumulator.Count + accumulator.Inner.X +
                         (int)accumulator.Inner.Y + (int)accumulator.Inner.Z;
        }
    }

    private static MethodInfo ForcedReset()
    {
        var method = NewMethod("Reset", typeof(int));
        var il = method.GetILGenerator();
        method.InitLocals = false;
        var local = il.DeclareLocal(typeof(Accumulator));
        var nested = typeof(Accumulator).GetField(nameof(Accumulator.Inner))!;
        var count = typeof(Accumulator).GetField(nameof(Accumulator.Count))!;
        var x = typeof(Inner).GetField(nameof(Inner.X))!;
        var y = typeof(Inner).GetField(nameof(Inner.Y))!;
        var z = typeof(Inner).GetField(nameof(Inner.Z))!;
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Ldc_I4, 17);
        il.Emit(OpCodes.Stfld, count);
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Ldflda, nested);
        il.Emit(OpCodes.Ldc_I4, 19);
        il.Emit(OpCodes.Stfld, x);
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Ldflda, nested);
        il.Emit(OpCodes.Ldc_I4, 21);
        il.Emit(OpCodes.Stfld, y);
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Ldflda, nested);
        il.Emit(OpCodes.Ldc_R4, 23f);
        il.Emit(OpCodes.Stfld, z);
        il.Emit(OpCodes.Ldc_I4, 0);
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Initobj, typeof(Accumulator));
        foreach (var field in new[] { count, x, y, z })
        {
            il.Emit(OpCodes.Ldloca, local);
            if (field.DeclaringType == typeof(Inner))
                il.Emit(OpCodes.Ldflda, nested);
            il.Emit(OpCodes.Ldfld, field);
            if (field == z)
                il.Emit(OpCodes.Conv_I4);
            il.Emit(OpCodes.Add);
        }
        il.Emit(OpCodes.Ret);
        return ((TypeBuilder)method.DeclaringType!).CreateType()!.GetMethod(method.Name)!;
    }

    private static MethodInfo InvalidType(Type type)
    {
        var method = NewMethod("Invalid", typeof(void));
        var il = method.GetILGenerator();
        var local = il.DeclareLocal(type);
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Initobj, type);
        il.Emit(OpCodes.Ret);
        return ((TypeBuilder)method.DeclaringType!).CreateType()!.GetMethod(method.Name)!;
    }

    private static MethodBuilder NewMethod(string name, Type result, params Type[] arguments)
    {
        var module = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"InitObjectFixture_{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("Fixture");
        var type = module.DefineType("Fixture", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static, result, arguments);
        foreach (var index in Enumerable.Range(0, arguments.Length))
            method.DefineParameter(index + 1, ParameterAttributes.None, $"arg{index}");
        return method;
    }
}
