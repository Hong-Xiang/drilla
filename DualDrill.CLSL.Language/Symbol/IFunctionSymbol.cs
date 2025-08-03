using System.Reflection;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.Symbol;

public interface IFunctionSymbol
    : IShaderSymbol<FunctionDeclaration>
{
    FunctionDeclaration? IShaderSymbol<FunctionDeclaration>.Lookup(ISymbolTableView table)
        => table[this];
}

public sealed record class CSharpMethodFunctionSymbol(
    MethodBase Method
) : IFunctionSymbol
{
}