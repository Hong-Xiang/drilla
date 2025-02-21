using DotNext.Patterns;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using System.Collections.Frozen;
using System.Numerics;
using System.Reflection;
using DualDrill.CLSL.Frontend.SymbolTable;

namespace DualDrill.CLSL.Frontend;

sealed class SharedBuiltinSymbolTable : ISingleton<SharedBuiltinSymbolTable>, ISymbolTableView
{
    public FrozenDictionary<Type, IShaderType> RuntimeTypes { get; }
    public FrozenDictionary<MethodBase, FunctionDeclaration> RuntimeMethods { get; }

    SharedBuiltinSymbolTable()
    {
        RuntimeTypes = GetRuntimeTypes().ToFrozenDictionary();
        RuntimeMethods = GetRuntimeMethods(RuntimeTypes).ToFrozenDictionary();
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
            var tn = CSharpProjectionConfiguration.Instance.GetCSharpTypeName(v);
            var name = $"{mathNamespace}.{tn}";
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

    private Dictionary<MethodBase, FunctionDeclaration> GetRuntimeMethods(
        IReadOnlyDictionary<Type, IShaderType> runtimeTypes)
    {
        var result = new Dictionary<MethodBase, FunctionDeclaration>();
        var mathAssembly = typeof(DMath).Assembly;
        var operationMethods = from t in mathAssembly.GetExportedTypes()
                               from m in t.GetMethods()
                               let attr = m.GetCustomAttributes().OfType<IOperationMethodAttribute>().SingleOrDefault()
                               where attr is not null
                               select (m, attr.Operation);
        foreach (var (m, op) in operationMethods)
        {
            result.Add(m, op.Function);
        }

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
                        var parameterDecls =
                            paramTypes.Select(p => new ParameterDeclaration(p.Name, runtimeTypes[p], []));
                        var parameterTypes = parameterDecls.Select(p => p.Type).ToArray();
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

    public MethodBodyAnalysisModel GetFunctionDefinition(FunctionDeclaration declaration) =>
        throw new NotSupportedException("All runtime methods have not definitions");

    public static SharedBuiltinSymbolTable Instance { get; } = new SharedBuiltinSymbolTable();

    // all entities in shared builtin context can only be directly refrenced
    // declarations is not allowed
    public VariableDeclaration? this[IVariableSymbol symbol] => null;
    public ParameterDeclaration? this[IParameterSymbol parameter] => null;

    public MemberDeclaration? this[FieldInfo method] => null;

    public IEnumerable<StructureDeclaration> StructureDeclarations => [];

    public IEnumerable<VariableDeclaration> VariableDeclarations => [];

    public IEnumerable<FunctionDeclaration> FunctionDeclarations => [];


    public FunctionDeclaration? this[IFunctionSymbol symbol] => symbol switch
    {
        CSharpMethodFunctionSymbol { Method: var m } => RuntimeMethods.TryGetValue(m, out var f) ? f : null,
        _ => throw new NotImplementedException()
    };

    public IShaderType? this[Type type] => RuntimeTypes.TryGetValue(type, out var found) ? found : null;
}