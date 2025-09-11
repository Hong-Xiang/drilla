using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IUnaryExpressionOperation : IOperation
{
    IShaderType SourceType { get; }
    IShaderType ResultType { get; }
    IUnaryExpression CreateExpression(IExpression expr);
    TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context);

    TO IOperation.EvaluateInstruction<TV, TR, TS, TO>(Instruction2<TV, TR> inst, TS semantic)
        => semantic.Operation1(inst, this, inst.Result, inst[0]);
}

public interface IUnaryExpressionOperationSemantic<in TX, out TO>
{
    TO OpOperation<TOperation, TSourceType, TResultType, TOp>(TX context, TOperation operation)
        where TOperation : IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>
        where TSourceType : ISingletonShaderType<TSourceType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IUnaryOp<TOp>;

    TO VectorComponentGet<TRank, TElement, TComponent>(TX context)
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>;

    TO VectorSwizzleGet<TRank, TElement, TPattern>(TX context)
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>
        where TPattern : Swizzle.IPattern<TPattern>;

    TO VectorFromScalarConstruct<TRank, TElement>(TX context, VectorFromScalarConstructOperation<TRank, TElement> operation)
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>;
}

public interface IUnaryExpressionOperation<TSelf> : IUnaryExpressionOperation, IOperation<TSelf>
    where TSelf : IUnaryExpressionOperation<TSelf>
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

public interface IUnaryExpressionOperation<TSelf, TSourceType, TResultType>
    : IUnaryExpressionOperation<TSelf>
    where TSelf : IUnaryExpressionOperation<TSelf, TSourceType, TResultType>
    where TSourceType : ISingletonShaderType<TSourceType>
    where TResultType : ISingletonShaderType<TResultType>
{
}

public interface IUnaryExpressionOperation<TSelf, TSourceType, TResultType, TOp>
    : IUnaryExpressionOperation<TSelf, TSourceType, TResultType>
    where TSelf : IUnaryExpressionOperation<TSelf, TSourceType, TResultType, TOp>
    where TSourceType : ISingletonShaderType<TSourceType>
    where TResultType : ISingletonShaderType<TResultType>
    where TOp : IUnaryOp<TOp>
{
    IShaderType IUnaryExpressionOperation.SourceType => TSourceType.Instance;
    IShaderType IUnaryExpressionOperation.ResultType => TResultType.Instance;
    string IOperation.Name => $"{TOp.Instance.Name}.{TSourceType.Instance.Name}.{TResultType.Instance.Name}";


    TR IUnaryExpressionOperation.Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context)
        => semantic.OpOperation<TSelf, TSourceType, TResultType, TOp>(context, TSelf.Instance);
}