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

    public RegionFunctionBody VisitFunctionBody(RegionFunctionBody body)
    {
        body = new StablePointerRegionParameterPass().VisitFunctionBody(body);
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

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Switch(
            IShaderValue selector,
            IReadOnlyList<RegionJump<IShaderValue>> caseTargets,
            RegionJump<IShaderValue> defaultTarget) =>
            throw new NotSupportedException(
                "Switch region arguments require selected-edge stores; eager region-parameter lowering cannot emit them safely.");

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReturnExpr(IShaderValue expr) => [];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReturnVoid() => [];

        private Instruction<IShaderValue, IShaderValue> StoreLocalVar(IShaderValue p, IShaderValue a) =>
            Instruction.Instruction.Factory.Store(default, new StoreOperation(), ParameterVars[p].Value, a);
    }
}