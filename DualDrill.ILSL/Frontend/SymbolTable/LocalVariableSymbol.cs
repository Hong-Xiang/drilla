using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public readonly record struct LocalVariableSymbol(int Index)
    : ISymbolTableSymbol<VariableDeclaration>
    , IVariableSymbol
{
    public VariableDeclaration? Lookup(ISymbolTableView table)
        => table[this];
}