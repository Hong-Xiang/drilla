using DotNext.Patterns;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

sealed class RuntimeCompilationContext : ISingleton<RuntimeCompilationContext>
{
    public ImmutableDictionary<Type, IShaderType> RuntimeTypes { get; }
    public ImmutableDictionary<MethodBase, FunctionDeclaration> RuntimeMethods { get; }

    RuntimeCompilationContext()
    {
        RuntimeTypes = GetRuntimeTypes().ToImmutableDictionary();
        RuntimeMethods = GetRuntimeMethods(RuntimeTypes).ToImmutableDictionary();
    }

    private Dictionary<Type, IShaderType> GetRuntimeTypes()
    {
        var result = new Dictionary<Type, IShaderType>()
        {
            [typeof(bool)] = BoolType.Instance,
            [typeof(int)] = ShaderType.I32,
            [typeof(uint)] = ShaderType.U32,
            [typeof(long)] = ShaderType.I64,
            [typeof(ulong)] = ShaderType.U64,
            [typeof(Half)] = ShaderType.F16,
            [typeof(float)] = ShaderType.F32,
            [typeof(double)] = ShaderType.F64,
            [typeof(Vector4)] = VecType<N4, FloatType<N32>>.Instance,
            [typeof(Vector3)] = VecType<N3, FloatType<N32>>.Instance,
            [typeof(Vector2)] = VecType<N2, FloatType<N32>>.Instance,
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
                        var parameterTypes = parameterDecls.Select(p => p.Type).ToArray();
                        if (m.Name == "vec2" && parameterTypes.Length == 2 && rt is VecType<N2, BoolType>)
                        {
                            var fl = ShaderFunction.Instance.GetFunction(m.Name, rt, parameterTypes);
                        }
                        var f = ShaderFunction.Instance.GetFunction(m.Name, rt, parameterTypes);
                        result.Add(m, f);
                    }
                }
            }
        }

        var vec4f32t = ShaderType.GetVecType(N4.Instance, ShaderType.F32);
        foreach (var c in typeof(Vector4).GetConstructors())
        {
            var parameters = c.GetParameters();
            if (!parameters.All(p => runtimeTypes.ContainsKey(p.ParameterType)))
            {
                continue;
            }
            var f = ShaderFunction.Instance.GetFunction("vec4", vec4f32t, [
                ..parameters.Select(p => runtimeTypes[p.ParameterType])
            ]);
            result.Add(c, f);
        }

        foreach (var m in typeof(Vector4).GetMethods())
        {
            if (m.Name == "Dot")
            {
                result.Add(m, ShaderFunction.Instance.GetFunction("dot", ShaderType.F32, [vec4f32t, vec4f32t]));
            }
        }

        return result;
    }
    public static RuntimeCompilationContext Instance { get; } = new RuntimeCompilationContext();
}
