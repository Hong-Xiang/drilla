using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Literal;

[JsonDerivedType(typeof(BoolLiteral), nameof(BoolLiteral))]
[JsonDerivedType(typeof(I32Literal), nameof(I32Literal))]
[JsonDerivedType(typeof(I64Literal), nameof(I64Literal))]
[JsonDerivedType(typeof(U32Literal), nameof(U32Literal))]
[JsonDerivedType(typeof(U64Literal), nameof(U64Literal))]
[JsonDerivedType(typeof(F32Literal), nameof(F32Literal))]
[JsonDerivedType(typeof(F64Literal), nameof(F64Literal))]
public interface ILiteral
{
    IShaderType Type { get; }
}

public interface INumericLiteral : ILiteral { }

public interface ILiteral<TShaderType> : ILiteral
    where TShaderType : class, IScalarType<TShaderType>
{
}

public readonly record struct BoolLiteral(bool Value) : ILiteral
{
    public IShaderType Type => ShaderType.Bool;
}

public record struct I32Literal(int Value) : ILiteral<IntType<N32>>
{
    public readonly IShaderType Type => IntType<N32>.Instance;
}

public record struct I64Literal(long Value) : ILiteral<IntType<N64>>
{
    public readonly IShaderType Type => IntType<N64>.Instance;
}

public record struct U32Literal(uint Value) : ILiteral<UIntType<N32>>
{
    public readonly IShaderType Type => UIntType<N32>.Instance;
}

public record struct U64Literal(ulong Value) : ILiteral<UIntType<N64>>
{
    public readonly IShaderType Type => UIntType<N64>.Instance;
}

public record struct F32Literal(float Value) : ILiteral<FloatType<N32>>
{
    public readonly IShaderType Type => IntType<N32>.Instance;
}

public record struct F64Literal(double Value) : ILiteral<FloatType<N64>>
{
    public readonly IShaderType Type => IntType<N64>.Instance;
}
