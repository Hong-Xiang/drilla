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
using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using ICSharpCode.Decompiler.CSharp.Syntax;

namespace DualDrill.CLSL;

/// <summary>
/// Public interfaces for CLSL compiler/parser/reflection functionalities on shader module IR
/// </summary>
public static class ShaderModuleExtension
{
    public static ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IStackStatement>> Parse(
        this ISharpShader shader
    )
    {
        var context = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(context);
        return parser.ParseShaderModule(shader);
    }

    public static ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IStackStatement>> EliminateBlockValueTransfer(
        this ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IStackStatement>> module
    )
        => module.MapBody((_, _, cfg) =>
            cfg.MapBody((label, bb) =>
            {
                if (bb.Outputs.IsEmpty)
                {
                    return bb;
                }

                var pushStmt = bb.Elements.OfType<PushStatement>().SingleOrDefault();

                var successorLabels = cfg.Successor(label).AllTargets();

                var statements = new List<IStackStatement>();

                statements.AddRange(bb.Elements.Where(s => s is not PushStatement));

                foreach (var sl in successorLabels)
                {
                    var bt = cfg[sl];
                    foreach (var (vo, vi) in bb.Outputs.Zip(bt.Inputs))
                    {
                        statements.Add(SyntaxFactory.AssignStatement(
                            SyntaxFactory.VarIdentifier(vi),
                            SyntaxFactory.VarIdentifier(vo)
                        ));
                    }
                }

                if (pushStmt is { } s)
                {
                    statements.Add(s);
                }

                return new BasicBlock<IStackStatement>([..statements], bb.Inputs, []);
            })
        );

    public static ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IInstruction>>
        BasicBlockTransformStatementsToInstructions(
            this ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IStackStatement>> module
        )
        => module.MapBody((_, _, body) =>
            body.MapBody(bb =>
                BasicBlock<IInstruction>.Create([
                    ..bb.Elements.SelectMany(e => e.ToInstructions())
                ])));

    public static ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IInstruction>>
        ReplaceOperationCallsToOperationInstruction(
            this ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IInstruction>> ir
        )
    {
        return ir.MapBody((m, f, b) =>
            b.MapBody(bb =>
            {
                return BasicBlock<IInstruction>.Create([
                    ..bb.Elements.Select(inst =>
                    {
                        if (inst is CallInstruction call)
                        {
                            if (call.Callee.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is
                                { } attr)
                            {
                                return attr.Operation.Instruction;
                            }
                        }

                        return inst;
                    })
                ]);
            }));
    }

    public static ShaderModuleDeclaration<FunctionBody<CompoundStatement>> ToAbstractSyntaxTreeFunctionBody(
        this ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> module
    )
    {
        return module.MapBody((m, f, b) =>
        {
            var builder = new StructuredInstructionToAbstractSyntaxTreeBuilder(
                module,
                f);
            return new FunctionBody<CompoundStatement>(builder.Build());
        });
    }

    public static ShaderModuleDeclaration<FunctionBody<CompoundStatement>> Simplify(
        this ShaderModuleDeclaration<FunctionBody<CompoundStatement>> module
    )
    {
        return module.MapBody((m, f, b) =>
        {
            var stmt = b.Body.AcceptVisitor(new AbstractSyntaxTreeSimplify());
            return new FunctionBody<CompoundStatement>((CompoundStatement)stmt);
        });
    }

    public static ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> Simplify(
        this ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> module
    )
    {
        // TODO: proper handling multiple run of simplify
        for (int i = 0; i < 4; i++)
        {
            module = module.Simplify1();
        }

        return module;
    }

    public static ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> Simplify1(
        this ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> module
    )
    {
        return module.MapBody((m, f, fBody) =>
        {
            var result = fBody.Root;
            var counter = new StructuredControlFlowLocalReferencesUsageCounter();
            _ = result.Accept(counter);
            var vSt = counter.VariableStoreCount.ToFrozenDictionary();
            var vLd = counter.VariableLoadCount.ToFrozenDictionary();

            var simplifer = new StructuredControlFlowSimplifier(vLd, vSt,
                counter.DirectJumpLabels.ToFrozenSet(),
                counter.NestedJumpLabels.ToFrozenSet()
            );
            var elements = result.Accept(simplifer).ToImmutableArray();
            result = elements switch
            {
                [Block r] => r,
                [Loop r] => r,
                [IfThenElse r] => r,
                _ => new Block(Label.Create(), new(elements))
            };

            return new StructuredStackInstructionFunctionBody(result);
        });
    }


