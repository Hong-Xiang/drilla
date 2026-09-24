using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Analysis;

public enum FunctionEffectUnknownReason
{
    UnknownOperation,
    UnknownCallee,
    MissingCalleeBody,
    UnsupportedRequirement,
    RecursiveCallCycle
}

public sealed record OperationRequirementSite(
    FunctionDeclaration Function,
    Label Label,
    int InstructionOrdinal,
    IOperation Operation,
    OperationRequirement Requirements,
    object? Payload);

public sealed record FunctionEffectUnknownSite(
    FunctionDeclaration Function,
    Label Label,
    int InstructionOrdinal,
    IOperation Operation,
    FunctionEffectUnknownReason Reason,
    IShaderValue? Callee,
    OperationRequirement UnsupportedRequirements,
    object? Payload);

public sealed record FunctionEffectSummary(
    OperationRequirement Requirements,
    ImmutableArray<OperationRequirementSite> RequirementSites,
    ImmutableArray<FunctionEffectUnknownSite> UnknownSites)
{
    public bool IsComplete => UnknownSites.IsEmpty;
}

public sealed record FunctionEffectAnalysisResult(
    ImmutableDictionary<FunctionDeclaration, FunctionEffectSummary> Summaries)
{
    public FunctionEffectSummary this[FunctionDeclaration function] => Summaries[function];
}

public static class FunctionEffectAnalysis
{
    private const OperationRequirement SupportedRequirements =
        OperationRequirement.MemoryRead |
        OperationRequirement.MemoryWrite |
        OperationRequirement.DerivativeQuad |
        OperationRequirement.SubgroupParticipation |
        OperationRequirement.WorkgroupBarrier;

    private static readonly FrozenSet<FunctionDeclaration> NumericBuiltins =
        ShaderFunction.Instance.Functions.ToFrozenSet();

    private static readonly FrozenSet<FunctionDeclaration> DerivativeBuiltins =
        ShaderFunction.Instance.Functions
            .Where(static function =>
                Enum.TryParse<NumericBuiltinFunctionName>(function.Name, out var name) &&
                name is >= NumericBuiltinFunctionName.dpdx and <= NumericBuiltinFunctionName.fwidthFine)
            .ToFrozenSet();

    private static readonly FrozenSet<Type> BinaryArithmeticOperators =
    [
        typeof(BinaryArithmetic.Add),
        typeof(BinaryArithmetic.Sub),
        typeof(BinaryArithmetic.Mul),
        typeof(BinaryArithmetic.Div),
        typeof(BinaryArithmetic.Rem),
        typeof(BinaryArithmetic.Min),
        typeof(BinaryArithmetic.Max),
        typeof(BinaryArithmetic.BitwiseAnd),
        typeof(BinaryArithmetic.BitwiseOr),
        typeof(BinaryArithmetic.BitwiseXor)
    ];

    private static readonly FrozenSet<Type> BinaryRelationalOperators =
    [
        typeof(BinaryRelational.Lt),
        typeof(BinaryRelational.Gt),
        typeof(BinaryRelational.Le),
        typeof(BinaryRelational.Ge),
        typeof(BinaryRelational.Eq),
        typeof(BinaryRelational.Ne)
    ];

    private static readonly FrozenSet<Type> BinaryLogicalOperators =
    [
        typeof(LogicalAnd),
        typeof(LogicalOr),
        typeof(LogicalXor),
        typeof(BinaryRelational.Eq),
        typeof(BinaryRelational.Ne)
    ];

