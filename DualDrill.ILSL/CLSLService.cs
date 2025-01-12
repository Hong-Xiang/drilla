using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Compiler;
using DualDrill.ILSL.Frontend;

namespace DualDrill.ILSL;

public interface ICLSLService
{
    public ShaderModuleDeclaration Reflect(ISharpShader shader);
    public ShaderModuleDeclaration Compile(ISharpShader shader);
    public ValueTask<string> EmitWGSL(ISharpShader module);
}

public sealed class CLSLService() : ICLSLService
{
    ICompilationContext Context = CompilationContext.Create();
    IReadOnlyList<Func<ICompilationContext, ShaderModuleCompilation, IShaderModulePass>> ModulePassFactories = [];
    IReadOnlyList<Func<ICompilationContext, MethodBodyCompilation, IMethodBodyPass>> MethodPassFactories = [];

    public async ValueTask<string> EmitWGSL(ISharpShader shader)
    {
        var module = Compile(shader);
        var sw = new IndentStringWriter("    ");
        var emitter = new ModuleToCodeVisitor(sw);
        await module.AcceptVisitor(emitter);
        return sw.ToString();
    }

    public ShaderModuleDeclaration Reflect(ISharpShader shader)
    {
        var parser = new RuntimeReflectionShaderModuleMetadataParser(Context);
        return parser.ParseShaderModule(shader);
    }

    public ShaderModuleDeclaration Compile(ISharpShader shader)
    {
        var compiler = new ShaderModuleCompiler(
           ModulePassFactories,
           MethodPassFactories
       );
        return compiler.Compile(new(Context, shader));
    }
}
