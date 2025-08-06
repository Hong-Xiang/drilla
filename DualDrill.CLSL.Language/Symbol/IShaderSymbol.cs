using DualDrill.CLSL.Frontend.SymbolTable;

namespace DualDrill.CLSL.Language.Symbol;

public interface IShaderSymbol
{
    // string? Name { get; }
}

public interface IShaderSymbol<out T> : IShaderSymbol
{
}
