using DotNext.Reflection;

using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.ILSL.Compiler;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DualDrill.ILSL.Frontend;
public sealed record class DeclarationsContext(
     List<StructureDeclaration> Types,
     List<VariableDeclaration> ModuleVariables,
     List<FunctionDeclaration> Functions
)
{
    public static DeclarationsContext Create() => new([], [], []);
}


public sealed record class ShaderModuleParser(CompilationContext Context, DeclarationsContext Declarations)
{


    //public ShaderModuleCompilationContext Context { get; } = ShaderModuleCompilationContext.Create();


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

        return new OpaqueType(t);
    }

    /// <summary>
    /// Parse new struct declaration based on relfection APIs
    /// currently only supports structs
    /// all access control are ignored (all fields, properties, methods are treated as public)
    /// basically it will parse all fields (including privates) and properties with automatically generated get, set, init etc.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
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



        Declarations.Types.Add(result);

        // for custom structs, a new zero-value constructor runtime method is added
        Context.ZeroValueConstructors.Add(result, new FunctionDeclaration(
            result.Name,
            [],
            new FunctionReturn(result, []),
            [new ZeroValueBuiltinFunctionAttribute(), new ShaderRuntimeMethodAttribute()]
        ));
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



    VariableDeclaration ParseModuleVariableDeclaration(FieldInfo info)
    {
        if (Context.FieldVariables.TryGetValue(info, out var result))
        {
            return result;
        }
        var decl = new VariableDeclaration(DeclarationScope.Module, Declarations.ModuleVariables.Count, info.Name, ParseType(info.FieldType), [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Declarations.ModuleVariables.Add(decl);
        Context.FieldVariables.Add(info, decl);
        return decl;
    }
    VariableDeclaration ParseModuleVariableDeclaration(PropertyInfo info)
    {
        var getter = info.GetGetMethod() ?? throw new NotSupportedException("Properties without getter is not supported");
        if (Context.PropertyGetterVariables.TryGetValue(getter, out var result))
        {
            return result;
        }
        var decl = new VariableDeclaration(DeclarationScope.Module, -1, info.Name, ParseType(info.PropertyType), [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.PropertyGetterVariables.Add(getter, decl);
        return decl;
    }

    // TODO static binding flags should not be used, add code to proper handle static readonly value
    static readonly BindingFlags VariableBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    IReadOnlyList<VariableDeclaration> ParseAllModuleVariableDeclarations(Type moduleType)
    {
        var fields = moduleType.GetFields(VariableBindingFlags);
        fields = [.. fields.Where(f => f.GetCustomAttributes().Any(a => a is IShaderAttribute))];
        return [.. fields.Select(ParseModuleVariableDeclaration)];
    }

    static readonly BindingFlags TargetMethodBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    public ShaderModuleDeclaration ParseShaderModule(ISharpShader module)
    {
        var moduleType = module.GetType();

        var entryMethods = moduleType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                                     .Where(m => m.GetCustomAttributes().Any(a => a is IShaderStageAttribute))
                                     .OrderBy(m => m.Name)
                                     .ToImmutableArray();

        ParseAllModuleVariableDeclarations(moduleType);

        foreach (var m in entryMethods)
        {
            _ = ParseMethodMetadata(m);
        }
        var functionBodies = Declarations.Functions
            .Select(f => KeyValuePair.Create(f, (IFunctionBody)new EmptyFunctionBody()))
            .ToImmutableDictionary();
        return new(
            [.. Declarations.Types, .. Declarations.ModuleVariables, .. Declarations.Functions],
            functionBodies);
    }

    ParameterDeclaration ParseParameter(ParameterInfo parameter)
    {
        return new ParameterDeclaration(
            parameter.Name ?? throw new NotSupportedException("Can not parse parameter without name"),
            ParseType(parameter.ParameterType),
            ParseAttribute(parameter));
    }

    FunctionReturn ParseReturn(MethodBase method)
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

        return new FunctionReturn(returnType, returnAttributes);
    }



    public FunctionDeclaration ParseMethodMetadata(MethodBase method)
    {
        if (Context.Functions.TryGetValue(method, out var result))
        {
            return result;
        }

        var decl = new FunctionDeclaration(
            method.Name,
            method.IsStatic ? [.. method.GetParameters().Select(ParseParameter)]
                            : [new ParameterDeclaration("this", ParseType(method.ReflectedType), []), .. method.GetParameters().Select(ParseParameter)],
            ParseReturn(method),
            ParseAttribute(method));

        Declarations.Functions.Add(decl);
        Context.Functions.Add(method, decl);

        if (!IsRuntimeMethod(method))
        {
            var instructions = method.GetInstructions();
            if (instructions is not null)
            {
                var callees = GetCalledMethods(instructions).ToArray();
                foreach (var callee in callees)
                {
                    _ = ParseMethodMetadata(callee);
                }
            }
        }
        return decl;
    }

    bool IsRuntimeMethod(MethodBase m)
    {
        return SharedCompilationContext.Instance.RuntimeMethods.ContainsKey(m);
    }

    IEnumerable<PropertyInfo> GetReferencedProperties(IReadOnlyList<Instruction> instructions)
    {
        var operands = instructions.Select(op => op.Operand).ToArray();
        return instructions.Select(op => op.Operand)
                           .OfType<PropertyInfo>();
    }

    IEnumerable<MethodBase> GetCalledMethods(IReadOnlyList<Instruction> instructions)
    {
        return instructions.Select(op => op.Operand)
                           .OfType<MethodBase>()
                           .Where(m =>
                           {
                               // generated getter and setters should not be considered
                               var t = m.DeclaringType;
                               if (Context.Types.TryGetValue(t, out var st) && (st is IVecType))
                               {
                                   return false;
                               }
                               if (m.IsSpecialName && m.CustomAttributes.Any(a => a.AttributeType == typeof(CompilerGeneratedAttribute)))
                               {
                                   var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                   if (props.Any(p => p.GetMethod == m || p.SetMethod == m))
                                   {
                                       return false;
                                   }
                               }
                               return true;
                           });
    }

    IEnumerable<Type> GetReferencedTypes(IReadOnlyList<Instruction> instructions)
    {
        return instructions.Select(op => op.Operand)
                           .OfType<ConstructorInfo>()
                           .Select(c => c.DeclaringType)
                           .OfType<Type>();
    }

}
