using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class IndirectionExpression() : IExpression
{
    public IShaderType Type => throw new NotImplementedException();
}
