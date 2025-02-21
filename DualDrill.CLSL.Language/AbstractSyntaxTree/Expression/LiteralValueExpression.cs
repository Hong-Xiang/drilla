using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class LiteralValueExpression(ILiteral Literal) : IExpression
{
    public IShaderType Type => Literal.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitLiteralValueExpression(this);

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
        => [ShaderInstruction.Const(Literal)];

    public IEnumerable<VariableDeclaration> ReferencedVariables => [];
}