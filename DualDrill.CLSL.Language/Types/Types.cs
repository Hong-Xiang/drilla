using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public interface IShaderType
{
    string Name { get; }
    IRefType GetRefType();
    IPtrType GetPtrType(IAddressSpace addressSpace);

    TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IShaderTypeVisitor1<TResult>;

    T Evaluate<T>(IShaderTypeSemantic<T, T> semantic);
}

public interface IShaderType<TSelf> : IShaderType, ISingleton<TSelf>
    where TSelf : IShaderType<TSelf>
{
    TResult IShaderType.Accept<TVisitor, TResult>(TVisitor visitor) => visitor.Visit((TSelf)this);
}

public interface IShaderTypeVisitor1<out TResult>
{
    TResult Visit<TType>(TType type) where TType : IShaderType<TType>;
}

public interface IShaderTypeSemantic<in TI, out TO>
{
    TO UnitType(UnitType t);
    TO BoolType(BoolType t);
    TO IntType<TWidth>(IntType<TWidth> t) where TWidth : IBitWidth;
    TO UIntType<TWidth>(UIntType<TWidth> t) where TWidth : IBitWidth;
    TO FloatType<TWidth>(FloatType<TWidth> t) where TWidth : IBitWidth;

    TO VecType<TRank, TElement>(VecType<TRank, TElement> t)
        where TRank : IRank<TRank> where TElement : IScalarType<TElement>;

    TO PtrType(IPtrType ptr);
    TO FunctionType(FunctionType t);
}

public interface IShaderTypeMatcher2<out TResult>
{
    TResult Match<TTypeL, TTypeR>(TTypeL typeL, TTypeR typeR)
        where TTypeL : IShaderType<TTypeL>
        where TTypeR : IShaderType<TTypeR>;
}

public interface ISingletonShaderType : IShaderType
{
}

public interface ISingletonShaderType<TSelf>
    : ISingletonShaderType, IShaderType<TSelf>, ISingleton<TSelf>
    where TSelf : ISingletonShaderType<TSelf>
{
    IRefType IShaderType.GetRefType() => throw new NotImplementedException();

    IPtrType IShaderType.GetPtrType(IAddressSpace s) => IPtrType.CreateFromSingletonType<TSelf>(s);
}

/// <summary>
///     type with size fully determined at shader creation time
/// </summary>
public interface ICreationFixedFootprintType : IShaderType
{
    int ByteSize { get; }
}

public interface IBasicPrimitiveType<TSelf> : ISingleton<TSelf>, ICreationFixedFootprintType
    where TSelf : class, IBasicPrimitiveType<TSelf>
{
}

public interface IPlainType : IShaderType
{
}

public interface IScalarType : IPlainType, IStorableType, ICreationFixedFootprintType
{
    IBitWidth BitWidth { get; }

    T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IGenericVisitor<T>;

    IConversionOperation GetConversionToOperation<TTarget>()
        where TTarget : IScalarType<TTarget>;

    IVecType GetVecType<TRank>() where TRank : class, IRank<TRank>;

    public interface IGenericVisitor<T>
    {
        public T Visit<TScalarType>(TScalarType scalarType)
            where TScalarType : class, IScalarType<TScalarType>;
    }
}

public interface IScalarType<TSelf> : IScalarType, ISingletonShaderType<TSelf>
    where TSelf : IScalarType<TSelf>
{
    IConversionOperation IScalarType.GetConversionToOperation<TTarget>() =>
        ScalarConversionOperation<TSelf, TTarget>.Instance;

    IVecType IScalarType.GetVecType<TRank>() => VecType<TRank, TSelf>.Instance;
}

public interface IIntegerType : IScalarType
{
    ISignedness Signedness { get; }
}

public interface IIntegerType<TBitwidth, TSign> : IIntegerType
    where TBitwidth : IBitWidth
    where TSign : ISignedness<TSign>
{
}

public interface IStorableType : IShaderType
{
}

public static partial class ShaderType
{
    public static string ElementName(this IScalarType t)
    {
        return t switch
        {
            BoolType => "b",
            _ => t.Name
        };
    }
}

public static class ShderTypeExtension
{
    public static IPtrType GetPtrType(this IShaderType t) => t.GetPtrType(GenericAddressSpace.Instance);
}