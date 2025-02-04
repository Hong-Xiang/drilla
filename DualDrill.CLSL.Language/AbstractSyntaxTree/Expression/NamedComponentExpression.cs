using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class NamedComponentExpression(IExpression Base, MemberDeclaration Component) : IExpression
{
    public IShaderType Type => Component.Type;
}
