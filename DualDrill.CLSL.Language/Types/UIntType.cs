using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed record class UIntType(IBitWidth BitWidth) : IIntegerType, IScalarType
{
    public string Name => $"u{BitWidth.Value}";

    public int ByteSize => BitWidth.Value / 8;
}

public static partial class ShaderType
{
    public static UIntType U8 { get; } = new(N8.Instance);
    public static UIntType U16 { get; } = new(N16.Instance);
    public static UIntType U32 { get; } = new(N32.Instance);
    public static UIntType U64 { get; } = new(N64.Instance);

    public static UIntType GetUIntType(IBitWidth bitWidth)
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

