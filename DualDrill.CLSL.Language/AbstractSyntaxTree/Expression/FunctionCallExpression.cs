using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class FunctionCallExpression(
    FunctionDeclaration Callee,
    ImmutableArray<IExpression> Arguments)
    : IExpression
    , IEquatable<FunctionCallExpression>
{
    public IShaderType Type => Callee.Return.Type;

    public bool Equals(FunctionCallExpression? other)
    {
        if (other is null)
        {
            return false;
        }
        return Callee.Equals(other.Callee) && Arguments.SequenceEqual(other.Arguments);
    }
}
