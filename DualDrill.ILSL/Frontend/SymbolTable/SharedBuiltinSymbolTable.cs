using System.Collections.Frozen;
using System.Numerics;
using System.Reflection;
using DotNext.Patterns;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;

namespace DualDrill.CLSL.Frontend;

internal sealed class SharedBuiltinSymbolTable : ISingleton<SharedBuiltinSymbolTable>, ISymbolTableView
{
    private SharedBuiltinSymbolTable()
    {
        RuntimeTypes = GetRuntimeTypes().ToFrozenDictionary();
        RuntimeMethods = GetRuntimeMethods(RuntimeTypes).ToFrozenDictionary();
    }

    public FrozenDictionary<Type, IShaderType> RuntimeTypes { get; }
    public FrozenDictionary<MethodBase, FunctionDeclaration> RuntimeMethods { get; }

    internal static MethodInfo TextureSampleLevelMethod { get; } =
        typeof(Texture2D<float>).GetMethod(nameof(Texture2D<float>.SampleLevel))
        ?? throw new MissingMethodException(typeof(Texture2D<float>).FullName, nameof(Texture2D<float>.SampleLevel));

    public static SharedBuiltinSymbolTable Instance { get; } = new();

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

    private Dictionary<Type, IShaderType> GetRuntimeTypes()
    {
        var result = new Dictionary<Type, IShaderType>
        {
            [typeof(void)] = UnitType.Instance,
            [typeof(bool)] = BoolType.Instance,
            [typeof(sbyte)] = IntType<N8>.Instance,
            [typeof(byte)] = UIntType<N8>.Instance,
            [typeof(short)] = IntType<N16>.Instance,
            [typeof(ushort)] = UIntType<N16>.Instance,
            [typeof(int)] = ShaderType.I32,
            [typeof(uint)] = ShaderType.U32,
            [typeof(long)] = ShaderType.I64,
            [typeof(ulong)] = ShaderType.U64,
            [typeof(Half)] = ShaderType.F16,
            [typeof(float)] = ShaderType.F32,
            [typeof(double)] = ShaderType.F64,
            [typeof(StructuredBuffer<float>)] = ReadOnlyStructuredBufferType<FloatType<N32>>.Instance,
            [typeof(StructuredBuffer<int>)] = ReadOnlyStructuredBufferType<IntType<N32>>.Instance,
            [typeof(StructuredBuffer<uint>)] = ReadOnlyStructuredBufferType<UIntType<N32>>.Instance,
            [typeof(RWStructuredBuffer<float>)] = ReadWriteStructuredBufferType<FloatType<N32>>.Instance,
            [typeof(RWStructuredBuffer<int>)] = ReadWriteStructuredBufferType<IntType<N32>>.Instance,
            [typeof(RWStructuredBuffer<uint>)] = ReadWriteStructuredBufferType<UIntType<N32>>.Instance,
            [typeof(Texture2D<float>)] = SampledTexture2DF32Type.Instance,
            [typeof(SamplerState)] = SamplerStateType.Instance,
            [typeof(Vector4)] = VecType<N4, FloatType<N32>>.Instance,
            [typeof(Vector3)] = VecType<N3, FloatType<N32>>.Instance,
            [typeof(Vector2)] = VecType<N2, FloatType<N32>>.Instance
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

    private Type[] GetNumericVectorTypes() => [typeof(Vector4), typeof(Vector3), typeof(Vector2)];

    private Type GetMappedVecType(Type t)
    {
        if (t == typeof(Vector4)) return typeof(vec4f32);

        if (t == typeof(Vector3)) return typeof(vec3f32);

        if (t == typeof(Vector2)) return typeof(vec2f32);

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
        foreach (var (m, op) in operationMethods) result.Add(m, op.Function);

        foreach (var m in typeof(DMath).GetMethods())
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
                        if (!result.ContainsKey(m)) result.Add(m, f);
                    }
                }
            }

        var vec4f32t = ShaderType.GetVecType(N4.Instance, ShaderType.F32);
        foreach (var c in typeof(Vector4).GetConstructors())
        {
            var parameters = c.GetParameters();
            if (!parameters.All(p => runtimeTypes.ContainsKey(p.ParameterType))) continue;

            var f = ShaderFunction.Instance.GetFunction("vec4", vec4f32t, [
                ..parameters.Select(p => runtimeTypes[p.ParameterType])
            ]);
            result.Add(c, f);
        }

        foreach (var m in typeof(Vector4).GetMethods())
            if (m.Name == "Dot")
                result.Add(m, ShaderFunction.Instance.GetFunction("dot", ShaderType.F32, vec4f32t, vec4f32t));

