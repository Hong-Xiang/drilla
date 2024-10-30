using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.ApiGen.DMath;

internal static class DMathCodeGenExtension
{
    public static IEnumerable<string> Components(this IRank rank) => rank switch
    {
        N2 => ["x", "y"],
        N3 => ["x", "y", "z"],
        N4 => ["x", "y", "z", "w"],
        _ => throw new NotSupportedException($"components for rank {rank} is not supported")
    };

    public static string CSharpName(this MatType matType)
         => $"mat{matType.Row.Value}x{matType.Column.Value}{matType.ElementType.ElementName()}";


    public static string ElementName(this IScalarType type) => type switch
    {
        BoolType _ => $"b",
        FloatType t => $"f{t.BitWidth.Value}",
        IntType t => $"i{t.BitWidth.Value}",
        UIntType t => $"u{t.BitWidth.Value}",
        _ => throw new NotSupportedException($"{nameof(ElementName)} does not support {type}")
    };

    public static Type ScalarCSharpType(this IScalarType t)
    {
        return t switch
        {
            BoolType _ => typeof(bool),
            IntType { BitWidth: N8 } => typeof(sbyte),
            IntType { BitWidth: N16 } => typeof(short),
            IntType { BitWidth: N32 } => typeof(int),
            IntType { BitWidth: N64 } => typeof(long),

            UIntType { BitWidth: N8 } => typeof(byte),
            UIntType { BitWidth: N16 } => typeof(ushort),
            UIntType { BitWidth: N32 } => typeof(uint),
            UIntType { BitWidth: N64 } => typeof(ulong),

            FloatType { BitWidth: N16 } => typeof(Half),
            FloatType { BitWidth: N32 } => typeof(float),
            FloatType { BitWidth: N64 } => typeof(double),
            _ => throw new NotSupportedException($"Primitive CSharp Type is not defined for {t}")
        };
    }

}
