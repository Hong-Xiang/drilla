using DotNext.Collections.Generic;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DualDrill.CLSL.Language.Region;
using ShaderValueDeclaration = DualDrill.CLSL.Language.Symbol.ShaderValueDeclaration;

namespace DualDrill.CLSL.Frontend;

/// <summary>
/// Parse shader module metadata from reflection APIs, 
/// including all types, shader module variables, functions signatures, etc.
/// method bodies are not parsed.
/// </summary>
/// <param name="Context"></param>
public sealed record class RuntimeReflectionParser(
    ISymbolTable Context,
    Dictionary<FunctionDeclaration, FunctionBody4> MethodBodies)
{
    public RuntimeReflectionParser()
        : this(CompilationContext.Create(), [])
    {
    }

    public RuntimeReflectionParser(ISymbolTable Context)
        : this(Context, [])
    {
    }

    public IShaderType ParseType(Type t)
    {
        if (Context[t] is { } found)
        {
            return found;
        }

        if (t.IsValueType)
        {
            var structureType = ParseStructDeclaration(t);
            Context.AddStructure(t, structureType);
            return structureType;
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
    StructureType ParseStructDeclaration(Type t)
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                      .Where(f => !f.Name.EndsWith("k__BackingField"));
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var result = new StructureDeclaration
        {
            Name = t.Name,
            Attributes = [.. t.GetCustomAttributes().OfType<IShaderAttribute>()]
        };
        var fieldMembers = fields.Select(ParseField);
        var propsMembers = props.Select(f => new MemberDeclaration(f.Name, ParseType(f.PropertyType),
            [.. f.GetCustomAttributes().OfType<IShaderAttribute>()]));

        result.Members = [.. fieldMembers, .. propsMembers];
        return new StructureType(result);
    }


    public VariableDeclaration ParseStaticField(FieldInfo fieldInfo)
    {
        var symbol = Symbol.Variable(fieldInfo);
        // TODO: distinct on module variable and function variable
        if (Context[symbol] is { } found)
        {
            return found;
        }

        var decl = new VariableDeclaration(
            DeclarationScope.Module,
            fieldInfo.Name,
            ParseType(fieldInfo.FieldType),
            [.. fieldInfo.GetCustomAttributes().OfType<IShaderAttribute>()]
        );

        Context.AddVariable(symbol, decl);
        return decl;
    }

    public MemberDeclaration ParseField(FieldInfo fieldInfo)
    {
        if (Context[fieldInfo] is { } found)
        {
            return found;
        }

        var decl = new MemberDeclaration(fieldInfo.Name,
            ParseType(fieldInfo.FieldType),
            [.. fieldInfo.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddStructureMember(fieldInfo, decl);
        return decl;
    }

    ImmutableHashSet<IShaderAttribute> ParseAttribute(ParameterInfo p)
    {
        return
        [
            ..p.GetCustomAttributes<BuiltinAttribute>(),
            ..p.GetCustomAttributes<LocationAttribute>(),
        ];
    }

    ImmutableHashSet<IShaderAttribute> ParseAttribute(MethodBase m)
    {
        return
        [
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

        var decl = new VariableDeclaration(
            DeclarationScope.Module,
            info.Name,
            ParseType(info.FieldType),
            [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddVariable(symbol, decl);
        return decl;
    }

    VariableDeclaration ParseModuleVariableDeclaration(PropertyInfo info)
    {
        var getter = info.GetGetMethod() ??
                     throw new NotSupportedException("Properties without getter is not supported");
        var symbol = Symbol.Variable(info);
        if (Context[symbol] is { } found)
        {
            return found;
        }

        var decl =
            new VariableDeclaration(DeclarationScope.Module,
                info.Name,
                ParseType(info.PropertyType),
                [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddVariable(symbol, decl);
        return decl;
    }

    // TODO static binding flags should not be used, add code to proper handle static readonly value
    static readonly BindingFlags VariableBindingFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

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
    public ShaderModuleDeclaration<FunctionBody4> ParseShaderModule(
        ISharpShader module)
    {
        var moduleType = module.GetType();

        var entryMethods = moduleType
                           .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                       BindingFlags.Instance)
                           .Where(m => m.GetCustomAttributes().Any(a => a is IShaderStageAttribute))
                           .OrderBy(m => m.Name)
                           .ToImmutableArray();

        var variables = ParseAllModuleVariableDeclarations(moduleType);

        foreach (var m in entryMethods)
        {
            _ = ParseMethod(m);
        }

        return new(
            [
                .. Context.StructureDeclarations,
                ..variables,
                .. Context.FunctionDeclarations
            ],
            MethodBodies.ToImmutableDictionary());
    }

    public ParameterDeclaration ParseParameter(ParameterInfo parameter)
    {
        var symbol = Symbol.Parameter(parameter);
        if (Context[symbol] is { } found)
        {
            return found;
        }

        var p = new ParameterDeclaration(
            parameter.Name ?? throw new NotSupportedException("Can not parse parameter without name"),
            ParseType(parameter.ParameterType),
            ParseAttribute(parameter));
        Context.AddParameter(symbol, p);
        return p;
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

        var metaAttributes = method.GetCustomAttributes().OfType<IShaderMetadataAttribute>().ToFrozenSet();


        {
            if (metaAttributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } attr)
            {
                var f = attr.Operation.Function;
                Context.AddFunctionDeclaration(symbol, f);
                return f;
            }
        }

        var model = new MethodBodyAnalysisModel(method);

        var decl = new FunctionDeclaration(
            method.Name,
            method.IsStatic
                ? [.. model.Parameters.Select(ParseParameter)]
                :
                [
                    new ParameterDeclaration("this", ParseType(method.DeclaringType).GetPtrType(), []),
                    .. model.Parameters.Select(ParseParameter)
                ],
            ParseMethodReturn(method),
            ParseAttribute(method));

        if (IsMethodDefinition(method))
        {
            Context.AddFunctionDefinition(symbol, decl, model);
            {
                foreach (var v in model.LocalVariables)
                {
                    _ = ParseType(v.LocalType);
                }
            }
            var callees = FilterCalledMethods(model.CalledMethods()).ToArray();
            foreach (var callee in callees)
            {
                _ = ParseMethod(callee);
            }

            if (model.Body is not null)
            {
                MethodBodies.Add(decl, ParseMethodBody3(decl));
            }
        }
        else
        {
            Context.AddFunctionDeclaration(symbol, decl);
        }

        return decl;
    }


    VariableDeclaration ParseLocalVariable(LocalVariableInfo info, ISymbolTable methodTable)
    {
        var t = ParseType(info.LocalType);
        var result = new VariableDeclaration(
            DeclarationScope.Function,
            $"loc_{info.LocalIndex}",
            t,
            []
        );
        methodTable.AddVariable(Symbol.Variable(info), result);
        return result;
    }

    public FunctionBody4 ParseMethodBody3(FunctionDeclaration f)
    {
        var model = Context.GetFunctionDefinition(f);
        var methodTable = new CompilationContext(Context);
        Dictionary<VariableDeclaration, ShaderValueDeclaration> localValues = [];
        Dictionary<ParameterDeclaration, IParameterBinding> parameterBindings = [];
        List<ShaderValueDeclaration> localVariableDeclarations = [];
        foreach (var v in model.LocalVariables)
        {
            var loc = ParseLocalVariable(v, methodTable);
            var valueDecl = new ShaderValueDeclaration(ShaderValue.Create($"loc_{v.LocalIndex}"), loc.Type.GetPtrType());
            localValues.Add(loc, valueDecl);
            localVariableDeclarations.Add(valueDecl);
        }

        List<IParameterBinding> parameterBindingsList = [];
        {
            foreach (var (index, p) in f.Parameters.Index())
            {
                methodTable.AddParameter(Symbol.Parameter(index), p);
                var binding = new ParameterPointerBinding(ShaderValue.Create(p.Name), p);
                parameterBindings.Add(p, binding);
                parameterBindingsList.Add(binding);
            }
        }

        Dictionary<Label, ImmutableStack<ShaderValueDeclaration>> basicBlockInputs = new()
        {
            [model.ControlFlowGraph.EntryLabel] = []
        };
        //Dictionary<Label, ImmutableStack<ValueDeclaration>> basicBlockOutputs = [];
        Dictionary<Label, ShaderRegionBody> basicBlocks = [];

        foreach (var l in model.ControlFlowGraph.Labels())
        {
            var visitor = new RuntimeReflectionInstructionParserVisitor3(
                model,
                f,
                model.ControlFlowGraph.Successor(l),
                basicBlockInputs[l],
                localValues,
                parameterBindings
            );
            var ilRange = model.ControlFlowGraph[l];
            for (var i = ilRange.InstructionIndex; i < ilRange.InstructionIndex + ilRange.InstructionCount; i++)
            {
                var cilInst = model[i];
                cilInst.Evaluate(visitor, model.IsStatic, methodTable);
            }

            //basicBlockOutputs.Add(l, visitor.Stack);

            ITerminator<RegionJump, ShaderValue>? terminator = visitor.Terminator;


            {
                var successor = model.ControlFlowGraph.Successor(l);
                var args = visitor.Stack.ToImmutableArray();
                terminator ??= Terminator.B.Br<RegionJump, ShaderValue>(new(successor.AllTargets().Single(), args));

                foreach (var tl in successor.AllTargets())
                {
                    if (basicBlockInputs.TryGetValue(tl, out var existed))
                    {
                        // if (visitor.Stack.Count() != existed.Count())
                        // {
                        //     throw new ValidationException(
                        //         $"Successor's input stack size {existed.Count()} not matching current output {visitor.Stack.Count()}",
                        //         model.Method);
                        // }
                        //
                        // if (!visitor.Stack.SequenceEqual(existed))
                        // {
                        //     throw new ValidationException(
                        //         $"Successor's input stack not match",
                        //         model.Method);
                        // }
                        // TODO: should we add validation here?
                    }
                    else
                    {
                        var inputs = ImmutableStack.CreateRange(visitor.Stack.Select(v => new ShaderValueDeclaration(ShaderValue.Create(), visitor.GetValueType(v))));
                        basicBlockInputs.Add(tl, inputs);
                    }
                }

                basicBlocks.Add(l, new ShaderRegionBody(
                    l,
                    [.. basicBlockInputs[l]],
                    Seq.Create(
                        [.. visitor.Statements],
                        terminator ?? throw new NotSupportedException("failed to resolve terminator")
                    )
                ));
            }
        }

        // return new StackIRFunctionBody3(
        //     model.ControlFlowGraph.EntryLabel,
        //     basicBlocks.ToFrozenDictionary()
        // );
        return new FunctionBody4(
            [.. parameterBindingsList],
            [.. localVariableDeclarations],
            RegionTree.Create<ShaderRegionBody>(
                model.ControlFlowGraph.EntryLabel,
                x => x.Body.Last.ToSuccessor(),
                basicBlocks.Select(kv => (kv.Key, kv.Value))
            )
        );
    }

    //ILocalDeclarationContext GetMethodLocalDeclaration(MethodBase method)
    //{
    //    var methodBody = method.GetMethodBody();
    //    if (methodBody is null)
    //    {
    //        return LocalDeclarationContext.Empty;
    //    }

    //    var instructions = method.GetInstructions();
    //    if (instructions is null)
    //    {
    //        return LocalDeclarationContext.Empty;
    //    }

    //    throw new NotImplementedException();
    //}

    bool IsMethodDefinition(MethodBase m)
    {
        return !SharedBuiltinSymbolTable.Instance.RuntimeMethods.ContainsKey(m);
    }

    IEnumerable<MethodBase> FilterCalledMethods(IEnumerable<MethodBase> calleeCandidates)
    {
        return calleeCandidates
            .Where(m =>
            {
                if (!IsMethodDefinition(m))
                {
                    return false;
                }

                // generated getter and setters should not be considered
                var t = m.DeclaringType;
                if (Context[t] is IVecType st)
                {
                    return false;
                }

                if (m.IsSpecialName &&
                    m.CustomAttributes.Any(a => a.AttributeType == typeof(CompilerGeneratedAttribute)))
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