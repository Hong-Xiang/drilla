using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Compiler;
using DualDrill.ILSL.Frontend;
using System.Collections.Immutable;

namespace DualDrill.ILSL;

public interface ICLSLCompiler
{
    public ShaderModuleDeclaration Reflect(ISharpShader shader);
    public ShaderModuleDeclaration Compile(ISharpShader shader);
    public ValueTask<string> EmitWGSL(ISharpShader module);
}

public sealed class CLSLCompiler() : ICLSLCompiler
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
        var metadataParser = new RuntimeReflectionShaderModuleMetadataParser(Context);
        var metadataModule = metadataParser.ParseShaderModule(shader);

        Dictionary<FunctionDeclaration, IFunctionBody> methodDefinitions = [];

        var methodParser = new RuntimeReflectionMethodBodyParser(Context);
        foreach (var (decl, method) in metadataModule.FunctionDefinitions)
        {
            methodDefinitions[decl] = methodParser.Parse(decl);
        }

        return metadataModule with
        {
            FunctionDefinitions = methodDefinitions.ToImmutableDictionary()
        };
    }
}
