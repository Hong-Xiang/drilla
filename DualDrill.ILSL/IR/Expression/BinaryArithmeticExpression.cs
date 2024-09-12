namespace DualDrill.ILSL.IR.Expression;

public enum BinaryArithmeticOp
{
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Remainder
}

public sealed record class BinaryArithmeticExpression(
    IExpression L,
    IExpression R,
    BinaryArithmeticOp Op
) : IExpression
{
}
