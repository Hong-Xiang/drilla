using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class AddressOfExpression(IExpression Base) : IExpression
{
    public IShaderType Type => new PtrType(Base.Type);
}
