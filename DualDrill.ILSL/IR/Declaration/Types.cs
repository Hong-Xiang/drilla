using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;
public interface IRank
{
    abstract static IEnumerable<T> Select<T>(Func<int, T> f);
    abstract static int Value { get; }
}
public readonly struct R2 : IRank
{
    public static int Value => 2;

    public static IEnumerable<T> Select<T>(Func<int, T> f) => [f(0), f(1)];
}
public readonly struct R3 : IRank
{
    public static int Value => 3;
    public static IEnumerable<T> Select<T>(Func<int, T> f) => [f(0), f(1), f(2)];
}
public readonly struct R4 : IRank
{
    public static int Value => 4;
    public static IEnumerable<T> Select<T>(Func<int, T> f) => [f(0), f(1), f(2), f(3)];
}

public interface IBitWidth
{
    abstract static int Value { get; }
}

public readonly struct B16 : IBitWidth
{
    public static int Value => 16;
}
public readonly struct B32 : IBitWidth
{
    public static int Value => 32;
}
public readonly struct B64 : IBitWidth
{
    public static int Value => 64;
}

public interface IType : IDeclaration
{
}

public interface IBuiltinType
{
    abstract static IType Instance { get; }
    int ByteSize { get; }
}

internal interface IBuiltinType<T> : IType, IBuiltinType
    where T : IBuiltinType<T>, new()
{
    static IType IBuiltinType.Instance => new T();

    ImmutableHashSet<IAttribute> IDeclaration.Attributes => [];
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
    public string Name => "bool";
    public int ByteSize => 4;
}

public interface IFloatType : IScalarType { }

public readonly struct FloatType<TBitWidth> : IFloatType, IBuiltinType<FloatType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public string Name => $"f{TBitWidth.Value}";

    public int ByteSize => TBitWidth.Value / 8;
    public readonly static FunctionDeclaration Cast = new FunctionDeclaration(
        new FloatType<TBitWidth>().Name,
        [],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );

    public readonly static FunctionDeclaration Sin = new FunctionDeclaration(
        "sin",
        [new ParameterDeclaration("e", new FloatType<TBitWidth>(), [])],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );

    public readonly static FunctionDeclaration Cos = new FunctionDeclaration(
        "cos",
        [new ParameterDeclaration("e", new FloatType<TBitWidth>(), [])],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );

    public readonly static FunctionDeclaration Sqrt = new FunctionDeclaration(
        "sqrt",
        [new ParameterDeclaration("e", new FloatType<TBitWidth>(), [])],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );

    public readonly static FunctionDeclaration Log = new FunctionDeclaration(
        "log",
        [new ParameterDeclaration("e", new FloatType<TBitWidth>(), [])],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );

    public readonly static FunctionDeclaration Pow = new FunctionDeclaration(
        "pow",
        [
            new ParameterDeclaration("base", new FloatType<TBitWidth>(), []),
            new ParameterDeclaration("exponent", new FloatType<TBitWidth>(), [])
        ],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );

    public readonly static FunctionDeclaration Clamp = new FunctionDeclaration(
        "clamp",
        [
            new ParameterDeclaration("value", new FloatType<TBitWidth>(), []),
            new ParameterDeclaration("min", new FloatType<TBitWidth>(), []),
            new ParameterDeclaration("max", new FloatType<TBitWidth>(), [])
        ],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );
    public readonly static FunctionDeclaration Max = new FunctionDeclaration(
        "max",
        [
            new ParameterDeclaration("value1", new FloatType<TBitWidth>(), []),
            new ParameterDeclaration("value2", new FloatType<TBitWidth>(), [])
        ],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );
    public readonly static FunctionDeclaration Min = new FunctionDeclaration(
        "min",
        [
            new ParameterDeclaration("value1", new FloatType<TBitWidth>(), []),
            new ParameterDeclaration("value2", new FloatType<TBitWidth>(), [])
        ],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );
    public readonly static FunctionDeclaration Abs = new FunctionDeclaration(
        "abs",
        [
            new ParameterDeclaration("value", new FloatType<TBitWidth>(), [])
        ],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );
    public readonly static FunctionDeclaration Floor = new FunctionDeclaration(
        "floor",
        [
            new ParameterDeclaration("value", new FloatType<TBitWidth>(), [])
        ],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );
    public readonly static FunctionDeclaration Exp = new FunctionDeclaration(
        "exp",
        [
            new ParameterDeclaration("value", new FloatType<TBitWidth>(), [])
        ],
        new FunctionReturn(new FloatType<TBitWidth>(), []),
        []
    );
    public readonly static FunctionDeclaration Sign = new FunctionDeclaration(
        "sign",
        [
            new ParameterDeclaration("value", new FloatType<TBitWidth>(), [])
        ],
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

    public readonly static FunctionDeclaration Cast = new FunctionDeclaration(
           new IntType<TBitWidth>().Name,
           [],
           new FunctionReturn(new IntType<TBitWidth>(), []),
           []
       );


    public string Name => $"i{TBitWidth.Value}";

    public int ByteSize => TBitWidth.Value / 8;
}

