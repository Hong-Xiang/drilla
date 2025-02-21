using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class FunctionCallExpression(
    FunctionDeclaration Callee,
    ImmutableArray<IExpression> Arguments)
    : IExpression
{
    public IShaderType Type => Callee.Return.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitFunctionCallExpression(this);

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
        =>
        [
            .. Arguments.SelectMany(e => e.ToInstructions()),
            ShaderInstruction.Call(Callee)
        ];

    public IEnumerable<VariableDeclaration> ReferencedVariables
        => Arguments.SelectMany(e => e.ReferencedVariables);

    public bool Equals(FunctionCallExpression? other)
    {
        if (other is null)
        {
            return false;
        }

        return Callee.Equals(other.Callee) && Arguments.SequenceEqual(other.Arguments);
    }
}