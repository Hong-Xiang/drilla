using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class IndexingExpression(IExpression Base, IExpression Index) : IExpression
{
    public IShaderType Type => Base.Type switch
    {
        IVecType vecType => vecType.ElementType,
        _ => throw new InvalidExpressionTypeException(nameof(IndexingExpression))
    };

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