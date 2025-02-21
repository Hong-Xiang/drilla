using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public interface IShaderType
{
    string Name { get; }
    IRefType GetRefType();
    IPtrType GetPtrType();
}

public interface IShaderType<TSelf> : IShaderType
    where TSelf : IShaderType<TSelf>
{
}

public interface ISingletonShaderType<TSelf>
    : IShaderType<TSelf>, ISingleton<TSelf>
    where TSelf : ISingletonShaderType<TSelf>
{
    IPtrType IShaderType.GetPtrType() => new PtrType(TSelf.Instance);
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

public sealed record class FixedSizedArrayType(IPlainType ElementType, int Size)
{
}

public sealed record class RuntimeSizedArrayType(IScalarType ElementType)
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