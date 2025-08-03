using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;

namespace DualDrill.CLSL.Language.Operation;

public interface IBinaryExpressionOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
    public IShaderType ResultType { get; }
    public IExpression CreateExpression(IExpression l, IExpression r);
    public IBinaryOp BinaryOp { get; }

    public IExpressionValueInstruction CreateValueInstruction(IValue l, IValue r);
}

public interface IBinaryExpressionOperation<TSelf>
    : IBinaryExpressionOperation
    , IOperation<TSelf>
    where TSelf : IBinaryExpressionOperation<TSelf>
{
    IInstruction IOperation.Instruction =>
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

    IExpression IBinaryExpressionOperation.CreateExpression(IExpression l, IExpression r) =>
        new BinaryOperationExpression<TSelf>(l, r);

    IExpressionValueInstruction IBinaryExpressionOperation.CreateValueInstruction(IValue l, IValue r)
    {
        if (l is not IValue<TLeftType> lv)
        {
            throw new ArgumentException(
                $"Invalid operand1 type, expected {TLeftType.Instance.Name}, got {l.Type.Name}", nameof(l));
        }

        if (r is not IValue<TRightType> rv)
        {
            throw new ArgumentException(
                $"Invalid operand2 type, expected {TRightType.Instance.Name}, got {r.Type.Name}", nameof(r));
        }

        var result = OperationValue.Create<TResultType>();
        return new ExpressionOperation2ValueInstruction<TSelf, TLeftType, TRightType, TResultType>(result, lv, rv);
    }

    IBinaryOp IBinaryExpressionOperation.BinaryOp => TOp.Instance;

    string IOperation.Name =>
        $"{TOp.Instance.Name}.{TLeftType.Instance.Name}.{TRightType.Instance.Name}.{TResultType.Instance.Name}";


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