using System.Collections.Immutable;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public static partial class ShaderType
{
    public static UnitType Unit { get; } = UnitType.Instance;
    public static BoolType Bool { get; } = BoolType.Instance;

    internal static IEnumerable<IRank> Ranks => [N2.Instance, N3.Instance, N4.Instance];

    public static ImmutableArray<IScalarType> IntTypes => [I8, I16, I32, I64];
    public static ImmutableArray<IScalarType> UIntTypes => [U8, U16, U32, U64];
    public static ImmutableArray<IScalarType> IntegerTypes => [.. IntTypes, .. UIntTypes];
    public static ImmutableArray<IScalarType> FloatTypes => [F16, F32, F64];
    public static ImmutableArray<IScalarType> NumericScalarTypes => [.. IntegerTypes, .. FloatTypes];
    public static ImmutableArray<IScalarType> ScalarTypes => [Bool, .. NumericScalarTypes];

    public static IShaderType Vec4F32 => GetVecType(N4.Instance, F32);
    public static IShaderType Vec3F32 => VecType<N3, FloatType<N32>>.Instance;
    public static IShaderType Vec2F32 => GetVecType(N2.Instance, F32);

    public static IEnumerable<IShaderType> GetVecTypes(IScalarType type) =>
        from r in Ranks
        select GetVecType(r, type);

    public static IEnumerable<IShaderType> GetScalarOrVectorTypes(IScalarType type) => [type, .. GetVecTypes(type)];
}