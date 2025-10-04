using System.Reflection;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface IVariableSymbol
    : ISymbolTableSymbol<VariableDeclaration>
{
    VariableDeclaration? ISymbolTableSymbol<VariableDeclaration>.
        Lookup(ISymbolTableView table)
        => table[this];
}

public sealed record class LocalVariableIndexSymbol(int Index)
    : IVariableSymbol
{
}

public sealed record class ShaderModuleFieldVariableSymbol(
    FieldInfo Field
) : IVariableSymbol
{
};

public sealed record class ShaderModulePropertyGetterVariableSymbol(
    PropertyInfo Property
) : IVariableSymbol
{
};