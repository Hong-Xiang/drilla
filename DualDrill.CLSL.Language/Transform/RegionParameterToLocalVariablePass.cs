using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Transform;

public sealed class RegionParameterToLocalVariablePass : IShaderModuleSimplePass
{
    public IDeclaration? VisitFunction(FunctionDeclaration decl) => decl;

    public FunctionBody4 VisitFunctionBody(FunctionBody4 body)
    {
        var model = new SemanticModel(body);
        Dictionary<Label, ImmutableArray<IShaderValue>> regionParamters = [];
        body.Body.Traverse((t, l, b) =>
        {
            regionParamters.Add(l, b.Parameters);
            return false;
        });
        var regionParamtersVars = regionParamters.Values
                                                 .SelectMany(p => p)
                                                 .ToDictionary(x => x,
                                                     x => new VariableDeclaration(FunctionAddressSpace.Instance,
                                                         string.Empty, x.Type, []));
        var regionParamtersUses =
            regionParamtersVars.ToDictionary(x => x.Key, x => (IShaderValue)BindShaderValue.Create(x.Key.Type));
        return body.MapRegionBody(bb =>
        {
            return bb with
            {
                Parameters = [],
                Body = Seq.Create(
                    [
                        .. bb.Parameters.Select(p => Instruction2.Factory.Load(default, new LoadOperation(),
                            regionParamtersUses[p], regionParamtersVars[p].Value)),
                        .. bb.Body.Elements,
                        .. bb.Body.Last.Evaluate(new JumpStoreSemantic(regionParamters, regionParamtersVars))
                    ],
                    bb.Body.Last
                )
            };
        }).MapValueUse(v =>
        {
            if (regionParamtersUses.TryGetValue(v, out var lv)) return lv;

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
    ) : ITerminatorSemantic<RegionJump, IShaderValue, IEnumerable<Instruction<IShaderValue, IShaderValue>>>
    {
        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Br(RegionJump target) =>
            Parameters[target.Label].Zip(target.Arguments, StoreLocalVar);


        public IEnumerable<Instruction<IShaderValue, IShaderValue>> BrIf(IShaderValue condition, RegionJump trueTarget,
            RegionJump falseTarget) =>
        [
            .. Parameters[trueTarget.Label].Zip(trueTarget.Arguments, StoreLocalVar),
            .. Parameters[falseTarget.Label].Zip(falseTarget.Arguments, StoreLocalVar)
        ];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReturnExpr(IShaderValue expr) => [];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReturnVoid() => [];

        private Instruction<IShaderValue, IShaderValue> StoreLocalVar(IShaderValue p, IShaderValue a) =>
            Instruction2.Factory.Store(default, new StoreOperation(), ParameterVars[p].Value, a);
    }
}