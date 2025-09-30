using DualDrill.CLSL.Language.Types;

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
    public static NumericBinaryArithmeticOperation<TType, TOp> Instance { get; } = new();
    public string Name => $"{TOp.Instance.Name}.{TType.Instance.Name}";
}