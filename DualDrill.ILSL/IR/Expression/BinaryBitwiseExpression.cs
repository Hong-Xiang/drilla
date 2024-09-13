namespace DualDrill.ILSL.IR.Expression;

public enum BinaryBitwiseOp
{
    BitwiseOr,
    BitwiseAnd,
    BitwiseExclusiveOr
}

public sealed record class BinaryBitwiseExpression(
    IExpression L,
    IExpression R,
    BinaryBitwiseOp Op
) : IExpression
{
}
