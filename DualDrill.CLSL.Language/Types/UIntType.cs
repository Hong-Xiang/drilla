using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Types;

public interface IUIntType : IIntegerType, IScalarType
{
}

[DebuggerDisplay("{Name}")]
public sealed class UIntType<TBitWidth> : IUIntType, INumericType<UIntType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    private UIntType()
    {
    }

    public static UIntType<TBitWidth> Instance { get; } = new();

    public string Name => $"u{BitWidth.Value}";

    public int ByteSize => BitWidth.Value / 8;

    public string ElementTypeName => Name;

    public IBitWidth BitWidth => TBitWidth.BitWidth;

    public T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IScalarType.IGenericVisitor<T>
        => visitor.Visit(this);
}

public static partial class ShaderType
{
    public static IUIntType U8 => UIntType<N8>.Instance;
    public static IUIntType U16 => UIntType<N16>.Instance;
    public static IUIntType U32 => UIntType<N32>.Instance;
    public static IUIntType U64 => UIntType<N64>.Instance;

    public static IUIntType GetUIntType(IBitWidth bitWidth)
    {
        return bitWidth switch
        {
            N8 => U8,
            N16 => U16,
            N32 => U32,
            N64 => U64,
            _ => throw new ArgumentException($"Unsupported bit width: {bitWidth.Value}")
        };
    }
}