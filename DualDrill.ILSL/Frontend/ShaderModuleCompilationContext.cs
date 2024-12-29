using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed record class ShaderModuleCompilationContext(
    Dictionary<Type, IShaderType> Types,
    Dictionary<MethodBase, FunctionDeclaration> Functions,
    Dictionary<MemberInfo, VariableDeclaration> Variables,
    Dictionary<Type, FunctionDeclaration> ZeroValueConstructors)
{
    public static ShaderModuleCompilationContext Create()
    {
        var types = new Dictionary<Type, IShaderType>();
        foreach (var t in RuntimeCompilationContext.Instance.RuntimeTypes)
        {
            types.Add(t.Key, t.Value);
        }
        var funcs = new Dictionary<MethodBase, FunctionDeclaration>();
        foreach (var f in RuntimeCompilationContext.Instance.RuntimeMethods)
        {
            funcs.Add(f.Key, f.Value);
        }
        return new ShaderModuleCompilationContext(types, funcs, new(), new());
    }

    public MethodCompilationContext GetMethodContext(MethodBase method)
    {
        if (!Functions.TryGetValue(method, out var func))
        {
            throw new KeyNotFoundException(method.Name);
        }
        return new(
            func.Parameters,
            [],
            Functions.ToImmutableDictionary(),
            Types.ToImmutableDictionary(),
            ZeroValueConstructors.ToImmutableDictionary(),
            method
        );
    }
}
