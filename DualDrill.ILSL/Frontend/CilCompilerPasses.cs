using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
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

public static class CilBlockPartitionPass
{
    public static ShaderModuleDeclaration<LabelledCilFunctionBody> Run(
        ShaderModuleDeclaration<PreCilFunctionBody> module) =>
        module.MapBody(static (_, _, body) =>
            new LabelledCilFunctionBody(
                body,
                CilBlockPartitioner.Partition(body.Raw.Code, body.Code)));
}

public static class CilToShaderStackPass
{
    public static ShaderModuleDeclaration<ShaderStackFunctionBody> Run(
        ShaderModuleDeclaration<LabelledCilFunctionBody> module) =>
        module.MapBody(static (_, _, body) => Lower(body));

    private static ShaderStackFunctionBody Lower(LabelledCilFunctionBody source)
    {
        var blocks = source.Blocks.Blocks.Select(cilBlock =>
        {
            var inputStack = cilBlock.EntryStack.Types.Reverse()
                                     .Select(type => type.ShaderType)
                                     .ToImmutableArray();
            var visitor = new CilToShaderStackVisitor(
                source.Environment,
                source.Declaration,
                cilBlock.Terminator,
                inputStack);
            foreach (var annotatedInstruction in cilBlock.Instructions)
            {
                ValidateStack(
                    source.Environment,
                    annotatedInstruction.Node,
                    annotatedInstruction.Annotation,
                    visitor.Stack);
                visitor.Lower(annotatedInstruction.Node, source.Raw.Symbols);
            }

            var terminator = visitor.Terminator;
            if (terminator is null)
                terminator = visitor.SyntheticTerminator(cilBlock.Terminator, cilBlock.Instructions[^1].Node);
            else if (cilBlock.Terminator is CilControlFlow.FallThrough or CilControlFlow.EndOfCode)
                throw new ValidationException(
                    $"Synthetic CIL control at IL_{cilBlock.ByteOffset:X4} produced a native terminator.",
                    source.Environment.Method);

            foreach (var targetLabel in cilBlock.Terminator.ToSuccessor().AllTargets())
            {
                var target = source.Blocks.Blocks.Single(block => ReferenceEquals(block.Label, targetLabel))
                                   .Instructions[0];
                ValidateStack(source.Environment, target.Node, target.Annotation, visitor.Stack);
            }

            return new ShaderStackBasicBlock(
                cilBlock.Label,
                inputStack,
                Seq.Create([.. visitor.Instructions], terminator));
        }).ToImmutableArray();

        return new ShaderStackFunctionBody(
            source,
            new BlockList<ShaderStackBasicBlock>(
                source.Blocks.EntryLabel,
                blocks,
                static block => block.Successor,
                CilStagePrettyPrinter.PrintShaderStackBlockList));
    }

    internal static void ValidateStack(
        CilMethodEnvironment environment,
        CilInstructionInfo instruction,
        PreStack pre,
        ImmutableArray<IShaderType> actual)
    {
        var expected = pre.Types.Reverse().Select(type => type.ShaderType).ToImmutableArray();
        if (!expected.SequenceEqual(actual))
            throw new ValidationException(
                $"Value stack does not match analyzed Pre stack at IL_{instruction.ByteOffset:X4}: " +
                $"expected [{string.Join(", ", expected.Select(type => type.Name))}], " +
                $"got [{string.Join(", ", actual.Select(type => type.Name))}].",
                environment.Method);
    }
}

public static class ShaderStackControlFlowPass
{
    public static ShaderModuleDeclaration<ShaderStackControlFlowBody> Run(
        ShaderModuleDeclaration<ShaderStackFunctionBody> module) =>
        module.MapBody(static (_, _, body) =>
            new ShaderStackControlFlowBody(
                body,
                ControlFlowGraph.Create(
                    body.Blocks,
                    static block => block.Successor,
                    CilStagePrettyPrinter.PrintShaderStackGraph)));
}

public static class ShaderStackToValuePass
{
    public static ShaderModuleDeclaration<CilValueControlFlowBody> Run(
        ShaderModuleDeclaration<ShaderStackControlFlowBody> module) =>
        module.MapBody(static (_, _, body) => Lift(body));

