using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct F64Literal(double Value) : ILiteral<F64Literal, double, FloatType<N64>>
{
}