using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.ILSL.Compiler;

public interface IMethodBodyPass
{
    CompilationContext Context { get; }
    MethodBodyCompilation Compilation { get; }
    IFunctionBody Compile(IFunctionBody compilation);
}


