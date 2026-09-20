using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL;

internal static class CooperationTargetVerifier
{
    internal static void Verify(
        ShaderModuleDeclaration<RegionFunctionBody> source,
        ShaderModuleDeclaration<SlangFunctionBody> target,
        CLSLCooperationFacts facts)
    {
        foreach (var participation in facts.EntryUniformQuadParticipations)
            foreach (var (function, labels) in participation.OriginalBlocks)
                VerifyFunction(
                    participation,
                    source.FunctionDefinitions[function],
                    target.FunctionDefinitions[function],
                    labels);
    }

    private static void VerifyFunction(
        EntryUniformQuadParticipation participation,
        RegionFunctionBody source,
        SlangFunctionBody target,
        ImmutableArray<Label> labels)
    {
        var context =
            $"PortableWgsl target correspondence for entry '{participation.Entry.Name}', " +
            $"function '{source.Declaration.Name}'";
        var tree = TargetTree.Create(target.Body, context);
        if (tree.Statements.Any(static statement => statement is SlangLoop or SlangContinue))
            throw Error(context, "produced loop/continue target control");

        foreach (var label in labels)
            if (tree.Statements.Count(statement =>
                    statement is SlangScope { OriginalLabel: var found } &&
                    ReferenceEquals(found, label)) != 1)
                throw Error(context, $"did not preserve label '{label.Name}' exactly once");

        var origins = target.Origins;
        VerifyControlledVariables(origins, tree, context);
        VerifyDefinitions(
            participation.Uniformity[source.Declaration],
            source,
            origins,
            tree,
            context);
        VerifyConditionals(
            participation.Uniformity[source.Declaration],
            source,
            origins,
            tree,
            context);
        VerifyTransfers(source, origins, tree, context);
        VerifyGates(origins, tree, context);
        VerifyRelevantSites(participation, source, tree, context);
        VerifyControlDataflow(target, origins, context);
    }

    private static void VerifyDefinitions(
        CooperationFunctionUniformityFacts facts,
        RegionFunctionBody source,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        foreach (var fact in facts.UniformValues)
        {
            if (fact.Kind is CooperationUniformValueKind.BlockParameter)
            {
                var parameterOrigin = Single(
                    origins.Parameters,
                    item => ReferenceEquals(item.Parameter, fact.Value) &&
                            ReferenceEquals(item.Label, fact.Label),
                    context,
                    "uniform block parameter origin");
                RequirePresent(tree, parameterOrigin.Definition, context);
                VerifyParameterOrigin(parameterOrigin, origins, tree, context);
                continue;
            }

            var ordinal = fact.InstructionOrdinal ??
                throw Error(context, "uniform operation fact has no instruction ordinal");
            var sourceInstruction = source[fact.Label].Body.Elements.ElementAt(ordinal);
            if (!ReferenceEquals(sourceInstruction.Result, fact.Value))
                throw Error(context, "uniform value fact does not identify its source definition");
            var origin = Single(
                origins.Definitions,
                item => ReferenceEquals(item.Label, fact.Label) &&
                        item.InstructionOrdinal == ordinal &&
                        item.Source.Equals(sourceInstruction),
                context,
                "uniform operation origin");
            RequirePresent(tree, origin.Definition, context);
            var targetInstruction = origin.Definition.Instruction;
            if (!ReferenceEquals(targetInstruction.Operation, sourceInstruction.Operation) ||
                !ReferenceEquals(targetInstruction.Result, sourceInstruction.Result) ||
                !ReferenceEquals(targetInstruction.Payload, sourceInstruction.Payload) ||
                !OperandsMatch(sourceInstruction.Operands, targetInstruction.Operands, origins))
                throw Error(
                    context,
                    $"uniform producer in block '{fact.Label.Name}', instruction {ordinal} " +
                    "does not preserve its operation/result/operand lineage");
            VerifyCapture(
                sourceInstruction.Result!,
                origin.Definition,
                origin.Capture,
                origins,
                tree,
                context);
        }

        foreach (var returned in facts.UniformReturns)
        {
            if (returned.Value is null)
                continue;
            var scope = Single(
                tree.Statements.OfType<SlangScope>(),
                item => ReferenceEquals(item.OriginalLabel, returned.Label),
                context,
                "uniform return source scope");
            var targetReturn = Single(
                Descendants(scope.Body).OfType<SlangReturnValue>(),
                static _ => true,
                context,
                "uniform target return");
            if (!OperandMatches(returned.Value, targetReturn.Value, origins))
                throw Error(
                    context,
                    $"uniform return in block '{returned.Label.Name}' does not preserve value lineage");
        }
    }

