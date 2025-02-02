using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Diagnostics;
namespace DualDrill.CLSL.Language.Types;

public interface IFloatType : IScalarType
{
}

public interface IFloatType<TSelf> : IFloatType, INumericType<TSelf>
    where TSelf : IFloatType<TSelf>
{
}

[DebuggerDisplay("{Name}")]
public sealed record class FloatType<TBitWidth> : IFloatType<FloatType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public static FloatType<TBitWidth> Instance => new();

    public string Name => $"f{BitWidth.Value}";
    public IBitWidth BitWidth => TBitWidth.BitWidth;
    public int ByteSize => BitWidth.Value / 8;

    public T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IScalarType.IGenericVisitor<T>
        => visitor.Visit(this);

    public INumericBinaryOperation GetBinaryOperation<TOp>() where TOp : IBinaryOp<TOp>
        => NumericBinaryOperation<FloatType<TBitWidth>, TOp>.Instance;
}


public static partial class ShaderType
{
    public static IFloatType F16 { get; } = FloatType<N16>.Instance;
    public static IFloatType F32 { get; } = FloatType<N32>.Instance;
    public static IFloatType F64 { get; } = FloatType<N64>.Instance;

    public static IFloatType GetFloatType(IBitWidth bitWidth)
    {
        return bitWidth switch
        {
            N16 => F16,
            N32 => F32,
            N64 => F64,
            _ => throw new ArgumentException($"Unsupported bit width: {bitWidth.Value}")
        };
    }
}
