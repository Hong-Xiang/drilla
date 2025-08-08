using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface ISymbolTableView2
{
    IShaderType? GetShaderType(Language.Symbol.ISymbol symbol);
    ShaderDecl? GetDeclaration(Language.Symbol.ISymbol symbol);
}


public interface ISymbolTable2 : ISymbolTableView2
{
    ISymbolTable2 AddDeclaration(Language.Symbol.ISymbol symbol, ShaderDecl decl);
}

public interface ISymbolTableView2<T> : ISymbolTableView2
{
    T GetFunctionDefinition(Language.Symbol.ISymbol symbol);
}

public interface ISymbolTable2<T> : ISymbolTable2, ISymbolTableView2<T>
{
    ISymbolTable2<T> AddFunctionDefinition(Language.Symbol.ISymbol symbol, T method);
}


