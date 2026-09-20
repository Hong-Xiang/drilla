using System.CodeDom.Compiler;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;

namespace DualDrill.CLSL.Test;

public sealed class AnnotatedStageValueTests
{
    [Fact]
    public void AnnotatedMapsOnlyTheRequestedComponentsExactlyOnce()
    {
        var node = new object();
        var annotation = new object();
        var value = new Annotated<object, object>(node, annotation, PrintNothing);
        var nodeCalls = 0;
        var annotationCalls = 0;

        var selected = value.Select(
            original =>
            {
                nodeCalls++;
                Assert.Same(node, original);
                return "node";
            },
            original =>
            {
                annotationCalls++;
                Assert.Same(annotation, original);
                return "annotation";
            },
            static (selectedNode, selectedAnnotation, output, _) =>
                output.Write($"{selectedNode}:{selectedAnnotation}"));
        var nodeOnly = value.SelectNode(original =>
        {
            Assert.Same(node, original);
            return "node-only";
        }, PrintNothing);
        var annotationOnly = value.SelectAnnotation(original =>
        {
            Assert.Same(annotation, original);
            return "annotation-only";
        }, PrintNothing);

        Assert.Equal(1, nodeCalls);
        Assert.Equal(1, annotationCalls);
        Assert.Equal(new Annotated<string, string>("node", "annotation", PrintNothing), selected);
        Assert.Equal("node:annotation", selected.PrettyPrint());
        Assert.Same(annotation, nodeOnly.Annotation);
        Assert.Same(node, annotationOnly.Node);
    }

    [Fact]
    public void AnnotatedSelectSatisfiesIdentityAndComposition()
    {
        var value = new Annotated<int, string>(7, "entry", PrintNothing);

        var identity = value.Select(static node => node, static annotation => annotation, PrintNothing);
        var sequential = value.Select(static node => node + 1, static annotation => annotation + "!", PrintNothing)
                              .Select(static node => node * 2, static annotation => annotation.Length, PrintNothing);
        var composed = value.Select(
            static node => (node + 1) * 2,
            static annotation => (annotation + "!").Length,
            PrintNothing);

        Assert.Equal(value, identity);
        Assert.Equal(sequential, composed);
    }

    [Fact]
    public void AnnotatedPrettyPrintUsesItsFixedTypedComposition()
    {
        var value = new Annotated<int, string>(
            7,
            "entry",
            static (node, annotation, output, _) =>
            {
                output.Write(node);
                output.Write(":");
                output.Write(annotation);
            });
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new IndentedTextWriter(text);

        value.PrettyPrint(writer, PrettyPrintOption.Default);

        Assert.Equal("7:entry", text.ToString());
    }

    [Fact]
    public void AnnotatedEqualityIgnoresItsFixedPrinter()
    {
        var left = new Annotated<int, string>(
            7,
            "entry",
            static (node, annotation, output, _) => output.Write($"{node}:{annotation}"));
        var right = new Annotated<int, string>(
            7,
            "entry",
            static (node, annotation, output, _) => output.Write($"{annotation}:{node}"));

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.Equal("7:entry", left.PrettyPrint());
        Assert.Equal("entry:7", right.PrettyPrint());
    }

    [Fact]
    public void CompletedStagesShareSourceAndGraphFactsByIdentity()
    {
        var method = GetMethod(nameof(Choose));
        var model = CompilerTestPipeline.Labelled(method);
        LinearCode<CilInstructionInfo> raw = model.RawCode;
        LinearCode<Annotated<CilInstructionInfo, PreStack>> pre = model.PreAnnotatedCode;

        Assert.Same(raw.Environment, pre.Environment);
        Assert.Same(method, model.Environment.Method);
        foreach (var annotated in pre.Instructions)
        {
            var original = raw[annotated.Node.Index];
            Assert.Equal(original, annotated.Node);
            Assert.Same(original.Instruction, annotated.Node.Instruction);
        }

        foreach (var block in model.Blocks.Blocks)
            foreach (var annotated in block.Instructions)
                Assert.Same(
                    Assert.Single(pre.Instructions, item => item.Node.Index == annotated.Node.Index).Annotation,
                    annotated.Annotation);
    }

