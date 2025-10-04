using DualDrill.ILSL.IR.Declaration;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Expression;

public sealed record class FunctionCallExpression(
    FunctionDeclaration Callee,
    ImmutableArray<IExpression> Arguments)
    : IExpression
    , IEquatable<FunctionCallExpression>
{
    public bool Equals(FunctionCallExpression? other)
    {
        if (other is null)
        {
            return false;
        }
        return Callee.Equals(other.Callee) && Arguments.SequenceEqual(other.Arguments);
    }
}
