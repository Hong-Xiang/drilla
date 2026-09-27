using System.Collections.Immutable;
using DualDrill.CLSL.Frontend;
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
using DualDrill.CLSL.Reflection;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Backend;

internal enum SlangControlFlowPolicy
{
    Native,
    WgslCompatible
}

public sealed class SlangTargetLowering
{
    public ShaderModuleDeclaration<SlangFunctionBody> Lower(
        ShaderModuleDeclaration<RegionFunctionBody> module) =>
        Lower(module, SlangControlFlowPolicy.Native);

    internal ShaderModuleDeclaration<SlangFunctionBody> Lower(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        SlangControlFlowPolicy controlFlowPolicy)
    {
        ShaderModuleMetadataValidator.Validate(module);
        WgslUniformLayoutValidator.Validate(module);
        ValidateTextureSampleOperations(module);
        PortableDerivativeTarget.ValidateModuleBindings(module);
        var definitions = module.FunctionDefinitions.ToImmutableDictionary(
            definition => definition.Key,
            definition => new FunctionLowerer(definition.Value, controlFlowPolicy).Lower());
        var missing = module.Declarations.OfType<FunctionDeclaration>()
            .Where(declaration => !definitions.ContainsKey(declaration))
            .Select(declaration => declaration.Name)
            .ToArray();
        if (missing.Length > 0)
            throw new NotSupportedException(
                $"Slang target lowering requires bodies for functions: {string.Join(", ", missing)}.");
        var target = new ShaderModuleDeclaration<SlangFunctionBody>(module.Declarations, definitions);
        CooperationTargetVerifier.VerifyReadWriteResources(module, target);
        return target;
    }

    private static void ValidateTextureSampleOperations(ShaderModuleDeclaration<RegionFunctionBody> module)
    {
        foreach (var (function, body) in module.FunctionDefinitions)
            body.Body.Traverse(region =>
            {
                foreach (var instruction in region.Body.Body.Elements)
                    if (instruction.Operation is TextureSampleLevelOperation operation)
                        ValidateTextureSampleLevel(function, instruction, operation);
            });
    }

    private static void ValidateTextureSampleLevel(
        FunctionDeclaration function,
        Instruction<IShaderValue, IShaderValue> instruction,
        TextureSampleLevelOperation operation)
    {
        if (!HasPhysicalOperandShape(instruction, 4) ||
            instruction.Operand0!.Type is not IPtrType ||
            !instruction.Operand0.Type.Equals(operation.TexturePointerType) ||
            instruction.Operand1!.Type is not IPtrType ||
            !instruction.Operand1.Type.Equals(operation.SamplerPointerType) ||
            !instruction.RestOperands[0].Type.Equals(ShaderType.Vec2F32) ||
            !instruction.RestOperands[1].Type.Equals(ShaderType.F32) ||
            instruction.Result is null ||
            !instruction.Result.Type.Equals(ShaderType.Vec4F32))
            throw new NotSupportedException(
                $"Function '{function.Name}': operation '{instruction.Operation.Name}': " +
                "invalid texture SampleLevel signature.");
    }

    private static bool HasPhysicalOperandShape(
        Instruction<IShaderValue, IShaderValue> instruction,
        int expectedCount) =>
        instruction.OperandCount == expectedCount &&
        instruction.Operand0 is not null &&
        (expectedCount == 1
            ? instruction.Operand1 is null
            : instruction.Operand1 is not null) &&
        !instruction.RestOperands.IsDefault &&
        instruction.RestOperands.Length == Math.Max(0, expectedCount - 2) &&
        instruction.RestOperands.All(static operand => operand is not null);

    private sealed class FunctionLowerer
    {
        private readonly Dictionary<IShaderValue, SlangPlace> aliases =
            new(ReferenceEqualityComparer.Instance);
        private readonly List<RegionTree<Label, ShaderRegionBody>> blockOrder = [];
        private readonly Dictionary<Label, RegionTree<Label, ShaderRegionBody>> blocks = [];
        private readonly Dictionary<IShaderValue, VariableDeclaration> captures =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IShaderValue, VariableDeclaration> parameterSlots =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Continuation, int> tokenIds = [];
        private readonly ImmutableArray<SlangParameterOrigin>.Builder parameterOrigins =
            ImmutableArray.CreateBuilder<SlangParameterOrigin>();
        private readonly ImmutableArray<SlangDefinitionOrigin>.Builder definitionOrigins =
            ImmutableArray.CreateBuilder<SlangDefinitionOrigin>();
        private readonly ImmutableArray<SlangDimensionsOrigin>.Builder dimensionsOrigins =
            ImmutableArray.CreateBuilder<SlangDimensionsOrigin>();
        private readonly ImmutableArray<SlangInstructionOrigin>.Builder instructionOrigins =
            ImmutableArray.CreateBuilder<SlangInstructionOrigin>();
        private readonly ImmutableArray<SlangTransferOrigin>.Builder transferOrigins =
            ImmutableArray.CreateBuilder<SlangTransferOrigin>();
        private readonly ImmutableArray<SlangConditionalOrigin>.Builder conditionalOrigins =
            ImmutableArray.CreateBuilder<SlangConditionalOrigin>();
        private readonly ImmutableArray<SlangGateOrigin>.Builder gateOrigins =
            ImmutableArray.CreateBuilder<SlangGateOrigin>();
        private readonly ImmutableArray<SlangReturnOrigin>.Builder returnOrigins =
            ImmutableArray.CreateBuilder<SlangReturnOrigin>();
        private readonly ImmutableArray<SlangCarrierBreakOrigin>.Builder carrierBreakOrigins =
            ImmutableArray.CreateBuilder<SlangCarrierBreakOrigin>();
        private readonly RegionFunctionBody source;
        private readonly SlangControlFlowPolicy controlFlowPolicy;
        private readonly VariableDeclaration? token;
        private readonly VariableDeclaration? returnValue;

