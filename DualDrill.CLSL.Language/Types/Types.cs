using DotNext.Patterns;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Types;

public interface IShaderType
{
    string Name { get; }

    IRefType RefType { get; }
    IPtrType PtrType { get; }
}

public interface ISingletonShaderType<TSelf> : IShaderType
    where TSelf : class, ISingletonShaderType<TSelf>, ISingleton<TSelf>
{
    IPtrType IShaderType.PtrType => SingletonPtrType<TSelf>.Instance;
    IRefType IShaderType.RefType => throw new NotImplementedException();
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
}

public interface IScalarType<TSelf> : IScalarType, ISingletonShaderType<TSelf>, ISingleton<TSelf>
    where TSelf : class, IScalarType<TSelf>
{
    IPtrType IShaderType.PtrType => SingletonPtrType<TSelf>.Instance;
    IRefType IShaderType.RefType => throw new NotImplementedException();
}

public interface IIntegerType : IScalarType { }



public sealed record class FixedSizedArrayType(IPlainType ElementType, int Size)
{
}

public sealed record class RuntimeSizedArrayType(IScalarType ElementType)
{
}

public readonly record struct StructureMember(string Name, IPlainType Type)
{
    public ImmutableArray<IShaderAttribute> Attributes { get; init; } = [];
}

public sealed record class StructureType(ImmutableArray<StructureMember> Members)
{
}


public interface IStorableType : IShaderType { }

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
