using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Backend;
using System.CodeDom.Compiler;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using ICSharpCode.Decompiler.IL;

namespace DualDrill.CLSL;

public interface ICLSLCompiler
{
    public ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>> Reflect(ISharpShader shader);
    public ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>> Compile(ISharpShader shader);
    public ValueTask<string> EmitWGSL(ISharpShader module);
}

public sealed class CLSLCompiler() : ICLSLCompiler
{
    ISymbolTable Context = CompilationContext.Create();

    public async ValueTask<string> EmitWGSL(ISharpShader shader)
    {
        return await shader.EmitWgslCode();
    }

    // TODO: reflect should not parse function body
    public ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>> Reflect(ISharpShader shader)
    {
        var parser = new RuntimeReflectionParser(Context);
        return parser.ParseShaderModule(shader);
    }

    public ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>> Compile(ISharpShader shader)
    {
        var parser = new RuntimeReflectionParser(Context);
        // var module = parser.ParseShaderModule(shader)
        //                    .BasicBlockTransformStatementsToInstructions()
        //                    .ReplaceOperationCallsToOperationInstruction();
        // return module.ToStructuredControlFlowStackModel();
        throw new NotImplementedException();
    }
}