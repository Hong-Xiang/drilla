using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorNumericOperation : IOperation
{
}

public interface IVectorBinaryNumericOperation : IVectorNumericOperation
{
    IShaderType LeftType { get; }
    IShaderType RightType { get; }
    IShaderType ResultType { get; }
    IBinaryOp Op { get; }
}

public interface IVectorNumericOperation<TSelf>
    : ISingleton<TSelf>, IOperation<TSelf>
    , IVectorNumericOperation
    where TSelf : IVectorNumericOperation<TSelf>
{
}

public interface IVectorBinaryNumericOperation<TSelf>
    : ISingleton<TSelf>, IOperation<TSelf>
    , IVectorNumericOperation<TSelf>
    , IBinaryOperation<TSelf>
    where TSelf : IVectorBinaryNumericOperation<TSelf>
{
}

public sealed class VectorNumericUnaryOperation<TRank, TElement, TOp>
    : IVectorNumericOperation<VectorNumericUnaryOperation<TRank, TElement, TOp>>
    , IUnaryOperation<VectorNumericUnaryOperation<TRank, TElement, TOp>>
    where TRank : IRank<TRank>
    where TElement : INumericType<TElement>
    where TOp : IUnaryOp

{
    public static VectorNumericUnaryOperation<TRank, TElement, TOp> Instance { get; } = new();

    private VectorNumericUnaryOperation()
    {
        Function = new FunctionDeclaration(
                Name,
                [
                    new ParameterDeclaration( "l", OperandType, [] ),
                    new ParameterDeclaration( "r", OperandType, [] )
                ],
                new FunctionReturn(OperandType, []),
                [((IOperation)this).GetOperationMethodAttribute()]
            );
    }

    public FunctionDeclaration Function { get; }

    public VecType<TRank, TElement> OperandType => VecType<TRank, TElement>.Instance;
    public IShaderType SourceType => OperandType;
    public IShaderType ResultType => OperandType;

    public string Name => GetType().CSharpFullName();

    IStructuredStackInstruction IOperation.Instruction => UnaryOperationInstruction<VectorNumericUnaryOperation<TRank, TElement, TOp>>.Instance;

    public IExpression CreateExpression(IExpression expr)
    {
        throw new NotImplementedException();
    }
}

public sealed class VectorNumericBinaryOperation<TRank, TElement, TOp>
    : IVectorBinaryNumericOperation<VectorNumericBinaryOperation<TRank, TElement, TOp>>
    , IVectorBinaryNumericOperation
    where TRank : IRank<TRank>
    // TODO: fix this to INumericType<TElement>
    where TElement : IScalarType<TElement>
    where TOp : IBinaryOp<TOp>

{
    public static VectorNumericBinaryOperation<TRank, TElement, TOp> Instance { get; } = new();

    private VectorNumericBinaryOperation()
    {
        Function = new FunctionDeclaration(
                Name,
                [
                    new ParameterDeclaration( "l", OperandType, [] ),
                    new ParameterDeclaration( "r", OperandType, [] )
                ],
                new FunctionReturn(OperandType, []),
                [((IOperation)this).GetOperationMethodAttribute()]
            );
    }

    public FunctionDeclaration Function { get; }

    public VecType<TRank, TElement> OperandType => VecType<TRank, TElement>.Instance;

    public string Name => GetType().CSharpFullName();

    public IShaderType LeftType => OperandType;

    public IShaderType RightType => OperandType;

    public IShaderType ResultType => OperandType;

    public IBinaryOp Op => TOp.Instance;
}


public sealed class ScalarVectorNumericOperation<TRank, TElement, TOp>
    : IVectorBinaryNumericOperation<ScalarVectorNumericOperation<TRank, TElement, TOp>>
    , IVectorBinaryNumericOperation
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
    where TOp : IBinaryOp<TOp>

{
    public static ScalarVectorNumericOperation<TRank, TElement, TOp> Instance { get; } = new();

    private ScalarVectorNumericOperation()
    {
        Function = new FunctionDeclaration(
                Name,
                [
                    new ParameterDeclaration( "l", ScalarType, [] ),
                    new ParameterDeclaration( "r", VectorType, [] )
                ],
                new FunctionReturn(VectorType, []),
                [((IOperation)this).GetOperationMethodAttribute()]
            );
    }

    public FunctionDeclaration Function { get; }
    public VecType<TRank, TElement> VectorType => VecType<TRank, TElement>.Instance;
    public IScalarType ScalarType => TElement.Instance;

    public string Name => GetType().CSharpFullName();

    public IShaderType LeftType => ScalarType;

    public IShaderType RightType => VectorType;

    public IShaderType ResultType => VectorType;
    public IBinaryOp Op => TOp.Instance;
}

public sealed class VectorScalarNumericOperation<TRank, TElement, TOp>
    : IVectorBinaryNumericOperation<VectorScalarNumericOperation<TRank, TElement, TOp>>
    , IVectorBinaryNumericOperation
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
    where TOp : IBinaryOp<TOp>

{
    public static VectorScalarNumericOperation<TRank, TElement, TOp> Instance { get; } = new();

    private VectorScalarNumericOperation()
    {
        Function = new FunctionDeclaration(
                Name,
                [
                    new ParameterDeclaration( "l", VectorType, [] ),
                    new ParameterDeclaration( "r", ScalarType, [] )
                ],
                new FunctionReturn(VectorType, []),
                [((IOperation)this).GetOperationMethodAttribute()]
            );
    }

    public FunctionDeclaration Function { get; }

    public VecType<TRank, TElement> VectorType => VecType<TRank, TElement>.Instance;
    public IScalarType ScalarType => TElement.Instance;

    public string Name => GetType().CSharpFullName();

    public IShaderType LeftType => VectorType;

    public IShaderType RightType => ScalarType;

    public IShaderType ResultType => VectorType;
    public IBinaryOp Op => TOp.Instance;
}
