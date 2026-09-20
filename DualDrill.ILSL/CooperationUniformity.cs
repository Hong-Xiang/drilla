using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL;

public enum CooperationUniformValueKind
{
    BlockParameter,
    OperationResult,
    HelperCallResult,
    NumericBuiltinResult
}

public sealed record CooperationUniformValueFact(
    FunctionDeclaration Function,
    Label Label,
    int? InstructionOrdinal,
    IShaderValue Value,
    CooperationUniformValueKind Kind,
    ImmutableArray<IShaderValue> Operands,
    FunctionDeclaration? Callee,
    object? Payload);

public sealed record CooperationUniformBindingFact(
    FunctionDeclaration Function,
    Label Source,
    int Arm,
    Label Target,
    int ParameterPosition,
    IShaderValue Argument,
    IShaderValue Parameter);

public sealed record CooperationUniformConditionalFact(
    FunctionDeclaration Function,
    Label Label,
    IShaderValue Condition,
    Label TrueTarget,
    Label FalseTarget);

public sealed record CooperationUniformReturnFact(
    FunctionDeclaration Function,
    Label Label,
    IShaderValue? Value);

public sealed record CooperationFunctionUniformityFacts(
    FunctionDeclaration Function,
    ImmutableArray<Label> OriginalBlocks,
    ImmutableArray<CooperationUniformValueFact> UniformValues,
    ImmutableArray<CooperationUniformBindingFact> UniformBindings,
    ImmutableArray<CooperationUniformConditionalFact> UniformConditionals,
    ImmutableArray<CooperationUniformReturnFact> UniformReturns,
    bool ReturnsUniform);

internal sealed record CooperationUniformityResult(
    ImmutableDictionary<FunctionDeclaration, CooperationFunctionUniformityFacts> Functions,
    ImmutableArray<(FunctionDeclaration Function, Label Label, int Ordinal, FunctionDeclaration Builtin)>
        UniformBuiltinCalls);

internal static class CooperationUniformity
{
    private static readonly FrozenSet<FunctionDeclaration> NumericBuiltins =
        ShaderFunction.Instance.Functions.ToFrozenSet();

    private static readonly FrozenSet<FunctionDeclaration> DerivativeBuiltins =
        ShaderFunction.Instance.Functions
            .Where(static function =>
                Enum.TryParse<NumericBuiltinFunctionName>(function.Name, out var name) &&
                name is >= NumericBuiltinFunctionName.dpdx and <= NumericBuiltinFunctionName.fwidthFine)
            .ToFrozenSet();

    internal static CooperationUniformityResult Analyze(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        FunctionEffectAnalysisResult effects,
        ImmutableArray<FunctionDeclaration> closure,
        ImmutableDictionary<FunctionDeclaration, ImmutableArray<CooperationCallSite>> calls,
        FunctionDeclaration entry)
    {
        var required = closure.ToHashSet(ReferenceEqualityComparer.Instance);
        var ordered = ImmutableArray.CreateBuilder<FunctionDeclaration>();
        var visited = new HashSet<FunctionDeclaration>(ReferenceEqualityComparer.Instance);

        void Visit(FunctionDeclaration function)
        {
            if (!visited.Add(function))
                return;
            foreach (var call in calls[function])
                if (required.Contains(call.Callee))
                    Visit(call.Callee);
            ordered.Add(function);
        }

        Visit(entry);
        var completed = new Dictionary<FunctionDeclaration, CooperationFunctionUniformityFacts>(
            ReferenceEqualityComparer.Instance);
        var builtinCalls = ImmutableArray.CreateBuilder<(
            FunctionDeclaration Function,
            Label Label,
            int Ordinal,
            FunctionDeclaration Builtin)>();
        foreach (var function in ordered)
        {
            var analyzed = AnalyzeFunction(
                module.FunctionDefinitions[function],
                effects[function],
                completed,
                entry,
                builtinCalls);
            completed.Add(function, analyzed);
        }

        return new(
            completed.ToImmutableDictionary(ReferenceEqualityComparer.Instance),
            builtinCalls.ToImmutable());
    }

