using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public interface INumericBinaryArithmeticOperation : IBinaryExpressionOperation
{
}

public sealed class NumericBinaryArithmeticOperation<TType, TOp>
    : INumericBinaryArithmeticOperation
    , IBinaryExpressionOperation<NumericBinaryArithmeticOperation<TType, TOp>, TType, TType, TType, TOp>
    where TType : INumericType<TType>
    where TOp : BinaryArithmetic.IOp<TOp>
{
    public string Name => $"{TOp.Instance.Name}.{TType.Instance.Name}";
    public static NumericBinaryArithmeticOperation<TType, TOp> Instance { get; } = new();
}