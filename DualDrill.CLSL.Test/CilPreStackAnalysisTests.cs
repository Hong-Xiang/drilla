using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Test;

public sealed class CilPreStackAnalysisTests
{
    [Fact]
    public void RawDecodeProducesOnlyTheRawLinearStage()
    {
        LinearCode<CilInstructionInfo> raw = CilMethodDecoder.Decode(GetMethod(nameof(Diamond)));

        Assert.NotEmpty(raw.Instructions);
        Assert.All(raw.Instructions, instruction => Assert.InRange(instruction.Index, 0, raw.Count - 1));
    }

    [Fact]
    public void ParserFailureBeforeModelRegistrationPoisonsOnlyThatParser()
    {
        var parser = new RuntimeReflectionParser();
        var method = GetMethod(nameof(WithFinally));

        Assert.Throws<NotSupportedException>(() => parser.ParseMethod(method));
        var declaration = Assert.IsType<FunctionDeclaration>(parser.Context[Symbol.Function(method)]);
        Assert.Throws<KeyNotFoundException>(() => parser.Context.GetFunctionDefinition(declaration));
        Assert.Empty(parser.MethodBodies);

        var retry = Assert.Throws<InvalidOperationException>(() => parser.ParseMethod(method));
        Assert.Contains("fresh compilation context", retry.Message);
        Assert.Throws<InvalidOperationException>(() => parser.ParseMethodBody3(declaration));

        _ = new RuntimeReflectionParser().ParseMethod(GetMethod(nameof(Diamond)));
    }

    [Fact]
    public void ParserFailureAfterRegistrationCannotBecomeSuccessOnRetry()
    {
        var parser = new RuntimeReflectionParser();
        var method = ((Func<uint, uint>)BooleanCallShader.ForwardUnsigned).Method;

        Assert.Throws<ValidationException>(() => parser.ParseMethod(method));
        var declaration = Assert.IsType<FunctionDeclaration>(parser.Context[Symbol.Function(method)]);
        var model = parser.Context.GetFunctionDefinition(declaration);
        var call = Assert.Single(
            model.RawCode.Instructions,
            instruction => instruction.Instruction.OpCode.FlowControl == FlowControl.Call);

        Assert.IsType<CilStackType.Int32>(Assert.Single(Pre(model, call.Index).Types));
        Assert.Empty(parser.MethodBodies);
        var retry = Assert.Throws<InvalidOperationException>(() => parser.ParseMethod(method));
        Assert.Contains(method.Name, retry.Message);
        Assert.Throws<InvalidOperationException>(() => parser.ParseMethodBody3(declaration));
    }

    [Fact]
    public void ReachableCalleeFailurePoisonsAndUnwindsTheCallerParser()
    {
        var parser = new RuntimeReflectionParser();
        var callerMethod = GetMethod(nameof(CallFailingCallee));

        Assert.Throws<ValidationException>(() => parser.ParseMethod(callerMethod));
        var caller = Assert.IsType<FunctionDeclaration>(parser.Context[Symbol.Function(callerMethod)]);
        var callerModel = parser.Context.GetFunctionDefinition(caller);

        Assert.Same(caller, callerModel.Declaration);
        Assert.Empty(parser.MethodBodies);
        var retry = Assert.Throws<InvalidOperationException>(() => parser.ParseMethod(callerMethod));
        Assert.Contains("fresh compilation context", retry.Message);
        Assert.Throws<InvalidOperationException>(() => parser.ParseMethodBody3(caller));
    }

    [Fact]
    public void RepeatedSuccessfulParseReturnsTheCompletedDefinition()
    {
        var parser = new RuntimeReflectionParser();
        var method = GetMethod(nameof(Diamond));

        var first = parser.ParseMethod(method);
        var second = parser.ParseMethod(method);

        Assert.Same(first, second);
        Assert.Single(parser.MethodBodies);
        Assert.Same(parser.MethodBodies[first], parser.MethodBodies[second]);
    }

