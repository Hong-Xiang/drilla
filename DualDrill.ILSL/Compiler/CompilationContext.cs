using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Reflection;

namespace DualDrill.ILSL.Compiler;

public sealed record class CompilationContext(
    Dictionary<Type, IShaderType> Types,

    Dictionary<MethodBase, FunctionDeclaration> Functions,

    // function definitions is inverse map of functions
    // but only for method with body
    Dictionary<FunctionDeclaration, MethodBase> FunctionDefinitions,

    Dictionary<FieldInfo, VariableDeclaration> FieldVariables,
    Dictionary<MethodBase, VariableDeclaration> PropertyGetterVariables,

    Dictionary<IShaderType, FunctionDeclaration> ZeroValueConstructors,
    List<StructureDeclaration> StructureDeclarations,
    List<VariableDeclaration> VariableDeclarations,
    List<FunctionDeclaration> FunctionDeclarations
)
{
    public static CompilationContext Create() => new([], [], [], [], [], [], [], [], []);
}

