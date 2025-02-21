using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct U64Literal(ulong Value) : ILiteral<U64Literal, ulong, UIntType<N64>>
{
}