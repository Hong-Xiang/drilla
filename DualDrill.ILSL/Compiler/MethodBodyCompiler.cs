using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.ILSL.Compiler;

public sealed class MethodBodyCompiler(
   ICompilationContext Context,
   IReadOnlyList<Func<ICompilationContext, MethodBodyCompilation, IMethodBodyPass>> MethodPassFactories
)
{
    public IFunctionBody Compile(MethodBodyCompilation compilation)
    {
        IFunctionBody result = new NotParsedFunctionBody(compilation.Method);
        foreach (var createPass in MethodPassFactories)
        {
            var pass = createPass(Context, compilation);
            result = pass.Compile(result);
        }
        return result;
    }
}