    [Fact]
    public void ReachableMutualRecursionCompletesBothDefinitions()
    {
        var parser = new RuntimeReflectionParser();

        var first = parser.ParseMethod(GetMethod(nameof(MutualA)));

        Assert.Contains(first, parser.MethodBodies.Keys);
        Assert.Contains(parser.MethodBodies.Keys, declaration => declaration.Name == nameof(MutualB));
        Assert.Equal(2, parser.MethodBodies.Count);
    }

    [Theory]
    [InlineData(nameof(ThrowingShaderIntrinsic))]
    [InlineData(nameof(ThrowingOperationIntrinsic))]
    public void UserDefinedIntrinsicIsDeclarationOnlyAndDoesNotPoisonParser(string methodName)
    {
        var parser = new RuntimeReflectionParser();
        var method = GetMethod(methodName);

        var declaration = parser.ParseMethod(method);

        Assert.DoesNotContain(declaration, parser.MethodBodies.Keys);
        Assert.Throws<KeyNotFoundException>(() => parser.Context.GetFunctionDefinition(declaration));
        _ = parser.ParseMethod(GetMethod(nameof(Diamond)));
        Assert.Single(parser.MethodBodies);
    }

    [Theory]
    [InlineData(nameof(CallThrowingShaderIntrinsic), nameof(ThrowingShaderIntrinsic))]
    [InlineData(nameof(CallThrowingOperationIntrinsic), nameof(ThrowingOperationIntrinsic))]
    public void ReachableUserDefinedIntrinsicCalleeIsNotBodyCompiled(string callerName, string intrinsicName)
    {
        var parser = new RuntimeReflectionParser();
        var intrinsicMethod = GetMethod(intrinsicName);

        var caller = parser.ParseMethod(GetMethod(callerName));
        var intrinsic = Assert.IsType<FunctionDeclaration>(parser.Context[Symbol.Function(intrinsicMethod)]);

        Assert.Contains(caller, parser.MethodBodies.Keys);
        Assert.DoesNotContain(intrinsic, parser.MethodBodies.Keys);
        Assert.Throws<KeyNotFoundException>(() => parser.Context.GetFunctionDefinition(intrinsic));
        _ = parser.ParseMethod(GetMethod(nameof(Diamond)));
        Assert.Equal(2, parser.MethodBodies.Count);
    }

    [Fact]
    public void DeadSourceIsRetainedButExcludedFromPreGraphAndCalleeCompilation()
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(Fixtures.DeadUnsupportedAndCall);
        var model = parser.Context.GetFunctionDefinition(declaration);
        var deadCall = Assert.Single(
            model.RawCode.Instructions,
            instruction => instruction.Instruction.Operand is MethodBase method &&
                           method.Name == Fixtures.DeadCallee.Name);
        var deadUnsupported = Assert.Single(
            model.RawCode.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Dup);
        var deadInitObject = Assert.Single(
            model.RawCode.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Initobj);
        var liveLabel = Assert.IsType<CilControlFlow.Branch>(
            model.ControlFlow.Node[model.ControlFlow.Node.EntryLabel].Terminator).Target;
        var liveTarget = model[model.LabelToInstructionIndex(liveLabel)];

