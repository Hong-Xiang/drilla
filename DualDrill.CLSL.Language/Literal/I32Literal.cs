using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct I32Literal(int Value) : ILiteral<I32Literal, int, IntType<N32>>
{
}