    public static FunctionEffectAnalysisResult Analyze(
        ShaderModuleDeclaration<RegionFunctionBody> module)
    {
        ArgumentNullException.ThrowIfNull(module);

        var definitions = module.FunctionDefinitions;
        var direct = definitions
            .OrderBy(static pair => FunctionKey(pair.Key), StringComparer.Ordinal)
            .ToDictionary(
                static pair => pair.Key,
                pair => AnalyzeDirect(pair.Key, pair.Value, definitions));
        var components = StronglyConnectedComponents(direct);
        var componentByFunction = components
            .SelectMany((component, index) => component.Functions.Select(function => (function, index)))
            .ToDictionary(static item => item.function, static item => item.index);
        var completed = new Dictionary<int, ComponentSummary>();

        ComponentSummary Complete(int componentIndex)
        {
            if (completed.TryGetValue(componentIndex, out var found))
                return found;

            var component = components[componentIndex];
            var requirements = new HashSet<OperationRequirementSite>();
            var unknowns = new HashSet<FunctionEffectUnknownSite>();
            var dependencies = new HashSet<int>();

            foreach (var function in component.Functions)
            {
                var facts = direct[function];
                requirements.UnionWith(facts.RequirementSites);
                unknowns.UnionWith(facts.UnknownSites);
                foreach (var call in facts.Calls)
                {
                    var dependency = componentByFunction[call.Callee];
                    if (dependency == componentIndex)
                    {
                        if (component.IsRecursive)
                            unknowns.Add(call.ToUnknown(FunctionEffectUnknownReason.RecursiveCallCycle));
                    }
                    else
                    {
                        dependencies.Add(dependency);
                    }
                }
            }

            foreach (var dependency in dependencies.OrderBy(index => components[index].Key, StringComparer.Ordinal))
            {
                var summary = Complete(dependency);
                requirements.UnionWith(summary.RequirementSites);
                unknowns.UnionWith(summary.UnknownSites);
            }

            var result = new ComponentSummary(
                SortRequirementSites(requirements, direct),
                SortUnknownSites(unknowns, direct));
            completed.Add(componentIndex, result);
            return result;
        }

        var summaries = definitions.Keys.ToImmutableDictionary(
            static function => function,
            function =>
            {
                var summary = Complete(componentByFunction[function]);
                return new FunctionEffectSummary(
                    summary.RequirementSites.Aggregate(
                        OperationRequirement.None,
                        static (requirements, site) => requirements | site.Requirements),
                    summary.RequirementSites,
                    summary.UnknownSites);
            });
        return new FunctionEffectAnalysisResult(summaries);
    }

    private static DirectFunctionFacts AnalyzeDirect(
        FunctionDeclaration function,
        RegionFunctionBody body,
        ImmutableDictionary<FunctionDeclaration, RegionFunctionBody> definitions)
    {
        var requirementSites = ImmutableArray.CreateBuilder<OperationRequirementSite>();
        var unknownSites = ImmutableArray.CreateBuilder<FunctionEffectUnknownSite>();
        var calls = ImmutableArray.CreateBuilder<CallSite>();
        var labelOrdinals = new Dictionary<Label, int>();
        var blocks = new List<ShaderRegionBody>();
        body.Body.Traverse((region, _, _) =>
        {
            labelOrdinals.Add(region.Label, blocks.Count);
            blocks.Add(region.Body);
            return false;
        });

        foreach (var block in blocks)
            foreach (var (instruction, instructionOrdinal) in block.Body.Elements.Select(
                         static (instruction, ordinal) => (instruction, ordinal)))
                Classify(
                    function,
                    block.Label,
                    instructionOrdinal,
                    instruction,
                    definitions,
                    requirementSites,
                    unknownSites,
                    calls);

        return new DirectFunctionFacts(
            requirementSites.ToImmutable(),
            unknownSites.ToImmutable(),
            calls.ToImmutable(),
            labelOrdinals);
    }

    private static void Classify(
        FunctionDeclaration function,
        Label label,
        int instructionOrdinal,
        Instruction<IShaderValue, IShaderValue> instruction,
        ImmutableDictionary<FunctionDeclaration, RegionFunctionBody> definitions,
        ImmutableArray<OperationRequirementSite>.Builder requirementSites,
        ImmutableArray<FunctionEffectUnknownSite>.Builder unknownSites,
        ImmutableArray<CallSite>.Builder calls)
    {
        var operation = instruction.Operation;
        switch (operation)
        {
            case CallOperation:
                ClassifyCall(
                    function,
                    label,
                    instructionOrdinal,
                    instruction,
                    definitions,
                    requirementSites,
                    unknownSites,
                    calls);
                return;
            case LoadOperation:
                AddRequirement(OperationRequirement.MemoryRead);
                return;
            case StoreOperation:
                AddRequirement(OperationRequirement.MemoryWrite);
                return;
        }

        if (KnownRequirement(operation) is { } requirement)
        {
            AddRequirement(requirement);
            return;
        }

        if (IsKnownPureOperation(operation))
            return;

        if (operation is IOperationRequirementProvider provider)
        {
            var requirements = provider.Requirements;
            var known = requirements & SupportedRequirements;
            var unsupported = requirements & ~SupportedRequirements;
            if (known != OperationRequirement.None)
                AddRequirement(known);
            if (unsupported != OperationRequirement.None)
                unknownSites.Add(Unknown(FunctionEffectUnknownReason.UnsupportedRequirement, null, unsupported));
            return;
        }

        unknownSites.Add(Unknown(FunctionEffectUnknownReason.UnknownOperation));
        return;

        void AddRequirement(OperationRequirement requirements) =>
            requirementSites.Add(new OperationRequirementSite(
                function,
                label,
                instructionOrdinal,
                operation,
                requirements,
                instruction.Payload));

        FunctionEffectUnknownSite Unknown(
            FunctionEffectUnknownReason reason,
            IShaderValue? callee = null,
            OperationRequirement unsupportedRequirements = OperationRequirement.None) =>
            new(
                function,
                label,
                instructionOrdinal,
                operation,
                reason,
                callee,
                unsupportedRequirements,
                instruction.Payload);
    }

