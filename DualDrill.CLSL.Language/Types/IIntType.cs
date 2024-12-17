using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;


public interface IIntType : IIntegerType
{
}

public interface IIntType<TSelf> : IIntType, IScalarType<TSelf>
    where TSelf : class, IIntType<TSelf>
{
}

public sealed record class IntType<TBitWidth> : IIntType<IntType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public static IntType<TBitWidth> Instance => new();

    public IBitWidth BitWidth => TBitWidth.BitWidth;

    public string Name => $"i{BitWidth.Value}";
    public int ByteSize => BitWidth.Value / 8;

    public T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IScalarType.IGenericVisitor<T>
        => visitor.Visit(this);
}

public static partial class ShaderType
{
    public static IIntType I8 { get; } = IntType<N8>.Instance;
    public static IIntType I16 { get; } = IntType<N16>.Instance;
    public static IIntType I32 { get; } = IntType<N32>.Instance;
    public static IIntType I64 { get; } = IntType<N64>.Instance;

    public static IIntType GetIntType(IBitWidth bitWidth)
    {
        return bitWidth switch
        {
            N8 => I8,
            N16 => I16,
            N32 => I32,
            N64 => I64,
            _ => throw new ArgumentException($"Unsupported bit width: {bitWidth.Value}")
        };
    }
}
