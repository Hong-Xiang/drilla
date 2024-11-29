using DotNext.Reflection;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.IR;
using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed record class CLSLParser(IMethodParser MethodParser)
{
    public ParserContext Context { get; } = ParserContext.Create();
    HashSet<TypeDeclaration> TypeDeclarations = [];
    HashSet<FunctionDeclaration> FunctionDeclarations = [];

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



    public IShaderType ParseType(Type t)
    {
        if (Context.Types.TryGetValue(t, out var foundResult))
        {
            return foundResult;
        }

        if (t.IsValueType)
        {
            var result = ParseStructDeclaration(t);
            Context.Types.Add(t, result);
            return result;
        }

        throw new NotSupportedException($"{nameof(ParseType)} can not support {t}");
    }

    StructureDeclaration ParseStructDeclaration(Type t)
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                      .Where(f => !f.Name.EndsWith("k__BackingField"));
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldMembers = fields.Select(f => new MemberDeclaration(f.Name, ParseType(f.FieldType), [.. f.GetCustomAttributes().OfType<IShaderAttribute>()]));
        var propsMembers = props.Select(f => new MemberDeclaration(f.Name, ParseType(f.PropertyType), [.. f.GetCustomAttributes().OfType<IShaderAttribute>()]));

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
        if (Context.Variables.TryGetValue(info, out var result))
        {
            return result;
        }
        var decl = new VariableDeclaration(DeclarationScope.Module, info.Name, ParseType(info.FieldType), [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.Variables.Add(info, decl);
        return decl;
    }
    VariableDeclaration ParseModuleVariableDeclaration(PropertyInfo info)
    {
        if (Context.Variables.TryGetValue(info, out var result))
        {
            return result;
        }
        var decl = new VariableDeclaration(DeclarationScope.Module, info.Name, ParseType(info.PropertyType), [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.Variables.Add(info, decl);
        return decl;

    }


    public ShaderModule ParseModule(ISharpShader module)
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
        ParseFunctionBodies();
        return new([.. Context.Variables.Values, .. TypeDeclarations, .. FunctionDeclarations]);
    }

    public FunctionDeclaration ParseMethodMetadata(MethodBase method)
    {
        if (Context.Functions.TryGetValue(method, out var result))
        {
            return result;
        }

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

        var decl = new FunctionDeclaration(
            method.Name,
            [.. method.GetParameters()
                      .Select(p => new ParameterDeclaration(p.Name, ParseType(p.ParameterType), ParseAttribute(p)))],
            new FunctionReturn(returnType, returnAttributes),
            ParseAttribute(method)
        );
        FunctionDeclarations.Add(decl);
        NeedParseBody.Add(method, decl);
        Context.Functions.Add(method, decl);

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
        return RuntimeDefinitions.Instance.RuntimeMethods.ContainsKey(m);
    }

    IEnumerable<MethodBase> GetCalledMethods(MethodBase method)
    {
        return method.GetInstructions().Select(op => op.Operand).OfType<MethodBase>();
    }

    private void ParseFunctionBodies()
    {
        foreach (var (m, f) in Context.Functions)
        {
            if (IsRuntimeMethod(m) || f.Body is not null)
            {
                continue;
            }
            f.Body = MethodParser.ParseMethodBody(Context.GetMethodContext(m), m);
        }
    }
}
