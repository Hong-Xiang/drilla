using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.Common;
using DualDrill.Common.Nat;
using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using System.Collections.Immutable;

namespace DualDrill.ILSL.Types;

public interface IType : IAstNode
{
    string Name { get; }
}

/// <summary>
/// type with size fully determined at shader creation time
/// </summary>
public interface ICreationFixedFootprintType : IType
{
    int ByteSize { get; }
}

public interface IBasicPrimitiveType<TSelf> : ISingleton<TSelf>, ICreationFixedFootprintType
    where TSelf : IBasicPrimitiveType<TSelf>
{
}

public interface IPlainType : IType
{
}

public interface IScalarType : IPlainType, IStorableType, ICreationFixedFootprintType
{
}

public interface IScalarType<TSelf> : IScalarType, IBasicPrimitiveType<TSelf>
    where TSelf : IScalarType<TSelf>
{
}


internal interface IZeroValueConstructibleType : IType
{
    FunctionDeclaration ZeroValueConstructor(ITargetLanguage targetLanguage);
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

sealed record class MemoryView(
    IStorableType StoreType,
    AccessMode AccessMode
)
{
}

sealed record class PointerType(
    AddressSpace AddressSpace,
    IStorableType StoreType,
    AccessMode AccessMode)
{ }

public enum ValueDeclarationKind
{
    Const,
    Override,
    Let,
    FormalParameter
}

public enum DeclarationScope
{
    Undefined,
    Module,
    Function
}
