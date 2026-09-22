using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using ParameterDeclaration = DualDrill.CLSL.Language.Declaration.ParameterDeclaration;

namespace DualDrill.CLSL.Frontend.SymbolTable;

public sealed class CompilationContext : ISymbolTable
{
    private readonly Dictionary<IFunctionSymbol, FunctionDeclaration> Functions = [];
    private readonly Dictionary<IVariableSymbol, VariableDeclaration> LocalVariables = [];
    private readonly Dictionary<FieldInfo, MemberDeclaration> Members = [];

    private readonly HashSet<StructureDeclaration> ModuleStructureDeclarations = [];
    private readonly Dictionary<IParameterSymbol, ParameterDeclaration> Parameters = [];
    private readonly ISymbolTableView? Parent;
    private readonly Dictionary<FieldInfo, MemberDeclaration> StructureMembers = [];
    private readonly Dictionary<Type, IShaderType> Types = [];

    public CompilationContext(ISymbolTableView? parent)
    {
        Parent = parent;
    }

    public ParameterDeclaration? this[ParameterInfo parameter] => throw new NotImplementedException();

    public IShaderType? this[Type type] => Types.TryGetValue(type, out var shaderType) ? shaderType : Parent?[type];

    public FunctionDeclaration? this[IFunctionSymbol symbol] =>
        Functions.TryGetValue(symbol, out var found) ? found : Parent?[symbol];

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

    public ISymbolTable AddType(Type symbol, IShaderType type)
    {
        Types.Add(symbol, type);
        return this;
    }

    public ISymbolTable AddStructure(Type symbol, StructureType type)
    {
        AddType(symbol, type);
        ModuleStructureDeclarations.Add(type.Declaration);
        return this;
    }

    public ISymbolTable AddStructureMember(FieldInfo symbol, MemberDeclaration declaration)
    {
        StructureMembers.Add(symbol, declaration);
        return this;
    }

    public IEnumerable<StructureDeclaration> StructureDeclarations => ModuleStructureDeclarations;

    public IEnumerable<VariableDeclaration> VariableDeclarations =>
        LocalVariables.Values;

    public IEnumerable<FunctionDeclaration> FunctionDeclarations => Functions.Values;

    internal ISymbolTableView Freeze() =>
        new FrozenSymbolTable(
            FreezeParent(Parent),
            Types.ToFrozenDictionary(),
            Functions.ToFrozenDictionary(),
            LocalVariables.ToFrozenDictionary(),
            Parameters.ToFrozenDictionary(),
            StructureMembers.ToFrozenDictionary(),
            [.. ModuleStructureDeclarations]);

    public static CompilationContext Create() => new(SharedBuiltinSymbolTable.Instance);

    private static ISymbolTableView? FreezeParent(ISymbolTableView? parent) =>
        parent switch
        {
            null => null,
            CompilationContext context => context.Freeze(),
            FrozenSymbolTable frozen => frozen,
            SharedBuiltinSymbolTable builtin => builtin,
            _ => throw new NotSupportedException(
                $"Cannot freeze symbol-table parent {parent.GetType().FullName}.")
        };
}

internal sealed class FrozenSymbolTable(
    ISymbolTableView? parent,
    FrozenDictionary<Type, IShaderType> types,
    FrozenDictionary<IFunctionSymbol, FunctionDeclaration> functions,
    FrozenDictionary<IVariableSymbol, VariableDeclaration> variables,
    FrozenDictionary<IParameterSymbol, ParameterDeclaration> parameters,
    FrozenDictionary<FieldInfo, MemberDeclaration> members,
    ImmutableArray<StructureDeclaration> structures)
    : ISymbolTableView
{
    public IShaderType? this[Type type] =>
        types.TryGetValue(type, out var value) ? value : parent?[type];

    public FunctionDeclaration? this[IFunctionSymbol symbol] =>
        functions.TryGetValue(symbol, out var value) ? value : parent?[symbol];

    public VariableDeclaration? this[IVariableSymbol symbol] =>
        variables.TryGetValue(symbol, out var value) ? value : parent?[symbol];

    public ParameterDeclaration? this[IParameterSymbol parameter] =>
        parameters.TryGetValue(parameter, out var value) ? value : parent?[parameter];

    public MemberDeclaration? this[FieldInfo field] =>
        members.TryGetValue(field, out var value) ? value : parent?[field];

    public IEnumerable<StructureDeclaration> StructureDeclarations => structures;
    public IEnumerable<VariableDeclaration> VariableDeclarations => variables.Values;
    public IEnumerable<FunctionDeclaration> FunctionDeclarations => functions.Values;
}