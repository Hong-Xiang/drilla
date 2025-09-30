using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend;

/// <summary>
///     Parse shader module metadata from reflection APIs,
///     including all types, shader module variables, functions signatures, etc.
///     method bodies are not parsed.
/// </summary>
/// <param name="Context"></param>
public sealed record class RuntimeReflectionParser(
    ISymbolTable Context,
    Dictionary<FunctionDeclaration, FunctionBody4> MethodBodies)
{
    // TODO static binding flags should not be used, add code to proper handle static readonly value
    private static readonly BindingFlags VariableBindingFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

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
        if (Context[t] is { } found) return found;

        if (t.IsValueType)
        {
            var structureType = ParseStructDeclaration(t);
            Context.AddStructure(t, structureType);
            return structureType;
        }

        return new OpaqueType(t);
    }

    /// <summary>
    ///     Parse new struct declaration based on relfection APIs
    ///     currently only supports structs
    ///     all access control are ignored (all fields, properties, methods are treated as public)
    ///     basically it will parse all fields (including privates) and properties with automatically generated get, set, init
    ///     etc.
    /// </summary>
    private StructureType ParseStructDeclaration(Type t)
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
        if (Context[symbol] is { } found) return found;

        var decl = new VariableDeclaration(
            UniformAddressSpace.Instance,
            fieldInfo.Name,
            ParseType(fieldInfo.FieldType),
            [.. fieldInfo.GetCustomAttributes().OfType<IShaderAttribute>()]
        );

        Context.AddVariable(symbol, decl);
        return decl;
    }

    public MemberDeclaration ParseField(FieldInfo fieldInfo)
    {
        if (Context[fieldInfo] is { } found) return found;

        var decl = new MemberDeclaration(fieldInfo.Name,
            ParseType(fieldInfo.FieldType),
            [.. fieldInfo.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddStructureMember(fieldInfo, decl);
        return decl;
    }

    private ImmutableHashSet<IShaderAttribute> ParseAttribute(ParameterInfo p) =>
    [
        ..p.GetCustomAttributes<BuiltinAttribute>(),
        ..p.GetCustomAttributes<LocationAttribute>()
    ];

    private ImmutableHashSet<IShaderAttribute> ParseAttribute(MethodBase m) =>
    [
        //..m.GetCustomAttributes<VertexAttribute>(),
        //..m.GetCustomAttributes<FragmentAttribute>(),
        ..m.GetCustomAttributes().OfType<IShaderAttribute>()
        //..m.GetCustomAttributes<ShaderMethodAttribute>(),
    ];

    private VariableDeclaration ParseModuleVariableDeclaration(FieldInfo info)
    {
        var symbol = Symbol.Variable(info);
        if (Context[symbol] is { } found) return found;

        var addressSpace = info.GetCustomAttributes().OfType<IAddressSpaceAttribute>().Single().AddressSpace;

        var decl = new VariableDeclaration(
            addressSpace,
            info.Name,
            ParseType(info.FieldType),
            [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddVariable(symbol, decl);
        return decl;
    }

    private VariableDeclaration ParseModuleVariableDeclaration(PropertyInfo info)
    {
        var getter = info.GetGetMethod() ??
                     throw new NotSupportedException("Properties without getter is not supported");
        var symbol = Symbol.Variable(info);
        if (Context[symbol] is { } found) return found;

        var addressSpace = info.CustomAttributes.OfType<IAddressSpaceAttribute>().Single().AddressSpace;
        var decl =
            new VariableDeclaration(
                addressSpace,
                info.Name,
                ParseType(info.PropertyType),
                [.. info.GetCustomAttributes().OfType<IShaderAttribute>()]);
        Context.AddVariable(symbol, decl);
        return decl;
    }

    private IReadOnlyList<VariableDeclaration> ParseAllModuleVariableDeclarations(Type moduleType)
    {
        var fields = moduleType.GetFields(VariableBindingFlags);
        fields = [.. fields.Where(f => f.GetCustomAttributes().Any(a => a is IShaderAttribute))];
        return [.. fields.Select(ParseModuleVariableDeclaration)];
    }

    /// <summary>
    ///     Parse shader module, since compilation context is fixed for this parser
    ///     use a parser for multiple shader modules will merge them into a larger module
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

        foreach (var m in entryMethods) _ = ParseMethod(m);

        return new ShaderModuleDeclaration<FunctionBody4>(
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
        if (Context[symbol] is { } found) return found;

        var p = new ParameterDeclaration(
            parameter.Name ?? throw new NotSupportedException("Can not parse parameter without name"),
            ParseType(parameter.ParameterType),
            ParseAttribute(parameter));
        Context.AddParameter(symbol, p);
        return p;
    }

    private FunctionReturn ParseMethodReturn(MethodBase method)
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
        if (Context[symbol] is { } found) return found;

        var metaAttributes = method.GetCustomAttributes().OfType<IShaderMetadataAttribute>().ToFrozenSet();


        {
            if (metaAttributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } attr)
            {
                var f = attr.Operation.Function;
                Context.AddFunctionDeclaration(symbol, f);
                return f;
            }
        }

        {
            var shaderMethodOperationAttribute = method.GetCustomAttributes().OfType<IShaderOperationMethodAttribute>()
                                                       .SingleOrDefault();
            if (shaderMethodOperationAttribute is not null)
            {
                var result = ParseMethodReturn(method);
                var parameters = method.GetParameters().Select(ParseParameter);
                var op = shaderMethodOperationAttribute.GetOperation(result.Type, parameters.Select(p => p.Type));
                Context.AddFunctionDeclaration(symbol, op.Function);
                return op.Function;
            }
        }

        var model = new MethodBodyAnalysisModel(method);

        var isEntryMethod = method.GetCustomAttributes().Any(a => a is IShaderStageAttribute);

        var decl = new FunctionDeclaration(
            method.Name,
            method.IsStatic
                ? [.. model.Parameters.Select(ParseParameter)]
                :
                [
                    new ParameterDeclaration("this",
                        ParseType(method.DeclaringType)
                            .GetPtrType(isEntryMethod ? InputAddressSpace.Instance : FunctionAddressSpace.Instance),
                        []),
                    .. model.Parameters.Select(ParseParameter)
                ],
            ParseMethodReturn(method),
            ParseAttribute(method));

        if (IsMethodDefinition(method))
        {
            Context.AddFunctionDefinition(symbol, decl, model);
            {
                foreach (var v in model.LocalVariables) _ = ParseType(v.LocalType);
            }
            var callees = FilterCalledMethods(model.CalledMethods()).ToArray();
            foreach (var callee in callees) _ = ParseMethod(callee);

            if (model.Body is not null) MethodBodies.Add(decl, ParseMethodBody3(decl));
        }
        else
        {
            Context.AddFunctionDeclaration(symbol, decl);
        }

        return decl;
    }


    private VariableDeclaration ParseLocalVariable(LocalVariableInfo info, ISymbolTable methodTable)
    {
        var t = ParseType(info.LocalType);
        var result = new VariableDeclaration(
            FunctionAddressSpace.Instance,
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
        // TODO: avoid duplicate
        var cfa = model.ControlFlowGraph.ControlFlowAnalysis();
        var methodTable = new CompilationContext(Context);
        foreach (var v in model.LocalVariables)
        {
            var loc = ParseLocalVariable(v, methodTable);
        }

        {
            foreach (var (index, p) in f.Parameters.Index()) methodTable.AddParameter(Symbol.Parameter(index), p);
        }

        Dictionary<Label, ImmutableStack<IShaderValue>> basicBlockInputs = new()
        {
            [model.ControlFlowGraph.EntryLabel] = []
        };
        //Dictionary<Label, ImmutableStack<ValueDeclaration>> basicBlockOutputs = [];
        Dictionary<Label, ShaderRegionBody> basicBlocks = [];

        foreach (var l in model.ControlFlowGraph.Labels())
        {
            Debug.WriteLine($"Label {l} == ");
            var visitor = new RuntimeReflectionInstructionParserVisitor3(
                model,
                f,
                model.ControlFlowGraph.Successor(l),
                basicBlockInputs[l]);
            var ilRange = model.ControlFlowGraph[l];
            for (var i = ilRange.InstructionIndex; i < ilRange.InstructionIndex + ilRange.InstructionCount; i++)
            {
                var cilInst = model[i];
                Debug.Write($"parse {cilInst.Instruction.OpCode}");
                cilInst.Evaluate(visitor, model.IsStatic, methodTable);
                Debug.Write(" -> ");
                Debug.WriteLine(string.Join(", ", visitor.Stack.Select(v => visitor.GetValueType(v).Name)));
            }

            var terminator = visitor.Terminator;

            {
                var successor = model.ControlFlowGraph.Successor(l);
                var args = visitor.GetStackOutput();
                terminator ??=
                    Terminator.B.Br<RegionJump, IShaderValue>(new RegionJump(successor.AllTargets().Single(), args));

                foreach (var tl in successor.AllTargets())
                {
                    ImmutableStack<IShaderValue> output =
                        [.. visitor.Stack.Select(v => (IShaderValue)ShaderValue.Create(v.Type)).Reverse()];
                    if (basicBlockInputs.TryGetValue(tl, out var existed))
                    {
                        if (!existed.Select(v => v.Type).SequenceEqual(output.Select(v => v.Type)))
                            throw new ValidationException("Stack output mismatch", model.Method);
                    }
                    else
                    {
                        basicBlockInputs.Add(tl, output);
                    }
                }

                basicBlocks.Add(l, new ShaderRegionBody(
                    l,
                    [.. basicBlockInputs[l].Reverse()],
                    Seq.Create(
                        [.. visitor.Instructions],
                        terminator ?? throw new NotSupportedException("failed to resolve terminator")
                    ),
                    cfa.PostDominatorTree.ImmediatePostDominator(l)
                ));
            }
        }

        // return new StackIRFunctionBody3(
        //     model.ControlFlowGraph.EntryLabel,
        //     basicBlocks.ToFrozenDictionary()
        // );
        return new FunctionBody4(
            f,
            RegionTree.Create(
                cfa,
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

    private bool IsMethodDefinition(MethodBase m) => !SharedBuiltinSymbolTable.Instance.RuntimeMethods.ContainsKey(m);

    private IEnumerable<MethodBase> FilterCalledMethods(IEnumerable<MethodBase> calleeCandidates)
    {
        return calleeCandidates
            .Where(m =>
            {
                if (!IsMethodDefinition(m)) return false;

                // generated getter and setters should not be considered
                var t = m.DeclaringType;
                if (Context[t] is IVecType st) return false;

                if (m.IsSpecialName &&
                    m.CustomAttributes.Any(a => a.AttributeType == typeof(CompilerGeneratedAttribute)))
                {
                    var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (props.Any(p => p.GetMethod == m || p.SetMethod == m)) return false;
                }

                return true;
            });
    }
}