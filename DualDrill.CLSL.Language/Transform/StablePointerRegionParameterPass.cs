using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Transform;

public sealed class StablePointerRegionParameterPass : IShaderModuleSimplePass
{
    public RegionFunctionBody VisitFunctionBody(RegionFunctionBody body)
    {
        var blocks = body.Body.Fold(new RegionBodiesSemantic()).ToImmutableDictionary(block => block.Label);
        var jumps = blocks.Values
            .SelectMany(block => block.Body.Last.Evaluate(new JumpsSemantic()))
            .ToImmutableArray();
        var incoming = jumps.ToLookup(jump => jump.Label);
        var constraints = blocks.Values.SelectMany(block =>
                block.Parameters.Select((parameter, index) => (
                    Parameter: parameter,
                    Constraint: new PointerConstraint(
                        block.Label,
                        index,
                        [.. incoming[block.Label].Select(jump => jump.Arguments[index])]))))
            .Where(item => item.Parameter.Type is IPtrType)
            .ToImmutableDictionary(item => item.Parameter, item => item.Constraint);
        var replacements = constraints.ToImmutableDictionary(
            item => item.Key,
            item => ResolvePointer(item.Key, constraints, body.Declaration.Name));

        if (replacements.IsEmpty) return body;

        return body.MapRegionBody(block => block with
        {
            Parameters = [.. block.Parameters.Where(parameter => parameter.Type is not IPtrType)],
            Body = Seq.Create(
                block.Body.Elements,
                block.Body.Last.Select(
                    jump => new RegionJump<IShaderValue>(
                        jump.Label,
                        [.. jump.Arguments.Where((_, index) =>
                            blocks[jump.Label].Parameters[index].Type is not IPtrType)]),
                    static value => value))
        }).MapValueUse(value => replacements.TryGetValue(value, out var root) ? root : value);
    }

    private sealed record PointerConstraint(
        Label Block,
        int Index,
        ImmutableArray<IShaderValue> Sources);

    private static IShaderValue ResolvePointer(
        IShaderValue parameter,
        ImmutableDictionary<IShaderValue, PointerConstraint> constraints,
        string function)
    {
        var origin = constraints[parameter];
        NotSupportedException Unsupported(string reason) =>
            new($"Function '{function}', block '{origin.Block.Name}', pointer parameter {origin.Index}: {reason}.");

        var pending = new Stack<IShaderValue>();
        var visited = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
        IShaderValue? root = null;
        pending.Push(parameter);
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

        return root ?? throw Unsupported("no stable address");
    }

    private sealed class JumpsSemantic
        : ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue, ImmutableArray<RegionJump<IShaderValue>>>
    {
        public ImmutableArray<RegionJump<IShaderValue>> Br(RegionJump<IShaderValue> target) => [target];

        public ImmutableArray<RegionJump<IShaderValue>> BrIf(
            IShaderValue condition,
            RegionJump<IShaderValue> trueTarget,
            RegionJump<IShaderValue> falseTarget) =>
            [trueTarget, falseTarget];

        public ImmutableArray<RegionJump<IShaderValue>> ReturnExpr(IShaderValue expr) => [];
        public ImmutableArray<RegionJump<IShaderValue>> ReturnVoid() => [];
    }

    private sealed class RegionBodiesSemantic
        : IRegionTreeFoldSemantic<
            Label,
            ShaderRegionBody,
            ImmutableArray<ShaderRegionBody>,
            ImmutableArray<ShaderRegionBody>>
    {
        public ImmutableArray<ShaderRegionBody> Block(
            Label label,
            Func<ImmutableArray<ShaderRegionBody>> body,
            Label? next) =>
            body();

        public ImmutableArray<ShaderRegionBody> Loop(
            Label label,
            Func<ImmutableArray<ShaderRegionBody>> body,
            Label? next,
            Label? breakNext) =>
            body();

        public ImmutableArray<ShaderRegionBody> Nested(
            ImmutableArray<ShaderRegionBody> head,
            Func<ImmutableArray<ShaderRegionBody>> next) =>
            [.. head, .. next()];

        public ImmutableArray<ShaderRegionBody> Single(ShaderRegionBody value) => [value];
    }

    public IDeclaration? VisitFunction(FunctionDeclaration decl) => decl;
    public IDeclaration? VisitMember(MemberDeclaration decl) => decl;
    public IDeclaration? VisitParameter(ParameterDeclaration decl) => decl;
    public IDeclaration? VisitStructure(StructureDeclaration decl) => decl;
    public IDeclaration? VisitValue(ValueDeclaration decl) => decl;
    public IDeclaration? VisitVariable(VariableDeclaration decl) => decl;
}