    [Fact]
    public void RealAnnotatedRowsAndGraphStagesImplementIPrintable()
    {
        var model = ParseModel();
        var facts = CompilerTestPipeline.ControlFacts(GetMethod(nameof(Choose)));
        IPrintable row = model.PreAnnotatedCode.Instructions[0];
        IPrintable blockStage = model.Blocks;
        IPrintable shaderGraphStage = CompilerTestPipeline.ShaderControlFlow(GetMethod(nameof(Choose)));
        IPrintable factRow = facts.Graph[facts.Graph.EntryLabel];
        IPrintable factsStage = facts;

        Assert.Contains(" pre=", row.PrettyPrint());
        Assert.Contains("labelled-cil-block-list", blockStage.PrettyPrint());
        Assert.Contains("shader-stack-cfg", shaderGraphStage.PrettyPrint());
        var printedFact = factRow.PrettyPrint();
        Assert.Contains(" facts={rpo=0 idom=none", printedFact);
        Assert.Contains(" incoming=[", printedFact);
        Assert.Contains(" loop-header=", printedFact);
        Assert.Contains("control:", printedFact);
        Assert.DoesNotContain(nameof(CilValueBasicBlock), printedFact);
        Assert.DoesNotContain(nameof(BlockControlFacts), printedFact);
        Assert.Contains("control-facts-cfg", factsStage.PrettyPrint());
    }

    [Fact]
    public void GenericGraphPrettyPrintUsesStableInvariantIdsAndOrderedTargets()
    {
        var entry = DualDrill.CLSL.Language.Symbol.Label.Create("entry");
        var whenTrue = DualDrill.CLSL.Language.Symbol.Label.Create("true");
        var whenFalse = DualDrill.CLSL.Language.Symbol.Label.Create("false");
        var join = DualDrill.CLSL.Language.Symbol.Label.Create("join");
        var graph = new ControlFlowGraph<string>(
            entry,
            new Dictionary<DualDrill.CLSL.Language.Symbol.Label,
                ControlFlowGraph<string>.NodeDefinition>
            {
                [entry] = new(new ConditionalSuccessor(whenTrue, whenFalse), "entry-data"),
                [whenTrue] = new(new UnconditionalSuccessor(join), "true-data"),
                [whenFalse] = new(new UnconditionalSuccessor(join), "false-data"),
                [join] = new(new TerminateSuccessor(), "join-data")
            });

        Assert.Equal(
        [
            "cfg generic (payload rendering: ToString)",
            "entry=^0(entry)",
            "^0(entry) predecessors=[] successor=br_if -> t: ^2(true) f: ^1(false) data=entry-data",
            "^1(false) predecessors=[^0(entry)] successor=br -> ^3(join) data=false-data",
            "^2(true) predecessors=[^0(entry)] successor=br -> ^3(join) data=true-data",
            "^3(join) predecessors=[^1(false), ^2(true)] successor=return data=join-data"
        ], Lines(graph.PrettyPrint()));
    }

    [Fact]
    public void PrettyPrintingAndLoweringDoNotChangeCompletedStageSnapshots()
    {
        var method = GetMethod(nameof(Choose));
        var rawModule = CompilerTestPipeline.ParseRaw(method);
        var model = CompilerTestPipeline.Labelled(rawModule, method);
        var rawSnapshot = model.RawCode.Instructions.ToArray();
        var preSnapshot = model.PreAnnotatedCode.Instructions
                               .Select(item => (item.Node, item.Annotation))
                               .ToArray();
        var blockSnapshot = model.Blocks.Blocks.ToArray();

        _ = model.RawCode.PrettyPrint();
        _ = model.PreAnnotatedCode.PrettyPrint();
        _ = model.Blocks.PrettyPrint();
        _ = ShaderStackToValuePass.Run(
            ShaderStackControlFlowPass.Run(
                CilToShaderStackPass.Run(
                    CilBlockPartitionPass.Run(
                        CilPreStackPass.Run(rawModule)))));

        Assert.Equal(rawSnapshot, model.RawCode.Instructions);
        Assert.Equal(preSnapshot, model.PreAnnotatedCode.Instructions.Select(item => (item.Node, item.Annotation)));
        Assert.Equal(blockSnapshot, model.Blocks.Blocks);
    }

