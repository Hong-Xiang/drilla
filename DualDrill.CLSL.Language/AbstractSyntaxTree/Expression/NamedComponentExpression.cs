using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class NamedComponentExpression(IExpression Base, MemberDeclaration Component) : IExpression
{
    public IShaderType Type => Component.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitNamedComponentExpression(this);

    public IEnumerable<IStructuredStackInstruction> ToInstructions()
        =>
        [
            ..Base.ToInstructions(),
            ShaderInstruction.Load(Component)
        ];

    public IEnumerable<VariableDeclaration> ReferencedVariables => Base.ReferencedVariables;
}