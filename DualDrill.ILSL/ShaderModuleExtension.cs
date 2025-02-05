using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;

namespace DualDrill.CLSL;

/// <summary>
/// Public interfaces for CLSL compiler/parser/reflection functionalities on shader module IR
/// </summary>
public static class ShaderModuleExtension
{
    public static string Dump(this IShaderModuleDeclaration shader)
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        shader.Dump(isw);
        return sw.ToString();
    }
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
                 var body = b.Instructions.Slice(structuredInstructionStart, structuredInstructionCount)
                                          .Cast<IStructuredStackInstruction>();
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
        var isw = new IndentStringWriter("\t");
        var visitor = new ModuleToCodeVisitor<CompoundStatement>(isw, module);
        await module.AcceptVisitor(visitor);
        return isw.ToString();
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
}
