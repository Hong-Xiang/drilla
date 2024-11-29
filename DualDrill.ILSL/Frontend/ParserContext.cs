using DotNext.Patterns;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

sealed class RuntimeDefinitions : ISingleton<RuntimeDefinitions>
{
    public ImmutableDictionary<Type, IShaderType> RuntimeTypes { get; }
    public ImmutableDictionary<MethodBase, FunctionDeclaration> RuntimeMethods { get; }

    RuntimeDefinitions()
    {
        RuntimeTypes = GetRuntimeTypes().ToImmutableDictionary();
        RuntimeMethods = GetRuntimeMethods(RuntimeTypes).ToImmutableDictionary();
    }

    private Dictionary<Type, IShaderType> GetRuntimeTypes()
    {
        var result = new Dictionary<Type, IShaderType>()
        {
            [typeof(bool)] = ShaderType.Bool,
            [typeof(int)] = ShaderType.I32,
            [typeof(uint)] = ShaderType.U32,
            [typeof(long)] = ShaderType.I64,
            [typeof(ulong)] = ShaderType.U64,
            [typeof(Half)] = ShaderType.F16,
            [typeof(float)] = ShaderType.F32,
            [typeof(double)] = ShaderType.F64,
            [typeof(Vector4)] = ShaderType.GetVecType(N4.Instance, ShaderType.F32),
            [typeof(Vector3)] = ShaderType.GetVecType(N3.Instance, ShaderType.F32),
            [typeof(Vector2)] = ShaderType.GetVecType(N2.Instance, ShaderType.F32),
        };
        var config = CSharpProjectionConfiguration.Instance;

        var mathNamespace = config.MathLibNameSpaceName;
        var mathAssembly = typeof(DMath).Assembly;
        foreach (var v in ShaderType.GetVecTypes())
        {
            var name = $"{mathNamespace}.{v.Name}";
            var t = mathAssembly.GetType(name) ?? throw new NotSupportedException($"Can not find type {name}");
            result.Add(t, v);
        }

        return result;
    }

    private Type[] GetNumericVectorTypes()
    {
        return [typeof(Vector4), typeof(Vector3), typeof(Vector2)];
    }

    private Type GetMappedVecType(Type t)
    {
        if (t == typeof(Vector4))
        {
            return typeof(vec4f32);
        }
        if (t == typeof(Vector3))
        {
            return typeof(vec3f32);
        }
        if (t == typeof(Vector2))
        {
            return typeof(vec2f32);
        }
        throw new NotSupportedException();
    }

    private Dictionary<MethodBase, FunctionDeclaration> GetRuntimeMethods(IReadOnlyDictionary<Type, IShaderType> runtimeTypes)
    {
        var result = new Dictionary<MethodBase, FunctionDeclaration>();
        foreach (var m in typeof(DMath).GetMethods())
        {
            if (m.IsStatic)
            {
                var returnType = m.ReturnType;
                if (runtimeTypes.TryGetValue(returnType, out var rt))
                {
                    var parameters = m.GetParameters();
                    var paramTypes = parameters.Select(p => p.ParameterType).ToArray();
                    if (paramTypes.All(runtimeTypes.ContainsKey))
                    {
                        var parameterDecls = paramTypes.Select(t => new ParameterDeclaration(t.Name, runtimeTypes[t], []));
                        result.Add(m, ShaderFunction.Instance.GetFunction(m.Name, [.. parameterDecls.Select(p => p.Type)]));
                    }
                }
            }
        }

        foreach (var c in typeof(Vector4).GetConstructors())
        {
            var parameters = c.GetParameters();
            var m = typeof(DMath).GetMethod("vec4", 0, parameters.Select(p => p.ParameterType).ToArray())
                    ?? throw new NotSupportedException($"constructor projection not found for {c}");
            result.Add(c, result[m]);
        }

        return result;
    }
    public static RuntimeDefinitions Instance { get; } = new RuntimeDefinitions();
}

public sealed record class ParserContext(
    Dictionary<Type, IShaderType> Types,
    Dictionary<MethodBase, FunctionDeclaration> Functions,
    Dictionary<MemberInfo, VariableDeclaration> Variables)
{
    public static ParserContext Create()
    {
        var types = new Dictionary<Type, IShaderType>();
        foreach (var t in RuntimeDefinitions.Instance.RuntimeTypes)
        {
            types.Add(t.Key, t.Value);
        }
        var funcs = new Dictionary<MethodBase, FunctionDeclaration>();
        foreach (var f in RuntimeDefinitions.Instance.RuntimeMethods)
        {
            funcs.Add(f.Key, f.Value);
        }
        return new ParserContext(types, funcs, new Dictionary<MemberInfo, VariableDeclaration>());
    }

    public MethodParseContext GetMethodContext(MethodBase method)
    {
        if (!Functions.TryGetValue(method, out var func))
        {
            throw new KeyNotFoundException(method.Name);
        }
        return MethodParseContext.Empty with
        {
            Parameters = func.Parameters,
            Methods = Functions.ToImmutableDictionary()
        };
    }
}
