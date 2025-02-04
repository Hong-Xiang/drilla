using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language;

public sealed record class UnaryArithmeticOperatorDefinition(
    UnaryArithmeticOp Op,
    IShaderType Source,
    IShaderType Result)
{
}

public sealed record class BinaryArithmeticOperatorDefinition(
    BinaryArithmetic.OpKind Op,
    IShaderType Left,
    IShaderType Right,
    IShaderType Result)
{
}

public sealed record class UnaryLogicalOperatorDefinition(UnaryLogicalOp Op, IShaderType Source, IShaderType Result)
{
}

public static class ShaderOperator
{
    public static readonly ImmutableArray<UnaryArithmeticOperatorDefinition> UnaryArithmeticOperatorDefinitions = [
        ..from s in ShaderType.NumericScalarTypes
          from t in ShaderType.GetScalarOrVectorTypes(s)
          select new UnaryArithmeticOperatorDefinition(UnaryArithmeticOp.Minus, t, t)
    ];

    static IEnumerable<BinaryArithmetic.OpKind> BinaryArithmeticWithOperator =>
        [BinaryArithmetic.OpKind.add,
         BinaryArithmetic.OpKind.sub,
         BinaryArithmetic.OpKind.mul,
         BinaryArithmetic.OpKind.div,
         BinaryArithmetic.OpKind.rem];
    public static readonly ImmutableArray<BinaryArithmeticOperatorDefinition> BinaryArithmeticOperatorDefinitions = [
        ..from o in BinaryArithmeticWithOperator
          from s in ShaderType.NumericScalarTypes
          from t in ShaderType.GetScalarOrVectorTypes(s)
          select new BinaryArithmeticOperatorDefinition(o, t, t, t),
        ..from o in BinaryArithmeticWithOperator
          from s in ShaderType.NumericScalarTypes
          from v in ShaderType.GetVecTypes(s)
          from d in (BinaryArithmeticOperatorDefinition[])[
            new BinaryArithmeticOperatorDefinition(o, s, v, v),
            new BinaryArithmeticOperatorDefinition(o, v, s, v)]
          select d,
    ];
}