    private static CooperationFunctionUniformityFacts AnalyzeFunction(
        RegionFunctionBody body,
        FunctionEffectSummary effects,
        IReadOnlyDictionary<FunctionDeclaration, CooperationFunctionUniformityFacts> completed,
        FunctionDeclaration entry,
        ImmutableArray<(
            FunctionDeclaration Function,
            Label Label,
            int Ordinal,
            FunctionDeclaration Builtin)>.Builder builtinCalls)
    {
        var regions = new Dictionary<Label, RegionTree<Label, ShaderRegionBody>>(
            ReferenceEqualityComparer.Instance);
        body.Body.Traverse(region =>
        {
            if (region.Definition.Kind is RegionKind.Loop)
                throw ShapeError(entry, body.Declaration, region.Label, "RegionKind.Loop is not admitted");
            regions.Add(region.Label, region);
        });

        var successors = new Dictionary<Label, ImmutableArray<(int Arm, RegionJump<IShaderValue> Jump)>>(
            ReferenceEqualityComparer.Instance);
        var incoming = regions.Keys.ToDictionary<Label, Label, ImmutableArray<(
            Label Source,
            int Arm,
            RegionJump<IShaderValue> Jump)>.Builder>(
            static label => label,
            static _ => ImmutableArray.CreateBuilder<(Label Source, int Arm, RegionJump<IShaderValue> Jump)>(),
            ReferenceEqualityComparer.Instance);
        foreach (var (label, region) in regions)
        {
            var edges = Edges(region.Body.Body.Last, entry, body.Declaration, label);
            successors.Add(label, edges);
            foreach (var (arm, jump) in edges)
            {
                var transfer = body.Control.Resolve(label, arm);
                if (transfer.Kind is not ScopedContinuationKind.Forward ||
                    !ReferenceEquals(transfer.Target, jump.Label))
                    throw ShapeError(
                        entry,
                        body.Declaration,
                        label,
                        "control edge is not the checked Forward transfer");
                incoming[jump.Label].Add((label, arm, jump));
            }
        }

        var order = TopologicalOrder(body.Entry, successors, incoming, regions.Count, entry, body.Declaration);
        var outgoing = new Dictionary<Label, FlowEnvironment>(ReferenceEqualityComparer.Instance);
        var uniformValues = ImmutableArray.CreateBuilder<CooperationUniformValueFact>();
        var uniformBindings = ImmutableArray.CreateBuilder<CooperationUniformBindingFact>();
        var uniformConditionals = ImmutableArray.CreateBuilder<CooperationUniformConditionalFact>();
        var uniformReturns = ImmutableArray.CreateBuilder<CooperationUniformReturnFact>();
        var allReturnsUniform = true;
        var definedValues = regions.Values
            .SelectMany(static region => region.Body.Parameters.Concat(
                region.Body.Body.Elements.Select(static instruction => instruction.Result)
                    .OfType<IShaderValue>()))
            .ToHashSet(ReferenceEqualityComparer.Instance);
        var externalValues = regions.Values
            .SelectMany(static region => region.Body.Body.Elements
                .SelectMany(static instruction => instruction.Operands)
                .Concat(TerminatorValues(region.Body.Body.Last)))
            .Where(value => value is not LiteralValue and not FunctionDeclaration &&
                            !definedValues.Contains(value))
            .ToImmutableHashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
        var directRequirements = effects.RequirementSites
            .Where(site => ReferenceEquals(site.Function, body.Declaration))
            .ToDictionary(site => (site.Label, site.InstructionOrdinal));
        var directUnknowns = effects.UnknownSites
            .Where(site => ReferenceEquals(site.Function, body.Declaration))
            .ToDictionary(site => (site.Label, site.InstructionOrdinal));

        foreach (var label in order)
        {
            var block = regions[label].Body;
            FlowEnvironment environment;
            if (ReferenceEquals(label, body.Entry))
            {
                var initialAvailable = externalValues.ToBuilder();
                initialAvailable.UnionWith(body.Declaration.Parameters.Select(static parameter => parameter.Value));
                environment = new(
                    initialAvailable.ToImmutable(),
                    ImmutableHashSet.Create<IShaderValue>(ReferenceEqualityComparer.Instance));
            }
            else
            {
                var edges = incoming[label].ToImmutable();
                if (edges.IsEmpty)
                    throw ShapeError(entry, body.Declaration, label, "block has no reachable incoming edge");
                var available = edges
                    .Select(edge => outgoing[edge.Source].Available)
                    .Aggregate(static (left, right) => left.Intersect(right));
                var uniform = edges
                    .Select(edge => outgoing[edge.Source].Uniform)
                    .Aggregate(static (left, right) => left.Intersect(right));
                environment = new(available, uniform);

                foreach (var (position, parameter) in block.Parameters.Index())
                {
                    environment = environment.Define(parameter, uniform: edges.All(edge =>
                        IsUniform(edge.Jump.Arguments[position], outgoing[edge.Source])));
                    if (!environment.Uniform.Contains(parameter))
                        continue;
                    foreach (var edge in edges)
                        uniformBindings.Add(new(
                            body.Declaration,
                            edge.Source,
                            edge.Arm,
                            label,
                            position,
                            edge.Jump.Arguments[position],
                            parameter));
                    uniformValues.Add(new(
                        body.Declaration,
                        label,
                        null,
                        parameter,
                        CooperationUniformValueKind.BlockParameter,
                        [.. edges.Select(edge => edge.Jump.Arguments[position])],
                        null,
                        null));
                }
            }

            foreach (var (ordinal, instruction) in block.Body.Elements.Index())
            {
                foreach (var operand in instruction.Operands)
                    RequireAvailable(operand, environment, entry, body.Declaration, label);
                if (instruction.Result is not { } result)
                    continue;

                var classification = Classify(
                    instruction,
                    label,
                    ordinal,
                    environment,
                    completed,
                    directRequirements,
                    directUnknowns);
                environment = environment.Define(result, classification.Uniform);
                if (!classification.Uniform)
                    continue;
                uniformValues.Add(new(
                    body.Declaration,
                    label,
                    ordinal,
                    result,
                    classification.Kind,
                    [.. instruction.Operands],
                    classification.Callee,
                    instruction.Payload));
                if (classification.Kind is CooperationUniformValueKind.NumericBuiltinResult)
                    builtinCalls.Add((body.Declaration, label, ordinal, classification.Callee!));
            }

            switch (block.Body.Last)
            {
                case Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>:
                    uniformReturns.Add(new(body.Declaration, label, null));
                    break;
                case Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned:
                    RequireAvailable(returned.Expr, environment, entry, body.Declaration, label);
                    if (IsUniform(returned.Expr, environment))
                        uniformReturns.Add(new(body.Declaration, label, returned.Expr));
                    else
                        allReturnsUniform = false;
                    break;
                case Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch:
                    foreach (var argument in branch.Target.Arguments)
                        RequireAvailable(argument, environment, entry, body.Declaration, label);
                    break;
                case Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch:
                    RequireAvailable(branch.Condition, environment, entry, body.Declaration, label);
                    if (!IsUniform(branch.Condition, environment))
                        throw ShapeError(
                            entry,
                            body.Declaration,
                            label,
                            "conditional control is varying");
                    foreach (var argument in branch.TrueTarget.Arguments.Concat(branch.FalseTarget.Arguments))
                        RequireAvailable(argument, environment, entry, body.Declaration, label);
                    uniformConditionals.Add(new(
                        body.Declaration,
                        label,
                        branch.Condition,
                        branch.TrueTarget.Label,
                        branch.FalseTarget.Label));
                    break;
            }

            outgoing.Add(label, environment);
        }

        return new(
            body.Declaration,
            order,
            uniformValues.ToImmutable(),
            uniformBindings.ToImmutable(),
            uniformConditionals.ToImmutable(),
            uniformReturns.ToImmutable(),
            allReturnsUniform);
    }

