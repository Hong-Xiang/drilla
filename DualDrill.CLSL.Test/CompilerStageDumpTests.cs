using System.CodeDom.Compiler;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using Label = DualDrill.CLSL.Language.Symbol.Label;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class CompilerStageDumpTests(ITestOutputHelper output)
{
    [Fact]
    public void RawPrettyPrintWorksWithoutConstructingCompletedStages()
    {
        LinearCode<CilInstructionInfo> code = CilMethodDecoder.Decode(GetMethod(nameof(Choose)));
        var raw = code.PrettyPrint();

        Assert.Contains("linear-cil raw", raw);
        Assert.Contains(" rel=+", raw);
        Assert.Contains(" resolved=IL_", raw);
        Assert.DoesNotContain(" pre=", raw);

        var deadSwitch = CilMethodDecoder.Decode(EmittedFixtures.DeadSwitch);
        Assert.Contains("switch rels=[", deadSwitch.PrettyPrint());
    }

    [Fact]
    public void ChooseDumpsActualConfigurationSpecificLinearCilAndCfg()
    {
        var model = ParseModel(GetMethod(nameof(Choose)));
        var blocks = model.Blocks;
        var shader = CompilerTestPipeline.ShaderStack(GetMethod(nameof(Choose)));
        var shaderGraph = CompilerTestPipeline.ShaderControlFlow(GetMethod(nameof(Choose)));
        var configuration = GetType().Assembly
                                     .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        output.WriteLine(model.PreAnnotatedCode.PrettyPrint());
        output.WriteLine(blocks.PrettyPrint());
        output.WriteLine(shader.PrettyPrint());
        output.WriteLine(shaderGraph.PrettyPrint());
        Assert.Equal(blocks.PrettyPrint(), blocks.PrettyPrint());

        switch (configuration)
        {
            case "Debug":
                Assert.Equal(
                [
                    "linear-cil pre-annotated reachable (byte ranges are half-open; stack order: bottom -> top)",
                    "#0 IL_0000..IL_0001 ldarg.0 pre=[]",
                    "#1 IL_0001..IL_0003 brtrue.s rel=+3 resolved=IL_0006 pre=[i32]",
                    "#2 IL_0003..IL_0004 ldarg.2 pre=[]",
                    "#3 IL_0004..IL_0006 br.s rel=+1 resolved=IL_0007 pre=[i32]",
                    "#4 IL_0006..IL_0007 ldarg.1 pre=[]",
                    "#5 IL_0007..IL_0008 ret pre=[i32]"
                ], Lines(model.PreAnnotatedCode.PrettyPrint()));
                Assert.Equal(
                [
                    "labelled-cil-block-list (storage order; byte ranges are half-open; stack order: bottom -> top)",
                    "entry=^0(0x0)",
                    "^0(0x0) instructions=#0..#1 bytes=IL_0000..IL_0003 entry=[]",
                    "    control: native brtrue.s rel=+3 resolved=IL_0006 taken=^2(0x6) fallthrough=^1(0x3)",
                    "^1(0x3) instructions=#2..#3 bytes=IL_0003..IL_0006 entry=[]",
                    "    control: native br.s rel=+1 resolved=IL_0007 target=^3(0x7)",
                    "^2(0x6) instructions=#4..#4 bytes=IL_0006..IL_0007 entry=[]",
                    "    control: synthetic fallthrough target=^3(0x7)",
                    "^3(0x7) instructions=#5..#5 bytes=IL_0007..IL_0008 entry=[i32]",
                    "    control: native ret"
                ], Lines(blocks.PrettyPrint()));
                break;
            case "Release":
                Assert.Equal(
                [
                    "linear-cil pre-annotated reachable (byte ranges are half-open; stack order: bottom -> top)",
                    "#0 IL_0000..IL_0001 ldarg.0 pre=[]",
                    "#1 IL_0001..IL_0003 brtrue.s rel=+2 resolved=IL_0005 pre=[i32]",
                    "#2 IL_0003..IL_0004 ldarg.2 pre=[]",
                    "#3 IL_0004..IL_0005 ret pre=[i32]",
                    "#4 IL_0005..IL_0006 ldarg.1 pre=[]",
                    "#5 IL_0006..IL_0007 ret pre=[i32]"
                ], Lines(model.PreAnnotatedCode.PrettyPrint()));
                Assert.Equal(
                [
                    "labelled-cil-block-list (storage order; byte ranges are half-open; stack order: bottom -> top)",
                    "entry=^0(0x0)",
                    "^0(0x0) instructions=#0..#1 bytes=IL_0000..IL_0003 entry=[]",
                    "    control: native brtrue.s rel=+2 resolved=IL_0005 taken=^2(0x5) fallthrough=^1(0x3)",
                    "^1(0x3) instructions=#2..#3 bytes=IL_0003..IL_0005 entry=[]",
                    "    control: native ret",
                    "^2(0x5) instructions=#4..#5 bytes=IL_0005..IL_0007 entry=[]",
                    "    control: native ret"
                ], Lines(blocks.PrettyPrint()));
                break;
            default:
                throw new InvalidOperationException($"Unsupported build configuration {configuration}.");
        }
        Assert.Contains("labelled-shader-stack-block-list", shader.PrettyPrint());
        Assert.Contains("shader-stack-cfg", shaderGraph.PrettyPrint());
    }

    [Fact]
    public void BlockListPrintsStorageOrderIncludingDisconnectedDefinitionsAndNonFirstEntry()
    {
        var method = GetMethod(nameof(Choose));
        var pre = Assert.Single(CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(method)).FunctionDefinitions.Values);
        var partition = CilBlockPartitioner.Partition(pre.Raw.Code, pre.Code);
        var returned = partition.Blocks.Last(block => block.Terminator is CilControlFlow.Return);
        var disconnected = new CilInstructionBlock(Label.Create("disconnected"), returned.Instructions, returned.Terminator);
        var reordered = new BlockList<CilInstructionBlock>(
            partition.EntryLabel,
            [disconnected, .. partition.Blocks.Reverse()],
            static block => block.Terminator.ToSuccessor(),
            CilStagePrettyPrinter.PrintBlockList);

        var printed = reordered.PrettyPrint();
        var headers = Lines(printed).Where(line => line.StartsWith("^")).ToArray();

        Assert.Equal(reordered.Blocks.Length, headers.Length);
        Assert.Contains($"entry=^{partition.Blocks.Length}(0x0)", printed);
        foreach (var (index, block) in reordered.Blocks.Index())
            Assert.StartsWith($"^{index}({block.Label.Name}) instructions=#{block.InstructionIndex}..", headers[index]);
        Assert.Equal(printed, reordered.PrettyPrint());
        Assert.DoesNotContain("predecessors", printed);
        Assert.DoesNotContain("rpo", printed);
        Assert.DoesNotContain("cfg", printed);
    }

    [Fact]
    public void RawPrintRetainsDeadSourceWhileCompletedPrintUsesOriginalReachableIndices()
    {
        var model = ParseModel(EmittedFixtures.Dead);

        var rawLines = Lines(model.RawCode.PrettyPrint());
        var lines = Lines(model.PreAnnotatedCode.PrettyPrint());
        Assert.Contains(rawLines, line => line.Contains("operand=99", StringComparison.Ordinal));
        Assert.Contains(rawLines, line => line.Contains("ret", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("operand=99", StringComparison.Ordinal));
        var liveLoad = Assert.Single(lines, line => line.Contains("operand=42", StringComparison.Ordinal));

        Assert.EndsWith("pre=[]", liveLoad, StringComparison.Ordinal);
        Assert.True(model.PreAnnotatedCode.Instructions[1].Node.Index > 1);
        Assert.Equal(2, model.Blocks.Blocks.Length);
        Assert.Equal(2, Lines(model.PrettyPrint()).Count(line => line.StartsWith("^")));
        var blocks = CilBlockPartitioner.Partition(model.RawCode, model.PreAnnotatedCode);
        Assert.Equal(model.PreAnnotatedCode.Instructions, blocks.Blocks.SelectMany(block => block.Instructions));
        Assert.Equal(2, Lines(blocks.PrettyPrint()).Count(line => line.StartsWith("^")));
        Assert.Contains("instructions=#3..#4", blocks.PrettyPrint());
    }

    [Fact]
    public void StackDumpUsesBottomToTopOrderAndCompactPointerAddressSpace()
    {
        var twoSlot = ParseModel(EmittedFixtures.TwoSlot);
        var pointer = ParseModel(EmittedFixtures.Pointer);

        Assert.Contains("pre=[i32, f32]", twoSlot.PreAnnotatedCode.PrettyPrint());
        Assert.Contains("pop pre=[managed-ptr<i32, Function>]", pointer.PreAnnotatedCode.PrettyPrint());
    }

    [Fact]
    public void ConditionalCfgRetainsSameTargetArmsAndOnePredecessorNode()
    {
        var model = ParseModel(EmittedFixtures.SameTarget);
        var dump = model.PrettyPrint();
        var shaderGraph = CompilerTestPipeline.ShaderControlFlow(EmittedFixtures.SameTarget).Graph;
        var conditional = Assert.Single(
            Lines(dump),
            line => line.Contains("control: native brtrue.s", StringComparison.Ordinal));
        var branch = Assert.IsType<ConditionalSuccessor>(
            model[model.Blocks.EntryLabel].Terminator.ToSuccessor());
        var shaderBranch = Assert.IsType<ConditionalSuccessor>(
            shaderGraph.Successor(shaderGraph.EntryLabel));

        Assert.Contains("taken=^1(0x3) fallthrough=^1(0x3)", conditional);
        Assert.Contains("^1(0x3)", dump);
        Assert.Same(branch.TrueTarget, branch.FalseTarget);
        Assert.Same(shaderBranch.TrueTarget, shaderBranch.FalseTarget);
        Assert.Single(shaderGraph.Predecessor(shaderBranch.TrueTarget));
    }

    [Fact]
    public void NumericFormattingIgnoresCallerWriterCulture()
    {
        var model = ParseModel(EmittedFixtures.TwoSlot);
        using var text = new StringWriter(CultureInfo.GetCultureInfo("fr-FR"));
        using var writer = new IndentedTextWriter(text);

        model.PreAnnotatedCode.PrettyPrint(writer, PrettyPrintOption.Default);

        Assert.Contains("operand=2.5", text.ToString());
        Assert.DoesNotContain("operand=2,5", text.ToString());
    }

    [Fact]
    public void ExistingRegionDumpReportsMissingLoopBreakMetadataHonestly()
    {
        var label = Label.Create("loop");
        var declaration = new FunctionDeclaration(
            "Loop",
            [],
            new FunctionReturn(ShaderType.Unit, []),
            []);
        var region = ShaderRegionBody.Create(
            label,
            [],
            [],
            Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>(),
            new ExitPostDominance.FunctionExit(false));
        var body = new RegionFunctionBody(
            declaration,
            RegionTree<Label, ShaderRegionBody>.Loop(label, [], region, null, null));

        var dump = body.Dump();

        Assert.Contains("loop ^0(loop) | break -> <not recorded>", dump);
        Assert.DoesNotContain("<null>", dump);
    }

    private static string[] Lines(string value) =>
        value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private static LabelledCilFunctionBody ParseModel(MethodInfo method)
        => CompilerTestPipeline.Labelled(method);

    private static MethodInfo GetMethod(string name) =>
        typeof(CompilerStageDumpTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static int Choose(bool choose, int left, int right) => choose ? left : right;

    private static class EmittedFixtures
    {
        private static readonly Type FixtureType = BuildType();

        public static MethodInfo Dead => Method(nameof(Dead));
        public static MethodInfo DeadSwitch => Method(nameof(DeadSwitch));
        public static MethodInfo Pointer => Method(nameof(Pointer));
        public static MethodInfo SameTarget => Method(nameof(SameTarget));
        public static MethodInfo TwoSlot => Method(nameof(TwoSlot));

        private static MethodInfo Method(string name) =>
            FixtureType.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{name} emitted fixture was not found.");

        private static Type BuildType()
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("CompilerStageDumpFixtures"),
                AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("CompilerStageDumpFixtures")
                               .DefineType(
                                   "CompilerStageDumpFixtures",
                                   TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            var dead = Define(type, nameof(Dead), typeof(int));
            var deadIl = dead.GetILGenerator();
            var live = deadIl.DefineLabel();
            deadIl.Emit(OpCodes.Br_S, live);
            deadIl.Emit(OpCodes.Ldc_I4, 99);
            deadIl.Emit(OpCodes.Ret);
            deadIl.MarkLabel(live);
            deadIl.Emit(OpCodes.Ldc_I4, 42);
            deadIl.Emit(OpCodes.Ret);

            var deadSwitch = Define(type, nameof(DeadSwitch), typeof(int));
            var deadSwitchIl = deadSwitch.GetILGenerator();
            var deadSwitchTarget = deadSwitchIl.DefineLabel();
            deadSwitchIl.Emit(OpCodes.Br_S, deadSwitchTarget);
            deadSwitchIl.Emit(OpCodes.Ldc_I4_0);
            deadSwitchIl.Emit(OpCodes.Switch, [deadSwitchTarget]);
            deadSwitchIl.MarkLabel(deadSwitchTarget);
            deadSwitchIl.Emit(OpCodes.Ldc_I4_1);
            deadSwitchIl.Emit(OpCodes.Ret);

            var pointer = Define(type, nameof(Pointer), typeof(int), typeof(int));
            pointer.DefineParameter(1, ParameterAttributes.None, "value");
            var pointerIl = pointer.GetILGenerator();
            pointerIl.Emit(OpCodes.Ldarga_S, (byte)0);
            pointerIl.Emit(OpCodes.Pop);
            pointerIl.Emit(OpCodes.Ldc_I4_0);
            pointerIl.Emit(OpCodes.Ret);

            var sameTarget = Define(type, nameof(SameTarget), typeof(int), typeof(bool));
            sameTarget.DefineParameter(1, ParameterAttributes.None, "choose");
            var sameTargetIl = sameTarget.GetILGenerator();
            var target = sameTargetIl.DefineLabel();
            sameTargetIl.Emit(OpCodes.Ldarg_0);
            sameTargetIl.Emit(OpCodes.Brtrue_S, target);
            sameTargetIl.MarkLabel(target);
            sameTargetIl.Emit(OpCodes.Ldc_I4_1);
            sameTargetIl.Emit(OpCodes.Ret);

            var twoSlot = Define(type, nameof(TwoSlot), typeof(int));
            var twoSlotIl = twoSlot.GetILGenerator();
            var twoSlotTarget = twoSlotIl.DefineLabel();
            twoSlotIl.Emit(OpCodes.Ldc_I4_7);
            twoSlotIl.Emit(OpCodes.Ldc_R4, 2.5f);
            twoSlotIl.Emit(OpCodes.Br_S, twoSlotTarget);
            twoSlotIl.MarkLabel(twoSlotTarget);
            twoSlotIl.Emit(OpCodes.Pop);
            twoSlotIl.Emit(OpCodes.Ret);

            return type.CreateType()
                   ?? throw new InvalidOperationException("Failed to create compiler stage dump fixture type.");
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
    }
}
