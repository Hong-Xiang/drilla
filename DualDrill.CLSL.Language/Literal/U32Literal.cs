using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct U32Literal(uint Value) : ILiteral<UIntType<N32>>
{
    public readonly IShaderType Type => UIntType<N32>.Instance;
}
