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
        [typeof(float)] = new FloatType<B32>(),
        [typeof(double)] = new FloatType<B64>(),
        [typeof(Vector4)] = new VecType<R4, FloatType<B32>>(),
    }.ToFrozenDictionary();


    public MetadataParser()
    {
    }

    static Dictionary<MethodBase, FunctionDeclaration> BuiltinMethods()
    {
        var result = new Dictionary<MethodBase, FunctionDeclaration>
        {
            {
                typeof(Vector4).GetConstructor(BindingFlags.Public, [typeof(float), typeof(float), typeof(float), typeof(float)]),
                VecType<R4, FloatType<B32>>.Constructors[4]
            }
        };

        return result;
    }

    IType ParseType(Type t)
    {
        return t switch
        {
            _ when t == typeof(float) => new FloatType<B32>(),
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

    public FunctionDeclaration ParseMethod(MethodBase method)
    {
        var returnType = method switch
        {
            MethodInfo m => ParseType(m.ReturnType),
            ConstructorInfo c => ParseType(c.DeclaringType),
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
            [.. method.GetParameters().Select(p => new ParameterDeclaration(p.Name, ParseType(p.ParameterType), ParseAttribute(p)))],
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
