using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Frontend;
using Lokad.ILPack.IL;

namespace DualDrill.ILSL.Compiler;

public sealed class ShaderModuleCompiler(
   IReadOnlyList<Func<ICompilationContext, ShaderModuleCompilation, IShaderModulePass>> ModulePassFactories,
   IReadOnlyList<Func<ICompilationContext, MethodBodyCompilation, IMethodBodyPass>> MethodPassFactories
)
{
    public ShaderModuleDeclaration Compile(ShaderModuleCompilation compilation)
    {
        var parser = new RuntimeReflectionShaderModuleMetadataParser(compilation.Context);
        // TODO: property use parser
        var result = parser.ParseShaderModule(compilation.Shader);
        foreach (var createPass in ModulePassFactories)
        {
            var pass = createPass(compilation.Context, compilation);
            result = pass.Run(result);
        }
        var methodBodies = new List<KeyValuePair<FunctionDeclaration, IFunctionBody>>();
        // TODO: add parameter etc context, and use new 
        var methodContext = new CompilationContext(compilation.Context);
        var methodCompiler = new MethodBodyCompiler(methodContext, MethodPassFactories);
        foreach (var (f, _) in result.FunctionDefinitions)
        {
            var method = compilation.Context.GetFunctionDefinition(f);
            var methodCompilation = MethodBodyCompilation.Create(compilation, method);
            var bodyResult = methodCompiler.Compile(methodCompilation);
            methodBodies.Add(KeyValuePair.Create(f, bodyResult));
        }
        result = result with
        {
            FunctionDefinitions = result.FunctionDefinitions.SetItems(methodBodies)
        };
        return result;
    }
}
