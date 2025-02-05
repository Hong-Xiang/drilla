﻿using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class BinaryExpression<TOperation, TOp>(
    IExpression Left,
    IExpression Right
) : IExpression
    where TOperation : IBinaryOperation<TOperation>
    where TOp : ISymbolOp<TOp>
{
    public IShaderType Type => TOperation.Instance.ResultType;

    TResult IExpression.Accept<TResult>(IExpressionVisitor<TResult> visitor)
    {
        return visitor.VisitBinaryExpression(this);
    }
}
