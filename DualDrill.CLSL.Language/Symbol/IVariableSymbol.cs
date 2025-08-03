using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.Symbol;

public interface IVariableSymbol
    : IShaderSymbol<VariableDeclaration>
    , IEquatable<IVariableSymbol>
{
    VariableDeclaration? IShaderSymbol<VariableDeclaration>.
        Lookup(ISymbolTableView table)
        => table[this];
}
