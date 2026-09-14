using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Transform;

public sealed class RegionParameterToLocalVariablePass : IShaderModuleSimplePass
{
    public IDeclaration? VisitFunction(FunctionDeclaration decl) => decl;

    private static FunctionBody4 EliminatePointerParameters(FunctionBody4 body)
    {
        var blockParams = new Dictionary<Label, ImmutableArray<IShaderValue>>();
        body.Body.Traverse((t, l, b) =>
        {
            blockParams[l] = b.Parameters;
            return false;
        });

        var jumps = new List<(Label Source, RegionJump Jump)>();
        body.Body.Traverse((t, l, b) =>
        {
            b.Body.Last.Evaluate(new CollectJumpsSemantic(l, jumps));
            return false;
        });

        var resolvedPointers = new Dictionary<IShaderValue, IShaderValue>();

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (targetLabel, targetParams) in blockParams)
            {
                var incomingJumps = jumps.Where(j => j.Jump.Label == targetLabel).ToList();
                if (incomingJumps.Count == 0)
                    continue;

                for (var i = 0; i < targetParams.Length; i++)
                {
                    var param = targetParams[i];
                    if (param.Type is not IPtrType)
                        continue;
                    if (resolvedPointers.ContainsKey(param))
                        continue;

                    IShaderValue? candidate = null;
                    var allSame = true;

                    foreach (var (_, jump) in incomingJumps)
                    {
                        if (i >= jump.Arguments.Length)
                        {
                            allSame = false;
                            break;
                        }

                        var arg = jump.Arguments[i];
                        while (resolvedPointers.TryGetValue(arg, out var next))
                        {
                            arg = next;
                        }

                        if (candidate is null)
                        {
                            candidate = arg;
                        }
                        else if (!candidate.Equals(arg))
                        {
                            allSame = false;
                            break;
                        }
                    }

                    if (allSame && candidate is not null)
                    {
                        resolvedPointers[param] = candidate;
                        changed = true;
                    }
                }
            }
        }

        if (resolvedPointers.Count == 0)
            return body;

        var removedIndices = new Dictionary<Label, HashSet<int>>();
        foreach (var (label, targetParams) in blockParams)
        {
            var indices = new HashSet<int>();
            for (var i = 0; i < targetParams.Length; i++)
            {
                if (resolvedPointers.ContainsKey(targetParams[i]))
                {
                    indices.Add(i);
                }
            }
            if (indices.Count > 0)
            {
                removedIndices[label] = indices;
            }
        }

        return body.MapRegionBody(bb =>
        {
            var newParams = bb.Parameters
                              .Where((_, idx) => !removedIndices.TryGetValue(bb.Label, out var set) || !set.Contains(idx))
                              .ToImmutableArray();

            var newTerminator = bb.Body.Last.Select(
                jump =>
                {
                    if (removedIndices.TryGetValue(jump.Label, out var set))
                    {
                        var newArgs = jump.Arguments
                                          .Where((_, idx) => !set.Contains(idx))
                                          .Select(a => resolvedPointers.TryGetValue(a, out var r) ? r : a)
                                          .ToImmutableArray();
                        return new RegionJump(jump.Label, newArgs);
                    }
                    else
                    {
                        var newArgs = jump.Arguments
                                          .Select(a => resolvedPointers.TryGetValue(a, out var r) ? r : a)
                                          .ToImmutableArray();
                        return new RegionJump(jump.Label, newArgs);
                    }
                },
                v => resolvedPointers.TryGetValue(v, out var r) ? r : v
            );

            var newElements = bb.Body.Elements.Select(
                stmt => stmt.Select(
                    v => resolvedPointers.TryGetValue(v, out var r) ? r : v,
                    static d => d
                )
            );

            return bb with
            {
                Parameters = newParams,
                Body = Seq.Create(newElements, newTerminator)
            };
        });
    }

    private sealed class CollectJumpsSemantic(Label source, List<(Label Source, RegionJump Jump)> jumps)
        : ITerminatorSemantic<RegionJump, IShaderValue, Unit>
    {
        public Unit Br(RegionJump target)
        {
            jumps.Add((source, target));
            return default;
        }

        public Unit BrIf(IShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
        {
            jumps.Add((source, trueTarget));
            jumps.Add((source, falseTarget));
            return default;
        }

        public Unit ReturnExpr(IShaderValue expr) => default;
        public Unit ReturnVoid() => default;
    }

    public FunctionBody4 VisitFunctionBody(FunctionBody4 body)
    {
        body = EliminatePointerParameters(body);
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
            regionParamtersVars.ToDictionary(x => x.Key, x => (IShaderValue)ShaderValue.Intermediate(x.Key.Type));
        return body.MapRegionBody(bb =>
        {
            return bb with
            {
                Parameters = [],
                Body = Seq.Create(
                    [
                        .. bb.Parameters.Select(p => Instruction.Instruction.Factory.Load(default, new LoadOperation(),
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
            Instruction.Instruction.Factory.Store(default, new StoreOperation(), ParameterVars[p].Value, a);
    }
}