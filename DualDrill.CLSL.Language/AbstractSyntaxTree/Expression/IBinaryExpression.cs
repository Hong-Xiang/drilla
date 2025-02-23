using System.Diagnostics;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public interface IBinaryExpression : IExpression
{
    public IExpression L { get; }
    public IExpression R { get; }
    public IBinaryExpressionOperation Operation { get; }
}

public sealed record class BinaryOperationExpression<TOperation>
    : IBinaryExpression
    where TOperation : IBinaryExpressionOperation<TOperation>
{
    public BinaryOperationExpression(IExpression left, IExpression right)
    {
        Debug.Assert(left.Type.Equals(TOperation.Instance.LeftType));
        Debug.Assert(right.Type.Equals(TOperation.Instance.RightType));
        L = left;
        R = right;
    }

    public IExpression L { get; }
    public IExpression R { get; }
    public IBinaryExpressionOperation Operation => TOperation.Instance;
    public IShaderType Type => TOperation.Instance.ResultType;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitBinaryExpression(this);

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
        =>
        [
            ..L.ToInstructions(),
            ..R.ToInstructions(),
            TOperation.Instance.Instruction
        ];

    public IEnumerable<VariableDeclaration> ReferencedVariables =>
    [
        ..L.ReferencedVariables,
        ..R.ReferencedVariables
    ];
}