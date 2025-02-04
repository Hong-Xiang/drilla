using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public interface IVariableIdentifierSymbol : ILoadStoreTargetSymbol
{
    IShaderType Type { get; }
    string Name { get; }
}

public sealed record class VariableIdentifierExpression(IVariableIdentifierSymbol Variable) : IExpression
{
    public IShaderType Type => Variable.Type;
}

public interface ILoadStoreTargetSymbol
{
    IShaderType Type { get; }
}
