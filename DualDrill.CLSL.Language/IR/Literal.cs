using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.IR;

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