    [Fact]
    public void ValueControlFlowAnalysisBelongsToTheFlatValueGraph()
    {
        var value = CompilerTestPipeline.ValueControlFlow(GetMethod(nameof(Choose)));
        var analysis = value.Graph.ControlFlowAnalysis();

        Assert.Same(value.Graph, analysis.ControlFlowGraph);
    }

    [Fact]
    public void ControlFactsStagePreservesTheFlatValueGraphByIdentity()
    {
        var stages = CompilerTestPipeline.CompileStages(GetMethod(nameof(Choose)));
        var value = Assert.Single(stages.PromotedValueControlFlow.FunctionDefinitions.Values);
        var facts = Assert.Single(stages.ControlFacts.FunctionDefinitions.Values);

        Assert.Same(value, facts.Source);
        Assert.Same(value.Declaration, facts.Declaration);
        Assert.Same(value.DeclarationContext, facts.DeclarationContext);
        Assert.Same(value.Graph.EntryLabel, facts.Graph.EntryLabel);
        Assert.Equal(value.Graph.Labels(), facts.Graph.Labels());
        foreach (var label in value.Graph.Labels())
        {
            Assert.Same(value.Graph[label], facts.Graph[label].Node);
            Assert.Same(value.Graph.Successor(label), facts.Graph.Successor(label));
        }
    }

