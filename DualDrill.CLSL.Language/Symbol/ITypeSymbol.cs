using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Symbol;

public sealed record class ShaderTypeSymbol(Type Type)
    : IShaderSymbol<IShaderType>
{
    public IShaderType? Lookup(ISymbolTableView2 table)
        => table.GetShaderType(this);
}