    private static void ClassifyCall(
        FunctionDeclaration function,
        Label label,
        int instructionOrdinal,
        Instruction<IShaderValue, IShaderValue> instruction,
        ImmutableDictionary<FunctionDeclaration, RegionFunctionBody> definitions,
        ImmutableArray<OperationRequirementSite>.Builder requirementSites,
        ImmutableArray<FunctionEffectUnknownSite>.Builder unknownSites,
        ImmutableArray<CallSite>.Builder calls)
    {
        var callee = instruction.OperandCount > 0 ? instruction[0] : null;
        if (callee is not FunctionDeclaration declaration)
        {
            unknownSites.Add(new FunctionEffectUnknownSite(
                function,
                label,
                instructionOrdinal,
                instruction.Operation,
                FunctionEffectUnknownReason.UnknownCallee,
                callee,
                OperationRequirement.None,
                instruction.Payload));
            return;
        }

        if (DerivativeBuiltins.Contains(declaration))
        {
            requirementSites.Add(new OperationRequirementSite(
                function,
                label,
                instructionOrdinal,
                instruction.Operation,
                OperationRequirement.DerivativeQuad,
                instruction.Payload));
            return;
        }

        if (NumericBuiltins.Contains(declaration))
            return;

        if (definitions.ContainsKey(declaration))
        {
            calls.Add(new CallSite(
                function,
                label,
                instructionOrdinal,
                instruction.Operation,
                declaration,
                instruction.Payload));
            return;
        }

        unknownSites.Add(new FunctionEffectUnknownSite(
            function,
            label,
            instructionOrdinal,
            instruction.Operation,
            FunctionEffectUnknownReason.MissingCalleeBody,
            declaration,
            OperationRequirement.None,
            instruction.Payload));
    }

    public static bool IsKnownPureOperation(IOperation operation)
    {
        if (operation is LiteralOperation or NopOperation or AccessChainOperation or
            AddressOfMemberOperation or AddressOfVecComponentOperation or
            VectorCompositeConstructionOperation or StructureCompositeConstructionOperation or StructureMemberGetOperation or
            ZeroConstructorOperation or LogicalNotOperation)
            return true;

        var type = operation.GetType();
        if (!type.IsGenericType)
            return false;

        var definition = type.GetGenericTypeDefinition();
        var arguments = type.GetGenericArguments();
        return definition == typeof(NumericBinaryArithmeticOperation<,>) &&
               BinaryArithmeticOperators.Contains(arguments[1]) ||
               definition == typeof(NumericBinaryRelationalOperation<,>) &&
               BinaryRelationalOperators.Contains(arguments[1]) ||
               definition == typeof(LogicalBinaryOperation<>) &&
               BinaryLogicalOperators.Contains(arguments[0]) ||
               definition == typeof(UnaryNumericArithmeticExpressionOperation<,>) &&
               arguments[1] == typeof(UnaryArithmetic.Negate) ||
               definition == typeof(ScalarConversionOperation<,>) ||
               definition == typeof(ScalarBitCastOperation<,>) ||
               definition == typeof(VectorNumericUnaryOperation<,,>) &&
               arguments[2] == typeof(UnaryArithmetic.Negate) ||
               definition == typeof(VectorExpressionNumericBinaryExpressionOperation<,,>) &&
               IsKnownBinaryOperator(arguments[2]) ||
               definition == typeof(ScalarVectorExpressionNumericOperation<,,>) &&
               IsKnownBinaryOperator(arguments[2]) ||
               definition == typeof(VectorScalarExpressionNumericOperation<,,>) &&
               IsKnownBinaryOperator(arguments[2]) ||
               definition == typeof(VectorFromScalarConstructOperation<,>);
    }

    private static bool IsKnownBinaryOperator(Type type) =>
        BinaryArithmeticOperators.Contains(type) ||
        BinaryRelationalOperators.Contains(type) ||
        BinaryLogicalOperators.Contains(type);

