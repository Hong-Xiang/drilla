using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend;

public static class CilPreStackPass
{
    public static ShaderModuleDeclaration<PreCilFunctionBody> Run(
        ShaderModuleDeclaration<RawCilFunctionBody> module) =>
        module.MapBody(static (_, declaration, body) =>
        {
            if (body.Code.Environment.Body?.ExceptionHandlingClauses.Count > 0)
                throw new NotSupportedException(
                    $"Exception handling is not supported for method {body.Code.Environment.Method}.");
            return new PreCilFunctionBody(
                body,
                CilPreStackAnalyzer.Analyze(body.Code, declaration, body.Symbols));
        });
}

public static class CilControlFlowPass
{
    public static ShaderModuleDeclaration<MethodBodyAnalysisModel> Run(
        ShaderModuleDeclaration<PreCilFunctionBody> module) =>
        module.MapBody(static (_, _, body) =>
            new MethodBodyAnalysisModel(
                body,
                CilControlFlowGraphBuilder.Build(body.Raw.Code, body.Code)));
}

public static class CilStackToValuePass
{
    public static ShaderModuleDeclaration<CilValueControlFlowBody> Run(
        ShaderModuleDeclaration<MethodBodyAnalysisModel> module) =>
        module.MapBody(static (_, _, body) => Lift(body));

    private static CilValueControlFlowBody Lift(MethodBodyAnalysisModel source)
    {
        var graph = source.ControlFlow;
        Dictionary<Label, ControlFlowGraph<CilValueBasicBlock>.NodeDefinition> definitions = [];
        foreach (var label in graph.Labels())
        {
            var cilBlock = graph[label];
            var inputStack = CreateInputStack(cilBlock.EntryStack.Types);
            var visitor = new RuntimeReflectionInstructionParserVisitor3(
                source.Environment,
                source.Declaration,
                cilBlock.Terminator,
                inputStack);
            foreach (var annotatedInstruction in cilBlock.Instructions)
            {
                var cilInstruction = annotatedInstruction.Node;
                ValidateStack(
                    source.Environment,
                    cilInstruction,
                    annotatedInstruction.Annotation,
                    visitor.Stack);
                cilInstruction.Evaluate(visitor, source.Environment.IsStatic, source.Raw.Symbols);
            }

            var arguments = visitor.GetStackOutput();
            var terminator = visitor.Terminator;
            if (terminator is null)
                terminator = cilBlock.Terminator switch
                {
                    CilControlFlow.FallThrough { Target: var target } =>
                        Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                            new RegionJump<IShaderValue>(target, arguments)),
                    CilControlFlow.EndOfCode =>
                        throw new NotSupportedException(
                            $"Method {source.Environment.Method} reaches the end of CIL without an explicit return."),
                    _ => throw new ValidationException(
                        $"Native CIL control at IL_{cilBlock.ByteOffset:X4} did not produce a terminator.",
                        source.Environment.Method)
                };
            else if (cilBlock.Terminator is CilControlFlow.FallThrough or CilControlFlow.EndOfCode)
                throw new ValidationException(
                    $"Synthetic CIL control at IL_{cilBlock.ByteOffset:X4} produced a native terminator.",
                    source.Environment.Method);

            foreach (var targetLabel in cilBlock.Terminator.ToSuccessor().AllTargets())
            {
                var target = graph[targetLabel].Instructions[0];
                ValidateStack(source.Environment, target.Node, target.Annotation, visitor.Stack);
            }

            var block = new CilValueBasicBlock(
                label,
                [.. inputStack.Reverse()],
                Seq.Create([.. visitor.Instructions], terminator));
            definitions.Add(label, new ControlFlowGraph<CilValueBasicBlock>.NodeDefinition(
                block.Successor,
                block));
        }

        return new CilValueControlFlowBody(
            source,
            new ControlFlowGraph<CilValueBasicBlock>(
                graph.EntryLabel,
                definitions));
    }

    private static ImmutableStack<IShaderValue> CreateInputStack(ImmutableStack<CilStackType> types)
    {
        ImmutableStack<IShaderValue> result = [];
        foreach (var type in types.Reverse())
            result = result.Push(ShaderValue.Intermediate(type.ShaderType));
        return result;
    }

    private static void ValidateStack(
        CilMethodEnvironment environment,
        CilInstructionInfo instruction,
        PreStack pre,
        ImmutableStack<IShaderValue> actual)
    {
        var expected = pre.Types;
        var actualTypes = actual.Select(value => CilStackType.FromShaderType(value.Type));
        if (!expected.SequenceEqual(actualTypes))
            throw new ValidationException(
                $"Value stack does not match analyzed Pre stack at IL_{instruction.ByteOffset:X4}: " +
                $"expected [{string.Join(", ", expected)}], got [{string.Join(", ", actualTypes)}].",
                environment.Method);
    }
}

public static class CilBlockControlFactsPass
{
    public static ShaderModuleDeclaration<CilValueControlFactsBody> Run(
        ShaderModuleDeclaration<CilValueControlFlowBody> module) =>
        module.MapBody(static (_, _, body) =>
            new CilValueControlFactsBody(
                body,
                ControlFlowFacts.Annotate(
                    body.Graph,
                    CilStagePrettyPrinter.CreateValueBlockControlFactsPrinter(
                        body.DeclarationContext))));
}

public static class CilRegionPass
{
    public static ShaderModuleDeclaration<FunctionBody4> Run(
        ShaderModuleDeclaration<CilValueControlFactsBody> module) =>
        module.MapBody(static (_, declaration, body) =>
            new FunctionBody4(
                declaration,
                RegionTree.Create(
                    body.Graph,
                    static (label, block, facts) =>
                        new ShaderRegionBody(
                            label,
                            block.Parameters,
                            block.Body,
                            facts.PostDominance))));
}

public static class CilModuleCompiler
{
    public static ShaderModuleDeclaration<FunctionBody4> Compile(
        ShaderModuleDeclaration<RawCilFunctionBody> module) =>
        CilRegionPass.Run(
            CilBlockControlFactsPass.Run(
                CilStackToValuePass.Run(
                    CilControlFlowPass.Run(
                        CilPreStackPass.Run(module)))));
}
