using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;


public interface IFloatOp<TSelf>
    where TSelf : IFloatOp<TSelf>
{ }
public interface IIntegerOp<TSelf>
    where TSelf : IIntegerOp<TSelf>
{ }

public interface ISignedIntegerOp<TSelf>
    where TSelf : ISignedIntegerOp<TSelf>
{ }

public interface IUnaryOp { }

public interface IOperation
{
    FunctionDeclaration Function { get; }
    string Name { get; }
    IOperationMethodAttribute GetOperationMethodAttribute();
    IStructuredStackInstruction Instruction { get; }
}

public interface IOperation<TSelf> : IOperation, ISingleton<TSelf>
    where TSelf : IOperation<TSelf>
{
    IOperationMethodAttribute IOperation.GetOperationMethodAttribute()
        => new OperationMethodAttribute<TSelf>();
}

public interface IUnaryOperation<TSelf> : IOperation, ISingleton<TSelf>
    where TSelf : IUnaryOperation<TSelf>
{
    IShaderType SourceType { get; }
    IShaderType ResultType { get; }
    IExpression CreateExpression(IExpression expr);
    IStructuredStackInstruction IOperation.Instruction => UnaryOperationInstruction<TSelf>.Instance;
}

public interface IUnaryScalarOperation<TSelf> : IUnaryOp
    where TSelf : IUnaryScalarOperation<TSelf>
{
}

public interface IBinaryOp
{
    IOperation GetVectorBinaryNumericOperation<TRank, TElement>()
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>;
    IOperation GetScalarVectorNumericOperation<TRank, TElement>()
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>;
    IOperation GetVectorScalarNumericOperation<TRank, TElement>()
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>;
}

public interface IBinaryOp<TSelf> : IBinaryOp, ISingleton<TSelf>
    where TSelf : IBinaryOp<TSelf>
{
    IOperation IBinaryOp.GetVectorBinaryNumericOperation<TRank, TElement>()
        => VectorNumericBinaryOperation<TRank, TElement, TSelf>.Instance;
    IOperation IBinaryOp.GetScalarVectorNumericOperation<TRank, TElement>()
        => ScalarVectorNumericOperation<TRank, TElement, TSelf>.Instance;
    IOperation IBinaryOp.GetVectorScalarNumericOperation<TRank, TElement>()
        => VectorScalarNumericOperation<TRank, TElement, TSelf>.Instance;
}

public interface IAbstractOp<TSelf>
    where TSelf : IAbstractOp<TSelf>
{
}

public interface IBinaryActionOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
    public IStatement CreateStatement(IExpression l, IExpression r);
}

public interface IBinaryOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
    public IShaderType ResultType { get; }
}

public interface IBinaryOperation<TSelf> : IBinaryOperation, IOperation<TSelf>, ISingleton<TSelf>
    where TSelf : IBinaryOperation<TSelf>
{
    IStructuredStackInstruction IOperation.Instruction => BinaryOperationInstruction<TSelf>.Instance;
    IOperationMethodAttribute IOperation.GetOperationMethodAttribute() => new OperationMethodAttribute<TSelf>();
}

public interface IBinaryOperation<TSelf, TDataType, TOp> : IBinaryOperation<TSelf>
    where TSelf : IBinaryOperation<TSelf, TDataType, TOp>
    where TDataType : IShaderType
    where TOp : IAbstractOp<TOp>
{
}

public interface INamedOp<TSelf>
    where TSelf : INamedOp<TSelf>
{
    abstract static string Name { get; }
}
public interface ISymbolOp<TSelf>
    where TSelf : ISymbolOp<TSelf>
{
    abstract static string Symbol { get; }
}

public interface IOpKind<TSelf, TOpKind>
    where TOpKind : struct, Enum
    where TSelf : IOpKind<TSelf, TOpKind>
{
    abstract static TOpKind Kind { get; }
}

public interface INumericBinaryOperation
{
}

public interface INumericBinaryOperation<TSelf>
    : INumericBinaryOperation, IBinaryOperation<TSelf>, ISingleton<TSelf>
    where TSelf : INumericBinaryOperation<TSelf>
{
}

public sealed class NumericBinaryOperation<TType, TOp>
    : IBinaryOperation<NumericBinaryOperation<TType, TOp>>
    , ISingleton<NumericBinaryOperation<TType, TOp>>
    , INumericBinaryOperation<NumericBinaryOperation<TType, TOp>>
    where TType : INumericType<TType>
    where TOp : IBinaryOp<TOp>
{
    public static NumericBinaryOperation<TType, TOp> Instance { get; } = new();
    public IShaderType ResultType => TType.Instance;
    public IShaderType LeftType => TType.Instance;
    public IShaderType RightType => TType.Instance;

    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => throw new NotImplementedException();
}

