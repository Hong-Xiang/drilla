using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Expression;

public enum SwizzleComponent
{
    x,
    y,
    z,
    w,
    r,
    g,
    b,
    a
}

public sealed record class VectorSwizzleAccessExpression(IExpression Base, ImmutableArray<SwizzleComponent> Components)
{
}
