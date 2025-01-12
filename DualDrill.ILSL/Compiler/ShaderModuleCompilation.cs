using DualDrill.ILSL.Frontend;

namespace DualDrill.ILSL.Compiler;

public sealed record class ShaderModuleCompilation(
    ICompilationContext Context,
    ISharpShader Shader
)
{
    public static ShaderModuleCompilation Create(ISharpShader shader)
    {
        return new(CompilationContext.Create(), shader);
    }
}
