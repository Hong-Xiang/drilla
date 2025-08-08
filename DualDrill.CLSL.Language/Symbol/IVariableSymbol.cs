using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.Symbol;

public interface IVariableSymbol
    : IShaderSymbol<VariableDeclaration>
    , IEquatable<IVariableSymbol>
{
}
