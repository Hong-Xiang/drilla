using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Compiler;

namespace DualDrill.ILSL;

public interface ICLSLService
{
    public ShaderModuleDeclaration Parse(ISharpShader shader);
    public ShaderModuleDeclaration Compile(ISharpShader shader);
    public ValueTask<string> EmitWGSL(ISharpShader module);
}

public sealed class CLSLService() : ICLSLService
{
    CompilationContext Context = CompilationContext.Create();
    IReadOnlyList<Func<CompilationContext, ShaderModuleCompilation, IShaderModulePass>> ModulePassFactories = [];
    IReadOnlyList<Func<CompilationContext, MethodBodyCompilation, IMethodBodyPass>> MethodPassFactories = [];

    public async ValueTask<string> EmitWGSL(ISharpShader shader)
    {
        var module = Compile(shader);
        var sw = new IndentStringWriter("    ");
        var emitter = new ModuleToCodeVisitor(sw);
        await module.AcceptVisitor(emitter);
        return sw.ToString();
    }

    public ShaderModuleDeclaration Parse(ISharpShader shader)
    {
        var compiler = new ShaderModuleCompiler(
            Context,
            ModulePassFactories,
            MethodPassFactories
        );
        return compiler.Compile(new(shader));
    }

    public ShaderModuleDeclaration Compile(ISharpShader shader)
    {
        var metadataDecl = Parse(shader);
        var methodBodies = new List<KeyValuePair<FunctionDeclaration, IFunctionBody>>();
        var methodCompiler = new MethodBodyCompiler(Context, MethodPassFactories);
        foreach (var (f, _) in metadataDecl.FunctionDefinitions)
        {
            var method = Context.FunctionDefinitions[f];
            var methodCompilation = new MethodBodyCompilation(shader, method, method.GetMethodBody(), method.GetInstructions());
            var bodyResult = methodCompiler.Compile(methodCompilation);
            methodBodies.Add(KeyValuePair.Create(f, bodyResult));
        }
        return metadataDecl with
        {
            FunctionDefinitions = metadataDecl.FunctionDefinitions.SetItems(methodBodies)
        };
    }
}
