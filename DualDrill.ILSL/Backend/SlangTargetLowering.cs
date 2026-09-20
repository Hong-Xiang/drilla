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

public sealed class SlangTargetLowering
{
    public ShaderModuleDeclaration<SlangFunctionBody> Lower(
        ShaderModuleDeclaration<RegionFunctionBody> module)
    {
        ShaderModuleMetadataValidator.Validate(module);
        WgslUniformLayoutValidator.Validate(module);
        var definitions = module.FunctionDefinitions.ToImmutableDictionary(
            definition => definition.Key,
            definition => new FunctionLowerer(definition.Value).Lower());
        var missing = module.Declarations.OfType<FunctionDeclaration>()
            .Where(declaration => !definitions.ContainsKey(declaration))
            .Select(declaration => declaration.Name)
            .ToArray();
        if (missing.Length > 0)
            throw new NotSupportedException(
                $"Slang target lowering requires bodies for functions: {string.Join(", ", missing)}.");
        return new ShaderModuleDeclaration<SlangFunctionBody>(module.Declarations, definitions);
    }

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
        private readonly RegionFunctionBody source;
        private readonly VariableDeclaration? token;

        internal FunctionLowerer(RegionFunctionBody source)
        {
            this.source = source;
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
            if (tokenIds.Count > 0)
                token = new VariableDeclaration(
                    FunctionAddressSpace.Instance,
                    "control",
                    ShaderType.I32,
                    []);

            FindCrossLabelCaptures();
        }

        internal SlangFunctionBody Lower()
        {
            var lowered = LowerRegion(source.Body);
            if (!lowered.Escapes.IsEmpty)
                throw Error(
                    $"root region has unresolved scoped transfers: " +
                    string.Join(", ", lowered.Escapes.Select(Format)));

            var declarations = source.LocalVariables
                .Concat(parameterSlots.Values)
                .Concat(captures.Values)
                .Concat(token is null ? [] : [token])
                .Select(variable => (SlangStatement)new SlangDeclare(variable));
            return new SlangFunctionBody(
                source.Declaration,
                new SlangBlock([.. declarations, .. lowered.Statements]));
        }

        private Lowered LowerRegion(RegionTree<Label, ShaderRegionBody> region)
        {
            var activation = LowerActivation(region);
            if (region.Definition.Kind is not RegionKind.Loop)
                return activation;

            var repeat = new Continuation(
                region.Label,
                region.Label,
                ScopedContinuationKind.Repeat);
            var repeats = activation.Escapes.Contains(repeat);
            var outward = activation.Escapes.Remove(repeat);
            var statements = activation.Statements.ToBuilder();

            if (repeats && outward.IsEmpty)
            {
                statements.Add(new SlangContinue());
            }
            else if (repeats)
            {
                var condition = TokenEquals(repeat, statements);
                statements.Add(new SlangIf(
                    condition,
                    new SlangBlock([(SlangStatement)new SlangContinue()]),
                    new SlangBlock([(SlangStatement)new SlangBreak()])));
            }
            else if (!outward.IsEmpty)
            {
                statements.Add(new SlangBreak());
            }

            return new Lowered(
                [(SlangStatement)new SlangLoop(region.Label, new SlangBlock(statements.ToImmutable()))],
                outward);
        }

        private Lowered LowerActivation(RegionTree<Label, ShaderRegionBody> region)
        {
            var suffix = LowerRaw(region);
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
                    if (!suffix.Escapes.IsEmpty)
                        carrier.Add(new SlangBreak());
                    statements.Add(new SlangDoOnce(new SlangBlock(carrier.ToImmutable())));
                }

                var childLowered = LowerRegion(child);
                if (suffix.Escapes.Count == 1)
                {
                    statements.AddRange(childLowered.Statements);
                }
                else
                {
                    var condition = TokenEquals(caught, statements);
                    statements.Add(new SlangIf(
                        condition,
                        new SlangBlock(childLowered.Statements),
                        SlangBlock.Empty));
                }