        internal FunctionLowerer(
            RegionFunctionBody source,
            SlangControlFlowPolicy controlFlowPolicy)
        {
            this.source = source;
            this.controlFlowPolicy = controlFlowPolicy;
            var resourceLocal = source.LocalVariables.FirstOrDefault(variable =>
                ShaderModuleMetadataValidator.IsResourceTypeOrPointer(variable.Type));
            if (resourceLocal is not null)
                throw Error(
                    $"local '{resourceLocal.Name}' has resource type '{resourceLocal.Type.Name}'; " +
                    "structured buffers are valid only as static shader-module fields");
            source.Body.Traverse(region =>
            {
                blocks.Add(region.Label, region);
                blockOrder.Add(region);
                foreach (var parameter in region.Body.Parameters)
                {
                    if (parameter.Type is IPtrType)
                        throw Error(
                            $"block '{region.Label.Name}' retains a pointer parameter; " +
                            "run stable pointer parameter lowering before Slang target lowering");
                    parameterSlots.Add(
                        parameter,
                        new VariableDeclaration(
                            FunctionAddressSpace.Instance,
                            $"parameter_{parameterSlots.Count}",
                            parameter.Type,
                            []));
                }
            });

            foreach (var transfer in source.Control.Transfers)
            {
                var continuation = Continuation.From(transfer);
                if (!tokenIds.ContainsKey(continuation))
                    tokenIds.Add(continuation, tokenIds.Count);
            }
            if (controlFlowPolicy is SlangControlFlowPolicy.WgslCompatible &&
                source.Declaration.ReturnType is not UnitType &&
                HasValueReturnInLoop(source.Body))
            {
                returnValue = new VariableDeclaration(
                    FunctionAddressSpace.Instance,
                    "return_value",
                    source.Declaration.ReturnType,
                    []);
            }
            if (tokenIds.Count > 0 || returnValue is not null)
                token = new VariableDeclaration(
                    FunctionAddressSpace.Instance,
                    "control",
                    ShaderType.I32,
                    []);

            FindCrossLabelCaptures();
        }

        internal SlangFunctionBody Lower()
        {
            var lowered = LowerRegion(source.Body, 0);
            if (!lowered.Escapes.IsEmpty)
                throw Error(
                    $"root region has unresolved scoped transfers: " +
                    string.Join(", ", lowered.Escapes.Select(Format)));
            if (lowered.Returns != (returnValue is not null))
                throw Error("WGSL-compatible return lowering did not reach the function root");

            var declarations = source.LocalVariables
                .Concat(parameterSlots.Values)
                .Concat(captures.Values)
                .Concat(returnValue is null ? [] : [returnValue])
                .Concat(token is null ? [] : [token])
                .Select(variable => (SlangStatement)new SlangDeclare(variable));
            var statements = lowered.Statements;
            if (lowered.Returns)
            {
                var returned = new SlangReturnValue(
                    new SlangPlaceOperand(new SlangVariablePlace(returnValue ??
                        throw Error("a hoisted return requires a typed value slot"))));
                statements = statements.Add(returned);
            }
            return new SlangFunctionBody(
                source.Declaration,
                new SlangBlock([.. declarations, .. statements]),
                new SlangLoweringOrigins(
                    token,
                    parameterSlots.ToImmutableDictionary(ReferenceEqualityComparer.Instance),
                    captures.ToImmutableDictionary(ReferenceEqualityComparer.Instance),
                    parameterOrigins.ToImmutable(),
                    definitionOrigins.ToImmutable(),
                    dimensionsOrigins.ToImmutable(),
                    instructionOrigins.ToImmutable(),
                    transferOrigins.ToImmutable(),
                    conditionalOrigins.ToImmutable(),
                    gateOrigins.ToImmutable(),
                    returnOrigins.ToImmutable(),
                    carrierBreakOrigins.ToImmutable()));
        }

