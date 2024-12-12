using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Literal;

[JsonDerivedType(typeof(BoolLiteral), nameof(BoolLiteral))]
[JsonDerivedType(typeof(FloatLiteral), nameof(FloatLiteral))]
[JsonDerivedType(typeof(IntLiteral), nameof(IntLiteral))]
[JsonDerivedType(typeof(I32Literal), nameof(I32Literal))]
[JsonDerivedType(typeof(I64Literal), nameof(I64Literal))]
[JsonDerivedType(typeof(UIntLiteral), nameof(UIntLiteral))]
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

public readonly record struct FloatLiteral(IBitWidth BitWidth, double Value)
    : INumericLiteral
{
    public IShaderType Type { get; } = ShaderType.GetFloatType(BitWidth);
}

public record struct I32Literal(int Value) : ILiteral<IntType<N32>>
{
    public readonly IShaderType Type => IntType<N32>.Instance;
}

public record struct I64Literal(long Value) : ILiteral<IntType<N64>>
{
    public readonly IShaderType Type => IntType<N64>.Instance;
}

public sealed record class IntLiteral(IBitWidth BitWidth, long Value)
    : INumericLiteral
{
    public IShaderType Type { get; } = ShaderType.GetIntType(BitWidth);
}

public sealed record class UIntLiteral(IBitWidth BitWidth, ulong Value)
    : INumericLiteral
{
    public IShaderType Type { get; } = ShaderType.GetUIntType(BitWidth);
}
