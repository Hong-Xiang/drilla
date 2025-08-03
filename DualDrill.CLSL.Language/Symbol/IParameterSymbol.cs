using System.Reflection;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.Symbol;

public interface IParameterSymbol
    : IShaderSymbol<ParameterDeclaration>
{
    ParameterDeclaration? IShaderSymbol<ParameterDeclaration>.Lookup(ISymbolTableView table)
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