    private static UniformClassification Classify(
        Instruction<IShaderValue, IShaderValue> instruction,
        Label label,
        int ordinal,
        FlowEnvironment environment,
        IReadOnlyDictionary<FunctionDeclaration, CooperationFunctionUniformityFacts> completed,
        IReadOnlyDictionary<(Label Label, int Ordinal), OperationRequirementSite> directRequirements,
        IReadOnlyDictionary<(Label Label, int Ordinal), FunctionEffectUnknownSite> directUnknowns)
    {
        if (instruction.Operation is CallOperation &&
            instruction.OperandCount > 0 &&
            instruction[0] is FunctionDeclaration callee)
        {
            if (completed.TryGetValue(callee, out var helper))
                return new(
                    helper.ReturnsUniform,
                    CooperationUniformValueKind.HelperCallResult,
                    callee);
            if (NumericBuiltins.Contains(callee))
                return new(
                    !DerivativeBuiltins.Contains(callee) &&
                    instruction.Operands.Skip(1).All(operand => IsUniform(operand, environment)),
                    CooperationUniformValueKind.NumericBuiltinResult,
                    callee);
            return UniformClassification.Varying;
        }

        if (instruction.Operation is LoadOperation or StoreOperation or IAddressOfOperation or AccessChainOperation ||
            instruction.Operation is IOperationRequirementProvider ||
            instruction.Result?.Type is IPtrType ||
            directRequirements.ContainsKey((label, ordinal)) ||
            directUnknowns.ContainsKey((label, ordinal)))
            return UniformClassification.Varying;

        return new(
            instruction.Operands.All(operand => IsUniform(operand, environment)),
            CooperationUniformValueKind.OperationResult,
            null);
    }

