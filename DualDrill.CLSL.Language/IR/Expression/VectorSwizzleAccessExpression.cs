using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Expression;

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

public sealed record class VectorSwizzleAccessExpression(IExpression Base, ImmutableArray<SwizzleComponent> Components) : IExpression
{
    public IShaderType Type { get; } = ((VecType)Base.Type).ElementType;
}