        private Lowered LowerRegion(
            RegionTree<Label, ShaderRegionBody> region,
            int enclosingLoopDepth)
        {
            var loopDepth = enclosingLoopDepth +
                (region.Definition.Kind is RegionKind.Loop ? 1 : 0);
            var activation = LowerActivation(region, loopDepth);
            if (region.Definition.Kind is not RegionKind.Loop)
                return activation;

            var repeat = new Continuation(
                region.Label,
                region.Label,
                ScopedContinuationKind.Repeat);
            var repeats = activation.Escapes.Contains(repeat);
            var outward = activation.Escapes.Remove(repeat);
            var statements = activation.Statements.ToBuilder();
            var exitsLoop = !outward.IsEmpty || activation.Returns;

            if (repeats && !exitsLoop)
            {
                statements.Add(new SlangContinue());
            }
            else if (repeats)
            {
                var gate = TokenEquals(repeat, statements);
                var conditional = new SlangIf(
                    gate.Condition,
                    new SlangBlock([(SlangStatement)new SlangContinue()]),
                    new SlangBlock([(SlangStatement)new SlangBreak()]));
                statements.Add(conditional);
                gateOrigins.Add(new(
                    Origin(repeat),
                    gate.TokenId,
                    gate.Comparison,
                    conditional));
            }
            else if (exitsLoop)
            {
                statements.Add(new SlangBreak());
            }

            return new Lowered(
                [(SlangStatement)new SlangLoop(region.Label, new SlangBlock(statements.ToImmutable()))],
                outward,
                activation.Returns);
        }

        private Lowered LowerActivation(
            RegionTree<Label, ShaderRegionBody> region,
            int loopDepth)
        {
            var suffix = LowerRaw(region, loopDepth);
            var bindings = region.Bindings.ToImmutableArray();
            var innermost = true;

            for (var index = bindings.Length - 1; index >= 0; index--)
            {
                var child = bindings[index];
                var caught = new Continuation(
                    child.Label,
                    region.Label,
                    ScopedContinuationKind.Forward);
                if (!suffix.Escapes.Contains(caught))
                    throw Error(
                        $"binding '{child.Label.Name}' owned by '{region.Label.Name}' is not consumed " +
                        "by any reachable scoped transfer");

                var statements = ImmutableArray.CreateBuilder<SlangStatement>();
                if (innermost)
                {
                    statements.AddRange(suffix.Statements);
                    innermost = false;
                }
                else
                {
                    var carrier = suffix.Statements.ToBuilder();
                    SlangBreak? carrierBreak = null;
                    if (!suffix.Escapes.IsEmpty || suffix.Returns)
                    {
                        carrierBreak = new SlangBreak();
                        carrier.Add(carrierBreak);
                    }
                    var once = new SlangDoOnce(new SlangBlock(carrier.ToImmutable()));
                    statements.Add(once);
                    if (carrierBreak is not null)
                        carrierBreakOrigins.Add(new(region.Label, child.Label, once, carrierBreak));
                }

                var childLowered = LowerRegion(child, loopDepth);
                if (suffix.Escapes.Count == 1 && !suffix.Returns)
                {
                    statements.AddRange(childLowered.Statements);
                }
                else
                {
                    var gate = TokenEquals(caught, statements);
                    var conditional = new SlangIf(
                        gate.Condition,
                        new SlangBlock(childLowered.Statements),
                        SlangBlock.Empty);
                    statements.Add(conditional);
                    gateOrigins.Add(new(
                        Origin(caught),
                        gate.TokenId,
                        gate.Comparison,
                        conditional));
                }

                suffix = new Lowered(
                    statements.ToImmutable(),
                    suffix.Escapes.Remove(caught).Union(childLowered.Escapes),
                    suffix.Returns || childLowered.Returns);
            }

            return suffix;
        }

        private Lowered LowerRaw(
            RegionTree<Label, ShaderRegionBody> region,
            int loopDepth)
        {
            var statements = ImmutableArray.CreateBuilder<SlangStatement>();
            foreach (var parameter in region.Body.Parameters)
            {
                var load = Instruction<SlangOperand, IShaderValue>.Create(
                    new LoadOperation(),
                    parameter,
                    [new SlangPlaceOperand(new SlangVariablePlace(parameterSlots[parameter]))]);
                var definition = new SlangBind(load);
                statements.Add(definition);
                var capture = CaptureDefinition(parameter, statements);
                parameterOrigins.Add(new(
                    region.Label,
                    parameter,
                    parameterSlots[parameter],
                    definition,
                    capture));
            }
            foreach (var (ordinal, instruction) in region.Body.Body.Elements.Index())
                LowerInstruction(region.Label, ordinal, instruction, statements);

            var terminated = LowerTerminator(region.Body.Body.Last, region.Label, loopDepth);
            statements.AddRange(terminated.Statements);
            var original = new SlangScope(
                region.Definition.Kind is RegionKind.Loop ? null : region.Label,
                new SlangBlock(statements.ToImmutable()));
            return new Lowered(
                [(SlangStatement)new SlangDoOnce(new SlangBlock([original]))],
                terminated.Escapes,
                terminated.Returns);
        }

        private Lowered LowerTerminator(
            ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
            Label sourceLabel,
            int loopDepth) =>
            terminator switch
            {
                Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> =>
                    LowerReturnVoid(sourceLabel),
                Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned =>
                    LowerReturnValue(sourceLabel, returned.Expr, loopDepth),
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch =>
                    LowerTransfer(sourceLabel, 0, branch.Target),
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    LowerConditional(sourceLabel, branch),
                Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> branch =>
                    LowerSwitch(sourceLabel, branch),
                _ => throw Error($"unsupported terminator in block '{sourceLabel.Name}'")
            };

