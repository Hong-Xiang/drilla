using System.Diagnostics;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IBinaryExpressionOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
    public IShaderType ResultType { get; }
    public IExpression CreateExpression(IExpression l, IExpression r);
    public IBinaryOp BinaryOp { get; }
}

public interface IBinaryExpressionOperation<TSelf>
    : IBinaryExpressionOperation
    , IOperation<TSelf>
    where TSelf : IBinaryExpressionOperation<TSelf>
{
    IStructuredStackInstruction IOperation.Instruction =>
        BinaryExpressionOperationInstruction<TSelf>.Instance;
}

public interface IBinaryExpressionOperation<TSelf, TLeftType, TRightType, TResultType, TOp>
    : IBinaryExpressionOperation<TSelf>
    where TSelf : IBinaryExpressionOperation<TSelf, TLeftType, TRightType, TResultType, TOp>
    where TLeftType : ISingletonShaderType<TLeftType>
    where TRightType : ISingletonShaderType<TRightType>
    where TResultType : ISingletonShaderType<TResultType>
    where TOp : IBinaryOp<TOp>
{
    IShaderType IBinaryExpressionOperation.LeftType => TLeftType.Instance;
    IShaderType IBinaryExpressionOperation.RightType => TRightType.Instance;
    IShaderType IBinaryExpressionOperation.ResultType => TResultType.Instance;
    IExpression IBinaryExpressionOperation.CreateExpression(IExpression l, IExpression r) => new Expression(l, r);

    IBinaryOp IBinaryExpressionOperation.BinaryOp => TOp.Instance;

    string IOperation.Name =>
        $"{TOp.Instance.Name}.{TLeftType.Instance.Name}.{TRightType.Instance.Name}.{TResultType.Instance.Name}";

    public sealed record class Expression
        : IBinaryExpression
    {
        public Expression(IExpression left, IExpression right)
        {
            Debug.Assert(left.Type.Equals(TLeftType.Instance));
            Debug.Assert(right.Type.Equals(TRightType.Instance));
            L = left;
            R = right;
        }

        public IExpression L { get; }
        public IExpression R { get; }
        public IBinaryExpressionOperation Operation => TSelf.Instance;
        public IShaderType Type => TResultType.Instance;

        public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
            => visitor.VisitBinaryExpression(this);

        public IEnumerable<IStructuredStackInstruction> ToInstructions()
            =>
            [
                ..L.ToInstructions(),
                ..R.ToInstructions(),
                TSelf.Instance.Instruction
            ];

        public IEnumerable<VariableDeclaration> ReferencedVariables =>
        [
            ..L.ReferencedVariables,
            ..R.ReferencedVariables
        ];
    }

    FunctionDeclaration IOperation.Function => OperationFunction;

    static readonly FunctionDeclaration OperationFunction = new(
        TSelf.Instance.Name,
        [
            new ParameterDeclaration("l", TLeftType.Instance, []),
            new ParameterDeclaration("r", TRightType.Instance, []),
        ],
        new FunctionReturn(TResultType.Instance, []),
        [
            TSelf.Instance.GetOperationMethodAttribute()
        ]
    );
}