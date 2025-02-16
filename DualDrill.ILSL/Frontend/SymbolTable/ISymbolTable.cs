using System.Reflection;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface ISymbolTable : ISymbolTableView
{
    void AddFunctionDeclaration(IFunctionSymbol symbol, FunctionDeclaration declaration);

    void AddFunctionDefinition(IFunctionSymbol symbol, FunctionDeclaration declaration,
        MethodBodyAnalysisModel? model = null);

    // all structures/variables locally referenced must be defined, thus no AddStructureDefinitionMethod
    VariableDeclaration AddVariable(IVariableSymbol symbol, VariableDeclaration declaration);
    ParameterDeclaration AddParameter(ParameterInfo symbol, ParameterDeclaration declaration);
    StructureDeclaration AddStructure(Type symbol, StructureType declaration);
    void AddStructureMember(FieldInfo symbol, MemberDeclaration declaration);
}