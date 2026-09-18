using System.CodeDom.Compiler;
using System.Globalization;
using System.Reflection;
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
        var value = new Annotated<object, object>(node, annotation);
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
            });
        var nodeOnly = value.SelectNode(original =>
        {
            Assert.Same(node, original);
            return "node-only";
        });
        var annotationOnly = value.SelectAnnotation(original =>
        {
            Assert.Same(annotation, original);
            return "annotation-only";
        });

        Assert.Equal(1, nodeCalls);
        Assert.Equal(1, annotationCalls);
        Assert.Equal(new Annotated<string, string>("node", "annotation"), selected);
        Assert.Same(annotation, nodeOnly.Annotation);
        Assert.Same(node, annotationOnly.Node);
    }

    [Fact]
    public void AnnotatedSelectSatisfiesIdentityAndComposition()
    {
        var value = new Annotated<int, string>(7, "entry");

        var identity = value.Select(static node => node, static annotation => annotation);
        var sequential = value.Select(static node => node + 1, static annotation => annotation + "!")
                              .Select(static node => node * 2, static annotation => annotation.Length);
        var composed = value.Select(
            static node => (node + 1) * 2,
            static annotation => (annotation + "!").Length);

        Assert.Equal(value, identity);
        Assert.Equal(sequential, composed);
    }

    [Fact]
    public void AnnotatedPrettyPrintUsesExplicitPureComposition()
    {
        var value = new Annotated<int, string>(7, "entry");
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new IndentedTextWriter(text);

        value.PrettyPrint(
            writer,
            PrettyPrintOption.Default,
            static (node, output, _) => output.Write(node),
            static (annotation, output, _) =>
            {
                output.Write(":");
                output.Write(annotation);
            });

        Assert.Equal("7:entry", text.ToString());
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
        _ = graph.PrettyPrint();
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
                equivalent.ControlFlowAnalysis())));

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

    private static MethodInfo GetMethod(string name) =>
        typeof(AnnotatedStageValueTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static int Choose(bool choose, int left, int right) => choose ? left : right;
}