    public static async ValueTask<string> EmitWgslCode(
        this ShaderModuleDeclaration<FunctionBody<CompoundStatement>> module
    )
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        var visitor = new ModuleToCodeVisitor<FunctionBody<CompoundStatement>>(isw, module,
            b => new WgslFunctionBodyVisitor(b, isw).VisitCompound(b.Body));
        await module.Accept<ValueTask>(visitor);
        return sw.ToString();
    }


    public static async ValueTask<string> EmitWgslCode(
        this ISharpShader shader
    )
    {
        var module = shader.Parse()
                           .BasicBlockTransformStatementsToInstructions()
                           .ReplaceOperationCallsToOperationInstruction()
                           .ToStructuredControlFlowStackModel()
                           .ToAbstractSyntaxTreeFunctionBody();
        var code = await module.EmitWgslCode();
        return code;
    }

    static void Dump(this IInstruction instruction, Func<Label, string> labelName,
        Func<VariableDeclaration, string> variableName, IndentedTextWriter writer)
    {
        switch (instruction)
        {
            case BrInstruction br:
                writer.WriteLine($"br {labelName(br.Target)};");
                break;
            case BrIfInstruction brIf:
                writer.WriteLine($"brIf {labelName(brIf.Target)};");
                break;
            case ReturnInstruction:
                writer.WriteLine("return");
                break;
            case LoadSymbolValueInstruction<VariableDeclaration> inst:
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
        this StructuredControlFlowElementSequence sequence,
        Func<Label, string> labelName,
        Func<VariableDeclaration, string> variableName,
        IndentedTextWriter writer
    )
    {
        foreach (var inst in sequence.Elements)
        {
            switch (inst)
            {
                case IInstruction stackInstruction:
                    stackInstruction.Dump(labelName, variableName, writer);
                    break;
                case IStructuredControlFlowRegion region:
                    region.Dump(labelName, variableName, writer);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public static void Dump(
        this IStructuredControlFlowRegion region,
        Func<Label, string> labelName,
        Func<VariableDeclaration, string> variableName,
        IndentedTextWriter writer)
    {
        switch (region)
        {
            case Loop r:
                writer.WriteLine($"loop {labelName(r.Label)}:");
                using (writer.IndentedScope())
                {
                    r.Body.Dump(labelName, variableName, writer);
                }

                break;
            case IfThenElse r:
                writer.WriteLine("if:");
                using (writer.IndentedScope())
                {
                    r.TrueBody.Dump(labelName, variableName, writer);
                }

                writer.WriteLine("else:");
                using (writer.IndentedScope())
                {
                    r.FalseBody.Dump(labelName, variableName, writer);
                }

                break;
            case Block r:
                writer.WriteLine($"block {labelName(r.Label)}:");
                using (writer.IndentedScope())
                {
                    r.Body.Dump(labelName, variableName, writer);
                }

                break;
            default:
                throw new NotSupportedException();
        }
    }


    // public static async ValueTask<string> Dump(
    //     this ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> module)
    // {
    //     var sw = new StringWriter();
    //     var isw = new IndentedTextWriter(sw);
    //     isw.Write(module.GetType().CSharpFullName());
    //     var visitor = new ModuleToCodeVisitor<StructuredStackInstructionFunctionBody>(isw, module, (b) =>
    //     {
    //         var labelIndex = b.Root.ReferencedLabels.Distinct().ToArray().Index()
    //                           .ToDictionary(x => x.Item, x => x.Index);
    //         var variables = b.Root.ReferencedLocalVariables.Distinct().Index()
    //                          .ToDictionary(x => x.Item, x => x.Index);
    //
    //         string VariableName(VariableDeclaration variable) =>
    //             variable.DeclarationScope == DeclarationScope.Function
    //                 ? $"var#{variables[variable]} {variable}"
    //                 : $"module var {variable.Name}";
    //
    //
    //         string LabelName(Label label) => $"label#{labelIndex[label]} {label}";
    //
    //         b.Root.Dump(LabelName, VariableName, isw);
    //         return ValueTask.CompletedTask;
    //     });
    //     await module.Accept(visitor);
    //     return sw.ToString();
    // }

    // public static async ValueTask<string> Dump(
    //     this ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IInstruction>> module)
    // {
    //     var sw = new StringWriter();
    //     var isw = new IndentedTextWriter(sw);
    //     isw.Write(module.GetType().CSharpFullName());
    //     var visitor = new ModuleToCodeVisitor<ControlFlowGraphFunctionBody<IInstruction>>(isw,
    //         module, (b) =>
    //         {
    //             var instructions = b.Labels.SelectMany(l => b[l].Elements.ToArray()).ToArray();
    //
    //             var labelIndex = b.Labels.Index().ToDictionary(x => x.Item, x => x.Index);
    //             var variables = instructions.OfType<LoadSymbolValueInstruction<VariableDeclaration>>()
    //                                         .Select(x => x.Target)
    //                                         .Concat(instructions.OfType<StoreSymbolInstruction<VariableDeclaration>>()
    //                                                             .Select(x => x.Target))
    //                                         .Concat(instructions
    //                                                 .OfType<LoadSymbolAddressInstruction<VariableDeclaration>>()
    //                                                 .Select(x => x.Target))
    //                                         .Where(v => v.DeclarationScope == DeclarationScope.Function)
    //                                         .Distinct()
    //                                         .Index()
    //                                         .ToDictionary(x => x.Item, x => x.Index);
    //
    //             string VariableName(VariableDeclaration variable) =>
    //                 variable.DeclarationScope == DeclarationScope.Function
    //                     ? $"var#{variables[variable]} {variable}"
    //                     : $"module var {variable.Name}";
    //
    //
    //             string LabelName(Label label) => $"label#{labelIndex[label]} {label}";
    //
    //
    //             foreach (var (k, v) in variables)
    //             {
    //                 isw.WriteLine($"var#{v} {k}");
    //             }
    //
    //             isw.WriteLine();
    //
    //             foreach (var l in b.Labels)
    //             {
    //                 isw.Write($"{LabelName(l)} : -> ");
    //                 b.Successor(l).Dump(LabelName, isw);
    //                 isw.WriteLine();
    //                 using (isw.IndentedScope())
    //                 {
    //                     foreach (var instruction in b[l].Elements)
    //                     {
    //                         instruction.Dump(LabelName, VariableName, isw);
    //                     }
    //                 }
    //
    //                 isw.WriteLine();
    //             }
    //
    //             return ValueTask.CompletedTask;
    //         });
    //     await module.Accept(visitor);
    //     return sw.ToString();
    // }


    public static async ValueTask Dump<TBody>(this ShaderModuleDeclaration<TBody> module, IndentedTextWriter writer)
        where TBody : IFunctionBody
    {
        writer.WriteLine(module.GetType().CSharpFullName());
        var visitor = new ModuleToCodeVisitor<TBody>(writer, module, (body) =>
        {
            using (writer.IndentedScope())
            {
                body.Dump(writer);
            }

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