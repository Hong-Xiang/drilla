using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.ILSL.Compiler;

public sealed class MethodBodyCompiler(
   CompilationContext Context,
   IReadOnlyList<Func<CompilationContext, MethodBodyCompilation, IMethodBodyPass>> MethodPassFactories
)
{
    public IFunctionBody Compile(MethodBodyCompilation compilation)
    {
        IFunctionBody result = new EmptyFunctionBody();
        foreach (var createPass in MethodPassFactories)
        {
            var pass = createPass(Context, compilation);
            result = pass.Compile(result);
        }
        return result;
    }
}
