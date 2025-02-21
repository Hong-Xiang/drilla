using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct U32Literal(uint Value) : ILiteral<U32Literal, uint, UIntType<N32>>
{
}