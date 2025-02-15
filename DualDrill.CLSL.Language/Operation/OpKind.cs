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

public interface IUnaryOperation<TSelf> : IOperation<TSelf>, ISingleton<TSelf>
    where TSelf : IUnaryOperation<TSelf>
{
    IShaderType SourceType { get; }
    IShaderType ResultType { get; }
    IExpression CreateExpression(IExpression expr);
    IStructuredStackInstruction IOperation.Instruction => UnaryOperationInstruction<TSelf>.Instance;
    IOperationMethodAttribute IOperation.GetOperationMethodAttribute()
        => new OperationMethodAttribute<TSelf>();
}

public interface IUnaryScalarOperation<TSelf> : IUnaryOperation<TSelf>
    where TSelf : IUnaryScalarOperation<TSelf>
{
}

public interface IAbstractOp
{
    string Name { get; }
}

public interface IBinaryOp : IAbstractOp
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

public interface IBinaryOp<TSelf> : IBinaryOp, IAbstractOp<TSelf>, ISingleton<TSelf>
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

public interface IBinaryStatementOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
    public IStatement CreateStatement(IExpression l, IExpression r);
}

public interface IBinaryExpressionOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
    public IShaderType ResultType { get; }
    public IExpression CreateExpression(IExpression l, IExpression r);
}

public interface IBinaryOperation<TSelf> : IBinaryExpressionOperation, IOperation<TSelf>, ISingleton<TSelf>
    where TSelf : IBinaryOperation<TSelf>
{
    IStructuredStackInstruction IOperation.Instruction => BinaryOperationInstruction<TSelf>.Instance;
    IOperationMethodAttribute IOperation.GetOperationMethodAttribute() => new OperationMethodAttribute<TSelf>();
}

public interface IBinaryOperation<TSelf, TDataType, TOp> : IBinaryOperation<TSelf>
    where TSelf : IBinaryOperation<TSelf, TDataType, TOp>
    where TDataType : IShaderType
    where TOp : IBinaryOp<TOp>
{
    IExpression IBinaryExpressionOperation.CreateExpression(IExpression l, IExpression r)
    {
        if (TOp.Instance is ISymbolOp op)
        {
            return op.GetBinaryExpression<TSelf>(l, r);
        }
        throw new NotSupportedException();
    }
}

public interface IBinaryExpressionOperation<TSelf, TLeftType, TRightType, TOp>
    : IBinaryOperation<TSelf>
    where TSelf : IBinaryExpressionOperation<TSelf, TLeftType, TRightType, TOp>
    where TLeftType : ISingletonShaderType<TLeftType>
    where TRightType : ISingletonShaderType<TRightType>
    where TOp : IBinaryOp<TOp>
{
    IExpression IBinaryExpressionOperation.CreateExpression(IExpression l, IExpression r)
    {
        if (TOp.Instance is ISymbolOp op)
        {
            return op.GetBinaryExpression<TSelf>(l, r);
        }
        throw new NotSupportedException($"{TOp.Instance}");
    }

    IShaderType IBinaryExpressionOperation.LeftType => TLeftType.Instance;
    IShaderType IBinaryExpressionOperation.RightType => TRightType.Instance;
}

public interface INamedOp<TSelf>
    where TSelf : INamedOp<TSelf>
{
    abstract static string Name { get; }
}
public interface ISymbolOp
{
    IExpression GetBinaryExpression<TOperation>(IExpression l, IExpression r)
            where TOperation : IBinaryOperation<TOperation>;
}

public interface ISymbolOp<TSelf> : ISymbolOp
    where TSelf : ISymbolOp<TSelf>
{
    abstract static string Symbol { get; }
    IExpression ISymbolOp.GetBinaryExpression<TOperation>(IExpression l, IExpression r)
            => new BinaryExpression<TOperation, TSelf>(l, r);
}

public interface IOpKind<TSelf, TOpKind> : IAbstractOp
    where TOpKind : struct, Enum
    where TSelf : IOpKind<TSelf, TOpKind>
{
    abstract static TOpKind Kind { get; }

    string IAbstractOp.Name => TSelf.Kind.ToString();
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
    , IBinaryExpressionOperation<NumericBinaryOperation<TType, TOp>, TType, TType, TOp>
    where TType : INumericType<TType>
    where TOp : IBinaryOp<TOp>
{
    public static NumericBinaryOperation<TType, TOp> Instance { get; } = new();
    public IShaderType ResultType => TType.Instance;
    public IShaderType LeftType => TType.Instance;
    public IShaderType RightType => TType.Instance;

    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => $"{TOp.Instance.Name}.{TType.Instance.Name}";
}

public sealed class NumericBinaryRelationOperation<TType, TOp>
    : IBinaryOperation<NumericBinaryRelationOperation<TType, TOp>>
    , IBinaryExpressionOperation<NumericBinaryRelationOperation<TType, TOp>, TType, TType, TOp>
    where TType : INumericType<TType>
    where TOp : IBinaryOp<TOp>

{
    public static NumericBinaryRelationOperation<TType, TOp> Instance { get; } = new();
    public IShaderType ResultType => BoolType.Instance;
    public FunctionDeclaration Function { get; } = new(
        $"{TOp.Instance.Name}.{TType.Instance.Name}",
        [],
        new FunctionReturn(BoolType.Instance, []),
        []
    );
    public string Name => TOp.Instance.Name;
}