        RegisterReadOnly(typeof(StructuredBuffer<float>),
            StructuredBufferLengthOperation<FloatType<N32>>.Instance,
            StructuredBufferLoadOperation<FloatType<N32>>.Instance);
        RegisterReadOnly(typeof(StructuredBuffer<int>),
            StructuredBufferLengthOperation<IntType<N32>>.Instance,
            StructuredBufferLoadOperation<IntType<N32>>.Instance);
        RegisterReadOnly(typeof(StructuredBuffer<uint>),
            StructuredBufferLengthOperation<UIntType<N32>>.Instance,
            StructuredBufferLoadOperation<UIntType<N32>>.Instance);

        void RegisterReadOnly(
            Type buffer,
            IReadOnlyStructuredBufferLengthOperation length,
            IReadOnlyStructuredBufferLoadOperation load)
        {
            result.Add(
                buffer.GetProperty(nameof(StructuredBuffer<float>.Length))?.GetMethod
                ?? throw new MissingMethodException(buffer.FullName, "get_Length"),
                length.Function);
            result.Add(
                buffer.GetProperty("Item")?.GetMethod
                ?? throw new MissingMethodException(buffer.FullName, "get_Item"),
                load.Function);
        }

        RegisterWritable(typeof(RWStructuredBuffer<float>),
            ReadWriteStructuredBufferLengthOperation<FloatType<N32>>.Instance,
            ReadWriteStructuredBufferLoadOperation<FloatType<N32>>.Instance,
            ReadWriteStructuredBufferStoreOperation<FloatType<N32>>.Instance);
        RegisterWritable(typeof(RWStructuredBuffer<int>),
            ReadWriteStructuredBufferLengthOperation<IntType<N32>>.Instance,
            ReadWriteStructuredBufferLoadOperation<IntType<N32>>.Instance,
            ReadWriteStructuredBufferStoreOperation<IntType<N32>>.Instance);
        RegisterWritable(typeof(RWStructuredBuffer<uint>),
            ReadWriteStructuredBufferLengthOperation<UIntType<N32>>.Instance,
            ReadWriteStructuredBufferLoadOperation<UIntType<N32>>.Instance,
            ReadWriteStructuredBufferStoreOperation<UIntType<N32>>.Instance);

        void RegisterWritable(
            Type buffer,
            IReadWriteStructuredBufferLengthOperation length,
            IReadWriteStructuredBufferLoadOperation load,
            IReadWriteStructuredBufferStoreOperation store)
        {
            var indexer = buffer.GetProperty("Item")
                ?? throw new MissingMemberException(buffer.FullName, "Item");
            result.Add(
                buffer.GetProperty(nameof(RWStructuredBuffer<float>.Length))?.GetMethod
                ?? throw new MissingMethodException(buffer.FullName, "get_Length"),
                length.Function);
            result.Add(
                indexer.GetMethod
                ?? throw new MissingMethodException(buffer.FullName, "get_Item"),
                load.Function);
            result.Add(
                indexer.SetMethod
                ?? throw new MissingMethodException(buffer.FullName, "set_Item"),
                store.Function);
        }

        result.Add(TextureSampleLevelMethod, TextureSampleLevelOperation.Instance.Function);

        return result;
    }

    internal static bool IsStructuredBufferFamily(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() is var definition &&
        (definition == typeof(StructuredBuffer<>) || definition == typeof(RWStructuredBuffer<>));

    internal static bool ContainsStructuredBuffer(Type type) =>
        IsStructuredBufferFamily(type) ||
        type.IsFunctionPointer &&
        (ContainsStructuredBuffer(type.GetFunctionPointerReturnType()) ||
         type.GetFunctionPointerParameterTypes().Any(ContainsStructuredBuffer)) ||
        type.HasElementType && type.GetElementType() is { } element && ContainsStructuredBuffer(element) ||
        type.IsGenericType && type.GetGenericArguments().Any(ContainsStructuredBuffer);

    internal static bool IsTexture2DFamily(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(Texture2D<>);

    internal static bool IsTextureOrSamplerFamily(Type type) =>
        IsTexture2DFamily(type) || type == typeof(SamplerState);

    internal static bool IsResourceFamily(Type type) =>
        IsStructuredBufferFamily(type) || IsTextureOrSamplerFamily(type);

    internal static bool ContainsShaderResource(Type type) =>
        IsResourceFamily(type) ||
        type.IsFunctionPointer &&
        (ContainsShaderResource(type.GetFunctionPointerReturnType()) ||
         type.GetFunctionPointerParameterTypes().Any(ContainsShaderResource)) ||
        type.HasElementType && type.GetElementType() is { } element && ContainsShaderResource(element) ||
        type.IsGenericType && type.GetGenericArguments().Any(ContainsShaderResource);
}