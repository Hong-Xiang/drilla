namespace DualDrill.CLSL.Language.Operation;

public static class Operation
{
    public static LogicalBinaryOperation<TOp> LogicalBinaryOperation<TOp>() where TOp : BinaryLogical.IOp<TOp> => new();
}