        Assert.DoesNotContain(model.PreAnnotatedCode.Instructions, item => item.Node.Index == deadCall.Index);
        Assert.DoesNotContain(model.PreAnnotatedCode.Instructions, item => item.Node.Index == deadUnsupported.Index);
        Assert.DoesNotContain(model.PreAnnotatedCode.Instructions, item => item.Node.Index == deadInitObject.Index);
        Assert.Empty(Pre(model, liveTarget.Index).Types);
        Assert.Equal(
            Enumerable.Range(0, model.InstructionCount),
            model.RawCode.Instructions.Select(instruction => instruction.Index));
        Assert.True(model.ControlFlow.Node.Labels()
                         .Sum(label => model.ControlFlow.Node[label].InstructionCount) <
                    model.InstructionCount);
        Assert.Equal(2, model.ControlFlow.Node.Count);
        Assert.Single(model.ControlFlow.Node.Predecessor(liveLabel));
        Assert.Null(parser.Context[Symbol.Function(Fixtures.DeadCallee)]);
        Assert.DoesNotContain(parser.MethodBodies.Keys, function => function.Name == Fixtures.DeadCallee.Name);
    }

    [Fact]
    public void LoopCarriedNonEmptyStackUsesOriginalBackwardTarget()
    {
        var model = ParseModel(Fixtures.LoopCarried);
        var backward = model.Labels.Select(label => model.ControlFlow.Node[label])
                            .Single(block => block.Terminator is CilControlFlow.ConditionalBranch control &&
                                             model.LabelToInstructionIndex(control.BranchTarget) <
                                             block.InstructionIndex);
        var control = Assert.IsType<CilControlFlow.ConditionalBranch>(backward.Terminator);
        var targetIndex = model.LabelToInstructionIndex(control.BranchTarget);

        Assert.Collection(
            Pre(model, targetIndex).Types,
            type => Assert.IsType<CilStackType.Int32>(type));
        Assert.Collection(
            Pre(model, backward.InstructionIndex).Types,
            type => Assert.IsType<CilStackType.Int32>(type));
    }

    [Fact]
    public void DeadSwitchStillFailsWholeSourceControlValidation()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(Fixtures.DeadSwitch));

        Assert.Contains("Switch", exception.Message);
        Assert.Contains("IL_", exception.Message);
        Assert.Contains(Fixtures.DeadSwitch.Name, exception.Message);
    }

    [Fact]
    public void ParserCreatesBottomToTopBlockParametersFromAnalyzedPre()
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(Fixtures.TwoSlotEdge);
        var model = parser.Context.GetFunctionDefinition(declaration);
        var branch = Assert.IsType<CilControlFlow.Branch>(
            model.ControlFlow.Node[model.ControlFlow.Node.EntryLabel].Terminator);
        var block = model.ControlFlow.Node[branch.Target];
        var body = parser.MethodBodies[declaration][branch.Target];

        Assert.Contains(model.RawCode.Instructions, instruction => instruction.Instruction.OpCode == OpCodes.Pop);
        Assert.Collection(
            block.EntryStack.Types,
            type => Assert.IsType<CilStackType.Float32>(type),
            type => Assert.IsType<CilStackType.Int32>(type));
        Assert.Collection(
            body.Parameters,
            value => Assert.Equal(IntType<N32>.Instance, value.Type),
            value => Assert.Equal(FloatType<N32>.Instance, value.Type));
    }

    [Fact]
    public void ReachableInitObjectIsRejectedInsteadOfDroppingTheZeroStore()
    {
        Assert.Equal(0, ResetAfterWrite());

#if DEBUG
        var ordinary = GetMethod(nameof(ResetAfterWrite));
        Assert.Contains(
            CilMethodDecoder.Decode(ordinary).Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Initobj);
        AssertInitObjectRejected(ordinary);
#endif

        AssertInitObjectRejected(Fixtures.ReachableInitObject);
    }

    [Fact]
    public void ScalarInputsAndStoresUseTheDocumentedPreNormalization()
    {
        var model = ParseModel(Fixtures.ScalarStorage);
        var stores = model.RawCode.Instructions
                          .Where(instruction => instruction.Instruction.OpCode.Name?.StartsWith("stloc") == true)
                          .ToArray();
        CilStackType[] expected =
        [
            CilStackType.Int32.Instance,
            CilStackType.Int32.Instance,
            CilStackType.Int32.Instance,
            CilStackType.Int32.Instance,
            CilStackType.Int32.Instance,
            CilStackType.Int32.Instance,
            CilStackType.Int32.Instance,
            CilStackType.Int64.Instance,
            CilStackType.Int64.Instance,
            CilStackType.Float32.Instance,
            CilStackType.Float64.Instance
        ];

        Assert.Equal(expected.Length, stores.Length);
        foreach (var (store, type) in stores.Zip(expected))
        {
            Assert.Equal(type, Assert.Single(Pre(model, store.Index).Types));
            Assert.Empty(Pre(model, store.Index + 1).Types);
        }
    }

    [Fact]
    public void SupportedScalarCallsSeeNormalizedPreFacts()
    {
        var model = ParseModel(Fixtures.SupportedScalarCalls);
        var calls = model.RawCode.Instructions
                         .Where(instruction => instruction.Instruction.Operand is MethodBase called &&
                                               called.Name.StartsWith("Identity"))
                         .ToArray();
        CilStackType[] expected =
        [
            CilStackType.Int32.Instance,
            CilStackType.Int32.Instance,
            CilStackType.Int64.Instance,
            CilStackType.Float32.Instance,
            CilStackType.Float64.Instance
        ];

        Assert.Equal(expected.Length, calls.Length);
        foreach (var (call, type) in calls.Zip(expected))
            Assert.Equal(type, Assert.Single(Pre(model, call.Index).Types));
    }

    [Theory]
    [InlineData("CallSByte", false)]
    [InlineData("CallByte", false)]
    [InlineData("CallUInt32", false)]
    [InlineData("CallUInt64", true)]
    public void NarrowAndUnsignedCallsHaveNormalizedPreBeforeExistingValueRejection(
        string methodName,
        bool isInt64)
    {
        var parser = new RuntimeReflectionParser();
        var method = Fixtures.Method(methodName);

        Assert.Throws<ValidationException>(() => parser.ParseMethod(method));
        var declaration = Assert.IsType<FunctionDeclaration>(parser.Context[Symbol.Function(method)]);
        var model = parser.Context.GetFunctionDefinition(declaration);
        var call = Assert.Single(
            model.RawCode.Instructions,
            instruction => instruction.Instruction.OpCode.FlowControl == FlowControl.Call);
        var type = Assert.Single(Pre(model, call.Index).Types);

        if (isInt64)
            Assert.IsType<CilStackType.Int64>(type);
        else
            Assert.IsType<CilStackType.Int32>(type);
    }

    [Fact]
    public void DiamondWithEqualTypesMergesAtOriginalInstruction()
    {
        var model = ParseModel(GetMethod(nameof(Diamond)));
        var merge = model.Labels.Select(label => model.ControlFlow.Node[label])
                         .Single(block => model.ControlFlow.Node.Predecessor(block.Label).Count == 2);

        Assert.Collection(
            Pre(model, merge.InstructionIndex).Types,
            type => Assert.IsType<CilStackType.Int32>(type));
    }

    [Theory]
    [MemberData(nameof(InvalidJoinMethods))]
    public void UnequalJoinStacksAreRejected(MethodInfo method, string expected)
    {
        var exception = Assert.Throws<ValidationException>(() => new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains(expected, exception.Message);
        Assert.Contains("IL_", exception.Message);
        Assert.Contains(method.Name, exception.Message);
    }

    [Fact]
    public void MalformedBranchTargetIsRejectedWithSourceAndTargetContext()
    {
        var method = GetMethod(nameof(Diamond));
        var raw = CilMethodDecoder.Decode(method);
        var branch = raw.Instructions.First(instruction =>
            instruction.Instruction.OpCode.FlowControl == FlowControl.Cond_Branch);
        var malformed = branch with { NextByteOffset = int.MaxValue };
        var resolver = typeof(CilMethodEnvironment).GetMethod(
            "ResolveBranchTarget",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Branch target resolver was not found.");
        var invocation = Assert.Throws<TargetInvocationException>(() => resolver.Invoke(raw.Environment, [malformed]));
        var exception = Assert.IsType<InvalidProgramException>(invocation.InnerException);

        Assert.Contains("Branch target overflows", exception.Message);
        Assert.Contains($"IL_{branch.ByteOffset:X4}", exception.Message);
        Assert.Contains(method.Name, exception.Message);
    }

    public static TheoryData<MethodInfo, string> InvalidJoinMethods =>
        new()
        {
            { Fixtures.HeightMismatch, "stack height mismatch" },
            { Fixtures.TypeMismatch, "stack type mismatch" }
        };

    private static int Diamond(bool choose, int left, int right)
    {
        var value = choose ? left : right;
        return value;
    }

    private static int MutualA(int value) => value <= 0 ? 0 : MutualB(value - 1);

    private static int MutualB(int value) => value <= 0 ? 1 : MutualA(value - 1);

    private static uint CallFailingCallee(uint value) => BooleanCallShader.ForwardUnsigned(value);

    private static int WithFinally(int value)
    {
        try
        {
            return value;
        }
        finally
        {
            GC.KeepAlive(value);
        }
    }

    [VectorCompositeConstructorMethod]
    private static vec2f32 ThrowingShaderIntrinsic(float left, float right) =>
        throw new InvalidOperationException("Intrinsic stubs must never execute or compile.");

    private static vec2f32 CallThrowingShaderIntrinsic(float left, float right) =>
        ThrowingShaderIntrinsic(left, right);

    [OperationMethod<VectorFromScalarConstructOperation<N4, FloatType<N32>>>]
    private static vec4f32 ThrowingOperationIntrinsic(float value) =>
        throw new InvalidOperationException("Intrinsic stubs must never execute or compile.");

    private static vec4f32 CallThrowingOperationIntrinsic(float value) =>
        ThrowingOperationIntrinsic(value);

    private struct Cell
    {
        public int Value;
    }

    private static int ResetAfterWrite()
    {
        Cell cell = default;
        cell.Value = 5;
        cell = default;
        return cell.Value;
    }

    private static void AssertInitObjectRejected(MethodInfo method)
    {
        var exception = Assert.Throws<ValidationException>(() => new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains("initobj", exception.Message);
        Assert.Contains("IL_", exception.Message);
        Assert.Contains(method.Name, exception.Message);
    }

    private static MethodInfo GetMethod(string name) =>
        typeof(CilPreStackAnalysisTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static MethodBodyAnalysisModel ParseModel(MethodInfo method)
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(method);
        return parser.Context.GetFunctionDefinition(declaration);
    }

    private static PreStack Pre(MethodBodyAnalysisModel model, int instructionIndex) =>
        Assert.Single(
            model.PreAnnotatedCode.Instructions,
            instruction => instruction.Node.Index == instructionIndex).Annotation;

    private static class Fixtures
    {
        private static readonly Type FixtureType = BuildType();

        public static MethodInfo DeadUnsupportedAndCall => Method(nameof(DeadUnsupportedAndCall));
        public static MethodInfo DeadCallee => Method(nameof(DeadCallee));
        public static MethodInfo LoopCarried => Method(nameof(LoopCarried));
        public static MethodInfo DeadSwitch => Method(nameof(DeadSwitch));
        public static MethodInfo TwoSlotEdge => Method(nameof(TwoSlotEdge));
        public static MethodInfo HeightMismatch => Method(nameof(HeightMismatch));
        public static MethodInfo TypeMismatch => Method(nameof(TypeMismatch));
        public static MethodInfo ScalarStorage => Method(nameof(ScalarStorage));
        public static MethodInfo SupportedScalarCalls => Method(nameof(SupportedScalarCalls));
        public static MethodInfo ReachableInitObject => Method(nameof(ReachableInitObject));

        public static MethodInfo Method(string name) =>
            FixtureType.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{name} emitted fixture was not found.");

        private static Type BuildType()
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("CilPreStackAnalysisFixtures"),
                AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("CilPreStackAnalysisFixtures");
            var type = module.DefineType(
                "CilPreStackAnalysisFixtures",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            var deadCallee = Define(type, nameof(DeadCallee), typeof(int));
            deadCallee.GetILGenerator().Emit(OpCodes.Ldc_I4_0);
            deadCallee.GetILGenerator().Emit(OpCodes.Ret);

            var dead = Define(type, nameof(DeadUnsupportedAndCall), typeof(int));
            var deadIl = dead.GetILGenerator();
            var deadCell = deadIl.DeclareLocal(typeof(Cell));
            var live = deadIl.DefineLabel();
            deadIl.Emit(OpCodes.Br, live);
            deadIl.Emit(OpCodes.Ldloca, deadCell);
            deadIl.Emit(OpCodes.Initobj, typeof(Cell));
            deadIl.Emit(OpCodes.Ldc_I4_0);
            deadIl.Emit(OpCodes.Dup);
            deadIl.Emit(OpCodes.Pop);
            deadIl.Emit(OpCodes.Pop);
            deadIl.Emit(OpCodes.Call, deadCallee);
            deadIl.Emit(OpCodes.Pop);
            deadIl.MarkLabel(live);
            deadIl.Emit(OpCodes.Ldc_I4, 42);
            deadIl.Emit(OpCodes.Ret);

            var deadSwitch = Define(type, nameof(DeadSwitch), typeof(int));
            var deadSwitchIl = deadSwitch.GetILGenerator();
            var deadSwitchLive = deadSwitchIl.DefineLabel();
            deadSwitchIl.Emit(OpCodes.Br, deadSwitchLive);
            deadSwitchIl.Emit(OpCodes.Ldc_I4_0);
            deadSwitchIl.Emit(OpCodes.Switch, [deadSwitchLive]);
            deadSwitchIl.MarkLabel(deadSwitchLive);
            deadSwitchIl.Emit(OpCodes.Ldc_I4_1);
            deadSwitchIl.Emit(OpCodes.Ret);

            var loop = Define(type, nameof(LoopCarried), typeof(int), typeof(bool));
            loop.DefineParameter(1, ParameterAttributes.None, "repeat");
            var loopIl = loop.GetILGenerator();
            var body = loopIl.DefineLabel();
            var test = loopIl.DefineLabel();
            loopIl.Emit(OpCodes.Ldc_I4_0);
            loopIl.Emit(OpCodes.Br, test);
            loopIl.MarkLabel(body);
            loopIl.Emit(OpCodes.Ldc_I4_1);
            loopIl.Emit(OpCodes.Add);
            loopIl.MarkLabel(test);
            loopIl.Emit(OpCodes.Ldarg_0);
            loopIl.Emit(OpCodes.Brtrue, body);
            loopIl.Emit(OpCodes.Ret);

            var twoSlot = Define(type, nameof(TwoSlotEdge), typeof(int));
            var twoSlotIl = twoSlot.GetILGenerator();
            var twoSlotTarget = twoSlotIl.DefineLabel();
            twoSlotIl.Emit(OpCodes.Ldc_I4_7);
            twoSlotIl.Emit(OpCodes.Ldc_R4, 2.0f);
            twoSlotIl.Emit(OpCodes.Br, twoSlotTarget);
            twoSlotIl.MarkLabel(twoSlotTarget);
            twoSlotIl.Emit(OpCodes.Pop);
            twoSlotIl.Emit(OpCodes.Ret);

            var height = Define(type, nameof(HeightMismatch), typeof(void), typeof(bool));
            height.DefineParameter(1, ParameterAttributes.None, "choose");
            EmitHeightMismatch(height.GetILGenerator());

            var mismatch = Define(type, nameof(TypeMismatch), typeof(void), typeof(bool));
            mismatch.DefineParameter(1, ParameterAttributes.None, "choose");
            EmitTypeMismatch(mismatch.GetILGenerator());

            DefineScalarFixtures(type);
            DefineReachableInitObject(type);

            return type.CreateType()
                   ?? throw new InvalidOperationException("Failed to create emitted CIL fixture type.");
        }

        private static MethodBuilder Define(
            TypeBuilder type,
            string name,
            Type returnType,
            params Type[] parameterTypes) =>
            type.DefineMethod(
                name,
                MethodAttributes.Public | MethodAttributes.Static,
                returnType,
                parameterTypes);

        private static void EmitHeightMismatch(ILGenerator il)
        {
            var valueArm = il.DefineLabel();
            var join = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Brtrue, valueArm);
            il.Emit(OpCodes.Br, join);
            il.MarkLabel(valueArm);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Br, join);
            il.MarkLabel(join);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
        }

        private static void EmitTypeMismatch(ILGenerator il)
        {
            var floatArm = il.DefineLabel();
            var join = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Brtrue, floatArm);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Br, join);
            il.MarkLabel(floatArm);
            il.Emit(OpCodes.Ldc_R4, 1.0f);
            il.MarkLabel(join);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
        }

        private static void DefineScalarFixtures(TypeBuilder type)
        {
            Type[] scalarTypes =
            [
                typeof(bool),
                typeof(sbyte),
                typeof(byte),
                typeof(short),
                typeof(ushort),
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong),
                typeof(float),
                typeof(double)
            ];
            var storage = Define(type, nameof(ScalarStorage), typeof(int), scalarTypes);
            var storageIl = storage.GetILGenerator();
            foreach (var (index, scalarType) in scalarTypes.Index())
            {
                storage.DefineParameter(index + 1, ParameterAttributes.None, $"value{index}");
                var local = storageIl.DeclareLocal(scalarType);
                storageIl.Emit(OpCodes.Ldarg, index);
                storageIl.Emit(OpCodes.Stloc, local);
            }
            storageIl.Emit(OpCodes.Ldc_I4_0);
            storageIl.Emit(OpCodes.Ret);

            var supportedTypes = new[]
            {
                (Name: "IdentityBool", Type: typeof(bool)),
                (Name: "IdentityInt32", Type: typeof(int)),
                (Name: "IdentityInt64", Type: typeof(long)),
                (Name: "IdentityFloat32", Type: typeof(float)),
                (Name: "IdentityFloat64", Type: typeof(double))
            };
            var supportedIdentities = supportedTypes
                                      .Select(item => DefineIdentity(type, item.Name, item.Type))
                                      .ToArray();
            var supportedCalls = Define(
                type,
                nameof(SupportedScalarCalls),
                typeof(int),
                [.. supportedTypes.Select(item => item.Type)]);
            var supportedCallsIl = supportedCalls.GetILGenerator();
            foreach (var (index, identity) in supportedIdentities.Index())
            {
                supportedCalls.DefineParameter(index + 1, ParameterAttributes.None, $"value{index}");
                supportedCallsIl.Emit(OpCodes.Ldarg, index);
                supportedCallsIl.Emit(OpCodes.Call, identity);
                supportedCallsIl.Emit(OpCodes.Pop);
            }
            supportedCallsIl.Emit(OpCodes.Ldc_I4_0);
            supportedCallsIl.Emit(OpCodes.Ret);

            DefineRejectedCall(type, "CallSByte", "IdentitySByte", typeof(sbyte));
            DefineRejectedCall(type, "CallByte", "IdentityByte", typeof(byte));
            DefineRejectedCall(type, "CallUInt32", "IdentityUInt32", typeof(uint));
            DefineRejectedCall(type, "CallUInt64", "IdentityUInt64", typeof(ulong));
        }

        private static void DefineReachableInitObject(TypeBuilder type)
        {
            var method = Define(type, nameof(ReachableInitObject), typeof(int));
            var il = method.GetILGenerator();
            var cell = il.DeclareLocal(typeof(Cell));
            var value = typeof(Cell).GetField(nameof(Cell.Value))
                        ?? throw new InvalidOperationException("Cell.Value field was not found.");
            il.Emit(OpCodes.Ldloca, cell);
            il.Emit(OpCodes.Initobj, typeof(Cell));
            il.Emit(OpCodes.Ldloca, cell);
            il.Emit(OpCodes.Ldc_I4_5);
            il.Emit(OpCodes.Stfld, value);
            il.Emit(OpCodes.Ldloca, cell);
            il.Emit(OpCodes.Initobj, typeof(Cell));
            il.Emit(OpCodes.Ldloca, cell);
            il.Emit(OpCodes.Ldfld, value);
            il.Emit(OpCodes.Ret);
        }

        private static MethodBuilder DefineIdentity(TypeBuilder type, string name, Type scalarType)
        {
            var identity = Define(type, name, scalarType, scalarType);
            identity.DefineParameter(1, ParameterAttributes.None, "value");
            var il = identity.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ret);
            return identity;
        }

        private static void DefineRejectedCall(
            TypeBuilder type,
            string callerName,
            string calleeName,
            Type scalarType)
        {
            var identity = DefineIdentity(type, calleeName, scalarType);
            var caller = Define(type, callerName, scalarType, scalarType);
            caller.DefineParameter(1, ParameterAttributes.None, "value");
            var il = caller.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, identity);
            il.Emit(OpCodes.Ret);
        }
    }
}
