using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
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
        var moduleStorage = source.Declarations.OfType<VariableDeclaration>()
            .ToImmutableHashSet<VariableDeclaration>(ReferenceEqualityComparer.Instance);
        foreach (var participation in facts.EntryUniformQuadParticipations)
            foreach (var (function, labels) in participation.OriginalBlocks)
                VerifyFunction(
                    participation,
                    source.FunctionDefinitions[function],
                    target.FunctionDefinitions[function],
                    labels,
                    moduleStorage);
    }

    private static void VerifyFunction(
        EntryUniformQuadParticipation participation,
        RegionFunctionBody source,
        SlangFunctionBody target,
        ImmutableArray<Label> labels,
        ImmutableHashSet<VariableDeclaration> moduleStorage)
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
        VerifyOriginalTransfers(
            participation.Uniformity[source.Declaration],
            source,
            context);
        var activeStorage = VerifyOriginalStorage(
            participation.Uniformity[source.Declaration],
            source,
            moduleStorage,
            context);
        VerifyActivationTemplate(source, target, origins, context);
        VerifyCarrierPartition(source, origins, tree, context);
        VerifyReturns(
            participation.Uniformity[source.Declaration],
            source,
            origins,
            tree,
            context);
        VerifyBreaks(origins, tree, context);
        VerifyOriginTables(source, origins, tree, context);
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
        VerifyTerminalTemplates(source, origins, tree, context);
        VerifyRelevantSites(participation, source, origins, tree, context);
        VerifyClosedWorld(origins, tree, context);
        VerifyControlDataflow(
            activeStorage,
            target,
            origins,
            context);
    }

    private static void VerifyOriginalTransfers(
        CooperationFunctionUniformityFacts facts,
        RegionFunctionBody source,
        string context)
    {
        if (source.Control.Transfers.Length != facts.OriginalTransfers.Length)
            throw Error(context, "original transfer count changed before target verification");
        foreach (var transfer in facts.OriginalTransfers)
            if (!CooperationUniformity.CooperationTransferFacts.Matches(
                    source,
                    transfer,
                    allowPointerParameterErasure: true))
                throw Error(
                    context,
                    $"original transfer from block '{transfer.Source.Name}', arm {transfer.Arm} " +
                    "changed target/owner/kind/arguments");
    }

    private static ImmutableHashSet<VariableDeclaration> VerifyOriginalStorage(
        CooperationFunctionUniformityFacts facts,
        RegionFunctionBody source,
        ImmutableHashSet<VariableDeclaration> moduleStorage,
        string context)
    {
        var actual = source.UsedValues()
            .OfType<VariablePointerValue>()
            .Select(static value => value.Declaration)
            .Where(static variable => variable.AddressSpace is not FunctionAddressSpace)
            .ToImmutableHashSet<VariableDeclaration>(ReferenceEqualityComparer.Instance);
        var whitelist = facts.OriginalNonFunctionStorage.ToImmutableHashSet<VariableDeclaration>(
            ReferenceEqualityComparer.Instance);
        if (facts.OriginalNonFunctionStorage.Any(variable =>
                variable.AddressSpace is FunctionAddressSpace ||
                !moduleStorage.Contains(variable)) ||
            actual.Any(variable =>
                !moduleStorage.Contains(variable) ||
                !whitelist.Contains(variable)))
            throw Error(context, "original non-function storage roots changed before target verification");
        return actual;
    }

    private static void VerifyActivationTemplate(
        RegionFunctionBody source,
        SlangFunctionBody target,
        SlangLoweringOrigins origins,
        string context)
    {
        var expected = Activation(source.Body);
        if (!expected.Escapes.IsEmpty)
            throw Error(context, "source-derived activation template has unresolved transfers");
        var declarations = target.Body.Statements.TakeWhile(static statement => statement is SlangDeclare).Count();
        if (target.Body.Statements.Skip(declarations).Any(static statement => statement is SlangDeclare))
            throw Error(context, "activation template contains a misplaced declaration");
        MatchBlock(
            expected.Body,
            new SlangBlock([.. target.Body.Statements.Skip(declarations)]),
            context);
        return;

        Template Activation(RegionTree<Label, ShaderRegionBody> region)
        {
            if (region.Definition.Kind is RegionKind.Loop)
                throw Error(context, "activation template does not admit loops");
            var suffix = Raw(region);
            var bindings = region.Bindings.ToImmutableArray();
            var innermost = true;
            for (var index = bindings.Length - 1; index >= 0; index--)
            {
                var child = bindings[index];
                var caught = new SlangContinuationOrigin(
                    child.Label,
                    region.Label,
                    ScopedContinuationKind.Forward);
                if (!suffix.Escapes.Contains(caught))
                    throw Error(context, "activation template cannot consume its checked continuation");

                var statements = ImmutableArray.CreateBuilder<SlangStatement>();
                if (innermost)
                {
                    statements.AddRange(suffix.Body.Statements);
                    innermost = false;
                }
                else
                {
                    var carrier = suffix.Body.Statements.ToBuilder();
                    if (!suffix.Escapes.IsEmpty)
                    {
                        var carrierOrigin = Single(
                            origins.CarrierBreaks,
                            origin => ReferenceEquals(origin.Owner, region.Label) &&
                                      ReferenceEquals(origin.NextBinding, child.Label),
                            context,
                            "source-derived carrier exit");
                        carrier.Add(carrierOrigin.Break);
                    }
                    statements.Add(new SlangDoOnce(new SlangBlock(carrier.ToImmutable())));
                }

                var childTemplate = Activation(child);
                if (suffix.Escapes.Count == 1)
                {
                    statements.AddRange(childTemplate.Body.Statements);
                }
                else
                {
                    var gate = Single(
                        origins.Gates,
                        origin => Equals(origin.Continuation, caught),
                        context,
                        "source-derived continuation gate");
                    statements.Add(gate.Comparison);
                    statements.Add(new SlangIf(
                        gate.Conditional.Condition,
                        childTemplate.Body,
                        SlangBlock.Empty));
                }

                suffix = new(
                    new SlangBlock(statements.ToImmutable()),
                    suffix.Escapes.Remove(caught).Union(childTemplate.Escapes));
            }
            return suffix;
        }

        Template Raw(RegionTree<Label, ShaderRegionBody> region)
        {
            var statements = ImmutableArray.CreateBuilder<SlangStatement>();
            foreach (var parameter in region.Body.Parameters)
            {
                var origin = Single(
                    origins.Parameters,
                    item => ReferenceEquals(item.Label, region.Label) &&
                            ReferenceEquals(item.Parameter, parameter),
                    context,
                    "source-derived parameter load");
                statements.Add(origin.Definition);
                if (origin.Capture is not null)
                    statements.Add(origin.Capture);
            }

            foreach (var (ordinal, instruction) in region.Body.Body.Elements.Index())
            {
                var instructionOrigins = origins.Instructions.Where(item =>
                        ReferenceEquals(item.Label, region.Label) &&
                        item.InstructionOrdinal == ordinal)
                    .Take(2)
                    .ToArray();
                var dimensionOrigins = origins.Dimensions.Where(item =>
                        ReferenceEquals(item.Label, region.Label) &&
                        item.InstructionOrdinal == ordinal)
                    .Take(2)
                    .ToArray();
                if (instructionOrigins.Length > 1 || dimensionOrigins.Length > 1)
                    throw Error(context, "activation template has duplicate source instruction origins");
                var origin = instructionOrigins.SingleOrDefault();
                var dimensions = dimensionOrigins.SingleOrDefault();
                if (origin is not null && dimensions is not null)
                    throw Error(context, "activation template has multiple origins for one source instruction");
                if (origin is null && dimensions is null)
                {
                    if (instruction.Operation is AddressOfMemberOperation or AddressOfVecComponentOperation)
                        continue;
                    throw Error(context, "activation template is missing a source instruction");
                }
                if (dimensions is not null)
                {
                    statements.Add(dimensions.Dimensions);
                    if (dimensions.Capture is not null)
                        statements.Add(dimensions.Capture);
                }
                else
                {
                    statements.Add(origin!.Target);
                    var definition = origins.Definitions.SingleOrDefault(item =>
                        ReferenceEquals(item.Label, region.Label) &&
                        item.InstructionOrdinal == ordinal);
                    if (definition?.Capture is not null)
                        statements.Add(definition.Capture);
                }
            }

            ImmutableHashSet<SlangContinuationOrigin> escapes;
            switch (region.Body.Body.Last)
            {
                case Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>:
                case Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>:
                    statements.Add(Single(
                        origins.Returns,
                        item => ReferenceEquals(item.Label, region.Label),
                        context,
                        "source-derived return").Return);
                    escapes = ImmutableHashSet<SlangContinuationOrigin>.Empty;
                    break;
                case Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>:
                    var transfer = Transfer(region.Label, 0);
                    statements.AddRange(TransferStatements(transfer));
                    escapes = ImmutableHashSet.Create(transfer.Continuation);
                    break;
                case Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>:
                    var whenTrue = Transfer(region.Label, 0);
                    var whenFalse = Transfer(region.Label, 1);
                    var conditional = Single(
                        origins.Conditionals,
                        item => ReferenceEquals(item.Source, region.Label),
                        context,
                        "source-derived conditional");
                    statements.Add(new SlangIf(
                        conditional.Conditional.Condition,
                        new SlangBlock(TransferStatements(whenTrue)),
                        new SlangBlock(TransferStatements(whenFalse))));
                    escapes = ImmutableHashSet.Create(whenTrue.Continuation, whenFalse.Continuation);
                    break;
                default:
                    throw Error(context, "activation template encountered unsupported source control");
            }

            return new(
                new SlangBlock(
                [
                    new SlangDoOnce(new SlangBlock(
                    [
                        new SlangScope(region.Label, new SlangBlock(statements.ToImmutable()))
                    ]))
                ]),
                escapes);
        }

        SlangTransferOrigin Transfer(Label sourceLabel, int arm) =>
            Single(
                origins.Transfers,
                item => ReferenceEquals(item.Source, sourceLabel) && item.Arm == arm,
                context,
                "source-derived transfer");

        static ImmutableArray<SlangStatement> TransferStatements(SlangTransferOrigin transfer) =>
        [
            .. transfer.Arguments.Select(static argument => (SlangStatement)argument.Definition),
            .. transfer.Arguments.Select(static argument => (SlangStatement)argument.Assignment),
            transfer.TokenAssignment,
            transfer.Break
        ];
    }

    private static void MatchBlock(SlangBlock expected, SlangBlock actual, string context)
    {
        if (expected.Statements.Length != actual.Statements.Length)
            throw Error(context, "actual AST does not match the source-derived activation template");
        foreach (var (left, right) in expected.Statements.Zip(actual.Statements))
        {
            switch (left, right)
            {
                case (SlangScope expectedScope, SlangScope actualScope)
                    when ReferenceEquals(expectedScope.OriginalLabel, actualScope.OriginalLabel):
                    MatchBlock(expectedScope.Body, actualScope.Body, context);
                    break;
                case (SlangDoOnce expectedOnce, SlangDoOnce actualOnce):
                    MatchBlock(expectedOnce.Body, actualOnce.Body, context);
                    break;
                case (SlangIf expectedIf, SlangIf actualIf)
                    when Equals(expectedIf.Condition, actualIf.Condition):
                    MatchBlock(expectedIf.WhenTrue, actualIf.WhenTrue, context);
                    MatchBlock(expectedIf.WhenFalse, actualIf.WhenFalse, context);
                    break;
                default:
                    if (!ReferenceEquals(left, right))
                        throw Error(context, "actual AST does not match the source-derived activation template");
                    break;
            }
        }
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
            if (!ReferenceEquals(sourceInstruction.Operation, fact.Operation) ||
                !ReferenceEquals(sourceInstruction.Result, fact.Value) ||
                !ReferenceEquals(sourceInstruction.Payload, fact.Payload) ||
                !sourceInstruction.Operands.SequenceEqual(
                    fact.Operands,
                    ReferenceEqualityComparer.Instance) ||
                (fact.Callee is not null &&
                 (sourceInstruction.OperandCount == 0 ||
                  !ReferenceEquals(sourceInstruction[0], fact.Callee))))
                throw Error(
                    context,
                    $"uniform fact for block '{fact.Label.Name}', instruction {ordinal} " +
                    "does not match the analyzed source definition");
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

        foreach (var returned in facts.UniformReturns.Where(static returned => returned.IsUniform))
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

    private static void VerifyOriginTables(
        RegionFunctionBody source,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var parameters = new Dictionary<IShaderValue, Label>(ReferenceEqualityComparer.Instance);
        source.Body.Traverse(region =>
        {
            foreach (var parameter in region.Body.Parameters)
                parameters.Add(parameter, region.Label);
        });
        if (origins.Parameters.Length != parameters.Count)
            throw Error(context, "block-parameter origin count does not match the source");
        foreach (var origin in origins.Parameters)
        {
            if (!parameters.TryGetValue(origin.Parameter, out var label) ||
                !ReferenceEquals(label, origin.Label))
                throw Error(context, "block-parameter origin does not match its source definition");
            RequirePresent(tree, origin.Definition, context);
            RequireSourceScope(tree, origin.Definition, origin.Label, context);
            VerifyParameterOrigin(origin, origins, tree, context);
        }
        if (origins.Parameters.Select(static origin => origin.Parameter)
                .Distinct(ReferenceEqualityComparer.Instance).Count() != origins.Parameters.Length)
            throw Error(context, "block parameter has multiple target origins");

        var definitionSites = new HashSet<(Label Label, int Ordinal)>();
        var definitionTargets = new HashSet<SlangBind>(ReferenceEqualityComparer.Instance);
        foreach (var origin in origins.Definitions)
        {
            if (!definitionSites.Add((origin.Label, origin.InstructionOrdinal)) ||
                !definitionTargets.Add(origin.Definition))
                throw Error(context, "source definition has multiple target origins");
            var sourceInstruction = SourceInstruction(source, origin.Label, origin.InstructionOrdinal, context);
            if (!origin.Source.Equals(sourceInstruction))
                throw Error(context, "definition origin does not match its source instruction");
            RequirePresent(tree, origin.Definition, context);
            RequireSourceScope(tree, origin.Definition, origin.Label, context);
            var targetInstruction = origin.Definition.Instruction;
            if (!ReferenceEquals(targetInstruction.Operation, sourceInstruction.Operation) ||
                !ReferenceEquals(targetInstruction.Result, sourceInstruction.Result) ||
                !ReferenceEquals(targetInstruction.Payload, sourceInstruction.Payload))
                throw Error(context, "definition origin changed its source operation/result/operand lineage");
            VerifyCapture(
                sourceInstruction.Result ??
                throw Error(context, "definition origin source has no result"),
                origin.Definition,
                origin.Capture,
                origins,
                tree,
                context);
        }

        var addressDefinitions = SourceDefinitions(source);
        var sourceValues = SourceValues(source, addressDefinitions);
        var carrierValues = CarrierValues(origins);
        var dimensionSites = new HashSet<(Label Label, int Ordinal)>();
        var dimensionTargets = new HashSet<SlangGetDimensions>(ReferenceEqualityComparer.Instance);
        foreach (var origin in origins.Dimensions)
        {
            if (!dimensionSites.Add((origin.Label, origin.InstructionOrdinal)) ||
                !dimensionTargets.Add(origin.Dimensions))
                throw Error(context, "source dimensions operation has multiple target origins");
            var sourceInstruction = SourceInstruction(source, origin.Label, origin.InstructionOrdinal, context);
            if (!origin.Source.Equals(sourceInstruction))
                throw Error(context, "dimensions origin does not match its source instruction");
            RequirePresent(tree, origin.Dimensions, context);
            RequireSourceScope(tree, origin.Dimensions, origin.Label, context);
            VerifyDimensionsOrigin(
                source,
                sourceInstruction,
                origin,
                addressDefinitions,
                sourceValues,
                carrierValues,
                origins,
                tree,
                context);
        }

        var instructionSites = new HashSet<(Label Label, int Ordinal)>();
        var instructionTargets = new HashSet<SlangStatement>(ReferenceEqualityComparer.Instance);
        foreach (var origin in origins.Instructions)
        {
            if (!instructionSites.Add((origin.Label, origin.InstructionOrdinal)) ||
                !instructionTargets.Add(origin.Target) ||
                dimensionSites.Contains((origin.Label, origin.InstructionOrdinal)) ||
                origin.Target is SlangGetDimensions)
                throw Error(context, "source instruction has multiple target origins");
            var sourceInstruction = SourceInstruction(source, origin.Label, origin.InstructionOrdinal, context);
            if (!origin.Source.Equals(sourceInstruction))
                throw Error(context, "instruction origin does not match its source instruction");
            RequirePresent(tree, origin.Target, context);
            RequireSourceScope(tree, origin.Target, origin.Label, context);
            switch (origin.Target)
            {
                case SlangBind binding:
                    VerifyTargetInstruction(
                        sourceInstruction,
                        binding.Instruction,
                        addressDefinitions,
                        origins,
                        context);
                    break;
                case SlangEffect effect:
                    VerifyTargetInstruction(
                        sourceInstruction,
                        effect.Instruction,
                        addressDefinitions,
                        origins,
                        context);
                    break;
                case SlangAssign assignment:
                    if (!SourceAssignmentMatches(
                            sourceInstruction,
                            assignment,
                            addressDefinitions,
                            origins))
                        throw Error(
                            context,
                            "source store/setter changed its typed target/value lineage");
                    break;
                default:
                    throw Error(context, "instruction origin has the wrong target statement kind");
            }
        }

        var sourceCalls = ImmutableArray.CreateBuilder<(Label Label, int Ordinal)>();
        source.Body.Traverse((_, label, block) =>
        {
            foreach (var (ordinal, instruction) in block.Body.Elements.Index())
                if (instruction.Operation is CallOperation)
                    sourceCalls.Add((label, ordinal));
            return false;
        });
        foreach (var call in sourceCalls)
            if (!instructionSites.Contains(call))
                throw Error(context, "source call is missing its exact target instruction origin");

        var sourceDimensions = ImmutableArray.CreateBuilder<(Label Label, int Ordinal)>();
        source.Body.Traverse((_, label, block) =>
        {
            foreach (var (ordinal, instruction) in block.Body.Elements.Index())
                if (instruction.Operation is StructuredBufferLengthOperation)
                    sourceDimensions.Add((label, ordinal));
            return false;
        });
        if (!dimensionSites.SetEquals(sourceDimensions))
            throw Error(context, "source dimensions operation count does not match its target origins");

        foreach (var definition in origins.Definitions)
        {
            var instruction = origins.Instructions.SingleOrDefault(origin =>
                ReferenceEquals(origin.Label, definition.Label) &&
                origin.InstructionOrdinal == definition.InstructionOrdinal);
            if (instruction is null || !ReferenceEquals(instruction.Target, definition.Definition))
                throw Error(context, "definition and instruction origins disagree");
        }
    }

    private static void VerifyTargetInstruction(
        Instruction<IShaderValue, IShaderValue> source,
        Instruction<SlangOperand, IShaderValue> target,
        IReadOnlyDictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>> addressDefinitions,
        SlangLoweringOrigins origins,
        string context,
        bool requireOperands = false)
    {
        if (!ReferenceEquals(target.Operation, source.Operation) ||
            !ReferenceEquals(target.Result, source.Result) ||
            !ReferenceEquals(target.Payload, source.Payload) ||
            (requireOperands ||
             source.Operation is CallOperation or StructuredBufferLoadOperation) &&
            !OperandsMatch(source.Operands, target.Operands, origins, addressDefinitions))
            throw Error(context, "instruction origin changed its source operation/result/operand lineage");
    }

    private static void VerifyDimensionsOrigin(
        RegionFunctionBody sourceBody,
        Instruction<IShaderValue, IShaderValue> source,
        SlangDimensionsOrigin origin,
        IReadOnlyDictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>> addressDefinitions,
        IReadOnlySet<IShaderValue> sourceValues,
        IReadOnlySet<IShaderValue> carrierValues,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var dimensions = origin.Dimensions;
        if (source.Operation is not StructuredBufferLengthOperation ||
            source.OperandCount != 1 ||
            source.Result is null ||
            !ReferenceEquals(origin.Source.Operation, source.Operation) ||
            !ReferenceEquals(origin.Source.Result, source.Result) ||
            !ReferenceEquals(origin.Source.Payload, source.Payload) ||
            !origin.Source.Operands.SequenceEqual(
                source.Operands,
                ReferenceEqualityComparer.Instance) ||
            dimensions.Buffer is not SlangPlaceOperand buffer ||
            !PlaceMatches(SourcePlace(source.Operand0!, addressDefinitions), buffer.Place) ||
            !ReferenceEquals(dimensions.Count, source.Result))
            throw Error(context, "dimensions origin changed its source operation/result/operand lineage");

        if (dimensions.Stride is not IntermediateValue ||
            !dimensions.Stride.Type.Equals(ShaderType.U32) ||
            ReferenceEquals(dimensions.Stride, dimensions.Count) ||
            sourceValues.Contains(dimensions.Stride) ||
            carrierValues.Contains(dimensions.Stride))
            throw Error(context, "dimensions stride is not a fresh writable u32 intermediate");

        VerifyCapture(
            source.Result,
            dimensions,
            origin.Capture,
            origins,
            tree,
            context);

        sourceBody.Body.Traverse((_, label, block) =>
        {
            foreach (var (ordinal, instruction) in block.Body.Elements.Index())
            {
                if (!instruction.Operands.Any(operand => ReferenceEquals(operand, source.Result)))
                    continue;
                var consumer = Single(
                    origins.Instructions,
                    item => ReferenceEquals(item.Label, label) &&
                            item.InstructionOrdinal == ordinal &&
                            item.Source.Equals(instruction),
                    context,
                    "dimensions count consumer origin");
                RequirePresent(tree, consumer.Target, context);
                RequireSourceScope(tree, consumer.Target, label, context);
                switch (consumer.Target)
                {
                    case SlangBind binding:
                        VerifyTargetInstruction(
                            instruction,
                            binding.Instruction,
                            addressDefinitions,
                            origins,
                            context,
                            requireOperands: true);
                        break;
                    case SlangEffect effect:
                        VerifyTargetInstruction(
                            instruction,
                            effect.Instruction,
                            addressDefinitions,
                            origins,
                            context,
                            requireOperands: true);
                        break;
                    case SlangAssign assignment when SourceAssignmentMatches(
                        instruction,
                        assignment,
                        addressDefinitions,
                        origins):
                        break;
                    default:
                        throw Error(context, "dimensions count consumer changed its source operand lineage");
                }
            }
            return false;
        });
    }

    private static bool SourceAssignmentMatches(
        Instruction<IShaderValue, IShaderValue> source,
        SlangAssign target,
        IReadOnlyDictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>> addressDefinitions,
        SlangLoweringOrigins origins) =>
        source.Operation switch
        {
            StoreOperation =>
                PlaceMatches(SourcePlace(source.Operand0!, addressDefinitions), target.Target) &&
                OperandMatches(source.Operand1!, target.Value, origins),
            IVectorComponentSetOperation component =>
                PlaceMatches(
                    new SlangComponentPlace(
                        SourcePlace(source.Operand0!, addressDefinitions),
                        component.Component.Name,
                        component.ElementType),
                    target.Target) &&
                OperandMatches(source.Operand1!, target.Value, origins),
            IVectorSwizzleSetOperation swizzle =>
                PlaceMatches(
                    new SlangSwizzlePlace(
                        SourcePlace(source.Operand0!, addressDefinitions),
                        swizzle.Pattern.Name,
                        swizzle.ValueVecType),
                    target.Target) &&
                OperandMatches(source.Operand1!, target.Value, origins),
            _ => false
        };

    private static SlangPlace SourcePlace(
        IShaderValue value,
        IReadOnlyDictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>> addressDefinitions) =>
        value switch
        {
            VariablePointerValue variable => new SlangVariablePlace(variable.Declaration),
            ParameterPointerValue parameter => new SlangParameterPlace(parameter.Declaration),
            _ when addressDefinitions.TryGetValue(value, out var definition) =>
                definition.Operation switch
                {
                    AddressOfMemberOperation member =>
                        new SlangMemberPlace(
                            SourcePlace(definition.Operand0!, addressDefinitions),
                            member.Member),
                    AddressOfVecComponentOperation component =>
                        new SlangComponentPlace(
                            SourcePlace(definition.Operand0!, addressDefinitions),
                            component.Component.Name,
                            component.Target.ElementType),
                    _ => throw new NotSupportedException(
                        $"PortableWgsl cannot derive source place for '{value}'.")
                },
            _ => throw new NotSupportedException(
                $"PortableWgsl cannot derive source place for '{value}'.")
        };

    private static bool PlaceMatches(SlangPlace expected, SlangPlace actual) =>
        (expected, actual) switch
        {
            (SlangVariablePlace left, SlangVariablePlace right) =>
                ReferenceEquals(left.Variable, right.Variable),
            (SlangParameterPlace left, SlangParameterPlace right) =>
                ReferenceEquals(left.Parameter, right.Parameter),
            (SlangMemberPlace left, SlangMemberPlace right) =>
                ReferenceEquals(left.Member, right.Member) &&
                PlaceMatches(left.Target, right.Target),
            (SlangComponentPlace left, SlangComponentPlace right) =>
                left.Component == right.Component &&
                Equals(left.ComponentType, right.ComponentType) &&
                PlaceMatches(left.Target, right.Target),
            (SlangSwizzlePlace left, SlangSwizzlePlace right) =>
                left.Pattern == right.Pattern &&
                Equals(left.SwizzleType, right.SwizzleType) &&
                PlaceMatches(left.Target, right.Target),
            _ => false
        };

    private static Dictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>> SourceDefinitions(
        RegionFunctionBody source)
    {
        var result = new Dictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>>(
            ReferenceEqualityComparer.Instance);
        source.Body.Traverse(region =>
        {
            foreach (var instruction in region.Body.Body.Elements)
                if (instruction.Result is { } value)
                    result.Add(value, instruction);
        });
        return result;
    }

    private static HashSet<IShaderValue> SourceValues(
        RegionFunctionBody source,
        IReadOnlyDictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>> definitions)
    {
        var result = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
        result.UnionWith(source.UsedValues().OfType<IShaderValue>());
        result.UnionWith(source.Declaration.Parameters.Select(static parameter => parameter.Value));
        result.UnionWith(definitions.Keys);
        source.Body.Traverse(region =>
        {
            result.UnionWith(region.Body.Parameters);
        });
        return result;
    }

    private static HashSet<IShaderValue> CarrierValues(SlangLoweringOrigins origins)
    {
        var result = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
        result.UnionWith(origins.ParameterSlots.Values.Select(static variable => variable.Value));
        result.UnionWith(origins.Captures.Values.Select(static variable => variable.Value));
        if (origins.ControlToken is { } token)
            result.Add(token.Value);
        return result;
    }

    private static Instruction<IShaderValue, IShaderValue> SourceInstruction(
        RegionFunctionBody source,
        Label label,
        int ordinal,
        string context)
    {
        if (!source.Labels.Contains(label, ReferenceEqualityComparer.Instance) ||
            ordinal < 0 ||
            ordinal >= source[label].Body.Elements.Count())
            throw Error(context, "origin references a missing source instruction");
        return source[label].Body.Elements.ElementAt(ordinal);
    }

    private static void VerifyReturns(
        CooperationFunctionUniformityFacts facts,
        RegionFunctionBody source,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        if (origins.Returns.Length != facts.UniformReturns.Length)
            throw Error(context, "target return origin count does not match source return terminators");
        foreach (var fact in facts.UniformReturns)
        {
            var origin = Single(
                origins.Returns,
                item => ReferenceEquals(item.Label, fact.Label),
                context,
                "source return origin");
            RequirePresent(tree, origin.Return, context);
            if (!ReferenceEquals(origin.Value, fact.Value))
                throw Error(context, "target return origin changed its source value");
            switch (source[fact.Label].Body.Last, origin.Return)
            {
                case (Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>, SlangReturnVoid):
                    break;
                case (
                    Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned,
                    SlangReturnValue targetReturn)
                    when ReferenceEquals(returned.Expr, fact.Value) &&
                         OperandMatches(returned.Expr, targetReturn.Value, origins):
                    break;
                default:
                    throw Error(context, "target return does not preserve its source terminator/value");
            }
            var sourceScope = tree.Ancestors(origin.Return).OfType<SlangScope>().FirstOrDefault();
            if (sourceScope is null || !ReferenceEquals(sourceScope.OriginalLabel, fact.Label))
                throw Error(context, "target return moved outside its original terminal source scope");
        }

        var actual = tree.Statements.Where(static statement =>
                statement is SlangReturnValue or SlangReturnVoid)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        var accounted = origins.Returns.Select(static origin => origin.Return)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        if (!actual.SetEquals(accounted))
            throw Error(context, "target contains an unaccounted return");
    }

    private static void VerifyBreaks(
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        foreach (var origin in origins.CarrierBreaks)
        {
            RequirePresent(tree, origin.Carrier, context);
            RequirePresent(tree, origin.Break, context);
            if (!ReferenceEquals(tree.NearestDoOnce(origin.Break), origin.Carrier) ||
                origin.Carrier.Body.Statements.IsEmpty ||
                !ReferenceEquals(origin.Carrier.Body.Statements[^1], origin.Break) ||
                !Descendants(origin.Carrier.Body).Any(statement =>
                    statement is SlangScope { OriginalLabel: var label } &&
                    ReferenceEquals(label, origin.Owner)))
                throw Error(context, "synthetic carrier break has the wrong parent/position/owner");
        }

        var actual = tree.Statements.OfType<SlangBreak>()
            .ToHashSet(ReferenceEqualityComparer.Instance);
        var accounted = origins.Transfers.Select(static origin => origin.Break)
            .Concat(origins.CarrierBreaks.Select(static origin => origin.Break))
            .ToHashSet(ReferenceEqualityComparer.Instance);
        if (!actual.SetEquals(accounted))
            throw Error(context, "target contains an unaccounted break");
    }

    private static void VerifyCarrierPartition(
        RegionFunctionBody source,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var parameterBuilder = ImmutableHashSet.CreateBuilder<IShaderValue>(
            ReferenceEqualityComparer.Instance);
        source.Body.Traverse(region =>
        {
            parameterBuilder.UnionWith(region.Body.Parameters);
        });
        var parameters = parameterBuilder.ToImmutable();
        if (origins.ParameterSlots.Count != parameters.Count ||
            !origins.ParameterSlots.Keys.ToHashSet(ReferenceEqualityComparer.Instance).SetEquals(parameters))
            throw Error(context, "parameter-slot map does not match original block parameters");

        var slots = origins.ParameterSlots.Values.ToArray();
        var captures = origins.Captures.Values.ToArray();
        if (slots.Distinct(ReferenceEqualityComparer.Instance).Count() != slots.Length ||
            captures.Distinct(ReferenceEqualityComparer.Instance).Count() != captures.Length)
            throw Error(context, "controlled parameter/capture slots are not injective");

        var generated = slots.Concat(captures)
            .Concat(origins.ControlToken is null ? [] : [origins.ControlToken])
            .ToArray();
        if (generated.Distinct(ReferenceEqualityComparer.Instance).Count() != generated.Length)
            throw Error(context, "parameter slots, capture slots, and control token are not disjoint");
        var originalStorage = source.LocalVariables
            .Concat(source.UsedValues()
                .OfType<VariablePointerValue>()
                .Select(static value => value.Declaration))
            .ToHashSet(ReferenceEqualityComparer.Instance);
        if (generated.Any(originalStorage.Contains))
            throw Error(context, "generated control carrier aliases original user storage");

        var declarations = tree.Statements.OfType<SlangDeclare>().ToArray();
        if (generated.Any(variable => declarations.Count(statement =>
                ReferenceEquals(statement.Variable, variable)) != 1))
            throw Error(context, "generated control carrier is not declared exactly once in the target AST");
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
        SlangStatement definition,
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

    private static void VerifyTerminalTemplates(
        RegionFunctionBody source,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        foreach (var label in source.Labels)
        {
            var scope = Single(
                tree.Statements.OfType<SlangScope>(),
                item => ReferenceEquals(item.OriginalLabel, label),
                context,
                "source-label scope");
            switch (source[label].Body.Last)
            {
                case Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>:
                case Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>:
                    var returned = Single(
                        origins.Returns,
                        origin => ReferenceEquals(origin.Label, label),
                        context,
                        "terminal return origin");
                    if (!tree.IsLastDirect(returned.Return, scope.Body))
                        throw Error(context, $"source block '{label.Name}' return is not terminal");
                    break;
                case Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>:
                    var transfer = Single(
                        origins.Transfers,
                        origin => ReferenceEquals(origin.Source, label) && origin.Arm == 0,
                        context,
                        "terminal transfer origin");
                    if (!tree.IsLastDirect(transfer.Break, scope.Body))
                        throw Error(context, $"source block '{label.Name}' transfer is not terminal");
                    break;
                case Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>:
                    var conditional = Single(
                        origins.Conditionals,
                        origin => ReferenceEquals(origin.Source, label),
                        context,
                        "terminal conditional origin");
                    if (!tree.IsLastDirect(conditional.Conditional, scope.Body))
                        throw Error(context, $"source block '{label.Name}' conditional is not terminal");
                    foreach (var arm in new[] { 0, 1 })
                    {
                        var armTransfer = Single(
                            origins.Transfers,
                            origin => ReferenceEquals(origin.Source, label) && origin.Arm == arm,
                            context,
                            $"terminal conditional arm {arm}");
                        var targetBlock = arm == 0
                            ? conditional.Conditional.WhenTrue
                            : conditional.Conditional.WhenFalse;
                        if (!tree.IsLastDirect(armTransfer.Break, targetBlock))
                            throw Error(
                                context,
                                $"source block '{label.Name}' conditional arm {arm} transfer is not terminal");
                    }
                    break;
                default:
                    throw Error(context, $"source block '{label.Name}' has an unsupported terminal template");
            }
        }
    }

    private static void VerifyControlledVariables(
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var allowedWrites = ControlledWrites(origins);
        var controlled = origins.ParameterSlots.Values
            .Concat(origins.Captures.Values)
            .Concat(origins.ControlToken is null ? [] : [origins.ControlToken])
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var assignment in tree.Statements.OfType<SlangAssign>())
            if (RootVariable(assignment.Target) is { } variable &&
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

    private static VariableDeclaration? RootVariable(SlangPlace place) =>
        place switch
        {
            SlangVariablePlace variable => variable.Variable,
            SlangMemberPlace member => RootVariable(member.Target),
            SlangComponentPlace component => RootVariable(component.Target),
            SlangSwizzlePlace swizzle => RootVariable(swizzle.Target),
            _ => null
        };

    private static void VerifyClosedWorld(
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var instructionTargets = origins.Instructions.Select(static origin => origin.Target)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        var allowedBinds = instructionTargets.OfType<SlangBind>()
            .Concat(origins.Parameters.Select(static origin => origin.Definition))
            .Concat(origins.Transfers.SelectMany(static origin => origin.Arguments)
                .Select(static origin => origin.Definition))
            .Concat(origins.Gates.Select(static origin => origin.Comparison))
            .ToHashSet(ReferenceEqualityComparer.Instance);
        var results = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
        foreach (var binding in tree.Statements.OfType<SlangBind>())
        {
            if (!allowedBinds.Contains(binding))
                throw Error(
                    context,
                    binding.Instruction.Operation is CallOperation
                        ? "target contains an unaccounted target call"
                        : "target contains an unaccounted target definition");
            if (!results.Add(binding.Instruction.Result!))
                throw Error(context, "target defines one SSA result more than once");
        }

        var allowedDimensions = origins.Dimensions.Select(static origin => origin.Dimensions)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var dimensions in tree.Statements.OfType<SlangGetDimensions>())
        {
            if (!allowedDimensions.Contains(dimensions))
                throw Error(context, "target contains an unaccounted dimensions operation");
            if (!results.Add(dimensions.Count) || !results.Add(dimensions.Stride))
                throw Error(context, "target defines one SSA result more than once");
        }

        var allowedEffects = instructionTargets.OfType<SlangEffect>()
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var effect in tree.Statements.OfType<SlangEffect>())
            if (!allowedEffects.Contains(effect))
                throw Error(context, "target contains an unaccounted effect");

        var allowedAssignments = instructionTargets.OfType<SlangAssign>()
            .Concat(ControlledWrites(origins))
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var assignment in tree.Statements.OfType<SlangAssign>())
            if (!allowedAssignments.Contains(assignment))
                throw Error(context, "target contains an unaccounted write");
    }

    private static HashSet<SlangAssign> ControlledWrites(SlangLoweringOrigins origins) =>
        origins.Transfers.Select(static item => item.TokenAssignment)
            .Concat(origins.Transfers.SelectMany(static item => item.Arguments)
                .Select(static item => item.Assignment))
            .Concat(origins.Parameters.Select(static item => item.Capture).OfType<SlangAssign>())
            .Concat(origins.Definitions.Select(static item => item.Capture).OfType<SlangAssign>())
            .Concat(origins.Dimensions.Select(static item => item.Capture).OfType<SlangAssign>())
            .ToHashSet<SlangAssign>(ReferenceEqualityComparer.Instance);

    private static void VerifyRelevantSites(
        EntryUniformQuadParticipation participation,
        RegionFunctionBody source,
        SlangLoweringOrigins origins,
        TargetTree tree,
        string context)
    {
        var addressDefinitions = SourceDefinitions(source);
        var sourceValues = SourceValues(source, addressDefinitions);
        var carrierValues = CarrierValues(origins);
        foreach (var fact in participation.OriginalRelevantInstructions.Where(
                     fact => ReferenceEquals(fact.Function, source.Declaration)))
        {
            var sourceInstruction = source[fact.Label].Body.Elements.ElementAt(fact.InstructionOrdinal);
            if (!ReferenceEquals(sourceInstruction.Operation, fact.Operation) ||
                !ReferenceEquals(sourceInstruction.Result, fact.Result) ||
                !ReferenceEquals(sourceInstruction.Payload, fact.Payload) ||
                !sourceInstruction.Operands.SequenceEqual(
                    fact.Operands,
                    ReferenceEqualityComparer.Instance))
                throw Error(
                    context,
                    $"relevant operation fact for block '{fact.Label.Name}', instruction " +
                    $"{fact.InstructionOrdinal} does not match the analyzed source");
            if (sourceInstruction.Operation is StructuredBufferLengthOperation)
            {
                var dimensions = Single(
                    origins.Dimensions,
                    item => ReferenceEquals(item.Label, fact.Label) &&
                            item.InstructionOrdinal == fact.InstructionOrdinal &&
                            item.Source.Equals(sourceInstruction),
                    context,
                    "relevant dimensions origin");
                RequirePresent(tree, dimensions.Dimensions, context);
                RequireSourceScope(tree, dimensions.Dimensions, fact.Label, context);
                VerifyDimensionsOrigin(
                    source,
                    sourceInstruction,
                    dimensions,
                    addressDefinitions,
                    sourceValues,
                    carrierValues,
                    origins,
                    tree,
                    context);
                continue;
            }
            var origin = Single(
                origins.Instructions,
                item => ReferenceEquals(item.Label, fact.Label) &&
                        item.InstructionOrdinal == fact.InstructionOrdinal &&
                        item.Source.Equals(sourceInstruction),
                context,
                "relevant operation origin");
            RequirePresent(tree, origin.Target, context);
            var targetInstruction = origin.Target switch
            {
                SlangBind binding => binding.Instruction,
                SlangEffect effect => effect.Instruction,
                _ => throw Error(context, "relevant operation origin is not a target instruction")
            };
            if (!ReferenceEquals(targetInstruction.Operation, sourceInstruction.Operation) ||
                !ReferenceEquals(targetInstruction.Result, sourceInstruction.Result) ||
                !ReferenceEquals(targetInstruction.Payload, sourceInstruction.Payload) ||
                !OperandsMatch(
                    sourceInstruction.Operands,
                    targetInstruction.Operands,
                    origins,
                    addressDefinitions))
                throw Error(
                    context,
                    $"relevant operation in block '{fact.Label.Name}', instruction " +
                    $"{fact.InstructionOrdinal} changed its callee/operand lineage");
            var sourceScope = tree.Ancestors(origin.Target).OfType<SlangScope>().FirstOrDefault();
            if (sourceScope is null || !ReferenceEquals(sourceScope.OriginalLabel, fact.Label))
                throw Error(
                    context,
                    $"relevant operation in block '{fact.Label.Name}', instruction " +
                    $"{fact.InstructionOrdinal} moved outside its original source-label scope");
        }
    }

    private static void VerifyControlDataflow(
        ImmutableHashSet<VariableDeclaration> activeStorage,
        SlangFunctionBody target,
        SlangLoweringOrigins origins,
        string context)
    {
        var gates = origins.Gates.ToDictionary<SlangGateOrigin, SlangIf, SlangGateOrigin>(
            static item => item.Conditional,
            static item => item,
            ReferenceEqualityComparer.Instance);
        var initial = ImmutableArray.Create(TargetState.WithVariables(activeStorage));
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
            case SlangGetDimensions dimensions:
                foreach (var state in input)
                    RequireDefined(dimensions.Buffer, state, context);
                return Flow.FromNormal(
                [
                    .. input.Select(state => state
                        .Define(dimensions.Count)
                        .Define(dimensions.Stride))
                ]);
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
        SlangLoweringOrigins origins,
        IReadOnlyDictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>>? addressDefinitions = null) =>
        source.Count() == target.Count() &&
        source.Zip(target).All(pair =>
            OperandMatches(pair.First, pair.Second, origins, addressDefinitions));

    private static bool OperandMatches(
        IShaderValue source,
        SlangOperand target,
        SlangLoweringOrigins origins,
        IReadOnlyDictionary<IShaderValue, Instruction<IShaderValue, IShaderValue>>? addressDefinitions = null)
    {
        if (source.Type is IPtrType &&
            addressDefinitions is not null &&
            target is SlangPlaceOperand place)
            return PlaceMatches(SourcePlace(source, addressDefinitions), place.Place);
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

    private static void RequireSourceScope(
        TargetTree tree,
        SlangStatement statement,
        Label label,
        string context)
    {
        var scope = tree.Ancestors(statement).OfType<SlangScope>().FirstOrDefault();
        if (scope is null || !ReferenceEquals(scope.OriginalLabel, label))
            throw Error(context, $"target instruction moved outside source-label scope '{label.Name}'");
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

        internal bool IsLastDirect(SlangStatement statement, SlangBlock block) =>
            locations.TryGetValue(statement, out var location) &&
            ReferenceEquals(location.Block, block) &&
            location.Index == block.Statements.Length - 1;

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

        internal static TargetState WithVariables(ImmutableHashSet<VariableDeclaration> variables) =>
            Empty with { Variables = variables };

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

    private readonly record struct Template(
        SlangBlock Body,
        ImmutableHashSet<SlangContinuationOrigin> Escapes);
}
