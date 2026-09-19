using System.Reflection;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface ISymbolTable : ISymbolTableView
{
    ISymbolTable AddFunctionDeclaration(IFunctionSymbol symbol, FunctionDeclaration declaration);

    // all structures/variables locally referenced must be defined, thus no AddStructureDefinitionMethod
    ISymbolTable AddVariable(IVariableSymbol symbol, VariableDeclaration declaration);
    ISymbolTable AddParameter(IParameterSymbol symbol, ParameterDeclaration declaration);
    ISymbolTable AddType(Type symbol, IShaderType declaration);
    ISymbolTable AddStructure(Type symbol, StructureType declaration);
    ISymbolTable AddStructureMember(FieldInfo symbol, MemberDeclaration declaration);
}