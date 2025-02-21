using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class FormalParameterExpression(ParameterDeclaration Parameter) : IExpression
{
    public IShaderType Type => Parameter.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitFormalParameterExpression(this);

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
        => [ShaderInstruction.Load(Parameter)];

    public IEnumerable<VariableDeclaration> ReferencedVariables => [];
}