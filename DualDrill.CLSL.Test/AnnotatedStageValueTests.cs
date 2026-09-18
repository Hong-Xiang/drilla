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
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(GetMethod(nameof(Choose)));
        var model = parser.Context.GetFunctionDefinition(declaration);
        LinearCode<CilInstructionInfo> raw = model.RawCode;
        LinearCode<Annotated<CilInstructionInfo, PreStack>> pre = model.PreAnnotatedCode;
        Annotated<ControlFlowGraph<CilInstructionBlock>, ControlFlowAnalysis> controlFlow = model.ControlFlow;

        Assert.Same(raw.Environment, pre.Environment);
        Assert.Same(controlFlow.Node, controlFlow.Annotation.ControlFlowGraph);
        Assert.Same(declaration, model.Declaration);
        foreach (var annotated in pre.Instructions)
        {
            var original = raw[annotated.Node.Index];
            Assert.Equal(original, annotated.Node);
            Assert.Same(original.Instruction, annotated.Node.Instruction);
        }

        foreach (var block in controlFlow.Node.Labels().Select(label => controlFlow.Node[label]))
            foreach (var annotated in block.Instructions)
                Assert.Same(
                    Assert.Single(pre.Instructions, item => item.Node.Index == annotated.Node.Index).Annotation,
                    annotated.Annotation);
    }

    [Fact]
    public void RealAnnotatedRowsAndGraphStageImplementIPrintable()
    {
        var model = ParseModel();
        IPrintable row = model.PreAnnotatedCode.Instructions[0];
        IPrintable graphStage = model.ControlFlow;

        Assert.Contains(" pre=", row.PrettyPrint());
        var graph = graphStage.PrettyPrint();
        Assert.Contains("reachable-cil-cfg", graph);
        Assert.Contains("control-flow-analysis", graph);
        Assert.Contains("immediate-dominators=", graph);
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
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(GetMethod(nameof(Choose)));
        var model = parser.Context.GetFunctionDefinition(declaration);
        var rawSnapshot = model.RawCode.Instructions.ToArray();
        var preSnapshot = model.PreAnnotatedCode.Instructions
                               .Select(item => (item.Node, item.Annotation))
                               .ToArray();
        var graph = model.ControlFlow.Node;
        var graphSnapshot = graph.Labels()
                                 .Select(label => (
                                     Label: label,
                                     Block: graph[label],
                                     Predecessors: graph.Predecessor(label).ToArray()))
                                 .ToArray();

        _ = model.RawCode.PrettyPrint();
        _ = model.PreAnnotatedCode.PrettyPrint();
        _ = model.ControlFlow.PrettyPrint();
        _ = parser.MethodBodies[declaration];

        Assert.Equal(rawSnapshot, model.RawCode.Instructions);
        Assert.Equal(preSnapshot, model.PreAnnotatedCode.Instructions.Select(item => (item.Node, item.Annotation)));
        Assert.Equal(graphSnapshot.Select(item => item.Label), graph.Labels());
        foreach (var item in graphSnapshot)
        {
            Assert.Same(item.Block, graph[item.Label]);
            Assert.Equal(item.Predecessors, graph.Predecessor(item.Label));
        }
    }

    [Fact]
    public void CompleteAggregateRejectsAnalysisFromAnotherGraph()
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(GetMethod(nameof(Choose)));
        var model = parser.Context.GetFunctionDefinition(declaration);
        var graph = model.ControlFlow.Node;
        var equivalent = new ControlFlowGraph<CilInstructionBlock>(
            graph.EntryLabel,
            graph.Labels().ToDictionary(
                label => label,
                label => new ControlFlowGraph<CilInstructionBlock>.NodeDefinition(
                    graph.Successor(label),
                    graph[label])));

        var exception = Assert.Throws<ArgumentException>(() => new MethodBodyAnalysisModel(
            declaration,
            model.RawCode,
            model.PreAnnotatedCode,
            new Annotated<ControlFlowGraph<CilInstructionBlock>, ControlFlowAnalysis>(
                graph,
                equivalent.ControlFlowAnalysis(),
                PrintNothing)));

        Assert.Contains("belong to the stored graph", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsGraphFromAnotherDecodedSource()
    {
        var firstParser = new RuntimeReflectionParser();
        var firstDeclaration = firstParser.ParseMethod(GetMethod(nameof(Choose)));
        var first = firstParser.Context.GetFunctionDefinition(firstDeclaration);
        var secondParser = new RuntimeReflectionParser();
        var secondDeclaration = secondParser.ParseMethod(GetMethod(nameof(Choose)));
        var second = secondParser.Context.GetFunctionDefinition(secondDeclaration);

        var exception = Assert.Throws<ArgumentException>(() => new MethodBodyAnalysisModel(
            firstDeclaration,
            first.RawCode,
            first.PreAnnotatedCode,
            second.ControlFlow));

        Assert.Contains("does not belong to the stored Pre-annotated source", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsBlocksStoredUnderDifferentLabels()
    {
        var model = ParseModel();
        var graph = model.ControlFlow.Node;
        var conditional = Assert.IsType<ConditionalSuccessor>(graph.Successor(graph.EntryLabel));
        var definitions = Definitions(graph);
        (definitions[conditional.TrueTarget], definitions[conditional.FalseTarget]) = (
            definitions[conditional.FalseTarget],
            definitions[conditional.TrueTarget]);
        var malformed = new ControlFlowGraph<CilInstructionBlock>(graph.EntryLabel, definitions);

        var exception = Assert.Throws<ArgumentException>(() => CompleteWithGraph(model, malformed));

        Assert.Contains("key does not match its block label", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsSuccessorDifferentFromConcreteTerminator()
    {
        var model = ParseModel();
        var graph = model.ControlFlow.Node;
        var conditional = Assert.IsType<ConditionalSuccessor>(graph.Successor(graph.EntryLabel));
        var definitions = Definitions(graph);
        definitions[graph.EntryLabel] = new(
            new ConditionalSuccessor(conditional.FalseTarget, conditional.TrueTarget),
            graph[graph.EntryLabel]);
        var malformed = new ControlFlowGraph<CilInstructionBlock>(graph.EntryLabel, definitions);

        var exception = Assert.Throws<ArgumentException>(() => CompleteWithGraph(model, malformed));

        Assert.Contains("successor does not match its block terminator", exception.Message);
    }

    [Fact]
    public void CompleteAggregateRejectsDisconnectedDefinitions()
    {
        var model = ParseModel();
        var graph = model.ControlFlow.Node;
        var definitions = Definitions(graph);
        definitions.Add(
            DualDrill.CLSL.Language.Symbol.Label.Create("disconnected"),
            new ControlFlowGraph<CilInstructionBlock>.NodeDefinition(
                new TerminateSuccessor(),
                graph[graph.EntryLabel]));
        var malformed = new ControlFlowGraph<CilInstructionBlock>(graph.EntryLabel, definitions);

        var exception = Assert.Throws<ArgumentException>(() => CompleteWithGraph(model, malformed));

        Assert.Contains("definitions disconnected from its entry", exception.Message);
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
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(fixture);
        var model = parser.Context.GetFunctionDefinition(declaration);
        var graph = model.ControlFlow.Node;
        var alternateEntry = Assert.Single(
            graph.Labels(), label => graph.Successor(label) is ConditionalSuccessor);
        var malformed = new ControlFlowGraph<CilInstructionBlock>(alternateEntry, Definitions(graph));

        Assert.NotSame(graph.EntryLabel, alternateEntry);
        Assert.Equal(graph.Count, malformed.Labels().Count());
        var exception = Assert.Throws<ArgumentException>(() => CompleteWithGraph(model, malformed));
        Assert.Contains("entry must begin at original instruction index 0", exception.Message);
    }

    private static MethodBodyAnalysisModel ParseModel()
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(GetMethod(nameof(Choose)));
        return parser.Context.GetFunctionDefinition(declaration);
    }

    private static Dictionary<DualDrill.CLSL.Language.Symbol.Label,
        ControlFlowGraph<CilInstructionBlock>.NodeDefinition> Definitions(
        ControlFlowGraph<CilInstructionBlock> graph) =>
        graph.Labels().ToDictionary(
            label => label,
            label => new ControlFlowGraph<CilInstructionBlock>.NodeDefinition(
                graph.Successor(label),
                graph[label]));

    private static MethodBodyAnalysisModel CompleteWithGraph(
        MethodBodyAnalysisModel source,
        ControlFlowGraph<CilInstructionBlock> graph) =>
        new(
            source.Declaration,
            source.RawCode,
            source.PreAnnotatedCode,
            new Annotated<ControlFlowGraph<CilInstructionBlock>, ControlFlowAnalysis>(
                graph,
                graph.ControlFlowAnalysis(),
                PrintNothing));

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

    private static int Choose(bool choose, int left, int right) => choose ? left : right;

    private static string[] Lines(string value) =>
        value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
