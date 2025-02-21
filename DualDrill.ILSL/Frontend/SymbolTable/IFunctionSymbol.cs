using System.Reflection;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface IFunctionSymbol
    : ISymbolTableSymbol<FunctionDeclaration>
{
    FunctionDeclaration? ISymbolTableSymbol<FunctionDeclaration>.Lookup(ISymbolTableView table)
        => table[this];
}

public sealed record class CSharpMethodFunctionSymbol(
    MethodBase Method
) : IFunctionSymbol
{
}