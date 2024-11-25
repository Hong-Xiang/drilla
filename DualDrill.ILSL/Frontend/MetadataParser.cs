using DotNext.Reflection;
using DualDrill.Common.Nat;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.Mathematics;
using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language;
using System.Diagnostics;

namespace DualDrill.ILSL.Frontend;

public sealed class MetadataParser
{
    public ParserContext Context { get; } = ParserContext.Create();
    Dictionary<MethodBase, FunctionDeclaration> NeedParseBody = [];

    FrozenDictionary<Type, IShaderType> BuiltinTypeMap = new Dictionary<Type, IShaderType>()
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
        [typeof(vec4f32)] = ShaderType.GetVecType(N4.Instance, ShaderType.F32),
        [typeof(vec3f32)] = ShaderType.GetVecType(N3.Instance, ShaderType.F32),
        [typeof(vec2f32)] = ShaderType.GetVecType(N2.Instance, ShaderType.F32),
    }.ToFrozenDictionary();


    //static Dictionary<MethodBase, FunctionDeclaration> BuiltinMethods()
    //{
    //    var result = new Dictionary<MethodBase, FunctionDeclaration>
    //    {
    //        {
    //            typeof(Vector4).GetConstructor(BindingFlags.Public | BindingFlags.Instance, [typeof(float), typeof(float), typeof(float), typeof(float)]),
    //            VecType<N4, FloatType<N32>>.Constructors[4]
    //        }
    //    };

    //    return result;
    //}

    IShaderType ParseTypeReference(Type t)
    {
        if (BuiltinTypeMap.TryGetValue(t, out var bt))
        {
            return bt;
        }
        // TODO: array type support?
        if (Context.StructDeclarations.TryGetValue(t, out var ct))
        {
            //return ct;
            throw new NotImplementedException();
        }
        // TODO: may be we should use is value type
        if (t.IsValueType)
        {
            throw new NotImplementedException();
            //var decl = ParseStructDeclaration(t);
            //Context.StructDeclarations.Add(t, decl);
            //return decl;
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

    ImmutableHashSet<IShaderAttribute> ParseAttribute(ParameterInfo p)
    {
        return [
            ..p.GetCustomAttributes<BuiltinAttribute>(),
            ..p.GetCustomAttributes<LocationAttribute>(),
        ];
    }
    ImmutableHashSet<IShaderAttribute> ParseAttribute(MethodBase m)
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


    public CLSL.Language.IR.Module ParseModule(IShaderModule module)
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
                _ = ParseMethodMetadata(m);
            }
        }
        return Build();
    }

    public FunctionDeclaration ParseMethodMetadata(MethodBase method)
    {
        if (Context.FunctionDeclarations.TryGetValue(method, out var result))
        {
            return result;
        }

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

        if (!IsRuntimeMethod(method))
        {
            var callees = GetCalledMethods(method);
            foreach (var callee in callees)
            {
                _ = ParseMethodMetadata(callee);
            }
        }
        return decl;
    }

    bool IsRuntimeMethod(MethodBase m)
    {
        if (m.DeclaringType == typeof(DMath))
        {
            return true;
        }
        if (m.DeclaringType == typeof(Vector4))
        {
            return true;
        }
        return false;
    }

    IEnumerable<MethodBase> GetCalledMethods(MethodBase method)
    {
        return method.GetInstructions().Where(op => op.Operand is MethodBase).Select(op => (MethodBase)op.Operand);
    }

    public void ParseFunctionBodies(IMethodParser frontend)
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
        var staticEnv = symbols.ToImmutableDictionary();
        foreach (var (m, f) in NeedParseBody)
        {
            var env = staticEnv;
            foreach (var p in f.Parameters)
            {
                if (env.ContainsKey(p.Name))
                {
                    env = env.SetItem(p.Name, p);
                }
                else
                {
                    env = env.Add(p.Name, p);
                }
            }
            f.Body = frontend.ParseMethodBody(env, m);
        }
    }

    public CLSL.Language.IR.Module Build()
    {
        return new([.. Context.VariableDeclarations.Values, .. Context.StructDeclarations.Values, .. Context.FunctionDeclarations.Values]);
    }
}