        private Lowered LowerReturnVoid(Label sourceLabel)
        {
            var statement = new SlangReturnVoid();
            returnOrigins.Add(new(sourceLabel, null, statement));
            return Lowered.Completed(statement);
        }

        private Lowered LowerReturnValue(
            Label sourceLabel,
            IShaderValue value,
            int loopDepth)
        {
            if (controlFlowPolicy is SlangControlFlowPolicy.WgslCompatible &&
                loopDepth > 0)
            {
                var slot = returnValue ??
                    throw Error("a nested typed return requires a return value slot");
                var tokenId = tokenIds.Count;
                var valueAssignment = new SlangAssign(
                    new SlangVariablePlace(slot),
                    Operand(value));
                var tokenAssignment = new SlangAssign(
                    new SlangVariablePlace(token ??
                        throw Error("a nested typed return requires a control token")),
                    new SlangValueOperand(Int(tokenId)));
                var @break = new SlangBreak();
                return new Lowered(
                    [valueAssignment, tokenAssignment, @break],
                    ImmutableHashSet<Continuation>.Empty,
                    true);
            }

            var statement = new SlangReturnValue(Operand(value));
            returnOrigins.Add(new(sourceLabel, value, statement));
            return Lowered.Completed(statement);
        }

        private Lowered LowerConditional(
            Label sourceLabel,
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch)
        {
            var whenTrue = LowerTransfer(sourceLabel, 0, branch.TrueTarget);
            var whenFalse = LowerTransfer(sourceLabel, 1, branch.FalseTarget);
            var conditional = new SlangIf(
                Operand(branch.Condition),
                new SlangBlock(whenTrue.Statements),
                new SlangBlock(whenFalse.Statements));
            conditionalOrigins.Add(new(sourceLabel, branch.Condition, conditional));
            return new Lowered(
                [conditional],
                whenTrue.Escapes.Union(whenFalse.Escapes),
                whenTrue.Returns || whenFalse.Returns);
        }

        private Lowered LowerSwitch(
            Label sourceLabel,
            Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> branch)
        {
            if (!branch.Selector.Type.Equals(ShaderType.I32))
                throw Error(
                    $"switch selector in block '{sourceLabel.Name}' must be i32, got {branch.Selector.Type.Name}");

            var lowered = LowerTransfer(sourceLabel, branch.CaseTargets.Length, branch.DefaultTarget);
            for (var index = branch.CaseTargets.Length - 1; index >= 0; index--)
            {
                var selected = LowerTransfer(sourceLabel, index, branch.CaseTargets[index]);
                var comparison = ShaderValue.Intermediate(ShaderType.Bool);
                var statements = ImmutableArray.CreateBuilder<SlangStatement>();
                statements.Add(new SlangBind(
                    Instruction<SlangOperand, IShaderValue>.Create(
                        NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>.Instance,
                        comparison,
                        [Operand(branch.Selector), new SlangValueOperand(Int(index))])));
                statements.Add(new SlangIf(
                    new SlangValueOperand(comparison),
                    new SlangBlock(selected.Statements),
                    new SlangBlock(lowered.Statements)));
                lowered = new Lowered(
                    statements.ToImmutable(),
                    selected.Escapes.Union(lowered.Escapes),
                    selected.Returns || lowered.Returns);
            }
            return lowered;
        }

        private Lowered LowerTransfer(
            Label sourceLabel,
            int arm,
            RegionJump<IShaderValue> jump)
        {
            var transfer = source.Control.Resolve(sourceLabel, arm);
            if (!transfer.Target.Equals(jump.Label))
                throw Error(
                    $"checked transfer from '{sourceLabel.Name}', arm {arm}, resolves to " +
                    $"'{transfer.Target.Name}', not '{jump.Label.Name}'");

            var target = blocks[transfer.Target].Body;
            var statements = ImmutableArray.CreateBuilder<SlangStatement>();
            var values = ImmutableArray.CreateBuilder<IShaderValue>();
            var snapshots = ImmutableArray.CreateBuilder<(
                int Position,
                IShaderValue Argument,
                IShaderValue Snapshot,
                SlangBind Definition)>();
            foreach (var (position, argument) in jump.Arguments.Index())
            {
                if (argument.Type is IPtrType)
                    throw Error(
                        $"transfer from '{sourceLabel.Name}', arm {arm}, retains a pointer argument");
                var captured = ShaderValue.Intermediate(argument.Type);
                var definition = new SlangBind(
                    Instruction<SlangOperand, IShaderValue>.Create(
                        new LoadOperation(),
                        captured,
                        [Operand(argument)]));
                statements.Add(definition);
                values.Add(captured);
                snapshots.Add((position, argument, captured, definition));
            }

            var arguments = ImmutableArray.CreateBuilder<SlangTransferArgumentOrigin>();
            foreach (var ((parameter, value), snapshot) in target.Parameters.Zip(values).Zip(snapshots))
            {
                var assignment = new SlangAssign(
                    new SlangVariablePlace(parameterSlots[parameter]),
                    new SlangValueOperand(value));
                statements.Add(assignment);
                arguments.Add(new(
                    snapshot.Position,
                    snapshot.Argument,
                    snapshot.Snapshot,
                    snapshot.Definition,
                    parameter,
                    parameterSlots[parameter],
                    assignment));
            }

            var continuation = Continuation.From(transfer);
            var tokenId = tokenIds[continuation];
            var tokenAssignment = new SlangAssign(
                new SlangVariablePlace(token ??
                    throw Error("a scoped transfer requires a control token")),
                new SlangValueOperand(Int(tokenId)));
            var @break = new SlangBreak();
            statements.Add(tokenAssignment);
            statements.Add(@break);
            transferOrigins.Add(new(
                sourceLabel,
                arm,
                jump,
                Origin(continuation),
                tokenId,
                arguments.ToImmutable(),
                tokenAssignment,
                @break));
            return new Lowered(
                statements.ToImmutable(),
                ImmutableHashSet.Create(continuation),
                false);
        }

