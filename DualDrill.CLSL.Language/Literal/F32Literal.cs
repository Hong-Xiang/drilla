using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct F32Literal(float Value) : ILiteral<FloatType<N32>>
{
    public readonly IShaderType Type => IntType<N32>.Instance;
}