    private static CilValueControlFlowBody Lift(ShaderStackControlFlowBody source)
    {
        var graph = source.Graph;
        Dictionary<Label, ControlFlowGraph<CilValueBasicBlock>.NodeDefinition> definitions = [];
        foreach (var label in graph.Labels())
        {
            var sourceBlock = graph[label];
            var parameters = sourceBlock.EntryStack
                                        .Select(type => (IShaderValue)ShaderValue.Intermediate(type))
                                        .ToImmutableArray();
            var stack = parameters.ToList();
            var instructions = new List<Instruction<IShaderValue, IShaderValue>>();
            foreach (var annotated in sourceBlock.Body.Elements)
            {
                ValidateTransition(stack, annotated.Annotation.Pre);
                switch (annotated.Node)
                {
                    case ShaderStackInstruction.Operation operation:
                        var snapshot = stack.ToImmutableArray();
                        var operands = operation.Instruction.Operands
                                                .Select(operand => Resolve(operand, snapshot))
                                                .ToImmutableArray();
                        var result = operation.Instruction.Result is { } resultType
                            ? ShaderValue.Intermediate(resultType)
                            : null;
                        instructions.Add(Instruction<IShaderValue, IShaderValue>.Create(
                            operation.Instruction.Operation,
                            result,
                            operands,
                            annotated.Annotation.Provenance));
                        stack.RemoveRange(stack.Count - operation.PopCount, operation.PopCount);
                        if (result is not null && result.Type is not UnitType)
                            stack.Add(result);
                        break;
                    case ShaderStackInstruction.PushAlias alias:
                        stack.Add(alias.Value.Value);
                        break;
                    case ShaderStackInstruction.Drop:
                        stack.RemoveAt(stack.Count - 1);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Unknown shader-stack instruction {annotated.Node.GetType().FullName}.");
                }
                ValidateTransition(stack, annotated.Annotation.Post);
            }

            var terminator = LiftTerminator(sourceBlock.Body.Last, stack);
            var block = new CilValueBasicBlock(
                label,
                parameters,
                Seq.Create(instructions, terminator));
            definitions.Add(label, new(block.Successor, block));
        }

        return new CilValueControlFlowBody(
            source,
            new ControlFlowGraph<CilValueBasicBlock>(
                graph.EntryLabel,
                definitions));
    }

    private static ITerminator<RegionJump<IShaderValue>, IShaderValue> LiftTerminator(
        Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition> annotated,
        List<IShaderValue> stack)
    {
        ValidateTransition(stack, annotated.Annotation.Pre);
        var result = annotated.Node.Evaluate(new LiftTerminatorSemantic(stack));
        ValidateTransition(stack, annotated.Annotation.Post);
        return result;
    }

    private static IShaderValue Resolve(
        ShaderStackOperand operand,
        IReadOnlyList<IShaderValue> stack) =>
        operand switch
        {
            ShaderStackOperand.Immediate immediate => immediate.Value,
            ShaderStackOperand.Depth depth when depth.Index < stack.Count =>
                stack[stack.Count - 1 - depth.Index],
            ShaderStackOperand.Depth depth =>
                throw new ArgumentException($"Shader-stack depth {depth.Index} exceeds value stack size {stack.Count}."),
            _ => throw new NotSupportedException($"Unknown shader-stack operand {operand.GetType().FullName}.")
        };

    private static void ValidateTransition(
        IReadOnlyList<IShaderValue> values,
        ImmutableArray<IShaderType> expected)
    {
        if (!values.Select(value => value.Type).SequenceEqual(expected))
            throw new ArgumentException("Mechanical shader-stack lifting disagrees with the typed transition.");
    }

    private sealed class LiftTerminatorSemantic(List<IShaderValue> stack)
        : ITerminatorSemantic<Label, ShaderStackOperand, ITerminator<RegionJump<IShaderValue>, IShaderValue>>
    {
        public ITerminator<RegionJump<IShaderValue>, IShaderValue> ReturnVoid() =>
            Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>();

        public ITerminator<RegionJump<IShaderValue>, IShaderValue> ReturnExpr(ShaderStackOperand expr)
        {
            var value = Resolve(expr, stack);
            stack.Clear();
            return Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(value);
        }

        public ITerminator<RegionJump<IShaderValue>, IShaderValue> Br(Label target) =>
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                new RegionJump<IShaderValue>(target, [.. stack]));

        public ITerminator<RegionJump<IShaderValue>, IShaderValue> BrIf(
            ShaderStackOperand condition,
            Label trueTarget,
            Label falseTarget)
        {
            var value = Resolve(condition, stack);
            stack.RemoveAt(stack.Count - 1);
            var arguments = stack.ToImmutableArray();
            return Terminator.B.BrIf(
                value,
                new RegionJump<IShaderValue>(trueTarget, arguments),
                new RegionJump<IShaderValue>(falseTarget, arguments));
        }
    }
}

public static class CilLocalPromotionPass
{
    public static ShaderModuleDeclaration<CilValueControlFlowBody> Run(
        ShaderModuleDeclaration<CilValueControlFlowBody> module) =>
        module.MapBody(static (_, _, body) =>
        {
            var raw = body.Source.Source.Source.Raw;
            var methodBody = raw.Code.Environment.Body
                             ?? throw new InvalidOperationException(
                                 $"Cannot promote locals for {raw.Code.Environment.Method}: " +
                                 "MethodBody metadata is unavailable.");
            return new CilValueControlFlowBody(
                body.Source,
                PromoteLocalsPass.Run(
                    body.Graph,
                    raw.DeclarationContext.LocalVariables,
                    methodBody.InitLocals));
        });
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
                CilLocalPromotionPass.Run(
                    ShaderStackToValuePass.Run(
                        ShaderStackControlFlowPass.Run(
                            CilToShaderStackPass.Run(
                                CilBlockPartitionPass.Run(
                                    CilPreStackPass.Run(module))))))));
}