                suffix = new Lowered(
                    statements.ToImmutable(),
                    suffix.Escapes.Remove(caught).Union(childLowered.Escapes));
            }

            return suffix;
        }

        private Lowered LowerRaw(RegionTree<Label, ShaderRegionBody> region)
        {
            var statements = ImmutableArray.CreateBuilder<SlangStatement>();
            foreach (var parameter in region.Body.Parameters)
            {
                var load = Instruction<SlangOperand, IShaderValue>.Create(
                    new LoadOperation(),
                    parameter,
                    [new SlangPlaceOperand(new SlangVariablePlace(parameterSlots[parameter]))]);
                statements.Add(new SlangBind(load));
                CaptureDefinition(parameter, statements);
            }
            foreach (var instruction in region.Body.Body.Elements)
                LowerInstruction(instruction, statements);

            var terminated = LowerTerminator(region.Body.Body.Last, region.Label);
            statements.AddRange(terminated.Statements);
            var original = new SlangScope(
                region.Definition.Kind is RegionKind.Loop ? null : region.Label,
                new SlangBlock(statements.ToImmutable()));
            return new Lowered(
                [(SlangStatement)new SlangDoOnce(new SlangBlock([original]))],
                terminated.Escapes);
        }

        private Lowered LowerTerminator(
            ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
            Label sourceLabel) =>
            terminator switch
            {
                Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> =>
                    Lowered.Completed(new SlangReturnVoid()),
                Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned =>
                    Lowered.Completed(new SlangReturnValue(Operand(returned.Expr))),
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch =>
                    LowerTransfer(sourceLabel, 0, branch.Target),
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    LowerConditional(sourceLabel, branch),
                Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> branch =>
                    LowerSwitch(sourceLabel, branch),
                _ => throw Error($"unsupported terminator in block '{sourceLabel.Name}'")
            };

        private Lowered LowerConditional(
            Label sourceLabel,
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch)
        {
            var whenTrue = LowerTransfer(sourceLabel, 0, branch.TrueTarget);
            var whenFalse = LowerTransfer(sourceLabel, 1, branch.FalseTarget);
            return new Lowered(
                [
                    new SlangIf(
                        Operand(branch.Condition),
                        new SlangBlock(whenTrue.Statements),
                        new SlangBlock(whenFalse.Statements))
                ],
                whenTrue.Escapes.Union(whenFalse.Escapes));
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
                    selected.Escapes.Union(lowered.Escapes));
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
            foreach (var argument in jump.Arguments)
            {
                if (argument.Type is IPtrType)
                    throw Error(
                        $"transfer from '{sourceLabel.Name}', arm {arm}, retains a pointer argument");
                var captured = ShaderValue.Intermediate(argument.Type);
                statements.Add(new SlangBind(
                    Instruction<SlangOperand, IShaderValue>.Create(
                        new LoadOperation(),
                        captured,
                        [Operand(argument)])));
                values.Add(captured);
            }

            foreach (var (parameter, value) in target.Parameters.Zip(values))
                statements.Add(new SlangAssign(
                    new SlangVariablePlace(parameterSlots[parameter]),
                    new SlangValueOperand(value)));

            var continuation = Continuation.From(transfer);
            statements.Add(new SlangAssign(
                new SlangVariablePlace(token ??
                    throw Error("a scoped transfer requires a control token")),
                new SlangValueOperand(Int(tokenIds[continuation]))));
            statements.Add(new SlangBreak());
            return new Lowered(
                statements.ToImmutable(),
                ImmutableHashSet.Create(continuation));
        }

        private SlangOperand TokenEquals(
            Continuation continuation,
            ImmutableArray<SlangStatement>.Builder statements)
        {
            var result = ShaderValue.Intermediate(ShaderType.Bool);
            statements.Add(new SlangBind(
                Instruction<SlangOperand, IShaderValue>.Create(
                    NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>.Instance,
                    result,
                    [
                        new SlangPlaceOperand(new SlangVariablePlace(token ??
                            throw Error("a scoped gate requires a control token"))),
                        new SlangValueOperand(Int(tokenIds[continuation]))
                    ])));
            return new SlangValueOperand(result);
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
            Instruction<IShaderValue, IShaderValue> instruction,
            ImmutableArray<SlangStatement>.Builder statements)
        {
            switch (instruction.Operation)
            {
                case StructuredBufferLengthOperation length:
                    ValidateStructuredBufferLength(instruction, length);
                    var count = instruction.Result!;
                    var stride = ShaderValue.Intermediate(ShaderType.U32);
                    statements.Add(new SlangGetDimensions(
                        Operand(instruction.Operand0),
                        count,
                        stride));
                    CaptureDefinition(count, statements);
                    return;
                case StructuredBufferLoadOperation load:
                    ValidateStructuredBufferLoad(instruction, load);
                    break;
                case AddressOfMemberOperation member:
                    DefineAlias(instruction, new SlangMemberPlace(
                        Place(instruction.Operand0, instruction.Operation.Name),
                        member.Member));
                    return;
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
                case LoadOperation when IsStructuredBufferValue(instruction.Operand0) ||
                                        IsStructuredBufferValue(instruction.Result):
                    throw UnsupportedOperation(instruction, "whole structured-buffer loads are not supported");
                case StoreOperation when IsStructuredBufferValue(instruction.Operand0) ||
                                         IsStructuredBufferValue(instruction.Operand1):
                    throw UnsupportedOperation(instruction, "whole structured-buffer stores are not supported");
                case StoreOperation:
                    statements.Add(new SlangAssign(
                        Place(instruction.Operand0, instruction.Operation.Name),
                        Operand(instruction.Operand1)));
                    return;
                case IVectorComponentSetOperation component:
                    statements.Add(new SlangAssign(
                        new SlangComponentPlace(
                            Place(instruction.Operand0, instruction.Operation.Name),
                            component.Component.Name,
                            component.ElementType),
                        Operand(instruction.Operand1)));
                    return;
                case IVectorSwizzleSetOperation swizzle:
                    statements.Add(new SlangAssign(
                        new SlangSwizzlePlace(
                            Place(instruction.Operand0, instruction.Operation.Name),
                            swizzle.Pattern.Name,
                            swizzle.ValueVecType),
                        Operand(instruction.Operand1)));
                    return;
                case ZeroConstructorOperation zero when zero.ResultType is not IVecType:
                    throw UnsupportedOperation(instruction, $"zero construction of {zero.ResultType.Name}");
            }

            if (!IsSupportedExpression(instruction.Operation))
                throw UnsupportedOperation(instruction, "operation has no Slang expression spelling");
            var lowered = instruction.Select(Operand, static result => result);
            if (lowered.Result is null ||
                instruction.Operation is CallOperation { ResultType: UnitType })
            {
                statements.Add(new SlangEffect(lowered));
            }
            else
            {
                if (lowered.Result.Type is UnitType)
                    throw UnsupportedOperation(instruction, "only calls may produce Unit effects");
                if (lowered.Result.Type is IPtrType)
                    throw UnsupportedOperation(instruction, "pointer results must lower to typed places");
                statements.Add(new SlangBind(lowered));
                CaptureDefinition(lowered.Result, statements);
            }
        }

        private void CaptureDefinition(
            IShaderValue value,
            ImmutableArray<SlangStatement>.Builder statements)
        {
            if (captures.TryGetValue(value, out var capture))
                statements.Add(new SlangAssign(
                    new SlangVariablePlace(capture),
                    new SlangValueOperand(value)));
        }

        private static bool IsSupportedExpression(IOperation operation) =>
            operation is NopOperation
                or LoadOperation
                or CallOperation
                or LiteralOperation
                or StructuredBufferLoadOperation
                or IUnaryExpressionOperation
                or IBinaryExpressionOperation
                or VectorCompositeConstructionOperation
                or ZeroConstructorOperation;

        private void ValidateStructuredBufferLength(
            Instruction<IShaderValue, IShaderValue> instruction,
            StructuredBufferLengthOperation operation)
        {
            if (instruction.OperandCount != 1 ||
                instruction.Operand0?.Type is not IPtrType ||
                !instruction.Operand0.Type.Equals(operation.BufferPointerType) ||
                instruction.Result is null ||
                !instruction.Result.Type.Equals(ShaderType.U32))
                throw UnsupportedOperation(instruction, "invalid read-only storage-buffer Length signature");
        }

        private void ValidateStructuredBufferLoad(
            Instruction<IShaderValue, IShaderValue> instruction,
            StructuredBufferLoadOperation operation)
        {
            if (instruction.OperandCount != 2 ||
                instruction.Operand0?.Type is not IPtrType ||
                !instruction.Operand0.Type.Equals(operation.BufferPointerType) ||
                instruction.Operand1 is null ||
                !instruction.Operand1.Type.Equals(ShaderType.U32) ||
                instruction.Result is null ||
                !instruction.Result.Type.Equals(ShaderType.F32))
                throw UnsupportedOperation(instruction, "invalid read-only storage-buffer load signature");
        }

        private static bool IsStructuredBufferValue(IShaderValue? value) =>
            value?.Type switch
            {
                ReadOnlyStructuredBufferType => true,
                IPtrType { BaseType: ReadOnlyStructuredBufferType } => true,
                _ => false
            };

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
            ImmutableHashSet<Continuation> Escapes)
        {
            internal static Lowered Completed(SlangStatement statement) =>
                new([statement], ImmutableHashSet<Continuation>.Empty);
        }
    }
}
