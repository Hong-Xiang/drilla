using System.Diagnostics;
using System.Reflection;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using ParameterDeclaration = DualDrill.CLSL.Language.Declaration.ParameterDeclaration;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public sealed class CompilationContext : ISymbolTable
{
    private readonly Dictionary<Type, IShaderType> Types = [];
    private readonly Dictionary<FunctionDeclaration, MethodBodyAnalysisModel> FunctionDefinitions = [];

    private readonly Dictionary<IFunctionSymbol, FunctionDeclaration> Functions = [];
    private readonly Dictionary<IParameterSymbol, ParameterDeclaration> Parameters = [];
    private readonly Dictionary<IVariableSymbol, VariableDeclaration> LocalVariables = [];
    private readonly Dictionary<FieldInfo, MemberDeclaration> Members = [];

    private readonly HashSet<StructureDeclaration> ModuleStructureDeclarations = [];
    private readonly Dictionary<FieldInfo, MemberDeclaration> StructureMembers = [];
    private readonly ISymbolTableView? Parent;

    public CompilationContext(ISymbolTableView? parent)
    {
        Parent = parent;
    }

    public static ISymbolTable Create() => new CompilationContext(SharedBuiltinSymbolTable.Instance);

    public IShaderType? this[Type type] => Types.TryGetValue(type, out var shaderType) ? shaderType : Parent?[type];

    public FunctionDeclaration? this[IFunctionSymbol symbol] =>
        Functions.TryGetValue(symbol, out var found) ? found : Parent?[symbol];

    public ParameterDeclaration? this[ParameterInfo parameter] => throw new NotImplementedException();

    public ParameterDeclaration? this[IParameterSymbol info] =>
        Parameters.TryGetValue(info, out var result) ? result : Parent?[info];

    public VariableDeclaration? this[IVariableSymbol symbol] =>
        LocalVariables.TryGetValue(symbol, out var result) ? result : Parent?[symbol];


    public MemberDeclaration? this[FieldInfo method] =>
        StructureMembers.TryGetValue(method, out var declaration) ? declaration : Parent?[method];


    public ISymbolTable AddParameter(IParameterSymbol symbol, ParameterDeclaration decl)
    {
        Parameters.Add(symbol, decl);
        return this;
    }

    public ISymbolTable AddVariable(IVariableSymbol symbol, VariableDeclaration declaration)
    {
        LocalVariables.Add(symbol, declaration);
        return this;
    }

    public ISymbolTable AddFunctionDeclaration(IFunctionSymbol symbol, FunctionDeclaration declaration)
    {
        Functions.Add(symbol, declaration);
        return this;
    }

    public ISymbolTable AddStructure(Type symbol, StructureType type)
    {
        Types.Add(symbol, type);
        ModuleStructureDeclarations.Add(type.Declaration);
        return this;
    }

    public ISymbolTable AddStructureMember(FieldInfo symbol, MemberDeclaration declaration)
    {
        StructureMembers.Add(symbol, declaration);
        return this;
    }

    public ISymbolTable AddFunctionDefinition(IFunctionSymbol symbol, FunctionDeclaration declaration,
        MethodBodyAnalysisModel? model = null)
    {
        if (symbol is CSharpMethodFunctionSymbol { Method: var method })
        {
            model ??= new MethodBodyAnalysisModel(method);
            Debug.Assert(method.Equals(model.Method));
            Functions.Add(symbol, declaration);
            FunctionDefinitions.Add(declaration, model);
            return this;
        }

        throw new NotSupportedException();
    }

    public MethodBodyAnalysisModel GetFunctionDefinition(FunctionDeclaration declaration) =>
        FunctionDefinitions[declaration];


    public IEnumerable<StructureDeclaration> StructureDeclarations => ModuleStructureDeclarations;

    public IEnumerable<VariableDeclaration> VariableDeclarations =>
        LocalVariables.Values;

    public IEnumerable<FunctionDeclaration> FunctionDeclarations => Functions.Values;
}