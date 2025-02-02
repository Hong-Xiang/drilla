using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct F64Literal(double Value) : ILiteral<FloatType<N64>>
{
    public readonly IShaderType Type => FloatType<N64>.Instance;
}