    private static void VerifyParameterOrigin(
        SlangParameterOrigin origin,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var instruction = origin.Definition.Instruction;
        var operands = instruction.Operands.ToImmutableArray();
        if (instruction.Operation is not LoadOperation ||
            !ReferenceEquals(instruction.Result, origin.Parameter) ||
            operands.Length != 1 ||
            operands[0] is not SlangPlaceOperand
            {
                Place: SlangVariablePlace { Variable: var slot }
            } ||
            !ReferenceEquals(slot, origin.Slot) ||
            !origins.ParameterSlots.TryGetValue(origin.Parameter, out var expected) ||
            !ReferenceEquals(expected, origin.Slot))
            throw Error(context, "block parameter definition does not load its assigned slot");
        VerifyCapture(
            origin.Parameter,
            origin.Definition,
            origin.Capture,
            origins,
            tree,
            context);
    }

    private static void VerifyCapture(
        IShaderValue value,
        SlangBind definition,
        SlangAssign? assignment,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        if (!origins.Captures.TryGetValue(value, out var capture))
        {
            if (assignment is not null)
                throw Error(context, "definition has an unexpected capture assignment");
            return;
        }
        if (assignment is null)
            throw Error(context, "captured uniform definition is missing its capture assignment");
        RequirePresent(tree, assignment, context);
        if (assignment.Target is not SlangVariablePlace { Variable: var target } ||
            !ReferenceEquals(target, capture) ||
            assignment.Value is not SlangValueOperand { Value: var assigned } ||
            !ReferenceEquals(assigned, value))
            throw Error(context, "captured uniform definition writes the wrong value or carrier");
        if (!tree.AreAdjacent(definition, assignment))
            throw Error(context, "capture assignment does not immediately follow its definition");
    }

