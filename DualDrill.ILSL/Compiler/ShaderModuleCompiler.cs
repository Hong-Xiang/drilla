using DualDrill.CLSL.Language.Declaration;
using Lokad.ILPack.IL;
using System.Reflection;

namespace DualDrill.ILSL.Compiler;

public sealed class ShaderModuleCompiler(
   CompilationContext Context,
   IReadOnlyList<Func<CompilationContext, ShaderModuleCompilation, IShaderModulePass>> ModulePassFactories,
   IReadOnlyList<Func<CompilationContext, MethodBodyCompilation, IMethodBodyPass>> MethodPassFactories
)
{
    public ShaderModuleDeclaration Compile(ShaderModuleCompilation compilation)
    {
        // TODO: property use parser
        var result = ShaderModuleDeclaration.Empty;
        foreach (var createPass in ModulePassFactories)
        {
            var pass = createPass(Context, compilation);
            result = pass.Run(result);
        }
        return result;
    }
}
