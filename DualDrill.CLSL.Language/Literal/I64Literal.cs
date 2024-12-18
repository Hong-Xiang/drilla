using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct I64Literal(long Value) : ILiteral<IntType<N64>>
{
    public readonly IShaderType Type => IntType<N64>.Instance;
}
