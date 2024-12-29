using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.ILSL.Compiler;

public interface IMethodBodyPass
{
    CompilationContext Context { get; }
    ShaderModuleCompilation ShaderModuleCompilation { get; }
    IFunctionBody Compile(IFunctionBody compilation);
}


