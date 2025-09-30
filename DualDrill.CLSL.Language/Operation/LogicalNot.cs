using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class LogicalNot
    : IUnaryOp<LogicalNot>
    , ISymbolOp<LogicalNot>
{
    public string Symbol => "!";
    public string Name => "not";
    public static LogicalNot Instance { get; } = new();
}

public sealed record class LogicalNotOperation
    : IUnaryExpressionOperation<LogicalNotOperation, BoolType, BoolType, LogicalNot>
{
    public string Name => LogicalNot.Instance.Name;
    public static LogicalNotOperation Instance => new();

 

    public override string ToString() => Name;
}