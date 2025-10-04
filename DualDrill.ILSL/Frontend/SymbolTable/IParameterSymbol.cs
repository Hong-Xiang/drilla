using System.Reflection;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface IParameterSymbol
    : ISymbolTableSymbol<ParameterDeclaration>
{
    ParameterDeclaration? ISymbolTableSymbol<ParameterDeclaration>.Lookup(ISymbolTableView table)
        => table[this];
}

public sealed record class ParameterIndexSymbol(int Index)
    : IParameterSymbol
{
}

public sealed record class ParameterInfoSymbol(ParameterInfo ParameterInfo)
    : IParameterSymbol
{
}