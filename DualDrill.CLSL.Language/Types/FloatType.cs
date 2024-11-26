using DualDrill.Common.Nat;
namespace DualDrill.CLSL.Language.Types;

public sealed record class FloatType(IBitWidth BitWidth) : IScalarType
{
    public string Name => $"f{BitWidth.Value}";
    public int ByteSize => BitWidth.Value / 8;

    public string ElementTypeName => Name;
}

public static partial class ShaderType
{
    public static FloatType F16 { get; } = new(N16.Instance);
    public static FloatType F32 { get; } = new(N32.Instance);
    public static FloatType F64 { get; } = new(N64.Instance);

    public static FloatType GetFloatType(IBitWidth bitWidth)
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
