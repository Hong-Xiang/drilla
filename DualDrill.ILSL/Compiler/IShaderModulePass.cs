using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.ILSL.Compiler;

public interface IShaderModulePass
{
    CompilationContext Context { get; }
    ShaderModuleCompilation Compilation { get; }
    ShaderModuleDeclaration Run(ShaderModuleDeclaration shaderModule);
}


