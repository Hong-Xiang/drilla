using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Frontend;
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

public abstract record CooperationUniformDependencies
{
    private CooperationUniformDependencies()
    {
    }

    public sealed record Unknown : CooperationUniformDependencies;

    public sealed record Known : CooperationUniformDependencies
    {
        public Known(IEnumerable<int> formalParameterPositions)
        {
            FormalParameterPositions =
            [
                .. formalParameterPositions
                    .Distinct()
                    .Order()
                    .Select(position => position >= 0
                        ? position
                        : throw new ArgumentOutOfRangeException(
                            nameof(formalParameterPositions),
                            position,
                            "Formal parameter positions must be non-negative."))
            ];
        }

        public ImmutableArray<int> FormalParameterPositions { get; }
    }
}

public enum CooperationDependencyValueKind
{
    BlockParameter,
    FunctionParameterLoad,
    OperationResult,
    HelperCallResult,
    NumericBuiltinResult
}

public sealed record CooperationDependencyValueFact(
    FunctionDeclaration Function,
    Label Label,
    int? InstructionOrdinal,
    IShaderValue Value,
    CooperationDependencyValueKind Kind,
    CooperationUniformDependencies Dependencies,
    IOperation? Operation,
    ImmutableArray<IShaderValue> Operands,
    FunctionDeclaration? Callee,
    object? Payload);

public sealed record CooperationDependencyBindingFact(
    FunctionDeclaration Function,
    Label Source,
    int Arm,
    Label Target,
    int ParameterPosition,
    IShaderValue Argument,
    CooperationUniformDependencies ArgumentDependencies,
    IShaderValue Parameter,
    CooperationUniformDependencies ParameterDependencies);

public sealed record CooperationUniformConditionalFact(
    FunctionDeclaration Function,
    Label Label,
    IShaderValue Condition,
    Label TrueTarget,
    Label FalseTarget);

public sealed record CooperationDependencyReturnFact(
    FunctionDeclaration Function,
    Label Label,
    IShaderValue? Value,
    CooperationUniformDependencies Dependencies);

public sealed record CooperationTransferFact(
    FunctionDeclaration Function,
    Label Source,
    int Arm,
    Label Target,
    Label Owner,
    ScopedContinuationKind Kind,
    ImmutableArray<IShaderValue> Arguments,
    ImmutableArray<IShaderValue> TargetParameters);

public sealed record CooperationFunctionUniformityFacts(
    FunctionDeclaration Function,
    ImmutableArray<Label> OriginalBlocks,
    ImmutableArray<VariableDeclaration> OriginalNonFunctionStorage,
    ImmutableArray<int> EligibleFormalParameterPositions,
    ImmutableArray<CooperationDependencyValueFact> DependencyValues,
    ImmutableArray<CooperationDependencyBindingFact> DependencyBindings,
    ImmutableArray<CooperationUniformConditionalFact> UniformConditionals,
    ImmutableArray<CooperationDependencyReturnFact> DependencyReturns,
    ImmutableArray<CooperationTransferFact> OriginalTransfers,
    CooperationUniformDependencies AggregateReturnDependencies);

