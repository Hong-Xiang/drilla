using System.Reflection;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface ISymbolTableSymbol<out TResult>
{
    TResult? Lookup(ISymbolTableView table);
}

public interface ISymbolTableView
{
    IShaderType? this[Type type] { get; }
    FunctionDeclaration? this[IFunctionSymbol symbol] { get; }
    VariableDeclaration? this[IVariableSymbol symbol] { get; }

    ParameterDeclaration? this[ParameterInfo parameter] { get; }

    VariableDeclaration? this[LocalVariableSymbol symbol] { get; }

    MemberDeclaration? this[FieldInfo method] { get; }

    // new entities declared/defined in this context
    IEnumerable<StructureDeclaration> StructureDeclarations { get; }
    IEnumerable<VariableDeclaration> VariableDeclarations { get; }
    IEnumerable<FunctionDeclaration> FunctionDeclarations { get; }
    MethodBodyAnalysisModel GetFunctionDefinition(FunctionDeclaration declaration);
}