    private static ImmutableArray<(int Arm, RegionJump<IShaderValue> Jump)> Edges(
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
        FunctionDeclaration entry,
        FunctionDeclaration function,
        Label label) =>
        terminator switch
        {
            Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => [(0, branch.Target)],
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                [(0, branch.TrueTarget), (1, branch.FalseTarget)],
            Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> => [],
            Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> => [],
            Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> =>
                throw ShapeError(entry, function, label, "switch control is not admitted"),
            _ => throw ShapeError(entry, function, label, "unsupported terminator")
        };

    private static IEnumerable<IShaderValue> TerminatorValues(
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
        terminator switch
        {
            Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => branch.Target.Arguments,
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                [branch.Condition, .. branch.TrueTarget.Arguments, .. branch.FalseTarget.Arguments],
            Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> branch =>
                [
                    branch.Selector,
                    .. branch.CaseTargets.SelectMany(static target => target.Arguments),
                    .. branch.DefaultTarget.Arguments
                ],
            Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned => [returned.Expr],
            Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> => [],
            _ => []
        };

    private static ImmutableArray<Label> TopologicalOrder(
        Label entryLabel,
        IReadOnlyDictionary<Label, ImmutableArray<(int Arm, RegionJump<IShaderValue> Jump)>> successors,
        IReadOnlyDictionary<Label, ImmutableArray<(Label Source, int Arm, RegionJump<IShaderValue> Jump)>.Builder>
            incoming,
        int blockCount,
        FunctionDeclaration entry,
        FunctionDeclaration function)
    {
        var indegree = incoming.ToDictionary<KeyValuePair<
            Label,
            ImmutableArray<(Label Source, int Arm, RegionJump<IShaderValue> Jump)>.Builder>, Label, int>(
            static item => item.Key,
            static item => item.Value.Count,
            ReferenceEqualityComparer.Instance);
        var ready = new Queue<Label>();
        ready.Enqueue(entryLabel);
        var result = ImmutableArray.CreateBuilder<Label>();
        while (ready.TryDequeue(out var label))
        {
            result.Add(label);
            foreach (var (_, jump) in successors[label])
            {
                indegree[jump.Label]--;
                if (indegree[jump.Label] == 0)
                    ready.Enqueue(jump.Label);
            }
        }
        if (result.Count != blockCount)
            throw ShapeError(entry, function, entryLabel, "control graph is cyclic");
        return result.ToImmutable();
    }

    private static void RequireAvailable(
        IShaderValue value,
        FlowEnvironment environment,
        FunctionDeclaration entry,
        FunctionDeclaration function,
        Label label)
    {
        if (!IsAlwaysAvailable(value) && !environment.Available.Contains(value))
            throw ShapeError(
                entry,
                function,
                label,
                "value is not definitely available on every incoming path");
    }

    private static bool IsUniform(IShaderValue value, FlowEnvironment environment) =>
        value is LiteralValue || environment.Uniform.Contains(value);

    private static bool IsAlwaysAvailable(IShaderValue value) =>
        value is LiteralValue or FunctionDeclaration;

    private static NotSupportedException ShapeError(
        FunctionDeclaration entry,
        FunctionDeclaration function,
        Label label,
        string reason) =>
        new(
            $"PortableWgsl entry '{entry.Name}' requires entry-uniform quad participation; " +
            $"function '{function.Name}', block '{label.Name}': {reason}.");

    private readonly record struct FlowEnvironment(
        ImmutableHashSet<IShaderValue> Available,
        ImmutableHashSet<IShaderValue> Uniform)
    {
        internal FlowEnvironment Define(IShaderValue value, bool uniform) =>
            new(Available.Add(value), uniform ? Uniform.Add(value) : Uniform);
    }

    private readonly record struct UniformClassification(
        bool Uniform,
        CooperationUniformValueKind Kind,
        FunctionDeclaration? Callee)
    {
        internal static UniformClassification Varying { get; } =
            new(false, CooperationUniformValueKind.OperationResult, null);
    }
}
