using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed record class IntType(IBitWidth BitWidth) : IIntegerType, IScalarType
{
    public string Name => $"i{BitWidth.Value}";
    public int ByteSize => BitWidth.Value / 8;
}

public static partial class ShaderType
{
    public static IntType I8 { get; } = new(N8.Instance);
    public static IntType I16 { get; } = new(N16.Instance);
    public static IntType I32 { get; } = new(N32.Instance);
    public static IntType I64 { get; } = new(N64.Instance);

    public static IntType GetIntType(IBitWidth bitWidth)
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
