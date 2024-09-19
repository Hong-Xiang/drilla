using DualDrill.ILSL.IR.Declaration;
using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed class MetadataParser
{
    Dictionary<MethodBase, FunctionDeclaration> ContextMethods = BuiltinMethods();
    Dictionary<MethodBase, bool> NeedBody = [];

    FrozenDictionary<Type, IType> BuiltinTypeMap = new Dictionary<Type, IType>()
    {
        [typeof(bool)] = new BoolType(),
        [typeof(int)] = new IntType<B32>(),
        [typeof(uint)] = new UIntType<B32>(),
        [typeof(long)] = new IntType<B64>(),
        [typeof(ulong)] = new UIntType<B64>(),
        [typeof(Half)] = new FloatType<B16>(),
        [typeof(float)] = new FloatType<B32>(),
        [typeof(double)] = new FloatType<B64>(),
        [typeof(Vector4)] = new VecType<R4, FloatType<B32>>(),
        [typeof(Vector3)] = new VecType<R3, FloatType<B32>>(),
        [typeof(Vector2)] = new VecType<R2, FloatType<B32>>(),
    }.ToFrozenDictionary();


    public MetadataParser()
    {
    }

    static Dictionary<MethodBase, FunctionDeclaration> BuiltinMethods()
    {
        var result = new Dictionary<MethodBase, FunctionDeclaration>
        {
            {
                typeof(Vector4).GetConstructor(BindingFlags.Public | BindingFlags.Instance, [typeof(float), typeof(float), typeof(float), typeof(float)]),
                VecType<R4, FloatType<B32>>.Constructors[4]
            }
        };

        return result;
    }

    IType ParseTypeReference(Type t)
    {
        if (BuiltinTypeMap.TryGetValue(t, out var bt))
        {
            return bt;
        }
        // TODO: array type support?
        return t switch
        {
            _ when t == typeof(float) => new FloatType<B32>(),
            _ => throw new NotSupportedException($"{nameof(ParseTypeReference)} does not support {t}")
        };
    }

    ImmutableHashSet<IR.IAttribute> ParseAttribute(ParameterInfo p)
    {
        return [
            ..p.GetCustomAttributes<BuiltinAttribute>(),
            ..p.GetCustomAttributes<LocationAttribute>(),
        ];
    }
    ImmutableHashSet<IR.IAttribute> ParseAttribute(MethodBase m)
    {
        return [
            ..m.GetCustomAttributes<VertexAttribute>(),
            ..m.GetCustomAttributes<FragmentAttribute>(),
        ];
    }


    static readonly BindingFlags TargetMethodBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public IR.Module ParseModule(IShaderModule module)
    {
        var moduleType = module.GetType();
        var methods = moduleType.GetMethods(TargetMethodBindingFlags);
        var context = ParserContext.Create();
        foreach (var m in methods)
        {
            var shaderStageAttributes = m.GetCustomAttributes().OfType<IShaderStageAttribute>().Any();
            if (shaderStageAttributes)
            {
                _ = ParseMethod(m);
            }
        }
        return new([.. context.FunctionDeclarations.Values]);
    }

    public FunctionDeclaration ParseMethod(MethodBase method)
    {
        var returnType = method switch
        {
            MethodInfo m => ParseTypeReference(m.ReturnType),
            ConstructorInfo c => ParseTypeReference(c.DeclaringType),
            _ => throw new NotSupportedException($"Unsupported method {method}")
        };
        var returnAttributes = method switch
        {
            MethodInfo m => ParseAttribute(m.ReturnParameter),
            ConstructorInfo m => [],
            _ => throw new NotSupportedException($"Unsupported method {method}")
        };

        return new FunctionDeclaration(
            method.Name,
            [.. method.GetParameters()
                      .Select(p => new ParameterDeclaration(p.Name, ParseTypeReference(p.ParameterType), ParseAttribute(p)))],
            new FunctionReturn(returnType, returnAttributes),
            ParseAttribute(method)
        );

        foreach (var inst in method.GetInstructions())
        {
            switch (inst.Operand)
            {
                case MethodInfo m:
                    break;
                case ConstructorInfo c:
                    break;
                default:
                    break;
            }
        }
    }

    public void AddShaderModule(IShaderModule module)
    {
    }

    public IR.Module Build()
    {
        return new([]);
    }
}
