using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Frontend;
using Lokad.ILPack.IL;
using System.Reflection;

namespace DualDrill.ILSL.Compiler;

public sealed class ShaderModuleCompiler(
   IReadOnlyList<Func<CompilationContext, ShaderModuleCompilation, IShaderModulePass>> ModulePassFactories,
   IReadOnlyList<Func<CompilationContext, MethodBodyCompilation, IMethodBodyPass>> MethodPassFactories
)
{
    public ShaderModuleDeclaration Compile(ShaderModuleCompilation compilation)
    {
        var parser = new ShaderModuleParser(compilation.Context);
        // TODO: property use parser
        var result = parser.ParseShaderModule(compilation.Shader);
        foreach (var createPass in ModulePassFactories)
        {
            var pass = createPass(compilation.Context, compilation);
            result = pass.Run(result);
        }
        return result;
    }
}
