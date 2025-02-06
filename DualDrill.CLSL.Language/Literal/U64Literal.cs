using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct U64Literal(ulong Value) : ILiteral<UIntType<N64>>
{
    public readonly IShaderType Type => UIntType<N64>.Instance;
    public override string ToString() => $"{Type.Name}({Value})";
}
