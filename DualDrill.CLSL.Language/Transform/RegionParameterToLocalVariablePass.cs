using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Transform;

public sealed class RegionParameterToLocalVariablePass : IShaderModuleSimplePass
{
    public IDeclaration? VisitFunction(FunctionDeclaration decl) => decl;

    private static FunctionBody4 EliminatePointerParameters(FunctionBody4 body)
    {
        var blocks = body.Body.Fold(new RegionBodiesSemantic()).ToImmutableDictionary(b => b.Label);
        var jumps = blocks.Values
            .SelectMany(block => block.Body.Last.Evaluate(new JumpsSemantic()))
            .ToImmutableArray();

        var incoming = jumps.ToLookup(jump => jump.Label);
        var constraints = blocks.Values.SelectMany(block =>
                block.Parameters.Select((parameter, index) => (Parameter: parameter,
                    Constraint: new PointerConstraint(block.Label, index,
                        [.. incoming[block.Label].Select(jump => jump.Arguments[index])]))))
            .Where(item => item.Parameter.Type is IPtrType)
            .ToImmutableDictionary(item => item.Parameter, item => item.Constraint);
        var replacements = constraints.ToImmutableDictionary(
            item => item.Key,
            item => ResolvePointer(item.Key, constraints, body.Declaration.Name));

        if (replacements.IsEmpty) return body;

        return body.MapRegionBody(block => block with
        {
            Parameters = [.. block.Parameters.Where(p => p.Type is not IPtrType)],
            Body = Seq.Create(block.Body.Elements, block.Body.Last.Select(
                jump => new RegionJump<IShaderValue>(jump.Label,
                    [.. jump.Arguments.Where((_, index) => blocks[jump.Label].Parameters[index].Type is not IPtrType)]),
                static value => value))
        }).MapValueUse(value => replacements.TryGetValue(value, out var root) ? root : value);
    }

    private sealed record PointerConstraint(Label Block, int Index, ImmutableArray<IShaderValue> Sources);

