using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnaryLogicalOp
{
    Not
}

public sealed record class UnaryLogicalExpression(
    IExpression Expr,
    UnaryLogicalOp Op
) : IExpression
{
    public IShaderType Type => Expr.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<VariableDeclaration> ReferencedVariables => Expr.ReferencedVariables;
}