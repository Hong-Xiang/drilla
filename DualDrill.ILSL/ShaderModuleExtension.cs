using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL;

/// <summary>
/// Public interfaces for CLSL compiler/parser/reflection functionalities on shader module IR
/// </summary>
public static class ShaderModuleExtension
{
    public static ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>> Parse(
        this ISharpShader shader
    )
    {
        var context = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(context);
        //return parser.ParseShaderModule(shader);
        throw new NotImplementedException();
    }

    public static ShaderModuleDeclaration<FunctionBody4> Parse4(
        this ISharpShader shader
    )
    {
        var context = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(context);
        return parser.ParseShaderModule(shader);
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
        var module = shader.Parse();


        ShaderModuleDeclaration<FunctionBody<CompoundStatement>> ast = null;
        // var module = shader.Parse()
        //                    .EliminateBlockValueTransfer()
        //                    .BasicBlockTransformStatementsToInstructions()
        //                    .ReplaceOperationCallsToOperationInstruction()
        //                    .ToStructuredControlFlowStackModel()
        //                    .Simplify()
        //                    .ToAbstractSyntaxTreeFunctionBody()
        //                    .Simplify();

        ast = ast.Simplify();
        var code = await ast.EmitWgslCode();
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
                writer.WriteLine($"brIf {labelName(brIf.TrueTarget)};");
                break;
            case ReturnResultStackInstruction:
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