using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Backend;

public sealed class SlangTargetLowering
{
    public ShaderModuleDeclaration<SlangFunctionBody> Lower(
        ShaderModuleDeclaration<FunctionBody4> module)
    {
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
        private readonly HashSet<Label> active = [];
        private readonly List<RegionTree<Label, ShaderRegionBody>> blockOrder = [];
        private readonly Dictionary<Label, RegionTree<Label, ShaderRegionBody>> blocks = [];
        private readonly Dictionary<IShaderValue, VariableDeclaration> captures =
            new(ReferenceEqualityComparer.Instance);
        private readonly Stack<LoopOwner> loopOwners = [];
        private readonly HashSet<Label> placedNonterminals = [];
        private readonly FunctionBody4 source;

        public FunctionLowerer(FunctionBody4 source)
        {
            this.source = source;
            source.Body.Traverse(region =>
            {
                if (!blocks.TryAdd(region.Label, region))
                    throw Error($"duplicate region definition '{region.Label.Name}'");
                blockOrder.Add(region);
                if (!region.Label.Equals(region.Body.Label))
                    throw Error(
                        $"region definition '{region.Label.Name}' contains body '{region.Body.Label.Name}'");
            });
            ValidateControl();
            FindCrossLabelCaptures();
        }

        public SlangFunctionBody Lower()
        {
            var declarations = source.LocalVariables.Concat(captures.Values)
                .Select(variable => (SlangStatement)new SlangDeclare(variable))
                .ToImmutableArray();
            var body = Expand(source.Entry, new NormalTransfer(source.Entry, [source.Entry]), null);
            return new SlangFunctionBody(
                source.Declaration,
                new SlangBlock([.. declarations, .. body.Statements]));
        }

        private void FindCrossLabelCaptures()
        {
            var definitions = new Dictionary<IShaderValue, Label>(ReferenceEqualityComparer.Instance);
            foreach (var block in blockOrder)
                foreach (var instruction in block.Body.Body.Elements)
                    if (instruction.Result is { } result && !definitions.TryAdd(result, block.Label))
                        throw Error($"value '{result}' has multiple instruction definitions");

            foreach (var block in blockOrder)
            {
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
        }

        private static IEnumerable<IShaderValue> TerminatorValues(
            ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
            terminator switch
            {
                Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned => [returned.Expr],
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch => [branch.Condition],
                _ => []
            };

        private void ValidateControl()
        {
            foreach (var block in blocks.Values)
                if (!block.Body.Parameters.IsEmpty)
                    throw Error(
                        $"block '{block.Label.Name}' has residual region parameters; " +
                        "run region parameter lowering before Slang target lowering");

            foreach (var block in blocks.Values)
            {
                foreach (var jump in Jumps(block.Body.Body.Last))
                {
                    if (!jump.Arguments.IsEmpty)
                        throw Error(
                            $"jump from '{block.Label.Name}' to '{jump.Label.Name}' has residual arguments; " +
                            "run region parameter lowering before Slang target lowering");
                    if (!blocks.ContainsKey(jump.Label))
                        throw Error(
                            $"jump from '{block.Label.Name}' references missing label '{jump.Label.Name}'");
                }
            }
        }

        private static IEnumerable<RegionJump<IShaderValue>> Jumps(
            ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
            terminator switch
            {
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => [branch.Target],
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    [branch.TrueTarget, branch.FalseTarget],
                _ => []
            };

        private Sequence LowerRegion(
            RegionTree<Label, ShaderRegionBody> region,
            Label? enclosingNext) =>
            region.Definition.Kind switch
            {
                RegionKind.Block => LowerBlock(region, enclosingNext),
                RegionKind.Loop => LowerLoop(region, enclosingNext),
                _ => throw Error($"unknown region kind for '{region.Label.Name}'")
            };

        private Sequence LowerBlock(
            RegionTree<Label, ShaderRegionBody> region,
            Label? enclosingNext)
        {
            var next = region.Body.ImmediatePostDominator;
            var statements = ImmutableArray.CreateBuilder<SlangStatement>();
            foreach (var instruction in region.Body.Body.Elements)
                LowerInstruction(instruction, statements);
            var terminated = LowerTerminator(region.Body.Body.Last, region.Label, next);
            statements.AddRange(terminated.Statements);
            var canFallThrough = terminated.CanFallThrough;
            if (canFallThrough && next is not null && !IsCurrentLoopTransfer(next))
            {
                var continuation = Expand(next, new NormalTransfer(next, [region.Label]), enclosingNext);
                statements.AddRange(continuation.Statements);
                canFallThrough = continuation.CanFallThrough;
            }
            return new Sequence(
                [(SlangStatement)new SlangScope(region.Label, new SlangBlock(statements.ToImmutable()))],
                canFallThrough);
        }

        private Sequence LowerLoop(
            RegionTree<Label, ShaderRegionBody> region,
            Label? enclosingNext)
        {
            var next = region.Body.ImmediatePostDominator;
            var normalTransfer = FindNormalTransfer(region);
            var statements = ImmutableArray.CreateBuilder<SlangStatement>();
            loopOwners.Push(new LoopOwner(region.Label, normalTransfer));
            try
            {
                foreach (var instruction in region.Body.Body.Elements)
                    LowerInstruction(instruction, statements);
                var terminated = LowerTerminator(region.Body.Body.Last, region.Label, next);
                statements.AddRange(terminated.Statements);
                if (terminated.CanFallThrough && next is not null && !IsCurrentLoopTransfer(next))
                    statements.AddRange(Expand(
                        next,
                        new NormalTransfer(next, [region.Label]),
                        enclosingNext).Statements);
            }
            finally
            {
                loopOwners.Pop();
            }

            var result = new Sequence(
                [(SlangStatement)new SlangLoop(region.Label, new SlangBlock(statements.ToImmutable()))],
                normalTransfer is not null);
            return normalTransfer is null
                ? result
                : result.Then(Expand(normalTransfer.Target, normalTransfer, enclosingNext));
        }

        private Sequence LowerTerminator(
            ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
            Label sourceLabel,
            Label? next) =>
            terminator switch
            {
                Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> =>
                    Sequence.Completed(new SlangReturnVoid()),
                Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned =>
                    Sequence.Completed(new SlangReturnValue(Operand(returned.Expr))),
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch =>
                    Expand(branch.Target.Label, new NormalTransfer(branch.Target.Label, [sourceLabel]), next),
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    LowerConditional(branch, sourceLabel, next),
                _ => throw Error($"unsupported terminator in block '{sourceLabel.Name}'")
            };

        private Sequence LowerConditional(
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch,
            Label sourceLabel,
            Label? next)
        {
            var whenTrue = Expand(
                branch.TrueTarget.Label,
                new NormalTransfer(branch.TrueTarget.Label, [sourceLabel]),
                next);
            var whenFalse = Expand(
                branch.FalseTarget.Label,
                new NormalTransfer(branch.FalseTarget.Label, [sourceLabel]),
                next);
            return new Sequence(
                [
                    new SlangIf(
                        Operand(branch.Condition),
                        new SlangBlock(whenTrue.Statements),
                        new SlangBlock(whenFalse.Statements))
                ],
                whenTrue.CanFallThrough || whenFalse.CanFallThrough);
        }

        private Sequence Expand(Label target, NormalTransfer transfer, Label? next)
        {
            if (loopOwners.TryPeek(out var owner))
            {
                if (owner.Header.Equals(target))
                    return Sequence.Completed(new SlangContinue());
                if (owner.NormalTransfer?.Target.Equals(target) ?? false)
                    return Sequence.Completed(new SlangBreak());
            }

            var terminal = blocks[target].Body.Successor is TerminateSuccessor;
            if (!(loopOwners.Count > 0 && terminal) && next is not null && target.Equals(next))
                return Sequence.FallThrough;
            if (active.Contains(target))
                throw UnsupportedTransfer(transfer, "an unowned expansion cycle was detected");
            if (!terminal && TryLowerLoopTransferContinuation(target, out var resolved))
                return resolved;
            if (!terminal && next is not null && TryLowerEdgeContinuation(target, next, out resolved))
                return resolved;
            if (!terminal && !placedNonterminals.Add(target))
                throw UnsupportedTransfer(
                    transfer,
                    $"the nonterminal target already has an executable AST placement " +
                    $"(lexical next: {next?.ToString() ?? "<exit>"}, " +
                    $"loop owners: {string.Join(" > ", loopOwners.Select(loop => loop.Header))})");

            active.Add(target);
            try
            {
                return LowerRegion(blocks[target], next);
            }
            finally
            {
                active.Remove(target);
            }
        }

        private bool TryLowerEdgeContinuation(Label target, Label next, out Sequence result)
        {
            var region = blocks[target];
            if (region.Definition.Kind != RegionKind.Block ||
                region.Body.Body.Last is not Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch ||
                !branch.Target.Arguments.IsEmpty ||
                !branch.Target.Label.Equals(next))
            {
                result = default;
                return false;
            }

            active.Add(target);
            try
            {
                var statements = ImmutableArray.CreateBuilder<SlangStatement>();
                foreach (var instruction in region.Body.Body.Elements)
                    LowerInstruction(instruction, statements);
                result = new Sequence(
                    [(SlangStatement)new SlangScope(target, new SlangBlock(statements.ToImmutable()))],
                    true);
                return true;
            }
            finally
            {
                active.Remove(target);
            }
        }

        private bool TryLowerLoopTransferContinuation(Label target, out Sequence result)
        {
            var region = blocks[target];
            if (region.Definition.Kind != RegionKind.Block ||
                region.Body.Body.Last is not Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch ||
                !branch.Target.Arguments.IsEmpty)
            {
                result = default;
                return false;
            }
            if (!loopOwners.TryPeek(out var owner))
            {
                result = default;
                return false;
            }

            SlangStatement? transfer = branch.Target.Label switch
            {
                var label when label.Equals(owner.Header) => new SlangContinue(),
                var label when owner.NormalTransfer?.Target.Equals(label) ?? false => new SlangBreak(),
                _ => null
            };
            if (transfer is null)
            {
                result = default;
                return false;
            }

            active.Add(target);
            try
            {
                var statements = ImmutableArray.CreateBuilder<SlangStatement>();
                foreach (var instruction in region.Body.Body.Elements)
                    LowerInstruction(instruction, statements);
                statements.Add(transfer);
                result = Sequence.Completed(
                    new SlangScope(target, new SlangBlock(statements.ToImmutable())));
                return true;
            }
            finally
            {
                active.Remove(target);
            }
        }

        private void LowerInstruction(
            Instruction<IShaderValue, IShaderValue> instruction,
            ImmutableArray<SlangStatement>.Builder statements)
        {
            switch (instruction.Operation)
            {
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
            if (lowered.Result is null)
                statements.Add(new SlangEffect(lowered));
            else
            {
                if (lowered.Result.Type is IPtrType)
                    throw UnsupportedOperation(instruction, "pointer results must lower to typed places");
                statements.Add(new SlangBind(lowered));
                if (captures.TryGetValue(lowered.Result, out var capture))
                    statements.Add(new SlangAssign(
                        new SlangVariablePlace(capture),
                        new SlangValueOperand(lowered.Result)));
            }
        }

        private static bool IsSupportedExpression(IOperation operation) =>
            operation is NopOperation
                or LoadOperation
                or CallOperation
                or LiteralOperation
                or IUnaryExpressionOperation
                or IBinaryExpressionOperation
                or VectorCompositeConstructionOperation
                or ZeroConstructorOperation;

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

        private NormalTransfer? FindNormalTransfer(RegionTree<Label, ShaderRegionBody> region)
        {
            HashSet<Label> regionLabels = [region.Label, .. region.Bindings.SelectMany(child => child.DefinedLabels())];
            var transfers = regionLabels
                .SelectMany(sourceLabel => blocks[sourceLabel].Body.Successor.AllTargets()
                    .Where(target => !regionLabels.Contains(target) &&
                                     blocks[target].Body.Successor is not TerminateSuccessor)
                    .Select(target => (Source: sourceLabel, Target: target)))
                .GroupBy(transfer => transfer.Target)
                .Select(group => new NormalTransfer(
                    group.Key,
                    [.. group.Select(transfer => transfer.Source)
                        .Distinct()
                        .OrderBy(label => label.ToString())]))
                .OrderBy(transfer => transfer.Target.ToString())
                .ToArray();
            return transfers switch
            {
                [] => null,
                [var transfer] => transfer,
                _ => throw Error(
                    $"unsupported loop transfers from '{region.Label.Name}': " +
                    string.Join("; ", transfers.Select(FormatTransfer)))
            };
        }

        private bool IsCurrentLoopTransfer(Label target) =>
            loopOwners.TryPeek(out var owner) &&
            (owner.Header.Equals(target) || (owner.NormalTransfer?.Target.Equals(target) ?? false));

        private NotSupportedException UnsupportedOperation(
            Instruction<IShaderValue, IShaderValue> instruction,
            string reason) =>
            Error($"operation '{instruction.Operation.Name}': {reason}");

        private NotSupportedException UnsupportedTransfer(NormalTransfer transfer, string reason) =>
            Error($"unsupported transfer {FormatTransfer(transfer)}: {reason}");

        private static string FormatTransfer(NormalTransfer transfer) =>
            $"from [{string.Join(", ", transfer.Sources)}] to {transfer.Target}";

        private NotSupportedException Error(string message) =>
            new($"Function '{source.Declaration.Name}': {message}.");

        private readonly record struct Sequence(
            ImmutableArray<SlangStatement> Statements,
            bool CanFallThrough)
        {
            public static Sequence FallThrough { get; } = new([], true);

            public static Sequence Completed(SlangStatement statement) => new([statement], false);

            public Sequence Then(Sequence next) =>
                CanFallThrough
                    ? new Sequence([.. Statements, .. next.Statements], next.CanFallThrough)
                    : this;
        }

        private sealed record NormalTransfer(Label Target, ImmutableArray<Label> Sources);
        private readonly record struct LoopOwner(Label Header, NormalTransfer? NormalTransfer);
    }
}
