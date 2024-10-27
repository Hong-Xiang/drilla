using DualDrill.Common.Nat;
using DualDrill.ILSL.IR.Declaration;

namespace DualDrill.ILSL.Types;

public interface IFloatType
{
}

public readonly struct FloatType<TBitWidth> : IFloatType, IScalarType<FloatType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public string Name => $"f{Nat.GetInstance<TBitWidth>().Value}";

    public int ByteSize => Nat.GetInstance<TBitWidth>().Value / 8;

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
