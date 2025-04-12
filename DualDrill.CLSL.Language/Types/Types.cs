using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public interface IShaderType
{
    string Name { get; }
    IRefType GetRefType();
    IPtrType GetPtrType();

    TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IShaderTypeVisitor1<TResult>;
}

public interface IShaderType<TSelf> : IShaderType
    where TSelf : IShaderType<TSelf>
{
    TResult IShaderType.Accept<TVisitor, TResult>(TVisitor visitor)
    {
        return visitor.Visit((TSelf)this);
    }
}

public interface IShaderTypeVisitor1<out TResult>
{
    TResult Visit<TType>(TType type) where TType : IShaderType<TType>;
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
}

/// <summary>
/// type with size fully determined at shader creation time
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

    public interface IGenericVisitor<T>
    {
        public T Visit<TScalarType>(TScalarType scalarType)
            where TScalarType : class, IScalarType<TScalarType>;
    }

    T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IGenericVisitor<T>;

    IConversionOperation GetConversionToOperation<TTarget>()
        where TTarget : IScalarType<TTarget>;

    IVecType GetVecType<TRank>() where TRank : class, IRank<TRank>;
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