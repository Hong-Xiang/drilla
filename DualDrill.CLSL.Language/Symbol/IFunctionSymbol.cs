using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.Declaration;
using System.Reflection;

namespace DualDrill.CLSL.Language.Symbol;


public sealed record class CSharpMethodFunctionSymbol(MethodBase Method)
    : IShaderSymbol<FunctionDecl<IShaderSymbol<ShaderDecl>>>
{
    public FunctionDecl<IShaderSymbol<ShaderDecl>>? Lookup(ISymbolTableView2 table)
        => table.GetDeclaration(this);
}