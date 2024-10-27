using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Types;

public interface IShaderType : IAstNode
{
    string Name { get; }
}

/// <summary>
/// type with size fully determined at shader creation time
/// </summary>
public interface ICreationFixedFootprintType : IShaderType
{
    int ByteSize { get; }
}

public interface IBasicPrimitiveType<TSelf> : ISingleton<TSelf>, ICreationFixedFootprintType
    where TSelf : IBasicPrimitiveType<TSelf>
{
}

public interface IPlainType : IShaderType
{
}

public interface IScalarType : IPlainType, IStorableType, ICreationFixedFootprintType
{
}

public interface IScalarType<TSelf> : IScalarType, IBasicPrimitiveType<TSelf>
    where TSelf : IScalarType<TSelf>
{
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


public interface IStorableType { }