    private static IShaderValue ResolvePointer(IShaderValue parameter,
        ImmutableDictionary<IShaderValue, PointerConstraint> constraints, string function)
    {
        var origin = constraints[parameter];
        NotSupportedException Unsupported(string reason) =>
            new($"Function '{function}', block '{origin.Block.Name}', pointer parameter {origin.Index}: {reason}.");

        var pending = new Stack<IShaderValue>();
        var visited = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
        IShaderValue? root = null;
        pending.Push(parameter);
        // Follow dependencies, not tentative aliases: a back edge cannot establish an address.
        while (pending.TryPop(out var value))
        {
            if (!visited.Add(value)) continue;
            if (constraints.TryGetValue(value, out var constraint))
            {
                if (constraint.Sources.IsEmpty) throw Unsupported("no incoming address");
                foreach (var source in constraint.Sources) pending.Push(source);
            }
            else if (value is ParameterPointerValue or VariablePointerValue)
            {
                if (root is not null && !ReferenceEquals(root, value))
                    throw Unsupported("multiple stable addresses");
                root = value;
            }
            else
            {
                throw Unsupported("unsupported pointer source");
            }
        }

        var grounded = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var value in visited)
            {
                if (grounded.Contains(value))
                    continue;
                if (!constraints.TryGetValue(value, out var constraint) ||
                    constraint.Sources.Any(source =>
                        source is ParameterPointerValue or VariablePointerValue || grounded.Contains(source)))
                    changed |= grounded.Add(value);
            }
        }

        if (visited.Any(value => constraints.ContainsKey(value) && !grounded.Contains(value)))
            throw Unsupported("no stable address");
        return root ?? throw Unsupported("no stable address");
    }

    private sealed class JumpsSemantic
        : ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue, ImmutableArray<RegionJump<IShaderValue>>>
    {
        public ImmutableArray<RegionJump<IShaderValue>> Br(RegionJump<IShaderValue> target) => [target];
        public ImmutableArray<RegionJump<IShaderValue>> BrIf(IShaderValue condition,
            RegionJump<IShaderValue> trueTarget, RegionJump<IShaderValue> falseTarget) =>
            [trueTarget, falseTarget];
        public ImmutableArray<RegionJump<IShaderValue>> ReturnExpr(IShaderValue expr) => [];
        public ImmutableArray<RegionJump<IShaderValue>> ReturnVoid() => [];
    }

    private sealed class RegionBodiesSemantic
        : IRegionTreeFoldSemantic<Label, ShaderRegionBody, ImmutableArray<ShaderRegionBody>, ImmutableArray<ShaderRegionBody>>
    {
        public ImmutableArray<ShaderRegionBody> Block(Label label, Func<ImmutableArray<ShaderRegionBody>> body,
            Label? next) => body();
        public ImmutableArray<ShaderRegionBody> Loop(Label label, Func<ImmutableArray<ShaderRegionBody>> body,
            Label? next, Label? breakNext) => body();
        public ImmutableArray<ShaderRegionBody> Nested(ImmutableArray<ShaderRegionBody> head,
            Func<ImmutableArray<ShaderRegionBody>> next) => [.. head, .. next()];
        public ImmutableArray<ShaderRegionBody> Single(ShaderRegionBody value) => [value];
    }

    public FunctionBody4 VisitFunctionBody(FunctionBody4 body)
    {
        body = EliminatePointerParameters(body);
        var regionParameters = body.Body.Fold(new RegionBodiesSemantic())
            .ToImmutableDictionary(block => block.Label, block => block.Parameters);
        if (regionParameters.Values.All(parameters => parameters.IsEmpty)) return body;
        var parameterVariables = regionParameters.Values
                                                 .SelectMany(p => p)
                                                 .ToDictionary(x => x,
                                                     x => new VariableDeclaration(FunctionAddressSpace.Instance,
                                                         string.Empty, x.Type, []));
        var parameterUses =
            parameterVariables.ToDictionary(x => x.Key, x => (IShaderValue)ShaderValue.Intermediate(x.Key.Type));
        return body.MapRegionBody(bb =>
        {
            return bb with
            {
                Parameters = [],
                Body = Seq.Create(
                    [
                        .. bb.Parameters.Select(p => Instruction.Instruction.Factory.Load(default, new LoadOperation(),
                            parameterUses[p], parameterVariables[p].Value)),
                        .. bb.Body.Elements,
                        .. bb.Body.Last.Evaluate(new JumpStoreSemantic(regionParameters, parameterVariables))
                    ],
                    bb.Body.Last.Select(static jump => new RegionJump<IShaderValue>(jump.Label, []),
                        static value => value)
                )
            };
        }).MapValueUse(v =>
        {
            if (parameterUses.TryGetValue(v, out var lv)) return lv;

            return v;
        });
    }

    public IDeclaration? VisitMember(MemberDeclaration decl) => decl;

    public IDeclaration? VisitParameter(ParameterDeclaration decl) => decl;

    public IDeclaration? VisitStructure(StructureDeclaration decl) => decl;

    public IDeclaration? VisitValue(ValueDeclaration decl) => decl;

    public IDeclaration? VisitVariable(VariableDeclaration decl) => decl;

    private sealed record class JumpStoreSemantic(
        IReadOnlyDictionary<Label, ImmutableArray<IShaderValue>> Parameters,
        IReadOnlyDictionary<IShaderValue, VariableDeclaration> ParameterVars
    ) : ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
        IEnumerable<Instruction<IShaderValue, IShaderValue>>>
    {
        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Br(RegionJump<IShaderValue> target) =>
            Parameters[target.Label].Zip(target.Arguments, StoreLocalVar);


        public IEnumerable<Instruction<IShaderValue, IShaderValue>> BrIf(IShaderValue condition,
            RegionJump<IShaderValue> trueTarget, RegionJump<IShaderValue> falseTarget)
        {
            if (trueTarget.Label == falseTarget.Label)
            {
                if (!trueTarget.Arguments.SequenceEqual(falseTarget.Arguments))
                    throw new NotSupportedException(
                        $"Conditional arguments to shared target '{trueTarget.Label.Name}' require edge-specific lowering.");
                return Br(trueTarget);
            }

            return [.. Br(trueTarget), .. Br(falseTarget)];
        }

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReturnExpr(IShaderValue expr) => [];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReturnVoid() => [];

        private Instruction<IShaderValue, IShaderValue> StoreLocalVar(IShaderValue p, IShaderValue a) =>
            Instruction.Instruction.Factory.Store(default, new StoreOperation(), ParameterVars[p].Value, a);
    }
}