using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct I32Literal(int Value) : ILiteral<IntType<N32>>
{
    public readonly IShaderType Type => IntType<N32>.Instance;
}
