using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Test;

public sealed class CilPreStackAnalysisTests
{
    [Fact]
    public void DeadSourceIsRetainedButExcludedFromPreGraphAndCalleeCompilation()
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(Fixtures.DeadUnsupportedAndCall);
        var model = parser.Context.GetFunctionDefinition(declaration);
        var deadCall = Assert.Single(
            model.Instructions,
            instruction => instruction.Instruction.Operand is MethodBase method &&
                           method.Name == Fixtures.DeadCallee.Name);
        var deadUnsupported = Assert.Single(
            model.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Dup);
        var liveLabel = Assert.IsType<CilControlFlow.Branch>(
            model.ControlFlowGraph[model.ControlFlowGraph.EntryLabel].Terminator).Target;
        var liveTarget = model[model.LabelToInstructionIndex(liveLabel)];

        Assert.False(model.PreStackTypes.ContainsKey(deadCall.Index));
        Assert.False(model.PreStackTypes.ContainsKey(deadUnsupported.Index));
        Assert.True(model.PreStackTypes.TryGetValue(liveTarget.Index, out var livePre));
        Assert.Empty(livePre);
        Assert.Equal(
            Enumerable.Range(0, model.InstructionCount),
            model.Instructions.Select(instruction => instruction.Index));
        Assert.True(model.ControlFlowGraph.Labels()
                         .Sum(label => model.ControlFlowGraph[label].InstructionCount) <
                    model.InstructionCount);
        Assert.Equal(2, model.ControlFlowGraph.Count);
        Assert.Single(model.ControlFlowGraph.Predecessor(liveLabel));
        Assert.Null(parser.Context[Symbol.Function(Fixtures.DeadCallee)]);
        Assert.DoesNotContain(parser.MethodBodies.Keys, function => function.Name == Fixtures.DeadCallee.Name);
    }

    [Fact]
    public void LoopCarriedNonEmptyStackUsesOriginalBackwardTarget()
    {
        var model = ParseModel(Fixtures.LoopCarried);
        var backward = model.Labels.Select(label => model.ControlFlowGraph[label])
                            .Single(block => block.Terminator is CilControlFlow.ConditionalBranch control &&
                                             model.LabelToInstructionIndex(control.BranchTarget) <
                                             block.InstructionIndex);
        var control = Assert.IsType<CilControlFlow.ConditionalBranch>(backward.Terminator);
        var targetIndex = model.LabelToInstructionIndex(control.BranchTarget);

        Assert.Collection(
            model.PreStackTypes[targetIndex],
            type => Assert.IsType<CilStackType.Int32>(type));
        Assert.Collection(
            model.PreStackTypes[backward.InstructionIndex],
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
            model.ControlFlowGraph[model.ControlFlowGraph.EntryLabel].Terminator);
        var block = model.ControlFlowGraph[branch.Target];
        var body = parser.MethodBodies[declaration][branch.Target];

        Assert.Collection(
            block.EntryStackTypes,
            type => Assert.IsType<CilStackType.Float32>(type),
            type => Assert.IsType<CilStackType.Int32>(type));
        Assert.Collection(
            body.Parameters,
            value => Assert.Equal(IntType<N32>.Instance, value.Type),
            value => Assert.Equal(FloatType<N32>.Instance, value.Type));
    }

    [Fact]
    public void DiamondWithEqualTypesMergesAtOriginalInstruction()
    {
        var model = ParseModel(GetMethod(nameof(Diamond)));
        var merge = model.Labels.Select(label => model.ControlFlowGraph[label])
                         .Single(block => model.ControlFlowGraph.Predecessor(block.Label).Count == 2);

        Assert.Collection(
            model.PreStackTypes[merge.InstructionIndex],
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
        var model = new MethodBodyAnalysisModel(method);
        var branch = model.Instructions.First(instruction =>
            instruction.Instruction.OpCode.FlowControl == FlowControl.Cond_Branch);
        var malformed = branch with { NextByteOffset = int.MaxValue };
        var resolver = typeof(MethodBodyAnalysisModel).GetMethod(
            "ResolveBranchTarget",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Branch target resolver was not found.");
        var invocation = Assert.Throws<TargetInvocationException>(() => resolver.Invoke(model, [malformed]));
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

    private static MethodInfo GetMethod(string name) =>
        typeof(CilPreStackAnalysisTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static MethodBodyAnalysisModel ParseModel(MethodInfo method)
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(method);
        return parser.Context.GetFunctionDefinition(declaration);
    }

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

        private static MethodInfo Method(string name) =>
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
            var live = deadIl.DefineLabel();
            deadIl.Emit(OpCodes.Br, live);
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
    }
}
