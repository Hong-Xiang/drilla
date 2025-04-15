using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DotNext.Collections.Generic;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Frontend;

/// <summary>
/// Parse shader module metadata from reflection APIs, 
/// including all types, shader module variables, functions signatures, etc.
/// method bodies are not parsed.
/// </summary>
/// <param name="Context"></param>
public sealed record class RuntimeReflectionParser(
    ISymbolTable Context,
    Dictionary<FunctionDeclaration, IUnifiedFunctionBody<StackInstructionBasicBlock>> MethodBodies)
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
    public ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>> ParseShaderModule(
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
                MethodBodies.Add(decl, ParseMethodBody2(decl, model));
            }
        }
        else
        {
            Context.AddFunctionDeclaration(symbol, decl);
        }

        return decl;
    }

    public IUnifiedFunctionBody<StackInstructionBasicBlock> ParseMethodBody2(FunctionDeclaration f)
    {
        var model = Context.GetFunctionDefinition(f);
        return ParseMethodBody2(f, model);
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

    public IUnifiedFunctionBody<StackInstructionBasicBlock> ParseMethodBody2(FunctionDeclaration f,
        MethodBodyAnalysisModel model)
    {
        var methodTable = new CompilationContext(Context);
        foreach (var v in model.LocalVariables)
        {
            _ = ParseLocalVariable(v, methodTable);
        }

        {
            foreach (var (index, p) in f.Parameters.Index())
            {
                methodTable.AddParameter(Symbol.Parameter(index), p);
            }
        }


        Dictionary<Label, ImmutableArray<IShaderType>> BasicBlockInputs = new()
        {
            [model.ControlFlowGraph.EntryLabel] = []
        };
        Dictionary<Label, ImmutableArray<IShaderType>> BasicBlockOutputs = [];
        Dictionary<Label, StackInstructionBasicBlock> basicBlocks = [];

        foreach (var l in model.ControlFlowGraph.Labels())
        {
            var visitor = new RuntimeReflectionParserInstructionVisitor(f,
                model,
                l,
                BasicBlockInputs[l],
                model.ControlFlowGraph.Successor(l)
            );
            var ilRange = model.ControlFlowGraph[l];
            for (var i = ilRange.InstructionIndex; i < ilRange.InstructionIndex + ilRange.InstructionCount; i++)
            {
                var cilInst = model[i];
                cilInst.Evaluate(visitor, model.IsStatic, methodTable);
            }

            visitor.FlushOutputs();


            BasicBlockOutputs.Add(l, visitor.Outputs);


            {
                var successor = model.ControlFlowGraph.Successor(l);

                foreach (var target in successor.AllTargets())
                {
                    if (BasicBlockInputs.TryGetValue(target, out var existed))
                    {
                        if (visitor.Outputs.Length != existed.Length)
                        {
                            throw new ValidationException(
                                $"Successor's input stack size {existed.Length} not matching current output {visitor.Outputs.Length}",
                                model.Method);
                        }


                        if (!visitor.Outputs.SequenceEqual(existed))
                        {
                        }

                        foreach (var (c, e) in visitor.Outputs.Zip(existed))
                        {
                            if (!c.Equals(e))
                            {
                                throw new ValidationException(
                                    $"Successor's input stack size {e.Name} not matching current output {c.Name}",
                                    model.Method);
                            }
                        }
                    }
                    else
                    {
                        BasicBlockInputs.Add(target, visitor.Outputs);
                    }
                }

                var bb = new StackInstructionBasicBlock(
                    l,
                    [..visitor.Instructions],
                    BasicBlockInputs[l],
                    BasicBlockOutputs[l]);

                Debug.Assert(successor.IsCompatible(bb.Terminator));

                basicBlocks.Add(l, bb);
            }
        }

        // var bodies = model.ControlFlowGraph.Labels().ToDictionary(l => l,
        //     l =>
        //         new ControlFlowGraph<StackInstructionBasicBlock>.NodeDefinition(
        //             model.ControlFlowGraph.Successor(l),
        //             basicBlocks[l]));
        // var cfg = new ControlFlowGraph<StackInstructionBasicBlock>(
        //     model.ControlFlowGraph.EntryLabel,
        //     bodies
        // );

        return UnifiedFunctionBody.Create<StackInstructionBasicBlock>(basicBlocks.Values);
    }

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