        private Gate TokenEquals(
            Continuation continuation,
            ImmutableArray<SlangStatement>.Builder statements)
        {
            var result = ShaderValue.Intermediate(ShaderType.Bool);
            var tokenId = tokenIds[continuation];
            var comparison = new SlangBind(
                Instruction<SlangOperand, IShaderValue>.Create(
                    NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>.Instance,
                    result,
                    [
                        new SlangPlaceOperand(new SlangVariablePlace(token ??
                            throw Error("a scoped gate requires a control token"))),
                        new SlangValueOperand(Int(tokenId))
                    ]));
            statements.Add(comparison);
            return new Gate(comparison, new SlangValueOperand(result), tokenId);
        }

        private void FindCrossLabelCaptures()
        {
            var definitions = new Dictionary<IShaderValue, Label>(ReferenceEqualityComparer.Instance);
            foreach (var block in blockOrder)
            {
                foreach (var parameter in block.Body.Parameters)
                    if (!definitions.TryAdd(parameter, block.Label))
                        throw Error($"value '{parameter}' has multiple parameter definitions");
                foreach (var instruction in block.Body.Body.Elements)
                    if (instruction.Result is { } result && !definitions.TryAdd(result, block.Label))
                        throw Error($"value '{result}' has multiple instruction definitions");
            }

            foreach (var block in blockOrder)
                foreach (var value in block.Body.Body.Elements.SelectMany(instruction => instruction.Operands)
                             .Concat(TerminatorValues(block.Body.Body.Last)))
                {
                    if (!definitions.TryGetValue(value, out var definition) ||
                        definition.Equals(block.Label) ||
                        captures.ContainsKey(value) ||
                        value.Type is IPtrType)
                        continue;
                    if (value.Type is UnitType)
                        throw Error($"unit value '{value}' crosses original-label scope");
                    captures.Add(
                        value,
                        new VariableDeclaration(
                            FunctionAddressSpace.Instance,
                            $"capture_{captures.Count}",
                            value.Type,
                            []));
                }
        }