    private static OperationRequirement? KnownRequirement(IOperation operation)
    {
        var type = operation.GetType();
        if (!type.IsGenericType)
            return null;

        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(VectorComponentGetExpressionOperation<,,>) ||
            definition == typeof(VectorSwizzleGetExpressionOperation<,>))
            return OperationRequirement.MemoryRead;
        if (definition == typeof(VectorComponentSetOperation<,,>) ||
            definition == typeof(VectorSwizzleSetOperation<,>))
            return OperationRequirement.MemoryWrite;
        return null;
    }

    private static ImmutableArray<Component> StronglyConnectedComponents(
        IReadOnlyDictionary<FunctionDeclaration, DirectFunctionFacts> functions)
    {
        var index = 0;
        var indices = new Dictionary<FunctionDeclaration, int>();
        var lowLinks = new Dictionary<FunctionDeclaration, int>();
        var stack = new Stack<FunctionDeclaration>();
        var onStack = new HashSet<FunctionDeclaration>();
        var components = ImmutableArray.CreateBuilder<Component>();

        void Visit(FunctionDeclaration function)
        {
            indices.Add(function, index);
            lowLinks.Add(function, index);
            index++;
            stack.Push(function);
            onStack.Add(function);

            foreach (var callee in functions[function].Calls
                         .Select(static call => call.Callee)
                         .Distinct()
                         .OrderBy(static callee => FunctionKey(callee), StringComparer.Ordinal))
            {
                if (!indices.ContainsKey(callee))
                {
                    Visit(callee);
                    lowLinks[function] = Math.Min(lowLinks[function], lowLinks[callee]);
                }
                else if (onStack.Contains(callee))
                {
                    lowLinks[function] = Math.Min(lowLinks[function], indices[callee]);
                }
            }

            if (lowLinks[function] != indices[function])
                return;

            var members = ImmutableArray.CreateBuilder<FunctionDeclaration>();
            FunctionDeclaration member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                members.Add(member);
            } while (!ReferenceEquals(member, function));

            var ordered = members
                .OrderBy(static item => FunctionKey(item), StringComparer.Ordinal)
                .ToImmutableArray();
            var recursive = ordered.Length > 1 ||
                            functions[ordered[0]].Calls.Any(call => ReferenceEquals(call.Callee, ordered[0]));
            components.Add(new Component(ordered, recursive));
        }

        foreach (var function in functions.Keys.OrderBy(static item => FunctionKey(item), StringComparer.Ordinal))
            if (!indices.ContainsKey(function))
                Visit(function);

        return components.ToImmutable();
    }

    private static ImmutableArray<OperationRequirementSite> SortRequirementSites(
        IEnumerable<OperationRequirementSite> sites,
        IReadOnlyDictionary<FunctionDeclaration, DirectFunctionFacts> direct) =>
        [.. sites.OrderBy(site => FunctionKey(site.Function), StringComparer.Ordinal)
            .ThenBy(site => direct[site.Function].LabelOrdinals[site.Label])
            .ThenBy(static site => site.InstructionOrdinal)
            .ThenBy(static site => site.Operation.Name, StringComparer.Ordinal)
            .ThenBy(static site => (int)site.Requirements)];

    private static ImmutableArray<FunctionEffectUnknownSite> SortUnknownSites(
        IEnumerable<FunctionEffectUnknownSite> sites,
        IReadOnlyDictionary<FunctionDeclaration, DirectFunctionFacts> direct) =>
        [.. sites.OrderBy(site => FunctionKey(site.Function), StringComparer.Ordinal)
            .ThenBy(site => direct[site.Function].LabelOrdinals[site.Label])
            .ThenBy(static site => site.InstructionOrdinal)
            .ThenBy(static site => site.Reason)
            .ThenBy(static site => site.Operation.Name, StringComparer.Ordinal)];

    private static string FunctionKey(FunctionDeclaration function) =>
        $"{function.Name}({string.Join(",", function.Parameters.Select(static parameter => parameter.Type.Name))})" +
        $"->{function.ReturnType.Name}";

    private sealed record DirectFunctionFacts(
        ImmutableArray<OperationRequirementSite> RequirementSites,
        ImmutableArray<FunctionEffectUnknownSite> UnknownSites,
        ImmutableArray<CallSite> Calls,
        IReadOnlyDictionary<Label, int> LabelOrdinals);

    private sealed record CallSite(
        FunctionDeclaration Function,
        Label Label,
        int InstructionOrdinal,
        IOperation Operation,
        FunctionDeclaration Callee,
        object? Payload)
    {
        public FunctionEffectUnknownSite ToUnknown(FunctionEffectUnknownReason reason) =>
            new(
                Function,
                Label,
                InstructionOrdinal,
                Operation,
                reason,
                Callee,
                OperationRequirement.None,
                Payload);
    }

    private sealed record Component(
        ImmutableArray<FunctionDeclaration> Functions,
        bool IsRecursive)
    {
        public string Key => FunctionKey(Functions[0]);
    }

    private sealed record ComponentSummary(
        ImmutableArray<OperationRequirementSite> RequirementSites,
        ImmutableArray<FunctionEffectUnknownSite> UnknownSites);
}
