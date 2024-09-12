using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;
public interface IRank
{
    abstract static IEnumerable<T> Select<T>(Func<int, T> f);
}
public readonly struct R2 : IRank
{
    public static IEnumerable<T> Select<T>(Func<int, T> f) => [f(0), f(1)];
}
public readonly struct R3 : IRank
{
    public static IEnumerable<T> Select<T>(Func<int, T> f) => [f(0), f(1), f(2)];
}
public readonly struct R4 : IRank
{
    public static IEnumerable<T> Select<T>(Func<int, T> f) => [f(0), f(1), f(2), f(3)];
}

public interface IBitWidth { }

public readonly struct B16 : IBitWidth { }
public readonly struct B32 : IBitWidth { }
public readonly struct B64 : IBitWidth { }

public interface IType : IDeclaration
{
}

public interface IBuiltinType
{
    abstract static IType Instance { get; }
}

internal interface IBuiltinType<T> : IType, IBuiltinType
    where T : IBuiltinType<T>, new()
{
    static IType IBuiltinType.Instance => new T();

    abstract static string GetName(ITargetLanguage targetLanguage, T type);

    internal static readonly IName NameInstance = new BuiltinTypeName<T>();
    IName IDeclaration.Name => NameInstance;
    ImmutableHashSet<IAttribute> IDeclaration.Attributes => [];
}

internal sealed record class BuiltinTypeName<T> : IName
    where T : IBuiltinType<T>, new()
{
    public string GetName(ITargetLanguage targetLanguage) => T.GetName(targetLanguage, new T());
}


public interface ISingletonType
{
    abstract static IType Instance { get; }
}

public interface IPlainType : IType
{
}

public interface IScalarType : IPlainType, IStorableType, IBuiltinType
{
}

public readonly record struct BoolType : IScalarType, IBuiltinType<BoolType>
{
    static string IBuiltinType<BoolType>.GetName(ITargetLanguage targetLanguage, BoolType type)
       => targetLanguage.GetName(type);
}

public interface IFloatType : IScalarType { }

public readonly struct FloatType<TBitWidth> : IFloatType, IBuiltinType<FloatType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    static string IBuiltinType<FloatType<TBitWidth>>.GetName(ITargetLanguage targetLanguage, FloatType<TBitWidth> type)
        => targetLanguage.GetName(type);

    public readonly static FunctionDeclaration Cast = new FunctionDeclaration(
        IBuiltinType<FloatType<TBitWidth>>.NameInstance,
        [],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );

}


internal interface IZeroValueConstructibleType : IType
{
    FunctionDeclaration ZeroValueConstructor(ITargetLanguage targetLanguage);
}

public interface IIntegerType : IScalarType { }

public readonly record struct IntType<TBitWidth>() : IIntegerType, IBuiltinType<IntType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public static string GetName(ITargetLanguage targetLanguage, IntType<TBitWidth> type)
        => targetLanguage.GetName(type);
}

public readonly record struct UIntType<TBitWidth>() : IIntegerType, IBuiltinType<UIntType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public static string GetName(ITargetLanguage targetLanguage, UIntType<TBitWidth> type)
        => targetLanguage.GetName(type);
}

public readonly record struct VecType<TSize, TElement>() : IBuiltinType<VecType<TSize, TElement>>
    , IStorableType
    where TSize : IRank
    where TElement : IScalarType
{
    static string IBuiltinType<VecType<TSize, TElement>>.GetName(ITargetLanguage targetLanguage, VecType<TSize, TElement> type)
        => targetLanguage.GetName(type);

    static readonly ImmutableArray<string> ParameterNames = ["x", "y", "z", "w"];

    public static readonly ImmutableArray<FunctionDeclaration> Constructors =
        [
          new FunctionDeclaration(
                IBuiltinType<VecType<TSize, TElement>>.NameInstance,
                [],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
                []
          ),
          ..TSize.Select(static count =>
            new FunctionDeclaration(
                IBuiltinType<VecType<TSize, TElement>>.NameInstance,
                [.. Enumerable.Range(0, count + 1).Select(static i => new ParameterDeclaration(Name.Create(ParameterNames[i]), TElement.Instance, []))],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
            []
        ))];
}

public readonly struct MatType<TRow, TCol, TElement>()
    : IBuiltinType<MatType<TRow, TCol, TElement>>, IStorableType
    where TRow : IRank
    where TCol : IRank
    where TElement : IScalarType
{
    static string IBuiltinType<MatType<TRow, TCol, TElement>>.GetName(ITargetLanguage targetLanguage, MatType<TRow, TCol, TElement> type)
        => targetLanguage.GetName(type);
}

public sealed record class FixedSizedArrayType(IPlainType ElementType, int Size)
{
}

public sealed record class RuntimeSizedArrayType(IScalarType ElementType)
{
}

public readonly record struct StructureMember(string Name, IPlainType Type)
{
    public ImmutableArray<IAttribute> Attributes { get; init; } = [];
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
