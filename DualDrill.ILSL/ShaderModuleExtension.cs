using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Diagnostics;

namespace DualDrill.CLSL;

/// <summary>
/// Public interfaces for CLSL compiler/parser/reflection functionalities on shader module IR
/// </summary>
public static class ShaderModuleExtension
{
    public static ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> Parse(
        this ISharpShader shader
    )
    {
        var context = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(context);
        return parser.ParseShaderModule(shader);
    }

    public static ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> ReplaceOperationCallsToOperationInstruction(
        this ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> ir
    )
    {
        return ir.MapBody((m, f, b) =>
            new UnstructuredStackInstructionFunctionBody(b.Instructions.Select(inst =>
            {
                if (inst is CallInstruction call)
                {
                    if (call.Callee.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } attr)
                    {
                        return attr.Operation.Instruction;
                    }
                }
                return inst;
            }))
        );
    }
    public static ShaderModuleDeclaration<CompoundStatement> ToAbstractSyntaxTreeFunctionBody(
           this ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> module
       )
    {
        return module.MapBody((m, f, b) =>
        {
            var builder = new StructuredInstructionToAbstractSyntaxTreeBuilder(
                module,
                f,
                b.Root
            );
            return builder.Build();
        });
    }

    public static ShaderModuleDeclaration<CompoundStatement> Simplify(
        this ShaderModuleDeclaration<CompoundStatement> module
    )
    {
        return module.MapBody((m, f, b) =>
        {
            var stmt = b.AcceptVisitor(new AbstractSyntaxTreeSimplify());
            return (CompoundStatement)stmt;
        });
    }

    public static ShaderModuleDeclaration<ControlFlowGraphFunctionBody> ToControlFlowGraph(
        this ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> module
    )
    {
        return module.MapBody((m, f, b) =>
         {
             Dictionary<int, Label> labels = [];
             Dictionary<Label, int> labelIndex = [];
             foreach (var (idx, inst) in b.Instructions.Index())
             {
                 if (inst is LabelInstruction l)
                 {
                     labels.Add(idx, l.Label);
                     labelIndex.Add(l.Label, idx);
                 }
             }
             var builder = new ControlFlowGraphBuilder(b.Instructions.Length, (idx) =>
                labels.TryGetValue(idx, out var l) ? l : Label.FromIndex(idx));

             foreach (var (idx, inst) in b.Instructions.Index())
             {
                 switch (inst)
                 {
                     case BrInstruction br:
                         builder.AddBr(idx, labelIndex[br.Target]);
                         break;
                     case BrIfInstruction brIf:
                         builder.AddBrIf(idx, labelIndex[brIf.Target]);
                         break;
                     case ReturnInstruction:
                         builder.AddReturn(idx);
                         break;
                     default:
                         break;
                 }
             }

             var cfg = builder.Build((l, r) =>
             {
                 Debug.Assert(r.Count > 0);
                 var isFirstLabel = b.Instructions[r.Start] is LabelInstruction;
                 var structuredInstructionStart = isFirstLabel ? r.Start + 1 : r.Start;
                 var structuredInstructionCount = isFirstLabel ? r.Count - 1 : r.Count;
                 IEnumerable<IStructuredStackInstruction> body = [];
                 if (structuredInstructionCount > 0)
                 {
                     if (b.Instructions[r.Start + r.Count - 1] is IJumpInstruction)
                     {
                         structuredInstructionCount--;
                     }
                     body = b.Instructions.Slice(structuredInstructionStart, structuredInstructionCount)
                                          .Cast<IStructuredStackInstruction>();
                 }
                 //var body = b.Instructions.Slice(structuredInstructionStart, structuredInstructionCount)
                 //                               .Cast<IStructuredStackInstruction>();

                 return BasicBlock<IStructuredStackInstruction>.Create([.. body]);
             });
             return new ControlFlowGraphFunctionBody(cfg);
         });
    }

    public static async ValueTask<string> EmitWgslCode(
        this ShaderModuleDeclaration<CompoundStatement> module
    )
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        var typeVisitor = new WgslCodeTypeReferenceVisitor(isw);
        var bodyVisitor = new WgslFunctionBodyVisitor(isw); ;
        var visitor = new ModuleToCodeVisitor<CompoundStatement>(isw, module,
            bodyVisitor.VisitCompound);
        await module.Accept<ValueTask>(visitor);
        return sw.ToString();
    }



    public static async ValueTask<string> EmitWgslCode(
        this ISharpShader shader
    )
    {
        var module = shader.Parse()
                           .ReplaceOperationCallsToOperationInstruction()
                           .ToControlFlowGraph()
                           .ToStructuredControlFlowStackModel()
                           .ToAbstractSyntaxTreeFunctionBody();
        var code = await module.EmitWgslCode();
        return code;
    }

    static void Dump(this ISuccessor successor, Func<Label, string> labelName, IndentedTextWriter writer)
    {
        switch (successor)
        {
            case BrOrNextSuccessor s:
                writer.Write($"br {labelName(s.Target)}");
                break;
            case BrIfSuccessor s:
                writer.Write($"br_if {labelName(s.TrueTarget)} {labelName(s.FalseTarget)}");
                break;
            case ReturnOrTerminateSuccessor:
                writer.Write($"return");
                break;
            default:
                throw new NotSupportedException();
        }
    }

    static void Dump(this IStackInstruction instruction, Func<Label, string> labelName, Func<VariableDeclaration, string> variableName, IndentedTextWriter writer)
    {
        switch (instruction)
        {
            case LabelInstruction l:
                writer.WriteLine($"label {labelName(l.Label)}:");
                break;
            case BrInstruction br:
                writer.WriteLine($"br {labelName(br.Target)};");
                break;
            case BrIfInstruction brIf:
                writer.WriteLine($"brIf {labelName(brIf.Target)};");
                break;
            case ReturnInstruction:
                writer.WriteLine("return");
                break;
            case LoadSymbolInstruction<VariableDeclaration> inst:
                writer.WriteLine($"load {variableName(inst.Target)};");
                break;
            case LoadSymbolAddressInstruction<VariableDeclaration> inst:
                writer.WriteLine($"load.address {variableName(inst.Target)};");
                break;
            case StoreSymbolInstruction<VariableDeclaration> inst:
                writer.WriteLine($"store {variableName(inst.Target)};");
                break;
            default:
                writer.WriteLine(instruction);
                break;
        }
    }


    public static void Dump(
         this IStructuredControlFlowRegion<IStructuredStackInstruction> region,
         Func<Label, string> labelName,
         Func<VariableDeclaration, string> variableName,
         IndentedTextWriter writer)
    {
        switch (region)
        {
            case Loop<IStructuredStackInstruction> r:
                writer.WriteLine($"loop {labelName(r.Label)}:");
                using (writer.IndentedScope())
                {
                    r.Body.Dump(labelName, variableName, writer);
                }
                break;
            case IfThenElse<IStructuredStackInstruction> r:
                writer.WriteLine("if:");
                using (writer.IndentedScope())
                {
                    r.TrueBlock.Dump(labelName, variableName, writer);
                }
                writer.WriteLine("else:");
                using (writer.IndentedScope())
                {
                    r.FalseBlock.Dump(labelName, variableName, writer);
                }
                break;
            case Block<IStructuredStackInstruction> r:
                writer.WriteLine($"block {labelName(r.Label)}:");
                using (writer.IndentedScope())
                {
                    foreach (var e in r.Body)
                    {
                        switch (e)
                        {
                            case BasicBlock<IStructuredStackInstruction> bb:
                                using (writer.IndentedScope())
                                {
                                    foreach (var inst in bb.Instructions.Span)
                                    {
                                        inst.Dump(labelName, variableName, writer);
                                    }
                                }
                                break;
                            case IStructuredControlFlowRegion<IStructuredStackInstruction> re:
                                re.Dump(labelName, variableName, writer);
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                    }
                }
                break;
            default:
                throw new NotSupportedException();
        }
    }


    public static async ValueTask<string> Dump(this ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> module)
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        isw.Write(module.GetType().CSharpFullName());
        var visitor = new ModuleToCodeVisitor<StructuredStackInstructionFunctionBody>(isw, module, (b) =>
        {
            var instructions = b.Root.Instructions.ToArray();
            var labelIndex = b.Root.Labels.Distinct().ToArray().Index().ToDictionary(x => x.Item, x => x.Index);
            var variables = instructions.OfType<LoadSymbolInstruction<VariableDeclaration>>().Select(x => x.Target)
                           .Concat(instructions.OfType<StoreSymbolInstruction<VariableDeclaration>>().Select(x => x.Target))
                           .Concat(instructions.OfType<LoadSymbolAddressInstruction<VariableDeclaration>>().Select(x => x.Target))
                           .Where(v => v.DeclarationScope == DeclarationScope.Function)
                           .Distinct()
                           .Index()
                           .ToDictionary(x => x.Item, x => x.Index);
            string VariableName(VariableDeclaration variable) =>
                variable.DeclarationScope == DeclarationScope.Function
                ? $"var#{variables[variable]} {variable}"
                : $"module var {variable.Name}";


            string LabelName(Label label) => $"label#{labelIndex[label]} {label}";

            b.Root.Dump(LabelName, VariableName, isw);
            return ValueTask.CompletedTask;
        });
        await module.Accept(visitor);
        return sw.ToString();
    }

    public static async ValueTask<string> Dump(this ShaderModuleDeclaration<ControlFlowGraphFunctionBody> module)
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        isw.Write(module.GetType().CSharpFullName());
        var visitor = new ModuleToCodeVisitor<ControlFlowGraphFunctionBody>(isw, module, (b) =>
        {
            var instructions = b.Graph.Labels().SelectMany(l => b.Graph[l].Instructions.ToArray()).ToArray();

            var labelIndex = b.Graph.Labels().Index().ToDictionary(x => x.Item, x => x.Index);
            var variables = instructions.OfType<LoadSymbolInstruction<VariableDeclaration>>().Select(x => x.Target)
                           .Concat(instructions.OfType<StoreSymbolInstruction<VariableDeclaration>>().Select(x => x.Target))
                           .Concat(instructions.OfType<LoadSymbolAddressInstruction<VariableDeclaration>>().Select(x => x.Target))
                           .Where(v => v.DeclarationScope == DeclarationScope.Function)
                           .Distinct()
                           .Index()
                           .ToDictionary(x => x.Item, x => x.Index);

            string VariableName(VariableDeclaration variable) =>
                variable.DeclarationScope == DeclarationScope.Function
                ? $"var#{variables[variable]} {variable}"
                : $"module var {variable.Name}";


            string LabelName(Label label) => $"label#{labelIndex[label]} {label}";


            foreach (var (k, v) in variables)
            {
                isw.WriteLine($"var#{v} {k}");
            }
            isw.WriteLine();

            foreach (var l in b.Graph.Labels())
            {
                isw.Write($"{LabelName(l)} : -> ");
                b.Graph.Successor(l).Dump(LabelName, isw);
                isw.WriteLine();
                using (isw.IndentedScope())
                {
                    foreach (var instruction in b.Graph[l].Instructions.Span)
                    {
                        instruction.Dump(LabelName, VariableName, isw);
                    }
                }
                isw.WriteLine();
            }
            return ValueTask.CompletedTask;
        });
        await module.Accept(visitor);
        return sw.ToString();
    }
    public static async ValueTask<string> Dump(this ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> module)
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        isw.Write(module.GetType().CSharpFullName());
        var visitor = new ModuleToCodeVisitor<UnstructuredStackInstructionFunctionBody>(isw, module, (b) =>
        {
            var labelIndex = b.Instructions.OfType<LabelInstruction>()
                                       .Select(inst => inst.Label)
                                       .Distinct()
                                       .Index()
                                       .ToDictionary(x => x.Item, x => x.Index);
            var variables = b.Instructions.OfType<LoadSymbolInstruction<VariableDeclaration>>().Select(x => x.Target)
                           .Concat(b.Instructions.OfType<StoreSymbolInstruction<VariableDeclaration>>().Select(x => x.Target))
                           .Concat(b.Instructions.OfType<LoadSymbolAddressInstruction<VariableDeclaration>>().Select(x => x.Target))
                           .Where(v => v.DeclarationScope == DeclarationScope.Function)
                           .Distinct()
                           .Index()
                           .ToDictionary(x => x.Item, x => x.Index);

            string LabelName(Label label) => $"label#{labelIndex[label]} {label}";
            string VariableName(VariableDeclaration variable) =>
                variable.DeclarationScope == DeclarationScope.Function
                ? $"var#{variables[variable]} {variable}"
                : $"module var {variable.Name}";

            foreach (var (k, v) in variables)
            {
                isw.WriteLine($"var#{v} {k}");
            }
            isw.WriteLine();

            foreach (var instruction in b.Instructions)
            {
                instruction.Dump(LabelName, VariableName, isw);
                if (instruction is IJumpInstruction)
                {
                    isw.WriteLine();
                }
            }
            return ValueTask.CompletedTask;
        });
        await module.Accept(visitor);
        return sw.ToString();
    }

    public static async ValueTask Dump<TBody>(this ShaderModuleDeclaration<TBody> module, IndentedTextWriter writer)
        where TBody : IFunctionBody
    {
        writer.Write(module.GetType().CSharpFullName());
        var typeVisitor = new WgslCodeTypeReferenceVisitor(writer);
        var visitor = new ModuleToCodeVisitor<TBody>(writer, module, (b) =>
        {
            return ValueTask.CompletedTask;
        });
        await module.Accept(visitor);
    }

    public static async ValueTask<string> Dump<TBody>(this ShaderModuleDeclaration<TBody> module)
        where TBody : IFunctionBody
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        await module.Dump(isw);
        return sw.ToString();
    }
}
