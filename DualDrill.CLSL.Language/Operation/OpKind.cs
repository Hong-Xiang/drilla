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
    IOperationMethodAttribute GetOperationMethodAttribute();
}

public interface IOperation<TSelf> : IOperation, ISingleton<TSelf>
    where TSelf : IOperation<TSelf>
{
    IOperationMethodAttribute IOperation.GetOperationMethodAttribute()
        => new OperationMethodAttribute<TSelf>();
}

public interface IUnaryScalarOperation<TSelf> : IUnaryOp
    where TSelf : IUnaryScalarOperation<TSelf>
{
}

public interface IBinaryOp<TSelf>
    where TSelf : IBinaryOp<TSelf>
{ }

public interface IGenericOp<TSelf>
    where TSelf : IGenericOp<TSelf>
{
}

/// <summary>
/// Operation of form t -> t -> t
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public interface IBinaryOperation<TSelf> : ISingleton<TSelf>
    where TSelf : IBinaryOperation<TSelf>
{
    public IShaderType OperandType { get; }
}

public interface IBinaryOperation<TSelf, TDataType, TOp> : IBinaryOperation<TSelf>
    where TSelf : IBinaryOperation<TSelf, TDataType, TOp>
    where TDataType : IShaderType
    where TOp : IGenericOp<TOp>
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

public interface IWASMOp { }
public interface IWGSLOp { }
public interface ISPIRVOp { }

public interface IBitWidthOp<TSelf, TBitWidth>
    where TSelf : IBitWidthOp<TSelf, TBitWidth>
    where TBitWidth : IBitWidth
{
}

public interface ISignGenericNumericOp<TSelf>
    where TSelf : ISignGenericNumericOp<TSelf>
{
}

public interface ISignedNumericOp<TSelf>
    where TSelf : ISignedNumericOp<TSelf>
{
}

public struct NumericSignedIntegerOp<TBitWidth, TOp, TSign>
    : ISignedNumericOp<NumericSignedIntegerOp<TBitWidth, TOp, TSign>>
    where TBitWidth : IBitWidth
    where TSign : ISignedness<TSign>
    where TOp : ISignedIntegerOp<TOp>
{
}

public struct NumericIntegerOp<TBitWidth, TOp>
    : ISignGenericNumericOp<NumericIntegerOp<TBitWidth, TOp>>
    where TBitWidth : IBitWidth
    where TOp : IIntegerOp<TOp>
{
}

public struct NumericFloatOp<TBitWidth, TOp>
    : ISignedNumericOp<NumericFloatOp<TBitWidth, TOp>>
    where TBitWidth : IBitWidth
    where TOp : IFloatOp<TOp>
{
}


public interface INumericBinaryOperation
{
    IStructuredStackInstruction Instruction { get; }
}

public interface INumericBinaryOperation<TSelf> : INumericBinaryOperation, IBinaryOperation<TSelf>, ISingleton<TSelf>
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

    public IStructuredStackInstruction Instruction => BinaryOperationInstruction<NumericBinaryOperation<TType, TOp>>.Instance;

    public IShaderType OperandType => TType.Instance;
}
