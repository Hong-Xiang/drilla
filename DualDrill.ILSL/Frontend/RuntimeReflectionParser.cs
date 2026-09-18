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
    private readonly HashSet<MethodBase> CompletedMethodDefinitions = [];
    private readonly HashSet<MethodBase> InProgressMethodDefinitions = [];
    private MethodBase? FailedMethod;

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
        EnsureUsable();
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
            ConstructorInfo c => ParseType(c.DeclaringType ??
                                           throw new NotSupportedException($"Constructor {c} has no declaring type.")),
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
        EnsureUsable();
        var declaration = ParseMethodDeclaration(method);
        if (!IsMethodDefinition(method) || CompletedMethodDefinitions.Contains(method) ||
            InProgressMethodDefinitions.Contains(method))
            return declaration;

        InProgressMethodDefinitions.Add(method);
        var completed = false;
        try
        {
            var symbol = Symbol.Function(method);
            var rawCode = CilMethodDecoder.Decode(method);
            foreach (var variable in rawCode.Environment.LocalVariables)
                _ = ParseType(variable.LocalType);

            var methodTable = CreateMethodTable(rawCode.Environment, declaration);
            var preAnnotatedCode = CilPreStackAnalyzer.Analyze(
                rawCode,
                declaration,
                methodTable,
                callee => _ = ParseMethodDeclaration(callee));
            var graph = CilControlFlowGraphBuilder.Build(rawCode, preAnnotatedCode);
            var model = new MethodBodyAnalysisModel(
                declaration,
                rawCode,
                preAnnotatedCode,
                new Annotated<ControlFlowGraph<CilInstructionBlock>, ControlFlowAnalysis>(
                    graph,
                    graph.ControlFlowAnalysis(),
                    CilStagePrettyPrinter.PrintAnalyzedControlFlow));
            Context.AddFunctionDefinition(symbol, declaration, model);

            foreach (var callee in FilterCalledMethods(model.CalledMethods()).Distinct())
                _ = ParseMethod(callee);

            if (model.Environment.Body is not null)
                MethodBodies.Add(declaration, ParseMethodBody3Core(declaration, model));

            CompletedMethodDefinitions.Add(method);
            completed = true;
            return declaration;
        }
        finally
        {
            InProgressMethodDefinitions.Remove(method);
            if (!completed)
                MarkFailed(method);
        }
    }

    private FunctionDeclaration ParseMethodDeclaration(MethodBase method)
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

        var decl = new FunctionDeclaration(
            method.Name,
            method.IsStatic
                ? [.. method.GetParameters().Select(ParseParameter)]
                :
                [
                    new ParameterDeclaration("this",
                        ParseType(method.DeclaringType ??
                                  throw new NotSupportedException($"Method {method} has no declaring type.")),
                        []),
                    .. method.GetParameters().Select(ParseParameter)
                ],
            ParseMethodReturn(method),
            ParseAttribute(method));

        Context.AddFunctionDeclaration(symbol, decl);
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
        EnsureUsable();
        var model = Context.GetFunctionDefinition(f);
        var completed = false;
        try
        {
            var result = ParseMethodBody3Core(f, model);
            completed = true;
            return result;
        }
        finally
        {
            if (!completed)
                MarkFailed(model.Environment.Method);
        }
    }

    private FunctionBody4 ParseMethodBody3Core(FunctionDeclaration f, MethodBodyAnalysisModel model)
    {
        var environment = model.Environment;
        var methodTable = CreateMethodTable(environment, f);
        var graph = model.ControlFlow.Node;
        var analysis = model.ControlFlow.Annotation;
        Dictionary<Label, ShaderRegionBody> basicBlocks = [];

        foreach (var l in graph.Labels())
        {
            Debug.WriteLine($"Label {l} == ");
            var cilBlock = graph[l];
            var inputStack = CreateInputStack(cilBlock.EntryStack.Types);
            var visitor = new RuntimeReflectionInstructionParserVisitor3(
                environment,
                f,
                cilBlock.Terminator,
                inputStack);
            foreach (var annotatedInstruction in cilBlock.Instructions)
            {
                var cilInst = annotatedInstruction.Node;
                ValidateStack(environment, cilInst, annotatedInstruction.Annotation, visitor.Stack);
                //Debug.Write($"parse {cilInst.Instruction.OpCode}");
                cilInst.Evaluate(visitor, environment.IsStatic, methodTable);
                //Debug.Write(" -> ");
                //Debug.WriteLine(string.Join(", ", visitor.Stack.Select(v => visitor.GetValueType(v).Name)));
            }

            {
                var args = visitor.GetStackOutput();
                var terminator = visitor.Terminator;
                if (terminator is null)
                {
                    terminator = cilBlock.Terminator switch
                    {
                        CilControlFlow.FallThrough { Target: var target } =>
                            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                                new RegionJump<IShaderValue>(target, args)),
                        CilControlFlow.EndOfCode =>
                            throw new NotSupportedException(
                                $"Method {environment.Method} reaches the end of CIL without an explicit return."),
                        _ => throw new ValidationException(
                            $"Native CIL control at IL_{cilBlock.ByteOffset:X4} did not produce a terminator.",
                            environment.Method)
                    };
                }
                else if (cilBlock.Terminator is CilControlFlow.FallThrough or CilControlFlow.EndOfCode)
                {
                    throw new ValidationException(
                        $"Synthetic CIL control at IL_{cilBlock.ByteOffset:X4} produced a native terminator.",
                        environment.Method);
                }

                var successor = cilBlock.Terminator.ToSuccessor();

                foreach (var tl in successor.AllTargets())
                {
                    var target = graph[tl].Instructions[0];
                    ValidateStack(environment, target.Node, target.Annotation, visitor.Stack);
                }

                basicBlocks.Add(l, new ShaderRegionBody(
                    l,
                    [.. inputStack.Reverse()],
                    Seq.Create(
                        [.. visitor.Instructions],
                        terminator ?? throw new NotSupportedException("failed to resolve terminator")
                    ),
                    analysis.PostDominatorTree.ImmediatePostDominator(l)
                ));
            }
        }

        return new FunctionBody4(
            f,
            RegionTree.Create(
                analysis,
                basicBlocks.Select(kv => (kv.Key, kv.Value))
            )
        );
    }

    private void EnsureUsable()
    {
        if (FailedMethod is not null)
            throw new InvalidOperationException(
                $"This runtime-reflection parser failed while compiling {FailedMethod}; " +
                "create a new parser with a fresh compilation context.");
    }

    private void MarkFailed(MethodBase method)
    {
        FailedMethod ??= method;
        CompletedMethodDefinitions.Clear();
        MethodBodies.Clear();
    }

    private CompilationContext CreateMethodTable(CilMethodEnvironment environment, FunctionDeclaration function)
    {
        var methodTable = new CompilationContext(Context);
        foreach (var variable in environment.LocalVariables)
            _ = ParseLocalVariable(variable, methodTable);
        foreach (var (index, parameter) in function.Parameters.Index())
            methodTable.AddParameter(Symbol.Parameter(index), parameter);
        return methodTable;
    }

    private static ImmutableStack<IShaderValue> CreateInputStack(ImmutableStack<CilStackType> types)
    {
        ImmutableStack<IShaderValue> result = [];
        foreach (var type in types.Reverse())
            result = result.Push(ShaderValue.Intermediate(type.ShaderType));
        return result;
    }

    private static void ValidateStack(
        CilMethodEnvironment environment,
        CilInstructionInfo instruction,
        PreStack pre,
        ImmutableStack<IShaderValue> actual)
    {
        var expected = pre.Types;
        var actualTypes = actual.Select(value => CilStackType.FromShaderType(value.Type));
        if (!expected.SequenceEqual(actualTypes))
            throw new ValidationException(
                $"Value stack does not match analyzed Pre stack at IL_{instruction.ByteOffset:X4}: " +
                $"expected [{string.Join(", ", expected)}], got [{string.Join(", ", actualTypes)}].",
                environment.Method);
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

    private bool IsMethodDefinition(MethodBase method) =>
        !SharedBuiltinSymbolTable.Instance.RuntimeMethods.ContainsKey(method) &&
        !method.GetCustomAttributes().Any(attribute =>
            attribute is IOperationMethodAttribute or IShaderOperationMethodAttribute);

    private IEnumerable<MethodBase> FilterCalledMethods(IEnumerable<MethodBase> calleeCandidates)
    {
        return calleeCandidates
            .Where(m =>
            {
                if (!IsMethodDefinition(m)) return false;

                // generated getter and setters should not be considered
                var declaringType = m.DeclaringType ??
                                    throw new NotSupportedException($"Method {m} has no declaring type.");
                if (Context[declaringType] is IVecType) return false;

                if (m.IsSpecialName &&
                    m.CustomAttributes.Any(a => a.AttributeType == typeof(CompilerGeneratedAttribute)))
                {
                    var props = declaringType.GetProperties(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (props.Any(p => p.GetMethod == m || p.SetMethod == m)) return false;
                }

                return true;
            });
    }
}