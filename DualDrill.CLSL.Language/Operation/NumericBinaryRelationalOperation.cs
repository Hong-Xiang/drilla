using System.Diagnostics;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface INumericBinaryRelationalOperation : IBinaryExpressionOperation
{
}

public sealed class NumericBinaryRelationalOperation<TType, TOp>
    : IBinaryExpressionOperation<NumericBinaryRelationalOperation<TType, TOp>, TType, TType, BoolType, TOp>
    , INumericBinaryRelationalOperation
    where TType : INumericType<TType>
    where TOp : BinaryRelational.IOp<TOp>

{
    public string Name => $"{TOp.Instance.Name}.{TType.Instance.Name}";
    public static NumericBinaryRelationalOperation<TType, TOp> Instance { get; } = new();
}