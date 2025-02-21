using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class AddressOfExpression(IExpression Base) : IExpression
{
    public IShaderType Type => new PtrType(Base.Type);

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => throw new NotImplementedException();

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<VariableDeclaration> ReferencedVariables => Base.ReferencedVariables;
}