        private static IEnumerable<IShaderValue> TerminatorValues(
            ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
            terminator switch
            {
                Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned =>
                    [returned.Expr],
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch =>
                    branch.Target.Arguments,
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    [branch.Condition, .. branch.TrueTarget.Arguments, .. branch.FalseTarget.Arguments],
                Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> branch =>
                    [
                        branch.Selector,
                        .. branch.CaseTargets.SelectMany(target => target.Arguments),
                        .. branch.DefaultTarget.Arguments
                    ],
                Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> => [],
                _ => throw new NotSupportedException(
                    $"Cross-label capture discovery does not support terminator {terminator.GetType().Name}.")
            };

        private void LowerInstruction(
            Label label,
            int ordinal,
            Instruction<IShaderValue, IShaderValue> instruction,
            ImmutableArray<SlangStatement>.Builder statements)
        {
            switch (instruction.Operation)
            {
                case IReadOnlyStructuredBufferLengthOperation length:
                    ValidateStructuredBufferLength(instruction, length);
                    var count = instruction.Result!;
                    var stride = ShaderValue.Intermediate(ShaderType.U32);
                    var dimensions = new SlangGetDimensions(
                        new SlangPlaceOperand(Place(instruction.Operand0, instruction.Operation.Name)),
                        count,
                        stride);
                    statements.Add(dimensions);
                    var capture = CaptureDefinition(count, statements);
                    dimensionsOrigins.Add(new(label, ordinal, instruction, dimensions, capture));
                    return;
                case IReadOnlyStructuredBufferLoadOperation load:
                    ValidateStructuredBufferLoad(instruction, load);
                    break;
                case IReadWriteStructuredBufferLengthOperation rwLength:
                    ValidateReadWriteStructuredBufferLength(instruction, rwLength);
                    var rwCount = instruction.Result!;
                    var rwStride = ShaderValue.Intermediate(ShaderType.U32);
                    var rwDimensions = new SlangGetDimensions(
                        new SlangPlaceOperand(Place(instruction.Operand0, instruction.Operation.Name)),
                        rwCount,
                        rwStride);
                    statements.Add(rwDimensions);
                    var rwCapture = CaptureDefinition(rwCount, statements);
                    dimensionsOrigins.Add(new(label, ordinal, instruction, rwDimensions, rwCapture));
                    return;
                case IReadWriteStructuredBufferLoadOperation rwLoad:
                    ValidateReadWriteStructuredBufferLoad(instruction, rwLoad);
                    break;
                case IReadWriteStructuredBufferStoreOperation rwStore:
                    ValidateReadWriteStructuredBufferStore(instruction, rwStore);
                    var indexedStore = new SlangAssign(
                        new SlangIndexedPlace(
                            Place(instruction.Operand0, instruction.Operation.Name),
                            Operand(instruction.Operand1),
                            rwStore.ElementType),
                        Operand(instruction[2]));
                    statements.Add(indexedStore);
                    instructionOrigins.Add(new(label, ordinal, instruction, indexedStore));
                    return;
                case TextureSampleLevelOperation sample:
                    ValidateTextureSampleLevel(source.Declaration, instruction, sample);
                    break;
                case AddressOfMemberOperation member:
                    if (!HasPhysicalOperandShape(instruction, 1) ||
                        instruction.Operand0!.Type is not IPtrType { BaseType: StructureType owner } ownerPointer ||
                        !owner.Declaration.Members.Contains(member.Member) ||
                        instruction.Result?.Type is not IPtrType resultPointer ||
                        !resultPointer.AddressSpace.Equals(ownerPointer.AddressSpace))
                        throw UnsupportedOperation(instruction, "invalid field owner or projected address space");
                    DefineAlias(instruction, new SlangMemberPlace(
                        Place(instruction.Operand0, instruction.Operation.Name),
                        member.Member));
                    return;
                case StructureMemberGetOperation get:
                    if (!HasPhysicalOperandShape(instruction, 1) ||
                        !get.Owner.Declaration.Members.Contains(get.Member) ||
                        !instruction.Operand0!.Type.Equals(get.Owner) ||
                        instruction.Result is null ||
                        !instruction.Result.Type.Equals(get.Member.Type))
                        throw UnsupportedOperation(instruction, "invalid typed structure member read");
                    break;
                case AddressOfVecComponentOperation component:
                    DefineAlias(instruction, new SlangComponentPlace(
                        Place(instruction.Operand0, instruction.Operation.Name),
                        component.Component.Name,
                        component.Target.ElementType));
                    return;
                case IAddressOfOperation:
                    throw UnsupportedOperation(instruction, "unsupported address projection");
                case AccessChainOperation:
                    throw UnsupportedOperation(instruction, "access chains are not supported");
                case ScalarConversionOperation<IntType<N32>, UIntType<N64>>:
                    throw UnsupportedOperation(
                        instruction, "i32-to-u64 conversion; unsigned widening is not implemented");
                case LoadOperation when IsResourceValue(instruction.Operand0) ||
                                        IsResourceValue(instruction.Result):
                    throw UnsupportedOperation(
                        instruction,
                        "whole structured-buffer or texture/sampler handle loads are not supported");
                case LoadOperation:
                    if (!HasPhysicalOperandShape(instruction, 1) ||
                        instruction.Operand0!.Type is not IPtrType loadPointer ||
                        instruction.Result is null ||
                        !instruction.Result.Type.Equals(loadPointer.BaseType))
                        throw UnsupportedOperation(instruction, "invalid typed load arity or pointee/result type");
                    break;
                case StoreOperation when IsResourceValue(instruction.Operand0) ||
                                         IsResourceValue(instruction.Operand1):
                    throw UnsupportedOperation(
                        instruction,
                        "whole structured-buffer or texture/sampler handle stores are not supported");
                case StoreOperation:
                    if (!HasPhysicalOperandShape(instruction, 2) ||
                        instruction.Result is not null ||
                        instruction.Operand0!.Type is not IPtrType destination ||
                        !destination.BaseType.Equals(instruction.Operand1!.Type) ||
                        destination.AddressSpace.Kind is AddressSpaceKind.Uniform or AddressSpaceKind.Input or AddressSpaceKind.Handle)
                        throw UnsupportedOperation(instruction, "invalid or read-only typed store");
                    var store = new SlangAssign(
                        Place(instruction.Operand0, instruction.Operation.Name),
                        Operand(instruction.Operand1));
                    statements.Add(store);
                    instructionOrigins.Add(new(label, ordinal, instruction, store));
                    return;
                case IVectorComponentSetOperation component:
                    var componentSet = new SlangAssign(
                        new SlangComponentPlace(
                            Place(instruction.Operand0, instruction.Operation.Name),
                            component.Component.Name,
                            component.ElementType),
                        Operand(instruction.Operand1));
                    statements.Add(componentSet);
                    instructionOrigins.Add(new(label, ordinal, instruction, componentSet));
                    return;
                case IVectorSwizzleSetOperation swizzle:
                    var swizzleSet = new SlangAssign(
                        new SlangSwizzlePlace(
                            Place(instruction.Operand0, instruction.Operation.Name),
                            swizzle.Pattern.Name,
                            swizzle.ValueVecType),
                        Operand(instruction.Operand1));
                    statements.Add(swizzleSet);
                    instructionOrigins.Add(new(label, ordinal, instruction, swizzleSet));
                    return;
                case ZeroConstructorOperation zero when zero.ResultType is not IVecType:
                    throw UnsupportedOperation(instruction, $"zero construction of {zero.ResultType.Name}");
                case StructureCompositeConstructionOperation composite:
                    var members = composite.ResultType.Declaration.Members;
                    if (!HasPhysicalOperandShape(instruction, members.Length) ||
                        !composite.Matches(
                            instruction.Result?.Type,
                            instruction.Operands.Select(operand => operand.Type)))
                        throw UnsupportedOperation(instruction, "invalid ordered structure composite");
                    break;
            }

            if (!IsSupportedExpression(instruction.Operation))
                throw UnsupportedOperation(instruction, "operation has no Slang expression spelling");
            var lowered = instruction.Select(Operand, static result => result);
            if (lowered.Result is null ||
                instruction.Operation is CallOperation { ResultType: UnitType })
            {
                var effect = new SlangEffect(lowered);
                statements.Add(effect);
                instructionOrigins.Add(new(label, ordinal, instruction, effect));
            }
            else
            {
                if (lowered.Result.Type is UnitType)
                    throw UnsupportedOperation(instruction, "only calls may produce Unit effects");
                if (lowered.Result.Type is IPtrType)
                    throw UnsupportedOperation(instruction, "pointer results must lower to typed places");
                var definition = new SlangBind(lowered);
                statements.Add(definition);
                var capture = CaptureDefinition(lowered.Result, statements);
                definitionOrigins.Add(new(label, ordinal, instruction, definition, capture));
                instructionOrigins.Add(new(label, ordinal, instruction, definition));
            }
        }

        private SlangAssign? CaptureDefinition(
            IShaderValue value,
            ImmutableArray<SlangStatement>.Builder statements)
        {
            if (!captures.TryGetValue(value, out var capture))
                return null;
            var assignment = new SlangAssign(
                new SlangVariablePlace(capture),
                new SlangValueOperand(value));
            statements.Add(assignment);
            return assignment;
        }

        private static bool IsSupportedExpression(IOperation operation) =>
            operation is NopOperation
                or LoadOperation
                or CallOperation
                or LiteralOperation
                or IReadOnlyStructuredBufferLoadOperation
                or IReadWriteStructuredBufferLoadOperation
                or TextureSampleLevelOperation
                or IUnaryExpressionOperation
                or IBinaryExpressionOperation
                or VectorCompositeConstructionOperation
                or StructureCompositeConstructionOperation
                or ZeroConstructorOperation;

        private void ValidateStructuredBufferLength(
            Instruction<IShaderValue, IShaderValue> instruction,
            IReadOnlyStructuredBufferLengthOperation operation)
        {
            if (!ReadOnlyStructuredBufferFamily.IsCanonicalLength(operation) ||
                !HasPhysicalOperandShape(instruction, 1) ||
                instruction.Operand0!.Type is not IPtrType ||
                !instruction.Operand0.Type.Equals(operation.BufferPointerType) ||
                instruction.Result is null ||
                !instruction.Result.Type.Equals(ShaderType.U32))
                throw UnsupportedOperation(instruction, "invalid read-only storage-buffer Length signature");
        }

        private void ValidateStructuredBufferLoad(
            Instruction<IShaderValue, IShaderValue> instruction,
            IReadOnlyStructuredBufferLoadOperation operation)
        {
            if (!ReadOnlyStructuredBufferFamily.IsCanonicalLoad(operation) ||
                !HasPhysicalOperandShape(instruction, 2) ||
                instruction.Operand0!.Type is not IPtrType ||
                !instruction.Operand0.Type.Equals(operation.BufferPointerType) ||
                !instruction.Operand1!.Type.Equals(ShaderType.U32) ||
                instruction.Result is null ||
                !instruction.Result.Type.Equals(operation.ElementType))
                throw UnsupportedOperation(instruction, "invalid read-only storage-buffer load signature");
        }

        private void ValidateReadWriteStructuredBufferLength(
            Instruction<IShaderValue, IShaderValue> instruction,
            IReadWriteStructuredBufferLengthOperation operation)
        {
            if (!ReadWriteStructuredBufferFamily.IsCanonicalLength(operation) ||
                !HasPhysicalOperandShape(instruction, 1) ||
                instruction.Operand0!.Type is not IPtrType ||
                !instruction.Operand0.Type.Equals(operation.BufferPointerType) ||
                instruction.Result is null ||
                !instruction.Result.Type.Equals(ShaderType.U32))
                throw UnsupportedOperation(instruction, "invalid read-write storage-buffer Length signature");
        }

        private void ValidateReadWriteStructuredBufferLoad(
            Instruction<IShaderValue, IShaderValue> instruction,
            IReadWriteStructuredBufferLoadOperation operation)
        {
            if (!ReadWriteStructuredBufferFamily.IsCanonicalLoad(operation) ||
                !HasPhysicalOperandShape(instruction, 2) ||
                instruction.Operand0!.Type is not IPtrType ||
                !instruction.Operand0.Type.Equals(operation.BufferPointerType) ||
                !instruction.Operand1!.Type.Equals(ShaderType.U32) ||
                instruction.Result is null ||
                !instruction.Result.Type.Equals(operation.ElementType))
                throw UnsupportedOperation(instruction, "invalid read-write storage-buffer load signature");
        }

        private void ValidateReadWriteStructuredBufferStore(
            Instruction<IShaderValue, IShaderValue> instruction,
            IReadWriteStructuredBufferStoreOperation operation)
        {
            if (!ReadWriteStructuredBufferFamily.IsCanonicalStore(operation) ||
                !HasPhysicalOperandShape(instruction, 3) ||
                instruction.Operand0!.Type is not IPtrType ||
                !instruction.Operand0.Type.Equals(operation.BufferPointerType) ||
                !instruction.Operand1!.Type.Equals(ShaderType.U32) ||
                !instruction.RestOperands[0].Type.Equals(operation.ElementType) ||
                instruction.Result is not null)
                throw UnsupportedOperation(instruction, "invalid read-write storage-buffer store signature");
        }

        private static bool HasPhysicalOperandShape(
            Instruction<IShaderValue, IShaderValue> instruction,
            int expectedCount) =>
            instruction.OperandCount == expectedCount &&
            instruction.Operand0 is not null &&
            (expectedCount == 1
                ? instruction.Operand1 is null
                : instruction.Operand1 is not null) &&
            !instruction.RestOperands.IsDefault &&
            instruction.RestOperands.Length == Math.Max(0, expectedCount - 2) &&
            instruction.RestOperands.All(static operand => operand is not null);

        private static bool IsResourceValue(IShaderValue? value) =>
            value is not null &&
            ShaderModuleMetadataValidator.IsResourceTypeOrPointer(value.Type);

        private void DefineAlias(
            Instruction<IShaderValue, IShaderValue> instruction,
            SlangPlace place)
        {
            var result = instruction.Result ??
                throw UnsupportedOperation(instruction, "address projection has no result");
            if (result.Type is not IPtrType pointer || !Equals(pointer.BaseType, place.Type))
                throw UnsupportedOperation(
                    instruction,
                    $"address result {result.Type.Name} does not point to {place.Type.Name}");
            if (aliases.TryGetValue(result, out var existing) && !Equals(existing, place))
                throw UnsupportedOperation(instruction, "address result has conflicting projections");
            aliases[result] = place;
        }

        private SlangOperand Operand(IShaderValue? value)
        {
            if (value is null) throw Error("instruction contains a missing operand");
            if (value is FunctionDeclaration function &&
                PortableDerivativeTarget.TryLower(function, out var target))
                return new SlangValueOperand(target);
            if (captures.TryGetValue(value, out var capture))
                return new SlangPlaceOperand(new SlangVariablePlace(capture));
            return value.Type is IPtrType
                ? new SlangPlaceOperand(Place(value, "operand"))
                : new SlangValueOperand(value);
        }

        private SlangPlace Place(IShaderValue? value, string operation)
        {
            if (value is null) throw Error($"{operation} contains a missing place operand");
            if (aliases.TryGetValue(value, out var alias)) return alias;
            return value switch
            {
                VariablePointerValue variable => new SlangVariablePlace(variable.Declaration),
                ParameterPointerValue parameter => new SlangParameterPlace(parameter.Declaration),
                _ => throw Error(
                    $"{operation} requires a typed place, but '{value}' is not a stable address")
            };
        }

        private NotSupportedException UnsupportedOperation(
            Instruction<IShaderValue, IShaderValue> instruction,
            string reason) =>
            Error($"operation '{instruction.Operation.Name}': {reason}");

        private NotSupportedException Error(string message) =>
            new($"Function '{source.Declaration.Name}': {message}.");

        private static IShaderValue Int(int value) =>
            ShaderValue.Literal(new I32Literal(value));

        private static string Format(Continuation continuation) =>
            $"{continuation.Kind.ToString().ToLowerInvariant()} " +
            $"{continuation.Target} owned by {continuation.Owner}";

        private static SlangContinuationOrigin Origin(Continuation continuation) =>
            new(continuation.Target, continuation.Owner, continuation.Kind);

        private static bool HasValueReturnInLoop(
            RegionTree<Label, ShaderRegionBody> region,
            bool insideLoop = false)
        {
            var nested = insideLoop || region.Definition.Kind is RegionKind.Loop;
            if (nested &&
                region.Body.Body.Last is
                    Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>)
                return true;
            return region.Bindings.Any(child => HasValueReturnInLoop(child, nested));
        }

        private sealed record Continuation(
            Label Target,
            Label Owner,
            ScopedContinuationKind Kind)
        {
            internal static Continuation From(ScopedTransfer<Label> transfer) =>
                new(transfer.Target, transfer.Owner, transfer.Kind);
        }

        private readonly record struct Lowered(
            ImmutableArray<SlangStatement> Statements,
            ImmutableHashSet<Continuation> Escapes,
            bool Returns)
        {
            internal static Lowered Completed(SlangStatement statement) =>
                new([statement], ImmutableHashSet<Continuation>.Empty, false);
        }

        private readonly record struct Gate(
            SlangBind Comparison,
            SlangOperand Condition,
            int TokenId);
    }
}