internal sealed record CooperationUniformityResult(
    ImmutableDictionary<FunctionDeclaration, CooperationFunctionUniformityFacts> Functions,
    ImmutableArray<(FunctionDeclaration Function, Label Label, int Ordinal, FunctionDeclaration Builtin)>
        DependencyBuiltinCalls);

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
        var moduleStorage = module.Declarations.OfType<VariableDeclaration>()
            .ToImmutableHashSet<VariableDeclaration>(ReferenceEqualityComparer.Instance);
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
                moduleStorage,
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
        ImmutableHashSet<VariableDeclaration> moduleStorage,
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
        var dependencyValues = ImmutableArray.CreateBuilder<CooperationDependencyValueFact>();
        var dependencyBindings = ImmutableArray.CreateBuilder<CooperationDependencyBindingFact>();
        var uniformConditionals = ImmutableArray.CreateBuilder<CooperationUniformConditionalFact>();
        var dependencyReturns = ImmutableArray.CreateBuilder<CooperationDependencyReturnFact>();
        var eligibleFormals = EligibleFormalLoads(body);
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
                    ImmutableDictionary.Create<IShaderValue, CooperationUniformDependencies.Known>(
                        ReferenceEqualityComparer.Instance));
            }
            else
            {
                var edges = incoming[label].ToImmutable();
                if (edges.IsEmpty)
                    throw ShapeError(entry, body.Declaration, label, "block has no reachable incoming edge");
                var available = edges
                    .Select(edge => outgoing[edge.Source].Available)
                    .Aggregate(static (left, right) => left.Intersect(right));
                var dependencies = edges
                    .Select(edge => outgoing[edge.Source].Dependencies)
                    .Aggregate(IntersectDependencies);
                environment = new(available, dependencies);

                foreach (var (position, parameter) in block.Parameters.Index())
                {
                    var parameterDependencies = DependencyLattice.Union(
                        edges.Select(edge =>
                            DependenciesOf(edge.Jump.Arguments[position], outgoing[edge.Source])));
                    environment = environment.Define(parameter, parameterDependencies);
                    if (parameterDependencies is not CooperationUniformDependencies.Known knownParameter)
                        continue;
                    foreach (var edge in edges)
                        dependencyBindings.Add(new(
                            body.Declaration,
                            edge.Source,
                            edge.Arm,
                            label,
                            position,
                            edge.Jump.Arguments[position],
                            DependenciesOf(edge.Jump.Arguments[position], outgoing[edge.Source]),
                            parameter,
                            knownParameter));
                    dependencyValues.Add(new(
                        body.Declaration,
                        label,
                        null,
                        parameter,
                        CooperationDependencyValueKind.BlockParameter,
                        knownParameter,
                        null,
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
                    value => DependenciesOf(value, environment),
                    completed,
                    eligibleFormals,
                    directRequirements,
                    directUnknowns);
                environment = environment.Define(result, classification.Dependencies);
                if (classification.Dependencies is not CooperationUniformDependencies.Known known)
                    continue;
                dependencyValues.Add(new(
                    body.Declaration,
                    label,
                    ordinal,
                    result,
                    classification.Kind,
                    known,
                    instruction.Operation,
                    [.. instruction.Operands],
                    classification.Callee,
                    instruction.Payload));
                if (classification.Kind is CooperationDependencyValueKind.NumericBuiltinResult)
                    builtinCalls.Add((body.Declaration, label, ordinal, classification.Callee!));
            }

            switch (block.Body.Last)
            {
                case Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>:
                    dependencyReturns.Add(new(
                        body.Declaration,
                        label,
                        null,
                        DependencyLattice.Empty));
                    break;
                case Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned:
                    RequireAvailable(returned.Expr, environment, entry, body.Declaration, label);
                    dependencyReturns.Add(new(
                        body.Declaration,
                        label,
                        returned.Expr,
                        DependenciesOf(returned.Expr, environment)));
                    break;
                case Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch:
                    foreach (var argument in branch.Target.Arguments)
                        RequireAvailable(argument, environment, entry, body.Declaration, label);
                    break;
                case Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch:
                    RequireAvailable(branch.Condition, environment, entry, body.Declaration, label);
                    if (!DependencyLattice.IsEmpty(DependenciesOf(branch.Condition, environment)))
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
            [.. body.UsedValues()
                .OfType<VariablePointerValue>()
                .Select(static value => value.Declaration)
                .Where(variable =>
                    variable.AddressSpace is not FunctionAddressSpace &&
                    moduleStorage.Contains(variable))
                .Distinct<VariableDeclaration>(ReferenceEqualityComparer.Instance)],
            [.. eligibleFormals.Values.Order()],
            dependencyValues.ToImmutable(),
            dependencyBindings.ToImmutable(),
            uniformConditionals.ToImmutable(),
            dependencyReturns.ToImmutable(),
            CooperationTransferFacts.Capture(body, order),
            DependencyLattice.Union(
                dependencyReturns.Select(static returned => returned.Dependencies)));
    }

    internal static class CooperationTransferFacts
    {
        internal static ImmutableArray<CooperationTransferFact> Capture(
            RegionFunctionBody body,
            ImmutableArray<Label> blockOrder) =>
        [
            .. blockOrder.SelectMany(source =>
                Jumps(body[source].Body.Last).Select((jump, arm) =>
                {
                    var transfer = body.Control.Resolve(source, arm);
                    return new CooperationTransferFact(
                        body.Declaration,
                        source,
                        arm,
                        transfer.Target,
                        transfer.Owner,
                        transfer.Kind,
                        jump.Arguments,
                        body[jump.Label].Parameters);
                }))
        ];

        internal static bool Matches(
            RegionFunctionBody body,
            CooperationTransferFact fact,
            bool allowPointerParameterErasure)
        {
            if (!ReferenceEquals(body.Declaration, fact.Function))
                return false;
            ScopedTransfer<Label> transfer;
            RegionJump<IShaderValue> jump;
            try
            {
                transfer = body.Control.Resolve(fact.Source, fact.Arm);
                jump = Jumps(body[fact.Source].Body.Last)[fact.Arm];
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }

            if (!ReferenceEquals(transfer.Source, fact.Source) ||
                transfer.Arm != fact.Arm ||
                !ReferenceEquals(transfer.Target, fact.Target) ||
                !ReferenceEquals(transfer.Owner, fact.Owner) ||
                transfer.Kind != fact.Kind ||
                !ReferenceEquals(jump.Label, fact.Target))
                return false;

            if (!allowPointerParameterErasure)
                return jump.Arguments.AsEnumerable().SequenceEqual(
                           fact.Arguments,
                           ReferenceEqualityComparer.Instance) &&
                       body[fact.Target].Parameters.AsEnumerable().SequenceEqual(
                           fact.TargetParameters,
                           ReferenceEqualityComparer.Instance);

            var retained = fact.TargetParameters.Zip(fact.Arguments)
                .Where(static pair => pair.First.Type is not IPtrType)
                .ToImmutableArray();
            return body[fact.Target].Parameters.AsEnumerable().SequenceEqual(
                       retained.Select(static pair => pair.First),
                       ReferenceEqualityComparer.Instance) &&
                   jump.Arguments.AsEnumerable().SequenceEqual(
                       retained.Select(static pair => pair.Second),
                       ReferenceEqualityComparer.Instance);
        }

        private static ImmutableArray<RegionJump<IShaderValue>> Jumps(
            ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
            terminator switch
            {
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => [branch.Target],
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    [branch.TrueTarget, branch.FalseTarget],
                Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> => [],
                Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> => [],
                _ => []
            };
    }

    internal static DependencyClassification Classify(
        Instruction<IShaderValue, IShaderValue> instruction,
        Label label,
        int ordinal,
        Func<IShaderValue, CooperationUniformDependencies> dependenciesOf,
        IReadOnlyDictionary<FunctionDeclaration, CooperationFunctionUniformityFacts> completed,
        IReadOnlyDictionary<ParameterPointerValue, int> eligibleFormals,
        IReadOnlyDictionary<(Label Label, int Ordinal), OperationRequirementSite> directRequirements,
        IReadOnlyDictionary<(Label Label, int Ordinal), FunctionEffectUnknownSite> directUnknowns)
    {
        if (instruction.Operation is CallOperation &&
            instruction.OperandCount > 0 &&
            instruction[0] is FunctionDeclaration callee)
        {
            if (completed.TryGetValue(callee, out var helper))
                return new(
                    DependencyLattice.Substitute(
                        helper.AggregateReturnDependencies,
                        position =>
                            position + 1 < instruction.OperandCount &&
                            instruction[position + 1] is { } argument
                                ? dependenciesOf(argument)
                                : DependencyLattice.Unknown),
                    CooperationDependencyValueKind.HelperCallResult,
                    callee);
            if (NumericBuiltins.Contains(callee))
                return new(
                    DerivativeBuiltins.Contains(callee)
                        ? DependencyLattice.Unknown
                        : DependencyLattice.Union(
                            instruction.Operands.Skip(1).Select(operand =>
                                dependenciesOf(operand))),
                    CooperationDependencyValueKind.NumericBuiltinResult,
                    callee);
            return DependencyClassification.Varying;
        }

        if (instruction.Operation is LoadOperation &&
            instruction.OperandCount == 1 &&
            instruction.Operand0 is ParameterPointerValue parameter &&
            eligibleFormals.TryGetValue(parameter, out var position))
            return new(
                DependencyLattice.ForFormal(position),
                CooperationDependencyValueKind.FunctionParameterLoad,
                null);

        if (instruction.Operation is LoadOperation or StoreOperation or IAddressOfOperation or AccessChainOperation ||
            instruction.Operation is IOperationRequirementProvider ||
            instruction.Result?.Type is IPtrType ||
            directRequirements.ContainsKey((label, ordinal)) ||
            directUnknowns.ContainsKey((label, ordinal)))
            return DependencyClassification.Varying;

        return new(
            FunctionEffectAnalysis.IsKnownPureOperation(instruction.Operation)
                ? DependencyLattice.Union(
                    instruction.Operands.Select(dependenciesOf))
                : DependencyLattice.Unknown,
            CooperationDependencyValueKind.OperationResult,
            null);
    }

    internal static ImmutableDictionary<ParameterPointerValue, int> EligibleFormalLoads(
        RegionFunctionBody body)
    {
        var positions = new Dictionary<ParameterPointerValue, int>(
            ReferenceEqualityComparer.Instance);
        foreach (var (position, parameter) in body.Declaration.Parameters.Index())
            if (parameter.Type is not IPtrType &&
                !ShaderModuleMetadataValidator.IsResourceTypeOrPointer(parameter.Type))
                positions.Add(parameter.Value, position);
        var invalid = new HashSet<ParameterPointerValue>(ReferenceEqualityComparer.Instance);
        var loaded = new HashSet<ParameterPointerValue>(ReferenceEqualityComparer.Instance);

        body.Body.Traverse(region =>
        {
            foreach (var instruction in region.Body.Body.Elements)
            {
                foreach (var operand in instruction.Operands.OfType<ParameterPointerValue>())
                {
                    if (!positions.ContainsKey(operand))
                        continue;
                    if (instruction.Operation is not LoadOperation ||
                        instruction.OperandCount != 1 ||
                        !ReferenceEquals(instruction.Operand0, operand) ||
                        instruction.Result is null ||
                        !instruction.Result.Type.Equals(operand.Declaration.Type))
                        invalid.Add(operand);
                    else
                        loaded.Add(operand);
                }
            }

            foreach (var value in TerminatorValues(region.Body.Body.Last).OfType<ParameterPointerValue>())
                if (positions.ContainsKey(value))
                    invalid.Add(value);
        });

        var eligible = ImmutableDictionary.CreateBuilder<ParameterPointerValue, int>(
            ReferenceEqualityComparer.Instance);
        foreach (var (parameter, position) in positions)
            if (loaded.Contains(parameter) &&
                !invalid.Contains(parameter))
                eligible.Add(parameter, position);
        return eligible.ToImmutable();
    }

    private static ImmutableDictionary<IShaderValue, CooperationUniformDependencies.Known>
        IntersectDependencies(
            ImmutableDictionary<IShaderValue, CooperationUniformDependencies.Known> left,
            ImmutableDictionary<IShaderValue, CooperationUniformDependencies.Known> right)
    {
        var result = ImmutableDictionary.CreateBuilder<
            IShaderValue,
            CooperationUniformDependencies.Known>(ReferenceEqualityComparer.Instance);
        foreach (var (value, dependencies) in left)
            if (right.TryGetValue(value, out var found) &&
                DependencyLattice.Equals(dependencies, found))
                result.Add(value, dependencies);
        return result.ToImmutable();
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

    private static CooperationUniformDependencies DependenciesOf(
        IShaderValue value,
        FlowEnvironment environment) =>
        value is LiteralValue
            ? DependencyLattice.Empty
            : environment.Dependencies.TryGetValue(value, out var dependencies)
                ? dependencies
                : DependencyLattice.Unknown;

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
        ImmutableDictionary<IShaderValue, CooperationUniformDependencies.Known> Dependencies)
    {
        internal FlowEnvironment Define(
            IShaderValue value,
            CooperationUniformDependencies dependencies) =>
            new(
                Available.Add(value),
                dependencies is CooperationUniformDependencies.Known known
                    ? Dependencies.SetItem(value, known)
                    : Dependencies.Remove(value));
    }

    internal readonly record struct DependencyClassification(
        CooperationUniformDependencies Dependencies,
        CooperationDependencyValueKind Kind,
        FunctionDeclaration? Callee)
    {
        internal static DependencyClassification Varying { get; } =
            new(
                DependencyLattice.Unknown,
                CooperationDependencyValueKind.OperationResult,
                null);
    }

    internal static class DependencyLattice
    {
        internal static CooperationUniformDependencies.Unknown Unknown { get; } = new();
        internal static CooperationUniformDependencies.Known Empty { get; } = new([]);

        internal static CooperationUniformDependencies.Known ForFormal(int position) =>
            new([position]);

        internal static CooperationUniformDependencies Union(
            IEnumerable<CooperationUniformDependencies> dependencies)
        {
            var positions = ImmutableArray.CreateBuilder<int>();
            foreach (var dependency in dependencies)
            {
                if (dependency is not CooperationUniformDependencies.Known known)
                    return Unknown;
                positions.AddRange(known.FormalParameterPositions);
            }
            return new CooperationUniformDependencies.Known(positions);
        }

        internal static CooperationUniformDependencies Substitute(
            CooperationUniformDependencies dependencies,
            Func<int, CooperationUniformDependencies> actual)
        {
            if (dependencies is not CooperationUniformDependencies.Known known)
                return Unknown;
            return Union(known.FormalParameterPositions.Select(actual));
        }

        internal static bool IsEmpty(CooperationUniformDependencies dependencies) =>
            dependencies is CooperationUniformDependencies.Known
            {
                FormalParameterPositions: { IsEmpty: true }
            };

        internal static bool Equals(
            CooperationUniformDependencies left,
            CooperationUniformDependencies right) =>
            (left, right) switch
            {
                (CooperationUniformDependencies.Unknown, CooperationUniformDependencies.Unknown) => true,
                (
                    CooperationUniformDependencies.Known leftKnown,
                    CooperationUniformDependencies.Known rightKnown) =>
                    leftKnown.FormalParameterPositions.AsEnumerable().SequenceEqual(
                        rightKnown.FormalParameterPositions),
                _ => false
            };
    }
}
