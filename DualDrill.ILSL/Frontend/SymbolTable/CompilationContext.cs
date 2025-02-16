using System.Diagnostics;
using System.Reflection;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using ParameterDeclaration = DualDrill.CLSL.Language.Declaration.ParameterDeclaration;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public interface IVariableSymbol
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

public sealed record class FunctionLocalVariableSymbol(
    LocalVariableInfo LocalVariableInfo
) : IVariableSymbol
{
}

public interface IFunctionSymbol
{
}

public readonly record struct CSharpMethodFunctionSymbol(
    MethodBase Method
) : IFunctionSymbol;

public static class Symbol
{
    public static IVariableSymbol Variable(FieldInfo field) => new ShaderModuleFieldVariableSymbol(field);
    public static IVariableSymbol Variable(PropertyInfo getter) => new ShaderModulePropertyGetterVariableSymbol(getter);
    public static IVariableSymbol Variable(LocalVariableInfo info) => new FunctionLocalVariableSymbol(info);
    public static IVariableSymbol Variable(int localIndex) => new LocalVariableSymbol(localIndex);
    public static IFunctionSymbol Function(MethodBase method) => new CSharpMethodFunctionSymbol(method);
}

public sealed class CompilationContext : ISymbolTable
{
    private readonly Dictionary<Type, IShaderType> Types = [];
    private readonly Dictionary<MethodBase, FunctionDeclaration> CSharpMethodFunctions = [];
    private readonly Dictionary<FunctionDeclaration, MethodBodyAnalysisModel> FunctionDefinitions = [];
    private readonly Dictionary<FieldInfo, VariableDeclaration> FieldVariables = [];
    private readonly Dictionary<PropertyInfo, VariableDeclaration> PropertyGetterVariables = [];
    private readonly Dictionary<LocalVariableInfo, VariableDeclaration> LocalVariables = [];
    private readonly Dictionary<LocalVariableSymbol, VariableDeclaration> LocalVariablesByIndex = [];
    private readonly Dictionary<IShaderType, FunctionDeclaration> ZeroValueConstructors = [];
    private readonly HashSet<StructureDeclaration> ModuleStructureDeclarations = [];
    private readonly Dictionary<ParameterInfo, ParameterDeclaration> Parameters = [];
    private readonly Dictionary<FieldInfo, MemberDeclaration> StructureMembers = [];
    private readonly ISymbolTableView? Parent;

    public CompilationContext(ISymbolTableView? parent)
    {
        Parent = parent;
    }

    public static ISymbolTable Create() => new CompilationContext(SharedBuiltinSymbolTable.Instance);

    public IShaderType? this[Type type] => Types.TryGetValue(type, out var shaderType) ? shaderType : Parent?[type];

    public FunctionDeclaration? this[IFunctionSymbol symbol] => symbol switch
    {
        CSharpMethodFunctionSymbol { Method: var method } => CSharpMethodFunctions.TryGetValue(method,
            out var declaration)
            ? declaration
            : null,
        _ => null
    } ?? Parent?[symbol];

    public ParameterDeclaration? this[ParameterInfo info] =>
        Parameters.TryGetValue(info, out var result) ? result : Parent?[info];

    private TResult? ChainedLookup<TKey, TResult>(Dictionary<TKey, TResult> dictionary, TKey key)
        where TKey : ISymbolTableSymbol<TResult>
    {
        return dictionary.TryGetValue(key, out var value) ? value : Parent is not null ? key.Lookup(Parent) : default;
    }

    public VariableDeclaration? this[LocalVariableSymbol symbol] =>
        ChainedLookup(LocalVariablesByIndex, symbol);

    public MemberDeclaration? this[FieldInfo method] =>
        StructureMembers.TryGetValue(method, out var declaration) ? declaration : Parent?[method];

    public VariableDeclaration? this[IVariableSymbol symbol] => symbol switch
    {
        ShaderModuleFieldVariableSymbol fieldSymbol => FieldVariables.TryGetValue(fieldSymbol.Field,
            out var declaration)
            ? declaration
            : null,

        ShaderModulePropertyGetterVariableSymbol { Property: var prop } => PropertyGetterVariables.TryGetValue(prop,
            out var declaration)
            ? declaration
            : null,

        FunctionLocalVariableSymbol { LocalVariableInfo: var info } => LocalVariables.TryGetValue(info, out var result)
            ? result
            : Parent?[symbol],
        _ => null
    };

    public ParameterDeclaration AddParameter(ParameterInfo info, ParameterDeclaration decl)
    {
        Parameters.Add(info, decl);
        return decl;
    }

    public VariableDeclaration AddVariable(IVariableSymbol symbol, VariableDeclaration declaration)
    {
        switch (symbol)
        {
            case ShaderModuleFieldVariableSymbol fieldSymbol:
            {
                FieldVariables.Add(fieldSymbol.Field, declaration);
                return declaration;
            }
            case ShaderModulePropertyGetterVariableSymbol getterSymbol:
            {
                PropertyGetterVariables.Add(getterSymbol.Property, declaration);
                return declaration;
            }
            case FunctionLocalVariableSymbol localSymbol:
            {
                LocalVariables.Add(localSymbol.LocalVariableInfo, declaration);
                return declaration;
            }
            default:
                throw new NotSupportedException();
        }
    }

    public void AddFunctionDeclaration(IFunctionSymbol symbol, FunctionDeclaration declaration)
    {
        if (symbol is CSharpMethodFunctionSymbol { Method: var method })
        {
            CSharpMethodFunctions.Add(method, declaration);
            return;
        }


        throw new NotSupportedException();
    }

    public StructureDeclaration AddStructure(Type symbol, StructureType type)
    {
        Types.Add(symbol, type);
        ModuleStructureDeclarations.Add(type.Declaration);
        return type.Declaration;
    }

    public void AddStructureMember(FieldInfo symbol, MemberDeclaration declaration)
    {
        StructureMembers.Add(symbol, declaration);
    }

    public void AddFunctionDefinition(IFunctionSymbol symbol, FunctionDeclaration declaration,
        MethodBodyAnalysisModel? model = null)
    {
        if (symbol is CSharpMethodFunctionSymbol { Method: var method })
        {
            model ??= new MethodBodyAnalysisModel(method);
            Debug.Assert(method.Equals(model.Method));
            CSharpMethodFunctions.Add(method, declaration);
            FunctionDefinitions.Add(declaration, model);
            return;
        }

        throw new NotSupportedException();
    }

    public MethodBodyAnalysisModel GetFunctionDefinition(FunctionDeclaration declaration) =>
        FunctionDefinitions[declaration];


    public IEnumerable<StructureDeclaration> StructureDeclarations => ModuleStructureDeclarations;

    public IEnumerable<VariableDeclaration> VariableDeclarations =>
        FieldVariables.Values.Concat(PropertyGetterVariables.Values);

    public IEnumerable<FunctionDeclaration> FunctionDeclarations => CSharpMethodFunctions.Values;
}