using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface ISymbolTable : ISymbolTableView
{
    ISymbolTable AddFunctionDeclaration(IFunctionSymbol symbol, FunctionDeclaration declaration);
    ISymbolTable AddVariable(IVariableSymbol symbol, VariableDeclaration declaration);
    ISymbolTable AddParameter(IParameterSymbol symbol, ParameterDeclaration declaration);
    ISymbolTable AddStructure(ITypeSymbol symbol, StructureType declaration);
    ISymbolTable AddStructureMember(IMemberSymbol symbol, MemberDeclaration declaration);
}

public interface ISymbolTable<TMethodDefinition> : ISymbolTable, ISymbolTableView<TMethodDefinition>
{
    ISymbolTable AddFunctionDefinition(IFunctionSymbol symbol, TMethodDefinition method);
}