    private static void VerifyConditionals(
        CooperationFunctionUniformityFacts facts,
        RegionFunctionBody source,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        if (origins.Conditionals.Length != facts.UniformConditionals.Length)
            throw Error(context, "source conditional origin count does not match proven uniform decisions");
        foreach (var fact in facts.UniformConditionals)
        {
            var terminator = source[fact.Label].Body.Last as
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> ??
                throw Error(context, $"conditional fact for block '{fact.Label.Name}' is not a BrIf");
            if (!ReferenceEquals(terminator.Condition, fact.Condition) ||
                !ReferenceEquals(terminator.TrueTarget.Label, fact.TrueTarget) ||
                !ReferenceEquals(terminator.FalseTarget.Label, fact.FalseTarget))
                throw Error(context, $"conditional fact for block '{fact.Label.Name}' changed source arms");
            var origin = Single(
                origins.Conditionals,
                item => ReferenceEquals(item.Source, fact.Label),
                context,
                "source conditional origin");
            RequirePresent(tree, origin.Conditional, context);
            if (!ReferenceEquals(origin.Condition, fact.Condition) ||
                !OperandMatches(fact.Condition, origin.Conditional.Condition, origins))
                throw Error(context, $"conditional in block '{fact.Label.Name}' changed its condition lineage");
            VerifyArm(origin.Conditional.WhenTrue, origins, fact.Label, 0, context);
            VerifyArm(origin.Conditional.WhenFalse, origins, fact.Label, 1, context);
        }

        var accounted = origins.Conditionals.Select(static item => item.Conditional)
            .Concat(origins.Gates.Select(static item => item.Conditional))
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var conditional in tree.Statements.OfType<SlangIf>())
            if (!accounted.Contains(conditional))
                throw Error(context, "target contains an unaccounted conditional");
        if (accounted.Count != tree.Statements.OfType<SlangIf>().Count())
            throw Error(context, "conditional origin does not identify exactly one actual target conditional");
    }

    private static void VerifyArm(
        SlangBlock block,
        SlangLoweringOrigins origins,
        Label source,
        int arm,
        string context)
    {
        var transfer = Single(
            origins.Transfers,
            item => ReferenceEquals(item.Source, source) && item.Arm == arm,
            context,
            $"conditional arm {arm} transfer origin");
        if (!Descendants(block).Any(statement => ReferenceEquals(statement, transfer.Break)))
            throw Error(context, $"conditional arm {arm} contains the wrong source transfer");
    }

    private static void VerifyTransfers(
        RegionFunctionBody source,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var expected = source.Control.Transfers;
        if (origins.Transfers.Length != expected.Length)
            throw Error(context, "target transfer origin count does not match checked source transfers");

        var ids = new Dictionary<SlangContinuationOrigin, int>();
        var owners = new Dictionary<int, SlangContinuationOrigin>();
        foreach (var transfer in expected)
        {
            var origin = Single(
                origins.Transfers,
                item => ReferenceEquals(item.Source, transfer.Source) && item.Arm == transfer.Arm,
                context,
                "checked transfer origin");
            var jump = Jump(source[transfer.Source].Body.Last, transfer.Arm);
            if (!ReferenceEquals(origin.Jump, jump) ||
                !Same(origin.Continuation, transfer))
                throw Error(
                    context,
                    $"transfer from '{transfer.Source.Name}', arm {transfer.Arm} changed target/owner/kind");
            if (ids.TryGetValue(origin.Continuation, out var knownId) && knownId != origin.TokenId)
                throw Error(context, "one continuation identity has multiple token IDs");
            ids[origin.Continuation] = origin.TokenId;
            if (owners.TryGetValue(origin.TokenId, out var knownOwner) &&
                !Equals(knownOwner, origin.Continuation))
                throw Error(context, "one token ID identifies multiple continuation identities");
            owners[origin.TokenId] = origin.Continuation;

            if (origin.Arguments.Length != jump.Arguments.Length)
                throw Error(context, "transfer argument origin count changed");
            foreach (var (position, argument) in jump.Arguments.Index())
            {
                var parameter = source[jump.Label].Parameters[position];
                var item = Single(
                    origin.Arguments,
                    candidate => candidate.Position == position,
                    context,
                    "transfer argument origin");
                RequirePresent(tree, item.Definition, context);
                RequirePresent(tree, item.Assignment, context);
                if (!ReferenceEquals(item.Argument, argument) ||
                    !ReferenceEquals(item.Parameter, parameter) ||
                    !origins.ParameterSlots.TryGetValue(parameter, out var slot) ||
                    !ReferenceEquals(slot, item.Slot) ||
                    item.Definition.Instruction.Operation is not LoadOperation ||
                    !ReferenceEquals(item.Definition.Instruction.Result, item.Snapshot) ||
                    item.Definition.Instruction.Operands.Count() != 1 ||
                    !OperandMatches(argument, item.Definition.Instruction.Operands.Single(), origins) ||
                    item.Assignment.Target is not SlangVariablePlace { Variable: var assignedSlot } ||
                    !ReferenceEquals(assignedSlot, slot) ||
                    item.Assignment.Value is not SlangValueOperand { Value: var assignedValue } ||
                    !ReferenceEquals(assignedValue, item.Snapshot))
                    throw Error(
                        context,
                        $"transfer from '{transfer.Source.Name}', arm {transfer.Arm}, parameter {position} " +
                        "does not preserve its parallel snapshot and slot assignment");
            }

            RequirePresent(tree, origin.TokenAssignment, context);
            RequirePresent(tree, origin.Break, context);
            var token = origins.ControlToken ??
                throw Error(context, "checked transfers require a control token");
            if (origin.TokenAssignment.Target is not SlangVariablePlace { Variable: var targetToken } ||
                !ReferenceEquals(targetToken, token) ||
                !TryInt(origin.TokenAssignment.Value, out var tokenId) ||
                tokenId != origin.TokenId)
                throw Error(context, "transfer writes the wrong control token or literal");

            var ordered = origin.Arguments.Select(static item => (SlangStatement)item.Definition)
                .Concat(origin.Arguments.Select(static item => (SlangStatement)item.Assignment))
                .Append(origin.TokenAssignment)
                .Append(origin.Break)
                .ToArray();
            if (!tree.AreInOrderInOneBlock(ordered))
                throw Error(
                    context,
                    $"transfer from '{transfer.Source.Name}', arm {transfer.Arm} does not snapshot all " +
                    "arguments before slot writes, token write, and break");
            var carrier = tree.NearestDoOnce(origin.Break);
            if (carrier is null ||
                !tree.Ancestors(origin.Break)
                    .TakeWhile(statement => !ReferenceEquals(statement, carrier))
                    .OfType<SlangScope>()
                    .Any(scope => ReferenceEquals(scope.OriginalLabel, transfer.Source)))
                throw Error(context, "transfer break is not owned by its source-label carrier");
        }
    }

    private static void VerifyGates(
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var token = origins.ControlToken;
        var continuations = origins.Transfers
            .GroupBy(static transfer => transfer.Continuation)
            .ToDictionary(static group => group.Key, static group => group.First().TokenId);
        foreach (var gate in origins.Gates)
        {
            RequirePresent(tree, gate.Comparison, context);
            RequirePresent(tree, gate.Conditional, context);
            var instruction = gate.Comparison.Instruction;
            var operands = instruction.Operands.ToImmutableArray();
            if (token is null ||
                instruction.Operation is not NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq> ||
                operands.Length != 2 ||
                operands[0] is not SlangPlaceOperand
                {
                    Place: SlangVariablePlace { Variable: var readToken }
                } ||
                !ReferenceEquals(readToken, token) ||
                !TryInt(operands[1], out var tokenId) ||
                tokenId != gate.TokenId ||
                !continuations.TryGetValue(gate.Continuation, out var continuationId) ||
                continuationId != gate.TokenId ||
                gate.Conditional.Condition is not SlangValueOperand { Value: var condition } ||
                !ReferenceEquals(condition, instruction.Result))
                throw Error(context, "token gate does not compare the correct token and continuation literal");
            if (!tree.AreAdjacent(gate.Comparison, gate.Conditional))
                throw Error(context, "token comparison does not immediately precede its continuation gate");
            if (!Descendants(gate.Conditional.WhenTrue).Any(statement =>
                    statement is SlangScope { OriginalLabel: var label } &&
                    ReferenceEquals(label, gate.Continuation.Target)))
                throw Error(context, "token gate does not guard its intended continuation");
        }
    }

    private static void VerifyControlledVariables(
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var allowedWrites = origins.Transfers.Select(static item => item.TokenAssignment)
            .Concat(origins.Transfers.SelectMany(static item => item.Arguments)
                .Select(static item => item.Assignment))
            .Concat(origins.Parameters.Select(static item => item.Capture).OfType<SlangAssign>())
            .Concat(origins.Definitions.Select(static item => item.Capture).OfType<SlangAssign>())
            .ToHashSet(ReferenceEqualityComparer.Instance);
        var controlled = origins.ParameterSlots.Values
            .Concat(origins.Captures.Values)
            .Concat(origins.ControlToken is null ? [] : [origins.ControlToken])
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var assignment in tree.Statements.OfType<SlangAssign>())
            if (assignment.Target is SlangVariablePlace { Variable: var variable } &&
                controlled.Contains(variable) &&
                !allowedWrites.Contains(assignment))
                throw Error(context, "target contains an unaccounted control/capture/slot write");

        var allowedTokenReads = origins.Gates.Select(static item => item.Comparison)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        if (origins.ControlToken is { } token)
            foreach (var bind in tree.Statements.OfType<SlangBind>())
                if (bind.Instruction.Operands.Any(operand =>
                        operand is SlangPlaceOperand
                        {
                            Place: SlangVariablePlace { Variable: var variable }
                        } &&
                        ReferenceEquals(variable, token)) &&
                    !allowedTokenReads.Contains(bind))
                    throw Error(context, "target contains an unaccounted control-token read");
    }

    private static void VerifyRelevantSites(
        EntryUniformQuadParticipation participation,
        RegionFunctionBody source,
        TargetTree tree,
        string context)
    {
        var targetInstructions = tree.Statements.Select(static statement =>
                (Instruction<SlangOperand, IShaderValue>?)(statement switch
                {
                    SlangBind binding => binding.Instruction,
                    SlangEffect effect => effect.Instruction,
                    _ => null
                }))
            .OfType<Instruction<SlangOperand, IShaderValue>>()
            .ToImmutableArray();
        foreach (var site in RelevantSites(participation, source.Declaration))
        {
            var sourceInstruction = source[site.Label].Body.Elements.ElementAt(site.InstructionOrdinal);
            if (targetInstructions.Count(instruction =>
                    ReferenceEquals(instruction.Operation, sourceInstruction.Operation) &&
                    ReferenceEquals(instruction.Result, sourceInstruction.Result) &&
                    ReferenceEquals(instruction.Payload, sourceInstruction.Payload)) != 1)
                throw Error(
                    context,
                    $"did not preserve exactly one placement of block '{site.Label.Name}', operation " +
                    $"'{sourceInstruction.Operation.Name}'");
        }
    }

    private static void VerifyControlDataflow(
        SlangFunctionBody target,
        SlangLoweringOrigins origins,
        string context)
    {
        var gates = origins.Gates.ToDictionary<SlangGateOrigin, SlangIf, SlangGateOrigin>(
            static item => item.Conditional,
            static item => item,
            ReferenceEqualityComparer.Instance);
        var initial = ImmutableArray.Create(TargetState.Empty);
        var result = Execute(target.Body, initial, gates, origins.ControlToken, context);
        if (!result.Breaks.IsEmpty)
            throw Error(context, "root target block has an unowned break");
    }

    private static Flow Execute(
        SlangBlock block,
        ImmutableArray<TargetState> input,
        IReadOnlyDictionary<SlangIf, SlangGateOrigin> gates,
        VariableDeclaration? token,
        string context)
    {
        var normal = input;
        var breaks = ImmutableArray.CreateBuilder<TargetState>();
        foreach (var statement in block.Statements)
        {
            if (normal.IsEmpty)
                break;
            var next = Execute(statement, normal, gates, token, context);
            normal = next.Normal;
            breaks.AddRange(next.Breaks);
        }
        return new(normal, breaks.ToImmutable());
    }

    private static Flow Execute(
        SlangStatement statement,
        ImmutableArray<TargetState> input,
        IReadOnlyDictionary<SlangIf, SlangGateOrigin> gates,
        VariableDeclaration? token,
        string context)
    {
        switch (statement)
        {
            case SlangDeclare:
                return Flow.FromNormal(input);
            case SlangBind bind:
                foreach (var state in input)
                    foreach (var operand in bind.Instruction.Operands)
                        RequireDefined(operand, state, context);
                return Flow.FromNormal([.. input.Select(state => state.Define(bind.Instruction.Result!))]);
            case SlangEffect effect:
                foreach (var state in input)
                    foreach (var operand in effect.Instruction.Operands)
                        RequireDefined(operand, state, context);
                return Flow.FromNormal(input);
            case SlangAssign assignment:
                foreach (var state in input)
                    RequireDefined(assignment.Value, state, context);
                return Flow.FromNormal(
                    [.. input.Select(state => state.Assign(assignment.Target, assignment.Value, token))]);
            case SlangScope scope:
                return Execute(scope.Body, input, gates, token, context);
            case SlangDoOnce once:
                var onceFlow = Execute(once.Body, input, gates, token, context);
                return Flow.FromNormal(Normalize([.. onceFlow.Normal, .. onceFlow.Breaks]));
            case SlangIf conditional:
                foreach (var state in input)
                    RequireDefined(conditional.Condition, state, context);
                if (gates.TryGetValue(conditional, out var gate))
                {
                    var whenTrue = input.Where(state => state.Token == gate.TokenId).ToImmutableArray();
                    var whenFalse = input.Where(state => state.Token != gate.TokenId).ToImmutableArray();
                    return Flow.Merge(
                        Execute(conditional.WhenTrue, whenTrue, gates, token, context),
                        Execute(conditional.WhenFalse, whenFalse, gates, token, context));
                }
                return Flow.Merge(
                    Execute(conditional.WhenTrue, input, gates, token, context),
                    Execute(conditional.WhenFalse, input, gates, token, context));
            case SlangReturnValue returned:
                foreach (var state in input)
                    RequireDefined(returned.Value, state, context);
                return Flow.Empty;
            case SlangReturnVoid:
                return Flow.Empty;
            case SlangBreak:
                return new([], input);
            case SlangLoop or SlangContinue:
                throw Error(context, "control dataflow does not admit loop/continue");
            default:
                throw Error(context, $"unknown target statement '{statement.GetType().Name}'");
        }
    }

    private static void RequireDefined(SlangOperand operand, TargetState state, string context)
    {
        switch (operand)
        {
            case SlangValueOperand { Value: LiteralValue or FunctionDeclaration }:
                return;
            case SlangValueOperand value when state.Values.Contains(value.Value):
                return;
            case SlangPlaceOperand { Place: SlangParameterPlace }:
                return;
            case SlangPlaceOperand place when PlaceDefined(place.Place, state):
                return;
            default:
                throw Error(context, "control dataflow reads a value without a reaching definition");
        }
    }

    private static bool PlaceDefined(SlangPlace place, TargetState state) =>
        place switch
        {
            SlangVariablePlace variable => state.Variables.Contains(variable.Variable),
            SlangParameterPlace => true,
            SlangMemberPlace member => PlaceDefined(member.Target, state),
            SlangComponentPlace component => PlaceDefined(component.Target, state),
            SlangSwizzlePlace swizzle => PlaceDefined(swizzle.Target, state),
            _ => false
        };

    private static ImmutableArray<TargetState> Normalize(IEnumerable<TargetState> states) =>
    [
        .. states.GroupBy(static state => state.Token)
            .Select(static group => group.Aggregate(static (left, right) => new(
                left.Values.Intersect(right.Values),
                left.Variables.Intersect(right.Variables),
                left.Token)))
    ];

    private static bool OperandsMatch(
        IEnumerable<IShaderValue> source,
        IEnumerable<SlangOperand> target,
        SlangLoweringOrigins origins) =>
        source.Count() == target.Count() &&
        source.Zip(target).All(pair => OperandMatches(pair.First, pair.Second, origins));

    private static bool OperandMatches(
        IShaderValue source,
        SlangOperand target,
        SlangLoweringOrigins origins)
    {
        if (origins.Captures.TryGetValue(source, out var capture))
            return target is SlangPlaceOperand
            {
                Place: SlangVariablePlace { Variable: var variable }
            } && ReferenceEquals(variable, capture);
        if (source is FunctionDeclaration function &&
            PortableDerivativeTarget.TryLower(function, out var mapped))
            return target is SlangValueOperand { Value: var value } && ReferenceEquals(value, mapped);
        return target is SlangValueOperand { Value: var found } && ReferenceEquals(found, source);
    }

    private static bool Same(SlangContinuationOrigin origin, ScopedTransfer<Label> transfer) =>
        ReferenceEquals(origin.Target, transfer.Target) &&
        ReferenceEquals(origin.Owner, transfer.Owner) &&
        origin.Kind == transfer.Kind;

    private static RegionJump<IShaderValue> Jump(
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
        int arm) =>
        terminator switch
        {
            Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch when arm == 0 => branch.Target,
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch when arm == 0 => branch.TrueTarget,
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch when arm == 1 => branch.FalseTarget,
            _ => throw new InvalidOperationException($"No source control arm {arm}.")
        };

    private static bool TryInt(SlangOperand operand, out int value)
    {
        if (operand is SlangValueOperand { Value: LiteralValue { Value: I32Literal literal } })
        {
            value = literal.Value;
            return true;
        }
        value = default;
        return false;
    }

    private static IEnumerable<(Label Label, int InstructionOrdinal)> RelevantSites(
        EntryUniformQuadParticipation participation,
        FunctionDeclaration function) =>
        participation.OriginalSensitiveSites
            .Where(site => ReferenceEquals(site.Function, function))
            .Select(static site => (site.Label, site.InstructionOrdinal))
            .Concat(participation.CallInheritance
                .Where(site => ReferenceEquals(site.Caller, function))
                .Select(static site => (site.Label, site.InstructionOrdinal)))
            .Distinct();

    private static T Single<T>(
        IEnumerable<T> source,
        Func<T, bool> predicate,
        string context,
        string description)
    {
        var found = source.Where(predicate).Take(2).ToArray();
        return found.Length == 1
            ? found[0]
            : throw Error(context, $"{description} count is {found.Length}, expected exactly one");
    }

    private static void RequirePresent(TargetTree tree, SlangStatement statement, string context)
    {
        if (!tree.Contains(statement))
            throw Error(context, "origin references a target statement that is not in the actual AST");
    }

    private static IEnumerable<SlangStatement> Descendants(SlangBlock block)
    {
        foreach (var statement in block.Statements)
        {
            yield return statement;
            foreach (var nested in statement switch
            {
                SlangScope scope => Descendants(scope.Body),
                SlangIf conditional => Descendants(conditional.WhenTrue)
                    .Concat(Descendants(conditional.WhenFalse)),
                SlangDoOnce once => Descendants(once.Body),
                SlangLoop loop => Descendants(loop.Body),
                _ => []
            })
                yield return nested;
        }
    }

    private static NotSupportedException Error(string context, string message) =>
        new($"{context}: {message}.");

    private sealed class TargetTree
    {
        private readonly Dictionary<SlangStatement, Location> locations =
            new(ReferenceEqualityComparer.Instance);

        private TargetTree()
        {
        }

        internal IEnumerable<SlangStatement> Statements => locations.Keys;

        internal static TargetTree Create(SlangBlock root, string context)
        {
            var result = new TargetTree();
            result.Collect(root, null, context);
            return result;
        }

        internal bool Contains(SlangStatement statement) => locations.ContainsKey(statement);

        internal bool AreAdjacent(SlangStatement first, SlangStatement second) =>
            locations.TryGetValue(first, out var left) &&
            locations.TryGetValue(second, out var right) &&
            ReferenceEquals(left.Block, right.Block) &&
            right.Index == left.Index + 1;

        internal bool AreInOrderInOneBlock(IReadOnlyList<SlangStatement> statements)
        {
            if (statements.Count == 0 || !locations.TryGetValue(statements[0], out var first))
                return false;
            var previous = -1;
            foreach (var statement in statements)
            {
                if (!locations.TryGetValue(statement, out var location) ||
                    !ReferenceEquals(location.Block, first.Block) ||
                    location.Index <= previous)
                    return false;
                previous = location.Index;
            }
            return true;
        }

        internal IEnumerable<SlangStatement> Ancestors(SlangStatement statement)
        {
            for (var parent = locations[statement].Parent; parent is not null; parent = locations[parent].Parent)
                yield return parent;
        }

        internal SlangDoOnce? NearestDoOnce(SlangStatement statement) =>
            Ancestors(statement).OfType<SlangDoOnce>().FirstOrDefault();

        private void Collect(SlangBlock block, SlangStatement? parent, string context)
        {
            foreach (var (index, statement) in block.Statements.Index())
            {
                if (!locations.TryAdd(statement, new(block, index, parent)))
                    throw Error(context, "one target statement object appears at multiple AST locations");
                switch (statement)
                {
                    case SlangScope scope:
                        Collect(scope.Body, statement, context);
                        break;
                    case SlangIf conditional:
                        Collect(conditional.WhenTrue, statement, context);
                        Collect(conditional.WhenFalse, statement, context);
                        break;
                    case SlangDoOnce once:
                        Collect(once.Body, statement, context);
                        break;
                    case SlangLoop loop:
                        Collect(loop.Body, statement, context);
                        break;
                }
            }
        }

        private sealed record Location(SlangBlock Block, int Index, SlangStatement? Parent);
    }

    private readonly record struct TargetState(
        ImmutableHashSet<IShaderValue> Values,
        ImmutableHashSet<VariableDeclaration> Variables,
        int? Token)
    {
        internal static TargetState Empty { get; } = new(
            ImmutableHashSet.Create<IShaderValue>(ReferenceEqualityComparer.Instance),
            ImmutableHashSet.Create<VariableDeclaration>(ReferenceEqualityComparer.Instance),
            null);

        internal TargetState Define(IShaderValue value) => this with { Values = Values.Add(value) };

        internal TargetState Assign(
            SlangPlace target,
            SlangOperand value,
            VariableDeclaration? token)
        {
            if (target is not SlangVariablePlace variable)
                return this;
            var tokenValue = ReferenceEquals(variable.Variable, token) && TryInt(value, out var id)
                ? id
                : Token;
            return this with
            {
                Variables = Variables.Add(variable.Variable),
                Token = tokenValue
            };
        }
    }

    private readonly record struct Flow(
        ImmutableArray<TargetState> Normal,
        ImmutableArray<TargetState> Breaks)
    {
        internal static Flow Empty { get; } = new([], []);
        internal static Flow FromNormal(ImmutableArray<TargetState> states) => new(states, []);
        internal static Flow Merge(Flow left, Flow right) =>
            new(
                Normalize([.. left.Normal, .. right.Normal]),
                Normalize([.. left.Breaks, .. right.Breaks]));
    }
}
