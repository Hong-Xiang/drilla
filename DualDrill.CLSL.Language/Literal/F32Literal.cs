using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct F32Literal(float Value) : ILiteral<F32Literal, float, FloatType<N32>>
{
}