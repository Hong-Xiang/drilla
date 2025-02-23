using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class AddressOfExpression(IExpression Base) : IExpression
{
    public IShaderType Type => Base.Type.GetPtrType();

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => throw new NotImplementedException();

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
    {
        return Base switch
        {
            VariableIdentifierExpression { Variable: VariableDeclaration v } => [ShaderInstruction.Load(v)],
            FormalParameterExpression { Parameter: var p } => [ShaderInstruction.Load(p)],
            _ => throw new NotImplementedException()
        };
    }

    public IEnumerable<VariableDeclaration> ReferencedVariables => Base.ReferencedVariables;
}