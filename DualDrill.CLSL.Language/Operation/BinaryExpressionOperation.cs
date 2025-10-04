using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IBinaryExpressionOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
    public IShaderType ResultType { get; }
    public IBinaryOp BinaryOp { get; }

    TO IOperation.EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic) =>
        semantic.Operation2(inst, this, inst.Result, inst[0], inst[1]);
}

public interface IBinaryExpressionOperation<TSelf>
    : IBinaryExpressionOperation
    , IOperation<TSelf>
    where TSelf : IBinaryExpressionOperation<TSelf>
{
}

public interface IBinaryExpressionOperation<TSelf, TLeftType, TRightType, TResultType, TOp>
    : IBinaryExpressionOperation<TSelf>
    where TSelf : IBinaryExpressionOperation<TSelf, TLeftType, TRightType, TResultType, TOp>
    where TLeftType : ISingletonShaderType<TLeftType>
    where TRightType : ISingletonShaderType<TRightType>
    where TResultType : ISingletonShaderType<TResultType>
    where TOp : IBinaryOp<TOp>
{
    static readonly FunctionDeclaration OperationFunction = new(
        TSelf.Instance.Name,
        [
            new ParameterDeclaration("l", TLeftType.Instance, []),
            new ParameterDeclaration("r", TRightType.Instance, [])
        ],
        new FunctionReturn(TResultType.Instance, []),
        [
            TSelf.Instance.GetOperationMethodAttribute()
        ]
    );

    IShaderType IBinaryExpressionOperation.LeftType => TLeftType.Instance;
    IShaderType IBinaryExpressionOperation.RightType => TRightType.Instance;
    IShaderType IBinaryExpressionOperation.ResultType => TResultType.Instance;

    IBinaryOp IBinaryExpressionOperation.BinaryOp => TOp.Instance;

    string IOperation.Name =>
        $"{TOp.Instance.Name}.{TLeftType.Instance.Name}.{TRightType.Instance.Name}.{TResultType.Instance.Name}";


    FunctionDeclaration IOperation.Function => OperationFunction;
}