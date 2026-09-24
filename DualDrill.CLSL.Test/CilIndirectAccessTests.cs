using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using Lokad.ILPack.IL;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class CilIndirectAccessTests(ITestOutputHelper output)
{
    public struct Inner
    {
        public uint Unsigned;
        public float Float;
    }

    public struct Outer
    {
        public Inner Inner;
        public int Count;
    }

    public struct UnsupportedRoot
    {
        public int Integer;
        public byte Byte;
    }

    public struct ReadonlyRoot
    {
        public readonly int Integer;
    }

    private sealed class StaticUniformShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0), Uniform]
        public static int Global;
#pragma warning restore CS0649
    }

    [Theory]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(uint), false)]
    [InlineData(typeof(uint), true)]
    public void BothIntegerLoadOpcodesReadActualSignedOrUnsignedStorageBits(Type localType, bool unsignedLoad)
    {
        var method = Method("IntegerBits", typeof(int), localType, false, (il, local) =>
        {
            il.Emit(OpCodes.Ldc_I4_5);
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldc_I4, int.MinValue);
            il.Emit(OpCodes.Stind_I4);
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(unsignedLoad ? OpCodes.Ldind_U4 : OpCodes.Ldind_I4);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(int.MinValue + 5, method.Invoke(null, null));

        var stages = CompilerTestPipeline.CompileStages(method);
        var body = Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        Assert.False(raw.Code.Environment.Body!.InitLocals);
        var local = Assert.Single(raw.DeclarationContext.LocalVariables);
        var sourceStore = Assert.Single(raw.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Stind_I4);
        var sourceLoad = Assert.Single(raw.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == (unsignedLoad ? OpCodes.Ldind_U4 : OpCodes.Ldind_I4));
        var store = Assert.Single(SourceEffects(body, sourceStore.Index));
        var load = Assert.Single(SourceEffects(body, sourceLoad.Index));
        Assert.IsType<StoreOperation>(store.Operation);
        Assert.IsType<LoadOperation>(load.Operation);
        Assert.Null(store.Result);
        Assert.Same(local.Value, store.Operand0);
        Assert.Same(local.Value, load.Operand0);
        Assert.Equal(local.Type, store.Operand1!.Type);
        Assert.Equal(local.Type, load.Result!.Type);
        var ordered = body.Graph[body.Graph.EntryLabel].Body.Elements.ToArray();
        Assert.True(Array.IndexOf(ordered, store) < Array.IndexOf(ordered, load));
        if (localType == typeof(uint))
        {
            Assert.Single(ordered, instruction =>
                instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>> &&
                instruction.Payload is ShaderStackProvenance { OriginalIndex: var index } &&
                index == sourceStore.Index);
            Assert.Single(ordered, instruction =>
                instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>> &&
                instruction.Payload is ShaderStackProvenance { OriginalIndex: var index } &&
                index == sourceLoad.Index);
        }
        Assert.NotEmpty(CilModuleCompiler.Compile(stages.Raw).FunctionDefinitions);
    }

    [Fact]
    public async Task FloatingStoreAndLoadPreserveNegativeZeroBits()
    {
        var method = Method("FloatBits", typeof(float), typeof(float), false, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldc_R4, -0.0f);
            il.Emit(OpCodes.Stind_R4);
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldind_R4);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits((float)method.Invoke(null, null)!));
        var stages = CompilerTestPipeline.CompileStages(method);
        var body = Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var local = Assert.Single(raw.DeclarationContext.LocalVariables);
        var storeSource = Assert.Single(raw.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Stind_R4);
        var loadSource = Assert.Single(raw.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldind_R4);
        Assert.Same(local.Value, Assert.Single(SourceEffects(body, storeSource.Index)).Operand0);
        Assert.Same(local.Value, Assert.Single(SourceEffects(body, loadSource.Index)).Operand0);
        var region = CilModuleCompiler.Compile(stages.Raw)
            .RunPass(new FunctionToOperationPass())
            .RunPass(new StablePointerRegionParameterPass());
        await new SlangService().ValidateAsync(new SlangEmitter(new SlangTargetLowering().Lower(region)).Emit());
    }

    [Fact]
    public void PublicCompilerEmitsActualIndirectLoadAndStoreInComputeWgsl()
    {
        var shader = ComputeShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.IR));
        var raw = compiler.Parse(shader);
        var method = Assert.Single(raw.FunctionDefinitions.Values).Code.Environment.Method;
        Assert.Contains(CilMethodDecoder.Decode(method).Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Stind_R4);
        Assert.Contains(CilMethodDecoder.Decode(method).Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldind_R4);
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        Assert.Contains(" = ", slang);
        Assert.Contains("@compute", wgsl);
        Assert.Contains("Output", wgsl);
    }

    [Fact]
    public void InitialValueAndNestedFieldProjectionKeepOriginalAddressAndOneEffect()
    {
        var method = Method("NestedInitial", typeof(int), typeof(Outer), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldc_I4_3);
            FieldAddress(il, local);
            il.Emit(OpCodes.Ldind_U4);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(3, method.Invoke(null, null));
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var body = Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values);
        var projected = Assert.Single(SourceEffects(body,
            raw.Code.Instructions.Single(instruction => instruction.Instruction.OpCode == OpCodes.Ldind_U4).Index));
        Assert.IsType<LoadOperation>(projected.Operation);
        Assert.IsAssignableFrom<IPtrType>(projected.Operand0!.Type);
        var promoted = Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values);
        Assert.Single(promoted.Graph.Labels()
            .SelectMany(label => promoted.Graph[label].Body.Elements)
            .Where(instruction => instruction.Operation is StoreOperation &&
                                  instruction.Payload is ShaderStackProvenance { Synthetic: true } &&
                                  ReferenceEquals(instruction.Operand0, raw.DeclarationContext.LocalVariables[0].Value)));
        Assert.NotEmpty(CilModuleCompiler.Compile(stages.Raw).FunctionDefinitions);
    }

    [Fact]
    public void InitLocalsTrueProvidesScalarZeroBeforeFirstIndirectRead()
    {
        var method = Method("InitialUnsigned", typeof(int), typeof(uint), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(0, method.Invoke(null, null));
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var promoted = Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values);
        var synthetic = Assert.Single(promoted.Graph.Labels()
            .SelectMany(label => promoted.Graph[label].Body.Elements)
            .Where(instruction => instruction.Operation is StoreOperation &&
                                  instruction.Payload is ShaderStackProvenance { Synthetic: true }));
        Assert.Same(Assert.Single(raw.DeclarationContext.LocalVariables).Value, synthetic.Operand0);
        Assert.NotEmpty(CilModuleCompiler.Compile(stages.Raw).FunctionDefinitions);
    }

    [Fact]
    public void ExplicitlyWrittenNestedFieldWorksWithInitLocalsFalse()
    {
        var method = Method("NestedWritten", typeof(uint), typeof(Outer), false, (il, local) =>
        {
            FieldAddress(il, local);
            il.Emit(OpCodes.Ldc_I4_M1);
            il.Emit(OpCodes.Stind_I4);
            FieldAddress(il, local);
            il.Emit(OpCodes.Ldind_U4);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(uint.MaxValue, method.Invoke(null, null));
        var stages = CompilerTestPipeline.CompileStages(method);
        Assert.Empty(Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values)
            .Graph.Labels().SelectMany(label =>
                Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values)
                    .Graph[label].Body.Elements)
            .Where(instruction => instruction.Payload is ShaderStackProvenance { Synthetic: true }));
        Assert.NotEmpty(CilModuleCompiler.Compile(stages.Raw).FunctionDefinitions);
    }

    [Theory]
    [InlineData(typeof(float), false)]
    [InlineData(typeof(bool), false)]
    [InlineData(typeof(float), true)]
    public void WrongPointeeOrStoreValueFailsAtCilBoundary(Type type, bool wrongStoreValue)
    {
        var method = Method("WrongType", typeof(void), type, true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            if (wrongStoreValue)
            {
                il.Emit(OpCodes.Ldc_R4, 1f);
                il.Emit(OpCodes.Stind_I4);
            }
            else
            {
                il.Emit(OpCodes.Ldind_I4);
                il.Emit(OpCodes.Pop);
            }
            il.Emit(OpCodes.Ret);
        });
        var failure = Assert.Throws<ValidationException>(() =>
            CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method)));
        Assert.Contains("IL_", failure.Message);
        Assert.Contains(method.Name, failure.Message);
    }

    [Fact]
    public void UnderflowAndUnsupportedWidthsRemainRejected()
    {
        foreach (var opcode in new[] { OpCodes.Ldind_I4, OpCodes.Stind_I4, OpCodes.Ldind_I1, OpCodes.Ldind_I8 })
        {
            var method = Method("Underflow" + opcode.Name, typeof(void), typeof(int), true, (il, _) =>
            {
                il.Emit(opcode);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ret);
            });
            var failure = Assert.Throws<ValidationException>(() =>
                CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(method)));
            Assert.Contains("IL_", failure.Message);
        }
    }

    [Fact]
    public void ParameterAndCarriedPointerRootsAreNotAdmitted()
    {
        var parameter = ParameterRoot();
        var carried = Method("Carried", typeof(int), typeof(int), true, (il, local) =>
        {
            var target = il.DefineLabel();
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Br_S, target);
            il.MarkLabel(target);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Ret);
        });
        foreach (var method in new[] { parameter, carried })
        {
            var error = Assert.Throws<ValidationException>(() =>
                CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method)));
            Assert.Contains("address", error.Message);
            Assert.Contains("IL_", error.Message);
        }
    }

    [Fact]
    public void StaticUniformAndStoredPointerAddressesCannotBypassRootProof()
    {
        var uniform = Method("Uniform", typeof(int), typeof(int), true, (il, _) =>
        {
            il.Emit(OpCodes.Ldsflda, typeof(StaticUniformShader).GetField(nameof(StaticUniformShader.Global))!);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Ret);
        });
        var stored = Method("StoredPointer", typeof(int), typeof(int), true, (il, local) =>
        {
            var alias = il.DeclareLocal(typeof(int).MakeByRefType());
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Stloc, alias);
            il.Emit(OpCodes.Ldloc, alias);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Ret);
        });
        foreach (var method in new[] { uniform, stored })
        {
            var error = Assert.Throws<ValidationException>(() =>
                CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method)));
            Assert.Contains("IL_", error.Message);
        }
    }

    [Fact]
    public void NativeOpcodeCannotBeAttributedToSyntheticMemoryEffect()
    {
        var method = Method("BadAttribution", typeof(int), typeof(int), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Ret);
        });
        var stages = CompilerTestPipeline.CompileStages(method);
        var original = Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var native = Assert.Single(raw.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldind_I4);
        var definitions = original.Graph.Labels().ToDictionary(label => label, label =>
        {
            var block = original.Graph[label];
            var instructions = block.Body.Elements.Select(instruction =>
                instruction.Operation is LoadOperation &&
                instruction.Payload is ShaderStackProvenance { OriginalIndex: var index } &&
                index == native.Index
                    ? instruction with
                    {
                        Payload = ShaderStackProvenance.SyntheticInitialization(native, 0)
                    }
                    : instruction);
            var changed = block with { Body = Seq.Create(instructions, block.Body.Last) };
            return new ControlFlowGraph<CilValueBasicBlock>.NodeDefinition(changed.Successor, changed);
        });
        var forged = new CilValueControlFlowBody(
            original.Source, new ControlFlowGraph<CilValueBasicBlock>(original.Graph.EntryLabel, definitions));
        var module = new ShaderModuleDeclaration<CilValueControlFlowBody>(
            stages.ValueControlFlow.Declarations,
            ImmutableDictionary<FunctionDeclaration, CilValueControlFlowBody>.Empty
                .Add(forged.Declaration, forged));
        var error = Assert.Throws<ValidationException>(() => CilModuleCompiler.ValidateIndirectAccesses(module));
        Assert.Contains("exactly one typed load", error.Message);
        Assert.Contains("IL_", error.Message);
    }

    [Fact]
    public void WholeUnsupportedOrReadonlyRootCannotBeAccessedByIntegerLeaf()
    {
        var unsupported = Method("Mixed", typeof(void), typeof(UnsupportedRoot), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldflda, typeof(UnsupportedRoot).GetField(nameof(UnsupportedRoot.Integer))!);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Stind_I4);
            il.Emit(OpCodes.Ret);
        });
        var readonlyField = Method("Readonly", typeof(void), typeof(ReadonlyRoot), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldflda, typeof(ReadonlyRoot).GetField(nameof(ReadonlyRoot.Integer))!);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Stind_I4);
            il.Emit(OpCodes.Ret);
        });
        foreach (var method in new[] { unsupported, readonlyField })
        {
            var error = Assert.Throws<ValidationException>(() =>
                CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method)));
            Assert.Contains("IL_", error.Message);
        }
    }

    private static void FieldAddress(ILGenerator il, LocalBuilder local)
    {
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Ldflda, typeof(Outer).GetField(nameof(Outer.Inner))!);
        il.Emit(OpCodes.Ldflda, typeof(Inner).GetField(nameof(Inner.Unsigned))!);
    }

    private static Instruction<IShaderValue, IShaderValue>[] SourceEffects(
        CilValueControlFlowBody body, int index) =>
        body.Graph.Labels().SelectMany(label => body.Graph[label].Body.Elements)
            .Where(instruction =>
                (instruction.Operation is LoadOperation or StoreOperation) &&
                instruction.Payload is ShaderStackProvenance
                { Synthetic: false, OriginalIndex: var original } && original == index)
            .ToArray();

    private static MethodInfo Method(
        string name, Type returnType, Type localType, bool initLocals,
        Action<ILGenerator, LocalBuilder> emit)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Indirect_{name}_{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Indirect").DefineType(
            "Indirect", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static,
            returnType, Type.EmptyTypes);
        method.InitLocals = initLocals;
        var il = method.GetILGenerator();
        emit(il, il.DeclareLocal(localType));
        return type.CreateType()!.GetMethod(name)!;
    }

    private static MethodInfo ParameterRoot()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("IndirectParameter"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("IndirectParameter").DefineType(
            "IndirectParameter", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod("ParameterRoot", MethodAttributes.Public | MethodAttributes.Static,
            typeof(int), [typeof(int)]);
        method.DefineParameter(1, ParameterAttributes.None, "value");
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarga_S, (byte)0);
        il.Emit(OpCodes.Ldind_I4);
        il.Emit(OpCodes.Ret);
        return type.CreateType()!.GetMethod("ParameterRoot")!;
    }

    private static ISharpShader ComputeShader()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("IndirectCompute"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("IndirectCompute").DefineType(
            "IndirectCompute", TypeAttributes.Public, typeof(object), [typeof(ISharpShader)]);
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
        method.InitLocals = true;
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

    private static int LocalInt()
    {
        int value = 3;
        ref int target = ref value;
        target = -7;
        return target;
    }

    private static uint LocalUInt()
    {
        uint value = 0x80000000u;
        ref uint target = ref value;
        target = uint.MaxValue;
        return target;
    }

    private static float LocalFloat()
    {
        float value = 1.25f;
        ref float target = ref value;
        target = -0.0f;
        return target;
    }

    private static int FieldInt()
    {
        Outer value = default;
        ref int target = ref value.Count;
        target = -7;
        return target;
    }

    [Fact]
    public void OrdinaryCSharpRefLocalCeilingMatchesConfigurationSpecificOriginalCil()
    {
        foreach (var method in new MethodInfo[]
                 {
                     ((Func<int>)LocalInt).Method,
                     ((Func<uint>)LocalUInt).Method,
                     ((Func<float>)LocalFloat).Method,
                     ((Func<int>)FieldInt).Method
                 })
        {
            var body = method.GetMethodBody()!;
            var raw = CilMethodDecoder.Decode(method);
            output.WriteLine(
                $"configuration={GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration}; " +
                $"method={method}; locals=[{string.Join(", ", body.LocalVariables.Select(local => local.LocalType))}]");
            output.WriteLine(raw.PrettyPrint());
            Assert.Contains(raw.Instructions, instruction =>
                instruction.Instruction.OpCode.Name?.StartsWith("ldind.", StringComparison.Ordinal) is true);
            Assert.Contains(raw.Instructions, instruction =>
                instruction.Instruction.OpCode.Name?.StartsWith("stind.", StringComparison.Ordinal) is true);
#if DEBUG
            Assert.Contains(body.LocalVariables, local => local.LocalType.IsByRef);
#endif
            if (body.LocalVariables.Any(local => local.LocalType.IsByRef))
            {
                var error = Assert.Throws<ValidationException>(() =>
                    CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method)));
                Assert.Contains("IL_", error.Message);
            }
            else
            {
                Assert.NotEmpty(CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method)).FunctionDefinitions);
            }
        }
        Assert.Equal(-7, LocalInt());
        Assert.Equal(uint.MaxValue, LocalUInt());
        Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(LocalFloat()));
        Assert.Equal(-7, FieldInt());
    }
}
