using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.ApiGen.DMath;

internal static class MathCodeGeneratorExtension
{
    public static IEnumerable<string> Components(this IRank rank) => rank switch
    {
        N2 => ["x", "y"],
        N3 => ["x", "y", "z"],
        N4 => ["x", "y", "z", "w"],
        _ => throw new NotSupportedException($"components for rank {rank} is not supported")
    };

    public static IEnumerable<Swizzle.IComponent> SwizzleComponents(this IRank rank) => rank switch
    {
        N2 => [Swizzle.X.Instance, Swizzle.Y.Instance],
        N3 => [Swizzle.X.Instance, Swizzle.Y.Instance, Swizzle.Z.Instance],
        N4 => [Swizzle.X.Instance, Swizzle.Y.Instance, Swizzle.Z.Instance, Swizzle.W.Instance],
        _ => throw new NotSupportedException($"components for rank {rank} is not supported")
    };

    



    public static string CSharpName(this MatType matType)
         => $"mat{matType.Row.Value}x{matType.Column.Value}{matType.ElementType.ElementName()}";


    public static string ElementName(this IScalarType type) => type switch
    {
        BoolType _ => $"b",
        IFloatType t => $"f{t.BitWidth.Value}",
        IIntType t => $"i{t.BitWidth.Value}",
        IUIntType t => $"u{t.BitWidth.Value}",
        _ => throw new NotSupportedException($"{nameof(ElementName)} does not support {type}")
    };

    public static Type ScalarCSharpType(this IScalarType t)
    {
        return t switch
        {
            BoolType _ => typeof(bool),
            IIntType { BitWidth: N8 } => typeof(sbyte),
            IIntType { BitWidth: N16 } => typeof(short),
            IIntType { BitWidth: N32 } => typeof(int),
            IIntType { BitWidth: N64 } => typeof(long),

            IUIntType { BitWidth: N8 } => typeof(byte),
            IUIntType { BitWidth: N16 } => typeof(ushort),
            IUIntType { BitWidth: N32 } => typeof(uint),
            IUIntType { BitWidth: N64 } => typeof(ulong),

            IFloatType { BitWidth: N16 } => typeof(Half),
            IFloatType { BitWidth: N32 } => typeof(float),
            IFloatType { BitWidth: N64 } => typeof(double),
            _ => throw new NotSupportedException($"Primitive CSharp Type is not defined for {t}")
        };
    }

}
