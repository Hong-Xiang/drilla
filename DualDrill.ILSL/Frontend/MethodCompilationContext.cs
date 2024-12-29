using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

using CLSLParameterDeclaration = ParameterDeclaration;

public sealed record class MethodCompilationContext(
    ImmutableArray<CLSLParameterDeclaration> Parameters,
    List<VariableDeclaration> LocalVariables,
    ImmutableDictionary<MethodBase, FunctionDeclaration> Methods,
    ImmutableDictionary<Type, IShaderType> Types,
    ImmutableDictionary<Type, FunctionDeclaration> ZeroValueConstructors,
    MethodBase Method)
{
}
