using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class LogicalBinaryOperation<TOp>
    : IBinaryExpressionOperation<LogicalBinaryOperation<TOp>, BoolType, BoolType, BoolType, TOp>
    where TOp : BinaryLogical.IOp<TOp>
{
    public string Name => TOp.Instance.Name;
    public static LogicalBinaryOperation<TOp> Instance { get; } = new();
}