using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;

namespace DualDrill.CLSL;

public interface ICLSLCompiler
{
    public ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>> Reflect(ISharpShader shader);
    public ShaderModuleDeclaration<FunctionBody4> Compile(ISharpShader shader);
    public ValueTask<string> EmitWGSL(ISharpShader module);
    public string EmitSlang(ISharpShader module);
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
        var module = parser.ParseShaderModule(shader);
        throw new NotImplementedException();
    }

    public ShaderModuleDeclaration<FunctionBody4> Compile(ISharpShader shader)
    {
        var parser = new RuntimeReflectionParser(Context);
        // var module = parser.ParseShaderModule(shader)
        //                    .BasicBlockTransformStatementsToInstructions()
        //                    .ReplaceOperationCallsToOperationInstruction();
        // return module.ToStructuredControlFlowStackModel();
        throw new NotImplementedException();
    }

    public string EmitSlang(ISharpShader module)
    {
        var parser = new RuntimeReflectionParser(Context);
        var shaderModule = parser.ParseShaderModule(module);
        return string.Empty;
    }
}