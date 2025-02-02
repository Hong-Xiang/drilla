using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Backend;

namespace DualDrill.CLSL;

public interface ICLSLCompiler
{
    public ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> Reflect(ISharpShader shader);
    public ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> Compile(ISharpShader shader);
    public ValueTask<string> EmitWGSL(ISharpShader module);
}

public sealed class CLSLCompiler() : ICLSLCompiler
{
    ICompilationContext Context = CompilationContext.Create();

    public async ValueTask<string> EmitWGSL(ISharpShader shader)
    {
        var module = Compile(shader);
        var sw = new IndentStringWriter("    ");
        var emitter = new ModuleToCodeVisitor<StructuredStackInstructionFunctionBody>(sw, module);
        await module.AcceptVisitor(emitter);
        return sw.ToString();
    }

    public ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> Reflect(ISharpShader shader)
    {
        var parser = new RuntimeReflectionParser(Context);
        return parser.ParseShaderModule(shader);
    }

    public ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> Compile(ISharpShader shader)
    {
        var parser = new RuntimeReflectionParser(Context);
        var module = parser.ParseShaderModule(shader);
        return module.ToControlFlowGraph().ToStructuredControlFlowStackModel();

    }
}
