using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class IndirectionExpression() : IExpression
{
    public IShaderType Type => throw new NotImplementedException();

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<VariableDeclaration> ReferencedVariables { get; }
}