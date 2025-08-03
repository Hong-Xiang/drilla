using System.Reflection;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Symbol;

public interface IMethodDefinition { }

public interface ISymbolTableView
{
    IShaderType? this[ITypeSymbol symbol] { get; }
    FunctionDeclaration? this[IFunctionSymbol symbol] { get; }
    VariableDeclaration? this[IVariableSymbol symbol] { get; }
    ParameterDeclaration? this[IParameterSymbol parameter] { get; }
    MemberDeclaration? this[IMemberSymbol symbol] { get; }

    // new entities declared/defined in this context
    IEnumerable<StructureDeclaration> StructureDeclarations { get; }
    IEnumerable<VariableDeclaration> VariableDeclarations { get; }
    IEnumerable<FunctionDeclaration> FunctionDeclarations { get; }
}

public interface ISymbolTableView<TMethodDefinition> : ISymbolTableView
{
    IMethodDefinition GetFunctionDefinition(IFunctionSymbol symbol);
}