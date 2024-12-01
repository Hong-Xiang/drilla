﻿using DotNext.Reflection;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.IR;
using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed record class CLSLParser(IMethodParser MethodParser)
{
    public ParserContext Context { get; } = ParserContext.Create();
    HashSet<TypeDeclaration> TypeDeclarations = [];
    HashSet<FunctionDeclaration> FunctionDeclarations = [];

    Dictionary<MethodBase, FunctionDeclaration> NeedParseBody = [];

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


    public ShaderModule ParseShaderModule(ISharpShader module)
    {
        var moduleType = module.GetType();

        var entryMethods = moduleType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                                     .Where(m => m.GetCustomAttributes().Any(a => a is IShaderStageAttribute))
                                     .OrderBy(m => m.Name)
                                     .ToImmutableArray();

        //var fieldVars = moduleType.GetFields(TargetVariableBindingFlags)
        //                          .Where(f => !f.Name.EndsWith("k__BackingField"));
        //var propVars = moduleType.GetProperties(TargetVariableBindingFlags);
        //foreach (var v in fieldVars)
        //{
        //    _ = ParseModuleVariableDeclaration(v);
        //}
        //foreach (var v in propVars)
        //{
        //    _ = ParseModuleVariableDeclaration(v);
        //}
        //var methods = moduleType.GetMethods(TargetMethodBindingFlags);
        foreach (var m in entryMethods)
        {
            _ = ParseMethodMetadata(m);
        }
        ParseFunctionBodies();
        return new([.. Context.Variables.Values, .. TypeDeclarations, .. FunctionDeclarations]);
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
            [.. method.GetParameters().Select(ParseParameter)],
            ParseReturn(method),
            ParseAttribute(method));

        FunctionDeclarations.Add(decl);
        NeedParseBody.Add(method, decl);
        Context.Functions.Add(method, decl);

        if (!IsRuntimeMethod(method))
        {
            var instructions = method.GetInstructions();
            if (instructions is not null)
            {
                var callees = GetCalledMethods(instructions);
                foreach (var callee in callees)
                {
                    _ = ParseMethodMetadata(callee);
                }
            }
        }
        return decl;
    }

    public CLSL.Language.IR.Statement.CompoundStatement ParseMethodBody(MethodBase method)
    {
        if (!Context.Functions.ContainsKey(method))
        {
            _ = ParseMethodMetadata(method);
        }
        return MethodParser.ParseMethodBody(Context.GetMethodContext(method), method);
    }

    bool IsRuntimeMethod(MethodBase m)
    {
        return RuntimeDefinitions.Instance.RuntimeMethods.ContainsKey(m);
    }

    IEnumerable<MethodBase> GetCalledMethods(IReadOnlyList<Instruction> instructions)
    {
        return instructions.Select(op => op.Operand)
                           .OfType<MethodBase>();
    }

    IEnumerable<Type> GetReferencedTypes(IReadOnlyList<Instruction> instructions)
    {
        return instructions.Select(op => op.Operand)
                           .OfType<ConstructorInfo>()
                           .Select(c => c.DeclaringType)
                           .OfType<Type>();
    }

    private void ParseFunctionBodies()
    {
        foreach (var (m, f) in Context.Functions)
        {
            if (IsRuntimeMethod(m) || f.Body is not null)
            {
                continue;
            }
            var methodContext = Context.GetMethodContext(m);
            f.Body = MethodParser.ParseMethodBody(methodContext, m);
        }
    }
}