public readonly record struct UIntType<TBitWidth>() : IIntegerType, IBuiltinType<UIntType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public string Name => $"u{TBitWidth.Value}";

    public int ByteSize => TBitWidth.Value / 8;
}

public readonly record struct VecType<TSize, TElement>() : IBuiltinType<VecType<TSize, TElement>>
    , IStorableType
    where TSize : IRank
    where TElement : IScalarType, new()
{
    static readonly ImmutableArray<string> ParameterNames = ["x", "y", "z", "w"];

    public static readonly ImmutableArray<FunctionDeclaration> Constructors =
        [
          new FunctionDeclaration(
                new VecType<TSize, TElement>().Name,
                [],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
                []
          ),
          ..TSize.Select(static count =>
            new FunctionDeclaration(
                new VecType<TSize, TElement>().Name,
                [.. Enumerable.Range(0, count + 1).Select(static i => new ParameterDeclaration(ParameterNames[i], TElement.Instance, []))],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
            []
        ))];

    public string Name => $"vec{TSize.Value}<{new TElement().Name}>";

    public static readonly FunctionDeclaration Dot =
        new FunctionDeclaration("dot",
                               [new ParameterDeclaration("e1", new VecType<TSize, TElement>(), []),
                                new ParameterDeclaration("e2", new VecType<TSize, TElement>(), [])],
                                new FunctionReturn(new TElement(), []),
                                []);
    public static readonly FunctionDeclaration Length =
    new FunctionDeclaration("length",
                            [],
                            new FunctionReturn(new TElement(), []),
                            []);
    public static readonly FunctionDeclaration Abs =
    new FunctionDeclaration("abs",
                            [new ParameterDeclaration("e", new VecType<TSize, TElement>(), [])],
                            new FunctionReturn(new VecType<TSize, TElement>(), []),
                            []);

    public static readonly FunctionDeclaration Normalize =
    new FunctionDeclaration("normalize",
                        [new ParameterDeclaration("e", new VecType<TSize, TElement>(), [])],
                        new FunctionReturn(new VecType<TSize, TElement>(), []),
                        []);
    public static readonly FunctionDeclaration Reflect =
    new FunctionDeclaration("reflect",
                    [new ParameterDeclaration("a", new VecType<TSize, TElement>(), []),
                    new ParameterDeclaration("norm", new VecType<TSize, TElement>(), [])],
                    new FunctionReturn(new VecType<TSize, TElement>(), []),
                    []);
    public static readonly FunctionDeclaration Cross =
    new FunctionDeclaration("cross",
                [new ParameterDeclaration("a", new VecType<TSize, TElement>(), []),
                    new ParameterDeclaration("b", new VecType<TSize, TElement>(), [])],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
                []);
    public int ByteSize => TSize.Value * new TElement().ByteSize;
}

public readonly struct MatType<TRow, TCol, TElement>()
    : IBuiltinType<MatType<TRow, TCol, TElement>>, IStorableType
    where TRow : IRank
    where TCol : IRank
    where TElement : IScalarType, new()
{
    public string Name => $"mat{TRow.Value}x{TCol.Value}<{new TElement().Name}>";

    public int ByteSize => TRow.Value * TCol.Value * new TElement().ByteSize;
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
