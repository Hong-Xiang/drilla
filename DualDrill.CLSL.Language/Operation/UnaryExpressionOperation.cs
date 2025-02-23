using System.Diagnostics;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IUnaryExpressionOperation
{
    IShaderType SourceType { get; }
    IShaderType ResultType { get; }
    IUnaryExpression CreateExpression(IExpression expr);
}

public interface IUnaryOperation<TSelf> : IUnaryExpressionOperation, IOperation<TSelf>
    where TSelf : IUnaryOperation<TSelf>
{
    IInstruction IOperation.Instruction => UnaryExpressionOperationInstruction<TSelf>.Instance;

    TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor, UnaryOperationExpression<TSelf> expr);

    static readonly FunctionDeclaration OperationFunction = new(
        TSelf.Instance.Name,
        [
            new ParameterDeclaration("value", TSelf.Instance.SourceType, []),
        ],
        new FunctionReturn(TSelf.Instance.ResultType, []),
        [new OperationMethodAttribute<TSelf>()]
    );

    FunctionDeclaration IOperation.Function => OperationFunction;

    IUnaryExpression IUnaryExpressionOperation.CreateExpression(IExpression expr) =>
        new UnaryOperationExpression<TSelf>(expr);
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
    string IOperation.Name => $"{TOp.Instance.Name}.{TSourceType.Instance.Name}.{TResultType.Instance.Name}";
}