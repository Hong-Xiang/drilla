using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class VectorCompositeConstructionOperation : IOperation
{
    public static readonly FrozenDictionary<FunctionType, VectorCompositeConstructionOperation> Operations =
        GetAllOperations();

    private VectorCompositeConstructionOperation(IRank size, IScalarType elementType,
        ImmutableArray<int> parameterPattern)
    {
        Size = size;
        ElementType = elementType;
        ResultType = ShaderType.GetVecType(size, elementType);
        List<IShaderAttribute> attrs =
            [new ShaderRuntimeMethodAttribute(), new VectorCompositeConstructorMethodAttribute()];
        if (parameterPattern.Length == size.Value) attrs.Add(new CompositeConstructorMethodAttribute());
        Function = new FunctionDeclaration(
            $"vec{size.Value}",
            [
                ..parameterPattern.Select((p, i) =>
                {
                    var t = p switch
                    {
                        1 => new ParameterDeclaration($"e{i}", ElementType, []),
                        2 => new ParameterDeclaration($"e{i}", ShaderType.GetVecType(N2.Instance, ElementType), []),
                        3 => new ParameterDeclaration($"e{i}", ShaderType.GetVecType(N3.Instance, ElementType), []),
                        _ => throw new NotSupportedException()
                    };
                    return t;
                })
            ],
            new FunctionReturn(ResultType, []),
            [.. attrs]);
        ParameterTypes = [.. Function.Parameters.Select(p => p.Type)];
    }

    public IShaderType ResultType { get; }

    public ImmutableArray<IShaderType> ParameterTypes { get; }

    public IRank Size { get; }
    public IScalarType ElementType { get; }

    public FunctionDeclaration Function { get; }

    public string Name => $"{ResultType.Name}.ctor({string.Join(',', ParameterTypes.Select(t => t.Name))})";


    public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotImplementedException();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.VectorCompositeConstruction(inst, this, inst.Result, [..inst.Operands]);

    private static FrozenDictionary<FunctionType, VectorCompositeConstructionOperation> GetAllOperations()
    {
        Dictionary<FunctionType, VectorCompositeConstructionOperation> ops = [];

        static IEnumerable<ImmutableArray<int>> ParameterPattern(int rank)
        {
            return rank switch
            {
                2 => [[1, 1]],
                3 => [[1, 1, 1], [1, 2], [2, 1]],
                4 => [[1, 1, 1, 1], [1, 1, 2], [1, 2, 1], [2, 1, 1], [1, 3], [3, 1], [2, 2]],
                _ => throw new NotSupportedException()
            };
        }

        foreach (var v in ShaderType.GetVecTypes())
        foreach (var p in ParameterPattern(v.Size.Value))
        {
            var op = new VectorCompositeConstructionOperation(v.Size, v.ElementType, p);
            ops.Add((FunctionType)op.Function.Type, op);
        }

        return ops.ToFrozenDictionary();
    }

    public static VectorCompositeConstructionOperation Get(
        IVecType resultType,
        IEnumerable<IShaderType> parameters)
    {
        var ps = parameters.ToImmutableArray();
        var v = resultType;
        var r = v.Size;
        var e = v.ElementType;
        var dims = 0;
        foreach (var p in ps)
        {
            if (p is IScalarType s && s.Equals(e))
            {
                dims += 1;
                continue;
            }

            if (p is IVecType vp && vp.ElementType.Equals(e))
            {
                dims += vp.Size.Value;
                continue;
            }

            throw new ArgumentException("Invalid parameter type", nameof(parameters));
        }

        if (dims != r.Value) throw new ArgumentException("Invalid parameter total dimension count", nameof(parameters));
        var ft = new FunctionType(ps, resultType);
        return Operations[ft];
    }

    public override string ToString() => $"op.{Name}";


    private string GetDebuggerDisplay() => ToString();
}