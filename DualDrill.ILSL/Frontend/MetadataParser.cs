using DotNext.Reflection;
using DualDrill.Common.Nat;
using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.Types;
using DualDrill.Mathematics;
using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed class MetadataParser()
{
    ParserContext Context = ParserContext.Create();
    Dictionary<MethodBase, FunctionDeclaration> NeedParseBody = [];

    FrozenDictionary<Type, IType> BuiltinTypeMap = new Dictionary<Type, IType>()
    {
        [typeof(bool)] = new BoolType(),
        [typeof(int)] = new IntType<N32>(),
        [typeof(uint)] = new UIntType<N32>(),
        [typeof(long)] = new IntType<N64>(),
        [typeof(ulong)] = new UIntType<N64>(),
        [typeof(Half)] = new FloatType<N16>(),
        [typeof(float)] = new FloatType<N32>(),
        [typeof(double)] = new FloatType<N64>(),
        [typeof(Vector4)] = new VecType<N4, FloatType<N32>>(),
        [typeof(Vector3)] = new VecType<N3, FloatType<N32>>(),
        [typeof(Vector2)] = new VecType<N2, FloatType<N32>>(),
        [typeof(vec4f32)] = new VecType<N4, FloatType<N32>>(),
        [typeof(vec3f32)] = new VecType<N3, FloatType<N32>>(),
        [typeof(vec2f32)] = new VecType<N2, FloatType<N32>>(),
    }.ToFrozenDictionary();


    static Dictionary<MethodBase, FunctionDeclaration> BuiltinMethods()
    {
        var result = new Dictionary<MethodBase, FunctionDeclaration>
        {
            {
                typeof(Vector4).GetConstructor(BindingFlags.Public | BindingFlags.Instance, [typeof(float), typeof(float), typeof(float), typeof(float)]),
                VecType<N4, FloatType<N32>>.Constructors[4]
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
        if (Context.StructDeclarations.TryGetValue(t, out var ct))
        {
            return ct;
        }
        // TODO: may be we should use is value type
        if (t.IsValueType)
        {
            var decl = ParseStructDeclaration(t);
            Context.StructDeclarations.Add(t, decl);
            return decl;
        }

        throw new NotSupportedException($"{nameof(ParseTypeReference)} does not support {t}");
    }

    StructureDeclaration ParseStructDeclaration(Type t)
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                      .Where(f => !f.Name.EndsWith("k__BackingField"));
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldMembers = fields.Select(f => new MemberDeclaration(f.Name, ParseTypeReference(f.FieldType), [.. f.GetCustomAttributes().OfType<IShaderAttribute>()]));
        var propsMembers = props.Select(f => new MemberDeclaration(f.Name, ParseTypeReference(f.PropertyType), [.. f.GetCustomAttributes().OfType<IShaderAttribute>()]));

        var result = new StructureDeclaration(t.Name, [
            ..fieldMembers,
            ..propsMembers
            ], [.. t.GetCustomAttributes().OfType<IShaderAttribute>()]);
        return result;
    }

    ImmutableHashSet<IR.IShaderAttribute> ParseAttribute(ParameterInfo p)
    {
        return [
            ..p.GetCustomAttributes<BuiltinAttribute>(),
            ..p.GetCustomAttributes<LocationAttribute>(),
        ];
    }
    ImmutableHashSet<IR.IShaderAttribute> ParseAttribute(MethodBase m)
    {
        return [
            ..m.GetCustomAttributes<VertexAttribute>(),
            ..m.GetCustomAttributes<FragmentAttribute>(),
            ..m.GetCustomAttributes<ShaderMethodAttribute>(),
        ];
    }


    static readonly BindingFlags TargetMethodBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly BindingFlags TargetVariableBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    VariableDeclaration ParseModuleVariableDeclaration(FieldInfo info)
    {
        if (Context.VariableDeclarations.TryGetValue(info, out var result))
        {
            return result;
        }
        var decl = new VariableDeclaration(DeclarationScope.Module, info.Name, ParseTypeReference(info.FieldType), [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.VariableDeclarations.Add(info, decl);
        return decl;
    }
    VariableDeclaration ParseModuleVariableDeclaration(PropertyInfo info)
    {
        if (Context.VariableDeclarations.TryGetValue(info, out var result))
        {
            return result;
        }
        var decl = new VariableDeclaration(DeclarationScope.Module, info.Name, ParseTypeReference(info.PropertyType), [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.VariableDeclarations.Add(info, decl);
        return decl;

    }


    public IR.Module ParseModule(IShaderModule module)
    {
        var moduleType = module.GetType();
        var fieldVars = moduleType.GetFields(TargetVariableBindingFlags)
                                  .Where(f => !f.Name.EndsWith("k__BackingField"));
        var propVars = moduleType.GetProperties(TargetVariableBindingFlags);
        foreach (var v in fieldVars)
        {
            _ = ParseModuleVariableDeclaration(v);
        }
        foreach (var v in propVars)
        {
            _ = ParseModuleVariableDeclaration(v);
        }
        var methods = moduleType.GetMethods(TargetMethodBindingFlags);
        foreach (var m in methods)
        {
            var shaderStageAttributes = m.GetCustomAttributes().OfType<IShaderStageAttribute>().Any();
            if (shaderStageAttributes)
            {
                _ = ParseMethod(m);
            }
        }
        return Build();
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

        var decl = new FunctionDeclaration(
            method.Name,
            [.. method.GetParameters()
                      .Select(p => new ParameterDeclaration(p.Name, ParseTypeReference(p.ParameterType), ParseAttribute(p)))],
            new FunctionReturn(returnType, returnAttributes),
            ParseAttribute(method)
        );
        NeedParseBody.Add(method, decl);
        Context.FunctionDeclarations.Add(method, decl);
        return decl;
    }

    IEnumerable<MethodBase> GetCalledMethods(MethodBase method)
    {
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
        throw new NotImplementedException();
    }

    public void ParseFunctionBodies(ILSpyFrontend frontend)
    {
        var symbols = new Dictionary<string, IDeclaration>();
        foreach (var d in Context.VariableDeclarations)
        {
            symbols.Add(d.Key.Name, d.Value);
        }
        foreach (var d in Context.FunctionDeclarations)
        {
            symbols.Add(d.Key.Name, d.Value);
        }
        // TODO: use ILSpyFrontEnd for body only, passing referenced symbols as environment
        foreach (var (m, f) in NeedParseBody)
        {
            f.Body = frontend.ParseMethod(m, symbols).Body;
        }
    }

    public IR.Module Build()
    {
        return new([.. Context.VariableDeclarations.Values, .. Context.StructDeclarations.Values, .. Context.FunctionDeclarations.Values]);
    }
}
