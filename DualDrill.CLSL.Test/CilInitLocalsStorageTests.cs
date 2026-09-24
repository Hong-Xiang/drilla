using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Mathematics;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class CilInitLocalsStorageTests
{
    public struct Fields
    {
        public uint Unsigned;
        public float Fraction;
    }

    public struct UnsupportedWhole
    {
        public int Value;
        public byte Other;
    }

    public struct Outer
    {
        public Fields Inner;
        public uint Count;
    }

    [Fact]
    public async Task InitialUnsignedAndFloatReadsHaveExplicitTypedStorageZeros()
    {
        var unsigned = Method("Unsigned", typeof(uint), typeof(uint), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });
        var fraction = Method("Fraction", typeof(float), typeof(float), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });

        Assert.Equal(0u, unsigned.Invoke(null, null));
        Assert.Equal(0f, fraction.Invoke(null, null));
        foreach (var method in new[] { unsigned, fraction })
        {
            var (raw, promoted, store) = Initialized(method);
            var local = Assert.Single(raw.DeclarationContext.LocalVariables);
            Assert.Same(local.Value, store.Operand0);
            Assert.Equal(local.Type, store.Operand1!.Type);
            Assert.Empty(InitialStores(CompilerTestPipeline.ValueControlFlow(method)));

            var region = CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method))
                .RunPass(new FunctionToOperationPass())
                .RunPass(new StablePointerRegionParameterPass());
            var source = new SlangEmitter(new SlangTargetLowering().Lower(region)).Emit();
            await new DualDrill.CLSL.SlangService().ValidateAsync(source);
            Assert.Contains($" : {local.Type.Name} =", source);
            Assert.NotEmpty(promoted.Graph.Labels());
        }
    }

    [Theory]
    [InlineData(nameof(Fields.Unsigned), typeof(uint), 0u)]
    [InlineData(nameof(Fields.Fraction), typeof(float), 0f)]
    public void PlainStructureFieldReadBeforeWriteObservesZero(string name, Type result, object expected)
    {
        var field = typeof(Fields).GetField(name)!;
        var method = Method("Field" + name, result, typeof(Fields), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(expected, method.Invoke(null, null));
        var (raw, promoted, store) = Initialized(method);
        var local = Assert.Single(raw.DeclarationContext.LocalVariables);
        Assert.Same(local.Value, store.Operand0);
        Assert.IsType<StructureCompositeConstructionOperation>(
            promoted.Graph.Labels()
                .SelectMany(label => promoted.Graph[label].Body.Elements)
                .Single(instruction => instruction.Operation is StructureCompositeConstructionOperation)
                .Operation);
    }

    [Fact]
    public async Task NestedPlainStructureZeroInitializesEveryFieldBeforeProjection()
    {
        var method = Method("NestedField", typeof(float), typeof(Outer), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldflda, typeof(Outer).GetField(nameof(Outer.Inner))!);
            il.Emit(OpCodes.Ldfld, typeof(Fields).GetField(nameof(Fields.Fraction))!);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(0f, method.Invoke(null, null));
        var (raw, promoted, store) = Initialized(method);
        Assert.Same(Assert.Single(raw.DeclarationContext.LocalVariables).Value, store.Operand0);
        Assert.Equal(2, promoted.Graph.Labels()
            .SelectMany(label => promoted.Graph[label].Body.Elements)
            .Count(instruction => instruction.Operation is StructureCompositeConstructionOperation &&
                                  instruction.Payload is ShaderStackProvenance { Synthetic: true }));
        var region = CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method))
            .RunPass(new FunctionToOperationPass())
            .RunPass(new StablePointerRegionParameterPass());
        await new SlangService().ValidateAsync(new SlangEmitter(new SlangTargetLowering().Lower(region)).Emit());
    }

    [Fact]
    public void MappedVectorLocalUsesExplicitComponentZeros()
    {
        var method = Method("Vector", typeof(vec2f32), typeof(vec2f32), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(default(vec2f32), method.Invoke(null, null));
        var (_, promoted, store) = Initialized(method);
        Assert.IsType<VecType<DualDrill.Common.Nat.N2, FloatType<DualDrill.Common.Nat.N32>>>(
            store.Operand1!.Type);
        Assert.Single(promoted.Graph.Labels()
            .SelectMany(label => promoted.Graph[label].Body.Elements)
            .Where(instruction => instruction.Operation is VectorCompositeConstructionOperation &&
                                  instruction.Payload is ShaderStackProvenance { Synthetic: true }));
    }

    [Fact]
    public void SyntheticEntryStoreDoesNotMasqueradeAsSourceInitObject()
    {
        var method = Method("InitAgain", typeof(uint), typeof(uint), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Initobj, typeof(uint));
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });
        var (raw, promoted, synthetic) = Initialized(method);
        var init = Assert.Single(raw.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Initobj);
        var native = Assert.Single(promoted.Graph.Labels()
            .SelectMany(label => promoted.Graph[label].Body.Elements)
            .Where(instruction => instruction.Operation is StoreOperation &&
                                  instruction.Payload is ShaderStackProvenance provenance &&
                                  !provenance.Synthetic && provenance.OriginalIndex == init.Index));
        Assert.Same(synthetic.Operand0, native.Operand0);
        Assert.NotEmpty(CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method)).FunctionDefinitions);
    }

    [Fact]
    public void PublicCompilerTranspilesExplicitInitialFloatStoreToWgsl()
    {
        var (shader, entry) = ComputeShader();
        var (raw, promoted, store) = Initialized(entry);
        Assert.DoesNotContain(raw.Code.Instructions, instruction =>
            instruction.Instruction.OpCode == OpCodes.Initobj ||
            instruction.Instruction.OpCode.Name?.StartsWith("stloc", StringComparison.Ordinal) is true);
        Assert.Equal(ShaderType.F32, store.Operand1!.Type);
        Assert.Equal(store, promoted.Graph[promoted.Graph.EntryLabel].Body.Elements.ElementAt(1));

        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        Assert.Contains(" : f32 =", slang);
        Assert.Contains("@compute", wgsl);
        Assert.Contains("0.0", wgsl);
    }

    [Theory]
    [InlineData(false, 0u)]
    [InlineData(true, 7u)]
    public void BranchReadsInitialOrWrittenUnsignedValue(bool assign, uint expected)
    {
        var method = Branch();
        Assert.Equal(expected, method.Invoke(null, [assign]));
        var (_, promoted, store) = Initialized(method);
        Assert.Equal(store, promoted.Graph[promoted.Graph.EntryLabel].Body.Elements.ElementAt(1));
    }

    [Fact]
    public async Task EntryBackedgeInitializesOnceBeforeLoopAndRetainsLaterWrite()
    {
        var method = Method("EntryBackedge", typeof(uint), typeof(uint), true, (il, local) =>
        {
            var start = il.DefineLabel();
            var done = il.DefineLabel();
            il.MarkLabel(start);
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Brtrue_S, done);
            il.Emit(OpCodes.Ldc_I4_7);
            il.Emit(OpCodes.Stloc, local);
            il.Emit(OpCodes.Br_S, start);
            il.MarkLabel(done);
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });

        Assert.Equal(7u, method.Invoke(null, null));
        var stages = CompilerTestPipeline.CompileStages(method);
        var (_, promoted, store) = Initialized(method);
        var originalEntry = promoted.Source.Graph.EntryLabel;
        Assert.NotSame(originalEntry, promoted.Graph.EntryLabel);
        Assert.NotEmpty(promoted.Graph.Predecessor(originalEntry));
        var preheader = promoted.Graph[promoted.Graph.EntryLabel];
        Assert.Empty(preheader.Parameters);
        Assert.Equal(store, preheader.Body.Elements.Last());
        Assert.DoesNotContain(promoted.Graph[originalEntry].Body.Elements, instruction =>
            instruction.Operation is StoreOperation &&
            instruction.Payload is ShaderStackProvenance { Synthetic: true });
        var region = CilModuleCompiler.Compile(stages.Raw)
            .RunPass(new FunctionToOperationPass())
            .RunPass(new StablePointerRegionParameterPass());
        var source = new SlangEmitter(new SlangTargetLowering().Lower(region)).Emit();
        await new DualDrill.CLSL.SlangService().ValidateAsync(source);
    }

    [Fact]
    public void EdgeOnlyLocalAddressCountsAsReferenced()
    {
        var method = Method("EdgeOnly", typeof(void), typeof(float), true, (il, local) =>
        {
            var target = il.DefineLabel();
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Br_S, target);
            il.MarkLabel(target);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
        });
        var (raw, _, store) = Initialized(method);
        Assert.Same(Assert.Single(raw.DeclarationContext.LocalVariables).Value, store.Operand0);
    }

    [Fact]
    public void FalseFlagUnusedAndPromotedLocalsDoNotGainStorageZeros()
    {
        var noInit = Method("NoInit", typeof(float), typeof(float), false, (il, local) =>
        {
            il.Emit(OpCodes.Ldc_R4, 5f);
            il.Emit(OpCodes.Stloc, local);
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });
        var unused = Method("Unused", typeof(int), typeof(uint), true, (il, _) =>
        {
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);
        });
        var promotedInt = Method("Promoted", typeof(int), typeof(int), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });
        foreach (var method in new[] { noInit, unused, promotedInt })
        {
            var stages = CompilerTestPipeline.CompileStages(method);
            var body = Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values);
            Assert.Empty(InitialStores(body));
            if (method == noInit || method == unused)
                Assert.Same(Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values).Graph, body.Graph);
        }
        Assert.Equal(5f, noInit.Invoke(null, null));
        Assert.Equal(1, unused.Invoke(null, null));
        Assert.Equal(0, promotedInt.Invoke(null, null));
    }

    [Fact]
    public void ExistingWideExplicitAssignmentIsUntouched()
    {
        var method = Method("Wide", typeof(long), typeof(long), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldc_I8, 13L);
            il.Emit(OpCodes.Stloc, local);
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(13L, method.Invoke(null, null));
        var stages = CompilerTestPipeline.CompileStages(method);
        Assert.Empty(InitialStores(Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values)));
    }

    [Fact]
    public void SupportedLeafInsideUnsupportedWholeRootDoesNotGetAnInitializationClaim()
    {
        var method = Method("UnsupportedRoot", typeof(int), typeof(UnsupportedWhole), true, (il, local) =>
        {
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldc_I4, 9);
            il.Emit(OpCodes.Stfld, typeof(UnsupportedWhole).GetField(nameof(UnsupportedWhole.Value))!);
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Ldfld, typeof(UnsupportedWhole).GetField(nameof(UnsupportedWhole.Value))!);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(9, method.Invoke(null, null));
        var stages = CompilerTestPipeline.CompileStages(method);
        Assert.Empty(InitialStores(Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values)));
    }

    [Fact]
    public void ReferencedLocalWithWrongMetadataDeclarationIsRejected()
    {
        var method = Method("WrongMapping", typeof(uint), typeof(uint), true, (il, local) =>
        {
            _ = il.DeclareLocal(typeof(float));
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        });
        var stages = CompilerTestPipeline.CompileStages(method);
        var originalRaw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var swapped = originalRaw.DeclarationContext.LocalVariables.Reverse().ToImmutableArray();
        var badRaw = new RawCilFunctionBody(
            originalRaw.Declaration, originalRaw.Code, swapped, originalRaw.Symbols);
        var pre = new PreCilFunctionBody(badRaw, Assert.Single(stages.Pre.FunctionDefinitions.Values).Code);
        var labelled = new LabelledCilFunctionBody(pre, Assert.Single(stages.Labelled.FunctionDefinitions.Values).Blocks);
        var shader = new ShaderStackFunctionBody(labelled, Assert.Single(stages.ShaderStack.FunctionDefinitions.Values).Blocks);
        var control = new ShaderStackControlFlowBody(
            shader, Assert.Single(stages.ShaderControlFlow.FunctionDefinitions.Values).Graph);
        var value = new CilValueControlFlowBody(
            control, Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values).Graph);
        var malformed = new ShaderModuleDeclaration<CilValueControlFlowBody>(
            stages.ValueControlFlow.Declarations,
            ImmutableDictionary<FunctionDeclaration, CilValueControlFlowBody>.Empty
                .Add(value.Declaration, value));

        var error = Assert.Throws<ValidationException>(() => CilLocalPromotionPass.Run(malformed));
        Assert.Contains("mismatched CLR metadata", error.Message);
        Assert.Contains(method.Name, error.Message);
    }

    private static (RawCilFunctionBody Raw, CilValueControlFlowBody Promoted,
        Instruction<IShaderValue, IShaderValue> Store) Initialized(MethodInfo method)
    {
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        Assert.True(raw.Code.Environment.Body!.InitLocals);
        var promoted = Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values);
        var store = Assert.Single(InitialStores(promoted));
        Assert.IsType<ShaderStackProvenance>(store.Payload);
        var provenance = (ShaderStackProvenance)store.Payload!;
        Assert.True(provenance.Synthetic);
        Assert.Equal(0, provenance.OriginalIndex);
        Assert.Equal(raw.Code[0].ByteOffset, provenance.ByteStart);
        Assert.Equal(raw.Code[0].NextByteOffset, provenance.ByteEnd);
        Assert.Null(store.Result);
        return (raw, promoted, store);
    }

    private static Instruction<IShaderValue, IShaderValue>[] InitialStores(CilValueControlFlowBody body) =>
        body.Graph.Labels().SelectMany(label => body.Graph[label].Body.Elements)
            .Where(instruction => instruction.Operation is StoreOperation &&
                                  instruction.Payload is ShaderStackProvenance { Synthetic: true })
            .ToArray();

    private static MethodInfo Branch()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("InitLocalsBranch"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("InitLocalsBranch").DefineType(
            "InitLocalsBranch", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod("Branch", MethodAttributes.Public | MethodAttributes.Static,
            typeof(uint), [typeof(bool)]);
        method.DefineParameter(1, ParameterAttributes.None, "assign");
        method.InitLocals = true;
        var il = method.GetILGenerator();
        var local = il.DeclareLocal(typeof(uint));
        var done = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Brfalse_S, done);
        il.Emit(OpCodes.Ldc_I4_7);
        il.Emit(OpCodes.Stloc, local);
        il.MarkLabel(done);
        il.Emit(OpCodes.Ldloc, local);
        il.Emit(OpCodes.Ret);
        return type.CreateType()!.GetMethod("Branch")!;
    }

    private static (ISharpShader Shader, MethodInfo Entry) ComputeShader()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("InitLocalsCompute"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("InitLocalsCompute").DefineType(
            "InitLocalsCompute", TypeAttributes.Public, typeof(object), [typeof(ISharpShader)]);
        _ = type.DefineDefaultConstructor(MethodAttributes.Public);
        var output = type.DefineField("Output", typeof(RWStructuredBuffer<float>),
            FieldAttributes.Private | FieldAttributes.Static);
        output.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(GroupAttribute).GetConstructor([typeof(int)])!, [0]));
        output.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BindingAttribute).GetConstructor([typeof(int), typeof(bool)])!, [0, false]));

        var run = type.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), Type.EmptyTypes);
        run.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(ComputeAttribute).GetConstructor(Type.EmptyTypes)!, []));
        run.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(WorkgroupSizeAttribute).GetConstructor([typeof(int), typeof(int), typeof(int)])!,
            [1, 1, 1]));
        run.InitLocals = true;
        var il = run.GetILGenerator();
        _ = il.DeclareLocal(typeof(float));
        il.Emit(OpCodes.Ldsflda, output);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Conv_U4);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Call, typeof(RWStructuredBuffer<float>).GetProperty("Item")!.SetMethod!);
        il.Emit(OpCodes.Ret);
        var complete = type.CreateType()!;
        return ((ISharpShader)Activator.CreateInstance(complete)!,
            complete.GetMethod("Run")!);
    }

    private static MethodInfo Method(
        string name, Type result, Type localType, bool initLocals,
        Action<ILGenerator, LocalBuilder> emit)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"InitLocals_{name}"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule($"InitLocals_{name}").DefineType(
            $"InitLocals_{name}", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static, result, Type.EmptyTypes);
        method.InitLocals = initLocals;
        var il = method.GetILGenerator();
        emit(il, il.DeclareLocal(localType));
        return type.CreateType()!.GetMethod(name)!;
    }
}
