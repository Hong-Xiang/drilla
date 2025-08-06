using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface ISymbolTableView2
{
    IShaderType? GetShaderType(IShaderSymbol symbol);
    ShaderDecl? GetDeclaration(IShaderSymbol symbol);
}


public interface ISymbolTable2 : ISymbolTableView2
{
    ISymbolTable2 AddDeclaration(IShaderSymbol symbol, ShaderDecl decl);
}

public interface ISymbolTableView2<T> : ISymbolTableView2
{
    T GetFunctionDefinition(IShaderSymbol symbol);
}

public interface ISymbolTable2<T> : ISymbolTable2, ISymbolTableView2<T>
{
    ISymbolTable2<T> AddFunctionDefinition(IShaderSymbol symbol, T method);
}