    [Fact]
    public void CompleteAggregateRejectsGraphFromAnotherDecodedSource()
    {
        var method = GetMethod(nameof(Choose));
        var first = CompilerTestPipeline.Labelled(method);
        var second = CompilerTestPipeline.Labelled(method);

        var exception = Assert.Throws<ArgumentException>(() => new LabelledCilFunctionBody(
            first.Pre,
            second.Blocks));

        Assert.Contains("do not belong to the stored Pre-annotated source", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsBlockWithForeignInstructionIdentity()
    {
        var model = ParseModel();
        var original = model.Blocks.Blocks[0];
        var instructions = original.Instructions.SetItem(
            0,
            new Annotated<CilInstructionInfo, PreStack>(
                original.Instructions[0].Node,
                new PreStack(original.Instructions[0].Annotation.Types),
                CilStagePrettyPrinter.PrintAnnotatedInstruction));
        var replacement = new CilInstructionBlock(
            original.Label,
            instructions,
            original.Terminator);
        var malformed = new BlockList<CilInstructionBlock>(
            model.Blocks.EntryLabel,
            [replacement, .. model.Blocks.Blocks[1..]],
            static block => block.Terminator.ToSuccessor(),
            CilStagePrettyPrinter.PrintBlockList);

        var exception = Assert.Throws<ArgumentException>(() => new LabelledCilFunctionBody(model.Pre, malformed));
        Assert.Contains("do not belong", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsMissingReachableInstructions()
    {
        var model = CompilerTestPipeline.Labelled(BuildLinearFixture());
        var blocks = model.Blocks.Blocks;
        var original = Assert.Single(blocks);
        var truncated = new CilInstructionBlock(
            original.Label,
            original.Instructions.RemoveAt(1),
            original.Terminator);
        var malformed = new BlockList<CilInstructionBlock>(
            model.Blocks.EntryLabel,
            [truncated],
            static block => block.Terminator.ToSuccessor(),
            CilStagePrettyPrinter.PrintBlockList);

        var exception = Assert.Throws<ArgumentException>(() => new LabelledCilFunctionBody(model.Pre, malformed));
        Assert.Contains("do not partition the complete reachable", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsDuplicateReachableInstructions()
    {
        var model = ParseModel();
        var blocks = model.Blocks.Blocks;
        var original = blocks[0];
        var duplicate = new CilInstructionBlock(
            original.Label,
            original.Instructions.Insert(0, original.Instructions[0]),
            original.Terminator);
        var malformed = new BlockList<CilInstructionBlock>(
            model.Blocks.EntryLabel,
            blocks.SetItem(0, duplicate),
            static block => block.Terminator.ToSuccessor(),
            CilStagePrettyPrinter.PrintBlockList);

        var exception = Assert.Throws<ArgumentException>(() => new LabelledCilFunctionBody(model.Pre, malformed));
        Assert.Contains("do not belong to the stored Pre-annotated source", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsDisconnectedBlocks()
    {
        var model = ParseModel();
        var blocks = model.Blocks.Blocks;
        var disconnected = new CilInstructionBlock(
            DualDrill.CLSL.Language.Symbol.Label.Create("disconnected"),
            blocks[^1].Instructions,
            blocks[^1].Terminator);
        var malformed = new BlockList<CilInstructionBlock>(
            model.Blocks.EntryLabel,
            [.. blocks, disconnected],
            static block => block.Terminator.ToSuccessor(),
            CilStagePrettyPrinter.PrintBlockList);

        var exception = Assert.Throws<ArgumentException>(() => new LabelledCilFunctionBody(model.Pre, malformed));
        Assert.Contains("disconnected from the entry", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsAnotherEntryEvenWhenEveryBlockRemainsReachable()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("AnnotatedStageEntryFixture"),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Fixture").DefineType(
            "EntryFixture", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod(
            "IncrementUntilThree", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
        method.DefineParameter(1, ParameterAttributes.None, "value");
        var il = method.GetILGenerator();
        var body = il.DefineLabel();
        var test = il.DefineLabel();
        il.MarkLabel(body);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Starg_S, (byte)0);
        il.Emit(OpCodes.Br_S, test);
        il.MarkLabel(test);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_3);
        il.Emit(OpCodes.Blt_S, body);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ret);
        var fixture = (type.CreateType() ?? throw new InvalidOperationException("Missing fixture type."))
            .GetMethod("IncrementUntilThree") ?? throw new InvalidOperationException("Missing fixture method.");
        var model = CompilerTestPipeline.Labelled(fixture);
        var graph = ControlFlowGraph.Create(model.Blocks, static block => block.Terminator.ToSuccessor());
        var alternateEntry = Assert.Single(
            graph.Labels(), label => graph.Successor(label) is ConditionalSuccessor);
        var malformed = new BlockList<CilInstructionBlock>(
            alternateEntry,
            model.Blocks.Blocks,
            static block => block.Terminator.ToSuccessor(),
            CilStagePrettyPrinter.PrintBlockList);

        Assert.NotSame(graph.EntryLabel, alternateEntry);
        Assert.Equal(graph.Count, malformed.Blocks.Length);
        var exception = Assert.Throws<ArgumentException>(() => new LabelledCilFunctionBody(model.Pre, malformed));
        Assert.Contains("entry must begin at original instruction index 0", exception.Message);
    }

    private static LabelledCilFunctionBody ParseModel()
        => CompilerTestPipeline.Labelled(GetMethod(nameof(Choose)));

    private static void PrintNothing<TNode, TAnnotation>(
        TNode node,
        TAnnotation annotation,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
    }

    private static MethodInfo GetMethod(string name) =>
        typeof(AnnotatedStageValueTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static MethodInfo BuildLinearFixture()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("AnnotatedStageLinearFixture"),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("AnnotatedStageLinearFixture").DefineType(
            "AnnotatedStageLinearFixture",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod(
            "AddThree",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(int),
            [typeof(int)]);
        method.DefineParameter(1, ParameterAttributes.None, "value");
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Ret);
        return (type.CreateType() ?? throw new InvalidOperationException("Missing fixture type."))
            .GetMethod("AddThree") ?? throw new InvalidOperationException("Missing fixture method.");
    }

    private static int Choose(bool choose, int left, int right) => choose ? left : right;

    private static string[] Lines(string value) =>
        value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
