using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class NamedComponentExpression(IExpression Base, string ComponentName) : IExpression
{
    public IShaderType Type => throw new NotImplementedException();
}
