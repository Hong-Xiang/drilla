using System.Diagnostics;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IUnaryExpressionOperation
{
    IShaderType SourceType { get; }
    IShaderType ResultType { get; }
    IExpression CreateExpression(IExpression expr);
}

public interface IUnaryOperation<TSelf> : IUnaryExpressionOperation, IOperation<TSelf>
    where TSelf : IUnaryOperation<TSelf>
{
    IStructuredStackInstruction IOperation.Instruction => UnaryOperationInstruction<TSelf>.Instance;
}

public interface IUnaryExpressionOperation<TSelf, TSourceType, TResultType, TOp>
    : IUnaryOperation<TSelf>
    where TSelf : IUnaryExpressionOperation<TSelf, TSourceType, TResultType, TOp>
    where TSourceType : ISingletonShaderType<TSourceType>
    where TResultType : ISingletonShaderType<TResultType>
    where TOp : IUnaryOp<TOp>
{
    IShaderType IUnaryExpressionOperation.SourceType => TSourceType.Instance;
    IShaderType IUnaryExpressionOperation.ResultType => TResultType.Instance;
    FunctionDeclaration IOperation.Function => OperationFunction;

    static readonly FunctionDeclaration OperationFunction = new(
        TSelf.Instance.Name,
        [
            new ParameterDeclaration("value", TSourceType.Instance, []),
        ],
        new FunctionReturn(TResultType.Instance, []),
        []
    );

    string IOperation.Name => $"{TOp.Instance.Name}.{TSourceType.Instance.Name}.{TResultType.Instance.Name}";

    IExpression IUnaryExpressionOperation.CreateExpression(IExpression expr) => new Expression(expr);
    TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor, Expression expr);


    public sealed record class Expression
        : IExpression
    {
        public Expression(IExpression expr)
        {
            Debug.Assert(expr.Type.Equals(TResultType.Instance));
            Expr = expr;
        }

        public IExpression Expr { get; }
        public IShaderType Type => TResultType.Instance;

        public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
            => TSelf.Instance.EvaluateExpression(visitor, this);

        public IEnumerable<IStructuredStackInstruction> ToInstructions()
            => [..Expr.ToInstructions(), TSelf.Instance.Instruction];

        public IEnumerable<VariableDeclaration> ReferencedVariables => Expr.ReferencedVariables;
    }
}