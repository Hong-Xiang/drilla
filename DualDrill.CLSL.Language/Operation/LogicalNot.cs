using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public sealed class LogicalNot
    : IUnaryOp<LogicalNot>
    , ISymbolOp<LogicalNot>
{
    public string Name => "not";
    public string Symbol => "!";
    public static LogicalNot Instance { get; } = new();
}

public sealed record class LogicalNotOperation
    : IUnaryExpressionOperation<LogicalNotOperation, BoolType, BoolType, LogicalNot>
{
    public string Name => LogicalNot.Instance.Name;
    public static LogicalNotOperation Instance => new();
    public override string ToString() => Name;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<LogicalNotOperation> expr)
        => visitor.VisitUnaryExpression<LogicalNotOperation, BoolType, BoolType, LogicalNot>(expr);
}