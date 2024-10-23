using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public interface IType : INode
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

public readonly record struct BoolType : IScalarType, IBasicPrimitiveType<BoolType>
{
    public static BoolType Instance { get; } = new();

    public string Name => "bool";
    public int ByteSize => 4;
}

public interface IFloatType
{
}

public readonly struct FloatType<TBitWidth> : IFloatType, IScalarType<FloatType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public string Name => $"f{TBitWidth.Value}";

    public int ByteSize => TBitWidth.Value / 8;

    public static FloatType<TBitWidth> Instance { get; } = new();

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

public readonly record struct IntType<TBitWidth>() : IIntegerType, IScalarType<IntType<TBitWidth>>
    where TBitWidth : IBitWidth
{

    public readonly static FunctionDeclaration Cast = new FunctionDeclaration(
           new IntType<TBitWidth>().Name,
           [],
           new FunctionReturn(new IntType<TBitWidth>(), []),
           []
       );

    public static IntType<TBitWidth> Instance { get; } = new();

    public string Name => $"i{TBitWidth.Value}";

    public int ByteSize => TBitWidth.Value / 8;
}

public readonly record struct UIntType<TBitWidth>() : IIntegerType, IBasicPrimitiveType<UIntType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public static UIntType<TBitWidth> Instance { get; } = new();

    public string Name => $"u{TBitWidth.Value}";

    public int ByteSize => TBitWidth.Value / 8;
}

public interface IVecType : IStorableType
{
    public IRank Size { get; }
    public IScalarType ElementType { get; }
}

public interface IVecType<TSelf> : IVecType
{
    abstract static TSelf Instance { get; }
}

public sealed class VecType<TSize, TElement>() : IBasicPrimitiveType<VecType<TSize, TElement>>
    , IVecType<VecType<TSize, TElement>>
    where TSize : IRank<TSize>
    where TElement : IScalarType<TElement>
{
    public static VecType<TSize, TElement> Instance { get; } = new();
    public IRank Size => TSize.Instance;

    static readonly ImmutableArray<string> ParameterNames = ["x", "y", "z", "w"];

    public static readonly ImmutableArray<FunctionDeclaration> Constructors =
        [
          new FunctionDeclaration(
                new VecType<TSize, TElement>().Name,
                [],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
                []
          ),
          ..Enumerable.Range(0, TSize.Value).Select(static count =>
            new FunctionDeclaration(
                new VecType<TSize, TElement>().Name,
                [.. Enumerable.Range(0, count + 1).Select(static i => new ParameterDeclaration(ParameterNames[i], TElement.Instance, []))],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
            []
        ))];

    public string Name => $"vec{TSize.Value}<{TElement.Instance.Name}>";

    public static readonly FunctionDeclaration Dot =
        new FunctionDeclaration("dot",
                               [new ParameterDeclaration("e1", new VecType<TSize, TElement>(), []),
                                new ParameterDeclaration("e2", new VecType<TSize, TElement>(), [])],
                                new FunctionReturn(TElement.Instance, []),
                                []);
    public static readonly FunctionDeclaration Length =
    new FunctionDeclaration("length",
                            [],
                            new FunctionReturn(TElement.Instance, []),
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
    public int ByteSize => TSize.Value * TElement.Instance.ByteSize;

    public IScalarType ElementType => TElement.Instance;
}

public readonly struct MatType<TRow, TCol, TElement>()
    : IBasicPrimitiveType<MatType<TRow, TCol, TElement>>, IStorableType
    where TRow : IRank<TRow>
    where TCol : IRank<TCol>
    where TElement : IScalarType<TElement>
{
    public static MatType<TRow, TCol, TElement> Instance { get; } = new();

    public string Name => $"mat{TRow.Value}x{TCol.Value}<{TElement.Instance.Name}>";

    public int ByteSize => TRow.Value * TCol.Value * TElement.Instance.ByteSize;
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
