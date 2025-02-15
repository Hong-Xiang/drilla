using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Diagnostics;
using System.Reflection;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ParameterDeclaration = DualDrill.CLSL.Language.Declaration.ParameterDeclaration;

namespace DualDrill.CLSL.Frontend;

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

public interface IParameterSymbol
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
    public static IFunctionSymbol Function(MethodBase method) => new CSharpMethodFunctionSymbol(method);
}

public interface ICompilationContextView
{
    IShaderType? this[Type type] { get; }
    FunctionDeclaration? this[IFunctionSymbol symbol] { get; }
    VariableDeclaration? this[IVariableSymbol symbol] { get; }

    ParameterDeclaration? this[ParameterInfo parameter] { get; }

    MemberDeclaration? this[FieldInfo method] { get; }

    // new entities declared/defined in this context
    IEnumerable<StructureDeclaration> StructureDeclarations { get; }
    IEnumerable<VariableDeclaration> VariableDeclarations { get; }
    IEnumerable<FunctionDeclaration> FunctionDeclarations { get; }
    MethodBodyAnalysisModel GetFunctionDefinition(FunctionDeclaration declaration);
}

public interface ICompilationContext : ICompilationContextView
{
    void AddFunctionDeclaration(IFunctionSymbol symbol, FunctionDeclaration declaration);

    void AddFunctionDefinition(IFunctionSymbol symbol, FunctionDeclaration declaration,
        MethodBodyAnalysisModel? model = null);

    // all structures/variables locally referenced must be defined, thus no AddStructureDefinitionMethod
    VariableDeclaration AddVariable(IVariableSymbol symbol, Func<int, VariableDeclaration> declaration);
    ParameterDeclaration AddParameter(ParameterInfo symbol, ParameterDeclaration declaration);
    StructureDeclaration AddStructure(Type symbol, StructureType declaration);
    void AddStructureMember(FieldInfo symbol, MemberDeclaration declaration);
}

public interface ISharedVariableIndexContext : ICompilationContext
{
    public int NextVariableIndex();
}

public sealed class CompilationContext : ISharedVariableIndexContext
{
    private readonly Dictionary<Type, IShaderType> Types = [];
    private readonly Dictionary<MethodBase, FunctionDeclaration> CSharpMethodFunctions = [];
    private readonly Dictionary<FunctionDeclaration, MethodBodyAnalysisModel> FunctionDefinitions = [];
    private readonly Dictionary<FieldInfo, VariableDeclaration> FieldVariables = [];
    private readonly Dictionary<PropertyInfo, VariableDeclaration> PropertyGetterVariables = [];
    private readonly Dictionary<LocalVariableInfo, VariableDeclaration> LocalVariables = [];
    private readonly Dictionary<IShaderType, FunctionDeclaration> ZeroValueConstructors = [];
    private readonly HashSet<StructureDeclaration> ModuleStructureDeclarations = [];
    private readonly Dictionary<ParameterInfo, ParameterDeclaration> Parameters = [];
    private readonly Dictionary<FieldInfo, MemberDeclaration> StructureMembers = [];
    private readonly ICompilationContextView? Parent;
    private int DefinedVariableCount { get; set; } = 0;

    public CompilationContext(ICompilationContextView? parent)
    {
        Parent = parent;
    }

    public static ICompilationContext Create() => new CompilationContext(SharedBuiltinCompilationContext.Instance);

    public IShaderType? this[Type type] => Types.TryGetValue(type, out var shaderType) ? shaderType : Parent?[type];

    public FunctionDeclaration? this[IFunctionSymbol symbol] => symbol switch
    {
        CSharpMethodFunctionSymbol { Method: var method } => CSharpMethodFunctions.TryGetValue(method,
            out var declaration)
            ? declaration
            : null,
        _ => null
    } ?? Parent?[symbol];

    public ParameterDeclaration? this[ParameterInfo info]
    {
        get
        {
            if (Parameters.TryGetValue(info, out var result))
            {
                return result;
            }
            else
            {
                return Parent?[info];
            }
        }
    }

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

    public VariableDeclaration AddVariable(IVariableSymbol symbol, Func<int, VariableDeclaration> declaration)
    {
        var index = NextVariableIndex();
        switch (symbol)
        {
            case ShaderModuleFieldVariableSymbol fieldSymbol:
            {
                var variable = declaration(index);
                FieldVariables.Add(fieldSymbol.Field, variable);
                return variable;
            }
            case ShaderModulePropertyGetterVariableSymbol getterSymbol:
            {
                var variable = declaration(index);
                PropertyGetterVariables.Add(getterSymbol.Property, variable);
                return variable;
            }
            case FunctionLocalVariableSymbol localSymbol:
            {
                var variable = declaration(index);
                LocalVariables.Add(localSymbol.LocalVariableInfo, variable);
                return variable;
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

    public int NextVariableIndex()
    {
        if (Parent is ISharedVariableIndexContext shared)
        {
            return shared.NextVariableIndex();
        }
        else
        {
            var index = DefinedVariableCount;
            DefinedVariableCount++;
            return index;
        }
    }

    public IEnumerable<StructureDeclaration> StructureDeclarations => ModuleStructureDeclarations;

    public IEnumerable<VariableDeclaration> VariableDeclarations =>
        FieldVariables.Values.Concat(PropertyGetterVariables.Values);

    public IEnumerable<FunctionDeclaration> FunctionDeclarations => CSharpMethodFunctions.Values;
}