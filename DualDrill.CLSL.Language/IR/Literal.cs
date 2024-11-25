using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.IR;

[JsonDerivedType(typeof(BoolLiteral), nameof(BoolLiteral))]
[JsonDerivedType(typeof(FloatLiteral), nameof(FloatLiteral))]
[JsonDerivedType(typeof(IntLiteral), nameof(IntLiteral))]
[JsonDerivedType(typeof(UIntLiteral), nameof(UIntLiteral))]
public interface ILiteral
{
    IShaderType Type { get; }

}

public interface INumericLiteral : ILiteral { }

public readonly record struct BoolLiteral(bool Value) : ILiteral
{
    public IShaderType Type => ShaderType.Bool;
}

public readonly record struct FloatLiteral(IBitWidth BitWidth, double Value)
    : INumericLiteral
{
    public IShaderType Type { get; } = ShaderType.GetFloatType(BitWidth);
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
