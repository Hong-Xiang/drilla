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

/// <summary>
/// Parse shader module metadata from reflection APIs, 
/// including all types, shader module variables, functions signatures, etc.
/// method bodies are not parsed.
/// </summary>
/// <param name="Context"></param>
public sealed record class RuntimeReflectionShaderModuleMetadataParser(ICompilationContext Context)
{
    public IShaderType ParseType(Type t)
    {
        if (Context[t] is { } found)
        {
            return found;
        }

        if (t.IsValueType)
        {
            var result = ParseStructDeclaration(t);
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
        var result = new StructureDeclaration
        {
            Name = t.Name,
            Attributes = [.. t.GetCustomAttributes().OfType<IShaderAttribute>()]
        };
        Context.AddStructure(t, result);
        // for each custom struct definition, a new zero-value constructor runtime method is added
        Context.AddFunctionDeclaration(new ZeroValueContructorFunctionSymbol(result), new FunctionDeclaration(
            result.Name,
            [],
            new FunctionReturn(result, []),
            [new ZeroValueBuiltinFunctionAttribute(), new ShaderRuntimeMethodAttribute()]
        ));

        var fieldMembers = fields.Select(f => new MemberDeclaration(f.Name, ParseType(f.FieldType), [.. f.GetCustomAttributes().OfType<IShaderAttribute>()]));
        var propsMembers = props.Select(f => new MemberDeclaration(f.Name, ParseType(f.PropertyType), [.. f.GetCustomAttributes().OfType<IShaderAttribute>()]));

        result.Members = [.. fieldMembers, .. propsMembers];
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
            //..m.GetCustomAttributes<ShaderMethodAttribute>(),
        ];
    }

    VariableDeclaration ParseModuleVariableDeclaration(FieldInfo info)
    {
        var symbol = Symbol.Variable(info);
        if (Context[symbol] is { } found)
        {
            return found;
        }
        var decl = Context.AddVariable(symbol, (index) =>
            new VariableDeclaration(
                        DeclarationScope.Module,
                        index,
                        info.Name,
                        ParseType(info.FieldType),
                        [.. info.GetCustomAttributes().OfType<IShaderAttribute>()])
        );
        return decl;
    }
    VariableDeclaration ParseModuleVariableDeclaration(PropertyInfo info)
    {
        var getter = info.GetGetMethod() ?? throw new NotSupportedException("Properties without getter is not supported");
        var symbol = Symbol.Variable(info);
        if (Context[symbol] is { } found)
        {
            return found;
        }
        var decl = Context.AddVariable(symbol, (index) =>
            new VariableDeclaration(DeclarationScope.Module,
                                    index,
                                    info.Name,
                                    ParseType(info.PropertyType),
                                    [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]));
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

    /// <summary>
    /// Parse shader module, since compilation context is fixed for this parser
    /// use a parser for multiple shader modules will merge them into a larger module
    /// </summary>
    /// <param name="module"></param>
    /// <returns></returns>
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
            _ = ParseMethod(m);
        }
        var emptyFunctionBodies = Context.FunctionDeclarations
            .Select(f => KeyValuePair.Create(f, (IFunctionBody)new NotParsedFunctionBody(Context.GetFunctionDefinition(f))))
            .ToImmutableDictionary();
        return new(
            [.. Context.StructureDeclarations,
             .. Context.VariableDeclarations,
             .. Context.FunctionDeclarations],
            emptyFunctionBodies);
    }

    ParameterDeclaration ParseParameter(ParameterInfo parameter)
    {
        return new ParameterDeclaration(
            parameter.Name ?? throw new NotSupportedException("Can not parse parameter without name"),
            ParseType(parameter.ParameterType),
            ParseAttribute(parameter));
    }

    FunctionReturn ParseMethodReturn(MethodBase method)
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



    public FunctionDeclaration ParseMethod(MethodBase method)
    {
        var symbol = Symbol.Function(method);
        if (Context[symbol] is { } found)
        {
            return found;
        }
        var decl = new FunctionDeclaration(
            method.Name,
            method.IsStatic ? [.. method.GetParameters().Select(ParseParameter)]
                            : [new ParameterDeclaration("this", ParseType(method.DeclaringType), []), .. method.GetParameters().Select(ParseParameter)],
            ParseMethodReturn(method),
            ParseAttribute(method));
        if (!IsExternalMethod(method))
        {
            Context.AddFunctionDefinition(symbol, decl);
            {
                var body = method.GetMethodBody();
                if (body is not null)
                {
                    foreach (var v in body.LocalVariables)
                    {
                        _ = ParseType(v.LocalType);
                    }
                }
            }
            var instructions = method.GetInstructions();
            if (instructions is not null)
            {
                var callees = GetCalledMethods(instructions).ToArray();
                foreach (var callee in callees)
                {
                    _ = ParseMethod(callee);
                }
            }
        }
        else
        {
            Context.AddFunctionDeclaration(symbol, decl);
        }

        return decl;
    }

    bool IsExternalMethod(MethodBase m)
    {
        return SharedBuiltinCompilationContext.Instance.RuntimeMethods.ContainsKey(m);
    }
    IEnumerable<MethodBase> GetCalledMethods(IReadOnlyList<Instruction> instructions)
    {
        return instructions.Select(op => op.Operand)
                           .OfType<MethodBase>()
                           .Where(m =>
                           {
                               if (IsExternalMethod(m))
                               {
                                   return false;
                               }
                               // generated getter and setters should not be considered
                               var t = m.DeclaringType;
                               if (Context[t] is IVecType st)
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
}
