using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct I64Literal(long Value) : ILiteral<I64Literal, long, IntType<N64>>
{
}